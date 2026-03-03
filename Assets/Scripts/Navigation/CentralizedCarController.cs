// ============================================================================
//  CENTRALIZED CAR CONTROLLER  v3.0  —  PLAYER ROUTE VISUALIZATION ONLY
//  ============================================================================
//  PURPOSE:
//    Draws the player car's planned route on screen as a line renderer.
//    Also tracks brake reaction time for research/scoring purposes.
//    That is ALL this script does.
//
//  DOES NOT:
//    ✗ Touch any Rigidbody  (no velocity, forces, or constraints — ever)
//    ✗ Move the car
//    ✗ Handle input
//    ✗ Compete with VehicleController (NWH VehiclePhysics2)
//
//  DOES:
//    ✓ Finds nearest NavNode to the player each frame (cached, 4Hz)
//    ✓ Requests a route from CentralizedNavigationSystem
//    ✓ Calls VisualizePlayerPath() → draws trimmed route ahead of car
//    ✓ Measures brake reaction time (obstacle ray + speed read-only check)
//    ✓ Provides GetAverageReactionTime() / GetWorstReactionTime() for scoring
//    ✓ Provides test followPath mode (Transform only, no Rigidbody)
//
//  HISTORY:
//    v1/v2 grabbed Rigidbody in Awake() and wrote rb.linearVelocity every
//    frame, causing player input to feel sluggish and the car to slow down.
//    All Rigidbody writes are permanently removed in v3.0.
//    The Rigidbody is read (speed only) in MeasureBrakeReaction() but
//    is never written to under any circumstances.
// ============================================================================

using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CentralizedCarController : MonoBehaviour
{
    // =========================================================================
    //  INSPECTOR
    // =========================================================================

    [Header("═══  NAVIGATION SYSTEM  ═══")]
    [Tooltip("Reference to CentralizedNavigationSystem. Auto-found if null.")]
    public CentralizedNavigationSystem navSystem;

    [Header("═══  ROUTE VISUALIZATION  ═══")]
    [Tooltip("Draw the player car's planned route as a line in the scene.")]
    public bool showRouteVisualization = true;

    [Tooltip("How often (seconds) to check if a new route is needed.\n" +
             "0.5s is responsive enough for normal driving.")]
    [Range(0.1f, 3f)]
    public float routeRefreshInterval = 0.5f;

    [Header("═══  FOLLOW PATH  (Test / Debug Only)  ═══")]
    [Tooltip("When true the car follows the route using Transform.position.\n\n" +
             "FOR TESTING ONLY — disable in production.\n" +
             "Uses kinematic Transform movement so it cannot fight VehicleController.\n" +
             "With this ON the physics car will be overridden visually.")]
    public bool followPath = false;

    [Tooltip("Speed in m/s used by the test follow mode. Does not affect VehicleController.")]
    [Range(1f, 30f)]
    public float testFollowSpeed = 10f;

    [Tooltip("Turn speed used by the test follow mode.")]
    [Range(1f, 10f)]
    public float testTurnSpeed = 4f;

    [Tooltip("XZ distance at which a waypoint is reached in test follow mode.")]
    [Range(1f, 10f)]
    public float testWaypointReachDistance = 4f;

    [Header("═══  BRAKE REACTION  (Research / Scoring)  ═══")]
    [Tooltip("Layer(s) for the forward obstacle detection ray.\n" +
             "Does not affect car movement — read-only research data.")]
    public LayerMask obstacleDetectionLayer;

    [Tooltip("How far ahead (metres) to cast the obstacle detection ray.")]
    [Range(5f, 60f)]
    public float reactionRayDistance = 20f;

    // =========================================================================
    //  PRIVATE STATE
    // =========================================================================

    // Route
    private List<int>     _currentNodePath  = new List<int>();
    private List<Vector3> _currentWaypoints = new List<Vector3>();
    private int           _waypointIndex    = 0;
    private int           _currentSourceNode = -1;
    private int           _currentDestNode   = -1;
    private float         _routeRefreshTimer = 0f;

    // Closest node cache (refreshed at CLOSEST_NODE_CACHE_HZ)
    private int   _lastClosestNode        = -1;
    private float _closestNodeCacheTimer  = 0f;
    private const float CLOSEST_NODE_CACHE_HZ = 0.25f;

    // Brake reaction measurement
    private bool  _obstacleLastFrame      = false;
    private float _obstacleFirstSeenTime  = -1f;
    private bool  _reactionFired          = false;
    private float _lastReactionTime       = -1f;

    // Reaction time history
    private float _reactionTimeSum        = 0f;
    private int   _reactionTimeCount      = 0;
    private float _worstReactionTime      = -1f;

    // Rigidbody — read-only reference, never written to
    private Rigidbody _rb;

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    private void Start()
    {
        // Auto-find nav system
        if (navSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
            navSystem = Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif
            if (navSystem == null)
            {
                Debug.LogError($"[CarController] {name}: No CentralizedNavigationSystem found. " +
                               "Route visualization disabled.");
                enabled = false;
                return;
            }
        }

        // Cache Rigidbody for read-only speed access in MeasureBrakeReaction()
        // We NEVER write to this — VehicleController owns it completely.
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (navSystem == null) return;

        _routeRefreshTimer     += Time.deltaTime;
        _closestNodeCacheTimer += Time.deltaTime;

        // Route refresh
        if (_routeRefreshTimer >= routeRefreshInterval)
        {
            _routeRefreshTimer = 0f;
            RefreshRouteIfNeeded();
        }

        // Visualization
        if (showRouteVisualization && _currentNodePath.Count > 0)
            navSystem.VisualizePlayerPath(_currentNodePath, transform.position);
        else if (!showRouteVisualization)
            navSystem.ClearPathVisualization();

        // Test follow mode (Transform only — no Rigidbody writes)
        if (followPath && _currentWaypoints.Count > 0)
            TestFollowPath();

        // Brake reaction measurement (read-only research data)
        MeasureBrakeReaction();
    }

    private void OnDisable()
    {
        if (navSystem != null) navSystem.ClearPathVisualization();
    }

    // =========================================================================
    //  ROUTE MANAGEMENT
    // =========================================================================

    private void RefreshRouteIfNeeded()
    {
        if (!navSystem.RouteCacheReady) return;

        int closestNode = GetClosestNodeCached();
        if (closestNode == -1) return;

        // Request new route only when we've moved to a different source node
        // or we have no route yet
        if (_currentNodePath.Count > 0 && closestNode == _currentSourceNode) return;

        var result = navSystem.RequestRoute(closestNode);
        if (!result.success)
        {
            Debug.LogWarning($"[CarController] Route request failed from node {closestNode}: {result.failReason}");
            return;
        }

        // Release old occupancy
        if (_currentSourceNode != -1 && _currentDestNode != -1)
            navSystem.ReleaseRoute(_currentSourceNode, _currentDestNode);

        _currentSourceNode = result.sourceNodeID;
        _currentDestNode   = result.destinationNodeID;
        _currentWaypoints  = result.waypoints ?? new List<Vector3>();
        _waypointIndex     = 0;

        navSystem.InvalidatePlayerPathCache();

        // Rebuild node path for the line renderer
        var nodePath = navSystem.FindPath(result.sourceNodeID, result.destinationNodeID);
        _currentNodePath = nodePath ?? new List<int>();
    }

    /// <summary>
    /// Forces an immediate route refresh regardless of refresh interval.
    /// Call this when the player enters a new zone or you want a new destination.
    /// </summary>
    public void ForceRouteRefresh()
    {
        _currentNodePath.Clear();
        _currentWaypoints.Clear();
        _waypointIndex     = 0;
        _currentSourceNode = -1;
        _currentDestNode   = -1;
        navSystem.InvalidatePlayerPathCache();
        RefreshRouteIfNeeded();
    }

    // =========================================================================
    //  TEST FOLLOW PATH  (Transform only — no Rigidbody writes)
    // =========================================================================

    /// <summary>
    /// Moves the car along the current route using Transform.position directly.
    ///
    /// IMPORTANT: This is kinematic-style movement — it does NOT write to any
    /// Rigidbody. It cannot interfere with VehicleController physics.
    ///
    /// Only active when followPath = true (default: false).
    /// In production followPath should be false and VehicleController drives.
    /// </summary>
    private void TestFollowPath()
    {
        if (_waypointIndex >= _currentWaypoints.Count)
        {
            // End of route — clear and let RefreshRouteIfNeeded pick a new one
            _currentNodePath.Clear();
            navSystem.InvalidatePlayerPathCache();
            return;
        }

        Vector3 target    = _currentWaypoints[_waypointIndex];
        Vector3 toTarget  = target - transform.position;
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);

        if (toTargetXZ.magnitude < testWaypointReachDistance)
        { _waypointIndex++; return; }

        if (toTargetXZ.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTargetXZ.normalized);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot,
                                                    testTurnSpeed * Time.deltaTime);
        }

        transform.position += transform.forward * testFollowSpeed * Time.deltaTime;
    }

    // =========================================================================
    //  CLOSEST NODE CACHE
    // =========================================================================

    private int GetClosestNodeCached()
    {
        if (_lastClosestNode == -1 || _closestNodeCacheTimer >= CLOSEST_NODE_CACHE_HZ)
        {
            _closestNodeCacheTimer = 0f;
            _lastClosestNode = navSystem.GetClosestNode(transform.position);
        }
        return _lastClosestNode;
    }

    // =========================================================================
    //  BRAKE REACTION MEASUREMENT
    // =========================================================================

    /// <summary>
    /// Casts a forward ray to detect obstacles and measures the time between
    /// the obstacle appearing and the player braking.
    ///
    /// Read-only research data — no movement impact whatsoever.
    /// Speed is read from _rb.linearVelocity (never written).
    /// Results feed into DashboardDataProvider and EnhancedDriverScoringSystem.
    /// </summary>
    private void MeasureBrakeReaction()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        bool    hitNow = Physics.Raycast(origin, transform.forward,
                                         reactionRayDistance, obstacleDetectionLayer);

        if (hitNow && !_obstacleLastFrame)
        {
            // New obstacle appeared
            _obstacleFirstSeenTime = Time.time;
            _reactionFired         = false;
        }
        else if (!hitNow && _obstacleLastFrame)
        {
            // Obstacle cleared
            _obstacleFirstSeenTime = -1f;
            _reactionFired         = false;
        }

        // Detect brake: car has slowed significantly while obstacle is present
        // _rb is read-only — we never write velocity here
        if (hitNow && !_reactionFired && _obstacleFirstSeenTime > 0f && _rb != null)
        {
            float speed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
            if (speed < 0.5f)   // Effectively stopped
            {
                _lastReactionTime  = Time.time - _obstacleFirstSeenTime;
                _reactionFired     = true;

                _reactionTimeSum  += _lastReactionTime;
                _reactionTimeCount++;
                if (_lastReactionTime > _worstReactionTime)
                    _worstReactionTime = _lastReactionTime;

                Debug.Log($"[CarController] Brake reaction: {_lastReactionTime * 1000f:F0} ms  " +
                          $"| Avg: {GetAverageReactionTime() * 1000f:F0} ms  " +
                          $"| Worst: {_worstReactionTime * 1000f:F0} ms");
            }
        }

        _obstacleLastFrame = hitNow;
    }

    // =========================================================================
    //  PUBLIC API  (used by DashboardDataProvider and EnhancedDriverScoringSystem)
    // =========================================================================

    /// <summary>
    /// Average brake reaction time across all recorded events in seconds.
    /// Returns -1 if no events recorded yet.
    /// Called by DashboardDataProvider.GetAvgReactionTimeSec().
    /// </summary>
    public float GetAverageReactionTime()
        => _reactionTimeCount > 0 ? _reactionTimeSum / _reactionTimeCount : -1f;

    /// <summary>
    /// Worst (longest) brake reaction time in seconds.
    /// Returns -1 if no events recorded yet.
    /// Called by DashboardDataProvider.GetWorstReactionTimeSec().
    /// </summary>
    public float GetWorstReactionTime()
        => _reactionTimeCount > 0 ? _worstReactionTime : -1f;

    /// <summary>
    /// Resets all reaction time history.
    /// Called by DashboardDataProvider.ClearStoredData().
    /// </summary>
    public void ClearReactionData()
    {
        _reactionTimeSum       = 0f;
        _reactionTimeCount     = 0;
        _worstReactionTime     = -1f;
        _lastReactionTime      = -1f;
        _obstacleFirstSeenTime = -1f;
        _obstacleLastFrame     = false;
        _reactionFired         = false;
    }

    // ── Read-only route info (for DashboardDataProvider + editor tools) ───────

    /// <summary>Last measured brake reaction time in seconds. -1 if none yet.</summary>
    public float LastBrakeReactionTime => _lastReactionTime;

    /// <summary>Current planned route node IDs.</summary>
    public List<int> CurrentNodePath => _currentNodePath;

    /// <summary>Current planned route dense waypoints.</summary>
    public List<Vector3> CurrentWaypoints => _currentWaypoints;

    /// <summary>Source node of the current route.</summary>
    public int CurrentSourceNode => _currentSourceNode;

    /// <summary>Destination node of the current route.</summary>
    public int CurrentDestNode => _currentDestNode;

    // =========================================================================
    //  GIZMOS
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Reaction ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f,
                       transform.forward * reactionRayDistance);

        // Current waypoint target (test follow mode)
        if (_currentWaypoints != null && _waypointIndex < _currentWaypoints.Count)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_currentWaypoints[_waypointIndex], 0.4f);
            Gizmos.DrawLine(transform.position, _currentWaypoints[_waypointIndex]);
        }
    }
#endif
}