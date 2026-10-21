using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Logs controller and hand input as discrete, UTC-stamped events: button presses, releases,
/// triggers, grips, whatever actions you point it at.
///
/// <para>This is event-driven rather than polled. The Input System raises its callbacks the
/// moment it processes a device report, so the timestamp we record is as close to the physical
/// press as the runtime can tell us — closer than checking the button's state once a frame,
/// which would round every press to the nearest frame boundary and lose up to ~14 ms at 72 Hz.
/// For reaction-time analysis against the watch, that difference matters.</para>
///
/// <para>Drop this on the same GameObject as the <see cref="SessionLogger"/> and add the actions
/// you care about in the inspector.</para>
/// </summary>
public class InputEventLogger : MonoBehaviour
{
    /// <summary>
    /// The actions to log. Add every control whose timing the study cares about — trigger, grip,
    /// primary/secondary buttons, thumbstick click, pinch.
    /// </summary>
    [Header("Actions to log")]
    [Tooltip("Every Input Action whose presses should be recorded.")]
    [SerializeField] private List<InputActionReference> actions = new List<InputActionReference>();

    /// <summary>
    /// Whether to log releases as well as presses. On by default: press-to-release duration is
    /// often what you actually want, and a release is one extra row.
    /// </summary>
    [Tooltip("Log the release as well as the press.")]
    [SerializeField] private bool logReleases = true;

    /// <summary>
    /// An action the researcher presses at the same moment they press the tag button on the
    /// Empatica watch, producing a <c>SYNC_MARKER</c> row. Fire it at the start and the end of
    /// every session — see <see cref="SessionLogger.SyncMarker"/> for why.
    /// </summary>
    [Header("Empatica synchronisation")]
    [Tooltip("Pressed simultaneously with the watch's tag button to align the two recordings.")]
    [SerializeField] private InputActionReference syncMarkerAction;

    void OnEnable()
    {
        foreach (InputActionReference reference in actions)
        {
            if (reference == null || reference.action == null) continue;

            reference.action.performed += OnPerformed;
            if (logReleases) reference.action.canceled += OnCanceled;

            // An action referenced but not enabled by a Player Input or Action Asset would
            // silently never fire, and the absence would only be noticed after the session.
            reference.action.Enable();
        }

        if (syncMarkerAction != null && syncMarkerAction.action != null)
        {
            syncMarkerAction.action.performed += OnSyncMarker;
            syncMarkerAction.action.Enable();
        }
    }

    void OnDisable()
    {
        foreach (InputActionReference reference in actions)
        {
            if (reference == null || reference.action == null) continue;

            reference.action.performed -= OnPerformed;
            reference.action.canceled -= OnCanceled;
        }

        if (syncMarkerAction != null && syncMarkerAction.action != null)
            syncMarkerAction.action.performed -= OnSyncMarker;
    }

    /// <summary>
    /// Handles a press. Records which physical control fired and, for analog controls, how far it
    /// was pushed.
    /// </summary>
    private void OnPerformed(InputAction.CallbackContext context) =>
        SessionLogger.Event("INPUT_PRESS", context.action.name, Describe(context));

    /// <summary>
    /// Handles a release.
    /// </summary>
    private void OnCanceled(InputAction.CallbackContext context) =>
        SessionLogger.Event("INPUT_RELEASE", context.action.name, Describe(context));

    /// <summary>
    /// Handles the synchronisation marker press.
    /// </summary>
    private void OnSyncMarker(InputAction.CallbackContext context) =>
        SessionLogger.SyncMarker($"input:{context.action.name}");

    /// <summary>
    /// Builds the detail field: which device and control fired, plus the analog value when the
    /// control has one. Knowing it was the right controller's trigger rather than just "Select"
    /// is usually the difference between usable and unusable data.
    /// </summary>
    private static string Describe(InputAction.CallbackContext context)
    {
        InputControl control = context.control;
        string path = control != null ? control.path : "unknown";

        // Not every action is a float; asking a button action for a float value is fine, asking
        // a Vector2 action is not, so it is guarded.
        string value = "";
        if (context.valueType == typeof(float))
            value = $"; value={context.ReadValue<float>():F3}";

        return $"control={path}{value}";
    }
}
