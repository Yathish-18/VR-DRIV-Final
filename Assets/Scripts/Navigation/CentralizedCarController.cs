// ============================================================================
//  CENTRALIZED CAR CONTROLLER  v4.3
//  ============================================================================
//  Does NOT touch Rigidbody / move the car / handle input.
//  Draws the player's planned route as a LineRenderer.
//
//  ROUTING ALGORITHM:
//  ─────────────────────────────────────────────────────────────────────────
//  On reroute:
//    1. Scan every node within nodeSearchRadius.
//    2. Score each node: nmDist × dirBias + graphDist × 0.25
//       Nodes ahead get 0.7× bias, behind get 1.3×. Forward preferred but
//       backward nodes are still considered if they're the only valid path.
//    3. Pick lowest-score node that has a valid FindPath → dest.
//    4. Build: [carPos] ──NavMesh bridge──► [srcNode] ──graph segments──► [dest]
//
//  REROUTE TRIGGER:
//    Car > offPathRerouteDistance from every remaining waypoint → reroute.
//
//  LINE DRAWING:
//    Forward segments always preferred.
//    Backward on bidirectional road: allowed if no forward exists.
//    Backward on one-way road: NEVER drawn — line hidden, reroute fires.
//
//  WAYPOINT GROUNDING:
//    Every drawn point is pinned to the real road surface via downward
//    physics raycasts (5-point cross pattern, median Y). Does NOT use
//    NavMesh.SamplePosition for height — NavMesh bake can float above road.
// ============================================================================

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class CentralizedCarController : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    //  INSPECTOR
    // ─────────────────────────────────────────────────────────────────────────

    [Header("═══  NAVIGATION SYSTEM  ═══")]
    public CentralizedNavigationSystem navSystem;

    [Header("═══  FIXED DESTINATION  ═══")]
    [Tooltip("Drag a NavNode here. Route always leads to this node.")]
    public NavNode fixedDestinationNode = null;
    private int FixedDestNodeID => fixedDestinationNode != null ? fixedDestinationNode.nodeID : -1;

    [Header("═══  ROUTE VISUALIZATION  ═══")]
    public bool showRouteVisualization = true;

    [Tooltip("Seconds between deviation checks.")]
    [Range(0.1f, 3f)]
    public float routeRefreshInterval = 0.5f;

    [Tooltip("Reroute when car is further than this from any remaining waypoint.")]
    [Range(5f, 80f)]
    public float offPathRerouteDistance = 18f;

    [Tooltip("Max metres between consecutive drawn line points (smoothness).")]
    [Range(1f, 20f)]
    public float maxLineSegmentLength = 4f;

    [Header("═══  NAVMESH  ═══")]
    public int navMeshAreaMask = NavMesh.AllAreas;

    [Header("═══  WAYPOINT GROUNDING  ═══")]
    [Tooltip("Layer mask for road/terrain surface. Set to your Road + Terrain layers.\n" +
             "CRITICAL: exclude buildings, vehicles, props.\n" +
             "If left as 'Everything', raycasts may hit wrong surfaces.")]
    public LayerMask roadSurfaceLayer = ~0;

    [Tooltip("How far above car's Y position to start the downward surface raycast.\n" +
             "30 m default handles most bridges and overpasses.")]
    [Range(5f, 80f)]
    public float groundRaycastOriginHeight = 30f;

    [Tooltip("Total downward cast distance from origin. 60 m default.\n" +
             "Increase for deep valleys on mountain roads.")]
    [Range(10f, 150f)]
    public float groundRaycastTotalLength = 60f;

    [Tooltip("Raycast hits further than this from car's Y are rejected.\n" +
             "Prevents snapping to rooftops or tunnels below.\n" +
             "25 m = city hills. 80–100 m = mountain roads.")]
    [Range(5f, 200f)]
    public float maxHeightDeviation = 25f;

    [Tooltip("Line renderer sits this far above the road surface.\n" +
             "0.15 m keeps it visible without clipping.")]
    [Range(0f, 1f)]
    public float lineHeightAboveRoad = 0.15f;

    [Header("═══  NODE SELECTION  ═══")]
    [Tooltip("Search radius for candidate nodes when rerouting.")]
    [Range(10f, 300f)]
    public float nodeSearchRadius = 120f;

    [Tooltip("Reject nodes where NavMesh road distance > straight dist × this.\n" +
             "Filters out nodes on the other side of a road divider.")]
    [Range(1.2f, 6f)]
    public float nodePathRatioLimit = 3f;

    [Tooltip("NavMesh area mask for node reachability checks.")]
    public int nodeSelectionAreaMask = NavMesh.AllAreas;

    [Header("═══  DEBUG (read-only)  ═══")]
    [SerializeField] private float dbgNearestWp = 0f;
    [SerializeField] private int dbgWpIndex = 0;
    [SerializeField] private int dbgWpTotal = 0;
    [SerializeField] private bool dbgRerouted = false;
    [SerializeField] private string dbgStatus = "—";
    [SerializeField] private string dbgRouteSrc = "—";
    [SerializeField] private int dbgSrcNode = -1;

    [Header("═══  TEST FOLLOW PATH  ═══")]
    public bool followPath = false;
    [Range(1f, 30f)] public float testFollowSpeed = 10f;
    [Range(1f, 10f)] public float testTurnSpeed = 4f;
    [Range(1f, 10f)] public float testWaypointReachDistance = 4f;

    [Header("═══  BRAKE REACTION  ═══")]
    public LayerMask obstacleDetectionLayer;
    [Range(5f, 60f)] public float reactionRayDistance = 20f;

    // ─────────────────────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────────────────────

    private List<Vector3> _wps = new List<Vector3>();
    private int _wpIdx = 0;
    private int _srcNode = -1;
    private int _dstNode = -1;
    private float _timer = 0f;
    private bool _built = false;

    // Segment indices in _wps where _wps[i]→_wps[i+1] is a ONE-WAY connection.
    // Built by MarkOneWaySegments() after every route build/reroute.
    // DrawRoute uses this to decide whether backward drawing is allowed.
    private HashSet<int> _oneWaySegIdx = new HashSet<int>();

    private LineRenderer _lr = null;
    private NavMeshPath _nmWork = null;
    private NavMeshPath _nmSel = null;
    private List<Vector3> _drawBuf = new List<Vector3>();
    private bool _prevViz = true;
    private Rigidbody _rb;

    // 5-point cross pattern for GroundPoint multi-sample (static, no alloc)
    private static readonly Vector2[] _groundOffsets =
    {
        Vector2.zero,
        new Vector2( 0.3f,  0f),
        new Vector2(-0.3f,  0f),
        new Vector2( 0f,    0.3f),
        new Vector2( 0f,   -0.3f),
    };
    private readonly List<float> _groundHits = new List<float>(5);

    // Brake reaction
    private bool _obsLast = false;
    private float _obsSeen = -1f;
    private bool _reacted = false;
    private float _lastRt = -1f;
    private float _rtSum = 0f;
    private int _rtCount = 0;
    private float _rtWorst = -1f;

    // ─────────────────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────────────────

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
                Debug.LogError($"[CarCtrl] {name}: CentralizedNavigationSystem not found.");
                enabled = false; return;
            }
        }

        _rb = GetComponent<Rigidbody>();
        _nmWork = new NavMeshPath();
        _nmSel = new NavMeshPath();
        _lr = navSystem.pathLineRenderer;
        _prevViz = showRouteVisualization;
    }

    private void Update()
    {
        if (navSystem == null) return;
        _timer += Time.deltaTime;

        if (!_built)
        {
            if (!navSystem.RouteCacheReady) return;
            BuildRoute();
            _timer = 0f;
        }
        else if (_timer >= routeRefreshInterval)
        {
            _timer = 0f;
            CheckAndReroute();
        }

        if (showRouteVisualization) { DrawRoute(); _prevViz = true; }
        else if (_prevViz) { navSystem.ClearPathVisualization(); _prevViz = false; }

        if (followPath && _wps.Count > 0) TestFollowPath();
        MeasureBrakeReaction();
        dbgWpIndex = _wpIdx;
        dbgWpTotal = _wps.Count;
    }

    private void OnDisable() { navSystem?.ClearPathVisualization(); }

    // ─────────────────────────────────────────────────────────────────────────
    //  BUILD INITIAL ROUTE
    // ─────────────────────────────────────────────────────────────────────────

    private void BuildRoute()
    {
        int dest = FixedDestNodeID;
        if (dest >= 0)
        {
            _wps = MakeHybridRoute(transform.position, dest, out _srcNode);
            _dstNode = dest;
            dbgRouteSrc = "hybrid-init";
        }
        else
        {
            // No fixed dest — pick a route from the pool
            int src = BestSrcNode(-1);
            if (src < 0) src = navSystem.GetClosestNode(transform.position);
            if (src < 0) return;
            var res = navSystem.RequestRoute(src);
            if (!res.success) return;
            _srcNode = res.sourceNodeID;
            _dstNode = res.destinationNodeID;
            _wps = MakeHybridRoute(transform.position, _dstNode, out int actualSrc);
            if (_wps.Count < 2) _wps = res.waypoints ?? new List<Vector3>();
            if (actualSrc >= 0) _srcNode = actualSrc;
            dbgRouteSrc = "pool-hybrid";
        }

        _wpIdx = 0;
        _built = _wps.Count >= 2;
        dbgSrcNode = _srcNode;
        navSystem.InvalidatePlayerPathCache();
        if (_built) MarkOneWaySegments(navSystem.FindPath(_srcNode, _dstNode));

        Debug.Log(_built
            ? $"[CarCtrl] Route built {_wps.Count} wps | {dbgRouteSrc} | {_srcNode}→{_dstNode}"
            : "[CarCtrl] BuildRoute FAILED — no waypoints.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CHECK AND REROUTE
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckAndReroute()
    {
        if (_wps.Count == 0) return;
        TrimPassedWaypoints();

        float minSq = float.MaxValue;
        Vector3 pos = transform.position;
        int end = Mathf.Min(_wpIdx + 80, _wps.Count);

        for (int i = _wpIdx; i < end; i++)
        {
            Vector3 d = _wps[i] - pos; d.y = 0f;
            float sq = d.sqrMagnitude;
            if (sq < minSq) minSq = sq;
        }

        dbgNearestWp = Mathf.Sqrt(minSq);
        dbgRerouted = false;

        if (dbgNearestWp > offPathRerouteDistance)
        {
            dbgRerouted = true;
            dbgStatus = $"REROUTING dist={dbgNearestWp:F1}m";
            Reroute();
        }
        else
        {
            dbgStatus = $"on-route nearWp={dbgNearestWp:F1}m";
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  REROUTE
    // ─────────────────────────────────────────────────────────────────────────

    private void Reroute()
    {
        if (_srcNode != -1 && _dstNode != -1)
            navSystem.ReleaseRoute(_srcNode, _dstNode);

        int dest = FixedDestNodeID >= 0 ? FixedDestNodeID : _dstNode;
        if (dest < 0 || !navSystem.nodeMap.ContainsKey(dest))
        {
            Debug.LogWarning("[CarCtrl] Reroute: no valid destination."); return;
        }

        List<Vector3> newWps = MakeHybridRoute(transform.position, dest, out int srcUsed);
        if (newWps.Count < 2)
        {
            Debug.LogWarning("[CarCtrl] Reroute failed — keeping old route."); return;
        }

        _srcNode = srcUsed;
        _dstNode = dest;
        _wps = newWps;
        _wpIdx = 0;
        dbgSrcNode = _srcNode;
        navSystem.InvalidatePlayerPathCache();
        MarkOneWaySegments(navSystem.FindPath(_srcNode, _dstNode));

        Debug.Log($"[CarCtrl] ✅ Rerouted {_wps.Count} wps | {dbgRouteSrc} | src={_srcNode}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MAKE HYBRID ROUTE
    //
    //  Builds: [carPos] ──NavMesh bridge──► [srcNode] ──graph segments──► [dest]
    //
    //  Graph = FindPath (A*, respects one-way connections)
    //  NavMesh bridge = CalculatePath from car to first graph node
    //    (road geometry, handles car being mid-segment between sparse nodes)
    //
    //  Fallback: pure NavMesh direct path (bidirectional, may ignore one-way)
    // ─────────────────────────────────────────────────────────────────────────

    private List<Vector3> MakeHybridRoute(Vector3 carPos, int destNodeID, out int srcNodeUsed)
    {
        srcNodeUsed = -1;
        if (!navSystem.nodeMap.ContainsKey(destNodeID)) return new List<Vector3>();

        // Step 1: find best source node (forward-biased, graph-path-validated)
        int src = BestSrcNode(destNodeID);
        if (src < 0) src = BestSrcNodeAny(destNodeID);   // fallback: any valid node

        if (src >= 0)
        {
            List<int> graphNodes = navSystem.FindPath(src, destNodeID);
            List<Vector3> graphWps = (graphNodes != null && graphNodes.Count >= 2)
                                           ? navSystem.GetDenseRoute(graphNodes)
                                           : null;

            if (graphWps != null && graphWps.Count >= 2)
            {
                srcNodeUsed = src;

                var route = new List<Vector3>(graphWps.Count + 40);
                route.Add(carPos);

                // Step 2: NavMesh bridge from car to first graph waypoint
                float gapDist = Vector3.Distance(carPos, graphWps[0]);
                if (gapDist > 1.5f)
                {
                    Vector3 snapCar = Snap(carPos, navMeshAreaMask, 8f);
                    Vector3 snapGrph = Snap(graphWps[0], navMeshAreaMask, 8f);
                    bool ok = NavMesh.CalculatePath(snapCar, snapGrph, navMeshAreaMask, _nmWork)
                              && _nmWork.status != NavMeshPathStatus.PathInvalid
                              && _nmWork.corners.Length >= 2;
                    if (ok)
                    {
                        for (int i = 1; i < _nmWork.corners.Length; i++)
                            AddSubdivided(route, _nmWork.corners[i - 1], _nmWork.corners[i]);
                    }
                }

                // Step 3: append graph waypoints
                int gStart = (route.Count > 1 &&
                              Vector3.Distance(route[route.Count - 1], graphWps[0]) < 2.5f)
                              ? 1 : 0;
                for (int i = gStart; i < graphWps.Count; i++)
                    route.Add(graphWps[i]);

                dbgRouteSrc = $"hybrid src={src}";
                return route;
            }
        }

        // Fallback: pure NavMesh (no one-way guarantee, better than nothing)
        Debug.LogWarning($"[CarCtrl] Graph path failed to dest={destNodeID}. " +
                          "Using pure NavMesh fallback (may ignore one-way roads).");

        Vector3 destPos = navSystem.nodeMap[destNodeID].transform.position;
        Vector3 snapCarF = Snap(carPos, navMeshAreaMask, 8f);
        Vector3 snapDstF = Snap(destPos, navMeshAreaMask, 8f);

        bool fbOk = NavMesh.CalculatePath(snapCarF, snapDstF, navMeshAreaMask, _nmWork)
                    && _nmWork.status != NavMeshPathStatus.PathInvalid
                    && _nmWork.corners.Length >= 2;

        if (fbOk)
        {
            var fb = new List<Vector3>(_nmWork.corners.Length * 4);
            fb.Add(carPos);
            for (int i = 1; i < _nmWork.corners.Length; i++)
                AddSubdivided(fb, _nmWork.corners[i - 1], _nmWork.corners[i]);
            dbgRouteSrc = "navmesh-fallback";
            return fb;
        }

        dbgRouteSrc = "FAILED";
        return new List<Vector3>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  BEST SOURCE NODE  (forward-biased scorer)
    //
    //  score = nmDist × dirBias + graphDist × 0.25
    //  dirBias: ahead=0.7×, behind=1.3× — forward preferred, backward possible
    //  Only nodes with valid FindPath(node→dest) are considered.
    //  destNodeID = -1 skips graph check (used for random-destination init).
    // ─────────────────────────────────────────────────────────────────────────

    private int BestSrcNode(int destNodeID)
    {
        if (navSystem?.nodeMap == null) return -1;

        Vector3 carPos = transform.position;
        Vector3 carFwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
        Vector3 snapCar = Snap(carPos, nodeSelectionAreaMask, 6f);

        int best = -1;
        float bestScore = float.MaxValue;

        foreach (var kvp in navSystem.nodeMap)
        {
            if (kvp.Value == null) continue;

            Vector3 nodePos = kvp.Value.transform.position;
            Vector3 toNode = nodePos - carPos; toNode.y = 0f;
            float sd = toNode.magnitude;
            if (sd > nodeSearchRadius) continue;

            // NavMesh reachability + ratio filter
            Vector3 snapNode = Snap(nodePos, nodeSelectionAreaMask, 6f);
            bool nmOk = NavMesh.CalculatePath(snapCar, snapNode, nodeSelectionAreaMask, _nmSel)
                        && _nmSel.status != NavMeshPathStatus.PathInvalid;
            if (!nmOk) continue;

            float nmDist = GetPathLen(_nmSel);
            if (sd > 1f && nmDist > sd * nodePathRatioLimit) continue;

            // Graph path check + distance estimation
            float graphDist = 0f;
            if (destNodeID >= 0)
            {
                List<int> gp = navSystem.FindPath(kvp.Key, destNodeID);
                if (gp == null || gp.Count < 2) continue;
                for (int i = 0; i < gp.Count - 1; i++)
                {
                    if (navSystem.nodeMap.ContainsKey(gp[i]) && navSystem.nodeMap.ContainsKey(gp[i + 1]))
                        graphDist += Vector3.Distance(navSystem.nodeMap[gp[i]].worldPosition,
                                                      navSystem.nodeMap[gp[i + 1]].worldPosition);
                }
            }

            // Direction bias
            float dot = sd > 1f ? Vector3.Dot(carFwd, toNode / sd) : 1f;
            float dirBias = dot >= 0f ? Mathf.Lerp(1f, 0.7f, dot)
                                      : Mathf.Lerp(1f, 1.3f, -dot);

            float score = nmDist * dirBias + graphDist * 0.25f;
            if (score < bestScore) { bestScore = score; best = kvp.Key; }
        }

        return best;
    }

    // Fallback: no direction bias — pure shortest graph+navmesh cost.
    private int BestSrcNodeAny(int destNodeID)
    {
        if (navSystem?.nodeMap == null) return -1;

        Vector3 snapCar = Snap(transform.position, nodeSelectionAreaMask, 6f);
        int best = -1;
        float bestLen = float.MaxValue;

        foreach (var kvp in navSystem.nodeMap)
        {
            if (kvp.Value == null) continue;
            float sd = Vector3.Distance(transform.position, kvp.Value.transform.position);
            if (sd > nodeSearchRadius) continue;

            Vector3 snapNode = Snap(kvp.Value.transform.position, nodeSelectionAreaMask, 6f);
            bool nmOk = NavMesh.CalculatePath(snapCar, snapNode, nodeSelectionAreaMask, _nmSel)
                        && _nmSel.status != NavMeshPathStatus.PathInvalid;
            if (!nmOk) continue;

            float nmDist = GetPathLen(_nmSel);
            if (sd > 1f && nmDist > sd * nodePathRatioLimit) continue;

            if (destNodeID >= 0)
            {
                List<int> gp = navSystem.FindPath(kvp.Key, destNodeID);
                if (gp == null || gp.Count < 2) continue;
            }

            if (nmDist < bestLen) { bestLen = nmDist; best = kvp.Key; }
        }

        return best;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TRIM PASSED WAYPOINTS
    // ─────────────────────────────────────────────────────────────────────────

    private void TrimPassedWaypoints()
    {
        const float ON_TOP = 2f;
        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        while (_wpIdx < _wps.Count - 1)
        {
            Vector3 toWp = _wps[_wpIdx] - transform.position; toWp.y = 0f;
            float dist = toWp.magnitude;

            if (dist < ON_TOP) { _wpIdx++; continue; }
            if (dist < offPathRerouteDistance * 0.6f && Vector3.Dot(fwd, toWp.normalized) < 0f)
            { _wpIdx++; continue; }
            break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MARK ONE-WAY SEGMENTS
    //
    //  Builds _oneWaySegIdx: segment indices i where _wps[i]→_wps[i+1]
    //  corresponds to a one-way (bidirectional=false) graph connection.
    //
    //  Method: map each waypoint to its nearest graph node (≤4 m), propagate
    //  forward/backward through dense sub-waypoints, then check each segment
    //  pair against the one-way connection lookup.
    // ─────────────────────────────────────────────────────────────────────────

    private void MarkOneWaySegments(List<int> graphNodePath)
    {
        _oneWaySegIdx.Clear();
        if (graphNodePath == null || graphNodePath.Count < 2) return;
        if (navSystem?.connectionDefinitions == null) return;

        // Build one-way lookup
        var oneWaySet = new HashSet<(int, int)>();
        foreach (var conn in navSystem.connectionDefinitions)
            if (!conn.bidirectional)
                oneWaySet.Add((conn.fromNodeID, conn.toNodeID));

        // Map each waypoint index to the nearest graph node ID
        int[] wpNode = new int[_wps.Count];
        for (int i = 0; i < _wps.Count; i++) wpNode[i] = -1;

        foreach (int nodeID in graphNodePath)
        {
            if (!navSystem.nodeMap.ContainsKey(nodeID)) continue;
            Vector3 npos = navSystem.nodeMap[nodeID].worldPosition;
            float bestDist = 4f;
            int bestIdx = -1;
            for (int i = 0; i < _wps.Count; i++)
            {
                float d = Vector3.Distance(_wps[i], npos);
                if (d < bestDist) { bestDist = d; bestIdx = i; }
            }
            if (bestIdx >= 0) wpNode[bestIdx] = nodeID;
        }

        // Propagate forward (dense sub-waypoints inherit left node)
        int cur = -1;
        int[] fwd = new int[_wps.Count];
        for (int i = 0; i < _wps.Count; i++)
        { if (wpNode[i] >= 0) cur = wpNode[i]; fwd[i] = cur; }

        // Propagate backward (bridge waypoints before first node get a node)
        cur = -1;
        int[] bwd = new int[_wps.Count];
        for (int i = _wps.Count - 1; i >= 0; i--)
        { if (wpNode[i] >= 0) cur = wpNode[i]; bwd[i] = cur; }

        // Mark one-way segments
        for (int i = 0; i < _wps.Count - 1; i++)
        {
            int nA = fwd[i], nB = bwd[i + 1];
            if (nA < 0 || nB < 0 || nA == nB) continue;
            if (oneWaySet.Contains((nA, nB))) _oneWaySegIdx.Add(i);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DRAW ROUTE
    //
    //  Segment selection priority (matches Google Maps / Waze behaviour):
    //    1. FORWARD  — any road type, always preferred, pick closest
    //    2. BACKWARD on BIDIRECTIONAL road — valid fallback if no forward exists
    //    3. BACKWARD on ONE-WAY road — NEVER drawn (physically impossible)
    //       → line hidden, reroute fires within routeRefreshInterval
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawRoute()
    {
        if (_lr == null) _lr = navSystem.pathLineRenderer;
        if (_lr == null) return;
        if (_wps.Count == 0) { _lr.positionCount = 0; _lr.enabled = false; return; }

        _drawBuf.Clear();
        _drawBuf.Add(transform.position);

        Vector3 pos = transform.position;
        Vector3 fwd = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;

        int bestFwdSeg = -1; float bestFwdDist = float.MaxValue;
        int bestBidSeg = -1; float bestBidDist = float.MaxValue;

        for (int i = _wpIdx; i < _wps.Count - 1; i++)
        {
            Vector3 a = _wps[i], b = _wps[i + 1], ab = b - a;
            float sqLen = ab.sqrMagnitude;
            float t = sqLen < 0.001f ? 0f : Mathf.Clamp01(Vector3.Dot(pos - a, ab) / sqLen);
            float dist = Vector3.Distance(pos, a + ab * t);

            Vector3 toMid = ((a + b) * 0.5f) - pos; toMid.y = 0f;
            bool isForward = toMid.sqrMagnitude < 0.01f
                             || Vector3.Dot(fwd, toMid.normalized) >= 0f;

            if (isForward)
            {
                if (dist < bestFwdDist) { bestFwdDist = dist; bestFwdSeg = i; }
            }
            else if (!_oneWaySegIdx.Contains(i))   // bidirectional backward
            {
                if (dist < bestBidDist) { bestBidDist = dist; bestBidSeg = i; }
            }
            // one-way backward: silently skipped
        }

        int seg;
        if (bestFwdSeg >= 0) seg = bestFwdSeg;
        else if (bestBidSeg >= 0) seg = bestBidSeg;
        else
        {
            // Past all valid waypoints on a one-way road.
            // Hide line — reroute will fire within routeRefreshInterval.
            _lr.positionCount = 0;
            _lr.enabled = false;
            return;
        }

        // Project car onto chosen segment for a seamless line start
        if (seg < _wps.Count - 1)
        {
            Vector3 a = _wps[seg], b = _wps[seg + 1], ab = b - a;
            float sqLen = ab.sqrMagnitude;
            float t = sqLen < 0.001f ? 0f : Mathf.Clamp01(Vector3.Dot(pos - a, ab) / sqLen);
            Vector3 pp = a + ab * t;
            if (Vector3.Distance(pos, pp) > 0.3f) _drawBuf.Add(pp);
        }

        // Remaining waypoints — ground each one to road surface
        for (int i = seg + 1; i < _wps.Count; i++)
            _drawBuf.Add(GroundPoint(_wps[i]));

        if (_drawBuf.Count < 2) { _lr.positionCount = 0; _lr.enabled = false; return; }

        _lr.positionCount = _drawBuf.Count;
        for (int i = 0; i < _drawBuf.Count; i++)
            _lr.SetPosition(i, _drawBuf[i]);
        _lr.enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GROUND POINT
    //
    //  Pins a waypoint to the actual road surface using a 5-point cross
    //  raycast pattern and taking the median Y — robust against seam hits.
    //
    //  WHY NOT NavMesh.SamplePosition:
    //    NavMesh can bake floating above the road (visible in scene view as
    //    a cyan surface above the tarmac). SamplePosition returns that float
    //    height, which gets reproduced in the line renderer. We use physics
    //    raycasts against the real mesh instead.
    //
    //  WHY MEDIAN (not average):
    //    Kerb edges and junction seams can return one outlier hit. Median
    //    ignores outliers; average would shift toward them.
    //
    //  ORIGIN = carY + groundRaycastOriginHeight:
    //    Starts well above the car so the cast finds the surface below
    //    regardless of what Y the NavMesh baked the waypoint at.
    //    maxHeightDeviation filters out rooftops and underground geometry.
    // ─────────────────────────────────────────────────────────────────────────

    private Vector3 GroundPoint(Vector3 pt)
    {
        float carY = transform.position.y;
        float originY = carY + groundRaycastOriginHeight;

        _groundHits.Clear();

        foreach (var offset in _groundOffsets)
        {
            Vector3 origin = new Vector3(pt.x + offset.x, originY, pt.z + offset.y);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit h,
                                groundRaycastTotalLength, roadSurfaceLayer))
            {
                float hitY = h.point.y;
                if (Mathf.Abs(hitY - carY) < maxHeightDeviation)
                    _groundHits.Add(hitY);
            }
        }

        if (_groundHits.Count == 0)
        {
            // No surface hit — use car's Y (never NavMesh — it may float)
            return new Vector3(pt.x, carY + lineHeightAboveRoad, pt.z);
        }

        _groundHits.Sort();
        float medianY = _groundHits[_groundHits.Count / 2];
        return new Vector3(pt.x, medianY + lineHeightAboveRoad, pt.z);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SUBDIVIDE SEGMENT  (with grounding)
    //
    //  Lerp is a straight line in 3D — it floats above slopes/curves.
    //  Subdivide the segment and ground each intermediate point.
    // ─────────────────────────────────────────────────────────────────────────

    private void AddSubdivided(List<Vector3> list, Vector3 a, Vector3 b)
    {
        int divs = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / maxLineSegmentLength));
        for (int s = 1; s <= divs; s++)
        {
            Vector3 pt = Vector3.Lerp(a, b, (float)s / divs);
            list.Add(GroundPoint(pt));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UTILITIES
    // ─────────────────────────────────────────────────────────────────────────

    private static Vector3 Snap(Vector3 p, int mask, float r)
        => NavMesh.SamplePosition(p, out NavMeshHit h, r, mask) ? h.position : p;

    private static float GetPathLen(NavMeshPath p)
    {
        float l = 0f;
        for (int i = 0; i < p.corners.Length - 1; i++)
            l += Vector3.Distance(p.corners[i], p.corners[i + 1]);
        return l;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────────────────

    public void ForceRouteRefresh()
    {
        _built = false; _wps.Clear(); _wpIdx = 0; _srcNode = -1; _dstNode = -1;
        navSystem?.InvalidatePlayerPathCache();
        if (navSystem != null && navSystem.RouteCacheReady) BuildRoute();
    }

    public float GetAverageReactionTime() => _rtCount > 0 ? _rtSum / _rtCount : -1f;
    public float GetWorstReactionTime() => _rtCount > 0 ? _rtWorst : -1f;
    public void ClearReactionData()
    {
        _rtSum = 0; _rtCount = 0; _rtWorst = -1; _lastRt = -1;
        _obsSeen = -1; _obsLast = false; _reacted = false;
    }

    public float LastBrakeReactionTime => _lastRt;
    public List<Vector3> CurrentWaypoints => _wps;
    public int CurrentSourceNode => _srcNode;
    public int CurrentDestNode => _dstNode;
    public bool HasRoute => _built && _wps.Count > 0;

    // ─────────────────────────────────────────────────────────────────────────
    //  BRAKE REACTION
    // ─────────────────────────────────────────────────────────────────────────

    private void MeasureBrakeReaction()
    {
        if (obstacleDetectionLayer.value == 0) return;

        bool hit = Physics.Raycast(transform.position + Vector3.up * 0.5f,
                                   transform.forward, reactionRayDistance,
                                   obstacleDetectionLayer);

        if (hit && !_obsLast) { _obsSeen = Time.time; _reacted = false; }
        if (!hit && _obsLast) { _obsSeen = -1f; _reacted = false; }

        if (hit && !_reacted && _obsSeen > 0f && _rb != null)
        {
            float spd = new Vector2(_rb.linearVelocity.x, _rb.linearVelocity.z).magnitude;
            if (spd < 0.5f)
            {
                _lastRt = Time.time - _obsSeen;
                _reacted = true;
                _rtSum += _lastRt;
                _rtCount++;
                if (_lastRt > _rtWorst) _rtWorst = _lastRt;
                Debug.Log($"[CarCtrl] Reaction {_lastRt * 1000f:F0}ms  " +
                          $"avg={GetAverageReactionTime() * 1000f:F0}ms");
            }
        }

        _obsLast = hit;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  TEST FOLLOW PATH
    // ─────────────────────────────────────────────────────────────────────────

    private void TestFollowPath()
    {
        if (_wpIdx >= _wps.Count) { _built = false; return; }

        Vector3 toWp = new Vector3(_wps[_wpIdx].x - transform.position.x, 0f,
                                   _wps[_wpIdx].z - transform.position.z);

        if (toWp.magnitude < testWaypointReachDistance) { _wpIdx++; return; }

        if (toWp.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(toWp.normalized),
                testTurnSpeed * Time.deltaTime);

        transform.position += transform.forward * testFollowSpeed * Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Brake reaction ray
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f,
                       transform.forward * reactionRayDistance);

        // Current target waypoint
        if (_wps != null && _wpIdx < _wps.Count)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_wps[_wpIdx], 0.4f);
            Gizmos.DrawLine(transform.position, _wps[_wpIdx]);
        }

        // Off-path distance circle (orange)
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.35f);
        const int segs = 24;
        for (int i = 0; i < segs; i++)
        {
            float a0 = i * (360f / segs) * Mathf.Deg2Rad;
            float a1 = (i + 1) * (360f / segs) * Mathf.Deg2Rad;
            Gizmos.DrawLine(
                transform.position + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * offPathRerouteDistance,
                transform.position + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * offPathRerouteDistance);
        }

        if (!Application.isPlaying) return;

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 3.5f,
            $"WP {dbgWpIndex}/{dbgWpTotal}  " +
            $"dst={(fixedDestinationNode != null ? fixedDestinationNode.name : _dstNode.ToString())}  " +
            $"src={dbgSrcNode}\n" +
            $"nearWp={dbgNearestWp:F1}m  {dbgRouteSrc}" +
            (dbgRerouted ? "  ← REROUTED" : ""),
            new GUIStyle
            {
                normal = new GUIStyleState
                {
                    textColor = dbgRerouted
                        ? new Color(1f, 0.4f, 0.1f)
                        : new Color(0.3f, 1f, 0.3f)
                },
                fontSize = 10,
                fontStyle = FontStyle.Bold
            });
    }
#endif
}