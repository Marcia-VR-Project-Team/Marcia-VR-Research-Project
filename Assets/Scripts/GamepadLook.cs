using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rotates the camera with a gamepad stick for non-VR (monitor) mode.
/// Place on the Main Camera. Disable the Tracked Pose Driver when this is active.
/// </summary>
public class GamepadLook : MonoBehaviour
{
    [Header("Input")]
    public InputActionReference look;       // Vector2 - right stick
    public InputActionReference recenter;   // Button  - optional

    [Header("Seated viewpoint")]
    [Tooltip("Eye height in metres, applied to this camera's local position. " +
             "Match it to where the headset camera sits when seated.")]
    public float eyeHeight = 1.2f;
    public bool applyEyeHeight = true;

    [Header("Look feel")]
    [Tooltip("Degrees per second at full stick deflection.")]
    public float speed = 90f;
    public float maxYaw = 80f;
    public float maxPitch = 50f;
    public bool invertY = false;

    [Header("Mouse look")]
    [Tooltip("Hold the right mouse button and move to turn the view. " +
             "The left button stays free for pressing buttons.")]
    public bool enableMouseLook = true;

    [Tooltip("Degrees per pixel of mouse movement.")]
    public float mouseSensitivity = 0.12f;

    [Tooltip("Seconds to ease toward the target angle. 0 = instant.")]
    [Range(0f, 0.3f)] public float smoothing = 0.06f;

    float yaw, pitch;
    float yawSmoothed, pitchSmoothed;
    float yawVel, pitchVel;

    void OnEnable()
    {
        if (look != null) look.action.Enable();
        if (recenter != null) recenter.action.Enable();

        // Start from whatever the camera is already facing.
        Vector3 e = transform.localEulerAngles;
        yaw = yawSmoothed = Mathf.DeltaAngle(0f, e.y);
        pitch = pitchSmoothed = Mathf.DeltaAngle(0f, e.x);

        if (applyEyeHeight)
            transform.localPosition = new Vector3(0f, eyeHeight, 0f);
    }

    void OnDisable()
    {
        if (look != null) look.action.Disable();
        if (recenter != null) recenter.action.Disable();
    }

    void Update()
    {
        Vector2 v = look != null ? look.action.ReadValue<Vector2>() : Vector2.zero;

        float dy = invertY ? v.y : -v.y;

        // Stick: a held position, so scale by time.
        yaw   = Mathf.Clamp(yaw   + v.x * speed * Time.deltaTime, -maxYaw,   maxYaw);
        pitch = Mathf.Clamp(pitch + dy  * speed * Time.deltaTime, -maxPitch, maxPitch);

        // Mouse: distance already moved this frame, so no time scaling.
        if (enableMouseLook)
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 d = mouse.delta.ReadValue();
                float mdy = invertY ? d.y : -d.y;

                yaw   = Mathf.Clamp(yaw   + d.x * mouseSensitivity, -maxYaw,   maxYaw);
                pitch = Mathf.Clamp(pitch + mdy * mouseSensitivity, -maxPitch, maxPitch);
            }
        }

        if (recenter != null && recenter.action.WasPressedThisFrame())
            yaw = pitch = 0f;

        if (smoothing > 0f)
        {
            yawSmoothed   = Mathf.SmoothDamp(yawSmoothed,   yaw,   ref yawVel,   smoothing);
            pitchSmoothed = Mathf.SmoothDamp(pitchSmoothed, pitch, ref pitchVel, smoothing);
        }
        else
        {
            yawSmoothed = yaw;
            pitchSmoothed = pitch;
        }

        transform.localRotation = Quaternion.Euler(pitchSmoothed, yawSmoothed, 0f);
    }

    /// <summary>Call this to snap the view forward (e.g. at the start of a trial).</summary>
    public void Recenter()
    {
        yaw = pitch = 0f;
    }
}