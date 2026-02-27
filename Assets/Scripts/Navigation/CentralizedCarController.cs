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

    [Header("Path Visualization")]
    [Tooltip("Update the line renderer every frame to trim it as the car advances. " +
             "Uses the NavMesh hybrid dense route for accurate surface-following visualization.")]
    public bool updateVisualizationEveryFrame = true;

    [Tooltip("If non-zero, re-visualizes at this interval instead of every frame (cheaper). " +
             "0 = every frame.")]
    public float visualizationUpdateInterval = 0f;

    private float _vizUpdateTimer = 0f;

    [Header("Dynamic Route Update")]
    public bool autoUpdateRoute = true;
    public float routeUpdateInterval = 3f;
    public float offRouteThreshold = 20f;

    [Header("Car Ahead Raycast (Brake Reaction Time)")]
    [Tooltip("Raycast distance = max range AND threshold. If car detected within this distance, measure brake reaction time.")]
    public float raycastDistance = 50f;
    public float raycastHeightOffset = 0.5f;
    [Tooltip("LayerMask recommended: assign vehicles to a 'Vehicle' layer so raycast only hits cars.")]
    public LayerMask vehicleRaycastLayerMask = -1;
    [Tooltip("Optional: NWH VehicleController for brake input. If null, uses keyboard (S=brake).")]
    public VehicleController vehicleControllerForInput;

    [Header("Debug Gizmos")]
    public bool showDebugGizmos = true;

    private List<int> currentPath = new List<int>();
    private int currentWaypointIndex = 0;
    private Rigidbody rb;
    private float routeUpdateTimer = 0f;
    private int lastClosestNodeID = -1;

    // Car-ahead brake reaction time (stored here; data provider reads from this)
    private const int MaxReactionEvents = 50;
    private List<float> _reactionTimesSec = new List<float>();
    private float _carAheadDetectedTime = -1f;
    private float _sessionStartTime;

    // FIX #2: prevents brake-held spam after a reaction is recorded
    private bool _waitingForBrakeRelease = false;

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
            Debug.Log($"[Car] Start. navSystem={(navSystem != null)}, targetNode={(targetNode != null)}");

        if (autoFindPath && targetNode != null)
            FindAndFollowPath();

        _sessionStartTime = Time.time;
    }

    void Update()
    {
        DoCarAheadRaycastAndReaction();

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (showDebugLogs) Debug.Log("[Car] Space pressed – recalculating path");
            FindAndFollowPath();
        }

        if (autoUpdateRoute && targetNode != null && navSystem != null)
        {
            routeUpdateTimer += Time.deltaTime;

            if (routeUpdateTimer >= routeUpdateInterval)
            {
                if (showDebugLogs) Debug.Log("[Car] Periodic route update triggered");
                FindAndFollowPath();
                routeUpdateTimer = 0f;
            }
        }

        // ── Per-frame line renderer trim (NavMesh hybrid) ─────────────────────
        // Keeps the line starting at the car's actual road position even when
        // the car is between two far-apart nodes.
        if (updateVisualizationEveryFrame && navSystem != null &&
            currentPath != null && currentPath.Count > 0)
        {
            if (visualizationUpdateInterval <= 0f)
            {
                navSystem.VisualizePlayerPath(currentPath, transform.position);
            }
            else
            {
                _vizUpdateTimer += Time.deltaTime;
                if (_vizUpdateTimer >= visualizationUpdateInterval)
                {
                    navSystem.VisualizePlayerPath(currentPath, transform.position);
                    _vizUpdateTimer = 0f;
                }
            }
        }
        // ─────────────────────────────────────────────────────────────────────

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
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
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

    /// <summary>
    /// Raycast detects car in front. Accepts BOTH NWH VehicleController (player-type)
    /// AND TrafficVehicle (NPC) cars. TrafficVehicle sits on root so GetComponentInParent
    /// finds it from any child collider hit.
    /// Reaction time = time from detection until player brakes.
    /// </summary>
    void DoCarAheadRaycastAndReaction()
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;
        Vector3 direction = transform.forward;

        if (Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, vehicleRaycastLayerMask))
        {
            // ── VEHICLE TYPE CHECK ──────────────────────────────────────────────
            // NWH player-type car: VehicleController may be anywhere in hierarchy
            VehicleController hitVehicle = hit.collider.GetComponentInParent<VehicleController>();
            if (hitVehicle == null) hitVehicle = hit.collider.GetComponent<VehicleController>();

            // NPC traffic car: TrafficVehicle is always on root
            TrafficVehicle hitTrafficVehicle = hit.collider.GetComponentInParent<TrafficVehicle>();
            if (hitTrafficVehicle == null) hitTrafficVehicle = hit.collider.GetComponent<TrafficVehicle>();

            // Must be at least one recognized vehicle type
            bool isRecognizedVehicle = (hitVehicle != null) || (hitTrafficVehicle != null);
            if (!isRecognizedVehicle) return;

            // FIX #3: root-based self-check covers nested hierarchies
            if (hit.collider.transform.root == transform.root) return;
            // ────────────────────────────────────────────────────────────────────

            float distanceToCar = hit.distance;

            // FIX #2: wait for brake release before starting a new detection cycle
            if (_waitingForBrakeRelease)
            {
                if (GetBrakeInput() <= 0.05f)
                {
                    _waitingForBrakeRelease = false;
                    if (showDebugLogs) Debug.Log("[Car] Brake released – ready for next reaction measurement");
                }
                return;
            }

            // FIX #1: else-if ensures detection frame != brake-check frame
            // (prevents instant 0s recording when player is already braking)
            if (_carAheadDetectedTime < 0f)
            {
                _carAheadDetectedTime = Time.time;

                string vehicleType = hitTrafficVehicle != null ? "NPC TrafficVehicle" : "VehicleController";
                if (showDebugLogs)
                    Debug.Log($"[Car] Car ahead ({vehicleType}) at {distanceToCar:F1}m – measuring brake reaction");
            }
            else if (GetBrakeInput() > 0.05f)
            {
                float reactionTime = Time.time - _carAheadDetectedTime;
                AddReactionTime(reactionTime);
                if (showDebugLogs)
                    Debug.Log($"[Car] Brake reaction: {reactionTime:F2}s (car ahead {distanceToCar:F1}m)");
                _carAheadDetectedTime = -1f;
                _waitingForBrakeRelease = true;
            }
        }
        else
        {
            // No vehicle in range – reset detection state.
            // Do NOT reset _waitingForBrakeRelease here: car may briefly leave
            // range while brake is still physically held.
            _carAheadDetectedTime = -1f;
        }
    }

    /// <summary>
    /// Always-visible gizmo for the car-ahead raycast.
    /// Color encodes detection state:
    ///   Cyan   = idle, no car in range
    ///   Yellow = car detected, reaction timer running
    ///   Red    = brake pressed / waiting for brake release cooldown
    /// </summary>
    void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Vector3 origin = transform.position + Vector3.up * raycastHeightOffset;
        Vector3 direction = transform.forward;

        // Color encodes current detection state
        if (_waitingForBrakeRelease)
            Gizmos.color = Color.red;
        else if (_carAheadDetectedTime >= 0f)
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = Color.cyan;

        // Main raycast line
        Gizmos.DrawLine(origin, origin + direction * raycastDistance);

        // End-cap sphere at max range
        Gizmos.DrawWireSphere(origin + direction * raycastDistance, 0.3f);

        // If car is detected, draw a sphere at the actual hit point
        if (_carAheadDetectedTime >= 0f)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, raycastDistance, vehicleRaycastLayerMask))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(hit.point, 0.6f);

                // Highlight line from origin to hit point
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, hit.point);
            }
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
        _carAheadDetectedTime = -1f;
        _waitingForBrakeRelease = false;
    }

    void OnDisable()
    {
        if (navSystem != null)
            navSystem.ClearPathVisualization();
    }

    void OnDestroy()
    {
        if (navSystem != null)
            navSystem.ClearPathVisualization();
    }

    private bool IsPlayerOffRoute()
    {
        if (currentPath == null || currentPath.Count == 0) return false;

        NavNode closestNode = GetClosestNode();
        if (closestNode == null) return false;

        int closestNodeID = closestNode.nodeID;
        bool isOnPath = currentPath.Contains(closestNodeID);

        float minDistToPath = float.MaxValue;
        foreach (int nodeID in currentPath)
        {
            if (navSystem.nodeMap.ContainsKey(nodeID))
            {
                float dist = Vector3.Distance(transform.position, navSystem.nodeMap[nodeID].worldPosition);
                if (dist < minDistToPath) minDistToPath = dist;
            }
        }

        bool offRoute = !isOnPath || minDistToPath > offRouteThreshold;

        if (offRoute && showDebugLogs)
            Debug.Log($"[Car] Off-route detected! Closest node: {closestNodeID}, On path: {isOnPath}, Distance to path: {minDistToPath:F2}");

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
            Debug.Log($"[Car] Finding path from node {startNode.nodeID} at {startNode.worldPosition} " +
                      $"to node {targetNode.nodeID} at {targetNode.worldPosition}");

        List<int> path = navSystem.FindPath(startNode.nodeID, targetNode.nodeID);
        if (path == null || path.Count == 0)
        {
            Debug.LogWarning("[Car] FindPath returned null or empty – no path found");
            currentPath.Clear();
            navSystem.ClearPathVisualization();
            return;
        }

        currentPath = path;
        currentWaypointIndex = 1;
        lastClosestNodeID = startNode.nodeID;

        if (showDebugLogs)
        {
            string pathStr = string.Join(" -> ", currentPath);
            Debug.Log($"[Car] Path found with {currentPath.Count} nodes: {pathStr}");
        }

        // Invalidate cache so dense route is rebuilt for the new path
        navSystem.InvalidatePlayerPathCache();
        navSystem.VisualizePlayerPath(currentPath, transform.position);
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
            Debug.Log($"[Car] Closest node is {closest.nodeID} at distance {closestDist:F2}");

        return closest;
    }
}