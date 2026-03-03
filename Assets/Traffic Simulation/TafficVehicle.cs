// ============================================================================
//  TRAFFIC VEHICLE  ·  v5.3  —  FULL SCENE GIZMOS
//
//  NEW vs v5.2:
//  ─────────────────────────────────────────────────────────────────────────
//  All raycasts and waypoints are now visible in the Scene view.
//  Toggle each layer independently in the Inspector under "SCENE GIZMOS".
//
//  DETECTION RAY
//    Full-length ray, colour-coded by what it hit:
//      Green  = nothing  |  Yellow = vehicle (far)  |  Red = vehicle/obstacle (stop zone)
//      Cyan   = traffic light
//    Diamond marker at exact hit point + distance label.
//    Orange ring marks the vehicle stopping distance on the ray.
//
//  WHEEL RAYS  (4 × downward ground casts)
//    Green line + solid sphere   = hit (contact point shown)
//    Red line                    = miss (full wheelRayDistance drawn)
//    Small circle at ray origin  = wheel hub height
//    "FL / FR / RL / RR" labels  optional (gizmoShowWaypointLabels)
//
//  CENTRE-BODY FALLBACK RAY
//    Orange/yellow  = fallback active + hit point
//    Magenta        = AIRBORNE (all 4 wheels AND centre-body miss)
//
//  WAYPOINTS
//    Cyan pulsing sphere   = current target WP  (car is driving toward this)
//    Green  sphere (WP+1)  = next waypoint
//    Lime   sphere (WP+2)
//    Yellow sphere (WP+3)  … up to gizmoUpcomingWaypointCount (0–5)
//    Vertical pole per sphere  = visible even when buried under terrain
//    Orange spheres/line       = active NavMesh recovery segment
//    Thin coloured line        = full remaining route (gizmoShowFullRoute)
//    Grey  line                = already-visited portion of route
//    Magenta pillar + ring     = destination node
//
//  All gizmos are drawn in OnDrawGizmos (always visible, not just selected).
//  Rich Inspector overlay remains on OnDrawGizmosSelected as before.
// ============================================================================
//
//  FIXES vs v5.1:
//  ─────────────────────────────────────────────────────────────────────────
//  BUG 1 — "Car drives sideways onto pavement / gets STUCK immediately"
//  ROOT CAUSE:
//    NavMesh.SamplePosition() in MoveVehicle() used navMeshSampleRadius (4 m
//    default). When a waypoint sits near the road edge, Unity happily returns
//    the nearest NavMesh point — which may be ON the baked pavement/kerb that
//    is also walkable. The car is invisibly snapped 2–3 m sideways each frame.
//  FIX:
//    Added a lateral-shift guard: only accept the NavMesh snap if it moves
//    the car ≤ navMeshMaxLateralSnap (default 0.8 m). Larger snaps are
//    discarded so the car keeps its intended direction.
//    This alone stops the majority of "sideways pavement" cases.
//
//  BUG 2 — "SyncSpawnRotation() causes NullRef / missing method crash"
//  ROOT CAUSE:
//    CentralizedNavigationSystem.SpawnTrafficVehicles() calls
//        tv.SyncSpawnRotation()
//    after applying the correct spawn rotation, but the method did not exist
//    in TrafficVehicle, so the call either silently no-ops (if the compiler
//    stripped it) or throws a MissingMethodException at runtime.
//  FIX:
//    Added SyncSpawnRotation() — it writes the post-spawn rotation into
//    _smoothedSlopeTilt so the first FixedUpdate doesn't fight to rotate
//    from identity → correct heading (the "spawn zigzag" artifact).
//
//  BUG 3 — "A few cars spawn facing backward / drive away from first WP"
//  ROOT CAUSE:
//    GetSpawnFacingDirection() only used waypoints[1] - waypoints[0].
//    For some pre-baked routes the first NavMesh corner sits fractionally
//    behind the node position (NavMesh path can start with a tiny U-turn
//    on curved roads), yielding a near-180° facing. This affects only the
//    routes whose first NavMesh corner happens to be slightly behind the
//    node — typically a small % of nodes near road bends.
//  FIX:
//    Average the direction of the first N waypoint steps (default 3).
//    Short segments are weighted less so a single outlier corner can't
//    flip the heading.  Result is stable even on tight bends.
//
//  BUG 4 — "Stuck recovery just skips waypoints; car still drives off-road"
//  ROOT CAUSE:
//    RecoverFromStuck() only advanced waypointIndex by 3 or called
//    RequestReroute(). Neither option re-paths around whatever geometry
//    the car is wedged against. The new route from RequestReroute also
//    uses pre-baked waypoints that may still pass through the obstacle.
//  FIX:
//    Added TryInjectNavMeshRecoveryPath():
//      • NavMesh.CalculatePath(currentPos → next graph node)
//      • Subdivide corners to maxWaypointSpacing-sized steps
//      • Inject the fresh waypoints into denseWaypoints at waypointIndex
//      • Car now follows a live, geometry-aware path out of the stuck zone
//      • Original route waypoints resume after the recovery segment
//    This is tried BEFORE the re-anchor / RequestReroute so it is fast and
//    doesn't produce a pool miss.
//
//  WHEEL ASSIGNMENT GUIDE (truck example) — unchanged from v5.1:
//    wheelFL       → "Rotating"                 (spin + ray origin)
//    wheelFL_Steer → "Whl HD FL_WheelController" (steer pivot)
// ============================================================================

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody))]
public class TrafficVehicle : MonoBehaviour
{
    // =========================================================================
    //  RUNTIME CONFIG
    // =========================================================================

    [Header("═══  RUNTIME REF (read-only)  ═══")]
    [SerializeField] private CentralizedNavigationSystem navSystem;
    private Rigidbody rb;

    [Header("═══  RUNTIME — MOVEMENT  ═══")]
    [SerializeField] private float maxSpeed;
    [SerializeField] private float turnSpeed;
    [SerializeField] private float speedSmoothTime;

    [Header("═══  RUNTIME — WAYPOINT REACH  ═══")]
    [SerializeField] private float waypointReachDistanceXZ;
    [SerializeField] private float waypointReachDistanceY;
    [SerializeField] private float minAdvanceInterval;

    [Header("═══  RUNTIME — DETECTION  ═══")]
    [SerializeField] private LayerMask detectionLayerMask;
    [SerializeField] private LayerMask npcVehicleLayer;
    [SerializeField] private LayerMask playerVehicleLayer;
    [SerializeField] private LayerMask trafficLightLayer;
    [SerializeField] private float     detectionRange;
    [SerializeField] private float     vehicleStoppingDistance;
    [SerializeField] private float     obstacleStoppingDistance;
    [SerializeField] private float     trafficLightStopDistance;
    [SerializeField] private float     maxRedLightWaitTime;

    [Header("═══  RUNTIME — GROUND & SLOPE  ═══")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("How far UP from car pivot the centre-body fallback ray starts.\n" +
             "Keep at 1.5–3.0 m so it clears any bottom colliders.")]
    [SerializeField] private float groundRayUpOffset = 2f;

    [Tooltip("Total downward reach of the centre-body fallback ray.\n" +
             "Set to vehicle half-height + 3 m margin. 5–10 m for trucks.")]
    [SerializeField] private float groundRayDistance = 8f;

    [Tooltip("Target height above road surface. 0.3–0.6 m typical.")]
    [SerializeField] private float rideHeight = 0.4f;

    [Tooltip("Y snap speed. 12–16 recommended. Low values cause floating.")]
    [SerializeField] private float groundSnapStrength = 14f;

    [Tooltip("Body tilt speed toward road normal. 10–15 recommended.")]
    [SerializeField] private float slopeTiltSpeed = 12f;

    [SerializeField] private float hillClimbBoost;

    [Header("═══  RUNTIME — STUCK DETECTION  ═══")]
    [SerializeField] private int   maxStuckFrames;
    [SerializeField] private float stuckMovementThreshold;
    [SerializeField] private int   maxPathRecalculations;

    [Header("═══  RUNTIME — DEBUG  ═══")]
    [SerializeField] public bool  showDebugRays;
    [SerializeField] private bool showDebugGizmos;

    // =========================================================================
    //  MODEL-SPECIFIC
    // =========================================================================

    [Header("═══  MODEL-SPECIFIC (set per prefab)  ═══")]
    [Tooltip("Wheel radius in metres.\n" +
             "Cars: ~0.33–0.40 m.  Trucks/buses: ~0.50–0.65 m.\n" +
             "Controls both visual spin speed and ground-ray origin height.")]
    [SerializeField] private float wheelRadius = 0.4f;

    // ── Wheel Spin Transforms ─────────────────────────────────────────────────
    [Header("═══  WHEEL SPIN TRANSFORMS  (ground ray origins)  ═══")]
    [Tooltip("Assign the SPINNING transform (e.g. 'Rotating' child of the WheelController).\n" +
             "The ground ray fires from this position + up*(wheelRadius*0.6).\n" +
             "Do NOT assign the leaf mesh or the WheelController pivot here.")]
    [SerializeField] private Transform wheelFL;
    [SerializeField] private Transform wheelFR;
    [SerializeField] private Transform wheelRL;
    [SerializeField] private Transform wheelRR;

    // ── Wheel Steer Pivots ────────────────────────────────────────────────────
    [Header("═══  WHEEL STEER PIVOTS  (optional — for deep hierarchies)  ═══")]
    [Tooltip("OPTIONAL. Assign the WheelController / steer pivot transform for each\n" +
             "FRONT wheel separately from the spin transform above.\n\n" +
             "Example truck hierarchy:\n" +
             "  Wheels / Whl HD FL_WheelController / Rotating / Whl HD FL\n" +
             "  → wheelFL       = 'Rotating'               (spin + ray)\n" +
             "  → wheelFL_Steer = 'Whl HD FL_WheelController' (steer)\n\n" +
             "If left empty, falls back to wheel.parent (simple car rigs).\n" +
             "Rear wheels never steer so no steer pivot needed for RL/RR.")]
    [SerializeField] private Transform wheelFL_Steer;
    [SerializeField] private Transform wheelFR_Steer;

    [Tooltip("Local spin axis. Usually Vector3.right (1,0,0).")]
    [SerializeField] private Vector3 wheelSpinAxis = Vector3.right;

    [Tooltip("Max front wheel visual steer angle (degrees).")]
    [SerializeField] private float maxSteerAngle = 30f;

    // ── Cached rest rotations for steer pivots ────────────────────────────────
    private Quaternion _restFL = Quaternion.identity;
    private Quaternion _restFR = Quaternion.identity;

    // =========================================================================
    //  GROUND SAMPLING CONFIG
    // =========================================================================

    [Header("═══  GROUND SAMPLING  ═══")]
    [Tooltip("Downward ray distance from each wheel origin.\n" +
             "For trucks/buses set 1.5–2.5 m. For cars 0.8–1.2 m.")]
    [SerializeField] private float wheelRayDistance = 2.0f;

    // ── Per-wheel hit cache ───────────────────────────────────────────────────
    private bool    _wFL_hit, _wFR_hit, _wRL_hit, _wRR_hit;
    private Vector3 _wFL_pt,  _wFR_pt,  _wRL_pt,  _wRR_pt;
    private int     _wheelHitCount = 0;

    // =========================================================================
    //  NAVMESH ROAD CLAMPING
    // =========================================================================

    [Header("═══  NAVMESH ROAD CLAMPING  ═══")]
    [SerializeField] private float navMeshSampleRadius = 4f;
    [SerializeField] private int   navMeshAreaMask     = NavMesh.AllAreas;

    [Tooltip("Maximum lateral (XZ) correction NavMesh clamping is allowed to apply.\n" +
             "Prevents the car being snapped sideways onto baked pavement / kerb.\n" +
             "0.8–1.2 m is a good range. Set higher only if you need aggressive road-centering.")]
    [SerializeField] private float navMeshMaxLateralSnap = 0.8f;

    // =========================================================================
    //  ROUTE STATE
    // =========================================================================

    [Header("═══  ROUTE  (read-only)  ═══")]
    [SerializeField] private int sourceNodeID      = -1;
    [SerializeField] private int destinationNodeID = -1;
    [SerializeField] private int currentNodeID     = -1;

    private List<Vector3> denseWaypoints = new List<Vector3>();
    private int           waypointIndex  = 0;
    private Vector3       currentTarget;
    private bool          hasTarget      = false;

    // ── Recovery state ────────────────────────────────────────────────────────
    // Set true while denseWaypoints contains a live-injected NavMesh segment.
    // Cleared once waypointIndex advances past the injected segment.
    private bool _inNavMeshRecovery   = false;
    private int  _recoveryEndIndex    = 0;

    // =========================================================================
    //  MOVEMENT STATE
    // =========================================================================

    private float   currentSpeed           = 0f;
    private float   speedSmoothVelocity    = 0f;
    private float   targetSpeed            = 0f;
    public  bool    isStopped              = false;
    private float   angleToCurrentWaypoint = 0f;
    private float   currentSteerAngle      = 0f;

    private Vector3 lastValidPosition;
    private int     stuckCounter       = 0;
    private bool    isStuck            = false;
    private int     pathRecalculations = 0;
    private float   lastAdvanceTime    = -999f;
    private bool    _advancedThisFrame = false;

    // Ground state
    private bool    isGrounded     = false;
    private Vector3 groundNormal   = Vector3.up;
    private float   currentGroundY = 0f;
    private Vector3 groundHitPoint = Vector3.zero;

    // Tilt
    private Quaternion _smoothedSlopeTilt = Quaternion.identity;

    // =========================================================================
    //  DETECTION STATE
    // =========================================================================

    private enum HitType { None, NpcVehicle, PlayerVehicle, TrafficLight, Obstacle }

    private HitType    _hitType     = HitType.None;
    private float      _hitDistance = 0f;
    private GameObject _hitObject   = null;

    private TrafficLightController _currentLight      = null;
    private bool                   _stoppedAtRed      = false;
    private float                  _redLightEntryTime = 0f;

    // =========================================================================
    //  SCENE GIZMO TOGGLES
    // =========================================================================

    [Header("═══  SCENE GIZMOS  ═══")]
    [Tooltip("Master switch — draw all scene gizmos for this vehicle.")]
    [SerializeField] private bool gizmosEnabled = true;
    [Tooltip("Show the forward detection raycast, its full range, and hit-point sphere.")]
    [SerializeField] private bool gizmoShowDetectionRay = true;
    [Tooltip("Show the 4 wheel ground rays (green=hit / red=miss) and contact spheres.")]
    [SerializeField] private bool gizmoShowWheelRays = true;
    [Tooltip("Show the centre-body fallback ground ray (fires only when all wheels miss).")]
    [SerializeField] private bool gizmoShowCentreBodyRay = true;
    [Tooltip("Highlight the CURRENT target waypoint the car is driving toward.")]
    [SerializeField] private bool gizmoShowCurrentWaypoint = true;
    [Tooltip("Number of upcoming NavMesh waypoints to show beyond the current one (0-5).")]
    [Range(0, 5)]
    [SerializeField] private int  gizmoUpcomingWaypointCount = 3;
    [Tooltip("Show the full remaining route path as a thin coloured line.")]
    [SerializeField] private bool gizmoShowFullRoute = true;
    [Tooltip("Show WP+1, WP+2 … labels on upcoming waypoints in Scene view.")]
    [SerializeField] private bool gizmoShowWaypointLabels = true;

    // ── Cached per-FixedUpdate so Gizmos thread can read them safely ─────────
    private Vector3 _gizDetectOrigin  = Vector3.zero;   // detection ray start
    private Vector3 _gizDetectDir     = Vector3.forward; // detection ray direction
    private bool    _gizDetectHit     = false;           // did detection ray hit?
    private Vector3 _gizDetectHitPt   = Vector3.zero;   // hit world position
    private float   _gizDetectRange   = 20f;             // full ray length

    private Vector3 _gizWheelOriginFL = Vector3.zero;   // per-wheel ray start points
    private Vector3 _gizWheelOriginFR = Vector3.zero;
    private Vector3 _gizWheelOriginRL = Vector3.zero;
    private Vector3 _gizWheelOriginRR = Vector3.zero;

    private Vector3 _gizCentreOrigin  = Vector3.zero;   // centre-body fallback ray
    private bool    _gizCentreHit     = false;
    private Vector3 _gizCentreHitPt   = Vector3.zero;
    private float   _gizCentreLen     = 10f;

    // =========================================================================
    //  DEBUG INSPECTOR
    // =========================================================================

    [Header("═══  DEBUG INFO (read-only)  ═══")]
    [SerializeField] private string  debugChainName           = "";
    [SerializeField] private int     debugCurrentWaypoint     = 0;
    [SerializeField] private int     debugNextWaypointIndex   = 0;
    [SerializeField] private int     debugCurrentNodeID       = -1;
    [SerializeField] private int     debugNextNodeID          = -1;
    [SerializeField] private int     debugTotalWaypoints      = 0;
    [SerializeField] private float   debugProgressPct         = 0f;
    [SerializeField] private float   debugDistToDest          = 0f;
    [SerializeField] private float   debugCurrentSpeed        = 0f;
    [SerializeField] private float   debugDistanceToWaypoint  = 0f;
    [SerializeField] private bool    debugIsStuck             = false;
    [SerializeField] private bool    debugIsObstacleDetected  = false;
    [SerializeField] private Vector3 debugTargetPosition      = Vector3.zero;
    [SerializeField] private Vector3 debugSpawnPosition       = Vector3.zero;
    [SerializeField] private string  debugHitType             = "None";
    [SerializeField] private float   debugHitDist             = 0f;
    [SerializeField] private string  debugLightState          = "None";
    [SerializeField] private bool    debugGrounded            = false;
    [SerializeField] private float   debugSlopeAngle          = 0f;
    [SerializeField] private float   debugGroundY             = 0f;
    [SerializeField] private string  debugGroundSource        = "None";
    [SerializeField] private int     debugWheelHits           = 0;
    [SerializeField] private bool    debugInNavMeshRecovery   = false;

    private Color _gizmoColor = Color.green;

    // =========================================================================
    //  SPAWN FACING DIRECTION
    //
    //  FIX v5.2: Average first N waypoint steps instead of just step [1]-[0].
    //  A single first NavMesh corner can be slightly behind the node on tight
    //  bends, giving a near-180° facing for that route. Averaging 3 steps
    //  produces a stable road-aligned heading for all node placements.
    // =========================================================================

    public Quaternion GetSpawnFacingDirection()
    {
        if (denseWaypoints != null && denseWaypoints.Count >= 2)
        {
            // Average direction of first min(3, count-1) waypoint steps.
            // Weight each step by its length so micro-steps don't dominate.
            int     samples  = Mathf.Min(3, denseWaypoints.Count - 1);
            Vector3 weightedDir = Vector3.zero;
            float   totalLen    = 0f;

            for (int i = 0; i < samples; i++)
            {
                Vector3 step = denseWaypoints[i + 1] - denseWaypoints[i];
                step.y = 0f;
                float len = step.magnitude;
                if (len > 0.01f)
                {
                    weightedDir += step.normalized * len;
                    totalLen    += len;
                }
            }

            if (totalLen > 0.01f)
            {
                Vector3 dir = (weightedDir / totalLen);
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    return Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        // Fallback — use currentTarget set in ApplyRouteResult
        if (hasTarget)
        {
            Vector3 dir = currentTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        return transform.rotation;
    }

    // =========================================================================
    //  SYNC SPAWN ROTATION  (NEW in v5.2)
    //
    //  Called by CentralizedNavigationSystem immediately after setting
    //  transform.rotation and rb.rotation to the correct spawn heading.
    //
    //  Without this, _smoothedSlopeTilt retains identity (Quaternion.identity)
    //  from Initialize().  The very first FixedUpdate call to MoveVehicle()
    //  then sees _smoothedSlopeTilt = identity and tries to Slerp from identity
    //  → correctHeading, causing the visible "spawn zigzag" during the first
    //  0.3 s of the vehicle's life.
    //
    //  With this call, _smoothedSlopeTilt already equals the correct heading
    //  before the first FixedUpdate runs — no initial rotation fight.
    // =========================================================================

    public void SyncSpawnRotation()
    {
        _smoothedSlopeTilt = transform.rotation;
        if (rb != null) rb.rotation = transform.rotation;
    }

    // =========================================================================
    //  INITIALIZE
    // =========================================================================

    public void Initialize(CentralizedNavigationSystem navSys,
                           int startNodeID,
                           CentralizedNavigationSystem.VehicleGroundConfig groundCfg,
                           CentralizedNavigationSystem.VehicleSharedConfig sharedCfg)
    {
        navSystem = navSys;

        // Ground config
        groundLayer        = groundCfg.groundLayer;
        groundRayUpOffset  = groundCfg.groundRayUpOffset;
        groundRayDistance  = groundCfg.groundRayDistance;
        rideHeight         = groundCfg.rideHeight;
        groundSnapStrength = groundCfg.groundSnapStrength;
        slopeTiltSpeed     = groundCfg.slopeTiltSpeed;
        hillClimbBoost     = groundCfg.hillClimbBoost;

        // Movement
        maxSpeed        = sharedCfg.speed * UnityEngine.Random.Range(0.85f, 1.15f);
        turnSpeed       = sharedCfg.turnSpeed;
        speedSmoothTime = sharedCfg.speedSmoothTime;

        // Waypoint
        waypointReachDistanceXZ = sharedCfg.waypointReachDistanceXZ;
        waypointReachDistanceY  = sharedCfg.waypointReachDistanceY;
        minAdvanceInterval      = sharedCfg.minAdvanceInterval;

        // Detection
        detectionLayerMask       = sharedCfg.detectionLayerMask;
        npcVehicleLayer          = sharedCfg.npcVehicleLayer;
        playerVehicleLayer       = sharedCfg.playerVehicleLayer;
        trafficLightLayer        = sharedCfg.trafficLightLayer;
        detectionRange           = sharedCfg.detectionRange;
        vehicleStoppingDistance  = sharedCfg.vehicleStoppingDistance;
        obstacleStoppingDistance = sharedCfg.obstacleStoppingDistance;
        trafficLightStopDistance = sharedCfg.trafficLightStopDistance;
        maxRedLightWaitTime      = sharedCfg.maxRedLightWaitTime;

        // Stuck
        maxStuckFrames         = sharedCfg.maxStuckFrames;
        stuckMovementThreshold = sharedCfg.stuckMovementThreshold;
        maxPathRecalculations  = sharedCfg.maxPathRecalculations;

        showDebugGizmos = sharedCfg.showDebugGizmos;

        // ── Rigidbody ─────────────────────────────────────────────────────────
        rb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        rb.mass            = 1200f;
        rb.linearDamping   = 4f;
        rb.angularDamping  = 10f;
        rb.interpolation   = RigidbodyInterpolation.Interpolate;
        rb.useGravity      = false;
        rb.constraints     = RigidbodyConstraints.None;

        // ── Node anchor ───────────────────────────────────────────────────────
        sourceNodeID      = navSystem.nodeMap.ContainsKey(startNodeID)
                              ? startNodeID : navSystem.GetRandomNode();
        currentNodeID     = sourceNodeID;
        destinationNodeID = -1;

        lastValidPosition  = transform.position;
        debugSpawnPosition = transform.position;

        _gizmoColor        = new Color(UnityEngine.Random.value,
                                       UnityEngine.Random.value,
                                       UnityEngine.Random.value);
        _smoothedSlopeTilt = transform.rotation;

        CacheWheelRestRotations();

        Debug.Log($"[{gameObject.name}] Initialized | Node={sourceNodeID} " +
                  $"Speed={maxSpeed:F1} m/s | WheelRefs: " +
                  $"FL={wheelFL != null} FR={wheelFR != null} " +
                  $"RL={wheelRL != null} RR={wheelRR != null} | " +
                  $"SteerPivots: FL={wheelFL_Steer != null} FR={wheelFR_Steer != null}");

        PickNewDestinationAndBuildRoute();
    }

    // =========================================================================
    //  CACHE WHEEL REST ROTATIONS
    // =========================================================================

    private void CacheWheelRestRotations()
    {
        Transform steerPivotFL = ResolveSteerPivot(wheelFL, wheelFL_Steer);
        Transform steerPivotFR = ResolveSteerPivot(wheelFR, wheelFR_Steer);

        _restFL = steerPivotFL != null ? steerPivotFL.localRotation : Quaternion.identity;
        _restFR = steerPivotFR != null ? steerPivotFR.localRotation : Quaternion.identity;
    }

    private static Transform ResolveSteerPivot(Transform spinTransform, Transform explicitPivot)
    {
        if (explicitPivot != null) return explicitPivot;
        if (spinTransform  != null) return spinTransform.parent;
        return null;
    }

    private void OnDestroy()
    {
        if (navSystem != null && sourceNodeID != -1 && destinationNodeID != -1)
            navSystem.ReleaseRoute(sourceNodeID, destinationNodeID);
    }

    // =========================================================================
    //  FIXED UPDATE
    // =========================================================================

    private void FixedUpdate()
    {
        if (navSystem == null || !hasTarget) return;
        if (rb == null || rb.isKinematic) return;

        _advancedThisFrame = false;

        SampleGround();
        RunDetection();

        // ── Waypoint reach ────────────────────────────────────────────────────
        float distXZ = HorizontalDistance(transform.position, currentTarget);
        Vector3 toWpXZ = new Vector3(currentTarget.x - transform.position.x,
                                     0f,
                                     currentTarget.z - transform.position.z);

        angleToCurrentWaypoint = toWpXZ.sqrMagnitude > 0.01f
            ? Vector3.Angle(new Vector3(transform.forward.x, 0f, transform.forward.z), toWpXZ)
            : 0f;

        if (distXZ < waypointReachDistanceXZ
         && (angleToCurrentWaypoint < 90f || distXZ < 2f)
         && Time.time - lastAdvanceTime >= minAdvanceInterval)
            AdvanceWaypoint();

        // ── Speed ─────────────────────────────────────────────────────────────
        bool shouldStop = ShouldStop();
        isStopped = shouldStop;

        float slopeFactor = 1f;
        if (!shouldStop && isGrounded && debugSlopeAngle > 5f)
        {
            float yDiff = currentTarget.y - transform.position.y;
            if (yDiff > 0.5f)
                slopeFactor = Mathf.Lerp(1f, 0.72f, Mathf.Clamp01(debugSlopeAngle / 35f));
        }

        targetSpeed  = shouldStop ? 0f : maxSpeed * slopeFactor;
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed,
                                        ref speedSmoothVelocity, speedSmoothTime);

        MoveVehicle();
        UpdateWheelVisuals();

        // ── Stuck detection ───────────────────────────────────────────────────
        if (!shouldStop)
        {
            stuckCounter++;

            if (stuckCounter % 10 == 0)
            {
                float moved = HorizontalDistance(transform.position, lastValidPosition);

                if (moved < stuckMovementThreshold && currentSpeed > 0.5f)
                {
                    isStuck = debugIsStuck = true;
                    if (stuckCounter >= maxStuckFrames) RecoverFromStuck();
                }
                else
                {
                    stuckCounter      = 0;
                    isStuck           = debugIsStuck = false;
                    lastValidPosition = transform.position;
                }
            }
        }
        else
        {
            stuckCounter      = 0;
            isStuck           = debugIsStuck = false;
            lastValidPosition = transform.position;
        }

        debugInNavMeshRecovery = _inNavMeshRecovery;
        UpdateDebugInfo();
    }

    // =========================================================================
    //  GROUND SAMPLING
    // =========================================================================

    private void SampleGround()
    {
        // Cache wheel origins for gizmos BEFORE casting (so they're valid even on miss)
        if (wheelFL != null) _gizWheelOriginFL = wheelFL.position + Vector3.up * (wheelRadius * 0.6f);
        if (wheelFR != null) _gizWheelOriginFR = wheelFR.position + Vector3.up * (wheelRadius * 0.6f);
        if (wheelRL != null) _gizWheelOriginRL = wheelRL.position + Vector3.up * (wheelRadius * 0.6f);
        if (wheelRR != null) _gizWheelOriginRR = wheelRR.position + Vector3.up * (wheelRadius * 0.6f);

        _wFL_hit = CastWheelRay(wheelFL, out _wFL_pt);
        _wFR_hit = CastWheelRay(wheelFR, out _wFR_pt);
        _wRL_hit = CastWheelRay(wheelRL, out _wRL_pt);
        _wRR_hit = CastWheelRay(wheelRR, out _wRR_pt);

        _wheelHitCount = (_wFL_hit ? 1 : 0) + (_wFR_hit ? 1 : 0)
                       + (_wRL_hit ? 1 : 0) + (_wRR_hit ? 1 : 0);
        debugWheelHits = _wheelHitCount;

        if (_wheelHitCount >= 4)
        {
            Compute4WheelNormal();
            isGrounded = debugGrounded = true;
            debugGroundSource = "4-Wheel";
            debugGroundY = currentGroundY;
            debugSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            return;
        }
        if (_wheelHitCount == 3)
        {
            Compute3WheelNormal();
            isGrounded = debugGrounded = true;
            debugGroundSource = "3-Wheel";
            debugGroundY = currentGroundY;
            debugSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            return;
        }
        if (_wheelHitCount == 2)
        {
            Compute2WheelNormal();
            isGrounded = debugGrounded = true;
            debugGroundSource = "2-Wheel";
            debugGroundY = currentGroundY;
            debugSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            return;
        }
        if (_wheelHitCount == 1)
        {
            ComputeSingleWheelFallback();
            isGrounded = debugGrounded = true;
            debugGroundSource = "1-Wheel";
            debugGroundY = currentGroundY;
            debugSlopeAngle = 0f;
            return;
        }

        // Centre-body fallback
        Vector3 origin = transform.position + Vector3.up * groundRayUpOffset;
        float   dist   = groundRayDistance + groundRayUpOffset;

        // Cache for gizmos
        _gizCentreOrigin = origin;
        _gizCentreLen    = dist;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist,
                            groundLayer, QueryTriggerInteraction.Ignore))
        {
            isGrounded      = debugGrounded = true;
            groundNormal    = hit.normal;
            currentGroundY  = hit.point.y;
            groundHitPoint  = hit.point;
            debugGroundY    = currentGroundY;
            debugSlopeAngle = Vector3.Angle(Vector3.up, groundNormal);
            debugGroundSource = "CentreBody";
            _gizCentreHit   = true;
            _gizCentreHitPt = hit.point;

            if (showDebugRays) Debug.DrawLine(origin, hit.point, Color.yellow);
        }
        else
        {
            isGrounded = debugGrounded = false;
            groundNormal      = Vector3.up;
            debugSlopeAngle   = 0f;
            debugGroundSource = "Airborne";
            _gizCentreHit     = false;

            if (showDebugRays) Debug.DrawRay(origin, Vector3.down * dist, Color.red);
        }
    }

    private bool CastWheelRay(Transform wheel, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;
        if (wheel == null) return false;

        Vector3 origin = wheel.position + Vector3.up * (wheelRadius * 0.6f);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                            wheelRayDistance, groundLayer, QueryTriggerInteraction.Ignore))
        {
            hitPoint = hit.point;
            if (showDebugRays) Debug.DrawLine(origin, hit.point, Color.green);
            return true;
        }

        if (showDebugRays) Debug.DrawRay(origin, Vector3.down * wheelRayDistance, Color.red);
        return false;
    }

    private void Compute4WheelNormal()
    {
        Vector3 d1 = _wRR_pt - _wFL_pt;
        Vector3 d2 = _wFR_pt - _wRL_pt;
        Vector3 n  = Vector3.Cross(d1, d2).normalized;
        if (n.y < 0f) n = -n;

        groundNormal   = ClampNormal(n);
        currentGroundY = (_wFL_pt.y + _wFR_pt.y + _wRL_pt.y + _wRR_pt.y) * 0.25f;
        groundHitPoint = (_wFL_pt   + _wFR_pt   + _wRL_pt   + _wRR_pt  ) * 0.25f;
    }

    private void Compute3WheelNormal()
    {
        var pts = new List<Vector3>(3);
        if (_wFL_hit) pts.Add(_wFL_pt);
        if (_wFR_hit) pts.Add(_wFR_pt);
        if (_wRL_hit) pts.Add(_wRL_pt);
        if (_wRR_hit) pts.Add(_wRR_pt);

        Vector3 n = Vector3.Cross(pts[1] - pts[0], pts[2] - pts[0]).normalized;
        if (n.y < 0f) n = -n;

        groundNormal   = ClampNormal(n);
        currentGroundY = (pts[0].y + pts[1].y + pts[2].y) / 3f;
        groundHitPoint = (pts[0]   + pts[1]   + pts[2]  ) / 3f;
    }

    private void Compute2WheelNormal()
    {
        Vector3 a, b;
        if      (_wFL_hit && _wFR_hit) { a = _wFL_pt; b = _wFR_pt; }
        else if (_wRL_hit && _wRR_hit) { a = _wRL_pt; b = _wRR_pt; }
        else if (_wFL_hit && _wRL_hit) { a = _wFL_pt; b = _wRL_pt; }
        else if (_wFR_hit && _wRR_hit) { a = _wFR_pt; b = _wRR_pt; }
        else if (_wFL_hit && _wRR_hit) { a = _wFL_pt; b = _wRR_pt; }
        else                           { a = _wFR_pt; b = _wRL_pt; }

        Vector3 axis  = (b - a).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, axis).normalized;
        Vector3 n     = Vector3.Cross(axis, right).normalized;
        if (n.y < 0f) n = -n;

        groundNormal   = ClampNormal(n);
        currentGroundY = (a.y + b.y) * 0.5f;
        groundHitPoint = (a   + b  ) * 0.5f;
    }

    private void ComputeSingleWheelFallback()
    {
        Vector3 pt = _wFL_hit ? _wFL_pt : _wFR_hit ? _wFR_pt : _wRL_hit ? _wRL_pt : _wRR_pt;
        groundNormal   = Vector3.up;
        currentGroundY = pt.y;
        groundHitPoint = pt;
    }

    private static Vector3 ClampNormal(Vector3 n)
    {
        const float MAX_TILT = 40f;
        float angle = Vector3.Angle(Vector3.up, n);
        if (angle > MAX_TILT)
            n = Vector3.Slerp(Vector3.up, n, MAX_TILT / angle);
        return n.normalized;
    }

    // =========================================================================
    //  DETECTION
    // =========================================================================

    private void RunDetection()
    {
        _hitType     = HitType.None;
        _hitDistance = float.MaxValue;
        _hitObject   = null;

        Vector3 origin  = transform.position + Vector3.up * 1.2f;
        Vector3 rawFwd  = transform.forward;
        Vector3 forward = new Vector3(rawFwd.x,
                                      Mathf.Clamp(rawFwd.y, -0.35f, 0.35f),
                                      rawFwd.z).normalized;

        // ── Cache for gizmo drawing ───────────────────────────────────────────
        _gizDetectOrigin = origin;
        _gizDetectDir    = forward;
        _gizDetectRange  = detectionRange;
        _gizDetectHit    = false;

        RaycastHit[] hits = Physics.RaycastAll(origin, forward, detectionRange,
                                               detectionLayerMask);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            int layerBit = 1 << hit.collider.gameObject.layer;

            if ((layerBit & trafficLightLayer) != 0)
            {
                TrafficLightController tlc =
                    hit.collider.GetComponentInParent<TrafficLightController>();
                if (tlc != null) _currentLight = tlc;

                if (_currentLight != null &&
                    _currentLight.currentState != TrafficLightController.LightState.Green &&
                    _hitType == HitType.None)
                {
                    _hitType          = HitType.TrafficLight;
                    _hitDistance      = hit.distance;
                    _hitObject        = hit.collider.gameObject;
                    _gizDetectHit     = true;
                    _gizDetectHitPt   = hit.point;
                }
                continue;
            }

            HitType type;
            if      ((layerBit & npcVehicleLayer)    != 0) type = HitType.NpcVehicle;
            else if ((layerBit & playerVehicleLayer) != 0) type = HitType.PlayerVehicle;
            else                                            type = HitType.Obstacle;

            _hitType          = type;
            _hitDistance      = hit.distance;
            _hitObject        = hit.collider.gameObject;
            _gizDetectHit     = true;
            _gizDetectHitPt   = hit.point;
            break;
        }

        if (_hitType == HitType.None || _hitType == HitType.TrafficLight)
            TryClearStaleLight();

        if (showDebugGizmos || showDebugRays)
        {
            Color c;
            switch (_hitType)
            {
                case HitType.TrafficLight:   c = Color.cyan;   break;
                case HitType.NpcVehicle:
                case HitType.PlayerVehicle:
                    c = _hitDistance < vehicleStoppingDistance ? Color.red : Color.yellow; break;
                case HitType.Obstacle:       c = Color.red;    break;
                default:                     c = Color.green;  break;
            }
            Debug.DrawRay(origin, forward * detectionRange, c);
        }

        debugHitType            = _hitType.ToString();
        debugHitDist            = _hitType != HitType.None ? _hitDistance : 0f;
        debugLightState         = _currentLight != null
                                    ? _currentLight.currentState.ToString() : "None";
        debugIsObstacleDetected = (_hitType == HitType.Obstacle   ||
                                   _hitType == HitType.NpcVehicle ||
                                   _hitType == HitType.PlayerVehicle);
    }

    private void TryClearStaleLight()
    {
        if (_currentLight == null) return;
        if (Vector3.Distance(transform.position,
                             _currentLight.transform.position) > detectionRange * 1.5f)
        {
            _currentLight = null;
            _stoppedAtRed = false;
        }
    }

    // =========================================================================
    //  STOP DECISION
    // =========================================================================

    private bool ShouldStop()
    {
        switch (_hitType)
        {
            case HitType.NpcVehicle:
            case HitType.PlayerVehicle: return ShouldStopForVehicle();
            case HitType.Obstacle:      return _hitDistance < obstacleStoppingDistance;
            case HitType.TrafficLight:  return ShouldStopForLight();
            default:
                if (_currentLight != null &&
                    _currentLight.currentState == TrafficLightController.LightState.Green)
                    _stoppedAtRed = false;
                return false;
        }
    }

    private bool ShouldStopForVehicle()
    {
        if (_hitDistance > vehicleStoppingDistance) return false;
        if (_hitType == HitType.NpcVehicle && _hitObject != null)
        {
            TrafficVehicle ahead = _hitObject.transform.root.GetComponent<TrafficVehicle>()
                                ?? _hitObject.GetComponentInParent<TrafficVehicle>();
            if (ahead != null)
            {
                if (_hitDistance < vehicleStoppingDistance * 0.6f) return true;
                return ahead.isStopped || ahead.currentSpeed < 1f;
            }
        }
        return true;
    }

    private bool ShouldStopForLight()
    {
        if (_currentLight == null) return false;
        var state = _currentLight.currentState;

        if (state == TrafficLightController.LightState.Green)
        { _stoppedAtRed = false; return false; }

        if (_stoppedAtRed && Time.time - _redLightEntryTime > maxRedLightWaitTime)
        {
            _stoppedAtRed = false;
            Debug.LogWarning($"[{gameObject.name}] Red-light timeout — proceeding.");
            return false;
        }

        bool inStopZone = _hitDistance < trafficLightStopDistance;
        if (state == TrafficLightController.LightState.Yellow && !inStopZone) return false;

        if (!_stoppedAtRed && inStopZone)
        { _stoppedAtRed = true; _redLightEntryTime = Time.time; }

        return _stoppedAtRed || inStopZone;
    }

    // =========================================================================
    //  MOVE VEHICLE
    //
    //  FIX v5.2 — NavMesh lateral-snap guard
    //  ─────────────────────────────────────────────────────────────────────────
    //  Previously: NavMesh.SamplePosition(..., navMeshSampleRadius=4m, ...)
    //  could snap the car up to 4 m sideways onto baked pavement / kerb.
    //
    //  Fix: measure the XZ shift the NavMesh correction would apply.
    //  Only accept it when the shift is ≤ navMeshMaxLateralSnap (default 0.8 m).
    //  This keeps the car on the road surface without letting it drift sideways.
    // =========================================================================

    private void MoveVehicle()
    {
        if (!hasTarget || rb == null || rb.isKinematic) return;

        Vector3 toTargetXZ = new Vector3(currentTarget.x - transform.position.x,
                                         0f,
                                         currentTarget.z - transform.position.z);

        if (toTargetXZ.sqrMagnitude > 0.01f)
        {
            Quaternion targetYaw  = Quaternion.LookRotation(toTargetXZ, Vector3.up);
            Quaternion currentYaw = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);
            float      turnMult   = Mathf.Lerp(1f, 2.5f,
                                        Mathf.Clamp01(angleToCurrentWaypoint / 90f));
            Quaternion smoothYaw  = Quaternion.Slerp(currentYaw, targetYaw,
                                        Time.fixedDeltaTime * turnSpeed * turnMult);

            float yawDelta = Mathf.DeltaAngle(currentYaw.eulerAngles.y,
                                              smoothYaw.eulerAngles.y);
            currentSteerAngle = Mathf.Clamp(yawDelta * 4f, -maxSteerAngle, maxSteerAngle);

            Quaternion desiredRot;
            if (isGrounded && debugSlopeAngle > 0.5f)
            {
                Vector3 gFwd = Vector3.ProjectOnPlane(
                    smoothYaw * Vector3.forward, groundNormal);
                desiredRot = gFwd.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(gFwd.normalized, groundNormal)
                    : smoothYaw;
            }
            else
            {
                desiredRot = smoothYaw;
            }

            _smoothedSlopeTilt = Quaternion.Slerp(_smoothedSlopeTilt, desiredRot,
                                                   Time.fixedDeltaTime * slopeTiltSpeed);
        }

        rb.MoveRotation(_smoothedSlopeTilt);

        if (currentSpeed < 0.01f) { SnapYToGround(); return; }

        float align = Mathf.Max(
            Mathf.Clamp01(1f - angleToCurrentWaypoint / 90f) *
            Mathf.Clamp01(1f - angleToCurrentWaypoint / 90f), 0.02f);

        Vector3 flatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
        if (flatFwd.sqrMagnitude < 0.001f) flatFwd = Vector3.forward;
        flatFwd.Normalize();

        float   moveDist = currentSpeed * align * Time.fixedDeltaTime;
        Vector3 newXZPos = new Vector3(rb.position.x + flatFwd.x * moveDist,
                                       rb.position.y,
                                       rb.position.z + flatFwd.z * moveDist);

        // ── NavMesh road clamping — lateral-snap guard ────────────────────────
        // Only correct position if the NavMesh snap is small (≤ navMeshMaxLateralSnap).
        // A large correction means the nearest NavMesh point is on a different
        // surface (pavement, kerb) — discard it so we don't get pulled sideways.
        if (NavMesh.SamplePosition(newXZPos, out NavMeshHit navHit,
                                   navMeshSampleRadius, navMeshAreaMask))
        {
            float lateralShift = new Vector2(navHit.position.x - newXZPos.x,
                                              navHit.position.z - newXZPos.z).magnitude;
            if (lateralShift <= navMeshMaxLateralSnap)
            {
                newXZPos.x = navHit.position.x;
                newXZPos.z = navHit.position.z;
            }
            // else: discard — pavement-edge snap rejected
        }

        float finalY;
        if (isGrounded)
        {
            float desiredY = currentGroundY + rideHeight;
            float lerpT    = Mathf.Clamp01(groundSnapStrength * Time.fixedDeltaTime);
            finalY = Mathf.Lerp(rb.position.y, desiredY, lerpT);
            finalY = Mathf.Clamp(finalY, desiredY - 0.8f, desiredY + 0.8f);
        }
        else
        {
            finalY = rb.position.y - 9.81f * Time.fixedDeltaTime;
        }

        rb.MovePosition(new Vector3(newXZPos.x, finalY, newXZPos.z));
    }

    private void SnapYToGround()
    {
        if (!isGrounded || rb == null) return;
        float desiredY = currentGroundY + rideHeight;
        float lerpT    = Mathf.Clamp01(groundSnapStrength * Time.fixedDeltaTime);
        float newY     = Mathf.Lerp(rb.position.y, desiredY, lerpT);
        rb.MovePosition(new Vector3(rb.position.x, newY, rb.position.z));
    }

    // =========================================================================
    //  WHEEL VISUALS
    // =========================================================================

    private void UpdateWheelVisuals()
    {
        if (wheelRadius <= 0.001f) return;

        float degreesPerMeter = 360f / (2f * Mathf.PI * wheelRadius);
        float deltaDeg        = currentSpeed * Time.fixedDeltaTime * degreesPerMeter;
        Quaternion spinDelta  = Quaternion.AngleAxis(deltaDeg, wheelSpinAxis);

        SpinWheel(wheelRL, spinDelta);
        SpinWheel(wheelRR, spinDelta);

        SteerAndSpinFrontWheel(wheelFL, wheelFL_Steer, spinDelta, currentSteerAngle, _restFL);
        SteerAndSpinFrontWheel(wheelFR, wheelFR_Steer, spinDelta, currentSteerAngle, _restFR);
    }

    private static void SpinWheel(Transform spinTransform, Quaternion spinDelta)
    {
        if (spinTransform == null) return;
        spinTransform.localRotation *= spinDelta;
    }

    private static void SteerAndSpinFrontWheel(Transform spinTransform,
                                               Transform explicitSteerPivot,
                                               Quaternion spinDelta,
                                               float steerDeg,
                                               Quaternion restRot)
    {
        if (spinTransform == null) return;

        Transform steerPivot = explicitSteerPivot != null
            ? explicitSteerPivot
            : spinTransform.parent;

        if (steerPivot != null)
            steerPivot.localRotation = restRot * Quaternion.AngleAxis(steerDeg, Vector3.up);

        spinTransform.localRotation *= spinDelta;
    }

    // =========================================================================
    //  WAYPOINT ADVANCEMENT
    // =========================================================================

    private void AdvanceWaypoint()
    {
        if (_advancedThisFrame) return;
        _advancedThisFrame = true;
        lastAdvanceTime    = Time.time;
        waypointIndex++;

        // Clear NavMesh recovery flag once we've passed the injected segment
        if (_inNavMeshRecovery && waypointIndex >= _recoveryEndIndex)
        {
            _inNavMeshRecovery = false;
            Debug.Log($"[{gameObject.name}] ✅ NavMesh recovery segment complete — resuming normal route.");
        }

        if (waypointIndex >= denseWaypoints.Count)
        {
            Debug.Log($"[{gameObject.name}] ✅ Reached Node {destinationNodeID}");
            navSystem.ReleaseRoute(sourceNodeID, destinationNodeID);
            sourceNodeID  = destinationNodeID;
            currentNodeID = sourceNodeID;
            _inNavMeshRecovery = false;
            PickNewDestinationAndBuildRoute();
            return;
        }

        currentTarget = denseWaypoints[waypointIndex];
        hasTarget     = true;
    }

    // =========================================================================
    //  ROUTE BUILDING
    // =========================================================================

    private void PickNewDestinationAndBuildRoute()
    {
        if (navSystem == null) { Debug.LogError($"[{gameObject.name}] No NavSystem."); return; }
        ApplyRouteResult(navSystem.RequestRoute(sourceNodeID));
    }

    private void ApplyRouteResult(CentralizedNavigationSystem.RouteResult result)
    {
        if (!result.success)
        {
            Debug.LogError($"[{gameObject.name}] Route failed: {result.failReason}");
            hasTarget = false;
            return;
        }

        sourceNodeID       = result.sourceNodeID;
        destinationNodeID  = result.destinationNodeID;
        denseWaypoints     = result.waypoints;
        waypointIndex      = 1;
        pathRecalculations = 0;
        _inNavMeshRecovery = false;

        hasTarget     = denseWaypoints.Count > 1;
        currentTarget = hasTarget ? denseWaypoints[waypointIndex] : transform.position;

        string routeStr     = $"{sourceNodeID}→{destinationNodeID}";
        debugChainName      = routeStr;
        debugTotalWaypoints = denseWaypoints.Count;

        Debug.Log($"[{gameObject.name}] ══ NEW ROUTE ══ {routeStr} | {denseWaypoints.Count} wps");
    }

    // =========================================================================
    //  STUCK RECOVERY
    //
    //  FIX v5.2 — NavMesh live re-path injected as Step 2
    //  ─────────────────────────────────────────────────────────────────────────
    //  Recovery order:
    //    1. Skip ahead 3 waypoints (fast, zero cost — handles transient jams)
    //    2. NavMesh.CalculatePath from current pos to destination node
    //       → subdivide corners → inject into denseWaypoints
    //       This gives the car a LIVE road-aware path out of the stuck zone
    //       without a pool miss or full re-anchor.
    //    3. A* re-anchor to nearest LoS node + RequestReroute (existing logic)
    //    4. Full re-anchor (last resort, unchanged)
    // =========================================================================

    private void RecoverFromStuck()
    {
        pathRecalculations++;
        stuckCounter = 0;
        Debug.LogWarning($"[{gameObject.name}] ⚠ Stuck {pathRecalculations}/{maxPathRecalculations}");

        // ── Step 1: Skip 3 waypoints ahead ───────────────────────────────────
        int skip = Mathf.Min(waypointIndex + 3, denseWaypoints.Count - 1);
        if (skip > waypointIndex)
        {
            waypointIndex = skip;
            currentTarget = denseWaypoints[waypointIndex];
            if (rb != null)
                rb.MovePosition(rb.position +
                                (currentTarget - transform.position).normalized * 0.8f);
            return;
        }

        // ── Step 2: NavMesh live re-path to destination ───────────────────────
        // Calculate a fresh NavMesh path from where we actually are right now
        // to the destination node. Inject the corners as new waypoints so the
        // car has a valid, geometry-aware escape route.
        if (!_inNavMeshRecovery && navSystem != null &&
            navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            Vector3 destPos = navSystem.nodeMap[destinationNodeID].worldPosition;
            if (TryInjectNavMeshRecoveryPath(transform.position, destPos))
                return;
        }

        // ── Step 3: Re-anchor to nearest node + RequestReroute ────────────────
        if (pathRecalculations < maxPathRecalculations)
        {
            int anchor = FindNearestNodeLoS();
            navSystem.ReleaseRoute(sourceNodeID, destinationNodeID);
            if (anchor != -1) { sourceNodeID = anchor; currentNodeID = anchor; }
            ApplyRouteResult(navSystem.RequestReroute(currentNodeID, destinationNodeID));
            return;
        }

        // ── Step 4: Full re-anchor (last resort) ─────────────────────────────
        Debug.LogError($"[{gameObject.name}] Max recalculations — full re-anchor.");
        navSystem.ReleaseRoute(sourceNodeID, destinationNodeID);
        int nearest = FindNearestNodeLoS();
        if (nearest == -1) nearest = navSystem.GetClosestNode(transform.position);
        if (nearest != -1) { sourceNodeID = nearest; currentNodeID = nearest; }
        pathRecalculations = 0;
        PickNewDestinationAndBuildRoute();
    }

    // =========================================================================
    //  NAVMESH RECOVERY PATH INJECTION  (NEW in v5.2)
    //
    //  Computes a live NavMesh path from 'from' to 'to' and splices the
    //  resulting corners — subdivided to waypointSpacing — in front of the
    //  remaining route waypoints.
    //
    //  denseWaypoints layout after injection:
    //    [0 .. waypointIndex-1]      — already visited (unchanged)
    //    [waypointIndex .. recEnd-1] — fresh NavMesh recovery segment  ← NEW
    //    [recEnd .. end]             — original remaining route waypoints
    //
    //  The car follows the recovery segment, then seamlessly continues on
    //  the original pre-baked route. _recoveryEndIndex marks the boundary.
    // =========================================================================

    private bool TryInjectNavMeshRecoveryPath(Vector3 from, Vector3 to)
    {
        var nmPath = new NavMeshPath();
        bool ok = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, nmPath);

        if (!ok || nmPath.status == NavMeshPathStatus.PathInvalid)
        {
            Debug.LogWarning($"[{gameObject.name}] NavMesh recovery path invalid — skipping injection.");
            return false;
        }

        if (nmPath.corners.Length < 2) return false;

        // Subdivide NavMesh corners to the same spacing as baked waypoints
        // so the car's steering behaves identically to normal route following.
        const float spacing = 5f; // match typical maxWaypointSpacing
        var recoveryPts = new List<Vector3>();
        for (int i = 0; i < nmPath.corners.Length - 1; i++)
        {
            Vector3 a = nmPath.corners[i], b = nmPath.corners[i + 1];
            recoveryPts.Add(a + Vector3.up * 0.15f);
            int divs = Mathf.Max(1, Mathf.FloorToInt(Vector3.Distance(a, b) / spacing));
            for (int s = 1; s < divs; s++)
                recoveryPts.Add(Vector3.Lerp(a, b, (float)s / divs) + Vector3.up * 0.15f);
        }
        recoveryPts.Add(nmPath.corners[nmPath.corners.Length - 1] + Vector3.up * 0.15f);

        // Splice: keep visited waypoints, insert recovery, append remaining
        var spliced = new List<Vector3>();
        for (int i = 0; i < waypointIndex; i++)
            spliced.Add(denseWaypoints[i]);

        int insertStart = spliced.Count;
        spliced.AddRange(recoveryPts);
        int insertEnd = spliced.Count;

        // Append remaining original waypoints (skip the ones we already passed)
        for (int i = waypointIndex; i < denseWaypoints.Count; i++)
            spliced.Add(denseWaypoints[i]);

        denseWaypoints     = spliced;
        waypointIndex      = insertStart;  // start of recovery segment
        _recoveryEndIndex  = insertEnd;
        _inNavMeshRecovery = true;

        currentTarget = denseWaypoints.Count > waypointIndex
            ? denseWaypoints[waypointIndex] : transform.position;
        hasTarget = true;

        Debug.Log($"[{gameObject.name}] 🔄 NavMesh recovery injected — " +
                  $"{recoveryPts.Count} pts  (total WPs: {denseWaypoints.Count})");
        return true;
    }

    private int FindNearestNodeLoS()
    {
        float best = float.MaxValue; int bestNode = -1;
        int   mask = detectionLayerMask & ~groundLayer;

        foreach (var kvp in navSystem.nodeMap)
        {
            if (kvp.Value == null) continue;
            Vector3 npos = kvp.Value.transform.position;
            float   dist = Vector3.Distance(transform.position, npos);
            if (dist > 60f) continue;

            Vector3 from = transform.position + Vector3.up * 1.5f;
            Vector3 to   = npos + Vector3.up * 1f;
            float   len  = Vector3.Distance(from, to);

            if (!Physics.Raycast(from, (to - from).normalized, len, mask) && dist < best)
            { best = dist; bestNode = kvp.Key; }
        }
        return bestNode;
    }

    // =========================================================================
    //  DEBUG INFO
    // =========================================================================

    private void UpdateDebugInfo()
    {
        debugCurrentWaypoint    = waypointIndex;
        debugNextWaypointIndex  = Mathf.Min(waypointIndex + 1,
                                            (denseWaypoints?.Count ?? 1) - 1);
        debugCurrentNodeID      = currentNodeID;
        debugTotalWaypoints     = denseWaypoints?.Count ?? 0;
        debugCurrentSpeed       = currentSpeed;
        debugProgressPct        = debugTotalWaypoints > 0
            ? (float)waypointIndex / debugTotalWaypoints * 100f : 0f;
        debugDistanceToWaypoint = hasTarget
            ? Vector3.Distance(transform.position, currentTarget) : 0f;
        debugTargetPosition     = currentTarget;

        if (navSystem != null && navSystem.nodeMap.ContainsKey(destinationNodeID))
            debugDistToDest = Vector3.Distance(transform.position,
                              navSystem.nodeMap[destinationNodeID].worldPosition);

        if (denseWaypoints != null && debugNextWaypointIndex < denseWaypoints.Count)
        {
            debugNextNodeID = -1;
            Vector3 nwp = denseWaypoints[debugNextWaypointIndex];
            float best  = 2f;
            foreach (var kvp in navSystem.nodeMap)
            {
                if (kvp.Value == null) continue;
                float d = Vector3.Distance(nwp, kvp.Value.transform.position);
                if (d < best) { best = d; debugNextNodeID = kvp.Key; }
            }
        }
    }

    // =========================================================================
    //  GIZMOS  —  Scene-view visualization
    //
    //  LEGEND:
    //  ─────────────────────────────────────────────────────────────────────────
    //  DETECTION RAY
    //    Green  ──────────────────────────────►   No hit (full range)
    //    Yellow ─────────────────────►  ◆         Vehicle detected (far zone)
    //    Red    ──────────►  ◆                     Vehicle/obstacle (stop zone)
    //    Cyan   ─────────────────►  ◆             Traffic light
    //    ◆  = diamond at hit point, size ∝ distance
    //
    //  WHEEL RAYS  (per-wheel downward cast)
    //    ┃ green line + ● green sphere   = wheel ray HIT ground
    //    ┃ red   line                    = wheel ray MISS (full wheelRayDistance)
    //    ○ lime wire circle at origin    = ray start (wheel hub height)
    //
    //  CENTRE-BODY FALLBACK RAY
    //    ┃ yellow/orange line + ● sphere = hit  (only visible when ≤ 3 wheels hit)
    //    ┃ magenta dashed line           = miss (airborne)
    //
    //  WAYPOINTS
    //    ● cyan   sphere  = current target waypoint  (drives toward this)
    //    ● green  sphere  = WP+1  (next after current)
    //    ● lime   sphere  = WP+2
    //    ● yellow sphere  = WP+3  … up to gizmoUpcomingWaypointCount
    //    ──── thin coloured line = remaining route  (gizmoShowFullRoute)
    //    ○ orange dashed = recovery segment waypoints
    // =========================================================================

#if UNITY_EDITOR

    private void OnDrawGizmos()
    {
        if (!gizmosEnabled || !Application.isPlaying) return;

        DrawWheelRayGizmos();
        DrawCentreBodyRayGizmo();
        DrawDetectionRayGizmo();
        DrawWaypointGizmos();
        DrawStateGizmos();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  WHEEL RAYS  —  live raycast per draw call
    //
    //  WHY LIVE, NOT CACHED:
    //    On sloped / hilly roads the car body tilts every render frame.
    //    The wheel Transform.position moves continuously with the slope,
    //    so any hit-point cached in FixedUpdate is already wrong by the
    //    time OnDrawGizmos fires (render rate ≠ physics rate).
    //
    //    The ray ORIGIN and LENGTH are always the same formula as
    //    CastWheelRay() uses, so this is a true mirror of the physics cast.
    //    The only difference: we use Physics.Raycast (single hit) instead
    //    of RaycastAll — that is fine for a debug draw and costs nothing
    //    at editor frame rates.
    // ─────────────────────────────────────────────────────────────────────────
    private void DrawWheelRayGizmos()
    {
        if (!gizmoShowWheelRays) return;

        DrawOneWheelRayLive("FL", wheelFL);
        DrawOneWheelRayLive("FR", wheelFR);
        DrawOneWheelRayLive("RL", wheelRL);
        DrawOneWheelRayLive("RR", wheelRR);
    }

    // Re-casts the wheel ray this instant from the wheel's CURRENT world position.
    // Origin and distance are identical to CastWheelRay() so the gizmo
    // exactly matches what the physics loop computed last FixedUpdate.
    private void DrawOneWheelRayLive(string label, Transform wheelTf)
    {
        if (wheelTf == null) return;

        // Same origin formula as CastWheelRay()
        Vector3 origin = wheelTf.position + Vector3.up * (wheelRadius * 0.6f);

        bool hit = Physics.Raycast(
            origin, Vector3.down, out RaycastHit rh,
            wheelRayDistance, groundLayer, QueryTriggerInteraction.Ignore);

        if (hit)
        {
            // ── Green ray: origin → exact hit point on slope surface ──────────
            Gizmos.color = new Color(0.15f, 1f, 0.25f, 1f);
            Gizmos.DrawLine(origin, rh.point);

            // ── Small solid sphere exactly at the ground contact point ─────────
            // This moves with the slope every frame — always accurate
            Gizmos.color = new Color(0.1f, 1f, 0.2f, 1f);
            Gizmos.DrawSphere(rh.point, 0.08f);

            // ── Thin circle at ray origin (wheel hub height marker) ───────────
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.45f);
            DrawWireCircleXZ(origin, 0.13f);
        }
        else
        {
            // ── Red ray: full wheelRayDistance downward, nothing hit ──────────
            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.8f);
            Gizmos.DrawLine(origin, origin + Vector3.down * wheelRayDistance);

            // ── Small red circle at origin to mark the missed wheel ───────────
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.3f);
            DrawWireCircleXZ(origin, 0.13f);
        }

        // FL / FR / RL / RR label beside the ray origin
        if (gizmoShowWaypointLabels)
            UnityEditor.Handles.Label(
                origin + Vector3.up * 0.12f + Vector3.right * 0.16f,
                label,
                new GUIStyle
                {
                    normal    = new GUIStyleState
                    {
                        textColor = hit
                            ? new Color(0.15f, 0.95f, 0.35f)
                            : new Color(1f,    0.3f,  0.3f)
                    },
                    fontSize  = 9,
                    fontStyle = FontStyle.Bold
                });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CENTRE-BODY FALLBACK RAY
    //  Only meaningful when ≤3 wheels hit (shown with a warning colour)
    // ─────────────────────────────────────────────────────────────────────────
    private void DrawCentreBodyRayGizmo()
    {
        if (!gizmoShowCentreBodyRay) return;
        // Only draw when the fallback was actually used this frame
        if (debugGroundSource != "CentreBody" && debugGroundSource != "Airborne") return;

        if (_gizCentreHit)
        {
            // Orange/yellow line — visible "fallback active" warning
            Gizmos.color = new Color(1f, 0.75f, 0.0f, 0.85f);
            Gizmos.DrawLine(_gizCentreOrigin, _gizCentreHitPt);
            Gizmos.DrawSphere(_gizCentreHitPt, 0.1f);

            // Label
            if (gizmoShowWaypointLabels)
                UnityEditor.Handles.Label(_gizCentreOrigin + Vector3.up * 0.15f,
                    "CentreBody",
                    new GUIStyle { normal = new GUIStyleState
                        { textColor = new Color(1f, 0.8f, 0f) }, fontSize = 9 });
        }
        else
        {
            // Airborne — magenta full-length miss
            Gizmos.color = new Color(1f, 0f, 1f, 0.55f);
            Gizmos.DrawLine(_gizCentreOrigin,
                            _gizCentreOrigin + Vector3.down * _gizCentreLen);

            if (gizmoShowWaypointLabels)
                UnityEditor.Handles.Label(_gizCentreOrigin + Vector3.up * 0.15f,
                    "AIRBORNE",
                    new GUIStyle { normal = new GUIStyleState
                        { textColor = Color.magenta }, fontSize = 9,
                        fontStyle = FontStyle.Bold });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  DETECTION RAY
    //  Full-length ray always drawn.  Color + hit diamond show current state.
    // ─────────────────────────────────────────────────────────────────────────
    private void DrawDetectionRayGizmo()
    {
        if (!gizmoShowDetectionRay) return;

        // Pick colour and stopping-zone threshold
        Color rayColor;
        Color hitColor;
        float stopDist = vehicleStoppingDistance;

        switch (_hitType)
        {
            case HitType.NpcVehicle:
            case HitType.PlayerVehicle:
                bool inStop = _hitDistance < stopDist;
                rayColor = inStop ? new Color(1f, 0.15f, 0.15f, 0.9f)
                                  : new Color(1f, 0.85f, 0f, 0.85f);
                hitColor = inStop ? Color.red : Color.yellow;
                break;
            case HitType.Obstacle:
                rayColor = new Color(1f, 0.15f, 0.15f, 0.9f);
                hitColor = Color.red;
                break;
            case HitType.TrafficLight:
                rayColor = new Color(0.2f, 0.9f, 1f, 0.85f);
                hitColor = Color.cyan;
                break;
            default:
                rayColor = new Color(0.2f, 1f, 0.2f, 0.6f);
                hitColor = Color.green;
                break;
        }

        // Full-length ray line
        Gizmos.color = rayColor;
        Gizmos.DrawLine(_gizDetectOrigin,
                        _gizDetectOrigin + _gizDetectDir * _gizDetectRange);

        // Arrowhead at end of ray (small cross)
        Vector3 tip    = _gizDetectOrigin + _gizDetectDir * _gizDetectRange;
        Vector3 right  = Vector3.Cross(_gizDetectDir, Vector3.up).normalized * 0.3f;
        Vector3 up     = Vector3.up * 0.3f;
        Gizmos.DrawLine(tip - right, tip + right);
        Gizmos.DrawLine(tip - up,   tip + up);

        // Stopping-distance indicator on the ray
        Vector3 stopPt = _gizDetectOrigin + _gizDetectDir * stopDist;
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
        DrawWireCircleXZ(stopPt, 0.35f);
        if (gizmoShowWaypointLabels)
            UnityEditor.Handles.Label(stopPt + Vector3.up * 0.4f,
                $"stop {stopDist:F1}m",
                new GUIStyle { normal = new GUIStyleState
                    { textColor = new Color(1f, 0.5f, 0.1f) }, fontSize = 8 });

        // Hit-point diamond + distance text
        if (_gizDetectHit)
        {
            Gizmos.color = hitColor;
            DrawDiamond(_gizDetectHitPt, 0.28f);
            Gizmos.DrawLine(_gizDetectOrigin, _gizDetectHitPt);

            if (gizmoShowWaypointLabels)
            {
                string hitLabel = $"{_hitType}  {_hitDistance:F1} m";
                UnityEditor.Handles.Label(_gizDetectHitPt + Vector3.up * 0.55f,
                    hitLabel,
                    new GUIStyle
                    {
                        normal    = new GUIStyleState { textColor = hitColor },
                        fontSize  = 10,
                        fontStyle = FontStyle.Bold,
                    });
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  WAYPOINT GIZMOS
    //  Current WP + next N upcoming WPs + full route path
    // ─────────────────────────────────────────────────────────────────────────
    private void DrawWaypointGizmos()
    {
        if (denseWaypoints == null || denseWaypoints.Count < 2) return;

        // ── Full remaining route path ─────────────────────────────────────────
        if (gizmoShowFullRoute)
        {
            for (int i = waypointIndex; i < denseWaypoints.Count - 1; i++)
            {
                bool isRecovery = _inNavMeshRecovery && i >= waypointIndex && i < _recoveryEndIndex;
                float alpha     = Mathf.Lerp(0.7f, 0.2f,
                                    (float)(i - waypointIndex) /
                                    Mathf.Max(1, denseWaypoints.Count - waypointIndex));

                if (isRecovery)
                    Gizmos.color = new Color(1f, 0.55f, 0f, alpha + 0.2f);
                else
                    Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, alpha);

                Gizmos.DrawLine(denseWaypoints[i]     + Vector3.up * 0.3f,
                                denseWaypoints[i + 1] + Vector3.up * 0.3f);
            }
        }

        // ── Already visited path (faint grey) ────────────────────────────────
        for (int i = 0; i < Mathf.Min(waypointIndex, denseWaypoints.Count - 1); i++)
        {
            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.18f);
            Gizmos.DrawLine(denseWaypoints[i]     + Vector3.up * 0.25f,
                            denseWaypoints[i + 1] + Vector3.up * 0.25f);
        }

        // ── Current target waypoint ───────────────────────────────────────────
        if (gizmoShowCurrentWaypoint && hasTarget)
        {
            Vector3 cur = currentTarget + Vector3.up * 0.3f;

            // Pulsing ring around current target
            float pulse = 0.25f + 0.12f * Mathf.Sin(Time.time * 5f);
            Gizmos.color = isStopped
                ? new Color(1f, 0.2f, 0.2f, 0.9f)
                : new Color(0.1f, 0.9f, 1f, 0.95f);
            Gizmos.DrawWireSphere(cur, pulse);
            Gizmos.DrawSphere(cur, 0.12f);

            // Line from vehicle to current target
            Gizmos.color = new Color(0.1f, 0.9f, 1f, 0.55f);
            Gizmos.DrawLine(transform.position + Vector3.up * 1.0f, cur);

            // Reach-distance circle on the ground
            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            DrawWireCircleXZ(currentTarget, waypointReachDistanceXZ);

            if (gizmoShowWaypointLabels)
                DrawWaypointLabel(cur, "WP", isStopped
                    ? new Color(1f, 0.3f, 0.3f) : new Color(0.2f, 0.95f, 1f), 11, true);
        }

        // ── Upcoming waypoints (WP+1, WP+2 …) ────────────────────────────────
        int showCount = Mathf.Clamp(gizmoUpcomingWaypointCount, 0, 5);

        // Palette: cyan → green → lime → yellow → orange
        Color[] upcomingColors =
        {
            new Color(0.1f, 0.9f,  0.3f, 0.95f),  // WP+1  bright green
            new Color(0.6f, 1f,    0.1f, 0.9f),   // WP+2  lime
            new Color(1f,   0.95f, 0.1f, 0.85f),  // WP+3  yellow
            new Color(1f,   0.65f, 0.1f, 0.8f),   // WP+4  orange
            new Color(1f,   0.35f, 0.7f, 0.75f),  // WP+5  pink
        };

        float[] sphereRadii = { 0.30f, 0.24f, 0.20f, 0.17f, 0.15f };

        for (int n = 1; n <= showCount; n++)
        {
            int idx = waypointIndex + n;
            if (idx >= denseWaypoints.Count) break;

            Vector3 wp  = denseWaypoints[idx] + Vector3.up * 0.3f;
            Color   col = upcomingColors[n - 1];
            float   rad = sphereRadii[n - 1];

            // Solid + wire sphere
            Gizmos.color = col;
            Gizmos.DrawSphere(wp, rad * 0.5f);
            Gizmos.DrawWireSphere(wp, rad);

            // Vertical pole so it's visible even when buried under terrain
            Gizmos.color = new Color(col.r, col.g, col.b, 0.35f);
            Gizmos.DrawLine(denseWaypoints[idx], wp + Vector3.up * 0.6f);

            // Recovery tint — orange halo
            if (_inNavMeshRecovery && idx < _recoveryEndIndex)
            {
                Gizmos.color = new Color(1f, 0.55f, 0f, 0.45f);
                Gizmos.DrawWireSphere(wp, rad * 1.5f);
            }

            if (gizmoShowWaypointLabels)
                DrawWaypointLabel(wp + Vector3.up * (rad + 0.15f),
                    $"WP+{n}", col, 10, false);
        }

        // ── Destination node marker ───────────────────────────────────────────
        if (navSystem != null && navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            Vector3 dp = navSystem.nodeMap[destinationNodeID].worldPosition;
            Gizmos.color = new Color(1f, 0f, 1f, 0.55f);
            Gizmos.DrawWireSphere(dp + Vector3.up * 0.5f, 1.4f);
            Gizmos.DrawLine(dp, dp + Vector3.up * 2.8f);

            if (gizmoShowWaypointLabels)
                DrawWaypointLabel(dp + Vector3.up * 3.1f,
                    $"DEST  Node {destinationNodeID}",
                    new Color(1f, 0.3f, 1f), 10, false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  STATE GIZMOS  (stuck flash, ground normal, NavMesh radius, traffic light)
    // ─────────────────────────────────────────────────────────────────────────
    private void DrawStateGizmos()
    {
        // ── Ground normal arrow ───────────────────────────────────────────────
        if (isGrounded)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
            Gizmos.DrawRay(groundHitPoint, groundNormal * 1.5f);
        }

        // ── NavMesh sample radius (semi-transparent disc) ─────────────────────
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.07f);
        DrawWireCircleXZ(transform.position, navMeshSampleRadius);

        // ── Stuck flash ───────────────────────────────────────────────────────
        if (isStuck)
        {
            float flash = (Time.time * 4f) % 1f < 0.5f ? 1f : 0f;
            Gizmos.color = new Color(1f, 0.45f * flash, 0f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 0.9f);
        }

        // ── Traffic light link ────────────────────────────────────────────────
        if (_currentLight != null)
        {
            Gizmos.color = _stoppedAtRed ? Color.red : Color.green;
            Gizmos.DrawWireSphere(_currentLight.transform.position + Vector3.up, 0.9f);
        }

        // ── Hit-object link ───────────────────────────────────────────────────
        if (_hitObject != null && _hitType != HitType.None)
        {
            switch (_hitType)
            {
                case HitType.NpcVehicle:
                case HitType.PlayerVehicle:
                    Gizmos.color = _hitDistance < vehicleStoppingDistance
                                     ? Color.red : Color.yellow; break;
                case HitType.TrafficLight: Gizmos.color = Color.cyan; break;
                default:                   Gizmos.color = Color.red;  break;
            }
            Gizmos.DrawWireSphere(_hitObject.transform.position + Vector3.up * 0.5f, 0.35f);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  OnDrawGizmosSelected — rich Inspector overlay label (unchanged behaviour)
    // ─────────────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        string status = isStuck ? "STUCK" : (isStopped ? "STOPPED" : "MOVING");
        if (_inNavMeshRecovery) status += " [NM-RECOVERY]";
        string detail = _hitType != HitType.None ? $" [{_hitType} @ {debugHitDist:F1}m]" : "";
        if (_stoppedAtRed) detail = " [RED LIGHT]";

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 6f,
            $"{gameObject.name}  {status}{detail}\n" +
            $"Route: {sourceNodeID} → {destinationNodeID}\n" +
            $"WP: {debugCurrentWaypoint}/{debugTotalWaypoints}  ({debugProgressPct:F0}%)\n" +
            $"Dist to dest: {debugDistToDest:F1} m\n" +
            $"Speed: {debugCurrentSpeed:F1} m/s  ({debugCurrentSpeed * 3.6f:F0} km/h)\n" +
            $"Steer: {currentSteerAngle:F1}°\n" +
            $"Detection: {debugHitType}  @ {debugHitDist:F1} m\n" +
            $"Light: {debugLightState}\n" +
            $"Grounded: {debugGrounded}  [{debugGroundSource}]  Slope: {debugSlopeAngle:F1}°\n" +
            $"Wheel Hits: {debugWheelHits}/4  Ground Y: {debugGroundY:F2}\n" +
            $"NavMesh radius: {navMeshSampleRadius:F1} m  LatSnap≤{navMeshMaxLateralSnap:F1} m\n" +
            $"Recalcs: {pathRecalculations}/{maxPathRecalculations}",
            new GUIStyle
            {
                normal    = new GUIStyleState { textColor = Color.white },
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
            });
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  GIZMO HELPERS
    // ─────────────────────────────────────────────────────────────────────────

    /// Draws a flat horizontal circle in XZ at 'centre' with radius 'r'.
    private static void DrawWireCircleXZ(Vector3 centre, float r, int segments = 24)
    {
        float step = 360f / segments;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i * step * Mathf.Deg2Rad;
            float a1 = (i + 1) * step * Mathf.Deg2Rad;
            Gizmos.DrawLine(
                centre + new Vector3(Mathf.Cos(a0) * r, 0f, Mathf.Sin(a0) * r),
                centre + new Vector3(Mathf.Cos(a1) * r, 0f, Mathf.Sin(a1) * r));
        }
    }

    /// Draws a 3-axis diamond (octahedron cross) at 'pos' for hit-point markers.
    private static void DrawDiamond(Vector3 pos, float size)
    {
        Gizmos.DrawLine(pos + Vector3.up    * size, pos - Vector3.up    * size);
        Gizmos.DrawLine(pos + Vector3.right * size, pos - Vector3.right * size);
        Gizmos.DrawLine(pos + Vector3.forward * size, pos - Vector3.forward * size);
    }

    /// Draws a Scene-view label at 'pos'.
    private static void DrawWaypointLabel(Vector3 pos, string text,
                                           Color col, int fontSize, bool bold)
    {
        UnityEditor.Handles.Label(pos, text,
            new GUIStyle
            {
                normal    = new GUIStyleState { textColor = col },
                fontSize  = fontSize,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
            });
    }

#endif  // UNITY_EDITOR

    // =========================================================================
    //  UTILITY
    // =========================================================================

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}