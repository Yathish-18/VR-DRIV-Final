using UnityEngine;

public class NPCVehicle : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 5f;
    public float turnSpeed = 2f;

    private int currentWaypointIndex = 0;

    void Update()
    {
        if (waypoints.Length == 0) return;

        Transform target = waypoints[currentWaypointIndex];
        Vector3 direction = (target.position - transform.position);
        direction.y = 0f; // prevent rotation on the Y axis if vehicle is on a flat plane
        Vector3 moveDir = direction.normalized;

        // Move towards the target smoothly
        transform.position = Vector3.Lerp(transform.position, transform.position + moveDir, speed * Time.deltaTime);

        // Rotate smoothly towards movement direction
        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        // Check if close enough to the waypoint
        if (direction.magnitude < 1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }
}
