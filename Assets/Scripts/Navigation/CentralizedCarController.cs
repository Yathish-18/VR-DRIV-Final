// ============================================================================
//  CENTRALIZED CAR CONTROLLER  v3.0  —  PLAYER ROUTE VISUALIZATION ONLY
//  ============================================================================
//  PURPOSE:
//    Visualizes the player car's current planned route as a line in the scene.
//    That's it. This script does NOTHING else.
//
//  WHAT THIS SCRIPT DOES NOT DO:
//    ✗ Does NOT touch any Rigidbody
//    ✗ Does NOT set velocity, forces, or constraints
//    ✗ Does NOT move the car
//    ✗ Does NOT handle input
//    ✗ Does NOT compete with VehicleController.cs in any way
//
//  WHAT THIS SCRIPT DOES:
//    ✓ Finds the nearest NavNode to the player car each frame
//    ✓ Requests a route from CentralizedNavigationSystem
//    ✓ Calls VisualizePlayerPath() to draw the route as a line renderer
//    ✓ Provides a test "follow path" mode (editor/debug only)
//    ✓ Measures brake reaction time (research data, read-only)
//
//  PHYSICS SAFETY:
//    This script caches but NEVER modifies the Rigidbody.
//    All movement in follow-path test mode uses Transform.position directly
//    (kinematic-style) so it cannot interfere with VehicleController physics.
//    Set followPath = false (default) for normal gameplay — script becomes
//    pure visualization with zero runtime cost.
//
//  RIGIDBODY HISTORY:
//    Previous versions grabbed the Rigidbody in Awake() and wrote
//    rb.linearVelocity every frame, causing:
//      • Player input to feel sluggish
//      • Car slowing down unexpectedly
//      • Input fighting between this script and VehicleController
//    All Rigidbody writes are permanently removed.
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

    [Tooltip("Reference to the CentralizedNavigationSystem in the scene.")]
    public CentralizedNavigationSystem navSystem;

    [Header("═══  ROUTE VISUALIZATION  ═══")]

    [Tooltip("Show the player car's planned route as a line in the scene view and game view.")]
    public bool showRouteVisualization = true;

    [Tooltip("Interval in seconds between route refresh checks. " +
             "Lower = more responsive but slightly more CPU. 0.5s is fine for most roads.")]
    [Range(0.1f, 2f)]
    public float routeRefreshInterval = 0.5f;

    [Header("═══  FOLLOW PATH (Test / Debug Only)  ═══")]

    [Tooltip("When true, the car autonomously follows the planned route using Transform.position.\n\n" +
             "FOR TESTING ONLY — disable in production.\n" +
             "Uses kinematic movement (no Rigidbody writes) so it cannot fight VehicleController.\n" +
             "However, with this ON the physics car will be overridden visually.")]
    public bool followPath = false;

    [Tooltip("Speed in m/s when followPath is enabled. Does not affect VehicleController speed.")]
    [Range(1f, 30f)]
    public float testFollowSpeed = 10f;

    [Tooltip("Turn speed when followPath is enabled.")]
    [Range(1f, 10f)]
    public float testTurnSpeed = 4f;

    [Tooltip("XZ distance at which a waypoint is considered reached during test follow.")]
    [Range(1f, 10f)]
    public float testWaypointReachDistance = 4f;

    [Header("═══  BRAKE REACTION (Research Data)  ═══")]

    [Tooltip("Layer(s) for the forward obstacle detection ray. Does not affect car movement.")]
    public LayerMask obstacleDetectionLayer;

    [Tooltip("How far ahead to cast the reaction-time ray.")]
    [Range(5f, 50f)]
    public float reactionRayDistance = 20f;

    // =========================================================================
    //  PRIVATE STATE
    // =========================================================================

    // Route data
    private List<int>     _currentNodePath = new List<int>();
    private List<Vector3> _currentWaypoints = new List<Vector3>();
    private int           _waypointIndex    = 0;
    private int           _currentSourceNode = -1;
    private int           _currentDestNode   = -1;
    private float         _routeRefreshTimer = 0f;

    // Brake reaction measurement (research only — no movement impact)
    private bool  _obstacleDetectedLastFrame = false;
    private float _obstacleFirstSeenTime     = -1f;
    private bool  _reactionFired             = false;
    private float _lastReactionTime          = -1f;

    // Reaction time history for average / worst tracking
    private float _reactionTimeSum           = 0f;
    private int   _reactionTimeCount         = 0;
    private float _worstReactionTime         = -1f;

    // Closest node cache
    private int   _lastClosestNode = -1;
    private float _closestNodeCacheTimer = 0f;
    private const float CLOSEST_NODE_CACHE_INTERVAL = 0.25f;

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    private void Start()
    {
        // Auto-find nav system if not assigned
        if (navSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
            navSystem = Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif
            if (navSystem == null)
            {
                Debug.LogError($"[CarController] {name}: No CentralizedNavigationSystem found in scene. " +
                               "Route visualization disabled.");
                enabled = false;
                return;
            }
        }

        // NOTE: We intentionally do NOT get or modify any Rigidbody here.
        // VehicleController.cs owns the Rigidbody completely.
        // This script has zero physics involvement.
    }

    private void Update()
    {
        if (navSystem == null) return;

        // ── Route refresh ─────────────────────────────────────────────────────
        _routeRefreshTimer       += Time.deltaTime;
        _closestNodeCacheTimer   += Time.deltaTime;

        if (_routeRefreshTimer >= routeRefreshInterval)
        {
            _routeRefreshTimer = 0f;
            RefreshRouteIfNeeded();
        }

        // ── Path visualization ────────────────────────────────────────────────
        if (showRouteVisualization && _currentNodePath.Count > 0)
            navSystem.VisualizePlayerPath(_currentNodePath, transform.position);
        else if (!showRouteVisualization)
            navSystem.ClearPathVisualization();

        // ── Test follow path (debug only, no Rigidbody) ───────────────────────
        if (followPath && _currentWaypoints.Count > 0)
            TestFollowPath();

        // ── Brake reaction measurement (research data, read-only) ─────────────
        MeasureBrakeReaction();
    }

    private void OnDisable()
    {
        // Clean up visualization when component is disabled
        if (navSystem != null)
            navSystem.ClearPathVisualization();
    }

    // =========================================================================
    //  ROUTE MANAGEMENT
    // =========================================================================

    private void RefreshRouteIfNeeded()
    {
        if (!navSystem.RouteCacheReady) return;

        int closestNode = GetClosestNodeCached();
        if (closestNode == -1) return;

        // Only request a new route if we've moved to a new source node
        // or we don't have a route yet
        bool needsNewRoute = _currentNodePath.Count == 0
                          || closestNode != _currentSourceNode;

        if (!needsNewRoute) return;

        var result = navSystem.RequestRoute(closestNode);

        if (!result.success)
        {
            Debug.LogWarning($"[CarController] Route request failed from node {closestNode}: " +
                             result.failReason);
            return;
        }

        // Release old route occupancy
        if (_currentSourceNode != -1 && _currentDestNode != -1)
            navSystem.ReleaseRoute(_currentSourceNode, _currentDestNode);

        _currentSourceNode = result.sourceNodeID;
        _currentDestNode   = result.destinationNodeID;
        _currentNodePath   = result.waypoints != null ? new List<int>() : new List<int>();

        // Store waypoints for test-follow mode
        _currentWaypoints = result.waypoints ?? new List<Vector3>();
        _waypointIndex    = 0;

        // Invalidate visualization cache so it rebuilds with new path
        navSystem.InvalidatePlayerPathCache();

        // Build node path for visualization (closest path from node graph)
        RebuildNodePathFromWaypoints(result.sourceNodeID, result.destinationNodeID);
    }

    private void RebuildNodePathFromWaypoints(int srcID, int dstID)
    {
        List<int> nodePath = navSystem.FindPath(srcID, dstID);
        _currentNodePath = nodePath ?? new List<int>();
    }

    /// <summary>
    /// Manually request a new route. Call this from other scripts
    /// (e.g. when the player enters a new area or the destination changes).
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
    //  TEST FOLLOW PATH  (debug only — no Rigidbody writes)
    // =========================================================================

    /// <summary>
    /// Moves the car along the current route using Transform directly.
    /// This is KINEMATIC-STYLE movement — it does not write to any Rigidbody.
    ///
    /// Only active when followPath = true.
    /// In production (followPath = false) this method is never called.
    /// VehicleController.cs owns movement in production.
    /// </summary>
    private void TestFollowPath()
    {
        if (_waypointIndex >= _currentWaypoints.Count)
        {
            // Reached end of route — request a new one
            _currentNodePath.Clear();
            navSystem.InvalidatePlayerPathCache();
            return;
        }

        Vector3 target    = _currentWaypoints[_waypointIndex];
        Vector3 toTarget  = target - transform.position;
        Vector3 toTargetXZ = new Vector3(toTarget.x, 0f, toTarget.z);

        // Advance waypoint if close enough
        if (toTargetXZ.magnitude < testWaypointReachDistance)
        {
            _waypointIndex++;
            return;
        }

        // Rotate toward target
        if (toTargetXZ.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(toTargetXZ.normalized);
            transform.rotation   = Quaternion.Slerp(transform.rotation, targetRot,
                                                    testTurnSpeed * Time.deltaTime);
        }

        // Move forward via Transform (no Rigidbody involvement)
        transform.position += transform.forward * testFollowSpeed * Time.deltaTime;
    }

    // =========================================================================
    //  CLOSEST NODE CACHE
    // =========================================================================

    private int GetClosestNodeCached()
    {
        // Re-query only on cache interval to avoid calling GetClosestNode every frame
        if (_lastClosestNode == -1 || _closestNodeCacheTimer >= CLOSEST_NODE_CACHE_INTERVAL)
        {
            _closestNodeCacheTimer = 0f;
            _lastClosestNode = navSystem.GetClosestNode(transform.position);
        }
        return _lastClosestNode;
    }

    // =========================================================================
    //  BRAKE REACTION MEASUREMENT  (research data, read-only)
    // =========================================================================

    /// <summary>
    /// Casts a forward ray to detect obstacles and measures how long before
    /// the player reacts (brakes). Purely observational — does not affect movement.
    /// </summary>
    private void MeasureBrakeReaction()
    {
        Vector3 origin    = transform.position + Vector3.up * 0.5f;
        bool    hitNow    = Physics.Raycast(origin, transform.forward, reactionRayDistance,
                                            obstacleDetectionLayer);

        if (hitNow && !_obstacleDetectedLastFrame)
        {
            // Obstacle just appeared
            _obstacleFirstSeenTime = Time.time;
            _reactionFired         = false;
        }
        else if (!hitNow && _obstacleDetectedLastFrame)
        {
            // Obstacle cleared
            _obstacleFirstSeenTime = -1f;
            _reactionFired         = false;
        }

        // Detect braking as significant deceleration
        // We read velocity from Rigidbody here (read-only, no writes)
        if (hitNow && !_reactionFired && _obstacleFirstSeenTime > 0f)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                float speed = new Vector2(rb.linearVelocity.x, rb.linearVelocity.z).magnitude;
                if (speed < 0.5f) // Car has slowed significantly
                {
                    _lastReactionTime = Time.time - _obstacleFirstSeenTime;
                    _reactionFired    = true;

                    // Accumulate for average / worst
                    _reactionTimeSum  += _lastReactionTime;
                    _reactionTimeCount++;
                    if (_lastReactionTime > _worstReactionTime)
                        _worstReactionTime = _lastReactionTime;

                    Debug.Log($"[CarController] Brake reaction time: {_lastReactionTime * 1000f:F0} ms | " +
                              $"Avg: {GetAverageReactionTime() * 1000f:F0} ms | " +
                              $"Worst: {_worstReactionTime * 1000f:F0} ms");
                }
            }
        }

        _obstacleDetectedLastFrame = hitNow;
    }

    // =========================================================================
    //  PUBLIC API
    // =========================================================================

    /// <summary>Last measured brake reaction time in seconds. -1 if not yet measured.</summary>
    public float LastBrakeReactionTime => _lastReactionTime;

    /// <summary>
    /// Average brake reaction time across all recorded events, in seconds.
    /// Returns -1 if no reactions recorded yet.
    /// Called by DashboardDataProvider.
    /// </summary>
    public float GetAverageReactionTime()
        => _reactionTimeCount > 0 ? _reactionTimeSum / _reactionTimeCount : -1f;

    /// <summary>
    /// Worst (longest) brake reaction time recorded this session, in seconds.
    /// Returns -1 if no reactions recorded yet.
    /// Called by DashboardDataProvider.
    /// </summary>
    public float GetWorstReactionTime()
        => _reactionTimeCount > 0 ? _worstReactionTime : -1f;

    /// <summary>
    /// Resets all reaction time history. Called by DashboardDataProvider.ClearStoredData().
    /// </summary>
    public void ClearReactionData()
    {
        _reactionTimeSum          = 0f;
        _reactionTimeCount        = 0;
        _worstReactionTime        = -1f;
        _lastReactionTime         = -1f;
        _obstacleFirstSeenTime    = -1f;
        _obstacleDetectedLastFrame = false;
        _reactionFired            = false;
    }

    /// <summary>Current planned route node IDs.</summary>
    public List<int> CurrentNodePath => _currentNodePath;

    /// <summary>Current planned route dense waypoints.</summary>
    public List<Vector3> CurrentWaypoints => _currentWaypoints;

    /// <summary>Source node ID of the current route.</summary>
    public int CurrentSourceNode => _currentSourceNode;

    /// <summary>Destination node ID of the current route.</summary>
    public int CurrentDestNode => _currentDestNode;

    // =========================================================================
    //  GIZMOS  (editor only)
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw reaction ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f,
                       transform.forward * reactionRayDistance);

        // Draw current waypoint target
        if (_currentWaypoints != null && _waypointIndex < _currentWaypoints.Count)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_currentWaypoints[_waypointIndex], 0.4f);
            Gizmos.DrawLine(transform.position, _currentWaypoints[_waypointIndex]);
        }
    }
#endif
}