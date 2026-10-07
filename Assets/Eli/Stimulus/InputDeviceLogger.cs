using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
// onAnyButtonPress is an IObservable; the Call extension that subscribes to it lives here.
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Logs raw device input across all three build modes: keyboard keys, mouse and trackpad
/// clicks, gamepad buttons and sticks, and VR controller buttons.
///
/// <para><b>Why this is global rather than a list of actions.</b> <see cref="InputEventLogger"/>
/// logs the specific Input Actions the study cares about, which is the right thing when you know
/// in advance what matters. This component is the opposite: it subscribes once to the Input
/// System itself and records every button press from every connected device, whatever it is.
/// For a study that runs in three different control schemes, that matters — the participant in
/// desktop mode presses keys and mouse buttons that no XR action map mentions, and a missing
/// binding would mean those presses simply never appear.</para>
///
/// <para>Sticks and pointer movement are not buttons and have no press event, so they are
/// handled separately below.</para>
/// </summary>
public class InputDeviceLogger : MonoBehaviour
{
    /// <summary>
    /// Log every button press from every device.
    /// </summary>
    [Header("What to log")]
    [Tooltip("Keyboard keys, mouse/trackpad clicks, gamepad and VR controller buttons.")]
    [SerializeField] private bool logButtonPresses = true;

    /// <summary>
    /// Log when a stick or thumbstick starts and stops being pushed.
    ///
    /// <para>Logging a stick every frame would bury the file, so only the transitions are
    /// recorded: the moment it leaves the deadzone and the moment it returns. That gives you
    /// when the participant started and stopped moving or turning, which is the question a
    /// stick actually answers.</para>
    /// </summary>
    [Tooltip("Log when a stick starts and stops moving, not every frame.")]
    [SerializeField] private bool logStickMovement = true;

    /// <summary>
    /// How far a stick must be pushed to count as moved, 0-1.
    /// </summary>
    [Tooltip("Stick deflection that counts as movement, 0-1.")]
    [Range(0.05f, 0.9f)]
    [SerializeField] private float stickDeadzone = 0.2f;

    /// <summary>
    /// Log mouse/trackpad clicks with the pointer position, so a click can be related to what
    /// was on screen.
    /// </summary>
    [Tooltip("Include the pointer position with mouse and trackpad clicks.")]
    [SerializeField] private bool includePointerPosition = true;

    /// <summary>
    /// Button controls to ignore. Some devices report derived or duplicate controls — "press" on
    /// a stick, "anyKey" on the keyboard — which would double-log a single physical action.
    /// </summary>
    [Tooltip("Control names never logged, to avoid double-logging one physical press.")]
    [SerializeField]
    private string[] ignoredControls = { "anyKey" };

    /// <summary>
    /// Handle for the global button-press subscription, so it can be released on disable.
    /// </summary>
    private System.IDisposable buttonSubscription;

    /// <summary>
    /// Which sticks are currently deflected, so only transitions are logged. Keyed by a stable
    /// device+control string.
    /// </summary>
    private readonly HashSet<string> activeSticks = new HashSet<string>();

    /// <summary>
    /// Reusable buffer for the per-frame stick scan, so the check allocates nothing.
    /// </summary>
    private readonly List<StickControl> stickBuffer = new List<StickControl>();

    /// <summary>
    /// Records every input device present at startup, and any that connect later.
    ///
    /// <para>This is what makes a missing input diagnosable. Without it, "the trackpad did not
    /// log" is ambiguous between the device never being seen by Unity and its presses not being
    /// captured — and those have completely different causes. Listing the devices separates the
    /// two before any guessing starts.</para>
    /// </summary>
    private void LogConnectedDevices()
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            SessionLogger.Event("INPUT_DEVICE_PRESENT", device.displayName,
                $"id={device.deviceId}; category={Categorise(device)}; " +
                $"layout={device.layout}; product={device.description.product}; " +
                $"interface={device.description.interfaceName}");
        }
    }

    void OnEnable()
    {
        LogConnectedDevices();

        if (logButtonPresses)
        {
            // onAnyButtonPress fires for any button-like control on any device, including ones
            // no action map binds. That is exactly the point: it cannot miss an input because
            // someone forgot to add a binding.
            buttonSubscription = InputSystem.onAnyButtonPress.Call(OnAnyButtonPressed);
        }
    }

    void OnDisable()
    {
        buttonSubscription?.Dispose();
        buttonSubscription = null;
        activeSticks.Clear();
    }

    /// <summary>
    /// Records a button press from any device.
    /// </summary>
    /// <param name="control">The control that was pressed.</param>
    private void OnAnyButtonPressed(InputControl control)
    {
        if (control == null) return;

        foreach (string ignored in ignoredControls)
            if (control.name == ignored) return;

        // "press" needs care rather than a blanket ignore.
        //
        // A Mouse reports both "leftButton" and a synonym "press" that shares its state, so
        // logging both would double every click. But a Touchscreen or Pen has no "leftButton" —
        // "press" is its only button — so ignoring the name outright silently discarded every
        // tap from those devices. That is why trackpad input appeared not to register at all.
        //
        InputDevice device = control.device;

        // So: drop "press" only where a real "leftButton" exists to represent the same action.
        if (control.name == "press" && device.TryGetChildControl("leftButton") != null) return;

        string category = Categorise(device);

        // deviceId and product are included because displayName alone is useless for telling a
        // laptop trackpad from an external mouse: Windows presents both as a generic "Mouse".
        // They are separate physical devices with separate ids, so the id is what actually
        // distinguishes them, and the product string usually names the hardware.
        string product = device.description.product;
        string detail = $"device={device.displayName}; id={device.deviceId}; " +
                        $"control={control.path}";

        if (!string.IsNullOrEmpty(product)) detail += $"; product={product}";

        // A click is only interpretable alongside where the pointer was.
        if (includePointerPosition && device is Pointer pointer)
        {
            Vector2 p = pointer.position.ReadValue();
            detail += $"; pointer=({p.x:F0},{p.y:F0})";
        }

        SessionLogger.Event($"INPUT_{category}_PRESS", control.name, detail);
    }

    void Update()
    {
        if (logStickMovement) ScanSticks();
    }

    /// <summary>
    /// Watches every stick on every device and logs the moments it crosses the deadzone in
    /// either direction.
    /// </summary>
    private void ScanSticks()
    {
        foreach (InputDevice device in InputSystem.devices)
        {
            if (!device.added) continue;

            stickBuffer.Clear();
            CollectSticks(device, stickBuffer);

            foreach (StickControl stick in stickBuffer)
            {
                Vector2 value = stick.ReadValue();
                bool moved = value.magnitude >= stickDeadzone;
                string key = device.deviceId + ":" + stick.path;

                if (moved && activeSticks.Add(key))
                {
                    SessionLogger.Event($"INPUT_{Categorise(device)}_STICK_START", stick.name,
                        $"device={device.displayName}; control={stick.path}; " +
                        $"value=({value.x:F2},{value.y:F2})");
                }
                else if (!moved && activeSticks.Remove(key))
                {
                    SessionLogger.Event($"INPUT_{Categorise(device)}_STICK_END", stick.name,
                        $"device={device.displayName}; control={stick.path}");
                }
            }
        }
    }

    /// <summary>
    /// Collects the stick controls on a device.
    /// </summary>
    private static void CollectSticks(InputDevice device, List<StickControl> into)
    {
        foreach (InputControl control in device.allControls)
            if (control is StickControl stick)
                into.Add(stick);
    }

    /// <summary>
    /// Groups a device into the category that appears in the event type, so the analysis can
    /// filter by input kind without parsing device names.
    ///
    /// <para>One honest limitation: a laptop trackpad and an external mouse both present
    /// themselves to the operating system as a generic <c>Mouse</c> device, and the Input System
    /// reports them identically. There is no reliable way to tell them apart here, so both are
    /// logged as POINTER with the device's own display name in the detail column — which is
    /// usually, but not always, enough to distinguish them afterwards.</para>
    /// </summary>
    private static string Categorise(InputDevice device)
    {
        switch (device)
        {
            case Keyboard _: return "KEYBOARD";
            case Mouse _: return "POINTER";
            case Gamepad _: return "GAMEPAD";
            case Pointer _: return "POINTER";
            default:
                // XR controllers arrive as generic devices whose layout is based on "XRController".
                return IsXR(device) ? "XRCONTROLLER" : "DEVICE";
        }
    }

    /// <summary>
    /// Whether a device derives from the XR controller layout.
    /// </summary>
    private static bool IsXR(InputDevice device)
    {
        // Checked against the base layout rather than an exact name, because the concrete layout
        // differs per runtime: Oculus Touch, Meta Quest Touch Plus and the generic OpenXR
        // controller are all different layouts that derive from XRController.
        return InputSystem.IsFirstLayoutBasedOnSecond(device.layout, "XRController");
    }
}
