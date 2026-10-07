using UnityEngine;

/// <summary>
/// Parents this object to the XR camera at runtime and holds it at a fixed offset in front of
/// the participant's face.
///
/// <para><b>Why this is needed.</b> A Screen Space - Overlay canvas does not appear in a VR
/// headset at all: overlay canvases are composited onto the main display, not into the stereo
/// render. So anything meant to cover the participant's view — a fade to black, a dialogue box —
/// has to be a world-space canvas positioned in front of the camera.</para>
///
/// <para>The camera itself lives inside a nested prefab (the XR Origin inside
/// "XR Origin Hands (XR Rig)"), so it cannot be dragged onto a component that lives outside that
/// prefab and have the reference survive. Resolving it at runtime sidesteps that entirely, the
/// same way <see cref="TrackingSampler"/> does.</para>
/// </summary>
public class AttachToHead : MonoBehaviour
{
    /// <summary>
    /// Where to sit relative to the camera, in metres. Z is forward, so the default places the
    /// object 30 cm in front of the eyes — close enough that a full-screen fade covers the whole
    /// field of view.
    /// </summary>
    [Tooltip("Offset from the camera in metres. +Z is in front of the participant.")]
    [SerializeField] private Vector3 localPosition = new Vector3(0f, 0f, 0.3f);

    /// <summary>
    /// Rotation relative to the camera. The default faces the object back toward the eyes.
    /// </summary>
    [Tooltip("Rotation relative to the camera, in degrees.")]
    [SerializeField] private Vector3 localEulerAngles = Vector3.zero;

    /// <summary>
    /// If true, keeps re-applying the offset every frame. Only needed if something else moves
    /// this object; parenting alone already makes it follow the head.
    /// </summary>
    [Tooltip("Re-apply the offset every frame. Normally unnecessary.")]
    [SerializeField] private bool holdEveryFrame = false;

    void Start()
    {
        Camera head = Camera.main;

        if (head == null)
        {
            // Left unparented rather than hidden: a visible object in the wrong place is a much
            // faster thing to diagnose than one that silently never renders.
            Debug.LogWarning(
                $"{name}: AttachToHead found no Camera.main, so it stays where it is in the " +
                "scene. In VR it will probably not be visible.");
            return;
        }

        transform.SetParent(head.transform, worldPositionStays: false);
        Apply();
    }

    void LateUpdate()
    {
        // LateUpdate rather than Update, so the offset is applied after the head pose has been
        // written for this frame. Doing it in Update would leave the object one frame behind.
        if (holdEveryFrame) Apply();
    }

    /// <summary>
    /// Snaps the object back to its configured offset from the camera.
    /// </summary>
    private void Apply()
    {
        transform.localPosition = localPosition;
        transform.localRotation = Quaternion.Euler(localEulerAngles);
    }
}
