using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Logs what the participant actually interacted with, and how they did it — a poke with a
/// fingertip, a ray press from a controller, or a mouse click on the same button in desktop
/// mode.
///
/// <para><b>Why this is separate from <see cref="InputDeviceLogger"/>.</b> That component
/// records that a button on a device went down. This one records that the exam's answer B was
/// selected. Both matter and neither substitutes for the other: a trigger pull tells you nothing
/// about what was aimed at, and a button press tells you nothing about which hand or method
/// caused it.</para>
///
/// <para><b>Why the method is recorded.</b> The same desk button is poked with a finger in VR
/// and clicked with a ray in desktop mode — <see cref="ModeController"/> disables the poke
/// filters so that works. If the log only said "button pressed", data from the three build
/// modes would be indistinguishable afterwards, and comparing them is the point of having three
/// modes. So every row names the interactor kind.</para>
///
/// <para>Put this anywhere in the scene; it finds the interactables itself.</para>
/// </summary>
public class InteractionLogger : MonoBehaviour
{
    /// <summary>
    /// Log XR interactable selections — poke, ray press, direct grab.
    /// </summary>
    [Header("What to log")]
    [Tooltip("XR interactables: pokes, ray presses, grabs.")]
    [SerializeField] private bool logXRInteractions = true;

    /// <summary>
    /// Log when a selection ends, giving a press duration.
    /// </summary>
    [Tooltip("Also log when the selection ends, which gives press duration.")]
    [SerializeField] private bool logSelectExit = true;

    /// <summary>
    /// Log hover enter/exit as well.
    ///
    /// <para>Off by default. Hover fires constantly as a ray sweeps across a panel, and would
    /// swamp the file. Turn it on only if the study analyses what the participant considered
    /// rather than what they chose.</para>
    /// </summary>
    [Tooltip("Log hovers too. Noisy — a ray sweeping a panel fires these constantly.")]
    [SerializeField] private bool logHover = false;

    /// <summary>
    /// Log clicks on Unity UI Buttons, which are not XR interactables.
    /// </summary>
    [Tooltip("Unity UI Button clicks (the canvas buttons, not the 3D desk buttons).")]
    [SerializeField] private bool logUIButtons = true;

    /// <summary>
    /// Include inactive objects when searching. The exam buttons are hidden until the exam
    /// begins, so without this they would never be hooked up and no answer would ever be logged.
    /// </summary>
    [Tooltip("Hook up inactive objects too — the exam buttons start hidden.")]
    [SerializeField] private bool includeInactive = true;

    /// <summary>
    /// Everything hooked, so the listeners can be removed cleanly on destroy.
    /// </summary>
    private readonly List<XRBaseInteractable> hookedInteractables = new List<XRBaseInteractable>();

    /// <summary>
    /// UI buttons hooked, likewise.
    /// </summary>
    private readonly List<Button> hookedButtons = new List<Button>();

    void Start()
    {
        // Start, not Awake: ModeController does its rewiring in Start, and the exam prefab may
        // still be assembling in Awake. Hooking after that means we see the final arrangement.
        if (logXRInteractions) HookInteractables();
        if (logUIButtons) HookUIButtons();

        SessionLogger.Event("INTERACTION_LOGGING_START", nameof(InteractionLogger),
            $"interactables={hookedInteractables.Count}; ui_buttons={hookedButtons.Count}");
    }

    /// <summary>
    /// Subscribes to every XR interactable in the scene.
    /// </summary>
    private void HookInteractables()
    {
        FindObjectsInactive inactive =
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;

        XRBaseInteractable[] interactables =
            FindObjectsByType<XRBaseInteractable>(inactive, FindObjectsSortMode.None);

        foreach (XRBaseInteractable interactable in interactables)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            if (logSelectExit) interactable.selectExited.AddListener(OnSelectExited);

            if (logHover)
            {
                interactable.hoverEntered.AddListener(OnHoverEntered);
                interactable.hoverExited.AddListener(OnHoverExited);
            }

            hookedInteractables.Add(interactable);
        }
    }

    /// <summary>
    /// Subscribes to every Unity UI Button in the scene.
    /// </summary>
    private void HookUIButtons()
    {
        FindObjectsInactive inactive =
            includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;

        Button[] buttons = FindObjectsByType<Button>(inactive, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            // Captured locally so the closure logs the button it was created for.
            Button captured = button;
            captured.onClick.AddListener(() => OnUIButtonClicked(captured));
            hookedButtons.Add(captured);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args) =>
        LogInteraction("INTERACTION_SELECT", args.interactableObject, args.interactorObject);

    private void OnSelectExited(SelectExitEventArgs args) =>
        LogInteraction("INTERACTION_SELECT_END", args.interactableObject, args.interactorObject);

    private void OnHoverEntered(HoverEnterEventArgs args) =>
        LogInteraction("INTERACTION_HOVER", args.interactableObject, args.interactorObject);

    private void OnHoverExited(HoverExitEventArgs args) =>
        LogInteraction("INTERACTION_HOVER_END", args.interactableObject, args.interactorObject);

    /// <summary>
    /// Writes one interaction row, naming the target, the method and the hand.
    /// </summary>
    private static void LogInteraction(string eventType, IXRInteractable interactable,
                                       IXRInteractor interactor)
    {
        if (interactable == null) return;

        string target = interactable.transform != null
            ? interactable.transform.name
            : "(unknown)";

        string method = DescribeInteractor(interactor);
        string source = interactor?.transform != null ? interactor.transform.name : "(unknown)";

        SessionLogger.Event(eventType, target, $"method={method}; interactor={source}");
    }

    /// <summary>
    /// Turns an interactor into the plain description the analysis needs: how the participant
    /// touched this thing.
    ///
    /// <para>"poke" is the VR push — a fingertip or controller tip physically entering the
    /// button. "ray" is a press at a distance, used by the VR controller's pointer and by the
    /// desktop mouse pointer alike. "direct" is a grab with the hand inside the object.</para>
    /// </summary>
    private static string DescribeInteractor(IXRInteractor interactor)
    {
        switch (interactor)
        {
            case XRPokeInteractor _: return "poke";
            // Gaze must be tested before ray: XRGazeInteractor derives from XRRayInteractor, so
            // the ray case would otherwise swallow every gaze and report it as a ray press.
            case XRGazeInteractor _: return "gaze";
            case XRRayInteractor _: return "ray";
            case XRDirectInteractor _: return "direct";
            case XRSocketInteractor _: return "socket";
            case NearFarInteractor near:
                // One interactor that switches between a near grab and a far ray depending on
                // distance, so the type alone does not say which happened.
                return near.interactionAttachController != null &&
                       near.selectionRegion.Value == NearFarInteractor.Region.Near
                    ? "near"
                    : "far";
            case null: return "unknown";
            default: return interactor.GetType().Name;
        }
    }

    /// <summary>
    /// Records a Unity UI Button click.
    /// </summary>
    private static void OnUIButtonClicked(Button button)
    {
        if (button == null) return;

        SessionLogger.Event("INTERACTION_UI_BUTTON", button.name,
            $"method=ui; label={ButtonLabel(button)}");
    }

    /// <summary>
    /// Reads a button's visible text, which is far more useful in the log than a GameObject name
    /// like "Button (3)".
    /// </summary>
    private static string ButtonLabel(Button button)
    {
        var tmp = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp != null && !string.IsNullOrEmpty(tmp.text)) return tmp.text;

        var text = button.GetComponentInChildren<Text>(true);
        return text != null ? text.text : "";
    }

    void OnDestroy()
    {
        foreach (XRBaseInteractable interactable in hookedInteractables)
        {
            if (interactable == null) continue;

            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        // UI listeners were added as closures and cannot be removed individually, so the whole
        // click list is left alone; the buttons are being torn down with the scene anyway.
        hookedInteractables.Clear();
        hookedButtons.Clear();
    }
}
