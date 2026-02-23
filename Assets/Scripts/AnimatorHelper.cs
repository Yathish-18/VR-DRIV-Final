using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimatorHelper : MonoBehaviour
{
    private Animator animator;

    void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // Generic methods for UnityEvents
    public void SetBoolTrue(string parameterName)
    {
        print("entered");
        animator.SetBool(parameterName, true);
    }

    public void SetBoolFalse(string parameterName)
    {
        animator.SetBool(parameterName, false);
    }

    public void SetTrigger(string parameterName)
    {
        animator.SetTrigger(parameterName);
    }
}
