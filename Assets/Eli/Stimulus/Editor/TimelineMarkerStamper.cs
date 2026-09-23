using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

/// <summary>
/// Editor tool that places a <see cref="LogMarker"/> at the start of every clip on a Timeline, so
/// each timeline event writes a row to the session log.
///
/// <para><b>Why a tool instead of markers committed into the .playable file.</b> The timeline has
/// two dozen clips and is still being edited. Markers placed by hand would be a snapshot that
/// silently goes stale the moment a clip moves, and nothing would flag the drift — the log would
/// just quietly report the wrong times. Regenerating from the clips themselves means the markers
/// cannot disagree with what actually plays.</para>
///
/// <para>Run it again after any timeline edit. It removes the markers it previously created
/// before adding new ones, so repeated runs do not stack up duplicates, and markers you added by
/// hand on other tracks are left alone.</para>
/// </summary>
public static class TimelineMarkerStamper
{
    /// <summary>
    /// Written into every generated marker's detail field so the tool can recognise its own work
    /// on a later run and clear it out.
    /// </summary>
    private const string GENERATED_TAG = "generated=auto";

    /// <summary>
    /// Stamps markers onto whichever TimelineAsset is selected in the Project window.
    /// </summary>
    [MenuItem("Tools/Session Logging/Stamp Log Markers On Selected Timeline")]
    public static void StampSelected()
    {
        TimelineAsset timeline = Selection.activeObject as TimelineAsset;

        if (timeline == null)
        {
            EditorUtility.DisplayDialog(
                "No Timeline selected",
                "Select a Timeline asset (.playable) in the Project window, then run this again.",
                "OK");
            return;
        }

        Stamp(timeline, logClipEnds: false);
    }

    /// <summary>
    /// As <see cref="StampSelected"/>, but also marks where each clip finishes. Useful when you
    /// care about how long the participant was exposed to something, rather than only when it
    /// began.
    /// </summary>
    [MenuItem("Tools/Session Logging/Stamp Log Markers On Selected Timeline (with clip ends)")]
    public static void StampSelectedWithEnds()
    {
        TimelineAsset timeline = Selection.activeObject as TimelineAsset;
        if (timeline == null) return;

        Stamp(timeline, logClipEnds: true);
    }

    /// <summary>
    /// Does the work: clears previously generated markers, then adds one per clip.
    /// </summary>
    /// <param name="timeline">The timeline to stamp.</param>
    /// <param name="logClipEnds">Also place a marker where each clip ends.</param>
    private static void Stamp(TimelineAsset timeline, bool logClipEnds)
    {
        Undo.RegisterCompleteObjectUndo(timeline, "Stamp Log Markers");

        // The built-in marker track routes notifications to the PlayableDirector's own
        // GameObject, which is where TimelineLogReceiver lives. A custom MarkerTrack would need
        // a binding set by hand for every scene the timeline is used in.
        MarkerTrack track = timeline.markerTrack;
        if (track == null)
        {
            timeline.CreateMarkerTrack();
            track = timeline.markerTrack;
        }

        int removed = ClearGenerated(track);

        int added = 0;
        var skipped = new List<string>();

        foreach (TrackAsset t in timeline.GetOutputTracks())
        {
            // Skip the marker track itself, and group tracks, which hold no clips.
            if (t is MarkerTrack) continue;

            // A muted track produces no sound, so marking its clips would put events in the log
            // that the participant never experienced. This is how the language condition is
            // chosen — one of the two teacher tracks is muted in the Timeline window — so the
            // markers follow whichever language is actually live. Re-run the tool after
            // switching languages and the log follows.
            if (IsMutedInHierarchy(t))
            {
                if (t.GetClips().Any()) skipped.Add(t.name);
                continue;
            }

            foreach (TimelineClip clip in t.GetClips())
            {
                added += AddMarker(track, clip.start, "TIMELINE_CLIP_START", clip.displayName,
                                   $"track={t.name}; duration={clip.duration:F2}");

                if (logClipEnds)
                    added += AddMarker(track, clip.end, "TIMELINE_CLIP_END", clip.displayName,
                                       $"track={t.name}");
            }

            // Signals are markers, not clips, so the loop above misses them — and they are
            // usually the most important moments on a timeline, because they actually drive
            // gameplay rather than just playing a sound. ExamBeginSignal is the example here.
            added += StampSignals(track, t);
        }

        // Signals can also sit on the timeline's own marker track, which is not an output track.
        added += StampSignals(track, track);

        EditorUtility.SetDirty(timeline);
        AssetDatabase.SaveAssets();

        // Naming the skipped tracks is the point: if the wrong language is muted, the mistake is
        // visible now rather than after a session produces a log of the wrong condition.
        string skippedNote = skipped.Count == 0
            ? ""
            : $" Skipped {skipped.Count} muted track(s) with clips: {string.Join(", ", skipped)}.";

        Debug.Log($"Stamped {added} log markers on '{timeline.name}' " +
                  $"(removed {removed} previously generated).{skippedNote} " +
                  "Add a TimelineLogReceiver to the PlayableDirector's GameObject if it has none.");
    }

    /// <summary>
    /// Whether this track is silenced, either by its own mute flag or by a muted group track
    /// above it.
    ///
    /// <para>Checking the parents matters: muting the "Teacher Speaking" group silences both
    /// language tracks underneath it, but leaves each child's own mute flag reading false.
    /// Trusting the local flag alone would log clips nobody heard.</para>
    /// </summary>
    private static bool IsMutedInHierarchy(TrackAsset track)
    {
        for (TrackAsset t = track; t != null; t = t.parent as TrackAsset)
            if (t.muted) return true;

        return false;
    }

    /// <summary>
    /// Places a log marker alongside every Signal on <paramref name="source"/>, at the same time,
    /// so the signal's firing appears in the log.
    /// </summary>
    /// <param name="track">The marker track to add to.</param>
    /// <param name="source">The track to scan for signal emitters.</param>
    /// <returns>How many markers were added.</returns>
    private static int StampSignals(MarkerTrack track, TrackAsset source)
    {
        int added = 0;

        foreach (SignalEmitter signal in source.GetMarkers().OfType<SignalEmitter>())
        {
            string signalName = signal.asset != null ? signal.asset.name : "(no signal asset)";

            added += AddMarker(track, signal.time, "TIMELINE_SIGNAL", signalName,
                               $"track={source.name}");
        }

        return added;
    }

    /// <summary>
    /// Creates one marker.
    /// </summary>
    /// <returns>1, so callers can total up what was added.</returns>
    private static int AddMarker(MarkerTrack track, double time, string eventType,
                                 string label, string detail)
    {
        LogMarker marker = track.CreateMarker<LogMarker>(time);
        marker.eventType = eventType;
        marker.label = string.IsNullOrEmpty(label) ? "(unnamed clip)" : label;
        marker.detail = $"{detail}; {GENERATED_TAG}";
        return 1;
    }

    /// <summary>
    /// Removes markers this tool created previously, leaving hand-placed markers and Signals
    /// untouched.
    /// </summary>
    /// <returns>How many were removed.</returns>
    private static int ClearGenerated(MarkerTrack track)
    {
        List<IMarker> stale = track.GetMarkers()
            .OfType<LogMarker>()
            .Where(m => m.detail != null && m.detail.Contains(GENERATED_TAG))
            .Cast<IMarker>()
            .ToList();

        foreach (IMarker m in stale) track.DeleteMarker(m);

        return stale.Count;
    }
}
