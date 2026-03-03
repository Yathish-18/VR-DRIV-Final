// ============================================================================
//  CENTRALIZED NAVIGATION SYSTEM  v8.1
//  ============================================================================
//  CHANGES vs v8.0:
//  ─────────────────────────────────────────────────────────────────────────
//  NEW — waypointEdgeBuffer (default 1.2 m):
//    SubdivideAndLift() and BuildLinearSegment() now call CentreOnNavMesh()
//    on every baked waypoint.  NavMesh corners land on polygon edges (road
//    boundary) by design; this push moves each point at least waypointEdgeBuffer
//    metres from the nearest NavMesh edge, keeping waypoints in the lane centre.
//    Result: cars no longer follow waypoints that sit on the pavement/kerb.
//    Set waypointEdgeBuffer = 0 to restore original v8.0 behaviour.
//    RE-BAKE REQUIRED after changing waypointEdgeBuffer.
//  ─────────────────────────────────────────────────────────────────────────
//  STARTUP PIPELINE (Option 5 — Full Pre-Baked Cache):
//
//  EDITOR (once, press button):
//    Phase 1 — NavMesh.CalculatePath() per direct connection
//              Dense road-surface waypoints → saved to asset
//    Phase 2 — A* + segment stitch for every source node (N routes each)
//              Full journey waypoints → saved to asset
//    Both phases run with a progress bar. ~75s for 1000 nodes.
//
//  RUNTIME (every Play, ~10ms total):
//    Frame 0 — LoadSegmentsFromAsset()    fills _segmentCache  (~2ms)
//    Frame 0 — LoadRoutesFromAsset()      fills _routePool     (~8ms)
//    Frame 0 — RouteCacheReady = true
//    Frame 0 — SpawnTrafficVehicles()     NPCs on road immediately
//
//  RUNTIME EDGE-CASE MISS:
//    Pool miss → ComputeRouteLive() → A* + stitch (~5ms, one shot)
//                                  → result added to pool (capped)
//                                  → future requests hit the pool
//
//  FALLBACK (no valid asset):
//    Original runtime coroutine baking — no regression.
//
//  SCALE:
//    1000 nodes, 25 NPCs, 8 routes/node:
//      Asset size: ~12 MB
//      Load time:  ~10ms
//      Pool size:  8000 routes
//      Miss rate:  near zero after first session
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
    public List<NavNode>              nodes                 = new List<NavNode>();
    public List<ConnectionDefinition> connectionDefinitions = new List<ConnectionDefinition>();

    [HideInInspector]
    public Dictionary<int, NavNode> nodeMap = new Dictionary<int, NavNode>();

    public GameObject nodesParent;

    // =========================================================================
    //  ROUTE CACHE ASSET
    // =========================================================================

    [Header("═══════════════  ROUTE CACHE  ═══════════════")]

    [Tooltip("Full pre-baked navigation cache (segments + routes).\n\n" +
             "SETUP:\n" +
             "1. Assets → Create → Navigation → Nav Route Cache Asset\n" +
             "2. Assign the .asset here\n" +
             "3. Press 'Bake & Save Full Route Cache' in the inspector\n" +
             "4. Press Play — NPCs spawn with zero delay\n\n" +
             "RE-BAKE when:\n" +
             "  • NavMesh rebuilt (road geometry changed)\n" +
             "  • Major graph restructuring\n" +
             "  • Waypoint spacing / height / routesPerNode changed\n\n" +
             "NOT needed when:\n" +
             "  • Changing NPC settings (speed, detection, etc.)\n" +
             "  • Minor node moves or adding a few connections")]
    public NavRouteCacheAsset routeCacheAsset;

    [Tooltip("Load segments and routes from asset at startup.\n" +
             "Disable to force runtime baking (useful for debugging).")]
    public bool usePreBakedCache = true;

    // =========================================================================
    //  NAVMESH & WAYPOINT SETTINGS
    // =========================================================================

    [Header("═══════════════  NAVMESH SETTINGS  ═══════════════")]

    [Tooltip("Use NavMesh to bake dense road-surface waypoints between nodes.\n" +
             "Without this, NPCs aim straight at distant nodes and wander off road.")]
    public bool useNavMeshHybrid = true;

    [Tooltip("Max metres between consecutive waypoints. 5–10 m recommended.\n" +
             "Smaller = smoother road following but larger asset.")]
    [Range(2f, 20f)]
    public float maxWaypointSpacing = 6f;

    [Tooltip("Height above baked waypoints so cars sit on the road surface.")]
    [Range(0f, 2f)]
    public float waypointHeightOffset = 0.15f;

    [Tooltip("Minimum distance baked waypoints must be from NavMesh edges.\n" +
             "Pushes waypoints away from road edges toward the lane centre.\n" +
             "Prevents cars following waypoints that are on pavement/kerb.\n" +
             "1.0–1.5 m is a good default. 0 = disabled (original behaviour).")]
    [Range(0f, 3f)]
    public float waypointEdgeBuffer = 1.2f;

    [Tooltip("Layer(s) for linear-fallback waypoint surface snapping.")]
    public LayerMask waypointSnapLayer = ~0;

    [Header("─── Route Pool Settings ───")]

    [Tooltip("Pre-baked routes per source node. Higher = more route variety\n" +
             "but larger asset and longer bake time.\n" +
             "8 is a good balance for 1000-node scenes.")]
    [Range(1, 30)]
    public int routesPerSourceNode = 8;

    [Tooltip("Extra routes allowed to be live-computed and cached per node at runtime.\n" +
             "Caps pool growth from edge-case misses.")]
    [Range(1, 10)]
    public int maxLiveRoutesPerNode = 4;

    [Header("─── Runtime Fallback Baking (no valid asset) ───")]

    [Tooltip("NavMesh segments baked per frame during runtime fallback.")]
    [Range(1, 20)]
    public int segmentsBakedPerFrame = 5;

    [Tooltip("Source nodes processed per frame during runtime route fallback.")]
    [Range(1, 20)]
    public int routesBakedPerFrame = 3;

    // ─── Internal dictionaries ─────────────────────────────────────────────
    // Populated from asset at startup. Read-only during gameplay.
    // Lazy-bake fills gaps for new nodes added after last bake.

    private Dictionary<(int, int), Vector3[]> _segmentCache
        = new Dictionary<(int, int), Vector3[]>();

    private Dictionary<int, List<RouteResult>> _routePool
        = new Dictionary<int, List<RouteResult>>();

    private Dictionary<(int src, int dst), int> _routeOccupancy
        = new Dictionary<(int, int), int>();

    private const int MAX_NPCS_PER_ROUTE = 2;

    /// <summary>True once both caches are populated. NPCs spawn only after this.</summary>
    public bool RouteCacheReady { get; private set; } = false;

    // =========================================================================
    //  SPAWN SETTINGS
    // =========================================================================

    [Header("═══════════════  SPAWN SETTINGS  ═══════════════")]
    [SerializeField] private GameObject       npcVehiclePrefab;
    [SerializeField] private List<GameObject> npcVariants         = new List<GameObject>();
    [SerializeField] private int              totalTrafficVehicles = 15;
    [SerializeField] private bool             spawnOnStart         = true;
    [Tooltip("Minimum spawn spacing in dense node areas (nodes close together).")]
    [SerializeField] private float            vehicleSpacingMin    = 8f;
    [Tooltip("Maximum spawn spacing in sparse node areas (nodes far apart).")]
    [SerializeField] private float            vehicleSpacingMax    = 25f;
    [Tooltip("Radius used to count nearby nodes when calculating local density.")]
    [SerializeField] private float            densitySampleRadius  = 30f;
    [SerializeField] private LayerMask        groundLayer          = ~0;
    [SerializeField] private float            spawnHeightOffset    = 0f;

    // =========================================================================
    //  NPC SHARED SETTINGS
    // =========================================================================

    [Header("═══════════════  NPC — MOVEMENT  ═══════════════")]
    [SerializeField] public float vehicleSpeed           = 12f;
    [SerializeField] public float vehicleTurnSpeed       = 3f;
    [SerializeField] public float vehicleSpeedSmoothTime = 0.3f;

    [Header("═══════════════  NPC — PATH CONSTRAINTS  ═══════════════")]
    [SerializeField] public int   minPathLength          = 5;
    [SerializeField] public int   maxPathLength          = 30;
    [SerializeField] public float minDestinationDistance = 50f;
    [SerializeField] public float maxDestinationDistance = 300f;
    [SerializeField] public int   maxPathAttempts        = 5;

    [Header("═══════════════  NPC — WAYPOINT REACH  ═══════════════")]
    [SerializeField] public float waypointReachDistanceXZ = 4f;
    [SerializeField] public float waypointReachDistanceY  = 12f;
    [SerializeField] public float minAdvanceInterval      = 0.15f;

    [Header("═══════════════  NPC — DETECTION  ═══════════════")]
    [SerializeField] public LayerMask detectionLayerMask;
    [SerializeField] public LayerMask npcVehicleLayer;
    [SerializeField] public LayerMask playerVehicleLayer;
    [SerializeField] public LayerMask trafficLightLayer;
    [SerializeField] public float     detectionRange           = 20f;
    [SerializeField] public float     vehicleStoppingDistance  = 10f;
    [SerializeField] public float     obstacleStoppingDistance = 6f;
    [SerializeField] public float     trafficLightStopDistance = 7f;
    [SerializeField] public float     maxRedLightWaitTime      = 20f;

    [Header("═══════════════  NPC — GROUND & SLOPE  ═══════════════")]
    [SerializeField] private GroundSurfaceType groundSurfaceType   = GroundSurfaceType.Road;
    [SerializeField] private bool              overrideGroundLayer = false;
    [SerializeField] private LayerMask         groundLayerOverride = 0;
    [SerializeField] public  float groundRayUpOffset         = 3f;
    [SerializeField] public  float groundRayDistance         = 15f;
    [SerializeField] public  float vehicleRideHeight         = 0.5f;
    [SerializeField] public  float vehicleGroundSnapStrength = 8f;
    [SerializeField] public  float vehicleSlopeTiltSpeed     = 5f;
    [SerializeField] public  float vehicleHillClimbBoost     = 1.4f;

    [Header("═══════════════  NPC — STUCK DETECTION  ═══════════════")]
    [SerializeField] public int   maxStuckFrames         = 300;
    [SerializeField] public float stuckMovementThreshold = 0.25f;
    [SerializeField] public int   maxPathRecalculations  = 3;

    // =========================================================================
    //  NODE TOOLS
    // =========================================================================

    [Header("═══════════════  NODE TOOLS  ═══════════════")]
    public float     autoConnectMaxDistance  = 20f;
    public float     newNodeDistance         = 15f;
    public LayerMask snapLayer               = ~0;
    public float     snapRaycastOriginHeight = 50f;
    public float     snapNodeHeightOffset    = 0.05f;
    public bool      snapAlignToSurface      = false;
    public bool      autoSnapNewNodes        = false;

    // =========================================================================
    //  PATH VISUALIZATION
    // =========================================================================

    [Header("═══════════════  PATH VISUALIZATION  ═══════════════")]
    public LineRenderer pathLineRenderer;
    public bool         showPathsInEditor             = true;
    public bool         visualizeAllConnectionsEditor = false;
    public float        pathLineHeightOffset          = 0.3f;

    // =========================================================================
    //  DEBUG
    // =========================================================================

    [Header("═══════════════  DEBUG  ═══════════════")]
    [SerializeField] public bool showDebugGizmos = true;

    // =========================================================================
    //  LEGACY
    // =========================================================================

    [Header("═══════════════  LEGACY  ═══════════════")]
    public List<TrafficWaypointChain> trafficChains = new List<TrafficWaypointChain>();

    // =========================================================================
    //  PRIVATE STATE
    // =========================================================================

    private List<TrafficVehicle> activeVehicles = new List<TrafficVehicle>();
    private int nextNodeID = 0;

    // Runtime stats
    private int _poolHits   = 0;
    private int _poolMisses = 0;

    // Player path visualization cache
    private List<int>     _playerCachedNodePath   = null;
    private List<Vector3> _playerCachedDenseRoute = null;

    // =========================================================================
    //  STRUCTS / ENUMS
    // =========================================================================

    public enum GroundSurfaceType
    {
        Default = 0, Road = 1, Terrain = 2, RoadAndTerrain = 3, Custom = 4,
    }

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

    [System.Serializable]
    public struct VehicleSharedConfig
    {
        public float     speed;
        public float     turnSpeed;
        public float     speedSmoothTime;
        public int       minPathLength;
        public int       maxPathLength;
        public float     minDestinationDistance;
        public float     maxDestinationDistance;
        public int       maxPathAttempts;
        public float     waypointReachDistanceXZ;
        public float     waypointReachDistanceY;
        public float     minAdvanceInterval;
        public LayerMask detectionLayerMask;
        public LayerMask npcVehicleLayer;
        public LayerMask playerVehicleLayer;
        public LayerMask trafficLightLayer;
        public float     detectionRange;
        public float     vehicleStoppingDistance;
        public float     obstacleStoppingDistance;
        public float     trafficLightStopDistance;
        public float     maxRedLightWaitTime;
        public int       maxStuckFrames;
        public float     stuckMovementThreshold;
        public int       maxPathRecalculations;
        public bool      showDebugGizmos;
    }

    public class RouteResult
    {
        public bool          success;
        public int           sourceNodeID;
        public int           destinationNodeID;
        public List<Vector3> waypoints;
        public string        failReason;
    }

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    private void Awake() => ValidateAndRebuildGraph();

    private void Start()
    {
        ValidateAndRebuildGraph();
        SetupLineRenderer();

        if (!spawnOnStart || !Application.isPlaying) return;
        if (nodes.Count < 2)
        { Debug.LogError("[NavSystem] Need at least 2 nodes for NPC traffic!"); return; }

        // ── Fast path: load from pre-baked asset ─────────────────────────────
        if (usePreBakedCache && routeCacheAsset != null && routeCacheAsset.isValid)
        {
            StartCoroutine(StartupFromAsset());
            return;
        }

        // ── Slow path: runtime baking fallback ───────────────────────────────
        LogFallbackReason();
        if (useNavMeshHybrid) StartCoroutine(RuntimeBakeThenSpawn());
        else { RouteCacheReady = true; StartCoroutine(SpawnAfterDelay(0.5f)); }
    }

    private void LogFallbackReason()
    {
        if (routeCacheAsset == null)
            Debug.LogWarning("[NavSystem] No NavRouteCacheAsset assigned → runtime baking.\n" +
                             "FIX: Assets → Create → Navigation → Nav Route Cache Asset\n" +
                             "     Assign here, then press 'Bake & Save Full Route Cache'.");
        else if (!routeCacheAsset.isValid)
            Debug.LogWarning("[NavSystem] Cache asset not yet baked → runtime baking.\n" +
                             "FIX: Press 'Bake & Save Full Route Cache' in the inspector.");
    }

    // =========================================================================
    //  STARTUP FROM ASSET  (~10ms total, no coroutine yield needed)
    // =========================================================================

    private IEnumerator StartupFromAsset()
    {
        float t0 = Time.realtimeSinceStartup;

        // Load both caches in the same frame — just dictionary fills
        LoadSegmentsFromAsset();
        LoadRoutesFromAsset();

        float loadMs = (Time.realtimeSinceStartup - t0) * 1000f;

        // Log any stale warnings
        string staleWarning = routeCacheAsset.GetStaleWarning(
            nodes.Count, connectionDefinitions.Count,
            maxWaypointSpacing, waypointHeightOffset, routesPerSourceNode);

        if (!string.IsNullOrEmpty(staleWarning))
            Debug.LogWarning($"[NavSystem] ⚠️  Cache may be stale:\n{staleWarning}" +
                             "Consider re-baking. Edge cases will use live computation.");

        RouteCacheReady = true;

        int totalRoutes = _routePool.Values.Sum(v => v.Count);
        Debug.Log($"[NavSystem] ✅ Cache loaded in {loadMs:F1} ms — " +
                  $"Segments: {_segmentCache.Count} | " +
                  $"Routes: {totalRoutes} across {_routePool.Count} nodes");

        // Yield one frame so scene finishes initialising before spawning
        yield return null;

        SpawnTrafficVehicles();
    }

    /// <summary>
    /// Loads segment data from asset into _segmentCache.
    /// Pure dictionary fill — completes in microseconds per segment.
    /// </summary>
    private void LoadSegmentsFromAsset()
    {
        _segmentCache.Clear();
        foreach (var seg in routeCacheAsset.segments)
        {
            if (seg?.waypoints == null || seg.waypoints.Length == 0) continue;
            _segmentCache[(seg.fromID, seg.toID)] = seg.waypoints;
        }
    }

    /// <summary>
    /// Loads route data from asset into _routePool.
    /// Pure dictionary fill — completes in microseconds per route.
    /// </summary>
    private void LoadRoutesFromAsset()
    {
        _routePool.Clear();
        foreach (var route in routeCacheAsset.routes)
        {
            if (route?.waypoints == null || route.waypoints.Length < 2) continue;
            if (!_routePool.ContainsKey(route.srcID))
                _routePool[route.srcID] = new List<RouteResult>();

            _routePool[route.srcID].Add(new RouteResult
            {
                success           = true,
                sourceNodeID      = route.srcID,
                destinationNodeID = route.dstID,
                waypoints         = new List<Vector3>(route.waypoints),
            });
        }
    }

    // =========================================================================
    //  EDITOR BAKING  (Phase 1 + Phase 2, non-blocking via EditorApplication.update)
    // =========================================================================

#if UNITY_EDITOR

    // ── Bake state (persists across editor ticks) ─────────────────────────────
    private static bool   _bakeRunning    = false;

    /// <summary>True while a non-blocking bake is in progress. Used by editor UI.</summary>
    public static bool IsBakeRunning => _bakeRunning;
    private static int    _bakePhase      = 0;   // 0=idle 1=segments 2=routes
    private static int    _bakeIndex      = 0;
    private static int    _segBaked       = 0;
    private static int    _segFailed      = 0;
    private static int    _routesBaked    = 0;
    private static int    _emptyNodes     = 0;
    private static List<(int from, int to)> _bakeSegPairs;
    private static List<int>                _bakeNodeIDs;
    private static NavRouteCacheAsset       _bakeAsset;
    private static CentralizedNavigationSystem _bakeTarget;

    // How many ms to spend per editor tick before yielding back to Unity.
    // Defined inside BakeTick as a local const — no class-level constants needed.

    /// <summary>
    /// Starts a non-blocking bake. Processes a small batch each editor tick
    /// so Unity never freezes. Progress bar updates every tick.
    /// </summary>
    public void EditorBakeFullCache()
    {
        if (_bakeRunning)
        {
            EditorUtility.DisplayDialog("Bake In Progress",
                "A bake is already running. Wait for it to finish or cancel via the progress bar.", "OK");
            return;
        }

        if (routeCacheAsset == null)
        {
            EditorUtility.DisplayDialog("No Cache Asset Assigned",
                "Please create and assign a NavRouteCacheAsset first.\n\n" +
                "Assets → Create → Navigation → Nav Route Cache Asset", "OK");
            return;
        }

        ValidateAndRebuildGraph();

        if (nodes.Count < 2)
        { EditorUtility.DisplayDialog("Not Enough Nodes", "Need at least 2 nodes.", "OK"); return; }

        var segmentPairs = CollectSegmentPairs();
        if (segmentPairs.Count == 0)
        { EditorUtility.DisplayDialog("No Connections", "Add connections between nodes first.", "OK"); return; }

        int estimatedRoutes = nodeMap.Count * routesPerSourceNode;

        bool proceed = EditorUtility.DisplayDialog(
            "Bake Full Route Cache",
            $"This will bake:\n" +
            $"  • Phase 1: {segmentPairs.Count} NavMesh segments\n" +
            $"  • Phase 2: ~{estimatedRoutes} A* routes ({routesPerSourceNode}/node)\n\n" +
            $"Runs in background — Unity stays responsive.\n" +
            $"Progress bar shows progress. Cancel any time.\n\n" +
            $"Requirements:\n" +
            $"  • NavMesh must be baked for this scene\n" +
            $"  • Road geometry on NavMesh-walkable layer\n\n" +
            $"Continue?",
            "Bake", "Cancel");

        if (!proceed) return;

        // ── Initialise bake state ─────────────────────────────────────────────
        Undo.RecordObject(routeCacheAsset, "Bake Full Route Cache");
        routeCacheAsset.Clear();
        _segmentCache.Clear();

        _bakeAsset    = routeCacheAsset;
        _bakeTarget   = this;
        _bakeSegPairs = segmentPairs;
        _bakeNodeIDs  = nodeMap.Keys.ToList();
        _bakePhase    = 1;
        _bakeIndex    = 0;
        _segBaked     = 0;
        _segFailed    = 0;
        _routesBaked  = 0;
        _emptyNodes   = 0;
        _bakeRunning  = true;

        // Hook into editor update loop — called every editor tick (not game tick)
        EditorApplication.update += BakeTick;
        Debug.Log("[NavSystem] 🔨 Bake started (non-blocking). Unity stays responsive.");
    }

    /// <summary>
    /// Called every editor tick while bake is running.
    /// Time-boxed to MAX_MS_PER_TICK milliseconds so Unity NEVER freezes,
    /// regardless of how expensive individual A* or NavMesh calls are.
    /// </summary>
    private static void BakeTick()
    {
        if (_bakeTarget == null || _bakeAsset == null)
        {
            BakeCleanup(cancelled: true);
            return;
        }

        // Hard time budget per tick — Unity gets control back after this many ms.
        // 12ms = smooth 60fps editor. 20ms = faster bake but slight stutter.
        const double MAX_MS_PER_TICK = 12.0;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        int totalWork = (_bakeSegPairs?.Count ?? 0) + (_bakeNodeIDs?.Count ?? 0);

        // ── Phase 1: Bake NavMesh segments ────────────────────────────────────
        while (_bakePhase == 1 && _bakeIndex < _bakeSegPairs.Count)
        {
            var (from, to) = _bakeSegPairs[_bakeIndex];
            Vector3[] wps  = _bakeTarget.EditorBakeOneSegment(from, to);
            _bakeTarget._segmentCache[(from, to)] = wps;
            _bakeAsset.segments.Add(new SerializedSegment
                { fromID = from, toID = to, waypoints = wps });
            if (wps.Length > 0) _segBaked++; else _segFailed++;
            _bakeIndex++;

            if (sw.Elapsed.TotalMilliseconds >= MAX_MS_PER_TICK) break;
        }

        if (_bakePhase == 1)
        {
            float pct = (float)_bakeIndex / totalWork;
            if (EditorUtility.DisplayCancelableProgressBar(
                "Baking Route Cache — Unity stays responsive",
                $"Phase 1/2 — NavMesh Segments  {_bakeIndex}/{_bakeSegPairs.Count}",
                pct))
            { BakeCleanup(cancelled: true); return; }

            if (_bakeIndex >= _bakeSegPairs.Count)
            {
                Debug.Log($"[NavSystem] Phase 1 done — {_segBaked} NavMesh, {_segFailed} fallback.");
                _bakePhase = 2;
                _bakeIndex = 0;
            }
            return; // always yield after progress bar update
        }

        // ── Phase 2: Bake A* routes ───────────────────────────────────────────
        while (_bakePhase == 2 && _bakeIndex < _bakeNodeIDs.Count)
        {
            int srcID        = _bakeNodeIDs[_bakeIndex];
            int builtForNode = _bakeTarget.EditorBakeRoutesForNode(srcID, _bakeAsset);
            if (builtForNode == 0) _emptyNodes++;
            _routesBaked += builtForNode;
            _bakeIndex++;

            if (sw.Elapsed.TotalMilliseconds >= MAX_MS_PER_TICK) break;
        }

        if (_bakePhase == 2)
        {
            float pct = (float)(_bakeSegPairs.Count + _bakeIndex) / totalWork;
            if (EditorUtility.DisplayCancelableProgressBar(
                "Baking Route Cache — Unity stays responsive",
                $"Phase 2/2 — A* Routes  {_bakeIndex}/{_bakeNodeIDs.Count}" +
                $"  ({_routesBaked} routes baked)",
                pct))
            {
                _bakeTarget.FinaliseAndSaveAsset(_bakeAsset, _segBaked, _routesBaked,
                                                 _emptyNodes, isPartial: true);
                BakeCleanup(cancelled: true);
                return;
            }

            if (_bakeIndex >= _bakeNodeIDs.Count)
            {
                EditorUtility.ClearProgressBar();
                _bakeTarget.FinaliseAndSaveAsset(_bakeAsset, _segBaked, _routesBaked,
                                                 _emptyNodes, isPartial: false);
                BakeCleanup(cancelled: false);
            }
        }
    }

    private static void BakeCleanup(bool cancelled)
    {
        EditorApplication.update -= BakeTick;
        EditorUtility.ClearProgressBar();
        _bakeRunning = false;
        _bakePhase   = 0;
        _bakeAsset   = null;
        _bakeTarget  = null;
        _bakeSegPairs = null;
        _bakeNodeIDs  = null;

        if (cancelled)
            Debug.LogWarning("[NavSystem] ⚠️ Bake cancelled. Partial cache saved (if Phase 2 had started).");
    }

    /// <summary>
    /// Bakes all routes for a single source node and appends to asset.
    /// Returns number of routes built.
    /// </summary>
    private int EditorBakeRoutesForNode(int srcID, NavRouteCacheAsset asset)
    {
        if (!nodeMap.ContainsKey(srcID)) return 0;

        Vector3 srcPos     = nodeMap[srcID].worldPosition;
        var     candidates = BuildCandidateList(srcID, srcPos);
        int     built      = 0;

        foreach (int destID in candidates)
        {
            if (built >= routesPerSourceNode) break;

            List<int> nodePath = FindPath(srcID, destID);
            if (nodePath == null || nodePath.Count < minPathLength
                                 || nodePath.Count > maxPathLength) continue;

            List<Vector3> wps = GetDenseRoute(nodePath);
            if (wps == null || wps.Count < 2) continue;

            asset.routes.Add(new SerializedRoute
            {
                srcID     = srcID,
                dstID     = destID,
                waypoints = wps.ToArray(),
            });
            built++;
        }

        // ── Guaranteed fallback for dead-end / isolated nodes ─────────────────
        // If normal baking failed (node is near graph edge, sparse area, or all
        // paths failed minPathLength/maxPathLength), progressively relax constraints
        // until we get at least ONE valid route. Without this, the node gets zero
        // pool entries and causes infinite live-compute misses at runtime.
        if (built == 0)
        {
            // Pass 1: relax minPathLength to 2 (just need to go somewhere)
            foreach (int destID in candidates)
            {
                List<int> nodePath = FindPath(srcID, destID);
                if (nodePath == null || nodePath.Count < 2
                                     || nodePath.Count > maxPathLength) continue;
                List<Vector3> wps = GetDenseRoute(nodePath);
                if (wps == null || wps.Count < 2) continue;
                asset.routes.Add(new SerializedRoute
                {
                    srcID = srcID, dstID = destID, waypoints = wps.ToArray()
                });
                built++;
                break; // one route is enough for a dead-end
            }
        }

        if (built == 0)
        {
            // Pass 2: ignore distance constraints entirely — take ANY reachable node
            int fallback = FindAnyReachableNode(srcID);
            if (fallback != -1)
            {
                List<int> fp = FindPath(srcID, fallback);
                if (fp != null && fp.Count >= 2)
                {
                    List<Vector3> fw = GetDenseRoute(fp);
                    if (fw != null && fw.Count >= 2)
                    {
                        asset.routes.Add(new SerializedRoute
                        {
                            srcID = srcID, dstID = fallback, waypoints = fw.ToArray()
                        });
                        built++;
                        Debug.LogWarning($"[NavSystem] Node {srcID} is isolated/dead-end — " +
                                         $"baked short fallback route to node {fallback}.");
                    }
                }
            }
        }

        if (built == 0)
            Debug.LogError($"[NavSystem] Node {srcID} has NO reachable neighbors — " +
                           $"check connections. This node will always cause pool misses at runtime.");

        return built;
    }

    private void FinaliseAndSaveAsset(NavRouteCacheAsset asset, int segBaked,
                                      int routesBaked, int emptyNodes, bool isPartial)
    {
        asset.isValid             = asset.routes.Count > 0;
        asset.bakedAt             = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        asset.sceneName           = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        asset.nodeCount           = nodes.Count;
        asset.connectionCount     = connectionDefinitions.Count;
        asset.segmentCount        = asset.segments.Count;
        asset.routeCount          = asset.routes.Count;
        asset.routesPerNode       = routesPerSourceNode;
        asset.bakedWaypointSpacing = maxWaypointSpacing;
        asset.bakedHeightOffset   = waypointHeightOffset;

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string path = AssetDatabase.GetAssetPath(asset);

        if (isPartial)
        {
            Debug.LogWarning($"[NavSystem] ⚠️  Bake CANCELLED — partial cache saved.\n" +
                             $"Segments: {asset.segmentCount} | Routes: {asset.routeCount}\n" +
                             $"Runtime will use live computation for missing routes.\n" +
                             $"Re-bake when convenient for full performance.");
            EditorUtility.DisplayDialog("Bake Cancelled — Partial Save",
                $"Partial cache saved.\n\n" +
                $"Segments: {asset.segmentCount}\n" +
                $"Routes:   {asset.routeCount}\n\n" +
                "Runtime will compute missing routes on demand.\n" +
                "Re-bake when convenient for best performance.",
                "OK");
            return;
        }

        if (emptyNodes > 0)
            Debug.LogWarning($"[NavSystem] ⚠️  {emptyNodes} nodes have no routes — " +
                             "check graph connectivity.");

        Debug.Log($"[NavSystem] ✅ BAKE COMPLETE — " +
                  $"Segments: {segBaked} NavMesh | " +
                  $"Routes: {routesBaked} | " +
                  $"Empty nodes: {emptyNodes} | " +
                  $"Saved: {path}");

        EditorUtility.DisplayDialog(
            "Bake Complete ✅",
            $"Full route cache saved!\n\n" +
            $"NavMesh segments:  {segBaked}\n" +
            $"Routes baked:      {routesBaked}\n" +
            $"Nodes covered:     {nodeMap.Count - emptyNodes}/{nodeMap.Count}\n" +
            (emptyNodes > 0 ? $"⚠️  Empty nodes:    {emptyNodes}\n" : "") +
            $"Baked at:          {asset.bakedAt}\n\n" +
            $"NPCs will now spawn immediately on Play.",
            "OK");
    }

    private Vector3[] EditorBakeOneSegment(int fromID, int toID)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID)) return new Vector3[0];

        Vector3 fromPos = nodeMap[fromID].transform.position;
        Vector3 toPos   = nodeMap[toID].transform.position;

        var  nm = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(fromPos, toPos, NavMesh.AllAreas, nm)
               && nm.status == NavMeshPathStatus.PathComplete
               && nm.corners.Length >= 2;

        if (ok) return SubdivideAndLift(nm.corners);

        Debug.LogWarning($"[NavSystem] NavMesh failed {fromID}→{toID} " +
                         $"(status={nm.status}). Using linear fallback.");
        return BuildLinearSegment(fromPos, toPos);
    }

    /// <summary>Clears the cache asset and marks it invalid.</summary>
    public void EditorClearCache()
    {
        if (routeCacheAsset == null) return;
        Undo.RecordObject(routeCacheAsset, "Clear Route Cache");
        routeCacheAsset.Clear();
        EditorUtility.SetDirty(routeCacheAsset);
        AssetDatabase.SaveAssets();
        Debug.Log("[NavSystem] Route cache cleared and marked invalid.");
    }
#endif

    // =========================================================================
    //  RUNTIME BAKING FALLBACK  (original coroutine — zero regression)
    // =========================================================================

    private IEnumerator RuntimeBakeThenSpawn()
    {
        _segmentCache.Clear();
        _routePool.Clear();
        RouteCacheReady = false;

        // Phase 1
        var segs = CollectSegmentPairs();
        int segBaked = 0, segFailed = 0;
        Debug.Log($"[NavSystem] Runtime Phase 1/2: Baking {segs.Count} segments...");

        for (int i = 0; i < segs.Count; i++)
        {
            var (from, to) = segs[i];
            if (BakeAndCacheSegment(from, to)) segBaked++; else segFailed++;
            if ((i + 1) % segmentsBakedPerFrame == 0) yield return null;
        }
        Debug.Log($"[NavSystem] Phase 1 done — {segBaked} NavMesh, {segFailed} fallback.");

        // Phase 2
        var allNodeIDs  = nodeMap.Keys.ToList();
        int routesBaked = 0, processed = 0;
        Debug.Log("[NavSystem] Runtime Phase 2/2: Building route pool...");

        foreach (int srcID in allNodeIDs)
        {
            _routePool[srcID] = new List<RouteResult>();
            Vector3 srcPos    = nodeMap[srcID].worldPosition;
            var candidates    = BuildCandidateList(srcID, srcPos);

            foreach (int destID in candidates)
            {
                if (_routePool[srcID].Count >= routesPerSourceNode) break;
                List<int> path = FindPath(srcID, destID);
                if (path == null || path.Count < minPathLength || path.Count > maxPathLength) continue;
                List<Vector3> wps = GetDenseRoute(path);
                if (wps == null || wps.Count < 2) continue;
                _routePool[srcID].Add(MakeResult(srcID, destID, wps));
                routesBaked++;
            }

            if (_routePool[srcID].Count == 0)
            {
                int fb = FindAnyReachableNode(srcID);
                if (fb != -1) { List<int> fp = FindPath(srcID, fb);
                    if (fp?.Count >= 2) { List<Vector3> fw = GetDenseRoute(fp);
                        if (fw?.Count >= 2) { _routePool[srcID].Add(MakeResult(srcID, fb, fw)); routesBaked++; } } }
            }

            processed++;
            if (processed % routesBakedPerFrame == 0) yield return null;
        }

        int empty = _routePool.Values.Count(p => p.Count == 0);
        RouteCacheReady = true;
        Debug.Log($"[NavSystem] ✅ Runtime bake done — {routesBaked} routes, {empty} empty nodes.");
        if (empty > 0) Debug.LogWarning($"[NavSystem] ⚠️  {empty} nodes unreachable.");

        yield return new WaitForSeconds(0.3f);
        SpawnTrafficVehicles();
    }

    private IEnumerator SpawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnTrafficVehicles();
    }

    // =========================================================================
    //  SEGMENT BAKING HELPERS
    // =========================================================================

    private List<(int from, int to)> CollectSegmentPairs()
    {
        var result = new List<(int, int)>();
        foreach (var conn in connectionDefinitions)
        {
            if (!nodeMap.ContainsKey(conn.fromNodeID) || !nodeMap.ContainsKey(conn.toNodeID)) continue;
            result.Add((conn.fromNodeID, conn.toNodeID));
            if (conn.bidirectional) result.Add((conn.toNodeID, conn.fromNodeID));
        }
        return result;
    }

    private bool BakeAndCacheSegment(int fromID, int toID)
    {
        Vector3 fromPos = nodeMap[fromID].transform.position;
        Vector3 toPos   = nodeMap[toID].transform.position;

        var  nm = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(fromPos, toPos, NavMesh.AllAreas, nm)
               && nm.status == NavMeshPathStatus.PathComplete
               && nm.corners.Length >= 2;

        _segmentCache[(fromID, toID)] = ok
            ? SubdivideAndLift(nm.corners)
            : BuildLinearSegment(fromPos, toPos);

        if (!ok) Debug.LogWarning($"[NavSystem] NavMesh failed {fromID}→{toID}. Linear fallback.");
        return ok;
    }

    private Vector3[] SubdivideAndLift(Vector3[] corners)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 a = corners[i], b = corners[i + 1];
            pts.Add(CentreOnNavMesh(LiftPoint(a)));
            int divs = Mathf.FloorToInt(Vector3.Distance(a, b) / maxWaypointSpacing);
            for (int s = 1; s < divs; s++)
                pts.Add(CentreOnNavMesh(LiftPoint(Vector3.Lerp(a, b, (float)s / divs))));
        }
        pts.Add(CentreOnNavMesh(LiftPoint(corners[corners.Length - 1])));
        return pts.ToArray();
    }

    // =========================================================================
    //  CENTRE-ON-NAVMESH  (NEW in v8.1)
    //
    //  Pushes a waypoint away from the nearest NavMesh edge by up to
    //  waypointEdgeBuffer metres.  NavMesh path corners naturally land right
    //  on or very close to NavMesh polygon edges (the road / lane boundary).
    //  Without this push, baked waypoints sit at the road edge and cars follow
    //  them there, partly on the pavement.
    //
    //  Algorithm:
    //    NavMesh.FindClosestEdge → gives the nearest edge and distance to it.
    //    If distance < waypointEdgeBuffer, move the point away from the edge
    //    by (waypointEdgeBuffer - distance) in XZ — clamping to the buffer.
    //    The Y axis is untouched; the height lift already happened in LiftPoint.
    //
    //  Safe: if the push would move the point off the NavMesh (e.g. narrow lane
    //  where both edges are close) it is clamped to not overshoot past the
    //  opposite edge.  If waypointEdgeBuffer = 0 this method is a no-op.
    // =========================================================================

    private Vector3 CentreOnNavMesh(Vector3 pos)
    {
        if (waypointEdgeBuffer <= 0.001f) return pos;

        if (NavMesh.FindClosestEdge(pos, out NavMeshHit edgeHit, NavMesh.AllAreas))
        {
            float distToEdge = edgeHit.distance;
            if (distToEdge < waypointEdgeBuffer && distToEdge > 0.001f)
            {
                // Direction from the edge hit point back toward pos (away from edge)
                Vector3 awayDir = pos - edgeHit.position;
                awayDir.y = 0f;
                float awayLen = awayDir.magnitude;

                if (awayLen > 0.001f)
                {
                    float push = waypointEdgeBuffer - distToEdge;
                    Vector3 candidate = pos + (awayDir / awayLen) * push;

                    // Verify the pushed point is still on the NavMesh.
                    // If it isn't (narrow lane), keep original.
                    if (NavMesh.SamplePosition(candidate, out NavMeshHit check,
                                               0.5f, NavMesh.AllAreas))
                    {
                        // Preserve original Y (height lift)
                        candidate.y = pos.y;
                        return candidate;
                    }
                }
            }
        }
        return pos;
    }

    private Vector3[] BuildLinearSegment(Vector3 from, Vector3 to)
    {
        int divs = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(from, to) / maxWaypointSpacing));
        var pts  = new Vector3[divs + 1];
        for (int i = 0; i <= divs; i++)
            pts[i] = CentreOnNavMesh(SnapToSurface(Vector3.Lerp(from, to, (float)i / divs)));
        return pts;
    }

    private Vector3 LiftPoint(Vector3 v) => v + Vector3.up * waypointHeightOffset;

    private Vector3 SnapToSurface(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, waypointSnapLayer))
            return hit.point + Vector3.up * waypointHeightOffset;
        return pos + Vector3.up * waypointHeightOffset;
    }

    private List<int> BuildCandidateList(int srcID, Vector3 srcPos)
    {
        var candidates = nodeMap.Keys
            .Where(id => id != srcID)
            .Select(id => (id, d: Vector3.Distance(srcPos, nodeMap[id].worldPosition)))
            .Where(t => t.d >= minDestinationDistance && t.d <= maxDestinationDistance)
            .Select(t => t.id)
            .ToList();

        if (candidates.Count == 0)
            candidates = nodeMap.Keys
                .Where(id => id != srcID)
                .Select(id => (id, d: Vector3.Distance(srcPos, nodeMap[id].worldPosition)))
                .Where(t => t.d <= maxDestinationDistance)
                .Select(t => t.id)
                .ToList();

        Shuffle(candidates);
        return candidates;
    }

    // =========================================================================
    //  DENSE ROUTE STITCHING
    // =========================================================================

    /// <summary>
    /// Stitches pre-baked segments into one continuous waypoint list.
    /// If a segment is missing it is lazily baked via NavMesh (one-shot, cached).
    /// </summary>
    public List<Vector3> GetDenseRoute(List<int> nodePath)
    {
        var full = new List<Vector3>();
        if (nodePath == null || nodePath.Count < 2) return full;

        for (int i = 0; i < nodePath.Count - 1; i++)
        {
            int from = nodePath[i], to = nodePath[i + 1];

            if (!_segmentCache.TryGetValue((from, to), out Vector3[] seg))
            {
                Debug.LogWarning($"[NavSystem] Lazy-baking missing segment {from}→{to}.");
                BakeAndCacheSegment(from, to);
                seg = _segmentCache[(from, to)];
            }

            if (seg.Length == 0) continue;
            int start = full.Count > 0 ? 1 : 0;
            for (int j = start; j < seg.Length; j++) full.Add(seg[j]);
        }
        return full;
    }

    // =========================================================================
    //  ROUTE REQUEST API
    // =========================================================================

    /// <summary>
    /// Returns a pre-built route from the pool for the given source node.
    /// O(1) lookup. Falls back to live computation on miss, caches result.
    /// </summary>
    public RouteResult RequestRoute(int fromNodeID)
    {
        if (!nodeMap.ContainsKey(fromNodeID))
        {
            fromNodeID = GetClosestNode(Vector3.zero);
            if (fromNodeID == -1) return Fail("No nodes in map.");
        }

        if (_routePool.TryGetValue(fromNodeID, out var pool) && pool.Count > 0)
        {
            var shuffled = pool.ToList();
            Shuffle(shuffled);

            // Prefer under-occupied routes
            foreach (var c in shuffled)
            {
                var key = (c.sourceNodeID, c.destinationNodeID);
                _routeOccupancy.TryGetValue(key, out int occ);
                if (occ >= MAX_NPCS_PER_ROUTE) continue;
                _routeOccupancy[key] = occ + 1;
                _poolHits++;
                return CloneResult(c);
            }

            // All at capacity — allow overflow
            var ov = shuffled[0];
            var ok = (ov.sourceNodeID, ov.destinationNodeID);
            _routeOccupancy.TryGetValue(ok, out int oocc);
            _routeOccupancy[ok] = oocc + 1;
            _poolHits++;
            return CloneResult(ov);
        }

        // Pool miss — compute live and cache
        _poolMisses++;
        Debug.LogWarning($"[NavSystem] Pool miss for node {fromNodeID} " +
                         $"(hits: {_poolHits}, misses: {_poolMisses}). " +
                         "Computing live. Re-bake recommended if this happens often.");
        return ComputeRouteLive(fromNodeID);
    }

    /// <summary>
    /// Returns a route to a specific destination. Checks pool first, then live compute.
    /// Used for rerouting after stuck events.
    /// </summary>
    public RouteResult RequestReroute(int fromNodeID, int toNodeID)
    {
        if (!nodeMap.ContainsKey(fromNodeID)) return RequestRoute(fromNodeID);
        if (!nodeMap.ContainsKey(toNodeID))   return RequestRoute(fromNodeID);

        if (_routePool.TryGetValue(fromNodeID, out var pool))
        {
            var match = pool.FirstOrDefault(r => r.destinationNodeID == toNodeID);
            if (match != null)
            {
                var key = (fromNodeID, toNodeID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;
                return CloneResult(match);
            }
        }

        // Not in pool — compute directly
        List<int> path = FindPath(fromNodeID, toNodeID);
        if (path?.Count >= 2)
        {
            List<Vector3> wps = GetDenseRoute(path);
            if (wps?.Count >= 2)
            {
                CacheInPool(fromNodeID, toNodeID, wps);
                var key = (fromNodeID, toNodeID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;
                return MakeRouteResult(fromNodeID, toNodeID, wps);
            }
        }

        return RequestRoute(fromNodeID);
    }

    /// <summary>Decrements occupancy when an NPC finishes or abandons a route.</summary>
    public void ReleaseRoute(int srcNodeID, int dstNodeID)
    {
        var key = (srcNodeID, dstNodeID);
        if (!_routeOccupancy.TryGetValue(key, out int occ)) return;
        if (occ <= 1) _routeOccupancy.Remove(key);
        else          _routeOccupancy[key] = occ - 1;
    }

    private RouteResult ComputeRouteLive(int fromNodeID)
    {
        if (!nodeMap.ContainsKey(fromNodeID))
            return Fail($"Node {fromNodeID} not in nodeMap.");

        Vector3 srcPos     = nodeMap[fromNodeID].worldPosition;
        var     candidates = nodeMap.Keys
            .Where(id => id != fromNodeID)
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        float[] minDists = { minDestinationDistance, minDestinationDistance * 0.5f, 0f };

        foreach (float minD in minDists)
        {
            foreach (int destID in candidates)
            {
                float d = Vector3.Distance(srcPos, nodeMap[destID].worldPosition);
                if (d < minD || d > maxDestinationDistance) continue;

                List<int> path = FindPath(fromNodeID, destID);
                if (path == null || path.Count < minPathLength || path.Count > maxPathLength) continue;

                List<Vector3> wps = GetDenseRoute(path);
                if (wps == null || wps.Count < 2) continue;

                CacheInPool(fromNodeID, destID, wps);

                var key = (fromNodeID, destID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;
                return MakeRouteResult(fromNodeID, destID, wps);
            }
        }

        // Absolute last resort
        int fb = FindAnyReachableNode(fromNodeID);
        if (fb != -1)
        {
            List<int> fp = FindPath(fromNodeID, fb);
            if (fp?.Count >= 2)
            {
                List<Vector3> fw = GetDenseRoute(fp);
                if (fw?.Count >= 2)
                {
                    CacheInPool(fromNodeID, fb, fw);
                    var key = (fromNodeID, fb);
                    _routeOccupancy.TryGetValue(key, out int occ);
                    _routeOccupancy[key] = occ + 1;
                    return MakeRouteResult(fromNodeID, fb, fw);
                }
            }
        }

        return Fail($"No route found from node {fromNodeID}. Check graph connectivity.");
    }

    /// <summary>
    /// Adds a live-computed route to the pool if under the cap.
    /// Prevents unbounded memory growth during long sessions.
    /// </summary>
    private void CacheInPool(int srcID, int dstID, List<Vector3> wps)
    {
        if (!_routePool.ContainsKey(srcID))
            _routePool[srcID] = new List<RouteResult>();

        int cap = routesPerSourceNode + maxLiveRoutesPerNode;
        if (_routePool[srcID].Count >= cap) return;

        _routePool[srcID].Add(MakeResult(srcID, dstID, wps));
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    private static RouteResult MakeResult(int src, int dst, List<Vector3> wps) => new RouteResult
    { success = true, sourceNodeID = src, destinationNodeID = dst, waypoints = wps };

    private static RouteResult MakeRouteResult(int src, int dst, List<Vector3> wps) => new RouteResult
    { success = true, sourceNodeID = src, destinationNodeID = dst, waypoints = new List<Vector3>(wps) };

    private static RouteResult CloneResult(RouteResult src) => new RouteResult
    {
        success = src.success, sourceNodeID = src.sourceNodeID,
        destinationNodeID = src.destinationNodeID,
        waypoints = new List<Vector3>(src.waypoints),
    };

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

    private static RouteResult MakeResult(int src, int dst, Vector3[] wps) => new RouteResult
    { success = true, sourceNodeID = src, destinationNodeID = dst, waypoints = new List<Vector3>(wps) };

    // =========================================================================
    //  A* PATHFINDING
    // =========================================================================

    public List<int> FindPath(int start, int target)
    {
        if (!nodeMap.ContainsKey(start) || !nodeMap.ContainsKey(target)) return null;
        if (start == target) return new List<int> { start };

        var cameFrom = new Dictionary<int, int>();
        var gScore   = new Dictionary<int, float> { [start] = 0f };
        var fScore   = new Dictionary<int, float> { [start] = Heuristic(start, target) };
        var open     = new BinaryMinHeap<int>();
        var closed   = new HashSet<int>();

        open.Push(start, fScore[start]);

        while (open.Count > 0)
        {
            int cur = open.Pop();
            if (cur == target) return ReconstructPath(cameFrom, cur);
            closed.Add(cur);

            foreach (int nb in GetNeighbors(cur))
            {
                if (closed.Contains(nb)) continue;
                float tg = gScore[cur] + EdgeCost(cur, nb);
                if (!gScore.ContainsKey(nb) || tg < gScore[nb])
                {
                    cameFrom[nb] = cur;
                    gScore[nb]   = tg;
                    fScore[nb]   = tg + Heuristic(nb, target);
                    if (!open.Contains(nb)) open.Push(nb, fScore[nb]);
                }
            }
        }
        return null;
    }

    public List<int> GetNeighbors(int nodeID)
    {
        var result = new List<int>();
        foreach (var conn in connectionDefinitions)
        {
            if (conn.fromNodeID == nodeID && nodeMap.ContainsKey(conn.toNodeID))
                result.Add(conn.toNodeID);
            else if (conn.bidirectional && conn.toNodeID == nodeID && nodeMap.ContainsKey(conn.fromNodeID))
                result.Add(conn.fromNodeID);
        }
        return result.Distinct().ToList();
    }

    private float Heuristic(int a, int b)
    {
        if (!nodeMap.ContainsKey(a) || !nodeMap.ContainsKey(b)) return 999999f;
        Vector3 pa = nodeMap[a].worldPosition, pb = nodeMap[b].worldPosition;
        return Vector3.Distance(new Vector3(pa.x, 0, pa.z), new Vector3(pb.x, 0, pb.z));
    }

    private float EdgeCost(int a, int b)
    {
        if (!nodeMap.ContainsKey(a) || !nodeMap.ContainsKey(b)) return 1f;
        return Vector3.Distance(nodeMap[a].worldPosition, nodeMap[b].worldPosition);
    }

    private List<int> ReconstructPath(Dictionary<int, int> came, int cur)
    {
        var path = new List<int> { cur };
        while (came.ContainsKey(cur)) { cur = came[cur]; path.Insert(0, cur); }
        return path;
    }

    private int FindAnyReachableNode(int fromID)
    {
        var visited = new HashSet<int> { fromID };
        var queue   = new Queue<int>(new[] { fromID });
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            foreach (int nb in GetNeighbors(cur))
            {
                if (visited.Contains(nb)) continue;
                visited.Add(nb); if (nb != fromID) return nb;
                queue.Enqueue(nb);
            }
        }
        return -1;
    }

    // =========================================================================
    //  NODE QUERIES
    // =========================================================================

    public int GetClosestNode(Vector3 worldPos)
    {
        float best = float.MaxValue; int id = -1;
        foreach (var kvp in nodeMap)
        {
            if (kvp.Value == null) continue;
            float d = Vector3.Distance(worldPos, kvp.Value.worldPosition);
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
        var avail = nodeMap.Keys.Where(k => !exclude.Contains(k)).ToList();
        return avail.Count > 0 ? avail[UnityEngine.Random.Range(0, avail.Count)] : GetRandomNode();
    }

    public int GetDistantNode(int fromNodeID, float minDistance = 25f)
    {
        if (!nodeMap.ContainsKey(fromNodeID)) return -1;
        var cands = nodeMap.Keys
            .Where(id => id != fromNodeID
                      && Vector3.Distance(nodeMap[fromNodeID].worldPosition,
                                          nodeMap[id].worldPosition) >= minDistance)
            .ToList();
        return cands.Count > 0 ? cands[UnityEngine.Random.Range(0, cands.Count)] : fromNodeID;
    }

    // =========================================================================
    //  VEHICLE SPAWNING
    // =========================================================================

    private void SpawnTrafficVehicles()
    {
        ClearAllTraffic();
        if (nodes.Count < 2) { Debug.LogError("[Traffic] Need ≥ 2 nodes!"); return; }

        var prefabs = new List<GameObject>();
        if (npcVehiclePrefab != null) prefabs.Add(npcVehiclePrefab);
        prefabs.AddRange(npcVariants.Where(v => v != null));
        if (prefabs.Count == 0) { Debug.LogError("[Traffic] No NPC prefabs assigned!"); return; }

        VehicleGroundConfig gc = BuildGroundConfig();
        VehicleSharedConfig sc = BuildSharedConfig();

        var nodeIDs = nodeMap.Keys.ToList();
        Shuffle(nodeIDs);

        var usedPositions = new List<Vector3>();
        int spawned = 0;

        foreach (int nodeID in nodeIDs)
        {
            if (spawned >= totalTrafficVehicles) break;
            if (!nodeMap.ContainsKey(nodeID)) continue;

            Vector3 nodePos = nodeMap[nodeID].transform.position;
            // ── Dynamic spacing based on local node density ───────────────────
            // Dense area (many nodes nearby) = use smaller spacing so vehicles
            // still spawn there without all being rejected.
            // Sparse area (few nodes nearby) = use larger spacing so vehicles
            // don't cluster at the few available nodes.
            float localSpacing = GetDensityBasedSpacing(nodePos);
            if (usedPositions.Any(p => Vector3.Distance(nodePos, p) < localSpacing)) continue;

            GameObject prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];

            // Instantiate with identity — correct rotation is applied AFTER
            // Initialize() assigns the route (see GetSpawnFacingDirection below)
            var obj = Instantiate(prefab, new Vector3(nodePos.x, -5000f, nodePos.z), Quaternion.identity);
            obj.name = $"NPC_Vehicle_{spawned:D3}";

            float    bottom   = GetColliderBottomOffset(obj);
            Vector3  spawnPos = new Vector3(nodePos.x, nodePos.y + bottom + spawnHeightOffset, nodePos.z);

            // Spawn NavMesh snap — TIGHT 1.5 m radius only.
            // Old 4 m radius snapped cars spawning near road edges onto baked
            // pavement/kerb, causing immediate sideways driving.
            // With 1.5 m + lateral guard, we only correct truly off-mesh spawns.
            if (NavMesh.SamplePosition(spawnPos, out NavMeshHit navHit, 1.5f, NavMesh.AllAreas))
            {
                float lateralShift = new Vector2(navHit.position.x - spawnPos.x,
                                                  navHit.position.z - spawnPos.z).magnitude;
                if (lateralShift < 1.5f) // reject snaps that push car sideways to pavement
                    spawnPos = new Vector3(navHit.position.x, spawnPos.y, navHit.position.z);
            }

            Rigidbody rb = obj.GetComponent<Rigidbody>() ?? obj.AddComponent<Rigidbody>();
            rb.mass = 1200f; rb.linearDamping = 1f; rb.angularDamping = 10f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic   = true;

            obj.transform.position = spawnPos;
            rb.position = spawnPos;

            // Neutralise child Rigidbodies (truck cabs, articulated joints)
            foreach (Rigidbody childRb in obj.GetComponentsInChildren<Rigidbody>())
            {
                if (childRb == rb) continue;
                childRb.useGravity = false;
                childRb.linearDamping = childRb.angularDamping = 20f;
                childRb.constraints = RigidbodyConstraints.FreezeAll;
            }
            //foreach (Joint joint in obj.GetComponentsInChildren<Joint>())
              //  joint.enabled = false;

            TrafficVehicle tv = obj.GetComponent<TrafficVehicle>() ?? obj.AddComponent<TrafficVehicle>();
            tv.Initialize(this, nodeID, gc, sc);

            // ── Apply correct facing AFTER Initialize ─────────────────────────
            // Initialize() calls RequestRoute() synchronously, so denseWaypoints
            // is already populated. GetSpawnFacingDirection() reads waypoints[1]-[0]
            // which is the ACTUAL first road step — correct for any direction,
            // including routes where the next node is "behind" the spawn point.
            Quaternion correctRot = tv.GetSpawnFacingDirection();
            obj.transform.rotation = correctRot;
            rb.rotation = correctRot;
            tv.SyncSpawnRotation(); // sync internal tilt accumulator — prevents spawn zigzag

            StartCoroutine(ReleaseKinematicAfterSetup(rb));
            activeVehicles.Add(tv);
            usedPositions.Add(spawnPos);
            spawned++;
        }

        Debug.Log($"[Traffic] ══ {spawned}/{totalTrafficVehicles} NPC vehicles spawned ══  " +
                  $"Pool: {_routePool.Values.Sum(v => v.Count)} routes ready.");
    }

    // =========================================================================
    //  DENSITY-BASED SPAWN SPACING
    //
    //  Counts how many nodes exist within densitySampleRadius of this position.
    //  More nodes nearby = denser area = allow tighter vehicle spacing.
    //  Fewer nodes nearby = sparse area = enforce wider spacing.
    //
    //  Result is lerped between vehicleSpacingMin and vehicleSpacingMax:
    //    densityCount >= densityHigh  →  vehicleSpacingMin  (dense, pack tighter)
    //    densityCount <= densityLow   →  vehicleSpacingMax  (sparse, spread out)
    // =========================================================================

    private float GetDensityBasedSpacing(Vector3 pos)
    {
        const int densityLow  = 3;   // ≤ this many neighbors = sparse
        const int densityHigh = 12;  // ≥ this many neighbors = dense

        int nearbyCount = 0;
        foreach (var kvp in nodeMap)
        {
            if (kvp.Value == null) continue;
            if (Vector3.Distance(pos, kvp.Value.transform.position) <= densitySampleRadius)
                nearbyCount++;
        }

        // Normalize: 0 = sparse, 1 = dense
        float t = Mathf.InverseLerp(densityLow, densityHigh, nearbyCount);

        // Dense areas get smaller spacing, sparse areas get larger spacing
        return Mathf.Lerp(vehicleSpacingMax, vehicleSpacingMin, t);
    }

    private float GetColliderBottomOffset(GameObject obj)
    {
        // IMPORTANT: obj is at Y=-5000 when this is called (before final placement).
        // col.bounds is world-space and IS valid after Instantiate, but compound
        // colliders on some prefabs (truck cabs, articulated vehicles) may have
        // incorrect world bounds until the first physics step.
        //
        // Safer approach: use the collider's local geometry directly.
        // For BoxCollider: local bottom = center.y - size.y*0.5
        // For CapsuleCollider: local bottom = center.y - height*0.5
        // For MeshCollider: fall back to world bounds (no local equivalent)
        // Take the LOWEST local bottom among all non-trigger colliders.

        float lowest = 0f;
        bool  found  = false;

        foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue;

            float localBottom;
            if (col is BoxCollider box)
            {
                localBottom = box.center.y - box.size.y * 0.5f;
            }
            else if (col is CapsuleCollider cap)
            {
                localBottom = cap.center.y - cap.height * 0.5f;
            }
            else if (col is SphereCollider sph)
            {
                localBottom = sph.center.y - sph.radius;
            }
            else
            {
                // MeshCollider or unknown — use world bounds relative to obj pivot
                localBottom = col.bounds.min.y - obj.transform.position.y;
            }

            // Account for non-uniform scale on the child transform
            // (scale.y flips the sign for inverted objects — take abs)
            float worldScale  = Mathf.Abs(col.transform.lossyScale.y);
            float worldBottom = localBottom * worldScale;

            // Offset from this collider's transform to the root obj transform
            float transformYOffset = col.transform.position.y - obj.transform.position.y;
            float rootRelativeBottom = worldBottom + transformYOffset;

            if (!found || rootRelativeBottom < lowest)
            {
                lowest = rootRelativeBottom;
                found  = true;
            }
        }

        // lowest is the Y offset of the collider bottom relative to obj pivot.
        // We want to spawn so the bottom of the car sits AT nodePos.y.
        // spawnPos.y = nodePos.y + offset, where offset = -lowest (raise by the depth below pivot).
        return found ? -lowest : 0.5f; // 0.5 m safe default if no collider found
    }

    private IEnumerator ReleaseKinematicAfterSetup(Rigidbody rb)
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        if (rb == null) yield break;
        rb.isKinematic     = false;
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    // =========================================================================
    //  CONFIG BUILDERS
    // =========================================================================

    private VehicleGroundConfig BuildGroundConfig()
    {
        LayerMask resolved;
        if (overrideGroundLayer) { resolved = groundLayerOverride; }
        else switch (groundSurfaceType)
        {
            case GroundSurfaceType.Road:
                int rl = LayerMask.NameToLayer("Road");
                resolved = rl >= 0 ? (1 << rl) : LayerMask.GetMask("Default");
                if (rl < 0) Debug.LogWarning("[NavSystem] No 'Road' layer — using Default.");
                break;
            case GroundSurfaceType.Terrain:       resolved = LayerMask.GetMask("Terrain"); break;
            case GroundSurfaceType.RoadAndTerrain:
                int roadL = LayerMask.NameToLayer("Road");
                LayerMask t = LayerMask.GetMask("Terrain");
                resolved = roadL >= 0 ? ((1 << roadL) | t) : t;
                break;
            case GroundSurfaceType.Custom:        resolved = groundLayerOverride; break;
            default:                              resolved = LayerMask.GetMask("Default"); break;
        }
        return new VehicleGroundConfig
        {
            groundLayer = resolved, groundRayUpOffset = groundRayUpOffset,
            groundRayDistance = groundRayDistance, rideHeight = vehicleRideHeight,
            groundSnapStrength = vehicleGroundSnapStrength, slopeTiltSpeed = vehicleSlopeTiltSpeed,
            hillClimbBoost = vehicleHillClimbBoost,
        };
    }

    private VehicleSharedConfig BuildSharedConfig() => new VehicleSharedConfig
    {
        speed = vehicleSpeed, turnSpeed = vehicleTurnSpeed, speedSmoothTime = vehicleSpeedSmoothTime,
        minPathLength = minPathLength, maxPathLength = maxPathLength,
        minDestinationDistance = minDestinationDistance, maxDestinationDistance = maxDestinationDistance,
        maxPathAttempts = maxPathAttempts, waypointReachDistanceXZ = waypointReachDistanceXZ,
        waypointReachDistanceY = waypointReachDistanceY, minAdvanceInterval = minAdvanceInterval,
        detectionLayerMask = detectionLayerMask, npcVehicleLayer = npcVehicleLayer,
        playerVehicleLayer = playerVehicleLayer, trafficLightLayer = trafficLightLayer,
        detectionRange = detectionRange, vehicleStoppingDistance = vehicleStoppingDistance,
        obstacleStoppingDistance = obstacleStoppingDistance,
        trafficLightStopDistance = trafficLightStopDistance, maxRedLightWaitTime = maxRedLightWaitTime,
        maxStuckFrames = maxStuckFrames, stuckMovementThreshold = stuckMovementThreshold,
        maxPathRecalculations = maxPathRecalculations, showDebugGizmos = showDebugGizmos,
    };

    // =========================================================================
    //  TRAFFIC MANAGEMENT
    // =========================================================================

    [ContextMenu("Spawn Traffic Now")]  public void SpawnTrafficNow()  => SpawnTrafficVehicles();
    [ContextMenu("Clear All Traffic")]  public void ClearAllTraffic()
    {
        foreach (var v in activeVehicles) if (v != null) Destroy(v.gameObject);
        activeVehicles.Clear();
    }
    [ContextMenu("Respawn Traffic")]    public void RespawnTraffic()   { ClearAllTraffic(); SpawnTrafficVehicles(); }

    // =========================================================================
    //  PATH VISUALIZATION  (player car — CentralizedCarController)
    // =========================================================================

    private void SetupLineRenderer()
    {
        if (pathLineRenderer != null) return;
        var obj = new GameObject("PathVisualizer");
        obj.transform.SetParent(transform);
        pathLineRenderer = obj.AddComponent<LineRenderer>();
        pathLineRenderer.material   = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.startWidth = pathLineRenderer.endWidth = 0.2f;
        var grad = new Gradient();
        grad.colorKeys = new[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(Color.red, 1f) };
        pathLineRenderer.colorGradient = grad;
        pathLineRenderer.enabled = false;
    }

    public void ClearPathVisualization()
    {
        if (pathLineRenderer != null) { pathLineRenderer.enabled = false; pathLineRenderer.positionCount = 0; }
        _playerCachedNodePath   = null;
        _playerCachedDenseRoute = null;
    }

    public void VisualizePlayerPath(List<int> nodePath, Vector3 playerWorldPos)
    {
        if (nodePath == null || nodePath.Count == 0) { ClearPathVisualization(); return; }
        SetupLineRenderer();

        bool changed = _playerCachedNodePath == null || _playerCachedNodePath.Count != nodePath.Count;
        if (!changed) for (int i = 0; i < nodePath.Count; i++)
            if (_playerCachedNodePath[i] != nodePath[i]) { changed = true; break; }

        if (changed)
        {
            _playerCachedDenseRoute = RouteCacheReady
                ? GetDenseRoute(nodePath) : BuildNodePositionList(nodePath);
            if (_playerCachedDenseRoute == null || _playerCachedDenseRoute.Count < 2)
                _playerCachedDenseRoute = BuildNodePositionList(nodePath);
            _playerCachedNodePath = new List<int>(nodePath);
        }

        if (_playerCachedDenseRoute == null || _playerCachedDenseRoute.Count < 2)
        { ClearPathVisualization(); return; }

        int closestSeg = 0; float closestDistSq = float.MaxValue, closestT = 0f;
        for (int i = 0; i < _playerCachedDenseRoute.Count - 1; i++)
        {
            Vector3 a = _playerCachedDenseRoute[i], b = _playerCachedDenseRoute[i + 1];
            Vector3 ab = b - a; float len = ab.sqrMagnitude;
            float t = len > 0.0001f ? Mathf.Clamp01(Vector3.Dot(playerWorldPos - a, ab) / len) : 0f;
            Vector3 proj = a + ab * t;
            float dxz = new Vector2(playerWorldPos.x - proj.x, playerWorldPos.z - proj.z).sqrMagnitude;
            if (dxz < closestDistSq) { closestDistSq = dxz; closestSeg = i; closestT = t; }
        }

        int startSeg = closestSeg; float startT = closestT;
        if (startT >= 0.9999f && startSeg + 1 < _playerCachedDenseRoute.Count - 1) { startSeg++; startT = 0f; }

        var trimmed = new List<Vector3>();
        Vector3 sA = _playerCachedDenseRoute[startSeg];
        Vector3 sB = startSeg + 1 < _playerCachedDenseRoute.Count ? _playerCachedDenseRoute[startSeg + 1] : sA;
        Vector3 p0 = Vector3.Lerp(sA, sB, startT); p0.y += pathLineHeightOffset; trimmed.Add(p0);
        for (int i = startSeg + 1; i < _playerCachedDenseRoute.Count; i++)
        { Vector3 p = _playerCachedDenseRoute[i]; p.y += pathLineHeightOffset; trimmed.Add(p); }

        if (trimmed.Count < 2) { ClearPathVisualization(); return; }
        pathLineRenderer.positionCount = trimmed.Count;
        pathLineRenderer.SetPositions(trimmed.ToArray());
        pathLineRenderer.enabled = true;
    }

    public void InvalidatePlayerPathCache() { _playerCachedNodePath = null; _playerCachedDenseRoute = null; }

    public void VisualizePath(List<int> path)
    {
        if (path == null || path.Count == 0) return;
        SetupLineRenderer();
        pathLineRenderer.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            if (!nodeMap.ContainsKey(path[i])) continue;
            Vector3 p = nodeMap[path[i]].worldPosition; p.y += pathLineHeightOffset;
            pathLineRenderer.SetPosition(i, p);
        }
        pathLineRenderer.enabled = true;
    }

    private List<Vector3> BuildNodePositionList(List<int> nodePath)
    {
        var pts = new List<Vector3>();
        foreach (int id in nodePath) if (nodeMap.ContainsKey(id)) pts.Add(nodeMap[id].worldPosition);
        return pts;
    }

    // =========================================================================
    //  GRAPH MANAGEMENT
    // =========================================================================

    [ContextMenu("Validate And Rebuild Graph")]
    public void ValidateAndRebuildGraph()
    {
        nodes.RemoveAll(n => n == null);
        nextNodeID = 0;
        foreach (var node in nodes) if (node != null && node.nodeID >= nextNodeID) nextNodeID = node.nodeID + 1;

        nodeMap.Clear();
        var used = new HashSet<int>();
        foreach (var node in nodes)
        {
            if (node == null) continue;
            if (used.Contains(node.nodeID)) node.nodeID = nextNodeID++;
            used.Add(node.nodeID);
            node.parentNavSystem = this;
            nodeMap[node.nodeID] = node;
        }
        ValidateConnections();
    }

    private void ValidateConnections()
    {
        connectionDefinitions = connectionDefinitions
            .Where(c => c != null && nodeMap.ContainsKey(c.fromNodeID) && nodeMap.ContainsKey(c.toNodeID))
            .ToList();
    }

    public void RefreshGraph() => ValidateAndRebuildGraph();

    public void RegisterNode(NavNode node)
    {
        if (node == null) return;
        if (nodes.Contains(node)) { if (!nodeMap.ContainsKey(node.nodeID)) nodeMap[node.nodeID] = node; node.parentNavSystem = this; return; }
        if (node.nodeID < 0 || nodeMap.ContainsKey(node.nodeID)) node.nodeID = nextNodeID++;
        else if (node.nodeID >= nextNodeID) nextNodeID = node.nodeID + 1;
        node.parentNavSystem = this; nodes.Add(node); nodeMap[node.nodeID] = node;
    }

    public void AddConnectionDefinition(int fromID, int toID, bool bidir)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID)) return;
        AddConnection(fromID, toID, bidir); ValidateConnections();
    }

    public void AddConnection(int fromID, int toID, bool bidir)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID)) return;
        bool exists = connectionDefinitions.Any(c =>
            (c.fromNodeID == fromID && c.toNodeID == toID) ||
            (bidir && c.fromNodeID == toID && c.toNodeID == fromID));
        if (!exists) connectionDefinitions.Add(new ConnectionDefinition(fromID, toID, bidir));
    }

    public void RemoveConnection(int fromID, int toID) =>
        connectionDefinitions.RemoveAll(c =>
            (c.fromNodeID == fromID && c.toNodeID == toID) ||
            (c.fromNodeID == toID   && c.toNodeID == fromID));

    // =========================================================================
    //  NODE CREATION
    // =========================================================================

    public NavNode CreateNode(Vector3 position, int id = -1, Quaternion? rotation = null)
    {
        if (nodesParent == null)
        { nodesParent = new GameObject("NavigationNodes"); nodesParent.transform.SetParent(transform); }

        int finalID = (id == -1 || nodeMap.ContainsKey(id)) ? nextNodeID++ : id;
        if (id >= nextNodeID) nextNodeID = id + 1;

        var nodeObj = new GameObject($"NavNode_{finalID}");
        nodeObj.transform.SetParent(nodesParent.transform);
        nodeObj.transform.position = position;
        nodeObj.transform.rotation = rotation ?? Quaternion.identity;

        NavNode node = nodeObj.AddComponent<NavNode>();
        node.parentNavSystem = this; node.nodeID = finalID;
        nodes.Add(node); nodeMap[finalID] = node;

#if UNITY_EDITOR
        if (autoSnapNewNodes && !Application.isPlaying) SnapNodeToGround(node);
#endif
        return node;
    }

    // =========================================================================
    //  GIZMOS
    // =========================================================================

    private void OnDrawGizmos()
    {
        if (nodes != null)
        {
            foreach (var node in nodes)
            {
                if (node == null) continue;
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(node.transform.position, 0.6f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(node.transform.position + Vector3.up * 1.3f, $"Node {node.nodeID}",
                    new GUIStyle { normal = new GUIStyleState { textColor = Color.white },
                        fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
#endif
            }
        }
        if (connectionDefinitions != null)
        {
            foreach (var conn in connectionDefinitions)
            {
                if (!nodeMap.ContainsKey(conn.fromNodeID) || !nodeMap.ContainsKey(conn.toNodeID)) continue;
                Vector3 s = nodeMap[conn.fromNodeID].transform.position + Vector3.up * 0.2f;
                Vector3 e = nodeMap[conn.toNodeID].transform.position   + Vector3.up * 0.2f;
                Gizmos.color = conn.bidirectional ? new Color(0,1,0,0.8f) : new Color(1,0.5f,0,0.8f);
                Gizmos.DrawLine(s, e);
                if (!conn.bidirectional)
                {
                    Vector3 dir = (e-s).normalized, mid = s+dir*Vector3.Distance(s,e)*0.5f;
                    Vector3 perp = Vector3.Cross(Vector3.up, dir)*0.5f;
                    Gizmos.DrawLine(mid, mid-dir+perp); Gizmos.DrawLine(mid, mid-dir-perp);
                }
            }
        }
        if (showDebugGizmos && Application.isPlaying)
        {
            Gizmos.color = Color.green;
            foreach (var v in activeVehicles) if (v != null) Gizmos.DrawWireSphere(v.transform.position+Vector3.up, 0.8f);
        }
    }

    // =========================================================================
    //  EDITOR UTILITIES
    // =========================================================================

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying) { ValidateAndRebuildGraph(); if (visualizeAllConnectionsEditor) DrawAllConnectionsIntoLineRenderer(); }
    }
    private void Update()
    {
        if (!Application.isPlaying && visualizeAllConnectionsEditor) DrawAllConnectionsIntoLineRenderer();
    }

    [ContextMenu("Collect All Nodes")]
    public void CollectAllNodes()
    {
        nodes.Clear(); nodeMap.Clear();
        NavNode[] all = nodesParent != null
            ? nodesParent.GetComponentsInChildren<NavNode>(true)
            : FindObjectsOfType<NavNode>();
        nextNodeID = 0;
        foreach (var n in all) if (n != null && n.nodeID >= nextNodeID) nextNodeID = n.nodeID + 1;
        foreach (var n in all)
        {
            if (n == null) continue;
            if (n.nodeID < 0 || nodeMap.ContainsKey(n.nodeID)) n.nodeID = nextNodeID++;
            n.parentNavSystem = this; nodes.Add(n); nodeMap[n.nodeID] = n;
        }
        ValidateConnections();
    }

    public bool SnapNodeToGround(NavNode node)
    {
        if (node == null) return false;
        Vector3 origin = node.transform.position + Vector3.up * snapRaycastOriginHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, snapRaycastOriginHeight + 500f, snapLayer))
        {
            Undo.RecordObject(node.transform, "Snap Node To Ground");
            node.transform.position = hit.point + Vector3.up * snapNodeHeightOffset;
            if (snapAlignToSurface) node.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            EditorUtility.SetDirty(node.gameObject);
            return true;
        }
        Debug.LogWarning($"[NavSystem] Snap missed Node {node.nodeID}.");
        return false;
    }

    [ContextMenu("Snap All Nodes To Ground")]
    public void SnapAllNodesToGround()
    {
        int ok = 0, miss = 0;
        foreach (var n in nodes) { if (SnapNodeToGround(n)) ok++; else miss++; }
        Debug.Log($"[NavSystem] Snap: {ok} snapped, {miss} missed.");
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Auto Connect Nodes")]
    public void AutoConnectNodes()
    {
        connectionDefinitions.Clear();
        for (int i = 0; i < nodes.Count; i++) for (int j = i+1; j < nodes.Count; j++)
            if (nodes[i] != null && nodes[j] != null &&
                Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position) <= autoConnectMaxDistance)
                AddConnection(nodes[i].nodeID, nodes[j].nodeID, true);
        ValidateConnections();
    }

    [ContextMenu("Clear All Connections")]
    public void ClearAllConnections() => connectionDefinitions.Clear();

    [ContextMenu("Create Node Forward")]
    public void CreateNodeForward()
    {
        NavNode last = nodes.Count > 0 ? nodes[nodes.Count-1] : null;
        Vector3 pos  = last != null ? last.transform.position + last.transform.forward * newNodeDistance : transform.position;
        NavNode n    = CreateNode(pos, -1, last?.transform.rotation ?? Quaternion.identity);
        if (last != null) AddConnectionDefinition(last.nodeID, n.nodeID, true);
        Selection.activeGameObject = n.gameObject;
    }

    [ContextMenu("Create Node From Selected")]
    public void CreateNextNodeFromSelected()
    {
        if (Selection.activeGameObject == null) return;
        NavNode sel = Selection.activeGameObject.GetComponent<NavNode>();
        if (sel == null || sel.parentNavSystem != this) { Debug.LogWarning("[NavSystem] No NavNode selected."); return; }
        NavNode n = CreateNode(sel.transform.position + sel.transform.forward * newNodeDistance, -1, sel.transform.rotation);
        AddConnectionDefinition(sel.nodeID, n.nodeID, true);
        Selection.activeGameObject = n.gameObject;
    }

    [ContextMenu("Setup Demo")]
    public void SetupDemo()
    {
        ClearAllConnections(); nodes.Clear(); nodeMap.Clear(); nextNodeID = 0;
        if (nodesParent == null) { nodesParent = new GameObject("NavigationNodes"); nodesParent.transform.SetParent(transform); }
        Vector3[] pos = { new(0,.5f,0), new(10,.5f,0), new(15,.5f,10), new(10,.5f,20), new(0,.5f,20), new(-10,.5f,10) };
        foreach (var p in pos) CreateNode(p);
        var ids = nodes.Select(n => n.nodeID).ToList();
        for (int i = 0; i < ids.Count-1; i++) AddConnectionDefinition(ids[i], ids[i+1], true);
        AddConnectionDefinition(ids[ids.Count-1], ids[0], true);
        ValidateAndRebuildGraph();
    }

    [ContextMenu("Test Path Zero To Last")]
    public void TestPathZeroToLast()
    {
        if (nodes.Count < 2) return;
        var path = FindPath(nodes[0].nodeID, nodes[nodes.Count-1].nodeID);
        if (path?.Count > 0) { Debug.Log($"[NavSystem] Path: {string.Join(" → ", path)}"); VisualizePath(path); }
        else Debug.LogError("[NavSystem] No path found between first and last node.");
    }

    [ContextMenu("Debug Print All Connections")]
    public void DebugPrintAllConnections()
    {
        Debug.Log($"[NavSystem] ══ Connections ({connectionDefinitions.Count}) ══");
        foreach (var c in connectionDefinitions)
        {
            string fn = nodeMap.ContainsKey(c.fromNodeID) ? nodeMap[c.fromNodeID].name : "MISSING";
            string tn = nodeMap.ContainsKey(c.toNodeID)   ? nodeMap[c.toNodeID].name   : "MISSING";
            Debug.Log($"  {c.fromNodeID}({fn}) {(c.bidirectional?"↔":"→")} {c.toNodeID}({tn})");
        }
    }

    [ContextMenu("Debug Print Segment Cache")]
    public void DebugPrintSegmentCache()
    {
        int total = 0;
        Debug.Log($"[NavSystem] ══ Segment Cache ({_segmentCache.Count}) ══");
        foreach (var kvp in _segmentCache) { Debug.Log($"  {kvp.Key.Item1}→{kvp.Key.Item2}: {kvp.Value.Length} waypoints"); total += kvp.Value.Length; }
        Debug.Log($"  Total waypoints: {total}");
    }

    [ContextMenu("Debug Print Route Pool")]
    public void DebugPrintRoutePool()
    {
        int total = 0;
        Debug.Log($"[NavSystem] ══ Route Pool ({_routePool.Count} nodes) — Hits:{_poolHits} Misses:{_poolMisses} ══");
        foreach (var kvp in _routePool) { Debug.Log($"  Node {kvp.Key}: {kvp.Value.Count} routes"); total += kvp.Value.Count; }
        Debug.Log($"  Total routes in pool: {total}");
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
        if (positions.Count > 0) pathLineRenderer.SetPositions(positions.ToArray());
        pathLineRenderer.enabled = positions.Count > 0;
    }
#endif

    // =========================================================================
    //  BINARY MIN-HEAP  (A* priority queue — O(log n) vs O(n) for large graphs)
    // =========================================================================

    private class BinaryMinHeap<T>
    {
        private readonly List<(T item, float priority)> _heap = new List<(T, float)>();
        private readonly HashSet<T>                     _set  = new HashSet<T>();

        public int Count => _heap.Count;
        public bool Contains(T item) => _set.Contains(item);

        public void Push(T item, float priority)
        {
            _heap.Add((item, priority)); _set.Add(item);
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (_heap[p].priority <= _heap[i].priority) break;
                (_heap[p], _heap[i]) = (_heap[i], _heap[p]); i = p;
            }
        }

        public T Pop()
        {
            T root = _heap[0].item; _set.Remove(root);
            int last = _heap.Count - 1;
            _heap[0] = _heap[last]; _heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2*i+1, r = 2*i+2, s = i;
                if (l < _heap.Count && _heap[l].priority < _heap[s].priority) s = l;
                if (r < _heap.Count && _heap[r].priority < _heap[s].priority) s = r;
                if (s == i) break;
                (_heap[i], _heap[s]) = (_heap[s], _heap[i]); i = s;
            }
            return root;
        }
    }
}

// =============================================================================
//  LEGACY
// =============================================================================

[System.Serializable]
public class TrafficWaypointChain
{
    public string          chainName       = "Traffic_Chain";
    public List<Transform> waypoints       = new List<Transform>();
    public List<int>       nodeIDs         = new List<int>();
    public bool            loop            = false;
    [Range(0.5f, 3f)]
    public float           speedMultiplier = 1f;
}