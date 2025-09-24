using System.Collections.Generic;
using UnityEngine;

public class CentralizedCarController : MonoBehaviour
{
    [Header("Car Settings")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 180f;
    public float arrivalDistance = 1f;

    [Header("Navigation")]
    public CentralizedNavigationSystem navSystem;
    public Transform target;

    [Header("Auto-Pathfinding Controls")]
    [Tooltip("Enable/disable automatic pathfinding when target moves")]
    public bool autoFindPath = true;

    [Tooltip("Enable/disable following the calculated path")]
    public bool followPath = true;

    [Tooltip("How far the target must move before recalculating path")]
    public float recalculateThreshold = 5f;

    [Tooltip("Cooldown time between automatic path recalculations")]
    public float pathfindCooldownTime = 2f;

    [Header("Manual Controls")]
    [Tooltip("Manually trigger pathfinding in play mode")]
    public bool triggerPathfinding = false;

    [Header("Debug")]
    public bool showDebugLogs = true;

    // Private variables
    private List<int> currentPathIDs;
    private int currentPathIndex = 0;
    private bool isFollowingPath = false;
    private float pathFindCooldown = 0f;
    private Vector3 lastTargetPosition;
    private bool hasValidPath = false;

    void Start()
    {
        if (navSystem == null)
            navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();

        // Store initial target position
        if (target != null)
        {
            lastTargetPosition = target.position;
        }

        // Auto-find path on start if both toggles are enabled and target is set
        if (autoFindPath && followPath && target != null)
        {
            Invoke(nameof(FindAndFollowPath), 1f); // Wait 1 second for nav system to initialize
        }

        if (showDebugLogs)
        {
            Debug.Log($"Car initialized - Auto Pathfind: {autoFindPath}, Follow Path: {followPath}");
        }
    }

    void Update()
    {
        HandleInput();
        HandleAutoPathfinding();
        HandlePathFollowing();
        UpdateCooldowns();
    }

    void HandleInput()
    {
        // Manual pathfinding trigger via inspector
        if (triggerPathfinding && target != null)
        {
            triggerPathfinding = false; // Reset the trigger
            FindAndFollowPath();
        }
    }

    void HandleAutoPathfinding()
    {
        // Only auto-pathfind if enabled and target exists
        if (!autoFindPath || target == null || pathFindCooldown > 0f) return;

        // Check if target moved significantly
        if (HasTargetMovedSignificantly())
        {
            FindAndFollowPath();
            pathFindCooldown = pathfindCooldownTime;
            lastTargetPosition = target.position;
        }
        // Check if we need initial pathfinding
        else if (!hasValidPath && !isFollowingPath)
        {
            if (ShouldCalculateInitialPath())
            {
                FindAndFollowPath();
                pathFindCooldown = pathfindCooldownTime;
            }
        }
    }

    void HandlePathFollowing()
    {
        // Only follow path if enabled and we have a valid path
        if (followPath && isFollowingPath && hasValidPath)
        {
            FollowPath();
        }
        else if (!followPath && isFollowingPath)
        {
            // Path following was disabled, pause movement but keep path
            if (showDebugLogs) Debug.Log("Path following disabled - car stopped but path remains");
        }
    }

    void UpdateCooldowns()
    {
        if (pathFindCooldown > 0)
            pathFindCooldown -= Time.deltaTime;
    }

    bool HasTargetMovedSignificantly()
    {
        if (target == null) return false;

        float distanceMoved = Vector3.Distance(target.position, lastTargetPosition);
        return distanceMoved > recalculateThreshold;
    }

    bool ShouldCalculateInitialPath()
    {
        if (target == null || navSystem == null) return false;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        return distanceToTarget > arrivalDistance * 2f;
    }

    public void FindAndFollowPath()
    {
        if (navSystem == null)
        {
            if (showDebugLogs) Debug.LogWarning("No navigation system assigned!");
            return;
        }

        if (target == null)
        {
            if (showDebugLogs) Debug.LogWarning("No target assigned!");
            return;
        }

        if (showDebugLogs)
            Debug.Log($"Finding path from {transform.position} to {target.position}");

        // Find path using world positions
        currentPathIDs = navSystem.FindPath(transform.position, target.position);

        if (currentPathIDs != null && currentPathIDs.Count > 0)
        {
            // Visualize the path
            navSystem.VisualizePath(currentPathIDs);

            currentPathIndex = 0;
            hasValidPath = true;

            // Only start following if path following is enabled
            if (followPath)
            {
                isFollowingPath = true;
            }

            if (showDebugLogs)
            {
                string pathStr = string.Join(" → ", currentPathIDs);
                Debug.Log($"Path found with {currentPathIDs.Count} nodes: [{pathStr}]");
                Debug.Log($"Path following: {(followPath ? "ENABLED" : "DISABLED")}");
            }
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning("No path found to target");
            hasValidPath = false;
            isFollowingPath = false;

            // Clear any existing path visualization
            if (navSystem != null)
                navSystem.ClearPath();
        }
    }

    void FollowPath()
    {
        if (currentPathIDs == null || currentPathIndex >= currentPathIDs.Count)
        {
            // Path completed
            CompletePathFollowing();
            return;
        }

        int targetNodeID = currentPathIDs[currentPathIndex];

        if (!navSystem.nodeMap.ContainsKey(targetNodeID) || navSystem.nodeMap[targetNodeID] == null)
        {
            if (showDebugLogs) Debug.LogError($"Target node {targetNodeID} not found in navigation system!");
            CompletePathFollowing();
            return;
        }

        Vector3 targetPos = navSystem.nodeMap[targetNodeID].transform.position;

        // Move towards target node
        Vector3 direction = (targetPos - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;

        // Rotate towards target
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Check if reached current node
        float distanceToCurrentTarget = Vector3.Distance(transform.position, targetPos);
        if (distanceToCurrentTarget < arrivalDistance)
        {
            if (showDebugLogs) Debug.Log($"Reached node {targetNodeID} (distance: {distanceToCurrentTarget:F2})");

            currentPathIndex++;

            if (currentPathIndex >= currentPathIDs.Count)
            {
                CompletePathFollowing();
            }
        }
    }

    void CompletePathFollowing()
    {
        isFollowingPath = false;
        hasValidPath = false;

        if (navSystem != null)
            navSystem.ClearPath();

        if (showDebugLogs) Debug.Log("Path following completed!");
    }

    // Public toggle methods
    public void ToggleAutoPathfinding()
    {
        autoFindPath = !autoFindPath;

        if (showDebugLogs)
            Debug.Log($"Auto-pathfinding {(autoFindPath ? "ENABLED" : "DISABLED")}");

        // If we just enabled auto-pathfinding and have a target, find path
        if (autoFindPath && target != null && !hasValidPath)
        {
            FindAndFollowPath();
        }
    }

    public void TogglePathFollowing()
    {
        followPath = !followPath;

        if (showDebugLogs)
            Debug.Log($"Path following {(followPath ? "ENABLED" : "DISABLED")}");

        if (followPath && hasValidPath)
        {
            // Resume following if we have a valid path
            isFollowingPath = true;
        }
        else if (!followPath)
        {
            // Stop following but keep the path
            isFollowingPath = false;
        }
    }

    public void SetAutoPathfinding(bool enabled)
    {
        autoFindPath = enabled;
        if (showDebugLogs)
            Debug.Log($"Auto-pathfinding set to {(autoFindPath ? "ENABLED" : "DISABLED")}");
    }

    public void SetPathFollowing(bool enabled)
    {
        followPath = enabled;
        if (showDebugLogs)
            Debug.Log($"Path following set to {(followPath ? "ENABLED" : "DISABLED")}");

        if (followPath && hasValidPath)
        {
            isFollowingPath = true;
        }
        else if (!followPath)
        {
            isFollowingPath = false;
        }
    }

    // Public method to set new target and automatically pathfind (respects current settings)
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (target != null)
        {
            lastTargetPosition = target.position;

            // Only auto-pathfind if auto-pathfinding is enabled
            if (autoFindPath)
            {
                // Stop current pathfinding
                StopPathfinding();
                // Find new path
                FindAndFollowPath();
            }
        }
    }

    // Stop current pathfinding
    public void StopPathfinding()
    {
        isFollowingPath = false;
        hasValidPath = false;
        currentPathIDs = null;

        if (navSystem != null)
            navSystem.ClearPath();

        if (showDebugLogs) Debug.Log("Pathfinding stopped");
    }

    // Status check methods
    public bool IsAutoPathfindingEnabled() => autoFindPath;
    public bool IsPathFollowingEnabled() => followPath;
    public bool IsCurrentlyFollowingPath() => isFollowingPath;
    public bool HasValidPath() => hasValidPath;

    void OnDrawGizmos()
    {
        // Draw car
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position, Vector3.one);

        // Draw forward direction
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 2f);

        // Draw target
        if (target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(target.position, 1f);

            // Only draw direct line if no valid path exists
            if (!hasValidPath)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }

        // Draw current path target and path line
        if (hasValidPath && currentPathIDs != null && navSystem != null)
        {
            // Draw path from car to nodes
            Gizmos.color = followPath ? Color.cyan : Color.yellow;

            Vector3 currentPos = transform.position;

            // Draw line to first node in path
            if (currentPathIndex < currentPathIDs.Count)
            {
                int firstNodeID = currentPathIDs[currentPathIndex];
                if (navSystem.nodeMap.ContainsKey(firstNodeID) && navSystem.nodeMap[firstNodeID] != null)
                {
                    Vector3 firstNodePos = navSystem.nodeMap[firstNodeID].transform.position;
                    Gizmos.DrawLine(currentPos, firstNodePos);

                    // Draw arrival distance indicator for current target
                    if (isFollowingPath)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawWireSphere(firstNodePos, arrivalDistance);
                    }
                }
            }

            // Draw path between nodes
            for (int i = currentPathIndex; i < currentPathIDs.Count - 1; i++)
            {
                int currentNodeID = currentPathIDs[i];
                int nextNodeID = currentPathIDs[i + 1];

                if (navSystem.nodeMap.ContainsKey(currentNodeID) && navSystem.nodeMap.ContainsKey(nextNodeID) &&
                    navSystem.nodeMap[currentNodeID] != null && navSystem.nodeMap[nextNodeID] != null)
                {
                    Vector3 currentNodePos = navSystem.nodeMap[currentNodeID].transform.position;
                    Vector3 nextNodePos = navSystem.nodeMap[nextNodeID].transform.position;

                    Gizmos.color = followPath ? Color.cyan : Color.yellow;
                    Gizmos.DrawLine(currentNodePos, nextNodePos);
                }
            }
        }

        // Draw status indicators
        Vector3 statusPos = transform.position + Vector3.up * 3f;

        // Auto-pathfinding indicator
        Gizmos.color = autoFindPath ? Color.green : Color.red;
        Gizmos.DrawWireCube(statusPos + Vector3.left * 1f, Vector3.one * 0.3f);

        // Path following indicator  
        Gizmos.color = followPath ? Color.green : Color.red;
        Gizmos.DrawWireCube(statusPos + Vector3.right * 1f, Vector3.one * 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        // Draw additional debug info when selected
        if (isFollowingPath && currentPathIDs != null && navSystem != null)
        {
            Gizmos.color = Color.magenta;

            for (int i = currentPathIndex; i < currentPathIDs.Count; i++)
            {
                int nodeID = currentPathIDs[i];
                if (navSystem.nodeMap.ContainsKey(nodeID) && navSystem.nodeMap[nodeID] != null)
                {
                    Vector3 nodePos = navSystem.nodeMap[nodeID].transform.position;
                    Gizmos.DrawWireSphere(nodePos, 0.5f);

                    // Draw path segment
                    if (i > currentPathIndex)
                    {
                        int prevNodeID = currentPathIDs[i - 1];
                        if (navSystem.nodeMap.ContainsKey(prevNodeID) && navSystem.nodeMap[prevNodeID] != null)
                        {
                            Vector3 prevPos = navSystem.nodeMap[prevNodeID].transform.position;
                            Gizmos.DrawLine(prevPos, nodePos);
                        }
                    }
                    else if (i == currentPathIndex)
                    {
                        // Draw line from car to first node
                        Gizmos.DrawLine(transform.position, nodePos);
                    }
                }
            }
        }
    }

    void OnGUI()
    {
        if (!showDebugLogs) return;

        // Display current status on screen
        GUILayout.BeginArea(new Rect(10, Screen.height - 100, 300, 80));
        GUILayout.Label($"Auto Pathfind: {(autoFindPath ? "ON" : "OFF")}");
        GUILayout.Label($"Follow Path: {(followPath ? "ON" : "OFF")}");
        GUILayout.Label($"Currently Following: {(isFollowingPath ? "YES" : "NO")}");
        GUILayout.Label($"Has Valid Path: {(hasValidPath ? "YES" : "NO")}");
        GUILayout.EndArea();
    }
}