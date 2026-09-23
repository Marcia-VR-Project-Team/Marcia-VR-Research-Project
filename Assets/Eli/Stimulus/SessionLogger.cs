using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;

/// <summary>
/// The single place every piece of session data is written from: discrete events (stimuli,
/// button presses, scene events, sync markers) and continuous tracking samples (head and hand
/// pose).
///
/// <para><b>Threading.</b> Callers never touch the disk. <see cref="Event"/> and
/// <see cref="Track"/> format a line and drop it on a lock-free queue, which takes a few
/// microseconds, then return. A dedicated background thread owns both files and does all the
/// actual I/O. That thread is not a coroutine and is not driven by Unity's update loop, so a
/// slow disk write or a long GC pause on the render thread cannot stall logging, and logging
/// cannot drop a frame.</para>
///
/// <para><b>Timestamps.</b> The timestamp is taken by the calling thread at the moment of the
/// call, not by the writer when it gets around to the line. Queueing therefore never distorts
/// when an event appears to have happened, however far behind the writer falls. All times come
/// from <see cref="SessionClock"/> and are UTC.</para>
///
/// <para><b>Output.</b> Three files per session, in <c>Application.persistentDataPath</c>,
/// sharing one timestamped session id:</para>
/// <list type="bullet">
/// <item><c>session_[id]_events.csv</c> — one row per discrete event.</item>
/// <item><c>session_[id]_tracking.csv</c> — one row per pose sample.</item>
/// <item><c>session_[id]_meta.txt</c> — session start time and clock information, for the
/// analyst who has to line these files up with the watch.</item>
/// </list>
/// </summary>
public class SessionLogger : MonoBehaviour
{
    /// <summary>
    /// The one logger in the scene. Static so <see cref="Event"/> and <see cref="Track"/> can be
    /// called from anywhere without a reference.
    /// </summary>
    private static SessionLogger _instance;

    /// <summary>
    /// True once the files are open and the writer thread is running. Checked before queueing so
    /// that logging before startup (or after shutdown) is a no-op rather than an exception.
    /// </summary>
    private static volatile bool _running;

    /// <summary>
    /// Pending event rows. <see cref="ConcurrentQueue{T}"/> is lock-free for our usage, so the
    /// main thread never blocks behind the writer thread.
    /// </summary>
    private readonly ConcurrentQueue<string> _eventQueue = new ConcurrentQueue<string>();

    /// <summary>
    /// Pending tracking rows. Kept separate from events because it is far higher volume and goes
    /// to a different file.
    /// </summary>
    private readonly ConcurrentQueue<string> _trackQueue = new ConcurrentQueue<string>();

    /// <summary>
    /// The background thread that drains both queues to disk.
    /// </summary>
    private Thread _writerThread;

    /// <summary>
    /// Set when a row is queued, so the writer can block instead of polling in a spin loop. This
    /// is what keeps an idle logger at essentially zero CPU while still writing promptly.
    /// </summary>
    private readonly AutoResetEvent _work = new AutoResetEvent(false);

    /// <summary>
    /// Signals the writer thread to flush what is left and exit.
    /// </summary>
    private volatile bool _shutdown;

    /// <summary>
    /// Identifies this session. Shared by all three output files and printed in the metadata, so
    /// files can never be mismatched.
    /// </summary>
    private string _sessionId;

    private string _eventPath;
    private string _trackPath;
    private string _metaPath;

    /// <summary>
    /// Optional label for the participant, written into the metadata file. Set it in the
    /// inspector before the run so the output is identifiable without renaming files by hand.
    /// </summary>
    [Header("Session")]
    [Tooltip("Participant or condition label, recorded in the session metadata file.")]
    [SerializeField] private string participantId = "unspecified";

    /// <summary>
    /// The TextMeshPro Text component used to show the output folder on the researcher's UI.
    /// </summary>
    [Header("Researcher UI")]
    [Tooltip("Optional. Displays where this session's files are being written.")]
    [SerializeField] private TMP_Text logLocation_TMP_Text = null;

    /// <summary>
    /// If true, opens Windows Explorer at the output folder when play mode ends. Convenient
    /// during development; turn it off for an actual run.
    /// </summary>
    [Tooltip("Open Windows Explorer at the output folder when the session ends.")]
    [SerializeField] private bool openExplorerOnExit = true;

    /// <summary>
    /// How often the writer thread forces its buffers to disk, in milliseconds. Flushing every
    /// row is slow at tracking rates; flushing never risks losing the tail of a session if the
    /// headset or editor is killed. A periodic flush bounds the loss to this interval.
    /// </summary>
    [Header("Writer")]
    [Tooltip("Maximum time unflushed rows can sit in memory, in milliseconds.")]
    [SerializeField] private int flushIntervalMs = 500;

    /// <summary>
    /// Column header for the events file. Kept next to the row format in
    /// <see cref="Event"/> so the two cannot drift apart.
    /// </summary>
    private const string EVENT_HEADER =
        "utc_iso,unix_ms,unity_time,event_type,source,detail";

    /// <summary>
    /// Column header for the tracking file.
    /// </summary>
    private const string TRACK_HEADER =
        "utc_iso,unix_ms,unity_time,event,target,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w,detail";

    /// <summary>
    /// The seven empty pose columns written on an event row, which has a time and an event but no
    /// position or rotation. Kept as a constant so the column count cannot drift away from
    /// <see cref="TRACK_HEADER"/> by a miscounted comma.
    /// </summary>
    private const string EMPTY_POSE = ",,,,,,,";

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            // A second logger would mean two sets of files and a split record. Drop it.
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        // Awake always runs on Unity's main thread, so this is the place to record which thread
        // that is. It must happen before the first Event() call below.
        _mainThreadId = Thread.CurrentThread.ManagedThreadId;

        // Anchor the clock before anything can log, so no timestamp predates the session start.
        SessionClock.Initialize();

        _sessionId = SessionClock.SessionStartUtc.ToString("yyyyMMdd_HHmmss") + "Z";

        // Application.persistentDataPath is a Unity API and must be read on the main thread, so
        // the paths are resolved here rather than on the writer thread.
        string dir = Application.persistentDataPath;
        _eventPath = Path.Combine(dir, $"session_{_sessionId}_events.csv");
        _trackPath = Path.Combine(dir, $"session_{_sessionId}_tracking.csv");
        _metaPath = Path.Combine(dir, $"session_{_sessionId}_meta.txt");

        WriteMetadata();

        _writerThread = new Thread(WriterLoop)
        {
            // A background thread will not keep the process alive if something goes wrong during
            // shutdown; OnApplicationQuit still gives it a chance to flush cleanly first.
            IsBackground = true,
            Name = "SessionLogger.Writer",
            // Logging must never compete with rendering for CPU.
            Priority = System.Threading.ThreadPriority.BelowNormal
        };
        _writerThread.Start();

        _running = true;

        UnityEngine.Debug.Log($"SESSION LOG: {_eventPath}");
        if (logLocation_TMP_Text != null)
            logLocation_TMP_Text.text = $"Session {_sessionId}\n{dir}";

        Event("SESSION_START", "SessionLogger", $"participant={participantId}");
    }

    /// <summary>
    /// Records a discrete event. Safe to call from any thread, and cheap enough to call from
    /// input callbacks and collision handlers.
    ///
    /// <para>The timestamp is taken here, on the calling thread, at the moment of the call.</para>
    /// </summary>
    /// <param name="eventType">A stable, machine-readable category, e.g. "BUTTON_DOWN",
    /// "ANOMALY_ACTIVATED", "ROUND_END". Use the same spelling every time so the analysis can
    /// filter on it.</param>
    /// <param name="source">What produced the event — usually a GameObject or action name.</param>
    /// <param name="detail">Anything else worth keeping. Free text; commas and quotes are
    /// escaped automatically.</param>
    public static void Event(string eventType, string source, string detail = "")
    {
        if (!_running) return;

        DateTime utc = SessionClock.UtcNow;

        // Unity's Time.time is only valid on the main thread, and we may be called from another
        // one. It is a convenience column for cross-referencing against Unity-side behaviour, so
        // it is left blank rather than risking an exception.
        string unityTime = IsMainThread
            ? Time.timeAsDouble.ToString("F4", CultureInfo.InvariantCulture)
            : "";

        string iso = SessionClock.ToIso(utc);
        string unix = SessionClock.ToUnixMilliseconds(utc).ToString("F3", CultureInfo.InvariantCulture);

        _instance._eventQueue.Enqueue(string.Concat(
            iso, ",", unix, ",", unityTime, ",",
            Csv(eventType), ",",
            Csv(source), ",",
            Csv(detail)));

        // The same event also goes into the tracking file, on its own row, so that one file holds
        // the whole session in time order: every row starts with the timestamp, and an event row
        // carries the event name right beside it. Both streams share this one queue precisely so
        // that order is preserved — two queues would let a sample and an event drained at
        // different moments land out of sequence, even though their timestamps were correct.
        _instance._trackQueue.Enqueue(string.Concat(
            iso, ",", unix, ",", unityTime, ",",
            Csv(eventType), ",",
            Csv(source),
            EMPTY_POSE, ",",
            Csv(detail)));

        _instance._work.Set();
    }

    /// <summary>
    /// Records one continuous tracking sample — a pose for the head, a hand, or any other
    /// tracked transform. Called by <see cref="TrackingSampler"/> at a fixed rate.
    ///
    /// <para>Rotation is stored as a quaternion rather than Euler angles on purpose: Euler
    /// angles are discontinuous and gimbal-locked, which produces artefacts exactly when the
    /// participant turns their head quickly, which is when the data matters most.</para>
    /// </summary>
    /// <param name="target">What was sampled, e.g. "Head", "LeftHand".</param>
    /// <param name="utc">The UTC time the pose was read, passed in so it reflects the sample
    /// instant rather than the moment this row happened to be formatted.</param>
    /// <param name="unityTime">Unity's <c>Time.timeAsDouble</c> at the sample instant.</param>
    /// <param name="position">World-space position.</param>
    /// <param name="rotation">World-space rotation.</param>
    public static void Track(string target, DateTime utc, double unityTime,
                             Vector3 position, Quaternion rotation)
    {
        if (!_running) return;

        var sb = new StringBuilder(160);
        sb.Append(SessionClock.ToIso(utc)).Append(',');
        sb.Append(SessionClock.ToUnixMilliseconds(utc).ToString("F3", CultureInfo.InvariantCulture)).Append(',');
        sb.Append(unityTime.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
        // The event column is empty on a pose row. Filtering the file on "event is not blank"
        // gives you just the events; leaving it blank keeps every sample row aligned with them.
        sb.Append(',');
        sb.Append(Csv(target)).Append(',');
        Append(sb, position.x); sb.Append(',');
        Append(sb, position.y); sb.Append(',');
        Append(sb, position.z); sb.Append(',');
        Append(sb, rotation.x); sb.Append(',');
        Append(sb, rotation.y); sb.Append(',');
        Append(sb, rotation.z); sb.Append(',');
        Append(sb, rotation.w);
        // Trailing empty detail column, so a pose row has the same column count as an event row.
        sb.Append(',');

        _instance._trackQueue.Enqueue(sb.ToString());
        _instance._work.Set();
    }

    /// <summary>
    /// Records a synchronisation marker: the event used to align this log with the Empatica
    /// recording.
    ///
    /// <para>Timestamps alone should be enough, but only if both clocks are actually correct.
    /// The headset and the watch each drift, and neither is guaranteed to have synced against a
    /// time server recently. The standard defence is to create a moment that is unmistakable in
    /// both records: press the tag button on the watch and fire this marker at the same instant,
    /// at the start of the session and again at the end. Two markers also let the analysis
    /// correct for linear drift over the session, not just a constant offset.</para>
    /// </summary>
    /// <param name="label">Which marker this is, e.g. "start", "end".</param>
    public static void SyncMarker(string label = "")
    {
        Event("SYNC_MARKER", "Researcher", label);
        UnityEngine.Debug.Log($"SYNC MARKER '{label}' at {SessionClock.ToIso(SessionClock.UtcNow)}");
    }

    /// <summary>
    /// Writes the session metadata file. This is what an analyst reads first, so it records not
    /// just when the session started but how far the machine's clock can be trusted.
    /// </summary>
    private void WriteMetadata()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# VR session log metadata");
        sb.AppendLine($"session_id: {_sessionId}");
        sb.AppendLine($"participant_id: {participantId}");
        sb.AppendLine($"session_start_utc: {SessionClock.ToIso(SessionClock.SessionStartUtc)}");
        sb.AppendLine($"session_start_unix_ms: {SessionClock.ToUnixMilliseconds(SessionClock.SessionStartUtc).ToString("F3", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"session_start_local: {SessionClock.SessionStartUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({TimeZoneInfo.Local.Id})");
        sb.AppendLine($"machine: {SystemInfo.deviceName}");
        sb.AppendLine($"unity_version: {Application.unityVersion}");
        sb.AppendLine($"high_resolution_timer: {Stopwatch.IsHighResolution} ({Stopwatch.Frequency} ticks/sec)");
        sb.AppendLine();
        sb.AppendLine("# All timestamps in the CSV files are UTC.");
        sb.AppendLine("# Join with Empatica data on the unix_ms column.");
        sb.AppendLine("# Check the SYNC_MARKER rows in the events file against the watch's tag");
        sb.AppendLine("# events before trusting the absolute offset between the two recordings.");

        File.WriteAllText(_metaPath, sb.ToString());
    }

    /// <summary>
    /// The writer thread's whole life. Opens both files, then blocks until there is something to
    /// write, drains everything available, and flushes periodically.
    /// </summary>
    private void WriterLoop()
    {
        try
        {
            // 64 KB buffers: large enough that tracking rows rarely trigger a syscall, small
            // enough to be irrelevant to memory.
            using var eventWriter = new StreamWriter(_eventPath, append: false, Encoding.UTF8, 1 << 16);
            using var trackWriter = new StreamWriter(_trackPath, append: false, Encoding.UTF8, 1 << 16);

            eventWriter.WriteLine(EVENT_HEADER);
            trackWriter.WriteLine(TRACK_HEADER);
            eventWriter.Flush();
            trackWriter.Flush();

            var sinceFlush = Stopwatch.StartNew();

            while (true)
            {
                // Sleep until a row arrives. The timeout bounds how long the last row can sit
                // unflushed when the queues go quiet.
                _work.WaitOne(flushIntervalMs);

                bool wrote = Drain(eventWriter, trackWriter);

                if (sinceFlush.ElapsedMilliseconds >= flushIntervalMs)
                {
                    if (wrote)
                    {
                        eventWriter.Flush();
                        trackWriter.Flush();
                    }
                    sinceFlush.Restart();
                }

                if (_shutdown)
                {
                    // Drain once more: rows queued between the shutdown flag and now.
                    Drain(eventWriter, trackWriter);
                    eventWriter.Flush();
                    trackWriter.Flush();
                    return;
                }
            }
        }
        catch (Exception e)
        {
            // Never let a logging failure take the session down, but make it loud.
            UnityEngine.Debug.LogError($"SessionLogger writer thread failed: {e}");
            _running = false;
        }
    }

    /// <summary>
    /// Empties both queues into their writers.
    /// </summary>
    /// <returns>True if anything was written, so the caller can skip a pointless flush.</returns>
    private bool Drain(StreamWriter eventWriter, StreamWriter trackWriter)
    {
        bool wrote = false;

        while (_eventQueue.TryDequeue(out string row))
        {
            eventWriter.WriteLine(row);
            wrote = true;
        }

        while (_trackQueue.TryDequeue(out string row))
        {
            trackWriter.WriteLine(row);
            wrote = true;
        }

        return wrote;
    }

    /// <summary>
    /// The managed thread id of Unity's main thread, captured in Awake. Used to decide whether
    /// Unity APIs are safe to touch from inside <see cref="Event"/>.
    /// </summary>
    private static int _mainThreadId = -1;

    /// <summary>
    /// Whether the calling thread is Unity's main thread.
    /// </summary>
    private static bool IsMainThread =>
        Thread.CurrentThread.ManagedThreadId == _mainThreadId;

    /// <summary>
    /// Appends a float using invariant culture, so the files are not corrupted by a machine whose
    /// locale uses a comma as the decimal separator — which would silently shift every column.
    /// </summary>
    private static void Append(StringBuilder sb, float value) =>
        sb.Append(value.ToString("F5", CultureInfo.InvariantCulture));

    /// <summary>
    /// Escapes a field for CSV: quotes it if it contains a comma, quote, or newline, and doubles
    /// any embedded quotes. Free-text detail fields would otherwise break the column layout.
    /// </summary>
    private static string Csv(string field)
    {
        if (string.IsNullOrEmpty(field)) return "";

        if (field.IndexOf(',') < 0 && field.IndexOf('"') < 0 &&
            field.IndexOf('\n') < 0 && field.IndexOf('\r') < 0)
            return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Stops the writer thread and waits briefly for it to flush, so the tail of the session is
    /// not lost.
    /// </summary>
    private void Shutdown()
    {
        if (!_running) return;

        Event("SESSION_END", "SessionLogger", "");

        _running = false;
        _shutdown = true;
        _work.Set();

        // Bounded wait: a hung writer must not hang the application's exit.
        _writerThread?.Join(2000);
    }

    void OnApplicationQuit()
    {
        Shutdown();

        if (!openExplorerOnExit) return;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        Process.Start("explorer.exe", $"/select,\"{_eventPath.Replace("/", "\\")}\"");
#endif
    }

    void OnDestroy()
    {
        if (_instance != this) return;

        // OnApplicationQuit does not run when a scene tears down in the editor, so shut down here
        // too. Shutdown is idempotent.
        Shutdown();
        _instance = null;
    }
}
