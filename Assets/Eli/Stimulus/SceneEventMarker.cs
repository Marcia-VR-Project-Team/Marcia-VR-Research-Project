using UnityEngine;

/// <summary>
/// Lets scene content log events without anyone writing a script.
///
/// <para>Most of what happens in the classroom is driven from the inspector — Animation Events,
/// Timeline signals, UnityEvents on XR interactables, trigger volumes. Each of those can call a
/// public method on a component, so this exposes the logger as inspector-callable methods and
/// covers everything that is not already logged from code.</para>
///
/// <para>Typical uses: a trigger volume at the front of the room logging when the participant
/// approaches the teacher; an Animation Event on the lecture logging when a slide changes; a
/// UnityEvent on the exam buttons logging each answer.</para>
/// </summary>
public class SceneEventMarker : MonoBehaviour
{
    /// <summary>
    /// The event type written to the log when a method is called without an explicit one. Use a
    /// stable, machine-readable name — it is what the analysis filters on.
    /// </summary>
    [Tooltip("Default event type for this marker, e.g. TEACHER_APPROACHED.")]
    [SerializeField] private string eventType = "SCENE_EVENT";

    /// <summary>
    /// Written into the detail column of every event from this marker. Use it for context that
    /// is fixed for this object, such as which region of the room it covers.
    /// </summary>
    [Tooltip("Fixed context added to every event from this marker.")]
    [SerializeField] private string detail = "";

    /// <summary>
    /// Only log the first occurrence. Useful for "participant entered the room" markers, where
    /// the volume would otherwise fire every time they step back across the boundary.
    /// </summary>
    [Tooltip("Log only the first time this marker fires.")]
    [SerializeField] private bool onceOnly = false;

    /// <summary>
    /// Fire automatically when something enters a trigger collider on this object. Leave off if
    /// the marker is driven by a UnityEvent instead.
    /// </summary>
    [Header("Trigger volume")]
    [Tooltip("Log automatically when a collider enters this object's trigger.")]
    [SerializeField] private bool logTriggerEnter = false;

    /// <summary>
    /// Log trigger exits as well as entries, giving a dwell time.
    /// </summary>
    [Tooltip("Also log when a collider leaves the trigger.")]
    [SerializeField] private bool logTriggerExit = false;

    /// <summary>
    /// Restricts trigger logging to colliders with this tag, normally the player. Without it,
    /// every stray physics object in the room would generate rows.
    /// </summary>
    [Tooltip("Only log colliders with this tag. Leave empty to log any collider.")]
    [SerializeField] private string requiredTag = "Player";

    /// <summary>
    /// Whether this marker has already fired, for <see cref="onceOnly"/>.
    /// </summary>
    private bool hasFired;

    /// <summary>
    /// Logs this marker's configured event. Wire this to a UnityEvent, Animation Event, or
    /// Timeline signal — it takes no parameters so it appears in the inspector's method list.
    /// </summary>
    public void LogEvent() => Emit(eventType, detail);

    /// <summary>
    /// Logs this marker's event with extra detail supplied by the caller. UnityEvents can pass a
    /// single string argument, so this covers cases like logging which answer was chosen.
    /// </summary>
    /// <param name="extraDetail">Appended to this marker's fixed detail.</param>
    public void LogEventWithDetail(string extraDetail) =>
        Emit(eventType, string.IsNullOrEmpty(detail) ? extraDetail : $"{detail}; {extraDetail}");

    /// <summary>
    /// Logs an event with a type chosen by the caller, overriding this marker's default. For a
    /// GameObject that needs to report several different things.
    /// </summary>
    /// <param name="type">The event type to record.</param>
    public void LogEventOfType(string type) => Emit(type, detail);

    void OnTriggerEnter(Collider other)
    {
        if (!logTriggerEnter) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

        Emit(eventType, Combine($"phase=enter; collider={other.name}"));
    }

    void OnTriggerExit(Collider other)
    {
        if (!logTriggerExit) return;
        if (!string.IsNullOrEmpty(requiredTag) && !other.CompareTag(requiredTag)) return;

        // Exits deliberately ignore onceOnly: an entry that was logged should always have its
        // matching exit, otherwise the dwell time cannot be computed.
        SessionLogger.Event(eventType, gameObject.name, Combine($"phase=exit; collider={other.name}"));
    }

    /// <summary>
    /// Writes the event, honouring <see cref="onceOnly"/>.
    /// </summary>
    private void Emit(string type, string eventDetail)
    {
        if (onceOnly && hasFired) return;
        hasFired = true;

        SessionLogger.Event(type, gameObject.name, eventDetail);
    }

    /// <summary>
    /// Joins this marker's fixed detail with per-occurrence detail.
    /// </summary>
    private string Combine(string extra) =>
        string.IsNullOrEmpty(detail) ? extra : $"{detail}; {extra}";
}
