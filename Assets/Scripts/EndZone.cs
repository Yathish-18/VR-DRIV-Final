using UnityEngine;

public class EndZone : MonoBehaviour
{
    public ForkliftEvaluator evaluator;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Box"))
        {
            if (evaluator != null)
            {
                evaluator.CompleteTask();
            }
        }
    }
}
