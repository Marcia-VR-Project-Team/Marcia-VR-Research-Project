using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Switches the classroom between VR (headset + hands) and Desktop (monitor +
/// gamepad). Set the Mode dropdown and press Play. Everything else is automatic.
///
/// Desktop mode:
///   - turns off the Tracked Pose Driver so the gamepad drives the view
///   - sets the camera to seated eye height (no headset to supply it)
///   - turns on the gamepad look script, the ray pointer and the crosshair
///   - hides the hands and controllers
///   - disables XR Poke Filters so the desk buttons accept a ray press,
///     and animates the cap so it still looks pressed
/// VR mode: the exact opposite.
/// </summary>
public class ModeController : MonoBehaviour
{
    public enum Mode { VR, Desktop, TripleMonitor }

    [Header("Mode")]
    [Tooltip("Used in the Editor. A build takes -mode vr | desktop | triple " +
             "from the command line, falling back to this.")]
    public Mode mode = Mode.Desktop;

    [Tooltip("Let the command line override the dropdown in a build.")]
    public bool allowCommandLineOverride = true;

    [Header("Three-monitor rig")]
    [Tooltip("The TripleDisplayRig component. Used only in TripleMonitor mode.")]
    public TripleDisplayRig tripleDisplayRig;

    [Tooltip("The DesktopPointer on Gamepad Pointer, so the cursor can be " +
             "mapped across all three screens.")]
    public DesktopPointer desktopPointer;

    [Header("Rig references")]
    [Tooltip("The Main Camera under Camera Offset.")]
    public Camera mainCamera;

    [Tooltip("The GamepadLook component on Main Camera.")]
    public GamepadLook gamepadLook;

    [Tooltip("The Gamepad Pointer object under Main Camera.")]
    public GameObject gamepadPointer;

    [Tooltip("The Desktop HUD canvas holding the crosshair.")]
    public GameObject desktopHUD;

    [Tooltip("Objects that exist only in VR: Left/Right Controller, Left/Right " +
             "Hand, Hand Visualizer, Hands Smoothing Post Processor, " +
             "Calibration Instructions Panel.")]
    public GameObject[] vrOnlyObjects;

    [Header("Desktop viewpoint")]
    [Tooltip("Seated eye height in metres. Read this off the camera's local Y " +
             "while wearing the headset seated at the desk.")]
    public float eyeHeight = 1.15f;

    [Header("Desktop button presses")]
    [Tooltip("Let the ray press poke-only buttons.")]
    public bool makeButtonsRayPressable = true;

    [Tooltip("Cap travel in metres. Match the Poke Follow Affordance's Max Distance.")]
    public float pressDepth = 0.0165f;

    public float pressDuration = 0.1f;
    public string capChildName = "Button";

    readonly List<XRPokeFilter> _disabledFilters = new();
    readonly List<MonoBehaviour> _disabledAffordances = new();

    // A cap's resting position, captured once at setup so repeated presses
    // can never drift it downward.
    readonly Dictionary<Transform, Vector3> _capHome = new();
    readonly Dictionary<Transform, Coroutine> _capRoutine = new();

    void Start()
    {
        if (allowCommandLineOverride) ApplyCommandLineMode();

        // Both non-VR modes share the same input, pointer and button handling;
        // only the camera arrangement differs.
        bool desktop = mode != Mode.VR;

        // --- Camera -------------------------------------------------------
        if (mainCamera != null)
        {
            var driver = mainCamera.GetComponent<TrackedPoseDriver>();
            if (driver != null) driver.enabled = !desktop;

            if (desktop)
                mainCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
        }

        // --- Desktop-only objects ----------------------------------------
        if (gamepadLook != null) gamepadLook.enabled = desktop;
        if (gamepadPointer != null) gamepadPointer.SetActive(desktop);
        if (desktopHUD != null) desktopHUD.SetActive(desktop);

        // --- VR-only objects ----------------------------------------------
        foreach (var go in vrOnlyObjects)
            if (go != null) go.SetActive(!desktop);

        // --- Three-monitor rig --------------------------------------------
        if (mode == Mode.TripleMonitor && tripleDisplayRig != null)
        {
            var cams = tripleDisplayRig.Build(mainCamera);

            // The cursor has to know which camera it is over.
            if (desktopPointer != null) desktopPointer.cameras = cams;
        }

        // --- Buttons -------------------------------------------------------
        if (desktop && makeButtonsRayPressable)
            MakeButtonsRayPressable();

        // --- XR ------------------------------------------------------------
        // "Initialize XR on Startup" is off so the non-VR modes never touch the
        // headset, which means VR mode has to bring it up itself.
        if (mode == Mode.VR) StartCoroutine(StartXR());

        Debug.Log($"[ModeController] Running in {mode} mode.");
    }

    void ApplyCommandLineMode()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].ToLowerInvariant() != "-mode") continue;

            switch (args[i + 1].ToLowerInvariant())
            {
                case "vr":      mode = Mode.VR; break;
                case "desktop":
                case "single":  mode = Mode.Desktop; break;
                case "triple":
                case "3":       mode = Mode.TripleMonitor; break;
                default:
                    Debug.LogWarning($"[ModeController] Unknown -mode '{args[i + 1]}'.");
                    break;
            }
            return;
        }
    }

    IEnumerator StartXR()
    {
        var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
        if (settings == null || settings.Manager == null)
        {
            Debug.LogError("[ModeController] No XR settings; cannot start VR.");
            yield break;
        }

        var manager = settings.Manager;

        if (manager.activeLoader == null)
            yield return manager.InitializeLoader();

        if (manager.activeLoader == null)
        {
            Debug.LogError("[ModeController] XR failed to initialize. " +
                           "Is the headset connected and its runtime running?");
            yield break;
        }

        manager.StartSubsystems();
        Debug.Log("[ModeController] XR started.");
    }

    void StopXR()
    {
        var manager = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager;
        if (manager == null || manager.activeLoader == null) return;

        manager.StopSubsystems();
        manager.DeinitializeLoader();
    }

    /// <summary>
    /// XRPokeFilter is a select filter that rejects anything that isn't a poke.
    /// A disabled filter reports canProcess = false, so XRI skips it and the
    /// interactable accepts a ray select. The Exam script listens to the
    /// interactable, so it keeps working unchanged.
    /// </summary>
    void MakeButtonsRayPressable()
    {
        // Include inactive: the exam buttons are hidden until the exam begins.
        var filters = FindObjectsByType<XRPokeFilter>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var filter in filters)
        {
            if (!filter.enabled) continue;
            filter.enabled = false;
            _disabledFilters.Add(filter);

            var interactable = filter.GetComponent<XRBaseInteractable>();
            if (interactable == null) continue;

            // The poke-follow affordance chases a poke point that no longer
            // exists, so switch it off and drive the cap ourselves.
            foreach (var mb in interactable.GetComponents<MonoBehaviour>())
            {
                if (mb == null || !mb.enabled) continue;
                if (mb.GetType().Name != "XRPokeFollowAffordance") continue;
                mb.enabled = false;
                _disabledAffordances.Add(mb);
            }

            // Record where the cap rests, before anything can move it.
            var cap = interactable.transform.Find(capChildName);
            if (cap != null && !_capHome.ContainsKey(cap))
                _capHome[cap] = cap.localPosition;

            interactable.selectEntered.AddListener(OnSelectEntered);
        }

        Debug.Log($"[ModeController] {_disabledFilters.Count} buttons are now ray-pressable.");
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        var cap = args.interactableObject.transform.Find(capChildName);
        if (cap == null) return;

        if (!_capHome.ContainsKey(cap))
            _capHome[cap] = cap.localPosition;

        // Cancel an animation still in flight on this cap, otherwise two
        // coroutines fight over the same transform.
        if (_capRoutine.TryGetValue(cap, out var running) && running != null)
            StopCoroutine(running);

        _capRoutine[cap] = StartCoroutine(PressCap(cap));
    }

    IEnumerator PressCap(Transform cap)
    {
        Vector3 up = _capHome[cap];
        Vector3 down = up - new Vector3(0f, pressDepth, 0f);

        // Always start from home, so an interrupted press can't compound.
        cap.localPosition = up;

        yield return Move(cap, up, down, pressDuration);
        yield return new WaitForSeconds(0.04f);
        yield return Move(cap, down, up, pressDuration);

        cap.localPosition = up;
        _capRoutine[cap] = null;
    }

    IEnumerator Move(Transform t, Vector3 from, Vector3 to, float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            t.localPosition = Vector3.Lerp(from, to, e / duration);
            e += Time.deltaTime;
            yield return null;
        }
        t.localPosition = to;
    }

    void OnDestroy()
    {
        foreach (var f in _disabledFilters) if (f != null) f.enabled = true;
        foreach (var a in _disabledAffordances) if (a != null) a.enabled = true;

        // Put every cap back where it started.
        foreach (var kvp in _capHome)
            if (kvp.Key != null) kvp.Key.localPosition = kvp.Value;

        if (mode == Mode.VR) StopXR();
    }
}