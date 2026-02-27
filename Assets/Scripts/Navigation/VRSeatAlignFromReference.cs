using UnityEngine;

public class AlignXRManually : MonoBehaviour
{
    public Transform xrOrigin;        // XR Origin transform
    public Transform vrCamera;        // Main Camera (child inside XR Origin)
    public Transform referenceCamera; // Your ideal camera

    [ContextMenu("Align XR To Reference")]
    public void Align()
    {
        if (xrOrigin == null || vrCamera == null || referenceCamera == null)
        {
            Debug.LogWarning("Assign all references!");
            return;
        }

        // World position difference
        Vector3 diff = referenceCamera.position - vrCamera.position;

        // Move XR Origin
        xrOrigin.position += diff;

        // Match Y rotation only
        float targetY = referenceCamera.eulerAngles.y;
        xrOrigin.rotation = Quaternion.Euler(0f, targetY, 0f);

        Debug.Log("Aligned using manual references.");
    }
}