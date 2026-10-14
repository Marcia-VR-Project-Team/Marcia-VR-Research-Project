using UnityEngine;

public class WindowButton : MonoBehaviour
{
    public AudioSource audioSource;

    private bool isPressedV = false;
    private bool isPressedW = false;

    public BlindsAnimation[] blindsAnimations;

    public void VolumeChange()
    {
        if (isPressedV)
        {
            audioSource.volume = 1f;
            isPressedV = false;
        }
        else
        {
            audioSource.volume = 0.4f;
            isPressedV = true;
        }
    }

    public void windowChange()
    {
        if (isPressedW)
        {

            foreach (BlindsAnimation blind in blindsAnimations)
            {
                if (blind != null)
                    blind.RaiseBlinds();
            }

            isPressedW = false;
        }
        else
        {

            foreach (BlindsAnimation blind in blindsAnimations)
            {
                if (blind != null)
                    blind.LowerBlinds();
            }

            isPressedW = true;
        }
    }
    



}
