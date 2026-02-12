using UnityEngine;

public class NPCVehicle : MonoBehaviour
{
    public Transform[] waypoints;
    public float speed = 5f;
    public float turnSpeed = 2f;
    
    [Header("Ground Detection")]
    [Tooltip("Auto-calculated on start, or set manually")]
    public float hoverHeight = 0.8f;
    public float groundCheckDistance = 20f;
    public LayerMask groundLayer = -1;
    
    [Header("Auto-Calculate Hover Height")]
    public bool autoCalculateHoverHeight = true; // ← Enable auto-calculation
    public float raycastDownDistance = 5f; // How far to check for initial ground

    private int currentWaypointIndex = 0;

    void Start()
    {
        if (autoCalculateHoverHeight)
        {
            CalculateHoverHeight();
        }
    }

    void CalculateHoverHeight()
    {
        // Raycast down from car to find ground
        RaycastHit hit;
        Vector3 rayStart = transform.position + Vector3.up * 2f;
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDownDistance, groundLayer))
        {
            // Calculate distance from car pivot to ground
            float distanceToGround = transform.position.y - hit.point.y;
            
            // This is our hover height!
            hoverHeight = distanceToGround;
            
            Debug.Log($"Auto-calculated hoverHeight: {hoverHeight:F2} units");
        }
        else
        {
            Debug.LogWarning($"Could not auto-calculate hoverHeight! Using default: {hoverHeight}");
        }
    }

    void Update()
    {
        if (waypoints.Length == 0) return;

        Transform target = waypoints[currentWaypointIndex];
        Vector3 direction = (target.position - transform.position);
        direction.y = 0f;
        Vector3 moveDir = direction.normalized;

        Vector3 newPos = Vector3.Lerp(transform.position, transform.position + moveDir, speed * Time.deltaTime);

        // Use the calculated hoverHeight
        RaycastHit hit;
        Vector3 rayStart = new Vector3(newPos.x, transform.position.y + 10f, newPos.z);
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, groundCheckDistance, groundLayer))
        {
            newPos.y = hit.point.y + hoverHeight; // ← Uses pre-calculated value
        }

        transform.position = newPos;

        if (moveDir != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        if (direction.magnitude < 1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        }
    }
}