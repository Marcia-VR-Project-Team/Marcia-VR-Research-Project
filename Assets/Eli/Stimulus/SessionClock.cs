using System;
using System.Diagnostics;

/// <summary>
/// A single, high-resolution UTC clock shared by everything that logs.
///
/// <para>Why this exists: <c>DateTime.UtcNow</c> only ticks about every 15 ms on Windows, so
/// several events inside the same frame would get identical timestamps and their real order
/// would be lost. Instead we read the wall clock ONCE at startup, and from then on we measure
/// elapsed time with a <see cref="Stopwatch"/> (which is backed by the hardware performance
/// counter, accurate to well under a microsecond) and add it to that anchor.</para>
///
/// <para>Every timestamp this class produces is UTC, never local time. Local time is ambiguous
/// across daylight-saving changes and across machines in different time zones, which makes it
/// useless for lining our data up with a wearable. The Empatica exports are also in UTC, so
/// UTC is the common ground.</para>
///
/// <para>This class is static and thread-safe: any thread may call it at any time without
/// touching the Unity API, so it works from the sampler thread and the writer thread as well
/// as from the main thread.</para>
/// </summary>
public static class SessionClock
{
    /// <summary>
    /// The wall-clock UTC time at the moment the clock was anchored. Everything else is measured
    /// as an offset from here.
    /// </summary>
    private static DateTime _utcAnchor;

    /// <summary>
    /// Started at the instant <see cref="_utcAnchor"/> was read. Its elapsed time is added to the
    /// anchor to produce the current time.
    /// </summary>
    private static readonly Stopwatch _stopwatch = new Stopwatch();

    /// <summary>
    /// Unix epoch (1970-01-01 00:00:00 UTC), the zero point for <see cref="UnixMilliseconds"/>.
    /// </summary>
    private static readonly DateTime _unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Whether <see cref="Initialize"/> has run. Used so a late caller cannot silently re-anchor
    /// the clock partway through a session and shift every timestamp after it.
    /// </summary>
    public static bool IsInitialized => _stopwatch.IsRunning;

    /// <summary>
    /// The UTC time the session started, i.e. the anchor. Recorded in the session metadata file.
    /// </summary>
    public static DateTime SessionStartUtc => _utcAnchor;

    /// <summary>
    /// Anchors the clock to the current wall-clock time. Called once by
    /// <see cref="SessionLogger"/> in Awake, before anything is logged. Calling it again has no
    /// effect, which keeps timestamps monotonic for the whole session.
    /// </summary>
    public static void Initialize()
    {
        if (IsInitialized) return;

        // Spin the stopwatch up first and read the wall clock immediately after, so the gap
        // between the two is as small as possible.
        _stopwatch.Start();
        _utcAnchor = DateTime.UtcNow;
    }

    /// <summary>
    /// The current UTC time, at sub-millisecond resolution.
    /// </summary>
    public static DateTime UtcNow =>
        IsInitialized ? _utcAnchor + _stopwatch.Elapsed : DateTime.UtcNow;

    /// <summary>
    /// The current time as milliseconds since the Unix epoch, with a fractional part.
    ///
    /// <para>This is the column to join on when merging with Empatica data. The E4 exports a
    /// start time in Unix seconds plus a fixed sample rate; EmbracePlus exports Unix timestamps
    /// directly. Both convert to this scale without any time-zone guessing.</para>
    /// </summary>
    public static double UnixMilliseconds => (UtcNow - _unixEpoch).TotalMilliseconds;

    /// <summary>
    /// Formats a UTC time as ISO-8601 with milliseconds and an explicit "Z" suffix, e.g.
    /// "2026-09-22T14:03:11.427Z". Human-readable, sorts correctly as text, and unambiguous
    /// about being UTC.
    /// </summary>
    /// <param name="utc">The UTC time to format.</param>
    public static string ToIso(DateTime utc) => utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>
    /// Converts a UTC time to milliseconds since the Unix epoch.
    /// </summary>
    /// <param name="utc">The UTC time to convert.</param>
    public static double ToUnixMilliseconds(DateTime utc) => (utc - _unixEpoch).TotalMilliseconds;
}
