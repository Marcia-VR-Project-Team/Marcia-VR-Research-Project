using UnityEngine;

/// <summary>
/// When triggered, flips a boolean Animator parameter to trigger an animation to play. Inherits from StimulusBase.cs
/// </summary>
[System.Serializable]
public class AnimationStimulus : StimulusBase
{
    /// <summary>
    /// The string used when logging that an AnimationStimulus has begun playing.
    /// </summary>
    private readonly static string STIMULUS_ANIMATION_START_TEXT = "ANIMATION_START";

    /// <summary>
    /// The string used when logging that an AnimationStimulus has stopped playing.
    /// </summary>
    private readonly static string STIMULUS_ANIMATION_STOP_TEXT = "ANIMATION_STOP";

    /// <summary>
    /// The Animator component used to play an animation when this AnimationStimulus is triggered.
    /// </summary>
    [Header("Animation")]
    [Tooltip("The Animator component used to play the Stimulus' animation. The Stimulus assumes the Animator will be in the idle, untriggered state when the scene is run.")]
    [SerializeField] private Animator animator = null;

    /// <summary>
    /// The name of the boolean parameter in the Animator's AnimationClip that, when flipped, triggers the animation.
    /// </summary>
    [Tooltip("The name of the animation parameter used to trigger the animation.")]
    [SerializeField] private string animationTriggerParameterName = "";

    /// <summary>
    /// If true, this AnimationStimulus will have its Animator returned to the original state upon completion. This flag should only be set to false if the Animator resets itself, or if this AnimationStimulus' ResetTrigger() function is manually called elsewhere.
    /// </summary>
    [Tooltip("If enabled, return this Stimulus' Animator to its original state. Only disable this flag if the Animator resets itself or the Stimulus' ResetTrigger() is called elsewhere.")]
    [SerializeField] private bool resetAfterTrigger = true;

    /// <summary>
    /// If true, this AnimationStimulus will reset immediately after the clip ends. If false, the value specified by the manualAnimationResetDelay will determine when the Animator is reset, but ResetAfterTrigger must be true for this to occur.
    /// </summary>
    [Tooltip("If disabled, use a manual delay to determine when to reset the Animator. Reset After Trigger must be enabled for this to occur.")]
    [SerializeField] private bool resetAfterAnimationEnds = true;

    /// <summary>
    /// If resetAfterAnimationEnds is false, this value specifies the number of seconds to wait before resetting the Animator after the animation finishes playing.
    /// </summary>
    [Tooltip("The wait time in seconds after triggering the stimulus before resetting. This value is only used when Reset After Animation Ends is disabled.")]
    [SerializeField][UnityEngine.Range(0.0f, 120.0f)] private float manualAnimationResetDelay = 0.0f;

    /// <summary>
    /// Stores the default state of the boolean parameter specified by animationTriggerParameterName that this AnimationStimulus controls. AnimationStimulus is agnostic to default state, and when resetting, the parameter will be set back to this value. Initialized on Start().
    /// </summary>
    private bool idleState;


    void Start()
    {
        if (!animator)
        {
            Debug.LogWarning($"No Animator attached to AnimationStimulus on {gameObject.name}. AnimationStimulus cannot be triggered until an Animator is assigned in the inspector.");
            enabled = false;
            return;
        }

        idleState = animator.GetBool(animationTriggerParameterName);
    }

    /// <summary>
    /// Resets the Animator's triggering parameter to idleState and, if specified to do so, logs AnimationStimulus activity and prints debug statements.
    /// </summary>
    public void ResetAnimation()
    {
        if (!animator)
        {
            Debug.LogError($"ResetAnimation called for AnimationStimulus {gameObject.name} without an assigned Animator. Please assign it in the inspector.");
            return;
        }

        // If designated to do so, reset this AnimationStimulus and its animator once the Animator is no longer in transition and following the specified delay.
        if (!resetAfterTrigger) return;

        if (animator.IsInTransition(0))
        {
            Invoke(nameof(this.ResetAnimation), 0.05f);
            return;
        }

        bool state = animator.GetBool(animationTriggerParameterName);
        animator.SetBool(animationTriggerParameterName, !state);

        // Log animation stop
        if (logActivity)
            StimulusLogger.Log(
                STIMULUS_ANIMATION_STOP_TEXT,
                gameObject.name,
                "Auto-Reset",
                $"Parameter: {animationTriggerParameterName}"
            );

        LogDebug($"{gameObject.name} AnimationStimulus' animation has been reset.");
    }

    /// <summary>
    /// Implements StimulusBase.TriggerStimulus(). If the Animator not already playing, this function flips the animation parameter's state from idle to playing and updates the UI Button with completion progress. Also logs this AnimationStimulus' activity and prints debug statements, if specified.
    /// </summary>
    /// <param name="triggerSource">The string representing how this AnimationStimulus was triggered.</param>
    public override void TriggerStimulus(string triggerSource = "Manual/Button")
    {
        if (isStimulusPlaying) return;

        // Log the trigger event
        if (logActivity) StimulusLogger.Log(STIMULUS_START_TEXT, gameObject.name, triggerSource);
        isStimulusPlaying = true;
        Invoke(nameof(ResetPlayingState), GetStimulusDuration());

        if (animator)
        {
            bool state = animator.GetBool(animationTriggerParameterName);

            // Only trigger this stimulus if it is idle.
            if (state != idleState)
                Debug.LogWarning($"Attempted to trigger AnimationStimulus {gameObject.name}, use ResetStimulus() to reset its state.");

            // Trigger the stimulus and log its state to the console.
            state = !state;
            animator.SetBool(animationTriggerParameterName, state);

            // Log animation start
            if (logActivity)
                StimulusLogger.Log(
                    STIMULUS_ANIMATION_START_TEXT,
                    gameObject.name,
                    triggerSource,
                    $"Parameter: {animationTriggerParameterName}"
                );

            LogDebug($"Animation triggered successfully for {gameObject.name} AnimationStimulus.");
        }

        // Reset this stimulus.
        if (animator && resetAfterTrigger)
        {
            float delay = resetAfterAnimationEnds ? GetAnimationLength() : manualAnimationResetDelay;
            Invoke(nameof(ResetAnimation), delay);
        }

        base.TriggerStimulus(triggerSource);
    }

    /// <summary>
    /// Implements StimulusBase.GetStimulusDuration(). Calls GetAnimationLength() if the Animator exists and gets the default StimulusBase value otherwise.
    /// </summary>
    /// <returns>The number of seconds the Animation will last when played, or the default StimulusBase value if no Animator is assigned.</returns>
    public override float GetStimulusDuration()
    {
        return animator ? GetAnimationLength() : base.GetStimulusDuration();
    }

    /// <summary>
    /// Implements StimulusBase.StopStimulus(). Stops playing the animation immediately if it exists and is playing, performs the default StimulusBase stopping logic, and logs the manual stopping event.
    /// </summary>
    public override void StopStimulus()
    {
        if (!animator || !animator.IsInTransition(0)) return;

        base.StopStimulus();
        StopAllCoroutines();
        bool state = animator.GetBool(animationTriggerParameterName);
        animator.SetBool(animationTriggerParameterName, !state);

        // Log animation stop
        if (logActivity) StimulusLogger.Log(STIMULUS_ANIMATION_STOP_TEXT, gameObject.name, "Manual Stop");
    }

    /// <summary>
    /// Helper function to calculate the length of the animation for the assigned Animator by dividing the animation's length by its speed.
    /// </summary>
    /// <returns>The total number of seconds the animation will last when played.</returns>
    private float GetAnimationLength()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.length / stateInfo.speed;
    }
}
