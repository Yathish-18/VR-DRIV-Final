using UnityEngine;

public class VRSeatFollower : MonoBehaviour
{
    public Transform seatAnchor;
    public Transform xrOrigin;

    void LateUpdate()
    {
        Vector3 targetPos = seatAnchor.position;
        xrOrigin.position = targetPos;
        // NO ROTATION SET HERE
    }
}