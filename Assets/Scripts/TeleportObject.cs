using UnityEngine;

public class TeleportObject : MonoBehaviour
{
    public Transform deskA;
    public Transform deskB;

    private bool isObjDeskA = true;

    public void SendToPosition()
    {

        if (isObjDeskA)
        {
            transform.position = deskB.position;
            isObjDeskA = false;
        }
        else
        {
            transform.position = deskA.position;
            isObjDeskA = true;
        }

    }
}
