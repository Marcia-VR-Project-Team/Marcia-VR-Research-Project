using UnityEngine;

/// <summary>
/// Singleton component for enabling the stopping of all AnimationStimulus playback, AudioStimulus playback, and StimulusSequences.
/// </summary>
public class StimuliCollector : MonoBehaviour
{
    /// <summary>
    /// The singleton instance is static, meaning a property of the class and not a StimuliCollector obejct, to ensure that only one StimuliCollector can exist in a scene.
    /// </summary>
    private static StimuliCollector _instance;

    /// <summary>
    /// The list of all AnimationStimulus components in the scene. Gathered on Start().
    /// </summary>
    [Tooltip("The list of all AnimationStimulus components in the scene. These are gathered on Start().")]
    [SerializeField] private AnimationStimulus[] animationStimuli;

    /// <summary>
    /// The list of all AudioStimulus components in the scene. Gathered on Start().
    /// </summary>
    [Tooltip("The list of all AudioStimulus components in the scene. These are gathered on Start().")]
    [SerializeField] private AudioStimulus[] audioStimuli;

    /// <summary>
    /// The list of all StimulusSequence components in the scene. Gathered on Start().
    /// </summary>
    [Tooltip("The list of all StimulusSequence components in the scene. These are gathered on Start().")]
    [SerializeField] private StimulusSequence[] sequences;

    void Awake()
    {
        if (_instance == null)
            _instance = this;
        else
            // If a collector already exists, destroy this duplicate
            Destroy(this.gameObject);
    }

    void Start()
    {
        animationStimuli = FindObjectsByType<AnimationStimulus>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        audioStimuli = FindObjectsByType<AudioStimulus>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sequences = FindObjectsByType<StimulusSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    /// <summary>
    /// Iterates over all StimulusSequence components and calls their stopping function.
    /// </summary>
    private void StopSequences()
    {
        if (sequences == null) return;
        foreach (StimulusSequence s in sequences)
            s.StopSequence();
    }

    /// <summary>
    /// Stops all StimulusSequence components because stopping any stimulus in a Sequence cannot currently terminate a StimulusSequence gracefully. Then iterates over all AudioStimulus components and calls their stopping function.
    /// </summary>
    public void StopAllSound()
    {
        StopSequences();
        foreach (AudioStimulus s in audioStimuli)
            if (s.isActiveAndEnabled) s.StopStimulus();

        Debug.Log("All stimuli have ceased playing sound.");
    }

    /// <summary>
    /// Stops all StimulusSequence components because stopping any stimulus in a Sequence cannot currently terminate a StimulusSequence gracefully. Then iterates over all AnimationStimulus components and calls their stopping function.
    /// </summary>
    public void StopAllAnimations()
    {
        StopSequences();
        foreach (AnimationStimulus s in animationStimuli)
            if (s.isActiveAndEnabled) s.StopStimulus();

        Debug.Log("All stimuli have ceased animation and been reset.");
    }

    /// <summary>
    /// Stops all StimulusSequence components, then iterates over all AudioStimulus components and AnimationStimulus components and calls their stopping function. We get to use StopStimulus here because of the abstract base class StimulusBase!
    /// </summary>
    public void StopAllStimuli()
    {
        StopSequences();
        foreach (AnimationStimulus s in animationStimuli)
            if (s.isActiveAndEnabled) s.StopStimulus();
        foreach (AudioStimulus s in audioStimuli)
            if (s.isActiveAndEnabled) s.StopStimulus();
    }
}
