using UnityEngine;

public class CarFollower : MonoBehaviour
{
    public Transform target; // This is NavTarget
    public float speed = 5f;
    public float turnSpeed = 2f;

    void Update()
    {
        Vector3 dir = (target.position - transform.position).normalized;
        Quaternion lookRotation = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * turnSpeed);
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}
