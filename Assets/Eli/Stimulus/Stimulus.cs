using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[System.Serializable, System.Obsolete("The Stimulus component is deprecated. It is no longer compatible with StimulusSequence, StimulusActionTrigger, or StimuliCollector. Please use AnimationStimulus for animation-based stimuli and AudioStimulus for auditory stimuli.")]
public class Stimulus : MonoBehaviour
{
    // Define logging constants
    private readonly static string STIMULUS_ACTION_TRIGGERED_TEXT = "STIMULUS_TRIGGERED_INPUTACTION";
    private readonly static string STIMULUS_START_TEXT = "STIMULUS_START";
    private readonly static string STIMULUS_ANIMATION_START_TEXT = "ANIMATION_START";
    private readonly static string STIMULUS_AUDIO_START_TEXT = "AUDIO_START";
    private readonly static string STIMULUS_AUDIO_STOP_TEXT = "AUDIO_STOP";
    private readonly static string STIMULUS_ANIMATION_STOP_TEXT = "ANIMATION_STOP";

    [Header("Animation")]
    [Tooltip("The animator used to play the Stimulus' animation. The Stimulus assumes the animator will be in the idle, untriggered state when the scene is run.")]
    [SerializeField] private Animator animator = null;

    [Tooltip("The name of the animation parameter used to trigger the animation.")]
    [SerializeField] private string animationTriggerParameterName = "";

    [Tooltip("If enabled, return this Stimulus' animator to its original state. Only disable this flag if the animator resets itself or the Stimulus' ResetTrigger() is called elsewhere.")]
    [SerializeField] private bool resetAfterTrigger = true;

    [Tooltip("If disabled, use a manual delay to determine when to reset the animator. NOTE: Reset After Trigger must be enabled to reset the animator.")]
    [SerializeField] private bool resetAfterAnimationEnds = true;

    [Tooltip("The wait time in seconds after triggering the stimulus before resetting. ")]
    [SerializeField] [UnityEngine.Range(0.0f, 120.0f)] private float manualAnimationResetDelay = 0.0f;

    // The default state of the animator. Assumes the animator's default state is idle.
    private bool idleState;
    
    
    [Header("Audio")]
    [Tooltip("The audio source used to play an audio clip when the stimulus is triggered.")]
    [SerializeField] private AudioSource audioSource = null;

    [Tooltip("The sound played when the simulus is triggered.")]
    [SerializeField] private AudioClip stimulusSound = null;

    [Header("UI Integration")]
    [Tooltip("The UI Button that displays the information about this Stimulus.")]
    [SerializeField] private Button button;

    [Tooltip("The GameObject tag corresponding to the TextMeshPro component containing the title of the Stimulus.")]
    [SerializeField] private string titleTag = "StimulusTitle";
    
    [Tooltip("The GameObject tag corresponding to the TextMeshPro component containing the Input Action used to trigger this stimulus, if one is present.")]
    [SerializeField] private string inputTextTag = "StimulusText";
    // The stimulus action trigger used to get the string name of the action.
    private StimulusActionTrigger stimulusActionTrigger;
    // The TMP Text component containing the title on its button.
    private TMP_Text tmpTitleText;
    // The TMP Text component containing the action to use to trigger the Stimulus.
    private TMP_Text tmpActionText;
    // The TMP Text component containing the "Use Action" text.
    private TMP_Text useActionText;
    // The default action string used to show how to trigger the Stimulus.
    private string defaultActionString = "";

    // Used to track whether the stimulus is currently in progress.
    private bool isStimulusPlaying = false;
    
    // Track if we're currently showing trigger completion progress.
    private bool isShowingProgress = false;
    private Coroutine progressCoroutine = null;

    [Header("Logging")]
    [SerializeField] private bool logActivity = true;

    [Header("Debug")]
    [SerializeField] private bool printDebugStatements = false;


    // Used to track the presence of the Animator, the Audio Source, and the Stimulus Sound.
    private bool hasAnimator;
    private bool hasAudioSource;
    private bool hasStimulusSound;

    private void LogDebug(string s)
    {
        if (printDebugStatements) Debug.Log(s);
    }

    void Start()
    {
        // Animation and audio verification.
        hasAnimator = animator != null;
        hasAudioSource = audioSource != null;
        hasStimulusSound = stimulusSound != null;

        if (!hasAnimator && !hasAudioSource) 
        {
            Debug.LogWarning($"No animator or audio source attached to Stimulus on {gameObject.name}. Stimulus cannot be triggered until an animator or audio source and audio clip are assigned in the inspector.");
            enabled = false;
            return;
        }

        if (hasAnimator)
            idleState = animator.GetBool(animationTriggerParameterName);

        if (hasAudioSource && !hasStimulusSound)
            Debug.LogWarning($"Audio Source assigned to Stimulus on {gameObject.name} without assigned Stimulus sound. Sound will not be played unless assigned in the inspector.");
        else if (!hasAudioSource || !hasStimulusSound)
             Debug.LogWarning($"Audio Source {(hasAudioSource ? "not set" : "set")}, Stimulus Sound {(hasStimulusSound ? "not set" : "set")}, not playing sound for {gameObject.name}.");

        SetupButton();
    }

    // If this component is attached to a UI Button, assign the button's text values.
    private void SetupButton()
    {
        // Get the TextMeshPro component references for assigning their values.
        if (button == null) return;
        button.onClick.AddListener(() => TriggerStimulus($"Button: {button.gameObject.name}"));
        stimulusActionTrigger = GetComponent<StimulusActionTrigger>();

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>();
        foreach (var t in texts)
        {
            if (t.CompareTag(titleTag)) tmpTitleText = t;
            else if (t.CompareTag(inputTextTag)) tmpActionText = t;
            else useActionText = t;
        }

        // Assign the title text and the text for the key to be pressed.
        if (tmpTitleText == null) Debug.LogWarning($"Button has been assigned to Stimulus on {name}, but it contains no TextMeshPro Text components in children tagged with {titleTag}. Please ensure {button.name} has a child GameObject tagged with {titleTag} and containing a TextMeshPro Text component.");
        else tmpTitleText.text = gameObject.name;

        if (tmpActionText == null) 
            Debug.LogWarning($"Button has been assigned to Stimulus on {name}, but it contains no TextMeshPro Text components in children tagged with {inputTextTag}. Please ensure {button.name} has a child GameObject tagged with {inputTextTag} and containing a TextMeshPro Text component.");
        else
        {
            if (stimulusActionTrigger != null)
            {
                defaultActionString = $"Click Button OR \"{stimulusActionTrigger.toggleAction.name}\"";
                tmpActionText.text = defaultActionString;
            }
            else
            {
                defaultActionString = "Click Button";
                tmpActionText.text = defaultActionString;
            }
        }
    }

    // Input Action-based callback for triggering this Stimulus.
    public void OnTriggerStimulus(InputAction.CallbackContext context)
    {
        string triggerSource = $"InputAction: {context.action.name}";
        if (logActivity) StimulusLogger.Log(STIMULUS_ACTION_TRIGGERED_TEXT, gameObject.name, triggerSource);

        LogDebug($"Stimulus triggered for {gameObject.name} by Input Action {context.action.name}");
        TriggerStimulus(triggerSource);
    }

    // Trigger the Stimulus and set the animator's trigger parameter to triggered. Only call if the animation has been reset.
    public void TriggerStimulus(string triggerSource = "Manual/Button")
    {
        if (isStimulusPlaying) return;
        
        // Log the trigger event
        if (logActivity) StimulusLogger.Log(STIMULUS_START_TEXT, gameObject.name, triggerSource);
        isStimulusPlaying = true;
        Invoke(nameof(StimulusNotPlaying), GetStimulusDuration());

        if (hasAnimator)
        {
            bool state = animator.GetBool(animationTriggerParameterName);
            
            // Only trigger this Stimulus if it is idle. 
            if (state != idleState) 
                Debug.LogWarning($"Attempted to trigger animation for Stimulus {gameObject.name}, use ResetStimulus() to reset its state.");

            // Trigger the Stimulus and log its state to the console.
            state = !state;
            animator.SetBool(animationTriggerParameterName, state);

            // Log animation start
            if (logActivity) StimulusLogger.Log(STIMULUS_ANIMATION_START_TEXT, gameObject.name, triggerSource,
                $"Parameter: {animationTriggerParameterName}");

            LogDebug($"Animation triggered successfully for {gameObject.name} Stimulus.");
        }

        // If the AudioSource and Sound are specified, play the sound. Otherwise, log their state to the console and don't play the sound.
        if (hasAudioSource && hasStimulusSound)
        {
            audioSource.PlayOneShot(stimulusSound);

            // Log audio start
            if (logActivity) StimulusLogger.Log(STIMULUS_AUDIO_START_TEXT, gameObject.name, triggerSource,
                $"Clip: {stimulusSound.name}, Duration: {stimulusSound.length}s");

            LogDebug($"Audio triggered successfully for {gameObject.name} Stimulus.");
        }  

        // Reset this stimulus.
        if (hasAnimator && resetAfterTrigger) 
        {
            float delay = resetAfterAnimationEnds ? GetAnimationLength() : manualAnimationResetDelay;
            Invoke(nameof(this.ResetAnimation), delay);
        }

        // Start progress display if button text exists
        if (tmpActionText != null && !isShowingProgress)
        {
            if (progressCoroutine != null)
                StopCoroutine(progressCoroutine);
            progressCoroutine = StartCoroutine(UpdateProgressText());
        }
    }

    private void StimulusNotPlaying()
    {
        isShowingProgress = false;
    }

    // Preconditions: hasAnimator == true
    private float GetAnimationLength()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.length / stateInfo.speed;
    }

    // Calculate the longest duration, between the animation and the sound clip. 
    public float GetStimulusDuration()
    {
        float animDuration = hasAnimator ? GetAnimationLength() : 0f;
        float soundDuration = (hasAudioSource && hasStimulusSound) ? stimulusSound.length : 0f;

        return Mathf.Max(animDuration, soundDuration);
    }

    // Update the completion progress on the UI.
    private IEnumerator UpdateProgressText()
    {
        if (tmpActionText == null) yield break;

        isShowingProgress = true;
        float duration = GetStimulusDuration();
        float elapsed = 0f;

        useActionText.text = "Progress:";
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percentage = Mathf.Clamp01(elapsed / duration) * 100f;
            tmpActionText.text = $"{percentage:F0}%";
            yield return null;
        }

        // Restore original button state
        ResetButton();
    }

    private void ResetButton()
    {
        if (button == null) return;
        useActionText.text = "Use Action";
        tmpActionText.text = defaultActionString;
        isShowingProgress = false;
    }
    
    // Reset the animator's parameter and the local flag.
    public void ResetAnimation()
    {
        if (!hasAnimator)
        {
            Debug.LogError($"ResetAnimation called for Stimulus {gameObject.name} without an assigned Animator. Please assign it in the inspector.");
            return;
        }

        // If designated to do so, reset this Stimulus and its animator once the animator is no longer in transition and following the specified delay.
        if (!resetAfterTrigger) return;

        if (animator.IsInTransition(0))
        {
            Invoke(nameof(this.ResetAnimation), 0.05f);
            return;
        }

        bool state = animator.GetBool(animationTriggerParameterName);
        animator.SetBool(animationTriggerParameterName, !state);

        // Log animation stop
        if (logActivity) StimulusLogger.Log(STIMULUS_ANIMATION_STOP_TEXT, gameObject.name, "Auto-Reset",
            $"Parameter: {animationTriggerParameterName}");

        LogDebug($"{gameObject.name} Stimulus animation has been reset");
    }

    public void StopSound()
    {
        if (hasAudioSource && audioSource.isPlaying) 
        {
            ResetButton();
            // StopAllCoroutines();
            audioSource.Stop();

            // Log audio stop
            if (logActivity) StimulusLogger.Log(STIMULUS_AUDIO_STOP_TEXT, gameObject.name, "Manual Stop");
        }
    }

    public void StopAnimation()
    {
        if (!hasAnimator || !animator.IsInTransition(0)) return;
        
        // StopAllCoroutines();
        ResetButton();
        bool state = animator.GetBool(animationTriggerParameterName);
        animator.SetBool(animationTriggerParameterName, !state);

        // Log animation stop
        if (logActivity) StimulusLogger.Log(STIMULUS_ANIMATION_STOP_TEXT, gameObject.name, "Manual Stop");
    }

    public void StopEverything()
    {
        StopAllCoroutines();
        ResetButton();
        StopSound();
        StopAnimation();
    }
}
