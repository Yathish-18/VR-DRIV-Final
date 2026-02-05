using UnityEngine;
using System.Collections.Generic;
using NWH.VehiclePhysics2;

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

    [Header("Traffic Light Raycast (Reaction Time)")]
    [Tooltip("Raycast forward to detect traffic light zone collider (EnhancedTrafficLightViolationDetector)")]
    public float raycastDistance = 60f;
    public float raycastHeightOffset = 0.5f;
    public LayerMask trafficLightLayerMask = -1;
    [Tooltip("Optional: NWH VehicleController for brake/throttle input. If null, uses keyboard (S=brake, W=throttle).")]
    public VehicleController vehicleControllerForInput;

    private List<int> currentPath = new List<int>();
    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    private float routeUpdateTimer = 0f;
    private int lastClosestNodeID = -1;

    // Traffic light reaction time (stored here; data provider reads from this)
    private const int MaxReactionEvents = 50;
    private List<float> _reactionTimesSec = new List<float>();
    private TrafficLightController _trackedTrafficLight;
    private TrafficLightController.LightState _previousLightState;
    private float _yellowToRedTime = -1f;
    private float _yellowToGreenTime = -1f;
    private float _distanceAtLightChange = -1f;
    private string _trackedLightID = "";
    private float _sessionStartTime;

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

        _sessionStartTime = Time.time;
        _previousLightState = TrafficLightController.LightState.Red; // will be overwritten when we hit a light
    }

    void Update()
    {
        // Traffic light raycast and reaction time (runs every frame when car is active)
        DoTrafficLightRaycastAndReaction();

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

    /// <summary>Raycast hits = car is near. Only measure reaction when signal changes *after* we're already tracking (sudden change).</summary>
    void DoTrafficLightRaycastAndReaction()
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;
        Vector3 direction = transform.forward;
        float sessionTime = Time.time - _sessionStartTime;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, trafficLightLayerMask))
        {
            var detector = hit.collider.GetComponent<EnhancedTrafficLightViolationDetector>();
            if (detector == null) return;

            TrafficLightController lightController = detector.GetTrafficLight();
            if (lightController == null) return;

            float distanceToLight = hit.distance;
            TrafficLightController.LightState currentState = lightController.currentState;

            // First frame we see this light: only init tracking (no reaction yet – car just became "near")
            if (_trackedTrafficLight != lightController)
            {
                _trackedTrafficLight = lightController;
                _previousLightState = currentState;
                _trackedLightID = lightController.GetTrafficLightID();
                _yellowToRedTime = -1f;
                _yellowToGreenTime = -1f;
            }
            else
            {
                // Already tracking (raycast was hitting): signal change = sudden → measure reaction
                // Detect Yellow -> Red: record time and wait for brake
                if (_previousLightState == TrafficLightController.LightState.Yellow && currentState == TrafficLightController.LightState.Red)
                {
                    _yellowToRedTime = Time.time;
                    _distanceAtLightChange = distanceToLight;
                    if (showDebugLogs) Debug.Log($"[Car] Yellow→Red at {_trackedLightID}, distance={distanceToLight:F1}m – measuring brake reaction");
                }
                // Detect Yellow -> Green: record time and wait for throttle
                if (_previousLightState == TrafficLightController.LightState.Yellow && currentState == TrafficLightController.LightState.Green)
                {
                    _yellowToGreenTime = Time.time;
                    _distanceAtLightChange = distanceToLight;
                    if (showDebugLogs) Debug.Log($"[Car] Yellow→Green at {_trackedLightID}, distance={distanceToLight:F1}m – measuring accelerator reaction");
                }
                _previousLightState = currentState;
            }

            // Check for brake reaction (after Yellow->Red)
            if (_yellowToRedTime > 0f && GetBrakeInput() > 0.05f)
            {
                float reactionTime = Time.time - _yellowToRedTime;
                AddReactionTime(reactionTime);
                if (showDebugLogs) Debug.Log($"[Car] Brake reaction: {reactionTime:F2}s at {_trackedLightID}");
                _yellowToRedTime = -1f;
            }

            // Check for accelerator reaction (after Yellow->Green)
            if (_yellowToGreenTime > 0f && GetThrottleInput() > 0.05f)
            {
                float reactionTime = Time.time - _yellowToGreenTime;
                AddReactionTime(reactionTime);
                if (showDebugLogs) Debug.Log($"[Car] Accelerator reaction: {reactionTime:F2}s at {_trackedLightID}");
                _yellowToGreenTime = -1f;
            }
        }
        else
        {
            _trackedTrafficLight = null;
        }
    }

    float GetBrakeInput()
    {
        if (vehicleControllerForInput != null && vehicleControllerForInput.input != null)
            return vehicleControllerForInput.input.InputSwappedBrakes;
        return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1f : 0f;
    }

    float GetThrottleInput()
    {
        if (vehicleControllerForInput != null && vehicleControllerForInput.input != null)
            return vehicleControllerForInput.input.InputSwappedThrottle;
        return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1f : 0f;
    }

    void AddReactionTime(float reactionTimeSec)
    {
        _reactionTimesSec.Add(reactionTimeSec);
        if (_reactionTimesSec.Count > MaxReactionEvents)
            _reactionTimesSec.RemoveAt(0);
    }

    /// <summary>Average reaction time this session. Data provider reads this.</summary>
    public float GetAverageReactionTime()
    {
        if (_reactionTimesSec.Count == 0) return -1f;
        float sum = 0f;
        foreach (float t in _reactionTimesSec) sum += t;
        return sum / _reactionTimesSec.Count;
    }

    /// <summary>Worst (slowest) reaction time this session. Data provider reads this.</summary>
    public float GetWorstReactionTime()
    {
        if (_reactionTimesSec.Count == 0) return -1f;
        float max = 0f;
        foreach (float t in _reactionTimesSec)
            if (t > max) max = t;
        return max;
    }

    /// <summary>Clear reaction data (e.g. when starting new session).</summary>
    public void ClearReactionData()
    {
        _reactionTimesSec.Clear();
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