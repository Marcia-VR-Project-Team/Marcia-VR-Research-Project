using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The abstract base class for the AnimationStimulus and AudioStimulus. Provides logging functionality, debugging, UI Integration, and a triggering interface. By having shared functionality here, other GameObjects and components (like StimulusSequence) can use the same functions (like TriggerStimulus) regardless of what kind of stimulus it is, and the behavior will be completely separate based on implementation. Abstract base classes are cool!!! Is never instantiated directly and cannot be added as a component.
/// </summary>
[System.Serializable]
public abstract class StimulusBase : MonoBehaviour
{
    /// <summary>
    /// The string used when logging that a StimulusBase has been triggered via a Unity Input Action.
    /// </summary>
    protected readonly static string STIMULUS_ACTION_TRIGGERED_TEXT = "STIMULUS_TRIGGERED_INPUTACTION";

    /// <summary>
    /// The string used when logging that a StimulusBase has had its stimulus started.
    /// </summary>
    protected readonly static string STIMULUS_START_TEXT = "STIMULUS_START";

    /// <summary>
    /// The Unity UI Button that displays the name, current completion progress, and trigger mechanism for this StimulusBase. Must be an instance of the Prefab "Stimulus Button" found in Assets->Eli.
    /// </summary>
    [Header("UI Integration")]
    [Tooltip("The UI Button that displays the information about this Stimulus. Ensure it is an instance of the Prefab \"Stimulus Button\" found in Assets->Eli.")]
    [SerializeField] private Button button;

    /// <summary>
    /// The Unity GameObject Tag corresponding to that of the TextMeshPro component containing the title of this StimulusBase.
    /// </summary>
    [Tooltip("The GameObject tag corresponding to the TextMeshPro component containing the title of the Stimulus.")]
    [SerializeField] private string titleTag = "StimulusTitle";

    /// <summary>
    /// The Unity GameObject Tag corresponding to that of the TextMeshPro component containing the Input Action used to trigger this StimulusBase, if one is present.
    /// </summary>    
    [Tooltip("The GameObject tag corresponding to the TextMeshPro component containing the Input Action used to trigger this stimulus, if one is present.")]
    [SerializeField] private string inputTextTag = "StimulusText";

    /// <summary>
    /// The StimulusActionTrigger component possessing the name of the Input Action used to trigger this StimulusBase.
    /// </summary>
    private StimulusActionTrigger stimulusActionTrigger;

    /// <summary> 
    /// The TextMeshPro Text component of this StimulusBase's UI Button, used to display the name of this StimulusBase.
    /// </summary>
    private TMP_Text tmpTitleText;

    /// <summary>
    /// The TextMeshPro Text component of this StimulusBase's UI Button, used to display the action to use to trigger the StimulusBase.
    /// </summary>
    private TMP_Text tmpActionText;

    /// <summary>
    /// The TextMeshPro Text component of this StimulusBase's UI Button, used to display the "Use Action" text.
    /// </summary>
    private TMP_Text useActionText;

    /// <summary> 
    /// The default string displayed via useActionText showing how to trigger this StimulusBase.
    /// </summary>
    private string defaultActionString = "";

    /// <summary>
    /// Used to track whether this StimulusBase is currently in progress, i.e., if it is playing.
    /// </summary>
    protected bool isStimulusPlaying = false;
    
    /// <summary>
    /// Tracks if this StimulusBase is currently showing the completion progress through its UI Button.
    /// </summary>
    private bool isShowingProgress = false;

    /// <summary>
    /// Contains the UnityEngine Coroutine that handles updates to the progress display on this StimulusBase's UI Button.
    /// </summary>
    private Coroutine progressCoroutine = null;

    /// <summary>
    /// Determines whether this StimulusBase will log its activity. True to log, false to not log. A Stimulus should almost always log its activity.
    /// </summary>
    [Header("Logging")]
    [SerializeField] protected bool logActivity = true;

    /// <summary>
    /// Determines whether this StimulusBase will print debug activity to the console. True to print, false to not print. Use when having problems setting up the Stimulus.
    /// </summary>
    [Header("Debug")]
    [SerializeField] private bool printDebugStatements = false;

    void Awake()
    {
        SetupButton();
    }

    /// <summary>
    /// This virtual function is implemented by StimulusBase subclasses. It defines the logic for what happens when the StimulusBase is triggered and how it handles playing audio and animations, and logging structure. By default this function initiates the UI Button update loop.
    /// </summary>
    /// <param name="triggerSource">The string representing how this StimulusBase was triggered.</param>
    public virtual void TriggerStimulus(string triggerSource = "Manual/Button")
    {
        // Start progress display if button text exists.
        if (tmpActionText && !isShowingProgress)
        {
            if (progressCoroutine != null) StopCoroutine(progressCoroutine);
            progressCoroutine = StartCoroutine(UpdateProgressText());
        }
    }

    /// <summary>
    /// This virtual function is implemented by StimulusBase subclasses. It defines the logic for calculating the total runtime of a Stimulus, since this requires different logic for animations, audio, etc. This function has no default behavior.
    /// </summary>
    /// <returns>The total number of seconds this Stimulus plays for.</returns>
    public virtual float GetStimulusDuration() { return 0f; }

    /// <summary>
    /// This virtual function is implemented by StimulusBase subclasses. It defines the logic for stopping an audio clip, animation, etc. By default this function cancels button UI updates, resets the button, and sets all state parameters of this Stimulus to a non-playing state.
    /// </summary>
    public virtual void StopStimulus()
    {
        CancelInvoke(nameof(ResetPlayingState));
        ResetPlayingState();
        ResetButton();
    }

    /// <summary>
    /// A short wrapper function for printing debug statements if the printDebugStatements flag of this StimulusBase is set to true.
    /// </summary>
    /// <param name="s">The string to be logged with Debug.Log().</param>
    protected void LogDebug(string s) { if (printDebugStatements) Debug.Log(s); }

    /// <summary>
    /// This function allows a Unity InputAction to trigger this StimulusBase with information about the InputAction that triggered it. It attempts to log the stimulus activity and print a debug statement, but most importantly, calls TriggerStimulus() on this Stimulus.
    /// </summary>
    /// <param name="context">The context generated by an InputAction set to trigger this StimulusBase when that action is performed.</param>
    public void OnTriggerStimulus(InputAction.CallbackContext context)
    {
        string triggerSource = $"InputAction: {context.action.name}";
        if (logActivity) StimulusLogger.Log(STIMULUS_ACTION_TRIGGERED_TEXT, gameObject.name, triggerSource);

        LogDebug($"Stimulus triggered for {gameObject.name} by InputAction {context.action.name}");
        TriggerStimulus(triggerSource);
    }

    /// <summary>
    /// If the UI Button on this StimulusBase has been assigned, calculate the duration of this StimulusBase and update the UI Button as it proceeds. Note that this function does not directly poll the audio or animation for its completion status, instead it measures the elapsed time since the Stimulus has been started after calculating its duration and compares the elapsed time to the duration.
    /// </summary>
    /// <returns>An IEnumerator or whatever. This just allows the function to be run as a thread or coroutine or whatever. You won't ever need this return value, most likely.</returns>
    private IEnumerator UpdateProgressText()
    {
        if (!tmpActionText) yield break;

        ShowProgress();
        float duration = GetStimulusDuration();
        float elapsed = 0f;
        int lastDisplayedPercentage = -1;

        useActionText.text = "Progress:";

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            int percentage = Mathf.Clamp(Mathf.FloorToInt((elapsed / duration) * 100f), 0, 100);

            if (percentage != lastDisplayedPercentage)
            {
                tmpActionText.text = $"{percentage:F0}%";
                lastDisplayedPercentage = percentage;                
            }
            yield return null;
        }

        // Restore original button state
        tmpActionText.text = "100%";
        ResetButton();
    }

    /// <summary>
    /// Sets the state of this StimulusBase to allow it to show progress on the UI Button.
    /// </summary>
    protected void ShowProgress()
    {
        isShowingProgress = true;
    }

    /// <summary>
    /// Resets the state of this StimulusBase to make it stop showing progress and stop playing.
    /// </summary>
    protected void ResetPlayingState()
    {
        isShowingProgress = false;
        isStimulusPlaying = false;
    }

    /// <summary>
    /// Resets this StimulusBase's UI Button back to its default state, not showing progress.
    /// </summary>
    private void ResetButton()
    {
        if (!button) return;
        useActionText.text = "Use Action";
        tmpActionText.text = defaultActionString;
        ResetPlayingState();
    }

    /// <summary>
    /// Sets the title and trigger text on the UI Button for this StimulusBase if it has been assigned.
    /// </summary>
    private void SetupButton()
    {
        // Get the TextMeshPro component references for assigning their values.
        if (!button) return;
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
        if (!tmpTitleText) 
            Debug.LogWarning($"Button has been assigned to Stimulus on {name}, but it contains no TextMeshPro Text components in children tagged with {titleTag}. Please ensure {button.name} has a child GameObject tagged with {titleTag} and containing a TextMeshPro Text component.");
        else 
            tmpTitleText.text = gameObject.name;

        if (!tmpActionText) 
            Debug.LogWarning($"Button has been assigned to Stimulus on {name}, but it contains no TextMeshPro Text components in children tagged with {inputTextTag}. Please ensure {button.name} has a child GameObject tagged with {inputTextTag} and containing a TextMeshPro Text component.");
        else
        {
            if (stimulusActionTrigger)
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
}