using UnityEngine;

public class TeleportPlayer : MonoBehaviour
{
    public Transform deskA;
    public Transform deskB;

    private bool isAtDeskA = true;

    public void SendToPosition()
    {

        if (isAtDeskA)
        {
            transform.position = deskB.position;
            isAtDeskA = false;
        }
        else
        {
            transform.position = deskA.position;
            isAtDeskA = true;
        }

    }
}
