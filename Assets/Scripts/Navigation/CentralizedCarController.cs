using UnityEngine;
using System.Collections.Generic;

public class CentralizedCarController : MonoBehaviour
{
    public CentralizedNavigationSystem navSystem;
    public NavNode targetNode;
    public bool autoFindPath = false;
    public bool followPath = true;
    public bool showDebugLogs = false;

    [Header("Driving")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 2f;

    [Header("Dynamic Route Update")]
    public bool autoUpdateRoute = true;
    public float routeUpdateInterval = 3f; // Recalculate every X seconds
    public float offRouteThreshold = 20f; // Distance to trigger immediate recalculation

    private List<int> currentPath = new List<int>();
    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    private float routeUpdateTimer = 0f;
    private int lastClosestNodeID = -1;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1000f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;
        }
    }

    void Start()
    {
        if (navSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
            navSystem = Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif
        }

        if (showDebugLogs)
        {
            Debug.Log($"[Car] Start. navSystem={(navSystem != null)}, targetNode={(targetNode != null)}");
        }

        if (autoFindPath && targetNode != null)
        {
            FindAndFollowPath();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (showDebugLogs) Debug.Log("[Car] Space pressed – recalculating path");
            FindAndFollowPath();
        }

        // Auto update route system
        if (autoUpdateRoute && targetNode != null && navSystem != null)
        {
            routeUpdateTimer += Time.deltaTime;

            //// Check if player went off-route
            //if (IsPlayerOffRoute())
            //{
            //    if (showDebugLogs) Debug.Log("[Car] Player off-route! Recalculating immediately...");
            //    FindAndFollowPath();
            //    routeUpdateTimer = 0f;
            //}
            // Periodic update
            //else
            if (routeUpdateTimer >= routeUpdateInterval)
            {
                if (showDebugLogs) Debug.Log("[Car] Periodic route update triggered");
                FindAndFollowPath();
                routeUpdateTimer = 0f;
            }
        }

        if (!followPath) return;

        if (navSystem == null || rb == null || currentPath == null) return;
        if (currentPath.Count == 0 || currentWaypointIndex >= currentPath.Count) return;

        int nodeId = currentPath[currentWaypointIndex];
        if (!navSystem.nodeMap.ContainsKey(nodeId))
        {
            if (showDebugLogs) Debug.LogWarning($"[Car] nodeMap does not contain ID {nodeId}");
            return;
        }

        NavNode target = navSystem.nodeMap[nodeId];
        if (target == null)
        {
            if (showDebugLogs) Debug.LogWarning($"[Car] target NavNode for ID {nodeId} is null");
            return;
        }

        Vector3 direction = target.worldPosition - transform.position;
        direction.y = 0f;
        float dist = direction.magnitude;

        if (dist > 0.2f)
        {
            direction.Normalize();
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
            rb.linearVelocity = transform.forward * moveSpeed;
        }
        else
        {
            if (showDebugLogs) Debug.Log($"[Car] Reached path node {nodeId} (index {currentWaypointIndex})");
            currentWaypointIndex++;
            if (currentWaypointIndex >= currentPath.Count)
            {
                if (showDebugLogs) Debug.Log("[Car] Reached final destination node – path complete");
                currentPath.Clear();
                navSystem.ClearPathVisualization();
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    void OnDisable()
    {
        // Clear path visualization when script is disabled or game stops
        if (navSystem != null)
        {
            navSystem.ClearPathVisualization();
        }
    }

    void OnDestroy()
    {
        // Clear path visualization when object is destroyed
        if (navSystem != null)
        {
            navSystem.ClearPathVisualization();
        }
    }

    private bool IsPlayerOffRoute()
    {
        if (currentPath == null || currentPath.Count == 0) return false;

        NavNode closestNode = GetClosestNode();
        if (closestNode == null) return false;

        int closestNodeID = closestNode.nodeID;

        // Check if closest node is in current path
        bool isOnPath = currentPath.Contains(closestNodeID);

        // Check distance to current path
        float minDistToPath = float.MaxValue;
        foreach (int nodeID in currentPath)
        {
            if (navSystem.nodeMap.ContainsKey(nodeID))
            {
                float dist = Vector3.Distance(transform.position, navSystem.nodeMap[nodeID].worldPosition);
                if (dist < minDistToPath)
                {
                    minDistToPath = dist;
                }
            }
        }

        // Player is off-route if:
        // 1. Closest node is NOT in current path, OR
        // 2. Distance to path exceeds threshold
        bool offRoute = !isOnPath || minDistToPath > offRouteThreshold;

        if (offRoute && showDebugLogs)
        {
            Debug.Log($"[Car] Off-route detected! Closest node: {closestNodeID}, On path: {isOnPath}, Distance to path: {minDistToPath:F2}");
        }

        return offRoute;
    }

    public void FindAndFollowPath()
    {
        if (navSystem == null)
        {
            Debug.LogWarning("[Car] navSystem is NULL – cannot pathfind");
            return;
        }
        if (targetNode == null)
        {
            Debug.LogWarning("[Car] targetNode is NULL – assign a NavNode as target");
            return;
        }

        if (navSystem.nodes == null || navSystem.nodes.Count == 0)
        {
            Debug.LogWarning("[Car] navSystem has no nodes – run Collect All Nodes / Setup Demo Scene");
            return;
        }

        NavNode startNode = GetClosestNode();
        if (startNode == null)
        {
            Debug.LogWarning("[Car] No closest node found to car position");
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log($"[Car] Finding path from node {startNode.nodeID} at {startNode.worldPosition} " +
                      $"to node {targetNode.nodeID} at {targetNode.worldPosition}");
        }

        List<int> path = navSystem.FindPath(startNode.nodeID, targetNode.nodeID);
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("[Car] FindPath returned null or empty – no path found");
            currentPath.Clear();
            navSystem.ClearPathVisualization();
            return;
        }

        currentPath = path;
        currentWaypointIndex = 1; // 0 is start node
        lastClosestNodeID = startNode.nodeID;

        if (showDebugLogs)
        {
            string pathStr = string.Join(" -> ", currentPath);
            Debug.Log($"[Car] Path found with {currentPath.Count} nodes: {pathStr}");
        }

        navSystem.VisualizePath(currentPath);
    }

    private NavNode GetClosestNode()
    {
        NavNode closest = null;
        float closestDist = float.MaxValue;
        Vector3 pos = transform.position;

        foreach (var node in navSystem.nodes)
        {
            if (node == null) continue;
            float d = Vector3.Distance(pos, node.worldPosition);
            if (d < closestDist)
            {
                closestDist = d;
                closest = node;
            }
        }

        if (showDebugLogs && closest != null)
        {
            Debug.Log($"[Car] Closest node is {closest.nodeID} at distance {closestDist:F2}");
        }

        return closest;
    }
}