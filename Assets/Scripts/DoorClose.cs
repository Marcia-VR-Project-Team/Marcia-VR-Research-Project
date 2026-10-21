using UnityEngine;

public class DoorCloseTrigger : MonoBehaviour
{
    [Header("Doors to control")]
    public GameObject doorA;          // the GameObject holding the Animator
    public GameObject doorB;          // the second door

    private bool isOpen = true;
    private bool triggered = false;
    
    public void Trigger()
    {
        Debug.Log("Trigger called");

        if (triggered)
        {
            Debug.Log("Already triggered");
            return;
        }

        if (isOpen)
        {
            Debug.Log("Before doorA");
            SetBoolOnAnimator(doorA);

            Debug.Log("After doorA");

            SetBoolOnAnimator(doorB);

            Debug.Log("After doorB");

            PlayDoorAudio(doorA);
            PlayDoorAudio(doorB);

            isOpen = false;
            triggered = true;
        }
    }

    private void SetBoolOnAnimator(GameObject door)
    {
        if (door == null)
        {
            Debug.LogError("Door is NULL");
            return;
        }

        Debug.Log($"Attempting to trigger door: {door.name}");

        Animator anim = door.GetComponent<Animator>();

        if (anim == null)
        {
            Debug.LogError($"No Animator found on {door.name}");
            return;
        }

        Debug.Log($"Found Animator on {door.name}");

        anim.SetTrigger("close");

        Debug.Log($"Set trigger 'close' on {door.name}");
    }

    private void PlayDoorAudio(GameObject door)
    {
        if (door == null) return;

        AudioSource audio = door.GetComponent<AudioSource>();
        if (audio != null)
        {
            // `Play()` restarts the clip; use PlayOneShot() if you want to layer sounds
            audio.Play();
        }
        else
        {
            Debug.LogWarning($"No AudioSource found on {door.name}");
        }
    }
}