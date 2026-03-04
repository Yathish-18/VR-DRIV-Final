// ============================================================================
//  CENTRALIZED CAR CONTROLLER  v4.2
//  ============================================================================
//  Does NOT touch Rigidbody / move the car / handle input.
//  Draws the player's planned route as a LineRenderer.
//
//  ROUTING ALGORITHM:
//  ─────────────────────────────────────────────────────────────────────────
//  On reroute:
//    1. Scan every node within nodeSearchRadius.
//    2. For each node compute:
//         • nmDist  = NavMesh path length from car → node
//           (respects physical road geometry, filters across-divider nodes)
//         • graphOk = FindPath(node → dest) exists
//           (respects one-way connections)
//         • score   = nmDist + graphLength * 0.3
//           Nodes ahead of the car get a 30% bonus (multiplier 0.7 on nmDist).
//    3. Pick the node with the lowest score that passes both checks.
//    4. Build the route:
//         [carPos] ──NavMesh──► [srcNode] ──graph segments──► [dest]
//       The NavMesh bridge from carPos to srcNode ensures the line starts
//       exactly where the car is, even mid-segment between two far nodes.
//       The graph segments respect one-way connections from srcNode onward.
//
//  REROUTE TRIGGER:
//    If the car is > offPathRerouteDistance from every remaining waypoint
//    → reroute immediately.
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

    [Header("═══  NODE SELECTION  ═══")]
    [Tooltip("Search radius for candidate nodes.")]
    [Range(10f, 300f)]
    public float nodeSearchRadius = 120f;

    [Tooltip("Reject nodes where NavMesh detour > straight dist × this. " +
             "Filters nodes across road dividers.")]
    [Range(1.2f, 6f)]
    public float nodePathRatioLimit = 3f;

    [Tooltip("NavMesh area mask for node reachability checks.")]
    public int nodeSelectionAreaMask = NavMesh.AllAreas;

    [Header("═══  DEBUG  ═══")]
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

    private LineRenderer _lr = null;
    private NavMeshPath _nmWork = null;   // reused scratch NavMeshPath
    private NavMeshPath _nmSel = null;   // reused for node selection
    private List<Vector3> _drawBuf = new List<Vector3>();
    private bool _prevViz = true;
    private Rigidbody _rb;

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
                Debug.LogError($"[CarController] {name}: CentralizedNavigationSystem not found.");
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
            // No fixed dest — pick random via pool
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
        Debug.Log(_built
            ? $"[CarCtrl] Route built {_wps.Count} wps | {dbgRouteSrc} | src={_srcNode}→dst={_dstNode}"
            : "[CarCtrl] BuildRoute FAILED");
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
        if (_srcNode != -1 && _dstNode != -1) navSystem.ReleaseRoute(_srcNode, _dstNode);

        int dest = FixedDestNodeID >= 0 ? FixedDestNodeID : _dstNode;
        if (dest < 0 || !navSystem.nodeMap.ContainsKey(dest))
        {
            Debug.LogWarning("[CarCtrl] Reroute: no valid destination."); return;
        }

        List<Vector3> newWps = MakeHybridRoute(transform.position, dest, out int srcUsed);
        if (newWps.Count < 2)
        {
            Debug.LogWarning("[CarCtrl] Reroute: MakeHybridRoute failed, keeping old route.");
            return;
        }

        _srcNode = srcUsed;
        _dstNode = dest;
        _wps = newWps;
        _wpIdx = 0;
        dbgSrcNode = _srcNode;
        navSystem.InvalidatePlayerPathCache();
        Debug.Log($"[CarCtrl] ✅ Rerouted {_wps.Count} wps | {dbgRouteSrc} | src={_srcNode}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  MAKE HYBRID ROUTE  ← the heart of the system
    //
    //  Returns a List<Vector3> that:
    //    • Starts at carPos (exact car position, no gap)
    //    • Follows road geometry via NavMesh to the chosen srcNode
    //    • Continues along one-way-respecting graph segments to dest
    //
    //  srcNodeUsed is set to the graph node used as the stitching point.
    // ─────────────────────────────────────────────────────────────────────────

    private List<Vector3> MakeHybridRoute(Vector3 carPos, int destNodeID, out int srcNodeUsed)
    {
        srcNodeUsed = -1;
        if (!navSystem.nodeMap.ContainsKey(destNodeID)) return new List<Vector3>();

        // ── Find best source node (respects one-way graph + NavMesh geometry) ─
        int src = BestSrcNode(destNodeID);
        if (src < 0)
        {
            // No forward node found — try without direction filter
            src = BestSrcNodeAny(destNodeID);
        }

        if (src >= 0)
        {
            List<int> graphNodes = navSystem.FindPath(src, destNodeID);
            List<Vector3> graphWps = (graphNodes != null && graphNodes.Count >= 2)
                                        ? navSystem.GetDenseRoute(graphNodes)
                                        : null;

            if (graphWps != null && graphWps.Count >= 2)
            {
                srcNodeUsed = src;

                // ── NavMesh bridge: carPos → first graph waypoint ─────────────
                var route = new List<Vector3>(graphWps.Count + 40);
                route.Add(carPos);

                Vector3 snapCar = Snap(carPos, navMeshAreaMask, 8f);
                Vector3 snapGrph = Snap(graphWps[0], navMeshAreaMask, 8f);

                if (Vector3.Distance(carPos, graphWps[0]) > 1.5f)
                {
                    bool ok = NavMesh.CalculatePath(snapCar, snapGrph, navMeshAreaMask, _nmWork)
                              && _nmWork.status != NavMeshPathStatus.PathInvalid
                              && _nmWork.corners.Length >= 2;
                    if (ok)
                    {
                        for (int i = 1; i < _nmWork.corners.Length; i++)
                            AddSubdivided(route, _nmWork.corners[i - 1], _nmWork.corners[i]);
                    }
                    // If bridge failed: we already added carPos, graph segments follow
                }

                // ── Append graph waypoints ────────────────────────────────────
                // Skip graphWps[0] if bridge already ended near it
                int gStart = (route.Count > 1 &&
                              Vector3.Distance(route[route.Count - 1], graphWps[0]) < 2.5f)
                             ? 1 : 0;
                for (int i = gStart; i < graphWps.Count; i++)
                    route.Add(graphWps[i]);

                dbgRouteSrc = $"hybrid src={src}";
                return route;
            }
        }

        // ── Fallback: pure NavMesh (no one-way guarantee, but shows something) ─
        Debug.LogWarning($"[CarCtrl] No graph path to dest={destNodeID} from any node. " +
                          "Falling back to pure NavMesh (may ignore one-way roads).");

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
    //  BEST SOURCE NODE  —  the node picker
    //
    //  Scores every nearby node with:
    //    score = nmDist × dirBias + graphDist × 0.25
    //
    //  dirBias: nodes ahead of the car get 0.7× (30% cheaper), nodes behind
    //  get 1.3× (30% more expensive). This strongly prefers forward nodes
    //  without completely excluding behind nodes, so we always find *something*
    //  even if the only valid graph node is at the junction behind the car.
    //
    //  Only nodes where FindPath(node → dest) succeeds are considered.
    //  destNodeID = -1 means no graph check (used for random-dest init).
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

            // NavMesh reachability
            Vector3 snapNode = Snap(nodePos, nodeSelectionAreaMask, 6f);
            bool nmOk = NavMesh.CalculatePath(snapCar, snapNode, nodeSelectionAreaMask, _nmSel)
                        && _nmSel.status != NavMeshPathStatus.PathInvalid;
            if (!nmOk) continue;

            float nmDist = GetPathLen(_nmSel);
            // Reject nodes that require a huge NavMesh detour vs straight line
            if (sd > 1f && nmDist > sd * nodePathRatioLimit) continue;

            // Graph path check
            float graphDist = 0f;
            if (destNodeID >= 0)
            {
                List<int> gp = navSystem.FindPath(kvp.Key, destNodeID);
                if (gp == null || gp.Count < 2) continue;
                // Estimate graph distance
                for (int i = 0; i < gp.Count - 1; i++)
                {
                    if (navSystem.nodeMap.ContainsKey(gp[i]) && navSystem.nodeMap.ContainsKey(gp[i + 1]))
                        graphDist += Vector3.Distance(navSystem.nodeMap[gp[i]].worldPosition,
                                                      navSystem.nodeMap[gp[i + 1]].worldPosition);
                }
            }

            // Direction bias: ahead = 0.7×, behind = 1.3×
            float dot = sd > 1f ? Vector3.Dot(carFwd, toNode / sd) : 1f;
            float dirBias = dot >= 0f ? Mathf.Lerp(1f, 0.7f, dot)
                                       : Mathf.Lerp(1f, 1.3f, -dot);

            float score = nmDist * dirBias + graphDist * 0.25f;
            if (score < bestScore) { bestScore = score; best = kvp.Key; }
        }

        return best;
    }

    // Same as BestSrcNode but with no direction bias at all — pure graph+navmesh cost.
    // Used as fallback when BestSrcNode returns -1.
    private int BestSrcNodeAny(int destNodeID)
    {
        if (navSystem?.nodeMap == null) return -1;

        Vector3 snapCar = Snap(transform.position, nodeSelectionAreaMask, 6f);
        int best = -1;
        float bestScore = float.MaxValue;

        foreach (var kvp in navSystem.nodeMap)
        {
            if (kvp.Value == null) continue;
            Vector3 nodePos = kvp.Value.transform.position;
            float sd = Vector3.Distance(transform.position, nodePos);
            if (sd > nodeSearchRadius) continue;

            Vector3 snapNode = Snap(nodePos, nodeSelectionAreaMask, 6f);
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

            if (nmDist < bestScore) { bestScore = nmDist; best = kvp.Key; }
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
            if (dist < offPathRerouteDistance * 0.6f
                && Vector3.Dot(fwd, toWp.normalized) < 0f) { _wpIdx++; continue; }
            break;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DRAW ROUTE
    // ─────────────────────────────────────────────────────────────────────────

    private void DrawRoute()
    {
        if (_lr == null) _lr = navSystem.pathLineRenderer;
        if (_lr == null) return;
        if (_wps.Count == 0) { _lr.positionCount = 0; _lr.enabled = false; return; }

        _drawBuf.Clear();
        _drawBuf.Add(transform.position);

        // Project car onto the remaining polyline for a seamless start
        Vector3 pos = transform.position;
        float minSq = float.MaxValue;
        int seg = _wpIdx;

        for (int i = _wpIdx; i < _wps.Count - 1; i++)
        {
            Vector3 a = _wps[i], b = _wps[i + 1], ab = b - a;
            float sqLen = ab.sqrMagnitude;
            float t = sqLen < 0.001f ? 0f : Mathf.Clamp01(Vector3.Dot(pos - a, ab) / sqLen);
            float sq = (pos - (a + ab * t)).sqrMagnitude;
            if (sq < minSq) { minSq = sq; seg = i; }
        }

        if (seg < _wps.Count - 1)
        {
            Vector3 a = _wps[seg], b = _wps[seg + 1], ab = b - a;
            float sqLen = ab.sqrMagnitude;
            float t = sqLen < 0.001f ? 0f : Mathf.Clamp01(Vector3.Dot(pos - a, ab) / sqLen);
            Vector3 pp = a + ab * t;
            if (Vector3.Distance(pos, pp) > 0.3f) _drawBuf.Add(pp);
        }

        for (int i = seg + 1; i < _wps.Count; i++) _drawBuf.Add(_wps[i]);

        if (_drawBuf.Count < 2) { _lr.positionCount = 0; _lr.enabled = false; return; }

        _lr.positionCount = _drawBuf.Count;
        for (int i = 0; i < _drawBuf.Count; i++) _lr.SetPosition(i, _drawBuf[i]);
        _lr.enabled = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  UTILITIES
    // ─────────────────────────────────────────────────────────────────────────

    private static Vector3 Snap(Vector3 p, int mask, float r)
    {
        return NavMesh.SamplePosition(p, out NavMeshHit h, r, mask) ? h.position : p;
    }

    private void AddSubdivided(List<Vector3> list, Vector3 a, Vector3 b)
    {
        int divs = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / maxLineSegmentLength));
        for (int s = 1; s <= divs; s++)
            list.Add(Vector3.Lerp(a, b, (float)s / divs));
    }

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
                _lastRt = Time.time - _obsSeen; _reacted = true;
                _rtSum += _lastRt; _rtCount++;
                if (_lastRt > _rtWorst) _rtWorst = _lastRt;
                Debug.Log($"[CarCtrl] Reaction {_lastRt * 1000f:F0}ms avg={GetAverageReactionTime() * 1000f:F0}ms");
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
                Quaternion.LookRotation(toWp.normalized), testTurnSpeed * Time.deltaTime);
        transform.position += transform.forward * testFollowSpeed * Time.deltaTime;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GIZMOS
    // ─────────────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f,
                       transform.forward * reactionRayDistance);

        if (_wps != null && _wpIdx < _wps.Count)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_wps[_wpIdx], 0.4f);
            Gizmos.DrawLine(transform.position, _wps[_wpIdx]);
        }

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
        UnityEditor.Handles.Label(transform.position + Vector3.up * 3.5f,
            $"WP {dbgWpIndex}/{dbgWpTotal}  dst={(fixedDestinationNode != null ? fixedDestinationNode.name : _dstNode.ToString())}  src={dbgSrcNode}\n" +
            $"nearWp={dbgNearestWp:F1}m  {dbgRouteSrc}" +
            (dbgRerouted ? "  ← REROUTED" : ""),
            new GUIStyle
            {
                normal = new GUIStyleState { textColor = dbgRerouted ? new Color(1f, 0.4f, 0.1f) : new Color(0.3f, 1f, 0.3f) },
                fontSize = 10,
                fontStyle = FontStyle.Bold
            });
    }
#endif
}