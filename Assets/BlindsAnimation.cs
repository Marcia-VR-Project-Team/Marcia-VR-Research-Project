using UnityEngine;
using System.Collections;

public class BlindsAnimation : MonoBehaviour
{
    public SkinnedMeshRenderer blindsRenderer;
    public float duration = 2f;

    [Header("Testing")]
    public bool lowerBlinds = false;

    private bool previousLowerBlinds = false;
    private Coroutine currentAnimation;

    void Start()
    {
        // Start with blinds fully raised
        blindsRenderer.SetBlendShapeWeight(0, 100f);

        // Ensure the initial state is raised
        lowerBlinds = false;
        previousLowerBlinds = false;
    }

    void Update()
    {
        if (lowerBlinds != previousLowerBlinds)
        {
            previousLowerBlinds = lowerBlinds;

            //Saying: "If an animation is already running, stop it."
            if (currentAnimation != null)
                StopCoroutine(currentAnimation);

            if (lowerBlinds)
                currentAnimation = StartCoroutine(AnimateBlinds(0f));
            else
                currentAnimation = StartCoroutine(AnimateBlinds(100f));
        }
    }

    public void LowerBlinds()
    {
        lowerBlinds = true;
    }

    public void RaiseBlinds()
    {
        lowerBlinds = false;
    }

    IEnumerator AnimateBlinds(float target)
    {
        float elapsed = 0f;
        float start = blindsRenderer.GetBlendShapeWeight(0);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float weight = Mathf.Lerp(
                start,
                target,
                Mathf.Clamp01(elapsed / duration)
            );

            blindsRenderer.SetBlendShapeWeight(0, weight);

            yield return null;
        }

        blindsRenderer.SetBlendShapeWeight(0, target);
        currentAnimation = null;
    }
}