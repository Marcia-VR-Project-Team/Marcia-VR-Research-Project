using UnityEngine;

/// <summary>
/// When triggered, plays a one-shot audio clip from an audio source. Inherits from StimulusBase.cs
/// </summary>
[System.Serializable]
public class AudioStimulus : StimulusBase
{
    /// <summary>
    /// The string used when logging that an AudioStimulus has begun playing.
    /// </summary>
    private readonly static string STIMULUS_AUDIO_START_TEXT = "AUDIO_START";

    /// <summary>
    /// The string used when logging that an AudioStimulus has stopped playing.
    /// </summary>
    private readonly static string STIMULUS_AUDIO_STOP_TEXT = "AUDIO_STOP";

    /// <summary>
    /// The AudioSource component used to play an AudioClip when this AudioStimulus is triggered.
    /// </summary>
    [Header("Audio")]
    [SerializeField, Tooltip("The AudioSource used to play an AudioClip when the stimulus is triggered.")]
    private AudioSource audioSource = null;

    /// <summary>
    /// The Unity AudioClip that is played when this AudioStimulus is triggered.
    /// </summary>
    [SerializeField, Tooltip("The sound played when the stimulus is triggered.")]
    private AudioClip audioClip = null;


    void Start()
    {
        if (!audioSource || !audioClip)
        {
            Debug.LogWarning($"Audio Source {(audioSource ? "" : "not ")}assigned, Audio Clip {(audioClip ? "" : "not ")}assigned to AudioStimulus on {gameObject.name}. Sound will not be played unless an Audio Source and an Audio Clip are assigned in the inspector.");
            enabled = false;
            return;
        }
    }
    
    /// <summary>
    /// Implements StimulusBase.TriggerStimulus(). If this AudioSource is not already playing, this function plays a one shot of the audioClip through the specified AudioSource component and updates the UI Button with completion progress as it plays. Also logs this AudioStimulus' activity and prints debug statements, if specified.
    /// </summary>
    /// <param name="triggerSource">The string representing how this AudioStimulus was triggered.</param>
    public override void TriggerStimulus(string triggerSource = "Manual/Button")
    {
        if (isStimulusPlaying) return;

        // Log the trigger event
        if (logActivity) StimulusLogger.Log(STIMULUS_START_TEXT, gameObject.name, triggerSource);
        isStimulusPlaying = true;
        Invoke(nameof(ResetPlayingState), GetStimulusDuration());

        // If the AudioSource and Sound are specified, play the sound. Otherwise, log their state to the console and don't play the sound.
        if (audioSource && audioClip)
        {
            audioSource.PlayOneShot(audioClip);

            // Log audio start to StimulusLogger
            if (logActivity)
                StimulusLogger.Log(
                    STIMULUS_AUDIO_START_TEXT,
                    gameObject.name,
                    triggerSource,
                    $"Clip: {audioClip.name}, Duration: {audioClip.length}s"
                );

            LogDebug($"Audio triggered successfully for {gameObject.name} Audio Stimulus");
        }

        base.TriggerStimulus(triggerSource);
    }

    /// <summary>
    /// Implements StimulusBase.GetStimulusDuration(). Calculates the number of seconds that this AudioClip will last when played if it exists, and returns the StimulusBase default value if no AudioClip exists.
    /// </summary>
    /// <returns>The number of seconds the audioClip will last when played, or the default StimulusBase value if no AudioClip is assigned.</returns>
    public override float GetStimulusDuration()
    {
        return (audioSource && audioClip) ? audioClip.length : base.GetStimulusDuration();
    }

    /// <summary>
    /// Implements StimulusBase.StopStimulus(). Stops playing the audioClip immediately if it exists and is playing, performs the default StimulusBase stopping logic, and logs the manual stopping event.
    /// </summary>
    public override void StopStimulus()
    {
        if (!audioSource || !audioSource.isPlaying) return;

        StopAllCoroutines();
        base.StopStimulus();
        audioSource.Stop();

        // Log audio stop
        if (logActivity) StimulusLogger.Log(STIMULUS_AUDIO_STOP_TEXT, gameObject.name, "Manual Stop");
    }
}
