using TMPro;
using UnityEngine;

/// <summary>
/// Backwards-compatible front end for the old stimulus log.
///
/// <para>Every existing <c>StimulusLogger.Log(...)</c> call in the Stimulus components still
/// compiles and still works. The entries now go into the session events file alongside input,
/// tracking and scene events, all sharing one UTC timeline.</para>
///
/// <para>This is still a MonoBehaviour purely so the existing scenes do not come up with a
/// missing script where the old logger GameObject was. It does no work itself: the writer
/// thread, the files and the clock all live in <see cref="SessionLogger"/> and
/// <see cref="SessionClock"/>. If this component finds itself in a scene without a
/// <see cref="SessionLogger"/>, it adds one, so an un-migrated scene still records data.</para>
///
/// <para>New code should call <see cref="SessionLogger.Event"/> directly. The intended end state
/// is that this component is deleted from the scenes and this file goes with it.</para>
/// </summary>
public class StimulusLogger : MonoBehaviour
{
    /// <summary>
    /// Unused. Kept only so Unity does not discard the serialized reference when this component
    /// is replaced by <see cref="SessionLogger"/>, which has its own equivalent field.
    /// </summary>
    // These fields are intentionally never read; the pragma keeps the Unity console clean.
#pragma warning disable CS0414
    [Header("Deprecated — configure SessionLogger instead")]
    [SerializeField] private TMP_Text logLocation_TMP_Text = null;

    /// <summary>
    /// Unused. See <see cref="logLocation_TMP_Text"/>.
    /// </summary>
    [SerializeField] private bool OpenExplorerOnApplicationExit = true;
#pragma warning restore CS0414

    void Awake()
    {
        if (GetComponent<SessionLogger>() != null) return;
        if (FindAnyObjectByType<SessionLogger>() != null) return;

        Debug.LogWarning(
            "StimulusLogger is deprecated and has been superseded by SessionLogger. " +
            "No SessionLogger was found in the scene, so one has been added automatically with " +
            "default settings. Add a SessionLogger component explicitly and remove this one, so " +
            "the participant id and output settings can be configured in the inspector.");

        gameObject.AddComponent<SessionLogger>();
    }

    /// <summary>
    /// Records a stimulus event.
    /// </summary>
    /// <param name="eventType">Type of event, e.g. "STIMULUS_START".</param>
    /// <param name="gameObjectName">The GameObject involved.</param>
    /// <param name="triggerSource">What triggered it, e.g. "InputAction: XRI_RightHand_A".</param>
    /// <param name="details">Anything else worth recording.</param>
    public static void Log(string eventType, string gameObjectName, string triggerSource, string details = "")
    {
        // The old signature carried two "where did this come from" fields. They are folded into
        // the source and detail columns rather than adding columns only stimuli would ever use.
        string detail = string.IsNullOrEmpty(details)
            ? $"trigger={triggerSource}"
            : $"trigger={triggerSource}; {details}";

        SessionLogger.Event(eventType, gameObjectName, detail);
    }
}
