// ============================================================================
//  TRAFFIC VEHICLE  ·  v5.1  —  FIXED WHEEL STEER (EULER CORRUPTION)
//
//  FIXES vs v5.0:
//  ─────────────────────────────────────────────────────────────────────────
//  BUG 1 — "Truck cab front shrinks / collapses at runtime"
//  ROOT CAUSE:
//    SpinWheel() was writing:
//        wheel.parent.localEulerAngles.y = steerDeg;
//    When the WheelController parent has ANY non-zero X or Z local rotation
//    baked in (camber, caster, toe — very common on truck rigs), Unity's
//    euler → quaternion round-trip silently zeros those axes, collapsing
//    the mesh geometry.  Cars work fine because their wheel pivots are flat.
//  FIX:
//    Cache each steer-pivot's ORIGINAL localRotation at Initialize() time
//    (CacheWheelRestRotations).  Each frame compose steer as:
//        steerPivot.localRotation = _restRot * Quaternion.AngleAxis(deg, up)
//    Pure quaternion — no euler read-back, no axis corruption.
//
//  BUG 2 — "Truck wheels sit below the road surface"
//  ROOT CAUSE:
//    The wheelFL/FR/RL/RR fields were typically assigned to the LEAF mesh
//    transform (e.g. "Whl HD FL") rather than the "Rotating" mid-transform.
//    Two consequences:
//      a) wheel.parent pointed to "Rotating" → steer went to wrong pivot.
//      b) Ground ray origin = mesh_position + up*(wheelRadius*0.6).
//         Mesh pivot is at the wheel centre, but if the assigned transform
//         is the visual mesh (which may sit AT ground level), the ray starts
//         below road and always misses.
//  FIX:
//    Added explicit "Steer Pivot" references (wheelFL_Steer etc.) so the
//    user can wire the WheelController transform for steer independently
//    from the spin/ray transform.  If left null, falls back to wheel.parent
//    (original v5.0 behaviour).
//    Also added groundRayLocalOffset so users can nudge ray origin per prefab.
//
//  WHEEL ASSIGNMENT GUIDE (truck example):
//    Hierarchy:  Wheels / Whl HD FL_WheelController / Rotating / Whl HD FL
//
//    wheelFL            → "Rotating"          (spin target + ray origin)
//    wheelFL_Steer      → "Whl HD FL_WheelController"  (steer pivot)
//    wheelRadius        → 0.55–0.65 for trucks (affects spin speed + ray)
//    groundRayLocalOffset → 0.0 (usually fine when wheelFL = Rotating)
//
//  For simple car rigs where there is no separate steer pivot object, leave
//  wheelFL_Steer empty — it will fall back to wheel.parent as before, but
//  now uses safe quaternion steer so no corruption occurs.
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

    // ── Cached rest rotations for steer pivots (set in CacheWheelRestRotations) ─
    // These preserve the prefab's baked-in camber/caster/toe angles.
    // Steer is composed as:  pivot.localRotation = _restRot * YawOffset
    // This is safe for any pivot orientation — no euler read-back corruption.
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

    private Color _gizmoColor = Color.green;

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

        // ── Cache steer pivot rest rotations BEFORE anything moves ────────────
        // Must happen AFTER Instantiate (so prefab-baked rotations are intact)
        // and BEFORE FixedUpdate starts rotating anything.
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
    //
    //  Snapshot the steer pivot's localRotation the moment the prefab is live.
    //  This preserves any camber / caster / toe baked into the prefab.
    //  Every frame we then compose:
    //      pivot.localRotation = _restRot * Quaternion.AngleAxis(steerDeg, Vector3.up)
    //  instead of the old, broken:
    //      pivot.localEulerAngles.y = steerDeg   ← corrupts X/Z
    // =========================================================================

    private void CacheWheelRestRotations()
    {
        // For each front wheel, the steer pivot is:
        //   (a) the explicit wheelFL_Steer transform if assigned, OR
        //   (b) wheelFL.parent as before (simple car rigs)
        Transform steerPivotFL = ResolveSteerPivot(wheelFL, wheelFL_Steer);
        Transform steerPivotFR = ResolveSteerPivot(wheelFR, wheelFR_Steer);

        _restFL = steerPivotFL != null ? steerPivotFL.localRotation : Quaternion.identity;
        _restFR = steerPivotFR != null ? steerPivotFR.localRotation : Quaternion.identity;
    }

    // Returns the explicit steer pivot if assigned, otherwise wheel.parent, otherwise null.
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
            float moved = HorizontalDistance(transform.position, lastValidPosition);
            if (moved < stuckMovementThreshold && currentSpeed > 0.5f)
            {
                stuckCounter++;
                isStuck = debugIsStuck = true;
                if (stuckCounter >= maxStuckFrames) RecoverFromStuck();
            }
            else
            {
                stuckCounter = 0; isStuck = debugIsStuck = false;
                lastValidPosition = transform.position;
            }
        }
        else
        {
            stuckCounter = 0; isStuck = debugIsStuck = false;
            lastValidPosition = transform.position;
        }

        UpdateDebugInfo();
    }

    // =========================================================================
    //  GROUND SAMPLING
    // =========================================================================

    private void SampleGround()
    {
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

            if (showDebugRays) Debug.DrawLine(origin, hit.point, Color.yellow);
        }
        else
        {
            isGrounded = debugGrounded = false;
            groundNormal      = Vector3.up;
            debugSlopeAngle   = 0f;
            debugGroundSource = "Airborne";

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
                    _hitType     = HitType.TrafficLight;
                    _hitDistance = hit.distance;
                    _hitObject   = hit.collider.gameObject;
                }
                continue;
            }

            HitType type;
            if      ((layerBit & npcVehicleLayer)    != 0) type = HitType.NpcVehicle;
            else if ((layerBit & playerVehicleLayer) != 0) type = HitType.PlayerVehicle;
            else                                            type = HitType.Obstacle;

            _hitType     = type;
            _hitDistance = hit.distance;
            _hitObject   = hit.collider.gameObject;
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

        if (NavMesh.SamplePosition(newXZPos, out NavMeshHit navHit,
                                   navMeshSampleRadius, navMeshAreaMask))
        {
            newXZPos.x = navHit.position.x;
            newXZPos.z = navHit.position.z;
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
    //
    //  STEER — pure quaternion composition, NO euler read-back:
    //      steerPivot.localRotation = _restRot * Quaternion.AngleAxis(deg, Vector3.up)
    //
    //  This preserves every baked axis (camber, caster, toe) on the steer pivot
    //  regardless of how complex the prefab hierarchy is.
    //
    //  SPIN — applied directly to the spin transform (wheelFL etc.) which is the
    //  "Rotating" child, keeping spin and steer on completely separate transforms.
    // =========================================================================

    private void UpdateWheelVisuals()
    {
        if (wheelRadius <= 0.001f) return;

        float degreesPerMeter = 360f / (2f * Mathf.PI * wheelRadius);
        float deltaDeg        = currentSpeed * Time.fixedDeltaTime * degreesPerMeter;
        Quaternion spinDelta  = Quaternion.AngleAxis(deltaDeg, wheelSpinAxis);

        // Rear wheels — spin only
        SpinWheel(wheelRL, spinDelta);
        SpinWheel(wheelRR, spinDelta);

        // Front wheels — steer + spin on separate transforms
        SteerAndSpinFrontWheel(wheelFL, wheelFL_Steer, spinDelta, currentSteerAngle, _restFL);
        SteerAndSpinFrontWheel(wheelFR, wheelFR_Steer, spinDelta, currentSteerAngle, _restFR);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spin-only  (rear wheels)
    // ─────────────────────────────────────────────────────────────────────────
    private static void SpinWheel(Transform spinTransform, Quaternion spinDelta)
    {
        if (spinTransform == null) return;
        spinTransform.localRotation *= spinDelta;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Steer + Spin  (front wheels)
    //
    //  steerPivot  = explicit wheelFL_Steer if assigned, else spinTransform.parent
    //  restRot     = steerPivot's original localRotation snapshotted at init
    //
    //  Safe quaternion steer:
    //      steerPivot.localRotation = restRot * AngleAxis(steerDeg, up)
    //  This rotates the pivot around its OWN local up axis by steerDeg degrees,
    //  preserving all other baked-in rotational offsets exactly.
    // ─────────────────────────────────────────────────────────────────────────
    private static void SteerAndSpinFrontWheel(Transform spinTransform,
                                               Transform explicitSteerPivot,
                                               Quaternion spinDelta,
                                               float steerDeg,
                                               Quaternion restRot)
    {
        if (spinTransform == null) return;

        // Resolve steer pivot
        Transform steerPivot = explicitSteerPivot != null
            ? explicitSteerPivot
            : spinTransform.parent;

        // Apply steer — quaternion only, no euler
        if (steerPivot != null)
        {
            steerPivot.localRotation = restRot * Quaternion.AngleAxis(steerDeg, Vector3.up);
        }

        // Apply spin — purely local, unaffected by steer pivot
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

        if (waypointIndex >= denseWaypoints.Count)
        {
            Debug.Log($"[{gameObject.name}] ✅ Reached Node {destinationNodeID}");
            navSystem.ReleaseRoute(sourceNodeID, destinationNodeID);
            sourceNodeID  = destinationNodeID;
            currentNodeID = sourceNodeID;
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

        hasTarget     = denseWaypoints.Count > 1;
        currentTarget = hasTarget ? denseWaypoints[waypointIndex] : transform.position;

        string routeStr     = $"{sourceNodeID}→{destinationNodeID}";
        debugChainName      = routeStr;
        debugTotalWaypoints = denseWaypoints.Count;

        Debug.Log($"[{gameObject.name}] ══ NEW ROUTE ══ {routeStr} | {denseWaypoints.Count} wps");
    }

    // =========================================================================
    //  STUCK RECOVERY
    // =========================================================================

    private void RecoverFromStuck()
    {
        pathRecalculations++;
        stuckCounter = 0;
        Debug.LogWarning($"[{gameObject.name}] ⚠ Stuck {pathRecalculations}/{maxPathRecalculations}");

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

        if (pathRecalculations < maxPathRecalculations)
        {
            int anchor = FindNearestNodeLoS();
            if (anchor != -1) { sourceNodeID = anchor; currentNodeID = anchor; }
            navSystem.ReleaseRoute(sourceNodeID, destinationNodeID);
            ApplyRouteResult(navSystem.RequestReroute(currentNodeID, destinationNodeID));
            return;
        }

        Debug.LogError($"[{gameObject.name}] Max recalculations — full re-anchor.");
        navSystem.ReleaseRoute(sourceNodeID, destinationNodeID);
        int nearest = FindNearestNodeLoS();
        if (nearest == -1) nearest = navSystem.GetClosestNode(transform.position);
        if (nearest != -1) { sourceNodeID = nearest; currentNodeID = nearest; }
        pathRecalculations = 0;
        PickNewDestinationAndBuildRoute();
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
    //  GIZMOS
    // =========================================================================

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || navSystem == null) return;

        if (denseWaypoints != null && denseWaypoints.Count > 1)
        {
            for (int i = 0; i < denseWaypoints.Count - 1; i++)
            {
                Gizmos.color = i < waypointIndex
                    ? new Color(0.35f, 0.35f, 0.35f, 0.25f)
                    : new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.7f);
                Gizmos.DrawLine(denseWaypoints[i]     + Vector3.up * 0.3f,
                                denseWaypoints[i + 1] + Vector3.up * 0.3f);
                if (i >= waypointIndex)
                    Gizmos.DrawWireSphere(denseWaypoints[i] + Vector3.up * 0.3f, 0.15f);
            }
        }

        if (hasTarget)
        {
            Gizmos.color = isStopped ? Color.red : Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.8f,
                            currentTarget + Vector3.up * 0.3f);
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(currentTarget, waypointReachDistanceXZ * 0.5f);
        }

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
            Gizmos.DrawLine(transform.position + Vector3.up * 1.2f,
                            _hitObject.transform.position + Vector3.up);
            Gizmos.DrawWireSphere(_hitObject.transform.position + Vector3.up * 0.5f, 0.4f);
        }

        if (_currentLight != null)
        {
            Gizmos.color = _stoppedAtRed ? Color.red : Color.green;
            Gizmos.DrawWireSphere(_currentLight.transform.position + Vector3.up, 1f);
        }

        if (navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            Vector3 dp = navSystem.nodeMap[destinationNodeID].worldPosition;
            Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
            Gizmos.DrawWireSphere(dp + Vector3.up * 0.5f, 1.5f);
            Gizmos.DrawLine(dp, dp + Vector3.up * 2.5f);
        }

        if (isStuck)
        {
            float flash = (Time.time * 4f) % 1f < 0.5f ? 1f : 0f;
            Gizmos.color = new Color(1f, 0.5f * flash, 0f, 0.9f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 2.5f, 1f);
        }

        if (isGrounded)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.9f);
            Gizmos.DrawRay(groundHitPoint, groundNormal * 2f);

            Gizmos.color = new Color(0f, 1f, 0.3f, 1f);
            if (_wFL_hit) Gizmos.DrawSphere(_wFL_pt, 0.1f);
            if (_wFR_hit) Gizmos.DrawSphere(_wFR_pt, 0.1f);
            if (_wRL_hit) Gizmos.DrawSphere(_wRL_pt, 0.1f);
            if (_wRR_hit) Gizmos.DrawSphere(_wRR_pt, 0.1f);

            Gizmos.color = new Color(0f, 1f, 0.5f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, navMeshSampleRadius);
        }
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;

        string status = isStuck ? "STUCK" : (isStopped ? "STOPPED" : "MOVING");
        string detail = _hitType != HitType.None ? $" [{_hitType} @ {debugHitDist:F1}m]" : "";
        if (_stoppedAtRed) detail = " [RED LIGHT]";

        Handles.Label(
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
            $"NavMesh radius: {navMeshSampleRadius:F1} m\n" +
            $"Recalcs: {pathRecalculations}/{maxPathRecalculations}",
            new GUIStyle
            {
                normal    = new GUIStyleState { textColor = Color.white },
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
            });
#endif
    }

    // =========================================================================
    //  UTILITY
    // =========================================================================

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x, dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}