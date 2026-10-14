using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

/// <summary>
/// A Timeline marker that writes one row to the session log when playback passes it.
///
/// <para>Markers are the right mechanism here rather than Signals. A Signal needs its own
/// <c>.signal</c> asset plus an entry in a SignalReceiver's list for every distinct event, so
/// logging two dozen timeline moments would mean two dozen assets and two dozen UnityEvent
/// bindings to keep in step. A marker carries its own label, so one type covers every event and
/// there is nothing to wire per marker.</para>
///
/// <para>Markers placed on the timeline's built-in marker track are delivered to components on
/// the PlayableDirector's own GameObject, which is why <see cref="TimelineLogReceiver"/> only
/// has to sit beside the director.</para>
/// </summary>
[System.Serializable]
public class LogMarker : Marker, INotification, INotificationOptionProvider
{
    /// <summary>
    /// The event type column in the log, e.g. "TIMELINE_AUDIO". Keep it stable and
    /// machine-readable; this is what the analysis filters on.
    /// </summary>
    [Tooltip("Event type recorded in the log, e.g. TIMELINE_AUDIO.")]
    public string eventType = "TIMELINE";

    /// <summary>
    /// What happened, e.g. "teacher-S1". Written to the log's source column.
    /// </summary>
    [Tooltip("What this marker represents, e.g. the clip name.")]
    public string label = "";

    /// <summary>
    /// Anything else worth keeping — the track it came from, the clip duration, a condition.
    /// </summary>
    [Tooltip("Extra context recorded alongside the event.")]
    public string detail = "";

    /// <summary>
    /// Fire even if playback jumps past this marker (a seek or a scrub), rather than only when
    /// the playhead crosses it in real time.
    ///
    /// <para>Off by default. For research data a marker that fires because someone dragged the
    /// playhead is a false event, and a scrub through the timeline would otherwise stamp the log
    /// with a burst of things that never happened to the participant.</para>
    /// </summary>
    [Tooltip("Also fire when playback skips past this marker. Off: only on real playback.")]
    public bool retroactive = false;

    /// <summary>
    /// Fire at most once per playable, even if the timeline loops.
    /// </summary>
    [Tooltip("Fire only once even if the timeline loops.")]
    public bool emitOnce = false;

    /// <summary>
    /// Fire while scrubbing in the editor. Off by default, so editing the timeline does not
    /// write rows into a session log.
    /// </summary>
    [Tooltip("Fire in edit mode. Off, so authoring does not pollute the log.")]
    public bool emitInEditor = false;

    /// <summary>
    /// Required by <see cref="INotification"/>. The default is fine: receivers identify this
    /// notification by its type, not by id.
    /// </summary>
    public PropertyName id => default;

    /// <summary>
    /// Translates the inspector toggles above into the flags Timeline's notification system
    /// expects.
    /// </summary>
    NotificationFlags INotificationOptionProvider.flags =>
        (retroactive ? NotificationFlags.Retroactive : default) |
        (emitOnce ? NotificationFlags.TriggerOnce : default) |
        (emitInEditor ? NotificationFlags.TriggerInEditMode : default);
}
