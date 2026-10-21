using UnityEngine;
using UnityEngine.Playables;

public class TimelineStart : MonoBehaviour
{
    // Gets playable director (timeline object)
    public PlayableDirector director;
    private bool started = false;

    void Update()
    {
        if (!started && Input.GetKeyDown(KeyCode.Space))
        {
            director.Play();
            started = true;
        }
    }
}