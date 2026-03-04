// ============================================================================
//  CENTRALIZED CAR CONTROLLER  v3.2  —  PLAYER ROUTE VISUALIZATION ONLY
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
//  v3.2 — SMART REROUTE WITH TWO MODES  (enum RerouteMode)
//  ─────────────────────────────────────────────────────────────────────────
//  Route is always built ONCE to FixedDestNodeID.
//  Each refresh tick:
//    1. TrimPassedWaypoints() — moves the start pointer forward. Zero flicker.
//    2. Deviation check using the chosen RerouteMode.
//    3. If deviated → rebuild to the SAME fixed destination (no line flip).
//
//  RerouteMode.AdaptiveThreshold
//    Distance from player to nearest remaining waypoint vs a threshold that
//    scales with local node spacing:
//      sparse nodes (40 m gap) → threshold up to 20 m → no false reroutes
//      dense nodes  ( 4 m gap) → threshold down to 5 m → catches wrong turns
//    No NavMesh calls at runtime. Pure waypoint math.
//
//  RerouteMode.NavMeshComparison
//    NavMesh.CalculatePath(currentPos → fixedDest) each tick.
//    Compares fresh path length vs remaining original path length.
//      fresh ≈ remaining          → on route → trim only
//      fresh > remaining × 1.3   → wrong way  → reroute
//    Geometry-aware. Works regardless of node density.
//    U-turns, dead ends, wrong turns all caught immediately.
//
//  HISTORY:
//    v3.0 — removed all Rigidbody writes
//    v3.1 — added autoUpdateRoute lock + FixedDestNodeID
//    v3.2 — TrimPassedWaypoints + AdaptiveThreshold + NavMeshComparison
// ============================================================================

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CentralizedCarController : MonoBehaviour
{
    // =========================================================================
    //  ENUM
    // =========================================================================

    public enum RerouteMode
    {
        AdaptiveThreshold,   // Pre-baked waypoint spacing. No NavMesh calls.
        NavMeshComparison    // NavMesh.CalculatePath each tick. Geometry-aware.
    }

    // =========================================================================
    //  INSPECTOR
    // =========================================================================

    [Header("═══  NAVIGATION SYSTEM  ═══")]
    [Tooltip("Reference to CentralizedNavigationSystem. Auto-found if null.")]
    public CentralizedNavigationSystem navSystem;

    [Header("═══  FIXED DESTINATION  ═══")]
    [Tooltip("Drag a NavNode from the scene here.\n" +
             "The route will always drive toward this node.\n" +
             "Leave empty to use random destination (original behaviour).")]
    public NavNode fixedDestinationNode = null;

    // Convenience getter — resolves the node ID from the dragged reference.
    private int FixedDestNodeID =>
        fixedDestinationNode != null ? fixedDestinationNode.nodeID : -1;

    [Header("═══  ROUTE VISUALIZATION  ═══")]
    [Tooltip("Draw the player car's planned route as a line in the scene.")]
    public bool showRouteVisualization = true;

    [Tooltip("How often (seconds) the trim + deviation check runs.")]
    [Range(0.1f, 3f)]
    public float routeRefreshInterval = 0.5f;

    [Header("═══  REROUTE MODE  ═══")]
    [Tooltip("AdaptiveThreshold — uses local waypoint spacing. No NavMesh calls at runtime.\n" +
             "NavMeshComparison — uses NavMesh.CalculatePath. Geometry-aware, handles any node density.")]
    public RerouteMode rerouteMode = RerouteMode.NavMeshComparison;

    [Header("  AdaptiveThreshold Settings")]
    [Tooltip("Threshold = local waypoint spacing × this. 0.5 = half the cell size.")]
    [Range(0.2f, 1f)]
    public float adaptiveThresholdMultiplier = 0.5f;

    [Tooltip("Minimum allowed threshold in metres (protects against micro-nodes).")]
    [Range(3f, 15f)]
    public float minDeviationThreshold = 5f;

    [Tooltip("Maximum allowed threshold in metres (protects against massive sparse gaps).")]
    [Range(15f, 60f)]
    public float maxDeviationThreshold = 30f;

    [Header("  NavMeshComparison Settings")]
    [Tooltip("Reroute when fresh NavMesh path length > remaining length × this.\n" +
             "1.3 = 30% longer = player went wrong way.")]
    [Range(1.1f, 2f)]
    public float navMeshLengthRatio = 1.3f;

    [Tooltip("NavMesh area mask for path calculation.")]
    public int navMeshAreaMask = NavMesh.AllAreas;

    [Header("═══  DEBUG (read-only)  ═══")]
    [SerializeField] private float  dbgDistToPath       = 0f;
    [SerializeField] private float  dbgAdaptiveThresh   = 0f;
    [SerializeField] private float  dbgRemainingLen     = 0f;
    [SerializeField] private float  dbgNavMeshLen       = 0f;
    [SerializeField] private bool   dbgRerouteTriggered = false;
    [SerializeField] private int    dbgWaypointIndex    = 0;
    [SerializeField] private int    dbgTotalWaypoints   = 0;

    [Header("═══  FOLLOW PATH  (Test / Debug Only)  ═══")]
    [Tooltip("FOR TESTING ONLY — cannot fight VehicleController.")]
    public bool  followPath               = false;
    [Range(1f, 30f)] public float testFollowSpeed           = 10f;
    [Range(1f, 10f)] public float testTurnSpeed             = 4f;
    [Range(1f, 10f)] public float testWaypointReachDistance = 4f;

    [Header("═══  BRAKE REACTION  (Research / Scoring)  ═══")]
    public LayerMask obstacleDetectionLayer;
    [Range(5f, 60f)] public float reactionRayDistance = 20f;

    // =========================================================================
    //  PRIVATE STATE
    // =========================================================================

    private List<Vector3> _currentWaypoints  = new List<Vector3>();
    private List<int>     _currentNodePath   = new List<int>();
    private int           _waypointIndex     = 0;
    private int           _currentSourceNode = -1;
    private int           _currentDestNode   = -1;
    private float         _routeRefreshTimer = 0f;
    private bool          _hasBuiltInitialRoute = false;

    private int   _lastClosestNode       = -1;
    private float _closestNodeCacheTimer = 0f;
    private const float CLOSEST_NODE_CACHE_HZ = 0.25f;

    private bool  _obstacleLastFrame     = false;
    private float _obstacleFirstSeenTime = -1f;
    private bool  _reactionFired         = false;
    private float _lastReactionTime      = -1f;
    private float _reactionTimeSum       = 0f;
    private int   _reactionTimeCount     = 0;
    private float _worstReactionTime     = -1f;

    private Rigidbody _rb;   // read-only, never written

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    private void Start()
    {
        if (navSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
            navSystem = Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif
            if (navSystem == null)
            {
                Debug.LogError($"[CarController] {name}: No CentralizedNavigationSystem found.");
                enabled = false;
                return;
            }
        }

        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (navSystem == null) return;

        _routeRefreshTimer     += Time.deltaTime;
        _closestNodeCacheTimer += Time.deltaTime;

        if (!_hasBuiltInitialRoute)
        {
            _routeRefreshTimer = 0f;
            BuildInitialRoute();
        }
        else if (_routeRefreshTimer >= routeRefreshInterval)
        {
            _routeRefreshTimer = 0f;
            SmartRefresh();
        }

        if (showRouteVisualization && _currentNodePath.Count > 0)
            navSystem.VisualizePlayerPath(_currentNodePath, transform.position);
        else if (!showRouteVisualization)
            navSystem.ClearPathVisualization();

        if (followPath && _currentWaypoints.Count > 0)
            TestFollowPath();

        MeasureBrakeReaction();

        dbgWaypointIndex  = _waypointIndex;
        dbgTotalWaypoints = _currentWaypoints.Count;
    }

    private void OnDisable()
    {
        if (navSystem != null) navSystem.ClearPathVisualization();
    }

    // =========================================================================
    //  BUILD INITIAL ROUTE
    // =========================================================================

    private void BuildInitialRoute()
    {
        if (!navSystem.RouteCacheReady) return;

        int closestNode = GetClosestNodeCached();
        if (closestNode == -1) return;

        var result = navSystem.RequestRoute(closestNode);

        if (!result.success)
        {
            Debug.LogWarning($"[CarController] Initial route failed: {result.failReason}");
            return;
        }

        ApplyRouteResult(result, FixedDestNodeID);
        _hasBuiltInitialRoute = true;

        Debug.Log($"[CarController] Initial route: {_currentSourceNode}→{_currentDestNode}" +
                  $" | {_currentWaypoints.Count} wps | mode={rerouteMode}");
    }

    // =========================================================================
    //  SMART REFRESH
    //  1. Trim passed waypoints  (no rebuild, no flicker)
    //  2. Deviation check        (mode-dependent)
    //  3. Rebuild if off-route   (same fixed destination)
    // =========================================================================

    private void SmartRefresh()
    {
        if (_currentWaypoints.Count == 0) return;

        dbgRerouteTriggered = false;

        TrimPassedWaypoints();

        bool offRoute = rerouteMode == RerouteMode.AdaptiveThreshold
            ? CheckDeviationAdaptive()
            : CheckDeviationNavMesh();

        if (offRoute)
        {
            dbgRerouteTriggered = true;
            RebuildRoute();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TRIM PASSED WAYPOINTS
    //  Advances _waypointIndex past any point that is now behind the car.
    //  Pure index math — waypoint list never changes, zero flicker.
    // ─────────────────────────────────────────────────────────────────────────
    private void TrimPassedWaypoints()
    {
        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        while (_waypointIndex < _currentWaypoints.Count - 1)
        {
            Vector3 toWp = _currentWaypoints[_waypointIndex] - transform.position;
            toWp.y = 0f;
            if (Vector3.Dot(fwd, toWp) < 0f)
                _waypointIndex++;
            else
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MODE A — ADAPTIVE THRESHOLD
    //  Distance to nearest remaining waypoint vs local-spacing-scaled threshold.
    //  sparse (40 m) → threshold 20 m  |  dense (4 m) → threshold 5 m
    // ─────────────────────────────────────────────────────────────────────────
    private bool CheckDeviationAdaptive()
    {
        if (_currentWaypoints.Count < 2) return false;

        int   closestIdx  = _waypointIndex;
        float closestDist = float.MaxValue;

        for (int i = _waypointIndex; i < _currentWaypoints.Count; i++)
        {
            float d = Vector3.Distance(transform.position, _currentWaypoints[i]);
            if (d < closestDist) { closestDist = d; closestIdx = i; }
        }

        int   nextIdx      = Mathf.Min(closestIdx + 1, _currentWaypoints.Count - 1);
        float localSpacing = Vector3.Distance(
            _currentWaypoints[closestIdx], _currentWaypoints[nextIdx]);

        float threshold = Mathf.Clamp(
            localSpacing * adaptiveThresholdMultiplier,
            minDeviationThreshold, maxDeviationThreshold);

        dbgDistToPath     = closestDist;
        dbgAdaptiveThresh = threshold;

        return closestDist > threshold;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MODE B — NAVMESH PATH COMPARISON
    //  Fresh NavMesh path length vs remaining original path length.
    //  fresh > remaining × ratio  →  player went off-route  →  reroute.
    // ─────────────────────────────────────────────────────────────────────────
    private bool CheckDeviationNavMesh()
    {
        if (FixedDestNodeID < 0)                                       return false;
        if (!navSystem.nodeMap.ContainsKey(FixedDestNodeID))           return false;

        float remainingLen = GetRemainingPathLength();
        dbgRemainingLen    = remainingLen;
        if (remainingLen < 2f) return false;   // nearly at dest — don't reroute

        Vector3 destPos = navSystem.nodeMap[FixedDestNodeID].worldPosition;

        var  nmPath = new NavMeshPath();
        bool ok     = NavMesh.CalculatePath(
            transform.position, destPos, navMeshAreaMask, nmPath);

        if (!ok || nmPath.status == NavMeshPathStatus.PathInvalid) return false;

        float newLen  = GetNavMeshPathLength(nmPath);
        dbgNavMeshLen = newLen;

        return newLen > remainingLen * navMeshLengthRatio;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  REBUILD ROUTE  (same fixed destination, fresh source from current pos)
    // ─────────────────────────────────────────────────────────────────────────
    private void RebuildRoute()
    {
        int destNode = FixedDestNodeID >= 0 ? FixedDestNodeID : _currentDestNode;
        if (destNode < 0) return;

        if (_currentSourceNode != -1 && _currentDestNode != -1)
            navSystem.ReleaseRoute(_currentSourceNode, _currentDestNode);

        int closestNode = GetClosestNodeCached();
        if (closestNode == -1) return;

        var result = navSystem.RequestRoute(closestNode);
        if (!result.success)
        {
            Debug.LogWarning($"[CarController] Reroute failed: {result.failReason}");
            return;
        }

        ApplyRouteResult(result, destNode);
        Debug.Log($"[CarController] Rerouted: {_currentSourceNode}→{_currentDestNode} " +
                  $"| {_currentWaypoints.Count} wps | mode={rerouteMode}");
    }

    // fixedDest: when >= 0, override the node path to this destination so the
    // line renderer always shows the route to the intended fixed target node,
    // even though RequestRoute() only accepts a single source parameter.
    private void ApplyRouteResult(CentralizedNavigationSystem.RouteResult result,
                                  int fixedDest = -1)
    {
        _currentSourceNode = result.sourceNodeID;
        _currentWaypoints  = result.waypoints ?? new List<Vector3>();
        _waypointIndex     = 0;

        // If a fixed destination is set, use FindPath(source → fixedDest) for the
        // line renderer node path so the visualization always aims at the right node.
        int destForPath    = (fixedDest >= 0) ? fixedDest : result.destinationNodeID;
        _currentDestNode   = destForPath;

        navSystem.InvalidatePlayerPathCache();
        _currentNodePath   = navSystem.FindPath(result.sourceNodeID, destForPath)
                             ?? new List<int>();
    }

    // =========================================================================
    //  PUBLIC — FORCE REFRESH
    // =========================================================================

    public void ForceRouteRefresh()
    {
        _hasBuiltInitialRoute = false;
        _currentWaypoints.Clear();
        _currentNodePath.Clear();
        _waypointIndex     = 0;
        _currentSourceNode = -1;
        _currentDestNode   = -1;
        navSystem.InvalidatePlayerPathCache();
        BuildInitialRoute();
    }

    // =========================================================================
    //  UTILITY
    // =========================================================================

    private float GetRemainingPathLength()
    {
        float len = 0f;
        for (int i = _waypointIndex; i < _currentWaypoints.Count - 1; i++)
            len += Vector3.Distance(_currentWaypoints[i], _currentWaypoints[i + 1]);
        return len;
    }

    private static float GetNavMeshPathLength(NavMeshPath path)
    {
        float len = 0f;
        for (int i = 0; i < path.corners.Length - 1; i++)
            len += Vector3.Distance(path.corners[i], path.corners[i + 1]);
        return len;
    }

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
    //  TEST FOLLOW PATH  (Transform only — no Rigidbody writes)
    // =========================================================================

    private void TestFollowPath()
    {
        if (_waypointIndex >= _currentWaypoints.Count)
        {
            _currentNodePath.Clear();
            _hasBuiltInitialRoute = false;
            navSystem.InvalidatePlayerPathCache();
            return;
        }

        Vector3 toWpXZ = new Vector3(
            _currentWaypoints[_waypointIndex].x - transform.position.x, 0f,
            _currentWaypoints[_waypointIndex].z - transform.position.z);

        if (toWpXZ.magnitude < testWaypointReachDistance) { _waypointIndex++; return; }

        if (toWpXZ.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toWpXZ.normalized), testTurnSpeed * Time.deltaTime);

        transform.position += transform.forward * testFollowSpeed * Time.deltaTime;
    }

    // =========================================================================
    //  BRAKE REACTION  (read-only — never writes to Rigidbody)
    // =========================================================================

    private void MeasureBrakeReaction()
    {
        Vector3 origin = transform.position + Vector3.up * 0.5f;
        bool    hitNow = Physics.Raycast(origin, transform.forward,
                                         reactionRayDistance, obstacleDetectionLayer);

        if      ( hitNow && !_obstacleLastFrame) { _obstacleFirstSeenTime = Time.time; _reactionFired = false; }
        else if (!hitNow &&  _obstacleLastFrame) { _obstacleFirstSeenTime = -1f;       _reactionFired = false; }

        if (hitNow && !_reactionFired && _obstacleFirstSeenTime > 0f && _rb != null)
        {
            float speed = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
            if (speed < 0.5f)
            {
                _lastReactionTime  = Time.time - _obstacleFirstSeenTime;
                _reactionFired     = true;
                _reactionTimeSum  += _lastReactionTime;
                _reactionTimeCount++;
                if (_lastReactionTime > _worstReactionTime) _worstReactionTime = _lastReactionTime;

                Debug.Log($"[CarController] Reaction: {_lastReactionTime * 1000f:F0} ms " +
                          $"| Avg: {GetAverageReactionTime() * 1000f:F0} ms " +
                          $"| Worst: {_worstReactionTime * 1000f:F0} ms");
            }
        }

        _obstacleLastFrame = hitNow;
    }

    // =========================================================================
    //  PUBLIC API
    // =========================================================================

    public float GetAverageReactionTime()
        => _reactionTimeCount > 0 ? _reactionTimeSum / _reactionTimeCount : -1f;

    public float GetWorstReactionTime()
        => _reactionTimeCount > 0 ? _worstReactionTime : -1f;

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

    public float         LastBrakeReactionTime => _lastReactionTime;
    public List<int>     CurrentNodePath       => _currentNodePath;
    public List<Vector3> CurrentWaypoints      => _currentWaypoints;
    public int           CurrentSourceNode     => _currentSourceNode;
    public int           CurrentDestNode       => _currentDestNode;
    public bool          HasRoute              => _hasBuiltInitialRoute && _currentNodePath.Count > 0;

    // =========================================================================
    //  GIZMOS
    // =========================================================================

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f,
                       transform.forward * reactionRayDistance);

        if (_currentWaypoints != null && _waypointIndex < _currentWaypoints.Count)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_currentWaypoints[_waypointIndex], 0.4f);
            Gizmos.DrawLine(transform.position, _currentWaypoints[_waypointIndex]);
        }

        if (!Application.isPlaying) return;

        string modeInfo = rerouteMode == RerouteMode.AdaptiveThreshold
            ? $"Adaptive  dist={dbgDistToPath:F1}m  thresh={dbgAdaptiveThresh:F1}m"
            : $"NavMesh   remaining={dbgRemainingLen:F1}m  fresh={dbgNavMeshLen:F1}m";

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 3.5f,
            $"WP {dbgWaypointIndex}/{dbgTotalWaypoints}  dest={( fixedDestinationNode != null ? fixedDestinationNode.name : _currentDestNode.ToString() )}\n" +
            modeInfo + (dbgRerouteTriggered ? "  ← REROUTED" : ""),
            new GUIStyle
            {
                normal    = new GUIStyleState
                {
                    textColor = dbgRerouteTriggered
                        ? new Color(1f, 0.4f, 0.1f)
                        : new Color(0.3f, 1f, 0.3f)
                },
                fontSize  = 10,
                fontStyle = FontStyle.Bold
            });

        // Draw adaptive threshold circle in scene
        if (rerouteMode == RerouteMode.AdaptiveThreshold && dbgAdaptiveThresh > 0f)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.2f);
            int seg = 24;
            float step = 360f / seg;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * step * Mathf.Deg2Rad, a1 = (i + 1) * step * Mathf.Deg2Rad;
                Gizmos.DrawLine(
                    transform.position + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * dbgAdaptiveThresh,
                    transform.position + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * dbgAdaptiveThresh);
            }
        }
    }
#endif
}