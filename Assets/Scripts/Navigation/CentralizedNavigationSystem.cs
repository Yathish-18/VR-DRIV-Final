// ============================================================================
//  CENTRALIZED NAVIGATION SYSTEM
//  • A* macro pathfinding over node graph
//  • NavMesh segment baking → dense surface-following Vector3 waypoints
//  • Per-segment route cache built at startup (spread over frames)
//  • Single inspector source-of-truth for ALL shared NPC settings
//  • Vehicles spawn only after cache is fully ready
// ============================================================================

using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CentralizedNavigationSystem : MonoBehaviour
{
    // =========================================================================
    //  GRAPH DATA
    // =========================================================================

    [Header("═══════════════  GRAPH DATA  ═══════════════")]
    public List<NavNode>             nodes               = new List<NavNode>();
    public List<ConnectionDefinition> connectionDefinitions = new List<ConnectionDefinition>();

    [HideInInspector]
    public Dictionary<int, NavNode>  nodeMap             = new Dictionary<int, NavNode>();

    public GameObject nodesParent;

    // =========================================================================
    //  HYBRID NAVMESH ROUTE CACHE
    // =========================================================================

    [Header("═══════════════  HYBRID ROUTE CACHE  ═══════════════")]

    [Tooltip("Use NavMesh to bake dense surface-following waypoints between each pair of " +
             "connected nodes. Requires a baked NavMesh on your road geometry.")]
    public bool useNavMeshHybrid = true;

    [Tooltip("Max distance between consecutive waypoints in the baked path. " +
             "Smaller = smoother but more memory. 5-10 m is a good range.")]
    [Range(2f, 20f)]
    public float maxWaypointSpacing = 6f;

    [Tooltip("Height added above every waypoint position so cars ride on top of the surface.")]
    [Range(0f, 2f)]
    public float waypointHeightOffset = 0.15f;

    [Tooltip("Layer(s) to raycast against when snapping interpolated waypoints to the road " +
             "surface (used when NavMesh is unavailable for a segment).")]
    public LayerMask waypointSnapLayer = ~0;

    [Tooltip("How many segments to bake per frame during startup. Higher = faster bake " +
             "but may cause a brief frame hitch. 3-8 is recommended.")]
    [Range(1, 20)]
    public int segmentsBakedPerFrame = 5;

    [Tooltip("How many ready routes to pre-compute per source node during startup. More = more variety, higher startup cost. 5-10 is a good range.")]
    [Range(1, 30)]
    public int routesPerSourceNode = 8;

    [Tooltip("How many nodes to process per frame during route pre-computation Phase 2. 2-5 recommended.")]
    [Range(1, 20)]
    public int routesBakedPerFrame = 3;

    /// <summary>
    /// Segment-level cache: (fromNodeID, toNodeID) → dense surface waypoints.
    /// Built once at startup; read-only during gameplay.
    /// </summary>
    private Dictionary<(int, int), Vector3[]> _segmentCache
        = new Dictionary<(int, int), Vector3[]>();

    /// <summary>
    /// True once PreBakeAllSegments coroutine has finished.
    /// Vehicles are spawned only after this flag is set.
    /// </summary>
    public bool RouteCacheReady { get; private set; } = false;

    // =========================================================================
    //  VEHICLE PREFABS & SPAWN SETTINGS
    // =========================================================================

    [Header("═══════════════  SPAWN SETTINGS  ═══════════════")]
    [SerializeField] private GameObject       npcVehiclePrefab;
    [SerializeField] private List<GameObject> npcVariants         = new List<GameObject>();
    [SerializeField] private int              totalTrafficVehicles = 15;
    [SerializeField] private bool             spawnOnStart         = true;

    [Tooltip("Minimum world-space gap between two spawned vehicles (metres).")]
    [SerializeField] private float vehicleSpacing = 15f;

    [Tooltip("Layer(s) considered road/ground for spawn-position raycasts.")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Tooltip("Extra Y added after the car is grounded at spawn. Usually 0.")]
    [SerializeField] private float spawnHeightOffset = 0f;

    // =========================================================================
    //  ★ CENTRALISED NPC CONFIGURATION
    //    Every field here is pushed into TrafficVehicle.Initialize().
    //    You never need to touch individual vehicle prefabs.
    // =========================================================================

    [Header("═══════════════  NPC — MOVEMENT  ═══════════════")]

    [Tooltip("Base forward speed in m/s. Each vehicle varies ±15 % randomly.")]
    [SerializeField] public float vehicleSpeed          = 12f;

    [Tooltip("How sharply vehicles turn toward the next waypoint (higher = snappier).")]
    [SerializeField] public float vehicleTurnSpeed      = 3f;

    [Tooltip("Speed smoothing time. Smaller = more aggressive acceleration/braking.")]
    [SerializeField] public float vehicleSpeedSmoothTime = 0.3f;

    // ── Path constraints ──────────────────────────────────────────────────────
    [Header("═══════════════  NPC — PATH CONSTRAINTS  ═══════════════")]

    [Tooltip("Minimum number of node-hops in a generated route. Prevents trivially short trips.")]
    [SerializeField] public int   minPathLength           = 5;

    [Tooltip("Maximum number of node-hops allowed. Guards against excessively long routes.")]
    [SerializeField] public int   maxPathLength           = 30;

    [Tooltip("Nearest a destination node may be to the source (world-space metres).")]
    [SerializeField] public float minDestinationDistance  = 50f;

    [Tooltip("Furthest a destination node may be from the source (world-space metres).")]
    [SerializeField] public float maxDestinationDistance  = 300f;

    [Tooltip("How many random destinations to try before falling back.")]
    [SerializeField] public int   maxPathAttempts         = 5;

    // ── Waypoint reach thresholds ─────────────────────────────────────────────
    [Header("═══════════════  NPC — WAYPOINT REACH  ═══════════════")]

    [Tooltip("XZ (horizontal) distance at which a waypoint is considered reached (metres).")]
    [SerializeField] public float waypointReachDistanceXZ = 4f;

    [Tooltip("Maximum vertical difference allowed when evaluating waypoint reach on slopes.")]
    [SerializeField] public float waypointReachDistanceY  = 12f;

    [Tooltip("Minimum seconds between consecutive waypoint advances (cascade guard).")]
    [SerializeField] public float minAdvanceInterval      = 0.15f;

    // ── Obstacle & traffic detection ──────────────────────────────────────────
    [Header("═══════════════  NPC — DETECTION  ═══════════════")]

    [Tooltip("Combined layer mask for ALL forward detection:\n" +
             "• NPC vehicles (TrafficVehicle layer)\n" +
             "• Player vehicle layer\n" +
             "• Traffic lights layer\n" +
             "• Static obstacles (walls, barriers)\n" +
             "Do NOT include Road or Terrain.")]
    [SerializeField] public LayerMask detectionLayerMask;

    [Tooltip("Layer that contains NPC traffic vehicles. Used to identify hits as NPCs.")]
    [SerializeField] public LayerMask npcVehicleLayer;

    [Tooltip("Layer that contains the player vehicle. Used to identify hits as player.")]
    [SerializeField] public LayerMask playerVehicleLayer;

    [Tooltip("Layer that contains traffic light colliders. Used to identify hits as lights.")]
    [SerializeField] public LayerMask trafficLightLayer;

    [Tooltip("How far ahead the single detection ray is cast (metres).")]
    [SerializeField] public float detectionRange          = 20f;

    [Tooltip("Distance at which a detected NPC/player vehicle causes a full stop.")]
    [SerializeField] public float vehicleStoppingDistance = 10f;

    [Tooltip("Distance at which a static obstacle causes a full stop.")]
    [SerializeField] public float obstacleStoppingDistance = 6f;

    [Tooltip("Distance at which the car stops behind a red traffic light.")]
    [SerializeField] public float trafficLightStopDistance = 7f;

    [Tooltip("Timeout (seconds) before ignoring a red light (stuck guard).")]
    [SerializeField] public float maxRedLightWaitTime      = 20f;

    // ── Ground / slope settings ───────────────────────────────────────────────
    [Header("═══════════════  NPC — GROUND & SLOPE  ═══════════════")]

    [Tooltip("Surface type your roads sit on. Drives the ground layer sent to each vehicle.")]
    [SerializeField] private GroundSurfaceType groundSurfaceType = GroundSurfaceType.Road;

    [Tooltip("Enable to override the auto-resolved ground layer with a manual mask.")]
    [SerializeField] private bool      overrideGroundLayer  = false;

    [Tooltip("Manual ground layer mask (only used when Override Ground Layer is ticked).")]
    [SerializeField] private LayerMask groundLayerOverride  = 0;

    [Tooltip("How far above the vehicle pivot the ground-snap ray starts.")]
    [SerializeField] public float groundRayUpOffset    = 3f;

    [Tooltip("Maximum downward distance for the ground-snap ray.")]
    [SerializeField] public float groundRayDistance    = 15f;

    [Tooltip("Target ride height above the road surface in metres.")]
    [SerializeField] public float vehicleRideHeight    = 0.5f;

    [Tooltip("Strength of the per-frame Y correction toward ride height. 8-12 is typical.")]
    [SerializeField] public float vehicleGroundSnapStrength = 8f;

    [Tooltip("How fast the car tilts to match slope normals.")]
    [SerializeField] public float vehicleSlopeTiltSpeed     = 5f;

    [Tooltip("Speed multiplier when the next waypoint is above the vehicle (uphill).")]
    [SerializeField] public float vehicleHillClimbBoost     = 1.4f;

    // ── Stuck detection ───────────────────────────────────────────────────────
    [Header("═══════════════  NPC — STUCK DETECTION  ═══════════════")]

    [Tooltip("Frames the vehicle must remain nearly stationary before 'stuck' recovery fires.")]
    [SerializeField] public int   maxStuckFrames           = 300;

    [Tooltip("Minimum XZ movement per frame below which the stuck counter increments (metres).")]
    [SerializeField] public float stuckMovementThreshold   = 0.25f;

    [Tooltip("How many path recalculations are allowed before the vehicle teleports to the " +
             "nearest reachable node.")]
    [SerializeField] public int   maxPathRecalculations    = 3;

    // ── Route pool ────────────────────────────────────────────────────────────
    [Header("═══════════════  NPC — ROUTE POOL  ═══════════════")]

    // ── Debug ─────────────────────────────────────────────────────────────────
    [Header("═══════════════  DEBUG  ═══════════════")]
    [SerializeField] public bool showDebugGizmos = true;

    // =========================================================================
    //  EDITOR UTILITIES (NODE CREATION / SNAPPING)
    // =========================================================================

    [Header("═══════════════  NODE TOOLS  ═══════════════")]
    public float autoConnectMaxDistance      = 20f;
    public float newNodeDistance             = 15f;
    public LayerMask snapLayer               = ~0;
    public float snapRaycastOriginHeight     = 50f;
    public float snapNodeHeightOffset        = 0.05f;
    public bool  snapAlignToSurface          = false;
    public bool  autoSnapNewNodes            = false;

    // =========================================================================
    //  PATH VISUALIZATION
    // =========================================================================

    [Header("═══════════════  PATH VISUALIZATION  ═══════════════")]
    public LineRenderer pathLineRenderer;
    public bool         showPathsInEditor             = true;
    public bool         visualizeAllConnectionsEditor = false;
    public float        pathLineHeightOffset          = 0.3f;

    // =========================================================================
    //  LEGACY (kept for compatibility)
    // =========================================================================

    [Header("═══════════════  LEGACY  ═══════════════")]
    [Tooltip("Legacy chain system — not used with destination-based traffic.")]
    public List<TrafficWaypointChain> trafficChains = new List<TrafficWaypointChain>();

    // =========================================================================
    //  PRIVATE STATE
    // =========================================================================

    private List<TrafficVehicle> activeVehicles = new List<TrafficVehicle>();
    private int nextNodeID = 0;

    // ── Route Pool ────────────────────────────────────────────────────────────
    // Pre-computed routes: sourceNodeID → list of ready RouteResults.
    // Built entirely at startup. RequestRoute() is a pure pool lookup — zero A* at runtime.
    private Dictionary<int, List<RouteResult>> _routePool
        = new Dictionary<int, List<RouteResult>>();

    // ── Route Occupancy ───────────────────────────────────────────────────────
    // How many active NPCs are currently on each (src→dest) pair.
    // Capped at MAX_NPCS_PER_ROUTE so the same route isn't given to every NPC.
    // NPC calls ReleaseRoute() when it finishes or switches route.
    private Dictionary<(int src, int dst), int> _routeOccupancy
        = new Dictionary<(int, int), int>();

    private const int MAX_NPCS_PER_ROUTE = 2;

    // =========================================================================
    //  STRUCTS / ENUMS
    // =========================================================================

    public enum GroundSurfaceType
    {
        Default        = 0,
        Road           = 1,
        Terrain        = 2,
        RoadAndTerrain = 3,
        Custom         = 4,
    }

    /// <summary>
    /// All shared ground/slope parameters passed to each TrafficVehicle at spawn.
    /// </summary>
    [System.Serializable]
    public struct VehicleGroundConfig
    {
        public LayerMask groundLayer;
        public float     groundRayUpOffset;
        public float     groundRayDistance;
        public float     rideHeight;
        public float     groundSnapStrength;
        public float     slopeTiltSpeed;
        public float     hillClimbBoost;
    }

    /// <summary>
    /// All shared NPC settings collected into one struct and pushed to each vehicle.
    /// </summary>
    [System.Serializable]
    public struct VehicleSharedConfig
    {
        // Movement
        public float speed;
        public float turnSpeed;
        public float speedSmoothTime;

        // Path constraints
        public int   minPathLength;
        public int   maxPathLength;
        public float minDestinationDistance;
        public float maxDestinationDistance;
        public int   maxPathAttempts;

        // Waypoint reach
        public float waypointReachDistanceXZ;
        public float waypointReachDistanceY;
        public float minAdvanceInterval;

        // Detection (unified single-ray system)
        public LayerMask detectionLayerMask;
        public LayerMask npcVehicleLayer;
        public LayerMask playerVehicleLayer;
        public LayerMask trafficLightLayer;
        public float     detectionRange;
        public float     vehicleStoppingDistance;
        public float     obstacleStoppingDistance;
        public float     trafficLightStopDistance;
        public float     maxRedLightWaitTime;

        // Stuck detection
        public int   maxStuckFrames;
        public float stuckMovementThreshold;
        public int   maxPathRecalculations;

        // Debug
        public bool showDebugGizmos;
    }

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    private void Awake()
    {
        ValidateAndRebuildGraph();
    }

    private void Start()
    {
        ValidateAndRebuildGraph();
        SetupLineRenderer();

        if (spawnOnStart && Application.isPlaying)
        {
            if (nodes.Count < 2)
            {
                Debug.LogError("[NavSystem] Need at least 2 nodes for traffic!");
                return;
            }

            if (useNavMeshHybrid)
                StartCoroutine(PreBakeAllSegmentsThenSpawn());
            else
            {
                RouteCacheReady = true;
                StartCoroutine(SpawnAfterDelay(0.5f));
            }
        }
    }

    private IEnumerator SpawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnTrafficVehicles();
    }

    // =========================================================================
    //  HYBRID: PRE-BAKE ALL SEGMENTS
    // =========================================================================

    private IEnumerator PreBakeAllSegmentsThenSpawn()
    {
        _segmentCache.Clear();
        _routePool.Clear();
        RouteCacheReady = false;

        // ── Phase 1: Bake NavMesh segments ───────────────────────────────────
        var segments = new List<(int from, int to)>();
        foreach (var conn in connectionDefinitions)
        {
            if (!nodeMap.ContainsKey(conn.fromNodeID) || !nodeMap.ContainsKey(conn.toNodeID)) continue;
            segments.Add((conn.fromNodeID, conn.toNodeID));
            if (conn.bidirectional) segments.Add((conn.toNodeID, conn.fromNodeID));
        }

        int segBaked = 0, segFailed = 0;
        Debug.Log($"[NavSystem] ── Phase 1/2: Baking {segments.Count} NavMesh segments...");

        for (int i = 0; i < segments.Count; i++)
        {
            var (from, to) = segments[i];
            if (BakeAndCacheSegment(from, to)) segBaked++; else segFailed++;
            if ((i + 1) % segmentsBakedPerFrame == 0) yield return null;
        }
        Debug.Log($"[NavSystem] Phase 1 done — {segBaked} NavMesh, {segFailed} fallback.");

        // ── Phase 2: Pre-compute full A* routes for every source node ─────────
        // NPCs call RequestRoute() → O(1) pool lookup. Zero A* at gameplay runtime.
        var allNodeIDs  = nodeMap.Keys.ToList();
        int routesBaked = 0, processed = 0;
        Debug.Log($"[NavSystem] ── Phase 2/2: Pre-computing routes " +
                  $"({routesPerSourceNode} per node, {allNodeIDs.Count} nodes)...");

        foreach (int srcID in allNodeIDs)
        {
            _routePool[srcID] = new List<RouteResult>();
            Vector3 srcPos    = nodeMap[srcID].worldPosition;

            // Candidates in ideal distance range, shuffled for variety
            var candidates = allNodeIDs
                .Where(id => id != srcID)
                .Select(id => (id, d: Vector3.Distance(srcPos, nodeMap[id].worldPosition)))
                .Where(t => t.d >= minDestinationDistance && t.d <= maxDestinationDistance)
                .Select(t => t.id)
                .ToList();

            // Relax min if nothing in range
            if (candidates.Count == 0)
                candidates = allNodeIDs
                    .Where(id => id != srcID)
                    .Select(id => (id, d: Vector3.Distance(srcPos, nodeMap[id].worldPosition)))
                    .Where(t => t.d <= maxDestinationDistance)
                    .Select(t => t.id)
                    .ToList();

            Shuffle(candidates);

            foreach (int destID in candidates)
            {
                if (_routePool[srcID].Count >= routesPerSourceNode) break;

                List<int> nodePath = FindPath(srcID, destID);
                if (nodePath == null || nodePath.Count < minPathLength
                                     || nodePath.Count > maxPathLength) continue;

                List<Vector3> waypoints = GetDenseRoute(nodePath);
                if (waypoints == null || waypoints.Count < 2) continue;

                _routePool[srcID].Add(new RouteResult
                {
                    success           = true,
                    sourceNodeID      = srcID,
                    destinationNodeID = destID,
                    waypoints         = waypoints,
                });
                routesBaked++;
            }

            // Hard fallback: if still empty, take ANY reachable neighbor
            if (_routePool[srcID].Count == 0)
            {
                int fb = FindAnyReachableNode(srcID);
                if (fb != -1)
                {
                    List<int> fp = FindPath(srcID, fb);
                    if (fp != null && fp.Count >= 2)
                    {
                        List<Vector3> fw = GetDenseRoute(fp);
                        if (fw != null && fw.Count >= 2)
                        {
                            _routePool[srcID].Add(new RouteResult
                            {
                                success = true, sourceNodeID = srcID,
                                destinationNodeID = fb, waypoints = fw,
                            });
                            routesBaked++;
                        }
                    }
                }
            }

            processed++;
            if (processed % routesBakedPerFrame == 0) yield return null;
        }

        int emptyNodes = _routePool.Values.Count(p => p.Count == 0);
        RouteCacheReady = true;

        Debug.Log($"[NavSystem] ✅ ALL READY — " +
                  $"{routesBaked} routes pre-computed across {allNodeIDs.Count} nodes. " +
                  $"Segments: {segBaked} NavMesh + {segFailed} fallback. " +
                  $"Nodes with no route: {emptyNodes}");

        if (emptyNodes > 0)
            Debug.LogWarning($"[NavSystem] ⚠️ {emptyNodes} nodes unreachable. " +
                             "Check node graph connectivity.");

        yield return new WaitForSeconds(0.3f);
        SpawnTrafficVehicles();
    }

    /// <summary>
    /// Bakes one directed segment into the cache.
    /// Returns true if NavMesh succeeded, false if linear fallback was used.
    /// </summary>
    private bool BakeAndCacheSegment(int fromID, int toID)
    {
        Vector3 fromPos = nodeMap[fromID].transform.position;
        Vector3 toPos   = nodeMap[toID].transform.position;

        // Try NavMesh first
        NavMeshPath nmPath = new NavMeshPath();
        bool navMeshOk = NavMesh.CalculatePath(fromPos, toPos, NavMesh.AllAreas, nmPath)
                      && nmPath.status == NavMeshPathStatus.PathComplete
                      && nmPath.corners.Length >= 2;

        Vector3[] waypoints;
        if (navMeshOk)
        {
            waypoints = SubdivideAndLiftCorners(nmPath.corners);
        }
        else
        {
            // Fallback: linearly interpolated + ground-snapped waypoints
            waypoints = BuildLinearFallbackSegment(fromPos, toPos);
            if (nmPath.status != NavMeshPathStatus.PathComplete)
                Debug.LogWarning($"[NavSystem] NavMesh failed {fromID}→{toID} " +
                                 $"(status={nmPath.status}). Using linear fallback.");
        }

        _segmentCache[(fromID, toID)] = waypoints;
        return navMeshOk;
    }

    /// <summary>
    /// Takes raw NavMesh corners, subdivides any gap larger than maxWaypointSpacing,
    /// and lifts every point by waypointHeightOffset.
    /// </summary>
    private Vector3[] SubdivideAndLiftCorners(Vector3[] corners)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 a = corners[i];
            Vector3 b = corners[i + 1];
            pts.Add(Lift(a));

            float segLen  = Vector3.Distance(a, b);
            int   divs    = Mathf.FloorToInt(segLen / maxWaypointSpacing);
            for (int s = 1; s < divs; s++)
            {
                float t     = (float)s / divs;
                Vector3 mid = Vector3.Lerp(a, b, t);
                pts.Add(Lift(mid));
            }
        }
        pts.Add(Lift(corners[corners.Length - 1]));
        return pts.ToArray();
    }

    /// <summary>
    /// Builds a ground-snapped linear path for segments where NavMesh is unavailable.
    /// </summary>
    private Vector3[] BuildLinearFallbackSegment(Vector3 from, Vector3 to)
    {
        var pts  = new List<Vector3>();
        float dist = Vector3.Distance(from, to);
        int   divs = Mathf.Max(2, Mathf.CeilToInt(dist / maxWaypointSpacing));

        for (int i = 0; i <= divs; i++)
        {
            float   t   = (float)i / divs;
            Vector3 pos = Vector3.Lerp(from, to, t);
            pos = SnapToWaypointSurface(pos);
            pts.Add(pos);
        }
        return pts.ToArray();
    }

    private Vector3 SnapToWaypointSurface(Vector3 pos)
    {
        Vector3 origin = pos + Vector3.up * 10f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, waypointSnapLayer))
            return hit.point + Vector3.up * waypointHeightOffset;
        return pos + Vector3.up * waypointHeightOffset;
    }

    private Vector3 Lift(Vector3 v) => v + Vector3.up * waypointHeightOffset;

    // =========================================================================
    //  ROUTE RESULT & REQUEST API
    // =========================================================================

    public class RouteResult
    {
        public bool          success;
        public int           sourceNodeID;
        public int           destinationNodeID;
        public List<Vector3> waypoints;
        public string        failReason;
    }

    // =========================================================================
    //  PUBLIC: ROUTE REQUEST API
    //
    //  RequestRoute(fromNodeID)
    //      → Pulls from pre-computed pool (zero A* at runtime)
    //      → Respects MAX_NPCS_PER_ROUTE occupancy per route pair
    //      → Falls back to live compute only if pool is empty/exhausted
    //
    //  RequestReroute(fromNodeID, toNodeID)
    //      → Used by stuck NPCs to re-anchor and resume toward same dest
    //
    //  ReleaseRoute(srcNodeID, dstNodeID)
    //      → NPC MUST call this when it finishes or abandons a route
    //        so the occupancy slot is freed for other NPCs
    // =========================================================================

    /// <summary>
    /// Primary route request. Returns a pre-baked route from the pool instantly.
    /// No A* runs at runtime unless the pool for this node is exhausted.
    /// </summary>
    public RouteResult RequestRoute(int fromNodeID)
    {
        // Validate source node
        if (!nodeMap.ContainsKey(fromNodeID))
        {
            fromNodeID = GetClosestNode(Vector3.zero);
            if (fromNodeID == -1) return Fail("No nodes in map.");
        }

        // ── Step 1: Try pool (pre-baked routes, randomised order) ────────────
        if (_routePool.TryGetValue(fromNodeID, out var poolEntries) && poolEntries.Count > 0)
        {
            // Shuffle pool order so NPCs don't all pick the same first entry
            var shuffled = poolEntries.ToList();
            Shuffle(shuffled);

            foreach (var candidate in shuffled)
            {
                var key = (candidate.sourceNodeID, candidate.destinationNodeID);

                // Skip if this route already has too many NPCs on it
                _routeOccupancy.TryGetValue(key, out int occupants);
                if (occupants >= MAX_NPCS_PER_ROUTE) continue;

                // Claim the slot
                _routeOccupancy[key] = occupants + 1;

                // Return a fresh copy so each NPC owns its own waypoint list
                return new RouteResult
                {
                    success           = true,
                    sourceNodeID      = candidate.sourceNodeID,
                    destinationNodeID = candidate.destinationNodeID,
                    waypoints         = new List<Vector3>(candidate.waypoints),
                };
            }

            // All pool routes are fully occupied — relax to 1 extra NPC per route
            // rather than leaving the NPC stranded
            foreach (var candidate in shuffled)
            {
                var key = (candidate.sourceNodeID, candidate.destinationNodeID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;

                Debug.LogWarning($"[NavSystem] All routes from node {fromNodeID} at capacity. " +
                                 $"Allowing extra NPC on {candidate.sourceNodeID}→{candidate.destinationNodeID}.");

                return new RouteResult
                {
                    success           = true,
                    sourceNodeID      = candidate.sourceNodeID,
                    destinationNodeID = candidate.destinationNodeID,
                    waypoints         = new List<Vector3>(candidate.waypoints),
                };
            }
        }

        // ── Step 2: Pool miss — compute live (startup may have missed this node) ─
        Debug.LogWarning($"[NavSystem] Pool miss for node {fromNodeID}. Computing live route.");
        return ComputeRouteLive(fromNodeID);
    }

    /// <summary>
    /// Reroute a stuck NPC toward its existing destination from a new anchor node.
    /// Tries pool first, then live compute, then full new route.
    /// </summary>
    public RouteResult RequestReroute(int fromNodeID, int toNodeID)
    {
        if (!nodeMap.ContainsKey(fromNodeID)) return RequestRoute(fromNodeID);
        if (!nodeMap.ContainsKey(toNodeID))   return RequestRoute(fromNodeID);

        // Try pool for this specific (from→to) pair
        if (_routePool.TryGetValue(fromNodeID, out var entries))
        {
            var match = entries.FirstOrDefault(r => r.destinationNodeID == toNodeID);
            if (match != null)
            {
                var key = (fromNodeID, toNodeID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;
                return new RouteResult
                {
                    success           = true,
                    sourceNodeID      = match.sourceNodeID,
                    destinationNodeID = match.destinationNodeID,
                    waypoints         = new List<Vector3>(match.waypoints),
                };
            }
        }

        // Live compute for this specific pair
        List<int> nodePath = FindPath(fromNodeID, toNodeID);
        if (nodePath != null && nodePath.Count >= 2)
        {
            List<Vector3> wps = GetDenseRoute(nodePath);
            if (wps != null && wps.Count >= 2)
            {
                var key = (fromNodeID, toNodeID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;
                return new RouteResult
                {
                    success = true, sourceNodeID = fromNodeID,
                    destinationNodeID = toNodeID, waypoints = wps,
                };
            }
        }

        // Destination unreachable from new anchor — pick entirely new route
        return RequestRoute(fromNodeID);
    }

    /// <summary>
    /// NPC calls this when it finishes a route or switches to a new one.
    /// Frees the occupancy slot so another NPC can use this route.
    /// </summary>
    public void ReleaseRoute(int srcNodeID, int dstNodeID)
    {
        var key = (srcNodeID, dstNodeID);
        if (_routeOccupancy.TryGetValue(key, out int occ))
        {
            int next = occ - 1;
            if (next <= 0) _routeOccupancy.Remove(key);
            else           _routeOccupancy[key] = next;
        }
    }

    // ── Live compute (fallback only — should rarely run during gameplay) ──────

    private RouteResult ComputeRouteLive(int fromNodeID)
    {
        if (!nodeMap.ContainsKey(fromNodeID)) return Fail($"Node {fromNodeID} not in map.");

        Vector3 srcPos     = nodeMap[fromNodeID].worldPosition;
        var     candidates = nodeMap.Keys
            .Where(id => id != fromNodeID && nodeMap.ContainsKey(id))
            .OrderBy(_ => UnityEngine.Random.value)   // random order
            .ToList();

        // Try distance-filtered candidates first, then relax
        float[] minDistFallbacks = { minDestinationDistance, minDestinationDistance * 0.5f, 0f };

        foreach (float minD in minDistFallbacks)
        {
            foreach (int destID in candidates)
            {
                float d = Vector3.Distance(srcPos, nodeMap[destID].worldPosition);
                if (d < minD || d > maxDestinationDistance) continue;

                List<int> path = FindPath(fromNodeID, destID);
                if (path == null || path.Count < minPathLength || path.Count > maxPathLength) continue;

                List<Vector3> wps = GetDenseRoute(path);
                if (wps == null || wps.Count < 2) continue;

                // Cache into pool for future reuse
                if (!_routePool.ContainsKey(fromNodeID))
                    _routePool[fromNodeID] = new List<RouteResult>();

                var cached = new RouteResult
                {
                    success = true, sourceNodeID = fromNodeID,
                    destinationNodeID = destID, waypoints = wps,
                };
                _routePool[fromNodeID].Add(cached);

                var key = (fromNodeID, destID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;

                Debug.Log($"[NavSystem] Live-computed route {fromNodeID}→{destID} and cached.");

                return new RouteResult
                {
                    success = true, sourceNodeID = fromNodeID,
                    destinationNodeID = destID,
                    waypoints = new List<Vector3>(wps),
                };
            }
        }

        // Last resort: any reachable neighbor via BFS
        int fallbackDest = FindAnyReachableNode(fromNodeID);
        if (fallbackDest != -1)
        {
            List<int> fp = FindPath(fromNodeID, fallbackDest);
            if (fp != null && fp.Count >= 2)
            {
                List<Vector3> fw = GetDenseRoute(fp);
                if (fw != null && fw.Count >= 2)
                {
                    var key = (fromNodeID, fallbackDest);
                    _routeOccupancy.TryGetValue(key, out int occ);
                    _routeOccupancy[key] = occ + 1;
                    return new RouteResult
                    {
                        success = true, sourceNodeID = fromNodeID,
                        destinationNodeID = fallbackDest, waypoints = fw,
                    };
                }
            }
        }

        return Fail($"No route found from node {fromNodeID} by any method.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int FindAnyReachableNode(int fromID)
    {
        var visited = new HashSet<int> { fromID };
        var queue   = new Queue<int>();
        queue.Enqueue(fromID);
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (int nb in GetNeighbors(cur))
            {
                if (visited.Contains(nb)) continue;
                visited.Add(nb);
                if (nb != fromID) return nb;
                queue.Enqueue(nb);
            }
        }
        return -1;
    }

    private static RouteResult Fail(string reason)
    {
        Debug.LogWarning($"[NavSystem] Route failed: {reason}");
        return new RouteResult { success = false, failReason = reason };
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
    }

    // =========================================================================
    //  GET DENSE ROUTE  (node path → stitched Vector3 waypoints from cache)
    // =========================================================================

    public List<Vector3> GetDenseRoute(List<int> nodePath)
    {
        var full = new List<Vector3>();
        if (nodePath == null || nodePath.Count < 2) return full;

        for (int i = 0; i < nodePath.Count - 1; i++)
        {
            int from = nodePath[i];
            int to   = nodePath[i + 1];
            var key  = (from, to);

            if (!_segmentCache.TryGetValue(key, out Vector3[] seg))
            {
                BakeAndCacheSegment(from, to);
                seg = _segmentCache[key];
                Debug.LogWarning($"[NavSystem] Lazy-baked segment {from}→{to} at runtime.");
            }

            int startIdx = (full.Count > 0 && seg.Length > 0) ? 1 : 0;
            for (int j = startIdx; j < seg.Length; j++)
                full.Add(seg[j]);
        }

        return full;
    }

    // =========================================================================
    //  GRAPH MANAGEMENT
    // =========================================================================

    [ContextMenu("Validate And Rebuild Graph")]
    public void ValidateAndRebuildGraph()
    {
        nodes.RemoveAll(n => n == null);

        nextNodeID = 0;
        foreach (var node in nodes)
            if (node.nodeID >= nextNodeID) nextNodeID = node.nodeID + 1;

        nodeMap.Clear();
        var usedIDs = new HashSet<int>();

        foreach (var node in nodes)
        {
            if (node == null) continue;
            if (usedIDs.Contains(node.nodeID)) node.nodeID = nextNodeID++;
            usedIDs.Add(node.nodeID);
            node.parentNavSystem = this;
            nodeMap[node.nodeID] = node;
        }

        ValidateConnections();
    }

    private void ValidateConnections()
    {
        connectionDefinitions = connectionDefinitions
            .Where(c => nodeMap.ContainsKey(c.fromNodeID) && nodeMap.ContainsKey(c.toNodeID))
            .ToList();
    }

    public void RefreshGraph()
    {
        ValidateAndRebuildGraph();
    }

    public void RegisterNode(NavNode node)
    {
        if (node == null) return;

        if (nodes.Contains(node))
        {
            if (!nodeMap.ContainsKey(node.nodeID)) nodeMap[node.nodeID] = node;
            node.parentNavSystem = this;
            return;
        }

        if (node.nodeID < 0 || nodeMap.ContainsKey(node.nodeID))
            node.nodeID = nextNodeID++;
        else if (node.nodeID >= nextNodeID)
            nextNodeID = node.nodeID + 1;

        node.parentNavSystem = this;
        nodes.Add(node);
        nodeMap[node.nodeID] = node;

#if UNITY_EDITOR
        UpdateEditorConnectionsVisualization();
#endif
    }

    public void AddConnectionDefinition(int fromID, int toID, bool bidirectional)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID)) return;
        AddConnection(fromID, toID, bidirectional);
        ValidateConnections();
#if UNITY_EDITOR
        UpdateEditorConnectionsVisualization();
#endif
    }

    public void AddConnection(int fromID, int toID, bool bidirectional)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID)) return;

        bool exists = connectionDefinitions.Any(c =>
            (c.fromNodeID == fromID && c.toNodeID == toID) ||
            (bidirectional && c.fromNodeID == toID && c.toNodeID == fromID));

        if (!exists)
            connectionDefinitions.Add(new ConnectionDefinition(fromID, toID, bidirectional));
    }

    public void RemoveConnection(int fromID, int toID)
    {
        connectionDefinitions.RemoveAll(c =>
            (c.fromNodeID == fromID && c.toNodeID == toID) ||
            (c.fromNodeID == toID   && c.toNodeID == fromID));
    }

    // =========================================================================
    //  NODE CREATION
    // =========================================================================

    public NavNode CreateNode(Vector3 position, int id = -1, Quaternion? rotation = null)
    {
        if (nodesParent == null)
        {
            nodesParent = new GameObject("NavigationNodes");
            nodesParent.transform.SetParent(transform);
        }

        int finalID = (id == -1 || nodeMap.ContainsKey(id)) ? nextNodeID++ : id;
        if (id >= nextNodeID) nextNodeID = id + 1;

        GameObject nodeObj = new GameObject($"NavNode_{finalID}");
        nodeObj.transform.SetParent(nodesParent.transform);
        nodeObj.transform.position = position;
        nodeObj.transform.rotation = rotation ?? Quaternion.identity;

        NavNode node = nodeObj.AddComponent<NavNode>();
        node.parentNavSystem = this;
        node.nodeID          = finalID;
        nodes.Add(node);
        nodeMap[finalID] = node;

#if UNITY_EDITOR
        if (autoSnapNewNodes && !Application.isPlaying)
            SnapNodeToGround(node);
#endif
        return node;
    }

    // =========================================================================
    //  NODE QUERIES
    // =========================================================================

    public int GetClosestNode(Vector3 worldPosition)
    {
        float best = float.MaxValue;
        int   id   = -1;
        foreach (var kvp in nodeMap)
        {
            if (kvp.Value == null) continue;
            float d = Vector3.Distance(worldPosition, kvp.Value.worldPosition);
            if (d < best) { best = d; id = kvp.Key; }
        }
        return id;
    }

    public int GetRandomNode()
    {
        if (nodeMap.Count == 0) return -1;
        var keys = nodeMap.Keys.ToList();
        return keys[UnityEngine.Random.Range(0, keys.Count)];
    }

    public int GetRandomNode(HashSet<int> exclude)
    {
        if (nodeMap.Count == 0) return -1;
        var available = nodeMap.Keys.Where(k => !exclude.Contains(k)).ToList();
        return available.Count > 0
            ? available[UnityEngine.Random.Range(0, available.Count)]
            : GetRandomNode();
    }

    public int GetDistantNode(int fromNodeID, float minDistance = 25f)
    {
        if (!nodeMap.ContainsKey(fromNodeID)) return -1;
        var candidates = nodeMap.Keys
            .Where(id => id != fromNodeID && nodeMap.ContainsKey(id))
            .Where(id => Vector3.Distance(nodeMap[fromNodeID].worldPosition,
                                          nodeMap[id].worldPosition) >= minDistance)
            .ToList();
        return candidates.Count > 0
            ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
            : fromNodeID;
    }

    // =========================================================================
    //  A* PATHFINDING
    // =========================================================================

    public List<int> FindPath(int start, int target)
    {
        if (!nodeMap.ContainsKey(start) || !nodeMap.ContainsKey(target))
        {
            Debug.LogWarning($"[NavSystem] FindPath: node {start} or {target} not in map.");
            return new List<int>();
        }

        if (start == target) return new List<int> { start };

        var cameFrom = new Dictionary<int, int>();
        var gScore   = new Dictionary<int, float> { { start, 0f } };
        var fScore   = new Dictionary<int, float> { { start, Heuristic(start, target) } };
        var openSet  = new PriorityQueue<int>();
        var closed   = new HashSet<int>();

        openSet.Enqueue(start, fScore[start]);

        while (openSet.Count > 0)
        {
            int current = openSet.Dequeue();
            if (current == target) return ReconstructPath(cameFrom, current);
            closed.Add(current);

            foreach (int nb in GetNeighbors(current))
            {
                if (closed.Contains(nb) || !nodeMap.ContainsKey(nb)) continue;
                float tg = gScore[current] + EdgeCost(current, nb);
                if (!gScore.ContainsKey(nb) || tg < gScore[nb])
                {
                    cameFrom[nb] = current;
                    gScore[nb]   = tg;
                    fScore[nb]   = tg + Heuristic(nb, target);
                    if (!openSet.Contains(nb)) openSet.Enqueue(nb, fScore[nb]);
                }
            }
        }

        Debug.LogWarning($"[NavSystem] No path found: {start} → {target}");
        return new List<int>();
    }

    private float Heuristic(int a, int b)
    {
        if (!nodeMap.ContainsKey(a) || !nodeMap.ContainsKey(b)) return 999999f;
        Vector3 pa = nodeMap[a].worldPosition;
        Vector3 pb = nodeMap[b].worldPosition;
        return Vector3.Distance(new Vector3(pa.x, 0, pa.z), new Vector3(pb.x, 0, pb.z));
    }

    private float EdgeCost(int a, int b)
    {
        if (!nodeMap.ContainsKey(a) || !nodeMap.ContainsKey(b)) return 1f;
        return Vector3.Distance(nodeMap[a].worldPosition, nodeMap[b].worldPosition);
    }

    public List<int> GetNeighbors(int nodeID)
    {
        var result = new List<int>();
        foreach (var c in connectionDefinitions)
        {
            if (c.fromNodeID == nodeID && nodeMap.ContainsKey(c.toNodeID))
                result.Add(c.toNodeID);
            else if (c.bidirectional && c.toNodeID == nodeID && nodeMap.ContainsKey(c.fromNodeID))
                result.Add(c.fromNodeID);
        }
        return result.Distinct().ToList();
    }

    private List<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
    {
        var path = new List<int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    // =========================================================================
    //  VEHICLE SPAWNING
    // =========================================================================

    private void SpawnTrafficVehicles()
    {
        ClearAllTraffic();

        if (nodes.Count < 2)
        {
            Debug.LogError("[Traffic] Need at least 2 nodes!");
            return;
        }

        var prefabs = new List<GameObject>();
        if (npcVehiclePrefab != null) prefabs.Add(npcVehiclePrefab);
        prefabs.AddRange(npcVariants.Where(v => v != null));

        if (prefabs.Count == 0) { Debug.LogError("[Traffic] No vehicle prefabs!"); return; }

        // Build shared configs once — same for every vehicle spawned this session
        VehicleGroundConfig  groundCfg = BuildGroundConfig();
        VehicleSharedConfig  sharedCfg = BuildSharedConfig();

        // Shuffle available node IDs for varied spawn positions
        var nodeIDs = nodeMap.Keys.ToList();
        for (int i = 0; i < nodeIDs.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, nodeIDs.Count);
            (nodeIDs[i], nodeIDs[r]) = (nodeIDs[r], nodeIDs[i]);
        }

        var usedPositions = new List<Vector3>();
        int spawned = 0;

        Debug.Log("[Traffic] ══════════ SPAWNING TRAFFIC ══════════");

        foreach (int nodeID in nodeIDs)
        {
            if (spawned >= totalTrafficVehicles) break;
            if (!nodeMap.ContainsKey(nodeID)) continue;

            Vector3 nodePos = nodeMap[nodeID].transform.position;

            bool tooClose = usedPositions.Any(p => Vector3.Distance(nodePos, p) < vehicleSpacing);
            if (tooClose) continue;

            GameObject prefab    = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            Quaternion spawnRot  = nodeMap[nodeID].transform.rotation;

            // Instantiate off-screen to read live collider bounds
            var vehicleObj = Instantiate(prefab, new Vector3(nodePos.x, -5000f, nodePos.z), spawnRot);
            vehicleObj.name = $"Traffic_{spawned:D3}";

            float bottomOffset  = GetLiveBottomOffset(vehicleObj);
            Vector3 spawnPos    = new Vector3(nodePos.x,
                                              nodePos.y + bottomOffset + spawnHeightOffset,
                                              nodePos.z);

            // Configure Rigidbody
            Rigidbody rb = vehicleObj.GetComponent<Rigidbody>() ?? vehicleObj.AddComponent<Rigidbody>();
            rb.mass           = 1200f;
            rb.linearDamping  = 1f;
            rb.angularDamping = 10f;
            rb.interpolation  = RigidbodyInterpolation.Interpolate;
            rb.constraints    = RigidbodyConstraints.None;
            rb.isKinematic    = true;

            vehicleObj.transform.position = spawnPos;
            rb.position = spawnPos;
            rb.rotation = spawnRot;

            TrafficVehicle tv = vehicleObj.GetComponent<TrafficVehicle>()
                             ?? vehicleObj.AddComponent<TrafficVehicle>();

            tv.Initialize(this, nodeID, groundCfg, sharedCfg);

            StartCoroutine(ReleaseKinematicNextFrame(rb));

            activeVehicles.Add(tv);
            usedPositions.Add(spawnPos);
            spawned++;

            Debug.Log($"[Traffic] Spawned {vehicleObj.name} at Node {nodeID} | " +
                      $"Y={spawnPos.y:F2} (bottomOffset={bottomOffset:F2})");
        }

        Debug.Log($"[Traffic] ══════════ {spawned} VEHICLES SPAWNED ══════════");
    }

    private float GetLiveBottomOffset(GameObject obj)
    {
        float lowest = 0f;
        bool  found  = false;
        foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue;
            float lb = col.bounds.min.y - obj.transform.position.y;
            if (!found || lb < lowest) { lowest = lb; found = true; }
        }
        return found ? Mathf.Abs(lowest) : 0f;
    }

    private IEnumerator ReleaseKinematicNextFrame(Rigidbody rb)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (rb == null) yield break;
        rb.isKinematic    = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // =========================================================================
    //  CONFIG BUILDERS
    // =========================================================================

    private VehicleGroundConfig BuildGroundConfig()
    {
        LayerMask resolved;
        if (overrideGroundLayer)
        {
            resolved = groundLayerOverride;
        }
        else
        {
            switch (groundSurfaceType)
            {
                case GroundSurfaceType.Road:
                    int rl = LayerMask.NameToLayer("Road");
                    resolved = rl >= 0 ? (1 << rl) : LayerMask.GetMask("Default");
                    if (rl < 0) Debug.LogWarning("[NavSystem] No 'Road' layer found, falling back to Default.");
                    break;
                case GroundSurfaceType.Terrain:
                    resolved = LayerMask.GetMask("Terrain");
                    break;
                case GroundSurfaceType.RoadAndTerrain:
                    int roadL   = LayerMask.NameToLayer("Road");
                    LayerMask t = LayerMask.GetMask("Terrain");
                    resolved    = roadL >= 0 ? ((1 << roadL) | t) : t;
                    break;
                case GroundSurfaceType.Custom:
                    resolved = groundLayerOverride;
                    break;
                default:
                    resolved = LayerMask.GetMask("Default");
                    break;
            }
        }

        return new VehicleGroundConfig
        {
            groundLayer        = resolved,
            groundRayUpOffset  = groundRayUpOffset,
            groundRayDistance  = groundRayDistance,
            rideHeight         = vehicleRideHeight,
            groundSnapStrength = vehicleGroundSnapStrength,
            slopeTiltSpeed     = vehicleSlopeTiltSpeed,
            hillClimbBoost     = vehicleHillClimbBoost,
        };
    }

    private VehicleSharedConfig BuildSharedConfig()
    {
        return new VehicleSharedConfig
        {
            speed                      = vehicleSpeed,
            turnSpeed                  = vehicleTurnSpeed,
            speedSmoothTime            = vehicleSpeedSmoothTime,

            minPathLength              = minPathLength,
            maxPathLength              = maxPathLength,
            minDestinationDistance     = minDestinationDistance,
            maxDestinationDistance     = maxDestinationDistance,
            maxPathAttempts            = maxPathAttempts,

            waypointReachDistanceXZ    = waypointReachDistanceXZ,
            waypointReachDistanceY     = waypointReachDistanceY,
            minAdvanceInterval         = minAdvanceInterval,

            detectionLayerMask         = detectionLayerMask,
            npcVehicleLayer            = npcVehicleLayer,
            playerVehicleLayer         = playerVehicleLayer,
            trafficLightLayer          = trafficLightLayer,
            detectionRange             = detectionRange,
            vehicleStoppingDistance    = vehicleStoppingDistance,
            obstacleStoppingDistance   = obstacleStoppingDistance,
            trafficLightStopDistance   = trafficLightStopDistance,
            maxRedLightWaitTime        = maxRedLightWaitTime,

            maxStuckFrames              = maxStuckFrames,
            stuckMovementThreshold      = stuckMovementThreshold,
            maxPathRecalculations       = maxPathRecalculations,

            showDebugGizmos             = showDebugGizmos,
        };
    }

    // =========================================================================
    //  TRAFFIC MANAGEMENT
    // =========================================================================

    [ContextMenu("Spawn Traffic Now")]
    public void SpawnTrafficNow() => SpawnTrafficVehicles();

    [ContextMenu("Clear All Traffic")]
    public void ClearAllTraffic()
    {
        foreach (var v in activeVehicles)
            if (v != null) Destroy(v.gameObject);
        activeVehicles.Clear();
        Debug.Log("[Traffic] All traffic cleared.");
    }

    [ContextMenu("Respawn Traffic")]
    public void RespawnTraffic() { ClearAllTraffic(); SpawnTrafficVehicles(); }

    // =========================================================================
    //  PATH VISUALIZATION
    // =========================================================================

    public void ClearPathVisualization()
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled       = false;
            pathLineRenderer.positionCount = 0;
        }
        _playerCachedNodePath   = null;
        _playerCachedDenseRoute = null;
    }

    private void SetupLineRenderer()
    {
        if (pathLineRenderer != null) return;

        var lrObj = new GameObject("PathVisualizer");
        lrObj.transform.SetParent(transform);
        pathLineRenderer = lrObj.AddComponent<LineRenderer>();
        pathLineRenderer.material   = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.startWidth = 0.2f;
        pathLineRenderer.endWidth   = 0.2f;

        var grad = new Gradient();
        grad.colorKeys = new[]
        {
            new GradientColorKey(Color.yellow, 0f),
            new GradientColorKey(Color.red,    1f)
        };
        pathLineRenderer.colorGradient = grad;
        pathLineRenderer.enabled       = false;
    }

    public void VisualizePath(List<int> path)
    {
        if (path == null || path.Count == 0) return;
        SetupLineRenderer();
        pathLineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            if (!nodeMap.ContainsKey(path[i])) continue;
            Vector3 p = nodeMap[path[i]].worldPosition;
            p.y += pathLineHeightOffset;
            pathLineRenderer.SetPosition(i, p);
        }
        pathLineRenderer.enabled = true;
    }

    // =========================================================================
    //  PLAYER PATH VISUALIZATION — HYBRID NAVMESH DENSE LINE
    //
    //  Uses the same pre-baked dense NavMesh waypoints as NPCs so the line
    //  follows the road surface exactly.  The line is trimmed from the player's
    //  projected position forward so it never "snaps back" when the car is
    //  between two far-apart nodes.
    // =========================================================================

    // Cached dense waypoints for the player's current route (node-path key).
    private List<int>    _playerCachedNodePath   = null;
    private List<Vector3> _playerCachedDenseRoute = null;

    /// <summary>
    /// Call once when a new route is computed, and again every frame (or every
    /// N seconds) to trim the line as the player advances.
    /// </summary>
    /// <param name="nodePath">The A* node-ID path (same list FindPath returns).</param>
    /// <param name="playerWorldPos">Current world position of the player car.</param>
    public void VisualizePlayerPath(List<int> nodePath, Vector3 playerWorldPos)
    {
        if (nodePath == null || nodePath.Count == 0)
        {
            ClearPathVisualization();
            return;
        }

        SetupLineRenderer();

        // ── Rebuild dense waypoint list only when the node path changes ──────
        bool pathChanged = (_playerCachedNodePath == null)
                        || (_playerCachedNodePath.Count != nodePath.Count);

        if (!pathChanged)
        {
            for (int i = 0; i < nodePath.Count; i++)
            {
                if (_playerCachedNodePath[i] != nodePath[i]) { pathChanged = true; break; }
            }
        }

        if (pathChanged)
        {
            // Use pre-baked NavMesh dense route if available, else fall back
            // to the normal node-position list lifted by pathLineHeightOffset.
            if (useNavMeshHybrid && RouteCacheReady)
            {
                _playerCachedDenseRoute = GetDenseRoute(nodePath);
                if (_playerCachedDenseRoute == null || _playerCachedDenseRoute.Count < 2)
                    _playerCachedDenseRoute = BuildNodePositionList(nodePath);
            }
            else
            {
                _playerCachedDenseRoute = BuildNodePositionList(nodePath);
            }

            _playerCachedNodePath = new List<int>(nodePath);
        }

        if (_playerCachedDenseRoute == null || _playerCachedDenseRoute.Count == 0)
        {
            ClearPathVisualization();
            return;
        }

        // ── Find player's projected position on the polyline ─────────────────
        // Walk every dense segment, project the player onto it (XZ only so
        // slopes don't skew the result), keep the closest hit.
        // "closestSeg" is the index of segment [closestSeg → closestSeg+1].
        // "closestT"   is how far along that segment the projection lands [0..1].

        int   closestSeg    = 0;
        float closestDistSq = float.MaxValue;
        float closestT      = 0f;

        for (int i = 0; i < _playerCachedDenseRoute.Count - 1; i++)
        {
            Vector3 a  = _playerCachedDenseRoute[i];
            Vector3 b  = _playerCachedDenseRoute[i + 1];
            Vector3 ab = b - a;
            float   len = ab.sqrMagnitude;

            float t = (len > 0.0001f)
                    ? Mathf.Clamp01(Vector3.Dot(playerWorldPos - a, ab) / len)
                    : 0f;

            // Compare XZ only — ignore height differences on slopes
            Vector3 proj   = a + ab * t;
            float   dxz    = (new Vector2(playerWorldPos.x - proj.x,
                                          playerWorldPos.z - proj.z)).sqrMagnitude;

            if (dxz < closestDistSq)
            {
                closestDistSq = dxz;
                closestSeg    = i;
                closestT      = t;
            }
        }

        // ── Build trimmed point list ──────────────────────────────────────────
        // Start from the exact projected position on the road so the line
        // origin is always glued to the car — no snapping back to node centres.
        //
        // When closestT == 1.0 the projected point equals _playerCachedDenseRoute[closestSeg+1],
        // so we advance the segment index to avoid a duplicate first point.

        int   startSeg = closestSeg;
        float startT   = closestT;

        if (startT >= 0.9999f && startSeg + 1 < _playerCachedDenseRoute.Count - 1)
        {
            // Snap to the start of the next segment
            startSeg++;
            startT = 0f;
        }

        var trimmed = new List<Vector3>();

        // Projected start (car's position snapped to road polyline)
        Vector3 sA = _playerCachedDenseRoute[startSeg];
        Vector3 sB = (startSeg + 1 < _playerCachedDenseRoute.Count)
                   ? _playerCachedDenseRoute[startSeg + 1]
                   : sA;
        Vector3 projStart = Vector3.Lerp(sA, sB, startT);
        projStart.y += pathLineHeightOffset;
        trimmed.Add(projStart);

        // All remaining waypoints after the projected segment
        for (int i = startSeg + 1; i < _playerCachedDenseRoute.Count; i++)
        {
            Vector3 p = _playerCachedDenseRoute[i];
            p.y += pathLineHeightOffset;
            trimmed.Add(p);
        }

        // Line needs at least 2 points; if we're at/past the last waypoint hide it
        if (trimmed.Count < 2)
        {
            ClearPathVisualization();
            return;
        }

        pathLineRenderer.positionCount = trimmed.Count;
        pathLineRenderer.SetPositions(trimmed.ToArray());
        pathLineRenderer.enabled = true;
    }

    /// <summary>Clears the cached player route (call when a brand-new path is assigned).</summary>
    public void InvalidatePlayerPathCache()
    {
        _playerCachedNodePath   = null;
        _playerCachedDenseRoute = null;
    }

    // Fallback: simple node-position list when NavMesh hybrid is not ready.
    private List<Vector3> BuildNodePositionList(List<int> nodePath)
    {
        var pts = new List<Vector3>();
        foreach (int id in nodePath)
        {
            if (nodeMap.ContainsKey(id))
                pts.Add(nodeMap[id].worldPosition);
        }
        return pts;
    }

    // =========================================================================
    //  GIZMOS
    // =========================================================================

    private void OnDrawGizmos()
    {
        // Nodes
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                if (node == null) continue;
                Gizmos.color = new Color(0f, 1f, 1f, 1f);
                Gizmos.DrawSphere(node.transform.position, 0.6f);
#if UNITY_EDITOR
                Handles.Label(node.transform.position + Vector3.up * 1.2f,
                              $"Node {node.nodeID}",
                              new GUIStyle
                              {
                                  normal    = new GUIStyleState { textColor = Color.white },
                                  fontSize  = 14,
                                  fontStyle = FontStyle.Bold,
                                  alignment = TextAnchor.MiddleCenter
                              });
#endif
            }
        }

        // Connections
        if (connectionDefinitions != null)
        {
            foreach (var conn in connectionDefinitions)
            {
                if (!nodeMap.ContainsKey(conn.fromNodeID) ||
                    !nodeMap.ContainsKey(conn.toNodeID)) continue;

                Vector3 s = nodeMap[conn.fromNodeID].transform.position + Vector3.up * 0.2f;
                Vector3 e = nodeMap[conn.toNodeID].transform.position   + Vector3.up * 0.2f;

                Gizmos.color = conn.bidirectional
                    ? new Color(0f, 1f, 0f, 0.8f)
                    : new Color(1f, 0.5f, 0f, 0.8f);

                Gizmos.DrawLine(s, e);

                if (!conn.bidirectional)
                {
                    Vector3 dir  = (e - s).normalized;
                    Vector3 mid  = s + dir * Vector3.Distance(s, e) * 0.5f;
                    Vector3 perp = Vector3.Cross(Vector3.up, dir) * 0.5f;
                    Gizmos.DrawLine(mid, mid - dir * 1f + perp);
                    Gizmos.DrawLine(mid, mid - dir * 1f - perp);
                }
            }
        }

        // Active vehicles
        if (showDebugGizmos && Application.isPlaying && activeVehicles != null)
        {
            Gizmos.color = Color.green;
            foreach (var v in activeVehicles)
                if (v != null) Gizmos.DrawWireSphere(v.transform.position + Vector3.up, 0.8f);
        }
    }

    // =========================================================================
    //  EDITOR UTILITIES
    // =========================================================================

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ValidateAndRebuildGraph();
            UpdateEditorConnectionsVisualization();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying && visualizeAllConnectionsEditor)
            DrawAllConnectionsIntoLineRenderer();
    }

    [ContextMenu("Collect All Nodes")]
    public void CollectAllNodes()
    {
        nodes.Clear(); nodeMap.Clear();

        NavNode[] all = nodesParent != null
            ? nodesParent.GetComponentsInChildren<NavNode>(true)
            : FindObjectsOfType<NavNode>();

        nextNodeID = 0;
        foreach (var n in all) if (n.nodeID >= nextNodeID) nextNodeID = n.nodeID + 1;

        foreach (var n in all)
        {
            if (n == null) continue;
            if (n.nodeID < 0 || nodeMap.ContainsKey(n.nodeID)) n.nodeID = nextNodeID++;
            n.parentNavSystem = this;
            nodes.Add(n);
            nodeMap[n.nodeID] = n;
        }

        ValidateConnections();
        UpdateEditorConnectionsVisualization();
    }

    public bool SnapNodeToGround(NavNode node)
    {
        if (node == null) return false;
        Vector3 origin = node.transform.position + Vector3.up * snapRaycastOriginHeight;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                            snapRaycastOriginHeight + 500f, snapLayer))
        {
            Undo.RecordObject(node.transform, "Snap Node to Ground");
            node.transform.position = hit.point + Vector3.up * snapNodeHeightOffset;
            if (snapAlignToSurface)
                node.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            EditorUtility.SetDirty(node.gameObject);
            return true;
        }

        Debug.LogWarning($"[NavSystem] Snap missed for Node {node.nodeID}. Check snapLayer.");
        return false;
    }

    [ContextMenu("Snap All Nodes To Ground")]
    public void SnapAllNodesToGround()
    {
        int ok = 0, miss = 0;
        foreach (var n in nodes) { if (SnapNodeToGround(n)) ok++; else miss++; }
        Debug.Log($"[NavSystem] Snap → {ok} snapped, {miss} missed.");
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Auto Connect Nodes")]
    public void AutoConnectNodes()
    {
        connectionDefinitions.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (nodes[j] == null) continue;
                if (Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position)
                    <= autoConnectMaxDistance)
                    AddConnection(nodes[i].nodeID, nodes[j].nodeID, true);
            }
        }
        ValidateConnections();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("Clear All Connections")]
    public void ClearAllConnections()
    {
        connectionDefinitions.Clear();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("Create Node Forward")]
    public void CreateNodeForward()
    {
        NavNode last = nodes.Count > 0 ? nodes.Last() : null;
        Vector3 pos  = last != null
            ? last.transform.position + last.transform.forward * newNodeDistance
            : transform.position;

        NavNode n = CreateNode(pos, -1, last?.transform.rotation ?? Quaternion.identity);
        if (last != null) AddConnectionDefinition(last.nodeID, n.nodeID, true);
        Selection.activeGameObject = n.gameObject;
    }

    [ContextMenu("Create Node From Selected")]
    public void CreateNextNodeFromSelected()
    {
        if (Selection.activeGameObject == null) return;
        NavNode sel = Selection.activeGameObject.GetComponent<NavNode>();
        if (sel == null || sel.parentNavSystem != this) { Debug.LogWarning("No NavNode selected."); return; }

        NavNode n = CreateNode(sel.transform.position + sel.transform.forward * newNodeDistance,
                               -1, sel.transform.rotation);
        AddConnectionDefinition(sel.nodeID, n.nodeID, true);
        Selection.activeGameObject = n.gameObject;
    }

    [ContextMenu("Setup Demo")]
    public void SetupDemo()
    {
        ClearAllConnections();
        nodes.Clear();
        nodeMap.Clear();
        nextNodeID = 0;

        if (nodesParent == null)
        {
            nodesParent = new GameObject("NavigationNodes");
            nodesParent.transform.SetParent(transform);
        }

        Vector3[] demoPositions =
        {
            new Vector3(  0, 0.5f,  0), new Vector3( 10, 0.5f,  0),
            new Vector3( 15, 0.5f, 10), new Vector3( 10, 0.5f, 20),
            new Vector3(  0, 0.5f, 20), new Vector3(-10, 0.5f, 10)
        };

        for (int i = 0; i < demoPositions.Length; i++)
            CreateNode(demoPositions[i], -1, Quaternion.identity);

        List<int> ids = nodes.Select(n => n.nodeID).ToList();
        for (int i = 0; i < ids.Count - 1; i++)
            AddConnectionDefinition(ids[i], ids[i + 1], true);
        AddConnectionDefinition(ids[ids.Count - 1], ids[0], true);

        ValidateAndRebuildGraph();
        UpdateEditorConnectionsVisualization();

        Debug.Log("[NavSystem] Demo setup complete — 6 nodes in a hexagon.");
    }

    private NavNode GetSelectedNode()
    {
        if (Selection.activeGameObject == null) return null;
        NavNode sel = Selection.activeGameObject.GetComponent<NavNode>();
        return (sel != null && sel.parentNavSystem == this) ? sel : null;
    }

    [ContextMenu("Test Path 0 To Last")]
    public void TestPathZeroToLast()
    {
        if (nodes.Count < 2) return;
        var path = FindPath(nodes[0].nodeID, nodes[nodes.Count - 1].nodeID);
        if (path.Count > 0) { Debug.Log($"Path: {string.Join(" → ", path)}"); VisualizePath(path); }
        else Debug.LogError("No path found.");
    }

    [ContextMenu("Debug Print All Nodes")]
    public void DebugPrintAllNodes()
    {
        Debug.Log($"══ Nodes ({nodes.Count}) ══");
        foreach (var n in nodes)
            if (n != null) Debug.Log($"  Node '{n.name}' ID={n.nodeID} Pos={n.worldPosition}");
    }

    [ContextMenu("Debug Print All Connections")]
    public void DebugPrintAllConnections()
    {
        Debug.Log($"══ Connections ({connectionDefinitions.Count}) ══");
        foreach (var c in connectionDefinitions)
        {
            string fn = nodeMap.ContainsKey(c.fromNodeID) ? nodeMap[c.fromNodeID].name : "MISSING";
            string tn = nodeMap.ContainsKey(c.toNodeID)   ? nodeMap[c.toNodeID].name   : "MISSING";
            Debug.Log($"  {c.fromNodeID}({fn}) {(c.bidirectional ? "↔" : "→")} {c.toNodeID}({tn})");
        }
    }

    [ContextMenu("Debug Print Route Cache")]
    public void DebugPrintRouteCache()
    {
        Debug.Log($"══ Segment Cache ({_segmentCache.Count} entries) ══");
        int totalPts = 0;
        foreach (var kvp in _segmentCache)
        {
            totalPts += kvp.Value.Length;
            Debug.Log($"  {kvp.Key.Item1}→{kvp.Key.Item2}: {kvp.Value.Length} waypoints");
        }
        Debug.Log($"  Total waypoints in cache: {totalPts} (~{totalPts * 12 / 1024} KB)");
    }

    [ContextMenu("Debug Print Route Pool")]
    public void DebugPrintRoutePool()
    {
        Debug.Log($"══ Route Pool ({_routePool.Count} source nodes) ══");
        foreach (var kvp in _routePool)
            Debug.Log($"  Node {kvp.Key}: {kvp.Value.Count} pre-baked routes");
        int emptyNodes = _routePool.Values.Count(p => p.Count == 0);
        if (emptyNodes > 0)
            Debug.LogWarning($"  ⚠️ {emptyNodes} nodes with zero routes — check connectivity.");
    }

    [ContextMenu("Debug Print Route Occupancy")]
    public void DebugPrintRouteOccupancy()
    {
        Debug.Log($"══ Route Occupancy ({_routeOccupancy.Count} active routes) ══");
        foreach (var kvp in _routeOccupancy.OrderByDescending(k => k.Value))
            Debug.Log($"  {kvp.Key.src}→{kvp.Key.dst}: {kvp.Value}/{MAX_NPCS_PER_ROUTE} NPCs");
    }

    private void UpdateEditorConnectionsVisualization()
    {
        if (Application.isPlaying || !visualizeAllConnectionsEditor) return;
        DrawAllConnectionsIntoLineRenderer();
    }

    private void DrawAllConnectionsIntoLineRenderer()
    {
        SetupLineRenderer();
        var positions = new List<Vector3>();
        foreach (var conn in connectionDefinitions)
        {
            if (!nodeMap.ContainsKey(conn.fromNodeID) || !nodeMap.ContainsKey(conn.toNodeID)) continue;
            positions.Add(nodeMap[conn.fromNodeID].transform.position + Vector3.up * 0.3f);
            positions.Add(nodeMap[conn.toNodeID].transform.position   + Vector3.up * 0.3f);
        }
        pathLineRenderer.positionCount = positions.Count;
        pathLineRenderer.SetPositions(positions.ToArray());
        pathLineRenderer.enabled = positions.Count > 0;
    }
#endif

    // =========================================================================
    //  PRIORITY QUEUE (A*)
    // =========================================================================

    public class PriorityQueue<T>
    {
        private readonly List<(T item, float priority)> _elements = new List<(T, float)>();
        public int Count => _elements.Count;

        public void Enqueue(T item, float priority)
        {
            _elements.Add((item, priority));
            int i = _elements.Count - 1;
            while (i > 0 && _elements[i - 1].priority > _elements[i].priority)
            {
                var tmp          = _elements[i - 1];
                _elements[i - 1] = _elements[i];
                _elements[i]     = tmp;
                i--;
            }
        }

        public T Dequeue()
        {
            var best = _elements[0];
            _elements.RemoveAt(0);
            return best.item;
        }

        public bool Contains(T item)
            => _elements.Any(e => EqualityComparer<T>.Default.Equals(e.item, item));
    }
}

// =============================================================================
//  TRAFFIC CHAIN (legacy — kept for compatibility)
// =============================================================================

[System.Serializable]
public class TrafficWaypointChain
{
    public string         chainName     = "Traffic_Chain";
    public List<Transform> waypoints    = new List<Transform>();
    public List<int>       nodeIDs      = new List<int>();
    public bool            loop         = false;
    [Range(0.5f, 3f)]
    public float           speedMultiplier = 1f;
}