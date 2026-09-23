using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

/// <summary>
/// Samples head and hand pose at a fixed rate and hands each sample to
/// <see cref="SessionLogger"/> for writing.
///
/// <para><b>An honest note about threading.</b> The file I/O genuinely runs on its own thread —
/// see <see cref="SessionLogger"/>. Reading the poses does not, and cannot: <c>Transform</c>,
/// <c>Camera</c> and the XR subsystems all throw if touched from a background thread, because
/// Unity's scene state is not thread-safe. So this component reads poses on the main thread and
/// immediately hands them off.</para>
///
/// <para>That still gets us what matters. The read itself is a few field accesses costing
/// microseconds, and nothing blocks on the disk. What it means in practice is that the sample
/// rate cannot exceed the frame rate: ask for 90 Hz on a headset running at 72 Hz and you will
/// get 72. Each row carries the UTC time it was actually read at, so irregular spacing is
/// visible in the data rather than hidden — never assume the rows are evenly spaced, resample
/// against the timestamp column.</para>
/// </summary>
public class TrackingSampler : MonoBehaviour
{
    /// <summary>
    /// The participant's head. Normally the XR rig's Main Camera. If left empty, the sampler
    /// falls back to <see cref="Camera.main"/>.
    /// </summary>
    [Header("What to sample")]
    [Tooltip("The head transform, normally the XR rig's Main Camera. Defaults to Camera.main.")]
    [SerializeField] private Transform head;

    /// <summary>
    /// The left hand or left controller anchor.
    /// </summary>
    [Tooltip("Left hand / controller transform.")]
    [SerializeField] private Transform leftHand;

    /// <summary>
    /// The right hand or right controller anchor.
    /// </summary>
    [Tooltip("Right hand / controller transform.")]
    [SerializeField] private Transform rightHand;

    /// <summary>
    /// Anything else worth tracking — a tracked object, an NPC, the room root. Each is logged
    /// under its GameObject name.
    /// </summary>
    [Tooltip("Any other transforms to sample. Logged under their GameObject names.")]
    [SerializeField] private List<Transform> additionalTargets = new List<Transform>();

    /// <summary>
    /// Target samples per second. 50 Hz is a reasonable default: fine enough to resolve a head
    /// turn or a reach, and comfortably below any headset's frame rate. The real rate is capped
    /// by the frame rate, as explained on the class.
    /// </summary>
    [Header("Rate")]
    [Tooltip("Target samples per second. Capped by the frame rate in practice.")]
    [Range(1f, 120f)]
    [SerializeField] private float samplesPerSecond = 50f;

    /// <summary>
    /// If true, also samples every finger joint of both hands via the XR Hands subsystem.
    ///
    /// <para>This is a lot of data — 26 joints per hand, so 52 extra rows per sample, roughly
    /// 100x the volume of head-and-hands alone. Only turn it on if the study actually analyses
    /// finger posture, and expect log files in the hundreds of megabytes.</para>
    /// </summary>
    [Header("Hand joints")]
    [Tooltip("Sample all finger joints. WARNING: ~52 extra rows per sample. Very large files.")]
    [SerializeField] private bool sampleHandJoints = false;

    /// <summary>
    /// How far a target must move before a new row is written, in metres. Anything smaller counts
    /// as "hasn't moved".
    ///
    /// <para>This is what stops the log filling with identical rows. A head that is genuinely
    /// still — a participant reading, or the editor running with no headset attached — produces
    /// the same pose every frame, and writing it 50 times a second is noise that hides the
    /// moments where something actually happened. 1&#160;mm is well below meaningful head or hand
    /// movement and well above tracking jitter.</para>
    /// </summary>
    [Header("Duplicate suppression")]
    [Tooltip("Minimum movement before a new row is written, in metres. 0 logs every sample.")]
    [SerializeField] private float minPositionDelta = 0.001f;

    /// <summary>
    /// How far a target must rotate before a new row is written, in degrees. See
    /// <see cref="minPositionDelta"/>.
    /// </summary>
    [Tooltip("Minimum rotation before a new row is written, in degrees. 0 logs every sample.")]
    [SerializeField] private float minRotationDelta = 0.1f;

    /// <summary>
    /// Write a row even when nothing moved, if this long has passed since the target's last row.
    ///
    /// <para>This matters more than it looks. Without it, a gap in the data would be ambiguous:
    /// it could mean the participant held still, or it could mean tracking dropped out, and no
    /// analysis could tell the two apart after the fact. A periodic row while stationary makes
    /// "still present, not moving" an explicit statement in the file, so a genuine gap means
    /// something went wrong.</para>
    /// </summary>
    [Tooltip("Log an unchanged pose at least this often, in seconds, so gaps stay meaningful.")]
    [SerializeField] private float maxHoldSeconds = 1f;

    /// <summary>
    /// The last pose written for each target, and when, used to decide whether a new sample is
    /// worth a row. Keyed by the same target name that appears in the log.
    /// </summary>
    private readonly Dictionary<string, (Vector3 pos, Quaternion rot, double time)> lastLogged =
        new Dictionary<string, (Vector3, Quaternion, double)>();

    /// <summary>
    /// The XR Hands subsystem, used only when <see cref="sampleHandJoints"/> is on. Found lazily
    /// because it does not exist until the XR subsystems have started.
    /// </summary>
    private XRHandSubsystem handSubsystem;

    /// <summary>
    /// Seconds between samples, derived from <see cref="samplesPerSecond"/>.
    /// </summary>
    private double sampleInterval;

    /// <summary>
    /// The Unity time the next sample is due.
    ///
    /// <para>This advances by exactly one interval per sample rather than being reset to "now",
    /// so a late frame does not permanently push the schedule back. Without that, small delays
    /// accumulate and the effective rate drifts below the requested one over a long session.</para>
    /// </summary>
    private double nextSampleTime;

    /// <summary>
    /// Cached list of joints to read, so the array is not rebuilt every sample.
    /// </summary>
    private static readonly XRHandJointID[] AllJoints = BuildJointList();

    /// <summary>
    /// The names searched for when locating the hands automatically. These match the rig prefab
    /// "XR Origin Hands (XR Rig)".
    /// </summary>
    /// <para>Several names are tried in order, because the project uses more than one rig and
    /// they do not agree: "XR Origin Hands (XR Rig)" calls them "Left Hand" / "Right Hand", while
    /// "Student XR Rig" calls them "Left Controller" / "Right Controller". Searching for a single
    /// name silently found nothing in half the scenes.</para>
    [Header("Auto-discovery")]
    [Tooltip("Names tried, in order, when Left Hand is left empty.")]
    [SerializeField]
    private string[] leftHandNames = { "Left Hand", "Left Controller", "LeftHand" };

    /// <summary>
    /// See <see cref="leftHandNames"/>.
    /// </summary>
    [Tooltip("Names tried, in order, when Right Hand is left empty.")]
    [SerializeField]
    private string[] rightHandNames = { "Right Hand", "Right Controller", "RightHand" };

    void Start()
    {
        // The rig is a prefab, and its camera sits inside a further nested prefab, so a
        // serialized reference to it cannot live on a standalone logging prefab. Resolving at
        // runtime instead keeps this component drop-in: add the prefab to any scene that has the
        // rig and it works, with nothing to wire in the inspector.
        AutoDiscover();

        sampleInterval = 1.0 / samplesPerSecond;
        nextSampleTime = Time.timeAsDouble;

        SessionLogger.Event("TRACKING_START", nameof(TrackingSampler),
            $"target_hz={samplesPerSecond}; joints={sampleHandJoints}; " +
            $"head={(head != null)}; left={(leftHand != null)}; right={(rightHand != null)}");
    }

    /// <summary>
    /// Fills in any target left empty in the inspector by searching the XR rig.
    ///
    /// <para>Anything that could not be found is reported as a warning AND recorded in the log
    /// itself. A silently missing hand is the worst outcome here — you would not notice until
    /// after the session, with the participant gone and the data unrepeatable — so the failure
    /// is written into the same file the analysis reads.</para>
    /// </summary>
    private void AutoDiscover()
    {
        Transform rigRoot = null;

        if (head == null && Camera.main != null)
        {
            head = Camera.main.transform;

            // The camera is parented deep inside the rig, so its root is the rig, which is where
            // the hands live too.
            rigRoot = head.root;
        }
        else if (head != null)
        {
            rigRoot = head.root;
        }

        if (leftHand == null) leftHand = FindAny(rigRoot, leftHandNames);
        if (rightHand == null) rightHand = FindAny(rigRoot, rightHandNames);

        Report("head", head);
        Report("left hand", leftHand);
        Report("right hand", rightHand);

        // Finding the same transform for two targets means the search matched something generic
        // rather than the actual hands, which would silently produce duplicate columns of data
        // that look plausible. Better to say so now than to discover it during analysis.
        WarnIfSame("head", head, "left hand", leftHand);
        WarnIfSame("head", head, "right hand", rightHand);
        WarnIfSame("left hand", leftHand, "right hand", rightHand);
    }

    /// <summary>
    /// Returns the first descendant matching any of the candidate names.
    /// </summary>
    private static Transform FindAny(Transform root, string[] names)
    {
        if (names == null) return null;

        foreach (string n in names)
        {
            Transform found = FindDescendant(root, n);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Warns, in the console and in the log, when two targets resolved to the same transform.
    /// </summary>
    private static void WarnIfSame(string labelA, Transform a, string labelB, Transform b)
    {
        if (a == null || b == null || a != b) return;

        Debug.LogWarning($"TrackingSampler resolved {labelA} and {labelB} to the same transform " +
                         $"('{a.name}'). Their logged poses will be identical. Assign them " +
                         "explicitly in the inspector.");

        SessionLogger.Event("TRACKING_TARGET_AMBIGUOUS", nameof(TrackingSampler),
            $"{labelA} and {labelB} both resolved to '{a.name}'");
    }

    /// <summary>
    /// Warns, and records in the log, when a target could not be resolved.
    /// </summary>
    private static void Report(string label, Transform found)
    {
        if (found != null) return;

        Debug.LogWarning($"TrackingSampler could not find the {label}. It will not be logged.");
        SessionLogger.Event("TRACKING_TARGET_MISSING", nameof(TrackingSampler), $"target={label}");
    }

    /// <summary>
    /// Depth-first search for a descendant with the given name. Unity's
    /// <c>Transform.Find</c> only walks a single path, and the hands sit at a depth that varies
    /// between rig versions, so the search is done by hand.
    /// </summary>
    /// <param name="root">Where to start. Null yields null.</param>
    /// <param name="name">The exact GameObject name to match.</param>
    private static Transform FindDescendant(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name)) return null;
        if (root.name == name) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), name);
            if (found != null) return found;
        }

        return null;
    }

    void Update()
    {
        double now = Time.timeAsDouble;
        if (now < nextSampleTime) return;

        // If the app hitched badly, several intervals may have elapsed. Skip the missed ones
        // rather than firing a burst of samples that all share nearly the same real timestamp:
        // the gap is real and should show up as a gap in the data.
        if (now - nextSampleTime > sampleInterval * 4)
            nextSampleTime = now;

        nextSampleTime += sampleInterval;

        SampleOnce();
    }

    /// <summary>
    /// Reads every configured pose and queues it. One UTC timestamp is taken for the whole
    /// sample so that the head and hand rows from a single frame share an identical time and can
    /// be grouped without a tolerance window.
    /// </summary>
    private void SampleOnce()
    {
        System.DateTime utc = SessionClock.UtcNow;
        double unityTime = Time.timeAsDouble;

        if (head != null)
            TrackIfChanged("Head", utc, unityTime, head.position, head.rotation);

        if (leftHand != null)
            TrackIfChanged("LeftHand", utc, unityTime, leftHand.position, leftHand.rotation);

        if (rightHand != null)
            TrackIfChanged("RightHand", utc, unityTime, rightHand.position, rightHand.rotation);

        for (int i = 0; i < additionalTargets.Count; i++)
        {
            Transform t = additionalTargets[i];
            if (t != null)
                TrackIfChanged(t.name, utc, unityTime, t.position, t.rotation);
        }

        if (sampleHandJoints) SampleHandJoints(utc, unityTime);
    }

    /// <summary>
    /// Writes a pose row only if the target has actually moved since its last row, or if it has
    /// been stationary long enough to be worth restating.
    /// </summary>
    /// <param name="target">The name this target is logged under.</param>
    /// <param name="utc">Timestamp shared by this whole sample.</param>
    /// <param name="unityTime">Unity's time at this sample.</param>
    /// <param name="position">World-space position.</param>
    /// <param name="rotation">World-space rotation.</param>
    private void TrackIfChanged(string target, System.DateTime utc, double unityTime,
                                Vector3 position, Quaternion rotation)
    {
        if (lastLogged.TryGetValue(target, out var prev))
        {
            bool moved = (position - prev.pos).sqrMagnitude > minPositionDelta * minPositionDelta;

            // Quaternion.Angle gives the actual angle between two orientations, which is what
            // "has it turned enough to care" means. Comparing components would treat the two
            // quaternions q and -q as different despite being the same rotation.
            bool turned = Quaternion.Angle(prev.rot, rotation) > minRotationDelta;

            bool overdue = unityTime - prev.time >= maxHoldSeconds;

            if (!moved && !turned && !overdue) return;
        }

        lastLogged[target] = (position, rotation, unityTime);
        SessionLogger.Track(target, utc, unityTime, position, rotation);
    }

    /// <summary>
    /// Reads every tracked finger joint of both hands from the XR Hands subsystem.
    /// </summary>
    /// <param name="utc">The timestamp shared by this whole sample.</param>
    /// <param name="unityTime">Unity's time at this sample.</param>
    private void SampleHandJoints(System.DateTime utc, double unityTime)
    {
        if (handSubsystem == null || !handSubsystem.running)
        {
            handSubsystem = FindRunningHandSubsystem();
            if (handSubsystem == null) return;
        }

        SampleHand(handSubsystem.leftHand, "LeftHand", utc, unityTime);
        SampleHand(handSubsystem.rightHand, "RightHand", utc, unityTime);
    }

    /// <summary>
    /// Reads one hand's joints. Joints whose pose is not currently tracked are skipped rather
    /// than logged as zeros, so missing data is absent instead of wrong.
    /// </summary>
    private void SampleHand(XRHand hand, string label, System.DateTime utc, double unityTime)
    {
        if (!hand.isTracked) return;

        foreach (XRHandJointID id in AllJoints)
        {
            XRHandJoint joint = hand.GetJoint(id);
            if (!joint.TryGetPose(out Pose pose)) continue;

            TrackIfChanged($"{label}.{id}", utc, unityTime, pose.position, pose.rotation);
        }
    }

    /// <summary>
    /// Finds the active XR Hands subsystem, if one is running.
    /// </summary>
    private static XRHandSubsystem FindRunningHandSubsystem()
    {
        var subsystems = new List<XRHandSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);

        foreach (XRHandSubsystem s in subsystems)
            if (s.running) return s;

        return null;
    }

    /// <summary>
    /// Builds the list of every real joint id, excluding the Invalid sentinel and the
    /// BeginMarker/EndMarker bookends of the enum.
    /// </summary>
    private static XRHandJointID[] BuildJointList()
    {
        var ids = new List<XRHandJointID>();

        for (int i = (int)XRHandJointID.BeginMarker; i < (int)XRHandJointID.EndMarker; i++)
        {
            var id = (XRHandJointID)i;
            if (id != XRHandJointID.Invalid) ids.Add(id);
        }

        return ids.ToArray();
    }
}
