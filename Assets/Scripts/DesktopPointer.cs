using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Aims the ray pointer at the mouse cursor when the mouse is being used, and
/// straight ahead (where the head is looking) when the gamepad is being used.
/// Whichever device the participant touched last wins, so there is nothing to
/// switch between them.
///
/// Put this on the Gamepad Pointer object, under Main Camera.
///
/// Works with one camera or with several (a three-monitor rig): it finds the
/// camera whose viewport the cursor is currently over and casts through that
/// one, so the cursor is live across every screen.
/// </summary>
public class DesktopPointer : MonoBehaviour
{
    [Header("Cameras")]
    [Tooltip("Leave empty for a single camera and it uses Camera.main. For a " +
             "three-monitor rig, list all three; the cursor works across all.")]
    public Camera[] cameras;

    [Header("Input")]
    [Tooltip("The same Look action the view uses. Moving the stick hands " +
             "control back to the gamepad.")]
    public InputActionReference look;

    [Header("Visuals")]
    [Tooltip("The crosshair. Shown for the gamepad, hidden for the mouse, " +
             "since the cursor is the pointer then.")]
    public GameObject crosshair;

    [Tooltip("Keep the crosshair visible even while using the mouse.")]
    public bool alwaysShowCrosshair = false;

    [Header("Switching")]
    [Tooltip("Mouse movement in pixels per frame before the mouse takes over.")]
    public float mouseWakeThreshold = 2f;

    [Tooltip("Stick deflection before the gamepad takes back over.")]
    public float stickWakeThreshold = 0.2f;

    bool _usingMouse;

    void OnEnable()
    {
        if (look != null) look.action.Enable();

        // The participant needs to see and move the cursor freely.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cameras == null || cameras.Length == 0)
        {
            var cam = GetComponentInParent<Camera>();
            cameras = new[] { cam != null ? cam : Camera.main };
        }
    }

    void Update()
    {
        UpdateActiveDevice();

        if (_usingMouse) AimAtCursor();
        else transform.localRotation = Quaternion.identity;   // straight ahead

        Cursor.visible = _usingMouse;
        if (crosshair != null)
            crosshair.SetActive(alwaysShowCrosshair || !_usingMouse);
    }

    void UpdateActiveDevice()
    {
        var mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue().magnitude > mouseWakeThreshold)
            _usingMouse = true;

        // Any stick movement or face-button press hands control back.
        if (look != null &&
            look.action.ReadValue<Vector2>().magnitude > stickWakeThreshold)
            _usingMouse = false;

        var pad = Gamepad.current;
        if (pad != null && pad.buttonSouth.wasPressedThisFrame)
            _usingMouse = false;
    }

    void AimAtCursor()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 screenPos = mouse.position.ReadValue();
        int displayIndex = 0;

        // With native multi-display, the cursor's coordinates have to be
        // translated into the display it is actually over. z carries the
        // display index. A single wide display (Surround/Eyefinity) skips this.
        if (Display.displays.Length > 1)
        {
            Vector3 rel = Display.RelativeMouseAt(screenPos);
            if (rel != Vector3.zero)
            {
                screenPos = new Vector2(rel.x, rel.y);
                displayIndex = (int)rel.z;
            }
        }

        Camera cam = CameraFor(screenPos, displayIndex);
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(screenPos);

        // The interactor casts along its own forward, so point it down the ray.
        transform.rotation = Quaternion.LookRotation(ray.direction, Vector3.up);
    }

    /// <summary>
    /// The camera the cursor is over: right display, and cursor inside its
    /// viewport rect (three cameras can share one wide display).
    /// </summary>
    Camera CameraFor(Vector2 screenPos, int displayIndex)
    {
        Camera fallback = null;

        foreach (var cam in cameras)
        {
            if (cam == null) continue;
            fallback ??= cam;

            if (Display.displays.Length > 1 && cam.targetDisplay != displayIndex)
                continue;

            if (cam.pixelRect.Contains(screenPos))
                return cam;
        }

        return fallback;
    }
}
