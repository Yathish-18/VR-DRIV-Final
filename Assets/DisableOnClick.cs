using UnityEngine;

public class DisableOnClick : MonoBehaviour
{
    public GameObject targetObject; // Assign this in the Inspector

    public void DisableTarget()
    {
        if (targetObject != null)
        {
            targetObject.SetActive(false);
        }
    }
}
