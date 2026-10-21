using UnityEngine;
using UnityEngine.AI;

public class TeacherController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform teachingSpot;
    [SerializeField] private Transform faceTarget;

    [Header("Animator Parameters")]
    [SerializeField] private string walkingParam = "isWalking";   // bool
    [SerializeField] private string teachingParam = "isTeaching"; // bool or trigger

    [Header("Settings")]
    [SerializeField] private float rotationSpeed = 5f;
    [SerializeField] private bool teachingIsTrigger = true;

    private NavMeshAgent agent;
    private Animator animator;

    private bool isMoving = false;
    private bool isTurning = false;
    private bool wasWalkingLastFrame = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (agent == null)
        {
            Debug.LogError($"{gameObject.name} is missing a NavMeshAgent.");
            enabled = false;
            return;
        }

        if (animator == null)
        {
            Debug.LogError($"{gameObject.name} is missing an Animator.");
            enabled = false;
            return;
        }

        agent.isStopped = true;

        wasWalkingLastFrame = animator.GetBool(walkingParam);
    }

    void Update()
    {
        bool walkingNow = animator.GetBool(walkingParam);

        // Detect animator bool changing from false -> true
        if (!wasWalkingLastFrame && walkingNow && !isMoving && !isTurning)
        {
            BeginWalkToTeachingSpot();
        }

        wasWalkingLastFrame = walkingNow;

        // Handle arrival
        if (isMoving && agent.isActiveAndEnabled && agent.isOnNavMesh && !agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
                {
                    isMoving = false;
                    agent.isStopped = true;
                    animator.SetBool(walkingParam, false);
                    isTurning = true;
                }
            }
        }

        // Handle turning toward class
        if (isTurning && faceTarget != null)
        {
            Vector3 dir = faceTarget.position - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime
                );

                if (Quaternion.Angle(transform.rotation, targetRot) < 2f)
                {
                    transform.rotation = targetRot;
                    isTurning = false;

                    if (teachingIsTrigger)
                        animator.SetTrigger(teachingParam);
                    else
                        animator.SetBool(teachingParam, true);
                }
            }
        }
    }

    private void BeginWalkToTeachingSpot()
    {
        if (teachingSpot == null)
        {
            Debug.LogWarning($"{gameObject.name} has no teaching spot assigned.");
            animator.SetBool(walkingParam, false);
            return;
        }

        if (!agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"{gameObject.name} agent is not on NavMesh yet.");
            animator.SetBool(walkingParam, false);
            return;
        }

        agent.enabled = true;
        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(teachingSpot.position);
        isMoving = true;
    }

    // Optional helper if you want to call this manually too
    public void StartTeachingSequence()
    {
        animator.SetBool(walkingParam, true);
    }
}