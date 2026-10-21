using UnityEngine;
using UnityEngine.AI; // only if needed

public class AnimateTimeline : MonoBehaviour
{

    [Header("Animation Type")]
    [SerializeField] private bool Walking = false;
    [SerializeField] private bool Talking = false;

    [Header("Animator Parameters")]
    [SerializeField] private string walkingParam = "isWalking";   // bool
    [SerializeField] private string talkingParam = "isTalking";

    private Animator animator;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (animator == null) return;

        animator.SetBool(walkingParam, Walking);
        animator.SetBool(talkingParam, Talking);

    }
}
