using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Receives <see cref="LogMarker"/> notifications from a Timeline and writes them to the session
/// log.
///
/// <para>Put this on the same GameObject as the PlayableDirector. Markers on the timeline's
/// built-in marker track are delivered to that GameObject's components, so no binding is needed
/// beyond adding the component.</para>
///
/// <para>The timestamp is taken inside <see cref="SessionLogger.Event"/> at the moment the
/// notification arrives, which is the frame the playhead crossed the marker — not when the row
/// reaches disk. So the UTC time in the log is the moment the sound actually started for the
/// participant, which is what you need to line it up against the watch.</para>
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class TimelineLogReceiver : MonoBehaviour, INotificationReceiver
{
    /// <summary>
    /// Also record the director's own playhead position with each event.
    ///
    /// <para>Worth keeping on. The UTC timestamp says when something happened in the world; the
    /// timeline position says where it happened in the lecture. Having both means you can still
    /// align events across participants if a session was paused or started late.</para>
    /// </summary>
    [Tooltip("Include the director's playhead time with each logged event.")]
    [SerializeField] private bool includeTimelineTime = true;

    /// <summary>
    /// The director this receiver belongs to, used to read the playhead.
    /// </summary>
    private PlayableDirector director;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
    }

    /// <summary>
    /// Called by Timeline when a notification reaches this GameObject. Anything that is not a
    /// <see cref="LogMarker"/> is ignored, so this can coexist with Signals on the same timeline.
    /// </summary>
    /// <param name="origin">The playable that sent the notification.</param>
    /// <param name="notification">The notification; only LogMarkers are handled.</param>
    /// <param name="context">User data supplied by the sender. Unused.</param>
    public void OnNotify(Playable origin, INotification notification, object context)
    {
        if (notification is not LogMarker marker) return;

        string detail = marker.detail;

        if (includeTimelineTime && director != null)
        {
            string t = $"timeline_t={director.time:F3}";
            detail = string.IsNullOrEmpty(detail) ? t : $"{detail}; {t}";
        }

        SessionLogger.Event(
            string.IsNullOrEmpty(marker.eventType) ? "TIMELINE" : marker.eventType,
            string.IsNullOrEmpty(marker.label) ? name : marker.label,
            detail);
    }
}
