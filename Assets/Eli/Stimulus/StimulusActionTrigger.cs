using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A helper component that attaches to a StimulusBase or StimulusSequence and allows it to be triggered with a Unity InputAction.
/// </summary>
public class StimulusActionTrigger : MonoBehaviour
{
    /// <summary>
    /// The Unity InputAction that will trigger the assigned StimulusBase or StimulusSequence.
    /// </summary>
    [Tooltip("The Unity Input Action used to trigger the stimulus.")]
    [SerializeField] public InputActionReference toggleAction;

    /// <summary>
    /// If this StimulusActionTrigger is attached to a GameObject containing a StimulusBase, this field will be assigned that component.
    /// </summary>
    private StimulusBase stimulus;

    /// <summary>
    /// If this StimulusActionTrigger is attached to a GameObject containing a StimulusBase, this field is false, and if attached to a GameObject containing a StimulusSequence, this field is true.
    /// </summary>
    private bool useSeq = false;

    /// <summary>
    /// If this StimulusActionTrigger is attached to a GameObject containing a StimulusSequence, this field will be assigned that component.
    /// </summary>
    private StimulusSequence stimSeq;

    void Start()
    {
        if (toggleAction == null)
        {
            Debug.LogError("No Input Action Reference is assigned to the Stimulus Action Trigger on " + gameObject.name + ". Assign it in the inspector.");
            return;
        }

        // Get the Stimulus component reference.
        if (gameObject.TryGetComponent(out stimulus))
            Debug.Log("Stimulus component found. Stimulus Action Trigger will use Input Action " + toggleAction.action.name + " to trigger Stimulus " + stimulus.gameObject.name + ".");
        else if (gameObject.TryGetComponent(out stimSeq))
        {
            useSeq = true;
            Debug.Log("Stimulus Sequence component found. Stimulus Action Trigger will use Input Action " + toggleAction.action.name + " to trigger Stimulus Sequence " + stimSeq.gameObject.name + ".");
        }
        else 
        {
            Debug.LogError("No Stimulus or Stimulus Sequence found.");
            return;
        }

        // Add the appropriate OnTriggerStimulus function to the Input Action's callback list.
        toggleAction.action.performed += useSeq ? stimSeq.OnTriggerStimulusSequence : stimulus.OnTriggerStimulus;
    }

    /// <summary>
    /// When this StimulusActionTrigger is destroyed, ensure it removes the callback function from the Input Action.
    /// </summary>
    void OnDestroy()
    {
        if (toggleAction != null)
            toggleAction.action.performed -= useSeq ? stimSeq.OnTriggerStimulusSequence : stimulus.OnTriggerStimulus;
    }
}
