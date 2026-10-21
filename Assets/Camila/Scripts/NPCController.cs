using UnityEngine;
using UnityEngine.AI;

public class NPCController : MonoBehaviour
{
    [Header("Sit Locations")]
    [SerializeField] private Transform[] sitTargets;

    [Header("Starting State")]
    [SerializeField] private bool startSitting = false;
    [SerializeField] private Transform startingSitTarget; // optional, only used if startSitting = true

    [Header("Animator Parameters")]
    [SerializeField] private string walkingParam = "isWalking";
    [SerializeField] private string sittingParam = "isSitting";

    [Header("Movement")]
    [SerializeField] private bool chooseDifferentChairIfPossible = true;

    private NavMeshAgent agent;
    private Animator animator;

    private Transform currentSitTarget;

    private bool isMovingToChair = false;
    private bool wasWalkingParamLastFrame = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (agent == null)
        {
            Debug.LogError($"{gameObject.name} is missing a NavMeshAgent.");
            return;
        }

        if (animator == null)
        {
            Debug.LogError($"{gameObject.name} is missing an Animator.");
            return;
        }

        if (sitTargets == null || sitTargets.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} has no sit targets assigned.");
        }

        if (startSitting)
        {
            // Pick either the assigned starting seat or a random one
            currentSitTarget = startingSitTarget != null ? startingSitTarget : GetRandomSitTarget(null);

            if (currentSitTarget != null)
            {
                SnapToSeat(currentSitTarget);
            }

            animator.SetBool(sittingParam, true);
            animator.SetBool(walkingParam, false);

            agent.isStopped = true;
            agent.enabled = false;
        }
        else
        {
            // Start standing wherever the character already is
            animator.SetBool(sittingParam, false);
            animator.SetBool(walkingParam, false);

            agent.enabled = true;
            agent.isStopped = true;
        }

        wasWalkingParamLastFrame = animator.GetBool(walkingParam);
    }

    void Update()
    {
        bool walkingNow = animator.GetBool(walkingParam);

        // Detect when some OTHER script flips isWalking from false -> true
        if (!wasWalkingParamLastFrame && walkingNow && !isMovingToChair)
        {
            BeginWalkToRandomChair();
        }

        wasWalkingParamLastFrame = walkingNow;

        // Handle arrival
        if (isMovingToChair && agent.enabled && !agent.pathPending)
        {
            if (agent.remainingDistance <= agent.stoppingDistance)
            {
                if (!agent.hasPath || agent.velocity.sqrMagnitude < 0.01f)
                {
                    SitDown();
                }
            }
        }
    }

    private void BeginWalkToRandomChair()
    {
        if (sitTargets == null || sitTargets.Length == 0)
        {
            Debug.LogWarning($"{gameObject.name} cannot walk to chair because no sit targets are assigned.");
            animator.SetBool(walkingParam, false);
            return;
        }

        Transform nextTarget = GetRandomSitTarget(currentSitTarget);

        if (nextTarget == null)
        {
            Debug.LogWarning($"{gameObject.name} could not find a valid sit target.");
            animator.SetBool(walkingParam, false);
            return;
        }

        currentSitTarget = nextTarget;

        if (!agent.enabled)
            agent.enabled = true;

        agent.isStopped = false;
        agent.ResetPath();
        agent.SetDestination(currentSitTarget.position);

        // Make sure sitting is turned off while walking
        animator.SetBool(sittingParam, false);

        isMovingToChair = true;
    }

    private void SitDown()
    {
        isMovingToChair = false;

        agent.isStopped = true;
        agent.ResetPath();
        agent.enabled = false;

        SnapToSeat(currentSitTarget);

        animator.SetBool(walkingParam, false);
        animator.SetBool(sittingParam, true);
    }

    private void SnapToSeat(Transform seat)
    {
        if (seat == null) return;

        transform.position = seat.position;
        transform.rotation = seat.rotation;
    }

    private Transform GetRandomSitTarget(Transform exclude)
    {
        if (sitTargets == null || sitTargets.Length == 0)
            return null;

        if (!chooseDifferentChairIfPossible || sitTargets.Length == 1 || exclude == null)
        {
            return sitTargets[Random.Range(0, sitTargets.Length)];
        }

        Transform chosen = sitTargets[Random.Range(0, sitTargets.Length)];

        // Try a few times not to repeat the same seat
        for (int i = 0; i < 10; i++)
        {
            if (chosen != exclude)
                return chosen;

            chosen = sitTargets[Random.Range(0, sitTargets.Length)];
        }

        return chosen;
    }

    // Optional helper if you want to call it manually from somewhere else too
    public void ForceWalkToRandomChair()
    {
        animator.SetBool(walkingParam, true);
    }
}