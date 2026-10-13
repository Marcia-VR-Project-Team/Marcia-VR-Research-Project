using UnityEngine;
using System.Collections;

public class BlindsAnimation : MonoBehaviour
{
    public SkinnedMeshRenderer blindsRenderer;
    public float duration = 2f;

    public void LowerBlinds()
    {
        StartCoroutine(AnimateBlinds(100f, 0f));
    }

    public void RaiseBlinds()
    {
        StartCoroutine(AnimateBlinds(0f, 100f));
    }

    IEnumerator AnimateBlinds(float start, float end)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float weight = Mathf.Lerp(
                start,
                end,
                Mathf.Clamp01(elapsed / duration)
            );

            blindsRenderer.SetBlendShapeWeight(0, weight);

            yield return null;
        }

        blindsRenderer.SetBlendShapeWeight(0, end);
    }
}