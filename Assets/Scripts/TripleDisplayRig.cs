using System.IO;
using UnityEngine;

/// <summary>
/// Builds a three-camera rig for a three-monitor setup, using only what Unity
/// can detect on any platform: how many displays exist and each one's pixel
/// resolution. No native calls, so this works on Windows, macOS and Linux.
///
/// Physical monitor size and viewing distance are not knowable cross-platform,
/// so instead of measuring, the geometry is derived:
///
///   - all three cameras share one VERTICAL field of view, which keeps the
///     horizon at the same height on every screen even if the monitors differ
///   - each camera's HORIZONTAL field of view follows from its own display's
///     aspect ratio
///   - each side camera is rotated by (half the centre's horizontal FOV +
///     half of its own), so the three views tile edge to edge with no gap and
///     no overlap
///
/// The result is continuous across the three screens whatever their
/// resolutions. Angle the physical monitors to roughly match and it reads
/// correctly. Anything you do want to override lives in display-config.json,
/// written next to the build on first run.
/// </summary>
public class TripleDisplayRig : MonoBehaviour
{
    [System.Serializable]
    public class DisplayConfig
    {
        [Tooltip("Vertical field of view shared by all three cameras.")]
        public float verticalFovDeg = 55f;

        [Tooltip("Which Unity display index sits on the left / centre / right. " +
                 "The OS decides the order, so if the screens come up wrong, " +
                 "swap these numbers rather than the cables.")]
        public int leftDisplay = 1;
        public int centerDisplay = 0;
        public int rightDisplay = 2;

        [Tooltip("Derive the side angle so the three views join seamlessly. " +
                 "Turn this off to force sideAngleDeg instead.")]
        public bool autoSideAngle = true;

        [Tooltip("Used only when autoSideAngle is off.")]
        public float sideAngleDeg = 45f;
    }

    [Header("Configuration")]
    public DisplayConfig config = new();

    [Tooltip("Load display-config.json from next to the build, and write a " +
             "default one if it is missing. Ignored in the Editor.")]
    public bool useConfigFile = true;

    [Header("Result (read-only)")]
    public Camera leftCamera;
    public Camera centerCamera;
    public Camera rightCamera;

    const string ConfigFileName = "display-config.json";

    /// <summary>
    /// Creates the two side cameras and points all three at their displays.
    /// Returns every camera it set up, for DesktopPointer's cursor mapping.
    /// </summary>
    public Camera[] Build(Camera mainCamera)
    {
        if (mainCamera == null)
        {
            Debug.LogError("[TripleDisplayRig] No main camera.");
            return new Camera[0];
        }

        LoadConfig();

        int detected = Display.displays.Length;
        Debug.Log($"[TripleDisplayRig] {detected} display(s) detected.");

        if (detected < 3)
            Debug.LogWarning($"[TripleDisplayRig] Only {detected} display(s) " +
                             "found. The side cameras will render, but you " +
                             "will not see them until three are connected.");

        centerCamera = mainCamera;
        centerCamera.targetDisplay = config.centerDisplay;
        centerCamera.fieldOfView = config.verticalFovDeg;   // vertical

        leftCamera  = CreateSideCamera(mainCamera, "Left Camera",  config.leftDisplay);
        rightCamera = CreateSideCamera(mainCamera, "Right Camera", config.rightDisplay);

        ApplyAngles();
        ActivateDisplays();

        return new[] { leftCamera, centerCamera, rightCamera };
    }

    Camera CreateSideCamera(Camera source, string name, int targetDisplay)
    {
        var go = new GameObject(name);

        // Children of the main camera, so they inherit the head's rotation
        // and the gamepad/mouse look drives all three together.
        go.transform.SetParent(source.transform, false);
        go.transform.localPosition = Vector3.zero;

        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(source);              // clipping planes, culling, clear flags
        cam.targetDisplay = targetDisplay;
        cam.fieldOfView = config.verticalFovDeg;

        // CopyFrom does not carry URP's per-camera settings, so mirror the
        // ones that would otherwise make the side screens look different.
        var srcData = source.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        var dstData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (srcData != null && dstData != null)
        {
            dstData.renderShadows = srcData.renderShadows;
            dstData.renderPostProcessing = srcData.renderPostProcessing;
            dstData.antialiasing = srcData.antialiasing;
            dstData.volumeLayerMask = srcData.volumeLayerMask;
        }

        return cam;
    }

    /// <summary>
    /// Rotates each side camera outward by half the centre's horizontal FOV
    /// plus half of its own, so the three frusta meet exactly.
    /// </summary>
    void ApplyAngles()
    {
        float centerHalf = HorizontalHalfAngle(centerCamera);

        float leftAngle = config.autoSideAngle
            ? centerHalf + HorizontalHalfAngle(leftCamera)
            : config.sideAngleDeg;

        float rightAngle = config.autoSideAngle
            ? centerHalf + HorizontalHalfAngle(rightCamera)
            : config.sideAngleDeg;

        leftCamera.transform.localRotation  = Quaternion.Euler(0f, -leftAngle,  0f);
        rightCamera.transform.localRotation = Quaternion.Euler(0f,  rightAngle, 0f);

        Debug.Log($"[TripleDisplayRig] Side angles: {leftAngle:F1}° / {rightAngle:F1}°, " +
                  $"vertical FOV {config.verticalFovDeg}°.");
    }

    /// <summary>Half the camera's horizontal FOV, from its vertical FOV and aspect.</summary>
    static float HorizontalHalfAngle(Camera cam)
    {
        float aspect = AspectFor(cam);
        float halfVertRad = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
        return Mathf.Atan(Mathf.Tan(halfVertRad) * aspect) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// A camera's aspect comes from the display it targets. Before that display
    /// is active its size may read as zero, so fall back to 16:9.
    /// </summary>
    static float AspectFor(Camera cam)
    {
        int i = cam.targetDisplay;
        if (i >= 0 && i < Display.displays.Length)
        {
            var d = Display.displays[i];
            if (d.systemWidth > 0 && d.systemHeight > 0)
                return (float)d.systemWidth / d.systemHeight;
        }
        return 16f / 9f;
    }

    /// <summary>
    /// Only display 0 is live at launch; the rest need activating, and only in
    /// a build. Activating uses each display's own native resolution.
    /// </summary>
    void ActivateDisplays()
    {
#if !UNITY_EDITOR
        for (int i = 1; i < Display.displays.Length; i++)
        {
            var d = Display.displays[i];
            if (!d.active) d.Activate();
        }
#endif
    }

    // ---- Config file -----------------------------------------------------

    string ConfigPath =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", ConfigFileName);

    void LoadConfig()
    {
#if UNITY_EDITOR
        return;   // in the Editor, use the Inspector values
#else
        if (!useConfigFile) return;

        try
        {
            if (File.Exists(ConfigPath))
            {
                JsonUtility.FromJsonOverwrite(File.ReadAllText(ConfigPath), config);
                Debug.Log($"[TripleDisplayRig] Loaded {ConfigPath}");
            }
            else
            {
                File.WriteAllText(ConfigPath, JsonUtility.ToJson(config, true));
                Debug.Log($"[TripleDisplayRig] Wrote default config to {ConfigPath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[TripleDisplayRig] Config problem, using defaults: {e.Message}");
        }
#endif
    }
}
