using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WalkingSound : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string walkParameter = "isWalking";

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        //udioSource.Play();

        audioSource.loop = true;       // keeps footsteps playing
        audioSource.spatialBlend = 1f; // makes it 3D
        
    }

    void Update()
    {
        if (!animator) return;

        bool isWalking = animator.GetBool("isWalking");
        bool isSitting = animator.GetBool("isSitting");

        if (isWalking && !isSitting && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if ((!isWalking || isSitting) && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}