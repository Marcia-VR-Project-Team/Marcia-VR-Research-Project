"""
Turn raw SENSE session logs into analysis-ready tables.

Run it from the repository root, with no arguments, and it finds the logs
itself:

    cd <repo root>
    py Analysis/combine_session_logs.py

On Windows use "py", not "python": a default Windows install routes
"python" to an App Execution Alias that opens the Microsoft Store instead
of running anything. On macOS and Linux use python3.

Or pass a folder, which is what logs pulled off a Quest need:

    py Analysis/combine_session_logs.py ./sessions

It looks in the current user's AppData by default, so it only finds
sessions recorded on the machine you run it from.

Outputs, written next to the logs in an "analysis" subfolder:

    <session>_combined.csv    one row per sample, with context forward-filled
    all_sessions.csv          every session stacked, for cross-participant work
    sessions_summary.csv      one row per session: is this session usable?

Standard library only, so it runs anywhere Python does without installing
anything. No pandas.

Why these three outputs:

* The combined file answers "where was the participant looking, and what was
  happening at that moment" without a manual join. The logger writes events and
  poses interleaved but on separate rows; analysis almost always wants the
  context attached to the pose row.
* The summary file answers "did this session actually work". For a study where
  a bad session means a participant who cannot be re-run, that question matters
  more than convenience columns, and it is the one nobody remembers to ask
  until the data is being analysed weeks later.
"""

import csv
import io
import math
import os
import re
import sys
from collections import Counter, defaultdict

# Where Unity writes the logs: Application.persistentDataPath on Windows.
DEFAULT_LOG_DIR = os.path.join(
    os.path.expanduser("~"), "AppData", "LocalLow", "VR Project", "SENSE")

# Columns carried straight through from the tracking file.
PASSTHROUGH = ["utc_iso", "unix_ms", "unity_time", "event", "target",
               "pos_x", "pos_y", "pos_z", "rot_x", "rot_y", "rot_z", "rot_w",
               "detail"]

# Columns this script adds.
DERIVED = ["session_id", "participant_id", "mode",
           "t_session_s", "timeline_clip", "timeline_track",
           "last_event", "since_event_s",
           "yaw_deg", "pitch_deg", "speed_mps"]


def read_meta(path):
    """Parse a session's _meta.txt into a dict. Returns {} if absent."""
    meta = {}
    if not os.path.exists(path):
        return meta

    for line in io.open(path, encoding="utf-8"):
        line = line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue
        key, value = line.split(":", 1)
        meta[key.strip()] = value.strip()

    return meta


def parse_detail(detail):
    """Turn 'a=1; b=2' into {'a': '1', 'b': '2'}."""
    out = {}
    for part in (detail or "").split(";"):
        if "=" in part:
            key, value = part.split("=", 1)
            out[key.strip()] = value.strip()
    return out


def to_float(text):
    try:
        return float(text)
    except (TypeError, ValueError):
        return None


def yaw_pitch(x, y, z, w):
    """
    Quaternion to yaw/pitch in degrees.

    Quaternions are correct to log but unreadable to analyse: you cannot look at
    a column of them and see where someone turned. Yaw and pitch are what a
    head-direction analysis actually uses. Roll is omitted because it carries
    almost no information for a seated participant.
    """
    if None in (x, y, z, w):
        return "", ""

    yaw = math.degrees(math.atan2(2.0 * (w * y + x * z),
                                  1.0 - 2.0 * (y * y + z * z)))

    # Clamped because floating point can push this a hair outside [-1, 1],
    # which would make asin throw on an otherwise valid sample.
    sin_pitch = max(-1.0, min(1.0, 2.0 * (w * x - y * z)))
    pitch = math.degrees(math.asin(sin_pitch))

    return round(yaw, 3), round(pitch, 3)


def combine_session(tracking_path, out_dir):
    """
    Build one <session>_combined.csv. Returns a summary dict for that session.
    """
    base = tracking_path[:-len("_tracking.csv")]
    session_id = os.path.basename(base).replace("session_", "")
    meta = read_meta(base + "_meta.txt")

    rows = list(csv.DictReader(io.open(tracking_path, encoding="utf-8-sig")))
    if not rows:
        return {"session_id": session_id, "usable": "NO", "note": "empty file"}

    participant = meta.get("participant_id", "")
    session_start_ms = to_float(meta.get("session_start_unix_ms")) or \
        to_float(rows[0].get("unix_ms")) or 0.0

    mode = ""

    # Forward-filled state.
    clip_name = ""
    clip_track = ""
    clip_ends_at = None          # unix_ms when the current clip finishes
    last_event = ""
    last_event_ms = None

    # Previous pose per target, for speed.
    prev_pose = {}

    event_counts = Counter()
    method_counts = Counter()
    device_counts = Counter()
    target_counts = Counter()
    warnings = []

    out_path = os.path.join(out_dir, os.path.basename(base) + "_combined.csv")
    out_file = io.open(out_path, "w", encoding="utf-8", newline="")
    writer = csv.DictWriter(out_file, fieldnames=PASSTHROUGH + DERIVED)
    writer.writeheader()

    for row in rows:
        now_ms = to_float(row.get("unix_ms"))
        event = row.get("event") or ""
        detail = parse_detail(row.get("detail"))

        if event:
            event_counts[event] += 1
            last_event = event
            last_event_ms = now_ms

            if event == "BUILD_MODE":
                mode = detail.get("mode", "")
            elif event == "SESSION_START" and not participant:
                participant = detail.get("participant", "")
            elif event == "TIMELINE_CLIP_START":
                clip_name = row.get("target", "")
                clip_track = detail.get("track", "")
                duration = to_float(detail.get("duration"))
                clip_ends_at = (now_ms + duration * 1000.0) \
                    if (duration is not None and now_ms is not None) else None
            elif event.startswith("INTERACTION_SELECT"):
                method_counts[detail.get("method", "?")] += 1
            elif "PRESS" in event:
                device_counts[(detail.get("device", "?"),
                               detail.get("id", "?"))] += 1
            elif event in ("TRACKING_TARGET_MISSING", "TRACKING_TARGET_AMBIGUOUS"):
                warnings.append("%s (%s)" % (event, row.get("detail", "")))

        # A clip stops being current once its duration has elapsed. Without
        # this, a 40-second clip would appear to still be playing minutes later.
        if clip_ends_at is not None and now_ms is not None and now_ms > clip_ends_at:
            clip_name, clip_track, clip_ends_at = "", "", None

        out = {k: row.get(k, "") for k in PASSTHROUGH}
        out["session_id"] = session_id
        out["participant_id"] = participant
        out["mode"] = mode
        out["t_session_s"] = round((now_ms - session_start_ms) / 1000.0, 3) \
            if now_ms is not None else ""
        out["timeline_clip"] = clip_name
        out["timeline_track"] = clip_track
        out["last_event"] = last_event
        out["since_event_s"] = round((now_ms - last_event_ms) / 1000.0, 3) \
            if (now_ms is not None and last_event_ms is not None) else ""

        target = row.get("target", "")
        if not event and target:
            target_counts[target] += 1

            x, y, z = (to_float(row.get("pos_x")), to_float(row.get("pos_y")),
                       to_float(row.get("pos_z")))
            qx, qy = to_float(row.get("rot_x")), to_float(row.get("rot_y"))
            qz, qw = to_float(row.get("rot_z")), to_float(row.get("rot_w"))

            out["yaw_deg"], out["pitch_deg"] = yaw_pitch(qx, qy, qz, qw)

            if None not in (x, y, z) and now_ms is not None:
                before = prev_pose.get(target)
                if before:
                    px, py, pz, pms = before
                    dt = (now_ms - pms) / 1000.0
                    if dt > 0:
                        dist = math.sqrt((x - px) ** 2 + (y - py) ** 2 + (z - pz) ** 2)
                        out["speed_mps"] = round(dist / dt, 4)
                prev_pose[target] = (x, y, z, now_ms)

        writer.writerow(out)

    out_file.close()

    first_ms = to_float(rows[0].get("unix_ms")) or 0.0
    last_ms = to_float(rows[-1].get("unix_ms")) or first_ms
    wall_s = (last_ms - first_ms) / 1000.0

    unity_times = [to_float(r.get("unity_time")) for r in rows]
    unity_times = [u for u in unity_times if u is not None]
    game_s = (max(unity_times) - min(unity_times)) if unity_times else 0.0

    # Game time should track wall clock closely. When it does not, the editor
    # was stalled or paused and the session is not what it appears to be -- a
    # run that looked like five seconds of data was really a third of a second.
    # This is the single most useful number for spotting a session that should
    # be discarded.
    time_ratio = (game_s / wall_s) if wall_s > 0 else 0.0

    usable = "yes"
    notes = []
    if wall_s < 10:
        usable, _ = "NO", notes.append("session shorter than 10s")
    if time_ratio < 0.8 and wall_s > 0:
        usable = "CHECK"
        notes.append("game time only %.0f%% of wall clock - editor stalled?" % (time_ratio * 100))
    if not event_counts.get("BUILD_MODE"):
        notes.append("no BUILD_MODE row")
    if not event_counts.get("SESSION_END"):
        usable = "CHECK"
        notes.append("no SESSION_END - session may have crashed")
    if not event_counts.get("SYNC_MARKER"):
        notes.append("no sync marker - Empatica alignment unverified")
    if warnings:
        usable = "CHECK"
        notes.extend(warnings)

    return {
        "session_id": session_id,
        "participant_id": participant,
        "mode": mode,
        "start_utc": meta.get("session_start_utc", rows[0].get("utc_iso", "")),
        "duration_s": round(wall_s, 1),
        "game_time_s": round(game_s, 1),
        "time_ratio": round(time_ratio, 3),
        "rows": len(rows),
        "events": sum(event_counts.values()),
        "pose_samples": sum(target_counts.values()),
        "targets": " ".join("%s=%d" % (k, v) for k, v in sorted(target_counts.items())),
        "timeline_clips": event_counts.get("TIMELINE_CLIP_START", 0),
        "timeline_signals": event_counts.get("TIMELINE_SIGNAL", 0),
        "interactions": sum(method_counts.values()),
        "methods": " ".join("%s=%d" % (k, v) for k, v in sorted(method_counts.items())),
        "input_devices": " ".join("%s(id%s)=%d" % (d, i, n)
                                  for (d, i), n in sorted(device_counts.items())),
        "sync_markers": event_counts.get("SYNC_MARKER", 0),
        "usable": usable,
        "notes": "; ".join(notes),
    }


def main():
    log_dir = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_LOG_DIR

    if not os.path.isdir(log_dir):
        print("Log folder not found: %s" % log_dir)
        print("Pass it explicitly:  python combine_session_logs.py <folder>")
        return 1

    tracking = sorted(os.path.join(log_dir, f) for f in os.listdir(log_dir)
                      if f.endswith("_tracking.csv"))

    if not tracking:
        print("No *_tracking.csv files in %s" % log_dir)
        return 1

    out_dir = os.path.join(log_dir, "analysis")
    if not os.path.isdir(out_dir):
        os.makedirs(out_dir)

    summaries = []
    for path in tracking:
        summary = combine_session(path, out_dir)
        summaries.append(summary)
        print("  %-26s %-7s %6.1fs  %5d rows  %s"
              % (summary["session_id"], summary.get("usable", "?"),
                 summary.get("duration_s", 0), summary.get("rows", 0),
                 summary.get("notes", "")))

    # Stack every combined file so cross-participant analysis is a filter
    # rather than a merge.
    all_path = os.path.join(out_dir, "all_sessions.csv")
    with io.open(all_path, "w", encoding="utf-8", newline="") as out:
        writer = csv.DictWriter(out, fieldnames=PASSTHROUGH + DERIVED)
        writer.writeheader()
        for path in tracking:
            combined = os.path.join(
                out_dir, os.path.basename(path)[:-len("_tracking.csv")] + "_combined.csv")
            if not os.path.exists(combined):
                continue
            for row in csv.DictReader(io.open(combined, encoding="utf-8-sig")):
                writer.writerow(row)

    summary_path = os.path.join(out_dir, "sessions_summary.csv")
    fields = ["session_id", "participant_id", "mode", "start_utc", "duration_s",
              "game_time_s", "time_ratio", "rows", "events", "pose_samples",
              "targets", "timeline_clips", "timeline_signals", "interactions",
              "methods", "input_devices", "sync_markers", "usable", "notes"]
    with io.open(summary_path, "w", encoding="utf-8", newline="") as out:
        writer = csv.DictWriter(out, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        for summary in summaries:
            writer.writerow(summary)

    print("\nWrote %d combined files" % len(summaries))
    print("  %s" % all_path)
    print("  %s" % summary_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
