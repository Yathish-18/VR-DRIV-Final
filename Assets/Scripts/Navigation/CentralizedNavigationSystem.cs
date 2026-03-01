// ============================================================================
//  CENTRALIZED NAVIGATION SYSTEM  v7.0  —  OPTION 4
//  ============================================================================
//  STARTUP PIPELINE (Option 4):
//
//  EDITOR  (press button once, only after NavMesh changes):
//    Phase 1 — NavMesh.CalculatePath() per direct connection
//              Dense road-surface waypoints saved to NavRouteCacheAsset
//
//  RUNTIME (every Play session):
//    Frame 0  — LoadSegmentsFromAsset()    ~0 ms   (dictionary fill)
//    Frame 1  — BuildRoutePoolSync()       ~50 ms  (A* + cache stitch)
//               Zero NavMesh calls — pure dictionary lookups
//    Frame 1  — SpawnTrafficVehicles()
//
//  FALLBACK (no valid asset assigned):
//    Original runtime coroutine runs as before — zero regression
//
//  WHY NavMesh BETWEEN NODES:
//    Without NavMesh, NPC midway between two distant nodes has no idea
//    the road curves — it aims straight at the destination and wanders off.
//    NavMesh.CalculatePath() returns actual road-surface corners which we
//    subdivide into dense waypoints so the NPC always has a nearby target.
//
//  WHY SEGMENTS ONLY IN ASSET:
//    Segments depend on NavMesh geometry  → stale only when roads rebuild
//    Routes   depend on node connections  → stale when ANY node/edge changes
//    Routes compute < 100ms from cached segments → pointless to persist them
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
    //  SEGMENT CACHE ASSET  (Option 4)
    // =========================================================================

    [Header("═══════════════  SEGMENT CACHE  ═══════════════")]

    [Tooltip("Pre-baked NavMesh segment data.\n\n" +
             "SETUP:\n" +
             "1. Assets → Create → Navigation → Nav Route Cache Asset\n" +
             "2. Assign the .asset file here\n" +
             "3. Press 'Bake & Save Segment Cache' in the inspector\n" +
             "4. Press Play — NPCs spawn with no startup delay\n\n" +
             "Re-bake only when NavMesh geometry changes (road mesh, terrain).\n" +
             "Moving nodes or editing connections does NOT require re-baking.")]
    public NavRouteCacheAsset routeCacheAsset;

    [Tooltip("If true and a valid cache asset is assigned, load segments at startup\n" +
             "instead of running NavMesh baking. Routes are always computed fresh.")]
    public bool usePreBakedCache = true;

    // =========================================================================
    //  NAVMESH & WAYPOINT SETTINGS
    // =========================================================================

    [Header("═══════════════  NAVMESH SETTINGS  ═══════════════")]

    [Tooltip("Use NavMesh to bake dense surface-following waypoints between nodes.\n" +
             "Requires a baked NavMesh on your road geometry.\n" +
             "Without this, NPCs wander when midway between distant nodes.")]
    public bool useNavMeshHybrid = true;

    [Tooltip("Maximum distance between consecutive waypoints (metres).\n" +
             "Smaller = smoother road following but more memory. 5–10 m recommended.")]
    [Range(2f, 20f)]
    public float maxWaypointSpacing = 6f;

    [Tooltip("Height added above every baked waypoint so cars sit on the surface.")]
    [Range(0f, 2f)]
    public float waypointHeightOffset = 0.15f;

    [Tooltip("Layer(s) to raycast when snapping linear-fallback waypoints to the road.")]
    public LayerMask waypointSnapLayer = ~0;

    [Header("─── Runtime Fallback Baking (no valid asset) ───")]

    [Tooltip("Segments baked per frame during runtime fallback. Higher = faster but may hitch.")]
    [Range(1, 20)]
    public int segmentsBakedPerFrame = 5;

    [Tooltip("Routes pre-computed per source node during runtime fallback.")]
    [Range(1, 30)]
    public int routesPerSourceNode = 8;

    [Tooltip("Nodes processed per frame during runtime route pre-computation.")]
    [Range(1, 20)]
    public int routesBakedPerFrame = 3;

    // ─── Internal segment cache ───────────────────────────────────────────────
    // Key: (fromNodeID, toNodeID) → dense road-surface Vector3 waypoints.
    // Populated from asset on startup OR by NavMesh calls in runtime fallback.
    // Read-only during gameplay. On-demand lazy baking for pool misses.
    private Dictionary<(int, int), Vector3[]> _segmentCache
        = new Dictionary<(int, int), Vector3[]>();

    // ─── Route pool ───────────────────────────────────────────────────────────
    // sourceNodeID → list of pre-built full routes.
    // Always built at runtime from _segmentCache. Never persisted to disk.
    private Dictionary<int, List<RouteResult>> _routePool
        = new Dictionary<int, List<RouteResult>>();

    // ─── Route occupancy ──────────────────────────────────────────────────────
    // (srcID, dstID) → number of active NPCs currently on that route.
    // Capped at MAX_NPCS_PER_ROUTE for traffic variety.
    private Dictionary<(int src, int dst), int> _routeOccupancy
        = new Dictionary<(int, int), int>();

    private const int MAX_NPCS_PER_ROUTE = 2;

    /// <summary>True once both _segmentCache and _routePool are ready to use.</summary>
    public bool RouteCacheReady { get; private set; } = false;

    // =========================================================================
    //  SPAWN SETTINGS
    // =========================================================================

    [Header("═══════════════  SPAWN SETTINGS  ═══════════════")]
    [SerializeField] private GameObject       npcVehiclePrefab;
    [SerializeField] private List<GameObject> npcVariants          = new List<GameObject>();
    [SerializeField] private int              totalTrafficVehicles  = 15;
    [SerializeField] private bool             spawnOnStart          = true;
    [SerializeField] private float            vehicleSpacing        = 15f;
    [SerializeField] private LayerMask        groundLayer           = ~0;
    [SerializeField] private float            spawnHeightOffset     = 0f;

    // =========================================================================
    //  NPC SHARED SETTINGS
    // =========================================================================

    [Header("═══════════════  NPC — MOVEMENT  ═══════════════")]
    [SerializeField] public float vehicleSpeed           = 12f;
    [SerializeField] public float vehicleTurnSpeed       = 3f;
    [SerializeField] public float vehicleSpeedSmoothTime = 0.3f;

    [Header("═══════════════  NPC — PATH CONSTRAINTS  ═══════════════")]
    [SerializeField] public int   minPathLength           = 5;
    [SerializeField] public int   maxPathLength           = 30;
    [SerializeField] public float minDestinationDistance  = 50f;
    [SerializeField] public float maxDestinationDistance  = 300f;
    [SerializeField] public int   maxPathAttempts         = 5;

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
    [SerializeField] private GroundSurfaceType groundSurfaceType    = GroundSurfaceType.Road;
    [SerializeField] private bool              overrideGroundLayer  = false;
    [SerializeField] private LayerMask         groundLayerOverride  = 0;
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
    [Tooltip("Legacy chain system — not used with the node-graph traffic system.")]
    public List<TrafficWaypointChain> trafficChains = new List<TrafficWaypointChain>();

    // =========================================================================
    //  PRIVATE STATE
    // =========================================================================

    private List<TrafficVehicle> activeVehicles = new List<TrafficVehicle>();
    private int nextNodeID = 0;

    // Player path visualization cache (used by CentralizedCarController)
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

    // =========================================================================
    //  ROUTE RESULT  (returned to TrafficVehicle / CentralizedCarController)
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

        if (!spawnOnStart || !Application.isPlaying) return;
        if (nodes.Count < 2)
        {
            Debug.LogError("[NavSystem] Need at least 2 nodes for NPC traffic!");
            return;
        }

        // ── Path A: Pre-baked asset available → fast startup ──────────────────
        if (usePreBakedCache && routeCacheAsset != null && routeCacheAsset.isValid)
        {
            StartCoroutine(StartupFromAsset());
            return;
        }

        // ── Path B: No asset → runtime baking fallback ────────────────────────
        LogFallbackReason();

        if (useNavMeshHybrid)
            StartCoroutine(RuntimeBakeThenSpawn());
        else
        {
            RouteCacheReady = true;
            StartCoroutine(SpawnAfterDelay(0.5f));
        }
    }

    private void LogFallbackReason()
    {
        if (routeCacheAsset == null)
            Debug.LogWarning("[NavSystem] No NavRouteCacheAsset assigned. " +
                             "Falling back to runtime baking (startup delay expected).\n" +
                             "FIX → Assets → Create → Navigation → Nav Route Cache Asset\n" +
                             "     Assign to this component, then press 'Bake & Save Segment Cache'.");
        else if (!routeCacheAsset.isValid)
            Debug.LogWarning("[NavSystem] Cache asset assigned but not yet baked. " +
                             "Falling back to runtime baking.\n" +
                             "FIX → Press 'Bake & Save Segment Cache' in the inspector.");
    }

    // =========================================================================
    //  PATH A: STARTUP FROM PRE-BAKED ASSET
    // =========================================================================

    private IEnumerator StartupFromAsset()
    {
        // Step 1: Load segments — pure dictionary fill, instant
        LoadSegmentsFromAsset();

        // Warn on settings mismatch
        if (!routeCacheAsset.SettingsMatch(maxWaypointSpacing, waypointHeightOffset))
            Debug.LogWarning("[NavSystem] ⚠️  Bake settings differ from current settings " +
                             "(maxWaypointSpacing or waypointHeightOffset changed). " +
                             "Re-bake recommended for accurate waypoint density.");

        if (routeCacheAsset.nodeCount != nodes.Count)
            Debug.LogWarning($"[NavSystem] ⚠️  Node count differs from bake time " +
                             $"(was {routeCacheAsset.nodeCount}, now {nodes.Count}). " +
                             "Missing segments will be lazily baked via NavMesh at runtime.");

        // Step 2: Yield so Unity finishes scene initialisation
        yield return null;

        // Step 3: Build route pool — no NavMesh, pure A* + cache lookups
        float t0 = Time.realtimeSinceStartup;
        BuildRoutePoolSync();
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;

        RouteCacheReady = true;

        int totalRoutes = _routePool.Values.Sum(v => v.Count);
        Debug.Log($"[NavSystem] ✅ Option-4 startup complete — " +
                  $"Segments: {_segmentCache.Count} loaded | " +
                  $"Routes: {totalRoutes} across {_routePool.Count} nodes | " +
                  $"Route-pool build: {ms:F1} ms");

        // Step 4: Spawn NPCs
        SpawnTrafficVehicles();
    }

    /// <summary>
    /// Fills _segmentCache from the asset. Pure dictionary fill — microseconds.
    /// </summary>
    private void LoadSegmentsFromAsset()
    {
        _segmentCache.Clear();
        int loaded = 0;

        foreach (SerializedSegment seg in routeCacheAsset.segments)
        {
            if (seg == null || seg.waypoints == null || seg.waypoints.Length == 0) continue;
            _segmentCache[(seg.fromID, seg.toID)] = seg.waypoints;
            loaded++;
        }

        Debug.Log($"[NavSystem] Loaded {loaded} segments from asset.");
    }

    /// <summary>
    /// Builds _routePool entirely from _segmentCache.
    /// Uses A* for node-path discovery, then GetDenseRoute() for stitching.
    /// No NavMesh calls. Runs synchronously — completes in one frame.
    /// </summary>
    private void BuildRoutePoolSync()
    {
        _routePool.Clear();

        var allNodeIDs  = nodeMap.Keys.ToList();
        int routesBuilt = 0;
        int emptyNodes  = 0;

        foreach (int srcID in allNodeIDs)
        {
            _routePool[srcID] = new List<RouteResult>();
            Vector3 srcPos    = nodeMap[srcID].worldPosition;

            // Prefer destinations in the ideal distance band
            var candidates = allNodeIDs
                .Where(id => id != srcID)
                .Select(id => (id, d: Vector3.Distance(srcPos, nodeMap[id].worldPosition)))
                .Where(t => t.d >= minDestinationDistance && t.d <= maxDestinationDistance)
                .Select(t => t.id)
                .ToList();

            // Relax minimum if nothing in range
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
                if (nodePath == null
                 || nodePath.Count < minPathLength
                 || nodePath.Count > maxPathLength) continue;

                List<Vector3> wps = GetDenseRoute(nodePath);
                if (wps == null || wps.Count < 2) continue;

                _routePool[srcID].Add(new RouteResult
                {
                    success           = true,
                    sourceNodeID      = srcID,
                    destinationNodeID = destID,
                    waypoints         = wps,
                });
                routesBuilt++;
            }

            // Hard fallback: guarantee at least one route per node
            if (_routePool[srcID].Count == 0)
            {
                int fallback = FindAnyReachableNode(srcID);
                if (fallback != -1)
                {
                    List<int> fp = FindPath(srcID, fallback);
                    if (fp != null && fp.Count >= 2)
                    {
                        List<Vector3> fw = GetDenseRoute(fp);
                        if (fw != null && fw.Count >= 2)
                        {
                            _routePool[srcID].Add(new RouteResult
                            {
                                success           = true,
                                sourceNodeID      = srcID,
                                destinationNodeID = fallback,
                                waypoints         = fw,
                            });
                            routesBuilt++;
                        }
                    }
                }
            }

            if (_routePool[srcID].Count == 0) emptyNodes++;
        }

        if (emptyNodes > 0)
            Debug.LogWarning($"[NavSystem] ⚠️  {emptyNodes} nodes have zero routes. " +
                             "Check graph connectivity or re-bake if segments are missing.");
    }

    // =========================================================================
    //  PATH B: RUNTIME BAKING FALLBACK  (original coroutine — unchanged)
    // =========================================================================

    private IEnumerator RuntimeBakeThenSpawn()
    {
        _segmentCache.Clear();
        _routePool.Clear();
        RouteCacheReady = false;

        // ── Phase 1: NavMesh segment baking ───────────────────────────────────
        var segments  = CollectSegmentPairs();
        int segBaked  = 0, segFailed = 0;

        Debug.Log($"[NavSystem] ── Runtime Phase 1/2: Baking {segments.Count} NavMesh segments...");

        for (int i = 0; i < segments.Count; i++)
        {
            var (from, to) = segments[i];
            if (BakeAndCacheSegment(from, to)) segBaked++; else segFailed++;
            if ((i + 1) % segmentsBakedPerFrame == 0) yield return null;
        }

        Debug.Log($"[NavSystem] Phase 1 done — {segBaked} NavMesh, {segFailed} linear fallback.");

        // ── Phase 2: Route pool ───────────────────────────────────────────────
        var allNodeIDs  = nodeMap.Keys.ToList();
        int routesBaked = 0, processed = 0;

        Debug.Log($"[NavSystem] ── Runtime Phase 2/2: Building route pool...");

        foreach (int srcID in allNodeIDs)
        {
            _routePool[srcID] = new List<RouteResult>();
            Vector3 srcPos    = nodeMap[srcID].worldPosition;

            var candidates = allNodeIDs
                .Where(id => id != srcID)
                .Select(id => (id, d: Vector3.Distance(srcPos, nodeMap[id].worldPosition)))
                .Where(t => t.d >= minDestinationDistance && t.d <= maxDestinationDistance)
                .Select(t => t.id)
                .ToList();

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
                List<Vector3> wps = GetDenseRoute(nodePath);
                if (wps == null || wps.Count < 2) continue;
                _routePool[srcID].Add(new RouteResult
                {
                    success = true, sourceNodeID = srcID,
                    destinationNodeID = destID, waypoints = wps,
                });
                routesBaked++;
            }

            // Fallback
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

        Debug.Log($"[NavSystem] ✅ Runtime bake done — {routesBaked} routes built, " +
                  $"{emptyNodes} nodes unreachable.");

        if (emptyNodes > 0)
            Debug.LogWarning($"[NavSystem] ⚠️  {emptyNodes} nodes have no routes. " +
                             "Check graph connectivity.");

        yield return new WaitForSeconds(0.3f);
        SpawnTrafficVehicles();
    }

    private IEnumerator SpawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnTrafficVehicles();
    }

    // =========================================================================
    //  EDITOR BAKING  (synchronous, progress bar)
    // =========================================================================

#if UNITY_EDITOR
    /// <summary>
    /// Called by the inspector "Bake and Save Segment Cache" button.
    /// Runs synchronously in the editor with a cancellable progress bar.
    /// Saves ALL NavMesh segments into routeCacheAsset (segments only).
    /// Routes are NOT saved — they are always computed fresh at runtime.
    /// </summary>
    public void EditorBakeSegmentCache()
    {
        if (routeCacheAsset == null)
        {
            EditorUtility.DisplayDialog(
                "No Cache Asset Assigned",
                "Please create and assign a NavRouteCacheAsset first.\n\n" +
                "Assets → Create → Navigation → Nav Route Cache Asset\n" +
                "Then drag it onto the 'Route Cache Asset' field on this component.",
                "OK");
            return;
        }

        ValidateAndRebuildGraph();

        if (nodes.Count < 2)
        {
            EditorUtility.DisplayDialog("Not Enough Nodes",
                "Need at least 2 nodes to bake segments.", "OK");
            return;
        }

        // Collect all segment pairs
        var segments = CollectSegmentPairs();

        if (segments.Count == 0)
        {
            EditorUtility.DisplayDialog("No Connections",
                "No connections found. Add connections between nodes first.", "OK");
            return;
        }

        // Confirm
        bool proceed = EditorUtility.DisplayDialog(
            "Bake Segment Cache",
            $"This will bake {segments.Count} NavMesh segments " +
            $"({connectionDefinitions.Count} connections) into '{routeCacheAsset.name}'.\n\n" +
            "Requirements:\n" +
            "  • NavMesh must be baked for this scene\n" +
            "  • Road geometry must be on a NavMesh-walkable layer\n\n" +
            "Segments only — routes are always computed fresh at runtime.\n\n" +
            "Continue?",
            "Bake", "Cancel");

        if (!proceed) return;

        // Clear asset
        Undo.RecordObject(routeCacheAsset, "Bake Segment Cache");
        routeCacheAsset.Clear();

        // Clear local segment cache so Phase 2 can use fresh data
        _segmentCache.Clear();

        int segBaked = 0, segFailed = 0;

        for (int i = 0; i < segments.Count; i++)
        {
            var (from, to) = segments[i];

            float progress = (float)i / segments.Count;
            bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                "Baking Segment Cache",
                $"Segment {i + 1}/{segments.Count}  ({from} → {to})",
                progress);

            if (cancelled)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogWarning("[NavSystem] Segment cache bake CANCELLED. " +
                                 "Asset not saved — re-bake to get a valid cache.");
                return;
            }

            Vector3[] wps = EditorBakeOneSegment(from, to);

            // Cache locally for any debug use
            _segmentCache[(from, to)] = wps;

            routeCacheAsset.segments.Add(new SerializedSegment
            {
                fromID    = from,
                toID      = to,
                waypoints = wps,
            });

            if (wps.Length > 0) segBaked++; else segFailed++;
        }

        EditorUtility.ClearProgressBar();

        // Write metadata
        routeCacheAsset.isValid              = segBaked > 0;
        routeCacheAsset.bakedAt              = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        routeCacheAsset.sceneName            = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        routeCacheAsset.nodeCount            = nodes.Count;
        routeCacheAsset.connectionCount      = connectionDefinitions.Count;
        routeCacheAsset.segmentCount         = routeCacheAsset.segments.Count;
        routeCacheAsset.bakedWaypointSpacing = maxWaypointSpacing;
        routeCacheAsset.bakedHeightOffset    = waypointHeightOffset;

        EditorUtility.SetDirty(routeCacheAsset);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();

        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(routeCacheAsset);
        Debug.Log($"[NavSystem] ✅ Segment cache bake complete — " +
                  $"{segBaked} NavMesh, {segFailed} linear-fallback | " +
                  $"Saved to: {assetPath}");

        EditorUtility.DisplayDialog(
            "Bake Complete ✅",
            $"Segment cache saved successfully!\n\n" +
            $"NavMesh segments:  {segBaked}\n" +
            $"Linear fallbacks:  {segFailed}\n" +
            $"Total segments:    {segBaked + segFailed}\n" +
            $"Baked at:          {routeCacheAsset.bakedAt}\n\n" +
            (segFailed > 0
                ? $"⚠️  {segFailed} segment(s) used linear fallback.\n" +
                  "Make sure those connections have road mesh baked into the NavMesh."
                : "All segments used NavMesh — perfect road following."),
            "OK");
    }

    /// <summary>
    /// Bake a single segment synchronously in the editor.
    /// Returns a non-null Vector3[] (may be empty on total failure).
    /// </summary>
    private Vector3[] EditorBakeOneSegment(int fromID, int toID)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID))
            return new Vector3[0];

        Vector3 fromPos = nodeMap[fromID].transform.position;
        Vector3 toPos   = nodeMap[toID].transform.position;

        var nmPath = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(fromPos, toPos, NavMesh.AllAreas, nmPath)
               && nmPath.status == NavMeshPathStatus.PathComplete
               && nmPath.corners.Length >= 2;

        if (ok) return SubdivideAndLift(nmPath.corners);

        Debug.LogWarning($"[NavSystem] NavMesh failed {fromID}→{toID} " +
                         $"(status={nmPath.status}). Using linear fallback.");
        return BuildLinearSegment(fromPos, toPos);
    }

    /// <summary>Clear the cache asset and mark it invalid.</summary>
    public void EditorClearSegmentCache()
    {
        if (routeCacheAsset == null) return;
        Undo.RecordObject(routeCacheAsset, "Clear Segment Cache");
        routeCacheAsset.Clear();
        EditorUtility.SetDirty(routeCacheAsset);
        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log("[NavSystem] Segment cache cleared and marked invalid.");
    }
#endif

    // =========================================================================
    //  SEGMENT BAKING HELPERS  (used by both editor and runtime fallback)
    // =========================================================================

    /// <summary>Returns all (from, to) pairs from connectionDefinitions.</summary>
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

    /// <summary>
    /// Bakes a single segment via NavMesh (or linear fallback) and stores in _segmentCache.
    /// Returns true if NavMesh succeeded.
    /// </summary>
    private bool BakeAndCacheSegment(int fromID, int toID)
    {
        Vector3 fromPos = nodeMap[fromID].transform.position;
        Vector3 toPos   = nodeMap[toID].transform.position;

        var  nmPath = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(fromPos, toPos, NavMesh.AllAreas, nmPath)
               && nmPath.status == NavMeshPathStatus.PathComplete
               && nmPath.corners.Length >= 2;

        Vector3[] wps = ok
            ? SubdivideAndLift(nmPath.corners)
            : BuildLinearSegment(fromPos, toPos);

        if (!ok)
            Debug.LogWarning($"[NavSystem] NavMesh failed {fromID}→{toID} " +
                             $"(status={nmPath.status}). Using linear fallback.");

        _segmentCache[(fromID, toID)] = wps;
        return ok;
    }

    private Vector3[] SubdivideAndLift(Vector3[] corners)
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < corners.Length - 1; i++)
        {
            Vector3 a = corners[i], b = corners[i + 1];
            pts.Add(LiftPoint(a));
            int divs = Mathf.FloorToInt(Vector3.Distance(a, b) / maxWaypointSpacing);
            for (int s = 1; s < divs; s++)
                pts.Add(LiftPoint(Vector3.Lerp(a, b, (float)s / divs)));
        }
        pts.Add(LiftPoint(corners[corners.Length - 1]));
        return pts.ToArray();
    }

    private Vector3[] BuildLinearSegment(Vector3 from, Vector3 to)
    {
        int divs = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(from, to) / maxWaypointSpacing));
        var pts  = new Vector3[divs + 1];
        for (int i = 0; i <= divs; i++)
            pts[i] = SnapToSurface(Vector3.Lerp(from, to, (float)i / divs));
        return pts;
    }

    private Vector3 LiftPoint(Vector3 v) => v + Vector3.up * waypointHeightOffset;

    private Vector3 SnapToSurface(Vector3 pos)
    {
        if (Physics.Raycast(pos + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, waypointSnapLayer))
            return hit.point + Vector3.up * waypointHeightOffset;
        return pos + Vector3.up * waypointHeightOffset;
    }

    // =========================================================================
    //  DENSE ROUTE STITCHING
    // =========================================================================

    /// <summary>
    /// Stitches pre-baked segments together into one continuous waypoint list.
    /// If a segment is missing, it is lazily baked on-demand via NavMesh (or linear fallback).
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
                // Lazy bake — happens only for segments not in asset (e.g. new node added)
                Debug.LogWarning($"[NavSystem] Segment {from}→{to} not in cache — lazy baking via NavMesh.");
                BakeAndCacheSegment(from, to);
                seg = _segmentCache[(from, to)];
            }

            if (seg.Length == 0) continue;

            // Skip first point of subsequent segments to avoid duplicates at junction
            int startIdx = (full.Count > 0) ? 1 : 0;
            for (int j = startIdx; j < seg.Length; j++)
                full.Add(seg[j]);
        }

        return full;
    }

    // =========================================================================
    //  ROUTE REQUEST API  (called by TrafficVehicle and CentralizedCarController)
    // =========================================================================

    /// <summary>
    /// Returns a pre-built route from the pool for the given source node.
    /// O(1) pool lookup — no A*, no NavMesh at runtime.
    /// Falls back to live computation if pool is empty.
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
            foreach (var candidate in shuffled)
            {
                var key = (candidate.sourceNodeID, candidate.destinationNodeID);
                _routeOccupancy.TryGetValue(key, out int occ);
                if (occ >= MAX_NPCS_PER_ROUTE) continue;
                _routeOccupancy[key] = occ + 1;
                return CloneResult(candidate);
            }

            // All routes at capacity — allow overflow rather than returning null
            var overflow = shuffled[0];
            var okey     = (overflow.sourceNodeID, overflow.destinationNodeID);
            _routeOccupancy.TryGetValue(okey, out int oocc);
            _routeOccupancy[okey] = oocc + 1;
            Debug.LogWarning($"[NavSystem] All routes from node {fromNodeID} at max occupancy. " +
                             "Allowing overflow NPC.");
            return CloneResult(overflow);
        }

        // Pool miss — compute live
        Debug.LogWarning($"[NavSystem] Route pool miss for node {fromNodeID}. " +
                         "Computing live (may cause brief hitch). " +
                         "Consider increasing routesPerSourceNode.");
        return ComputeRouteLive(fromNodeID);
    }

    /// <summary>
    /// Returns a pre-built or freshly computed route from srcNode to a specific destination.
    /// Used for rerouting after a stuck event.
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
        if (path != null && path.Count >= 2)
        {
            List<Vector3> wps = GetDenseRoute(path);
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

        return RequestRoute(fromNodeID);
    }

    /// <summary>
    /// Decrements occupancy count when an NPC finishes or abandons a route.
    /// Call from TrafficVehicle when switching routes.
    /// </summary>
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
            return Fail($"Node {fromNodeID} not found in nodeMap.");

        Vector3 srcPos     = nodeMap[fromNodeID].worldPosition;
        var     candidates = nodeMap.Keys
            .Where(id => id != fromNodeID)
            .OrderBy(_ => UnityEngine.Random.value)
            .ToList();

        // Try strict distance, then relaxed, then any
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

                // Cache in pool for future use
                if (!_routePool.ContainsKey(fromNodeID))
                    _routePool[fromNodeID] = new List<RouteResult>();
                _routePool[fromNodeID].Add(new RouteResult
                {
                    success = true, sourceNodeID = fromNodeID,
                    destinationNodeID = destID, waypoints = wps,
                });

                var key = (fromNodeID, destID);
                _routeOccupancy.TryGetValue(key, out int occ);
                _routeOccupancy[key] = occ + 1;

                return new RouteResult
                {
                    success = true, sourceNodeID = fromNodeID,
                    destinationNodeID = destID,
                    waypoints = new List<Vector3>(wps),
                };
            }
        }

        // Last resort
        int fb = FindAnyReachableNode(fromNodeID);
        if (fb != -1)
        {
            List<int> fp = FindPath(fromNodeID, fb);
            if (fp != null && fp.Count >= 2)
            {
                List<Vector3> fw = GetDenseRoute(fp);
                if (fw != null && fw.Count >= 2)
                {
                    var key = (fromNodeID, fb);
                    _routeOccupancy.TryGetValue(key, out int occ);
                    _routeOccupancy[key] = occ + 1;
                    return new RouteResult
                    {
                        success = true, sourceNodeID = fromNodeID,
                        destinationNodeID = fb, waypoints = fw,
                    };
                }
            }
        }

        return Fail($"No route found from node {fromNodeID}. Check graph connectivity.");
    }

    private static RouteResult CloneResult(RouteResult src) => new RouteResult
    {
        success           = src.success,
        sourceNodeID      = src.sourceNodeID,
        destinationNodeID = src.destinationNodeID,
        waypoints         = new List<Vector3>(src.waypoints),
    };

    private static RouteResult Fail(string reason)
    {
        Debug.LogWarning($"[NavSystem] Route failed: {reason}");
        return new RouteResult { success = false, failReason = reason };
    }

    // =========================================================================
    //  A* PATHFINDING
    // =========================================================================

    public List<int> FindPath(int start, int target)
    {
        if (!nodeMap.ContainsKey(start) || !nodeMap.ContainsKey(target))
        {
            Debug.LogWarning($"[NavSystem] FindPath: node {start} or {target} not in nodeMap.");
            return null;
        }
        if (start == target) return new List<int> { start };

        var cameFrom = new Dictionary<int, int>();
        var gScore   = new Dictionary<int, float> { [start] = 0f };
        var fScore   = new Dictionary<int, float> { [start] = Heuristic(start, target) };
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
    //  NODE QUERIES
    // =========================================================================

    public int GetClosestNode(Vector3 worldPosition)
    {
        float best = float.MaxValue; int id = -1;
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
                visited.Add(nb);
                if (nb != fromID) return nb;
                queue.Enqueue(nb);
            }
        }
        return -1;
    }

    // =========================================================================
    //  VEHICLE SPAWNING
    // =========================================================================

    private void SpawnTrafficVehicles()
    {
        ClearAllTraffic();

        if (nodes.Count < 2)
        {
            Debug.LogError("[Traffic] Need at least 2 nodes to spawn traffic!");
            return;
        }

        // Build prefab list
        var prefabs = new List<GameObject>();
        if (npcVehiclePrefab != null) prefabs.Add(npcVehiclePrefab);
        prefabs.AddRange(npcVariants.Where(v => v != null));

        if (prefabs.Count == 0)
        {
            Debug.LogError("[Traffic] No NPC vehicle prefabs assigned!");
            return;
        }

        VehicleGroundConfig groundCfg = BuildGroundConfig();
        VehicleSharedConfig sharedCfg = BuildSharedConfig();

        // Shuffle node order to vary spawn positions
        var nodeIDs = nodeMap.Keys.ToList();
        Shuffle(nodeIDs);

        var usedPositions = new List<Vector3>();
        int spawned       = 0;

        foreach (int nodeID in nodeIDs)
        {
            if (spawned >= totalTrafficVehicles) break;
            if (!nodeMap.ContainsKey(nodeID)) continue;

            Vector3 nodePos = nodeMap[nodeID].transform.position;

            // Enforce minimum gap between spawned vehicles
            if (usedPositions.Any(p => Vector3.Distance(nodePos, p) < vehicleSpacing)) continue;

            GameObject prefab   = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            Quaternion spawnRot = nodeMap[nodeID].transform.rotation;

            // Spawn below world temporarily for bound measurement
            var vehicleObj = Instantiate(prefab, new Vector3(nodePos.x, -5000f, nodePos.z), spawnRot);
            vehicleObj.name = $"NPC_Vehicle_{spawned:D3}";

            // Ground the vehicle properly
            float    bottomOffset = GetColliderBottomOffset(vehicleObj);
            Vector3  spawnPos     = new Vector3(nodePos.x, nodePos.y + bottomOffset + spawnHeightOffset, nodePos.z);

            // Configure Rigidbody — kinematic during setup, released next FixedUpdate
            Rigidbody rb = vehicleObj.GetComponent<Rigidbody>()
                        ?? vehicleObj.AddComponent<Rigidbody>();
            rb.mass             = 1200f;
            rb.linearDamping    = 1f;
            rb.angularDamping   = 10f;
            rb.interpolation    = RigidbodyInterpolation.Interpolate;
            rb.constraints      = RigidbodyConstraints.None;
            rb.isKinematic      = true;

            vehicleObj.transform.position = spawnPos;
            rb.position = spawnPos;
            rb.rotation = spawnRot;

            /*// Neutralise child Rigidbodies (truck cab, articulated joints, etc.)
            foreach (Rigidbody childRb in vehicleObj.GetComponentsInChildren<Rigidbody>())
            {
                if (childRb == rb) continue;
                childRb.useGravity     = false;
                childRb.linearDamping  = 20f;
                childRb.angularDamping = 20f;
                childRb.constraints    = RigidbodyConstraints.FreezeAll;
            }
            foreach (Joint joint in vehicleObj.GetComponentsInChildren<Joint>())
                joint.enabled = false;*/

            // Initialise TrafficVehicle — this is the ONLY component that moves NPCs
            TrafficVehicle tv = vehicleObj.GetComponent<TrafficVehicle>()
                             ?? vehicleObj.AddComponent<TrafficVehicle>();
            tv.Initialize(this, nodeID, groundCfg, sharedCfg);

            StartCoroutine(ReleaseKinematicAfterSetup(rb));

            activeVehicles.Add(tv);
            usedPositions.Add(spawnPos);
            spawned++;
        }

        Debug.Log($"[Traffic] ══ {spawned}/{totalTrafficVehicles} NPC vehicles spawned ══");
    }

    private float GetColliderBottomOffset(GameObject obj)
    {
        float lowest = 0f; bool found = false;
        foreach (Collider col in obj.GetComponentsInChildren<Collider>(true))
        {
            if (col.isTrigger) continue;
            float lb = col.bounds.min.y - obj.transform.position.y;
            if (!found || lb < lowest) { lowest = lb; found = true; }
        }
        return found ? Mathf.Abs(lowest) : 0f;
    }

    private IEnumerator ReleaseKinematicAfterSetup(Rigidbody rb)
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
                    if (rl < 0) Debug.LogWarning("[NavSystem] No 'Road' layer found — using Default.");
                    break;
                case GroundSurfaceType.Terrain:
                    resolved = LayerMask.GetMask("Terrain");
                    break;
                case GroundSurfaceType.RoadAndTerrain:
                    int roadLayer = LayerMask.NameToLayer("Road");
                    LayerMask terrain = LayerMask.GetMask("Terrain");
                    resolved = roadLayer >= 0 ? ((1 << roadLayer) | terrain) : terrain;
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

    private VehicleSharedConfig BuildSharedConfig() => new VehicleSharedConfig
    {
        speed                  = vehicleSpeed,
        turnSpeed              = vehicleTurnSpeed,
        speedSmoothTime        = vehicleSpeedSmoothTime,
        minPathLength          = minPathLength,
        maxPathLength          = maxPathLength,
        minDestinationDistance = minDestinationDistance,
        maxDestinationDistance = maxDestinationDistance,
        maxPathAttempts        = maxPathAttempts,
        waypointReachDistanceXZ = waypointReachDistanceXZ,
        waypointReachDistanceY  = waypointReachDistanceY,
        minAdvanceInterval     = minAdvanceInterval,
        detectionLayerMask     = detectionLayerMask,
        npcVehicleLayer        = npcVehicleLayer,
        playerVehicleLayer     = playerVehicleLayer,
        trafficLightLayer      = trafficLightLayer,
        detectionRange         = detectionRange,
        vehicleStoppingDistance  = vehicleStoppingDistance,
        obstacleStoppingDistance = obstacleStoppingDistance,
        trafficLightStopDistance = trafficLightStopDistance,
        maxRedLightWaitTime    = maxRedLightWaitTime,
        maxStuckFrames         = maxStuckFrames,
        stuckMovementThreshold = stuckMovementThreshold,
        maxPathRecalculations  = maxPathRecalculations,
        showDebugGizmos        = showDebugGizmos,
    };

    // =========================================================================
    //  TRAFFIC MANAGEMENT (public API)
    // =========================================================================

    [ContextMenu("Spawn Traffic Now")]
    public void SpawnTrafficNow() => SpawnTrafficVehicles();

    [ContextMenu("Clear All Traffic")]
    public void ClearAllTraffic()
    {
        foreach (var v in activeVehicles)
            if (v != null) Destroy(v.gameObject);
        activeVehicles.Clear();
    }

    [ContextMenu("Respawn Traffic")]
    public void RespawnTraffic() { ClearAllTraffic(); SpawnTrafficVehicles(); }

    // =========================================================================
    //  PATH VISUALIZATION  (used by CentralizedCarController for player car)
    // =========================================================================

    private void SetupLineRenderer()
    {
        if (pathLineRenderer != null) return;
        var obj = new GameObject("PathVisualizer");
        obj.transform.SetParent(transform);
        pathLineRenderer = obj.AddComponent<LineRenderer>();
        pathLineRenderer.material   = new Material(Shader.Find("Sprites/Default"));
        pathLineRenderer.startWidth = 0.2f;
        pathLineRenderer.endWidth   = 0.2f;
        var grad = new Gradient();
        grad.colorKeys = new[]
        {
            new GradientColorKey(Color.yellow, 0f),
            new GradientColorKey(Color.red,    1f),
        };
        pathLineRenderer.colorGradient = grad;
        pathLineRenderer.enabled = false;
    }

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

    /// <summary>
    /// Updates the line renderer to show the player's remaining route,
    /// trimmed from the car's current projected position on the path.
    /// Called by CentralizedCarController every frame — no Rigidbody interaction.
    /// </summary>
    public void VisualizePlayerPath(List<int> nodePath, Vector3 playerWorldPos)
    {
        if (nodePath == null || nodePath.Count == 0) { ClearPathVisualization(); return; }
        SetupLineRenderer();

        // Rebuild dense route only when the node path changes
        bool pathChanged = _playerCachedNodePath == null
                        || _playerCachedNodePath.Count != nodePath.Count;
        if (!pathChanged)
        {
            for (int i = 0; i < nodePath.Count; i++)
                if (_playerCachedNodePath[i] != nodePath[i]) { pathChanged = true; break; }
        }

        if (pathChanged)
        {
            _playerCachedDenseRoute = RouteCacheReady
                ? GetDenseRoute(nodePath)
                : BuildNodePositionList(nodePath);

            if (_playerCachedDenseRoute == null || _playerCachedDenseRoute.Count < 2)
                _playerCachedDenseRoute = BuildNodePositionList(nodePath);

            _playerCachedNodePath = new List<int>(nodePath);
        }

        if (_playerCachedDenseRoute == null || _playerCachedDenseRoute.Count < 2)
        { ClearPathVisualization(); return; }

        // Find closest segment to player (XZ only — ignore height difference on hills)
        int   closestSeg  = 0;
        float closestDistSq = float.MaxValue;
        float closestT    = 0f;

        for (int i = 0; i < _playerCachedDenseRoute.Count - 1; i++)
        {
            Vector3 a = _playerCachedDenseRoute[i];
            Vector3 b = _playerCachedDenseRoute[i + 1];
            Vector3 ab = b - a;
            float len  = ab.sqrMagnitude;
            float t    = len > 0.0001f
                ? Mathf.Clamp01(Vector3.Dot(playerWorldPos - a, ab) / len)
                : 0f;
            Vector3 proj = a + ab * t;
            float dxz    = new Vector2(playerWorldPos.x - proj.x,
                                       playerWorldPos.z - proj.z).sqrMagnitude;
            if (dxz < closestDistSq) { closestDistSq = dxz; closestSeg = i; closestT = t; }
        }

        // Advance past segment if very close to end
        int   startSeg = closestSeg;
        float startT   = closestT;
        if (startT >= 0.9999f && startSeg + 1 < _playerCachedDenseRoute.Count - 1)
        { startSeg++; startT = 0f; }

        // Build trimmed waypoint list starting from projected position
        var trimmed = new List<Vector3>();
        Vector3 segA  = _playerCachedDenseRoute[startSeg];
        Vector3 segB  = (startSeg + 1 < _playerCachedDenseRoute.Count)
                      ? _playerCachedDenseRoute[startSeg + 1] : segA;
        Vector3 proj0 = Vector3.Lerp(segA, segB, startT);
        proj0.y += pathLineHeightOffset;
        trimmed.Add(proj0);

        for (int i = startSeg + 1; i < _playerCachedDenseRoute.Count; i++)
        {
            Vector3 p = _playerCachedDenseRoute[i];
            p.y += pathLineHeightOffset;
            trimmed.Add(p);
        }

        if (trimmed.Count < 2) { ClearPathVisualization(); return; }

        pathLineRenderer.positionCount = trimmed.Count;
        pathLineRenderer.SetPositions(trimmed.ToArray());
        pathLineRenderer.enabled = true;
    }

    public void InvalidatePlayerPathCache()
    {
        _playerCachedNodePath   = null;
        _playerCachedDenseRoute = null;
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

    private List<Vector3> BuildNodePositionList(List<int> nodePath)
    {
        var pts = new List<Vector3>();
        foreach (int id in nodePath)
            if (nodeMap.ContainsKey(id)) pts.Add(nodeMap[id].worldPosition);
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
        foreach (var node in nodes)
            if (node != null && node.nodeID >= nextNodeID) nextNodeID = node.nodeID + 1;

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
            .Where(c => c != null
                     && nodeMap.ContainsKey(c.fromNodeID)
                     && nodeMap.ContainsKey(c.toNodeID))
            .ToList();
    }

    public void RefreshGraph() => ValidateAndRebuildGraph();

    public void RegisterNode(NavNode node)
    {
        if (node == null) return;
        if (nodes.Contains(node))
        {
            if (!nodeMap.ContainsKey(node.nodeID)) nodeMap[node.nodeID] = node;
            node.parentNavSystem = this;
            return;
        }
        if (node.nodeID < 0 || nodeMap.ContainsKey(node.nodeID)) node.nodeID = nextNodeID++;
        else if (node.nodeID >= nextNodeID) nextNodeID = node.nodeID + 1;
        node.parentNavSystem = this;
        nodes.Add(node);
        nodeMap[node.nodeID] = node;
    }

    public void AddConnectionDefinition(int fromID, int toID, bool bidirectional)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID)) return;
        AddConnection(fromID, toID, bidirectional);
        ValidateConnections();
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

        var nodeObj = new GameObject($"NavNode_{finalID}");
        nodeObj.transform.SetParent(nodesParent.transform);
        nodeObj.transform.position = position;
        nodeObj.transform.rotation = rotation ?? Quaternion.identity;

        NavNode node = nodeObj.AddComponent<NavNode>();
        node.parentNavSystem = this;
        node.nodeID          = finalID;
        nodes.Add(node);
        nodeMap[finalID] = node;

#if UNITY_EDITOR
        if (autoSnapNewNodes && !Application.isPlaying) SnapNodeToGround(node);
#endif
        return node;
    }

    // =========================================================================
    //  UTILITY
    // =========================================================================

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[r]) = (list[r], list[i]);
        }
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
                Gizmos.color = new Color(0f, 1f, 1f, 1f);
                Gizmos.DrawSphere(node.transform.position, 0.6f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    node.transform.position + Vector3.up * 1.3f,
                    $"Node {node.nodeID}",
                    new GUIStyle
                    {
                        normal    = new GUIStyleState { textColor = Color.white },
                        fontSize  = 13,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                    });
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
                Gizmos.color = conn.bidirectional
                    ? new Color(0f, 1f, 0f, 0.8f)
                    : new Color(1f, 0.5f, 0f, 0.8f);
                Gizmos.DrawLine(s, e);

                if (!conn.bidirectional)
                {
                    Vector3 dir  = (e - s).normalized;
                    Vector3 mid  = s + dir * (Vector3.Distance(s, e) * 0.5f);
                    Vector3 perp = Vector3.Cross(Vector3.up, dir) * 0.5f;
                    Gizmos.DrawLine(mid, mid - dir + perp);
                    Gizmos.DrawLine(mid, mid - dir - perp);
                }
            }
        }

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
            if (visualizeAllConnectionsEditor) DrawAllConnectionsIntoLineRenderer();
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
        foreach (var n in all)
            if (n != null && n.nodeID >= nextNodeID) nextNodeID = n.nodeID + 1;
        foreach (var n in all)
        {
            if (n == null) continue;
            if (n.nodeID < 0 || nodeMap.ContainsKey(n.nodeID)) n.nodeID = nextNodeID++;
            n.parentNavSystem = this;
            nodes.Add(n);
            nodeMap[n.nodeID] = n;
        }
        ValidateConnections();
    }

    public bool SnapNodeToGround(NavNode node)
    {
        if (node == null) return false;
        Vector3 origin = node.transform.position + Vector3.up * snapRaycastOriginHeight;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                            snapRaycastOriginHeight + 500f, snapLayer))
        {
            Undo.RecordObject(node.transform, "Snap Node To Ground");
            node.transform.position = hit.point + Vector3.up * snapNodeHeightOffset;
            if (snapAlignToSurface)
                node.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            EditorUtility.SetDirty(node.gameObject);
            return true;
        }
        Debug.LogWarning($"[NavSystem] Snap raycast missed Node {node.nodeID} — " +
                         "check snapLayer and snapRaycastOriginHeight.");
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
    }

    [ContextMenu("Clear All Connections")]
    public void ClearAllConnections() => connectionDefinitions.Clear();

    [ContextMenu("Create Node Forward")]
    public void CreateNodeForward()
    {
        NavNode last = nodes.Count > 0 ? nodes[nodes.Count - 1] : null;
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
        if (sel == null || sel.parentNavSystem != this)
        { Debug.LogWarning("[NavSystem] No NavNode selected."); return; }
        NavNode n = CreateNode(
            sel.transform.position + sel.transform.forward * newNodeDistance,
            -1, sel.transform.rotation);
        AddConnectionDefinition(sel.nodeID, n.nodeID, true);
        Selection.activeGameObject = n.gameObject;
    }

    [ContextMenu("Setup Demo")]
    public void SetupDemo()
    {
        ClearAllConnections(); nodes.Clear(); nodeMap.Clear(); nextNodeID = 0;
        if (nodesParent == null)
        { nodesParent = new GameObject("NavigationNodes"); nodesParent.transform.SetParent(transform); }
        Vector3[] pos =
        {
            new Vector3(0,0.5f,0),  new Vector3(10,0.5f,0),
            new Vector3(15,0.5f,10), new Vector3(10,0.5f,20),
            new Vector3(0,0.5f,20), new Vector3(-10,0.5f,10),
        };
        foreach (var p in pos) CreateNode(p);
        var ids = nodes.Select(n => n.nodeID).ToList();
        for (int i = 0; i < ids.Count - 1; i++) AddConnectionDefinition(ids[i], ids[i + 1], true);
        AddConnectionDefinition(ids[ids.Count - 1], ids[0], true);
        ValidateAndRebuildGraph();
    }

    [ContextMenu("Test Path Zero To Last")]
    public void TestPathZeroToLast()
    {
        if (nodes.Count < 2) return;
        var path = FindPath(nodes[0].nodeID, nodes[nodes.Count - 1].nodeID);
        if (path != null && path.Count > 0)
        { Debug.Log($"[NavSystem] Test path: {string.Join(" → ", path)}"); VisualizePath(path); }
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
            Debug.Log($"  {c.fromNodeID}({fn}) {(c.bidirectional ? "↔" : "→")} {c.toNodeID}({tn})");
        }
    }

    [ContextMenu("Debug Print Segment Cache")]
    public void DebugPrintSegmentCache()
    {
        Debug.Log($"[NavSystem] ══ Segment Cache ({_segmentCache.Count}) ══");
        int total = 0;
        foreach (var kvp in _segmentCache)
        {
            Debug.Log($"  {kvp.Key.Item1}→{kvp.Key.Item2}: {kvp.Value.Length} waypoints");
            total += kvp.Value.Length;
        }
        Debug.Log($"  Total waypoints in memory: {total}");
    }

    [ContextMenu("Debug Print Route Pool")]
    public void DebugPrintRoutePool()
    {
        Debug.Log($"[NavSystem] ══ Route Pool ({_routePool.Count} source nodes) ══");
        int total = 0;
        foreach (var kvp in _routePool)
        { Debug.Log($"  Node {kvp.Key}: {kvp.Value.Count} routes"); total += kvp.Value.Count; }
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
    //  PRIORITY QUEUE  (A* helper)
    // =========================================================================

    public class PriorityQueue<T>
    {
        private readonly List<(T item, float priority)> _heap = new List<(T, float)>();
        public int Count => _heap.Count;

        public void Enqueue(T item, float priority)
        {
            _heap.Add((item, priority));
            int i = _heap.Count - 1;
            while (i > 0 && _heap[(i - 1) / 2].priority > _heap[i].priority)
            {
                int parent = (i - 1) / 2;
                (_heap[parent], _heap[i]) = (_heap[i], _heap[parent]);
                i = parent;
            }
        }

        public T Dequeue()
        {
            T root = _heap[0].item;
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, smallest = i;
                if (l < _heap.Count && _heap[l].priority < _heap[smallest].priority) smallest = l;
                if (r < _heap.Count && _heap[r].priority < _heap[smallest].priority) smallest = r;
                if (smallest == i) break;
                (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                i = smallest;
            }
            return root;
        }

        public bool Contains(T item) => _heap.Any(e => EqualityComparer<T>.Default.Equals(e.item, item));
    }
}

// =============================================================================
//  LEGACY TRAFFIC CHAIN  (kept for backwards compatibility)
// =============================================================================

[System.Serializable]
public class TrafficWaypointChain
{
    public string         chainName       = "Traffic_Chain";
    public List<Transform> waypoints      = new List<Transform>();
    public List<int>       nodeIDs        = new List<int>();
    public bool            loop           = false;
    [Range(0.5f, 3f)]
    public float           speedMultiplier = 1f;
}