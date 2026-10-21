using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using System.Collections;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Contains a list of StimulusBase components that will be triggered in a linear order with an optional delay after it ends before triggering the next.
/// </summary>
public class StimulusSequence : MonoBehaviour
{
    /// <summary>
    /// A wrapper class with a StimulusBase and a float delay representing the step units of a StimulusSequence.
    /// </summary>
    [System.Serializable]
    public class Step
    {
        /// <summary>
        /// The StimulusBase to be triggered in this sequence. Because AudioStimulus and AnimationStimulus both inherit from StimulusBase, we can simply call TriggerStimulus() on any stimulus! Nice and easy.
        /// </summary>
        [Tooltip("The Stimulus to be triggered in this step.")]
        public StimulusBase stimulus = null;

        /// <summary>
        /// specifies how long to wait after this Step's StimulusBase finishes playing before triggering the next Step. This value is always used.
        /// </summary>
        [Tooltip("The delay time in seconds after triggering the stimulus in this step.")]
        [Range(0f, 120f)] public float delayAfter = 0f;
    }

    /// <summary>
    ///  The list of Steps to be triggered in sequence.
    /// </summary>
    [Header("Sequencing")]
    [Tooltip("Add steps here to be triggered in sequence.")]
    [SerializeField] public List<Step> stimuli;

    /// <summary>
    /// The UI button displaying information about this StimulusSequence. Must be an instance of the Prefab "Stimulus Button" found in Assets->Eli.
    /// </summary>
    [Header("UI Integration")]
    [Tooltip("The UI Button that displays the information about this Stimulus Sequence. Ensure it is an instance of the Prefab \"Stimulus Button\" found in Assets->Eli.")]
    [SerializeField] private Button button;

    /// <summary>
    /// The Unity GameObject Tag corresponding to that of the TextMeshPro component containing the title of this StimulusSequence.
    /// </summary>
    [Tooltip("The GameObject tag corresponding to the TextMeshPro component containing the title of the Stimulus Sequence.")]
    [SerializeField] private string titleTag = "StimulusTitle";
    
    /// <summary>
    /// The Unity GameObject Tag corresponding to that of the TextMeshPro component containing The Input Action used to trigger this StimulusSequence, if one is present.
    /// </summary>
    [Tooltip("The GameObject tag corresponding to the TextMeshPro component containing the Input Action used to trigger this Stimulus Sequence, if one is present.")]
    [SerializeField] private string inputTextTag = "StimulusText";

    /// <summary>
    /// The StimulusActionTrigger component possessing the name of the Input Action used to trigger this StimulusSequence.
    /// </summary>
    private StimulusActionTrigger stimulusActionTrigger;

    /// <summary>
    /// The TextMeshPro Text component of this StimulusSequence's UI Button, used to display the name of this StimulusSequence.
    /// </summary>
    private TMP_Text tmpTitleText;

    /// <summary>
    /// The TextMeshPro Text component of this StimulusSequence's UI Button, used to display the action to use to trigger the StimulusSequence.
    /// </summary>
    private TMP_Text tmpActionText;

    /// <summary>
    /// The TextMeshPro Text component of this StimulusSequence's UI Button, used to display the "Use Action" text.
    /// </summary>
    private TMP_Text useActionText;

    /// <summary>
    /// The default string displayed via useActionText showing how to trigger this StimulusSequence.
    /// </summary>
    private string defaultActionString = "";
    

    /// <summary>
    /// Used to track whether this StimulusSequence is current showing its completion progress through its UI Button.
    /// </summary>
    private bool isShowingProgress = false;

    /// <summary>
    /// Contains the UnityEngine Coroutine that handles updates to the progress display on this StimulusSequence's UI Button.
    /// </summary>
    private Coroutine progressCoroutine = null;

    /// <summary>
    /// Determines whether this StimulusSequence will print debug activity to the console. True to print, false to not print. Use when having problems setting up the StimulusSequence.
    /// </summary>
    [Header("Debug")]
    [SerializeField] private bool printDebugStatements = false;

    /// <summary>
    /// A short wrapper function for printing debug statements if the printDebugStatements flag of this StimulusSequence is set to true.
    /// </summary>
    /// <param name="s">The string to be logged with Debug.Log().</param>
    private void LogDebug(string s)
    {
        if (printDebugStatements) Debug.Log(s);
    }

    void Start()
    {
        SetupButton();
    }

    /// <summary>
    /// Sets the title and trigger text on the UI Button for this StimulusSequence if it has been assigned.
    /// </summary>
    private void SetupButton()
    {
        // Get the TextMeshPro component references for assigning their values.
        if (button == null) return;
        button.onClick.AddListener(TriggerStimulusSequence);
        stimulusActionTrigger = GetComponent<StimulusActionTrigger>();

        TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>();
        foreach (var t in texts)
        {
            if (t.CompareTag(titleTag)) tmpTitleText = t;
            else if (t.CompareTag(inputTextTag)) tmpActionText = t;
            else useActionText = t;
        }

        // Assign the title text and the text for the key to be pressed.
        if (tmpTitleText == null) Debug.LogWarning($"Button has been assigned to Stimulus Sequence on {name}, but it contains no TextMeshPro Text components in children tagged with {titleTag}. Please ensure {button.name} has a child GameObject tagged with {titleTag} and containing a TextMeshPro Text component.");
        else tmpTitleText.text = $"{gameObject.name} (Seq.)";

        if (tmpActionText == null) 
            Debug.LogWarning($"Button has been assigned to Stimulus Sequence on {name}, but it contains no TextMeshPro Text components in children tagged with {inputTextTag}. Please ensure {button.name} has a child GameObject tagged with {inputTextTag} and containing a TextMeshPro Text component.");
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

    /// <summary>
    /// This function allows a Unity InputAction to trigger this StimulusSequence with information about the InputAction that triggered it. It attempts to log the stimulus activity and print a debug statement, but most importantly, calls TriggerStimulusSequence() on this StimulusSequence.
    /// </summary>
    /// <param name="context">The context generated by an InputAction set to trigger this StimulusSequence when that action is performed.</param>
    public void OnTriggerStimulusSequence(InputAction.CallbackContext context)
    {
        LogDebug($"Stimulus Sequence triggered for {gameObject.name} by Input Action {context.action.name}");
        TriggerStimulusSequence();
    }

    /// <summary>
    /// Begins a coroutine to execute all the Steps for this StimulusSequence in order, and initates thr UI Button completion progress update loop.
    /// </summary>
    public void TriggerStimulusSequence()
    {
        StartCoroutine(ExecuteSequence());

        // Start progress display
        if (tmpActionText != null && !isShowingProgress)
        {
            if (progressCoroutine != null)
                StopCoroutine(progressCoroutine);
            progressCoroutine = StartCoroutine(UpdateSequenceProgressText());
        }
    }

    /// <summary>
    /// Executes each Step sequentially by triggering its StimulusBase and waiting for the specified delay.
    /// </summary>
    /// <returns>An IEnumerator or whatever. This just allows the function to be run as a thread or coroutine or whatever. You won't ever need this return value, most likely.</returns>
    private IEnumerator ExecuteSequence()
    {
        foreach (Step step in stimuli)
        {   
            float stimulusDuration = step.stimulus.GetStimulusDuration();
            
            // Wait for stimulus to complete.
            step.stimulus.TriggerStimulus();
            yield return new WaitForSeconds(stimulusDuration);

            // Optional delay before next stimulus
            if (step.delayAfter > 0)
                yield return new WaitForSeconds(step.delayAfter);
        }
    }

    /// <summary>
    /// If the UI Button on this StimulusSequence has been assigned, calculate the total combined duration of all steps and their delays and update the UI Button as it proceeds. Note that this function does not directly poll any stimuli for their completion status, rather it measures the elapsed time since the StimulusSequence has been started after calculating its duration and compares the elapsed time to the duration.
    /// </summary>
    /// <returns></returns>
    private IEnumerator UpdateSequenceProgressText()
    {
        if (tmpActionText == null || button == null) yield break;

        useActionText.text = "Progress:";
        isShowingProgress = true;

        int totalSteps = stimuli.Count;
        int currentStep = 0;

        // Calculate total sequence duration for overall percentage.
        float totalSequenceDuration = 0f;
        foreach (Step step in stimuli)
            totalSequenceDuration += step.stimulus.GetStimulusDuration() + step.delayAfter;

        float overallElapsed = 0f;
        int lastDisplayedOverall = -1;
        int lastDisplayedStep = -1;

        foreach (Step step in stimuli)
        {
            currentStep++;

            // Calculate this step's total duration.
            float stepDuration = step.stimulus.GetStimulusDuration() + step.delayAfter;
            float stepElapsed = 0f;

            while (stepElapsed < stepDuration)
            {
                stepElapsed += Time.deltaTime;
                overallElapsed += Time.deltaTime;

                int stepPercentage = Mathf.Clamp(Mathf.FloorToInt((stepElapsed / stepDuration) * 100f), 0, 100);
                int overallPercentage = Mathf.Clamp(Mathf.FloorToInt((overallElapsed / totalSequenceDuration) * 100f), 0, 100);

                if (stepPercentage != lastDisplayedStep || overallPercentage != lastDisplayedOverall)
                {
                    tmpActionText.text = $"Step {currentStep}/{totalSteps} - {stepPercentage:F0}% | Total: {overallPercentage:F0}%";
                    lastDisplayedStep = stepPercentage;
                    lastDisplayedOverall = overallPercentage;
                }
                yield return null;
            }
        }
        
        // Reset original button state.
        tmpActionText.text = $"Step {totalSteps}/{totalSteps} - 100% | Total: 100%";
        ResetButton();
    }

    /// <summary>
    /// Resets this StimulusSequence's UI Button back to its default state, no longer showing progress.
    /// </summary>
    private void ResetButton()
    {
        if (button != null)
        {
            useActionText.text = "Use Action";
            tmpActionText.text = defaultActionString;
            isShowingProgress = false;
        }
    }

    /// <summary>
    /// Stops all threads of execution and coroutines running for this StimulusSequence, and resets its UI Button.
    /// </summary>
    public void StopSequence()
    {
        StopAllCoroutines();
        ResetButton();
    }
}
