// TRAFFIC VEHICLE - DESTINATION-BASED NAVIGATION WITH TRAFFIC LIGHT COMPLIANCE
// Saves complete route and navigates between random destinations
// Fixed: Hill climbing, slope detection, obstacle detection on slopes, stuck detection on hills

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody))]
public class TrafficVehicle : MonoBehaviour
{
    private CentralizedNavigationSystem navSystem;
    private Rigidbody rb;
    private Transform targetWaypoint;

    // Movement settings
    private float maxSpeed = 12f;
    private float acceleration = 5f;
    private float turnSpeed = 3f;
    private float stoppingDistance = 8f;
    private float detectionRange = 15f;
    private LayerMask obstacleLayer;

    // SAVED ROUTE DATA (Core feature - path saved in vehicle)
    [Header("=== SAVED ROUTE DATA ===")]
    [SerializeField] private int sourceNodeID = -1;
    [SerializeField] private int destinationNodeID = -1;
    [SerializeField] private List<int> savedRoutePath = new List<int>();
    [SerializeField] private int currentPathIndex = 0;

    // Path constraints
    [Header("=== PATH SETTINGS ===")]
    [SerializeField] private int minPathLength = 5;
    [SerializeField] private int maxPathLength = 30;
    [SerializeField] private float minDestinationDistance = 50f;
    [SerializeField] private float maxDestinationDistance = 300f;
    [SerializeField] private int maxPathAttempts = 5;

    // Movement state
    private int currentNodeID = -1;
    private float currentSpeed = 0f;
    private bool isStopped = false;
    private Vector3 lastValidPosition;
    private float waypointReachDistance = 5f;  // kept for any external references
    private int stuckCounter = 0;
    private const int MAX_STUCK_FRAMES = 180;
    private int pathRecalculations = 0;
    private const int MAX_RECALCULATIONS = 3;

    // ===================================================
    // HILL / SLOPE SETTINGS
    // ===================================================
    [Header("=== HILL / SLOPE SETTINGS ===")]
    [Tooltip("Layers considered road/terrain surface for ground-snap raycasts. " +
             "Must include your Road and Terrain layers but NOT vehicles or buildings.")]
    [SerializeField] private LayerMask groundLayer = ~0;

    [Tooltip("How far above the car pivot to start the downward ground-snap raycast.")]
    [SerializeField] private float groundRayUpOffset = 3f;

    [Tooltip("Max downward ray distance for the ground-snap ray.")]
    [SerializeField] private float groundRayDistance = 8f;

    [Tooltip("Target ride height above the ground surface (metres). " +
             "Tune so wheels sit flush: start at 0.5 and adjust.")]
    [SerializeField] private float rideHeight = 0.5f;

    [Tooltip("How strongly the car's Y is corrected toward ride height each physics step. " +
             "8-12 works well. Too high = jitter, too low = floaty.")]
    [SerializeField] private float groundSnapStrength = 8f;

    [Tooltip("How fast the car body tilts to match the slope normal.")]
    [SerializeField] private float slopeTiltSpeed = 5f;

    [Tooltip("Speed multiplier applied when the next waypoint is above the car (uphill). " +
             "Prevents stalling on steep roads. 1.3-1.6 recommended.")]
    [SerializeField] private float hillClimbBoost = 1.4f;

    [Tooltip("Horizontal (XZ) distance to consider a waypoint reached. " +
             "Using XZ only means hills don't prevent node advancement.")]
    [SerializeField] private float waypointReachDistanceXZ = 5f;

    [Tooltip("Vertical tolerance on top of waypointReachDistanceXZ. " +
             "Needed so very tall hill nodes still trigger advancement.")]
    [SerializeField] private float waypointReachDistanceY = 4f;

    // Cached ground state (updated every FixedUpdate via SampleGround)
    private bool isGrounded = false;
    private Vector3 groundNormal = Vector3.up;
    private float currentGroundY = 0f;

    // ===================================================
    // TRAFFIC LIGHT DETECTION
    // ===================================================
    [Header("=== TRAFFIC LIGHT DETECTION ===")]
    [SerializeField] private float trafficLightDetectionRange = 5f;
    [SerializeField] private float trafficLightStoppingDistance = 7f;
    [Tooltip("Extra distance buffer so NPC stops BEFORE the violation detector collider zone.")]
    [SerializeField] private float violationColliderBuffer = 3f;
    [Tooltip("Maximum seconds to wait at a red light before proceeding anyway.")]
    [SerializeField] private float maxRedLightWaitTime = 20f;
    [SerializeField] private LayerMask trafficLightLayerMask = -1;
    [SerializeField] private bool enableTrafficLightCompliance = true;

    private EnhancedTrafficLightViolationDetector currentTrafficLight = null;
    private bool isInTrafficLightZone = false;
    private bool isStoppedAtRedLight = false;
    private float timeEnteredRedLightZone = 0f;
    private Vector3 redLightStopPosition = Vector3.zero;
    private bool hasReachedStopPosition = false;

    // ===================================================
    // VEHICLE-AHEAD DETECTION
    // ===================================================
    [Header("=== VEHICLE AHEAD DETECTION ===")]
    [SerializeField] private float vehicleDetectionRange = 20f;
    [SerializeField] private float vehicleStoppingDistance = 10f;
    [SerializeField] private float lateralDetectionWidth = 2.5f;
    [SerializeField] private int multiRayCount = 3;
    [SerializeField] private bool enableVehicleAheadDetection = true;

    private GameObject detectedVehicleAhead = null;
    private float distanceToVehicleAhead = 0f;

    // ===================================================
    // DEBUG INFO (READ ONLY)
    // ===================================================
    [Header("=== DEBUG INFO (READ ONLY) ===")]
    [SerializeField] private string debugRouteName = "";
    [SerializeField] private int debugPathProgress = 0;
    [SerializeField] private int debugTotalNodes = 0;
    [SerializeField] private float debugProgressPercent = 0f;
    [SerializeField] private float debugDistanceToDestination = 0f;
    [SerializeField] private float debugCurrentSpeed = 0f;
    [SerializeField] private float debugDistanceToWaypoint = 0f;
    [SerializeField] private bool debugIsStuck = false;
    [SerializeField] private bool debugIsObstacleDetected = false;
    [SerializeField] private string debugSavedRoute = "";
    [SerializeField] private string debugNextNodes = "";
    [SerializeField] private bool showDebugGizmos = true;

    [SerializeField] private bool debugAtRedLight = false;
    [SerializeField] private string debugTrafficLightID = "None";
    [SerializeField] private string debugTrafficLightState = "None";
    [SerializeField] private float debugDistanceToTrafficLight = 0f;
    [SerializeField] private bool debugVehicleAheadDetected = false;
    [SerializeField] private float debugDistanceToVehicleAhead = 0f;

    // Hill debug
    [SerializeField] private bool debugIsGrounded = false;
    [SerializeField] private float debugSlopeAngle = 0f;
    [SerializeField] private float debugGroundY = 0f;

    private Color debugColor = Color.green;
    private float targetSpeed = 0f;
    private float speedSmoothVelocity = 0f;
    private float speedSmoothTime = 0.3f;

    // Alignment tracking – how many degrees the car is off from facing its waypoint.
    // Used in MoveVehicle to scale drive speed: full speed when aligned, near-zero when pointing away.
    private float angleToCurrentWaypoint = 0f;

    // Minimum seconds between consecutive waypoint advances.
    // Prevents cascade-skipping through tightly-spaced nodes in one FixedUpdate tick.
    private float lastAdvanceTime = -999f;
    private const float MIN_ADVANCE_INTERVAL = 0.15f;

    // ===================================================
    // INITIALIZE
    // ===================================================

    /// <summary>
    /// Called by CentralizedNavigationSystem when spawning this vehicle.
    /// The groundConfig struct carries all ground/slope settings so they are
    /// configured once centrally and never need touching per-vehicle.
    /// </summary>
    public void Initialize(CentralizedNavigationSystem navSys, int startNodeID, float speed,
                           float stopDist, float detectRange, LayerMask obstacles,
                           CentralizedNavigationSystem.VehicleGroundConfig groundConfig = default)
    {
        navSystem        = navSys;
        maxSpeed         = speed;
        stoppingDistance = stopDist;
        detectionRange   = detectRange;
        obstacleLayer    = obstacles;

        // ── Apply centralised ground/slope config from CentralizedNavigationSystem ──
        // Only override if the config carries a non-zero groundLayer (i.e. was actually set).
        // If default(VehicleGroundConfig) was passed the inspector values are kept as-is.
        if (groundConfig.groundLayer != 0)
        {
            groundLayer        = groundConfig.groundLayer;
            rideHeight         = groundConfig.rideHeight;
            groundSnapStrength = groundConfig.groundSnapStrength;
            slopeTiltSpeed     = groundConfig.slopeTiltSpeed;
            hillClimbBoost     = groundConfig.hillClimbBoost;

            Debug.Log($"[{gameObject.name}] Ground config applied from NavSystem → " +
                      $"groundLayer={groundConfig.groundLayer.value} " +
                      $"rideHeight={rideHeight:F2} snapStrength={groundSnapStrength:F1} " +
                      $"tiltSpeed={slopeTiltSpeed:F1} climbBoost={hillClimbBoost:F2}");
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.mass           = 1200f;
        rb.linearDamping  = 1f;
        rb.angularDamping = 10f;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.constraints    = RigidbodyConstraints.None;

        maxSpeed     *= Random.Range(0.85f, 1.15f);
        acceleration  = maxSpeed * 1.5f;
        turnSpeed     = maxSpeed * 0.3f;

        lastValidPosition = transform.position;
        rb.position       = transform.position;

        if (startNodeID != -1 && navSystem.nodeMap.ContainsKey(startNodeID))
        {
            sourceNodeID  = startNodeID;
            currentNodeID = startNodeID;
        }
        else
        {
            sourceNodeID  = navSystem.GetRandomNode();
            currentNodeID = sourceNodeID;
        }

        debugColor = new Color(Random.value, Random.value, Random.value);

        PickNewDestinationAndSaveRoute();

        Debug.Log($"[{gameObject.name}] ========== INITIALIZATION ==========");
        Debug.Log($"[{gameObject.name}] Spawn Node: {sourceNodeID}");
        Debug.Log($"[{gameObject.name}] Position  : {transform.position}");
        Debug.Log($"[{gameObject.name}] Max Speed : {maxSpeed:F1} m/s");
        Debug.Log($"[{gameObject.name}] =====================================");
    }

    // ===================================================
    // FIXED UPDATE
    // ===================================================

    private void FixedUpdate()
    {
        if (navSystem == null || savedRoutePath == null || savedRoutePath.Count == 0)
            return;

        if (rb != null && rb.isKinematic)
            return;

        UpdateDebugInfo();

        // Reset per-frame advance guard
        _advancedThisFrame = false;

        // FIX: Sample ground every physics tick so slope/height data is always fresh
        SampleGround();

        DetectTrafficLightAhead();
        DetectVehicleAhead();

        bool hasObstacle = DetectObstacle();
        debugIsObstacleDetected = hasObstacle;

        // FIX: Waypoint reached check uses XZ distance + Y tolerance separately.
        // Original Vector3.Distance included Y, so large height difference on hills
        // prevented the car from ever "reaching" a node it was horizontally next to.
        float distXZ = targetWaypoint != null
            ? HorizontalDistance(transform.position, targetWaypoint.position)
            : 0f;
        float distY = targetWaypoint != null
            ? Mathf.Abs(transform.position.y - targetWaypoint.position.y)
            : 0f;

        // Cache angle-to-waypoint so MoveVehicle can use it for speed scaling.
        if (targetWaypoint != null)
        {
            Vector3 toWpXZ = new Vector3(
                targetWaypoint.position.x - transform.position.x,
                0f,
                targetWaypoint.position.z - transform.position.z);
            angleToCurrentWaypoint = toWpXZ.sqrMagnitude > 0.01f
                ? Vector3.Angle(new Vector3(transform.forward.x, 0f, transform.forward.z), toWpXZ)
                : 0f;
        }

        // Only advance when:
        //   (a) within reach distance on XZ plane AND Y tolerance, AND
        //   (b) the car is at least roughly facing forward (angle < 90°),
        //       OR it is extremely close (< 2 m) — prevents backward glide-through.
        //   (c) enough time has passed since the last advance (avoids cascade skipping).
        bool waypointReached = distXZ < waypointReachDistanceXZ
                            && distY < waypointReachDistanceY
                            && (angleToCurrentWaypoint < 90f || distXZ < 2f)
                            && (Time.time - lastAdvanceTime >= MIN_ADVANCE_INTERVAL);
        if (waypointReached)
            AdvanceAlongSavedRoute();

        bool shouldStopForTrafficLight = ShouldStopForTrafficLight();
        bool shouldStopForVehicle      = ShouldStopForVehicleAhead();
        bool shouldStop = hasObstacle || shouldStopForTrafficLight || shouldStopForVehicle;

        // FIX: Apply uphill speed boost so car doesn't stall on steep roads
        float slopeBoost = 1f;
        if (!shouldStop && targetWaypoint != null)
        {
            float heightDiff = targetWaypoint.position.y - transform.position.y;
            if (heightDiff > 0.5f)
                slopeBoost = hillClimbBoost;
        }

        targetSpeed = shouldStop ? 0f : maxSpeed * slopeBoost;
        isStopped   = shouldStop;

        debugAtRedLight           = shouldStopForTrafficLight;
        debugVehicleAheadDetected = shouldStopForVehicle;

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

        MoveVehicle();

        // FIX: Stuck detection now uses horizontal distance only.
        // On steep uphills the car moves slowly — purely vertical progress was
        // being misread as "not moving" and triggering spurious repath loops.
        if (!shouldStopForTrafficLight && !shouldStopForVehicle)
        {
            float movedXZ = HorizontalDistance(transform.position, lastValidPosition);
            if (movedXZ < 0.25f && currentSpeed > 0.5f)
            {
                stuckCounter++;
                debugIsStuck = true;
                if (stuckCounter >= MAX_STUCK_FRAMES)
                {
                    Debug.LogWarning($"[{gameObject.name}] ⚠️ STUCK for 3s! Attempting recovery...");
                    RecoverFromStuck();
                }
            }
            else
            {
                stuckCounter      = 0;
                debugIsStuck      = false;
                lastValidPosition = transform.position;
            }
        }
        else
        {
            stuckCounter      = 0;
            debugIsStuck      = false;
            lastValidPosition = transform.position;
        }
    }

    // ===================================================
    // FIX: GROUND SAMPLING
    // Fires a downward ray to find slope normal and surface Y each physics tick.
    // ===================================================

    private void SampleGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * groundRayUpOffset;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            groundRayDistance + groundRayUpOffset, groundLayer))
        {
            isGrounded     = true;
            groundNormal   = hit.normal;
            currentGroundY = hit.point.y;

            debugSlopeAngle = Vector3.Angle(groundNormal, Vector3.up);
            debugGroundY    = currentGroundY;
            debugIsGrounded = true;
        }
        else
        {
            isGrounded      = false;
            groundNormal    = Vector3.up;
            debugSlopeAngle = 0f;
            debugIsGrounded = false;
        }
    }

    // ===================================================
    // TRAFFIC LIGHT DETECTION (logic unchanged, cleaned up)
    // ===================================================

    private void DetectTrafficLightAhead()
    {
        if (!enableTrafficLightCompliance)
        {
            currentTrafficLight         = null;
            isInTrafficLightZone        = false;
            debugTrafficLightID         = "Disabled";
            debugTrafficLightState      = "N/A";
            debugDistanceToTrafficLight = 0f;
            return;
        }

        Collider[] nearby = Physics.OverlapSphere(
            transform.position + Vector3.up * 1f,
            trafficLightDetectionRange,
            trafficLightLayerMask);

        EnhancedTrafficLightViolationDetector closestLight = null;
        float closestDistance = float.MaxValue;

        foreach (Collider col in nearby)
        {
            var detector = col.GetComponent<EnhancedTrafficLightViolationDetector>();
            if (detector == null) continue;

            Vector3 toLight = detector.transform.position - transform.position;
            toLight.y = 0;
            float dot  = Vector3.Dot(transform.forward, toLight.normalized);
            float dist = Vector3.Distance(transform.position, detector.transform.position);

            bool isAhead          = dot > -0.2f;
            bool alreadyAtThisLight = (currentTrafficLight == detector && isStoppedAtRedLight);

            if ((isAhead || alreadyAtThisLight) && dist < closestDistance)
            {
                closestLight    = detector;
                closestDistance = dist;
            }
        }

        if (closestLight != null)
        {
            bool isNewLight     = (currentTrafficLight == null || currentTrafficLight != closestLight);
            currentTrafficLight         = closestLight;
            isInTrafficLightZone        = true;
            debugDistanceToTrafficLight = closestDistance;
            debugTrafficLightID         = currentTrafficLight.GetTrafficLightID();

            TrafficLightController tlc = currentTrafficLight.GetTrafficLight();
            if (tlc != null)
            {
                debugTrafficLightState = tlc.currentState.ToString();
                if (isNewLight && tlc.currentState == TrafficLightController.LightState.Red)
                {
                    Vector3 dir = (currentTrafficLight.transform.position - transform.position).normalized;
                    redLightStopPosition   = currentTrafficLight.transform.position - dir * trafficLightStoppingDistance;
                    redLightStopPosition.y = transform.position.y;
                    hasReachedStopPosition = false;
                    if (showDebugGizmos)
                        Debug.Log($"[{gameObject.name}] 🚦 Red light detected: {debugTrafficLightID}");
                }
            }
            else
            {
                debugTrafficLightState = "No Controller";
            }
        }
        else
        {
            if (isInTrafficLightZone)
            {
                isInTrafficLightZone   = false;
                isStoppedAtRedLight    = false;
                hasReachedStopPosition = false;
                if (showDebugGizmos)
                    Debug.Log($"[{gameObject.name}] ✅ Cleared traffic light zone");
            }
            currentTrafficLight         = null;
            debugTrafficLightID         = "None";
            debugTrafficLightState      = "N/A";
            debugDistanceToTrafficLight = 0f;
        }
    }

    private bool ShouldStopForTrafficLight()
    {
        if (!enableTrafficLightCompliance || currentTrafficLight == null)
        {
            isStoppedAtRedLight = false;
            return false;
        }

        TrafficLightController tlc = currentTrafficLight.GetTrafficLight();
        if (tlc == null) { isStoppedAtRedLight = false; return false; }

        var lightState = tlc.currentState;

        if (lightState == TrafficLightController.LightState.Green)
        {
            if (isStoppedAtRedLight && showDebugGizmos)
                Debug.Log($"[{gameObject.name}] 🟢 GREEN light! Proceeding through {debugTrafficLightID}");
            isStoppedAtRedLight    = false;
            hasReachedStopPosition = false;
            return false;
        }

        if (isStoppedAtRedLight && Time.time - timeEnteredRedLightZone > maxRedLightWaitTime)
        {
            if (showDebugGizmos)
                Debug.LogWarning($"[{gameObject.name}] ⏱️ Red light timeout, proceeding through {debugTrafficLightID}");
            isStoppedAtRedLight    = false;
            hasReachedStopPosition = false;
            return false;
        }

        float stopDist = trafficLightStoppingDistance + violationColliderBuffer;

        if (lightState == TrafficLightController.LightState.Red ||
            lightState == TrafficLightController.LightState.Yellow)
        {
            bool closeEnoughToStop = debugDistanceToTrafficLight < stopDist;

            if (lightState == TrafficLightController.LightState.Yellow && !closeEnoughToStop)
                return false;

            if (!isStoppedAtRedLight && closeEnoughToStop)
            {
                isStoppedAtRedLight     = true;
                hasReachedStopPosition  = true;
                timeEnteredRedLightZone = Time.time;
                if (showDebugGizmos)
                    Debug.Log($"[{gameObject.name}] 🛑 STOPPED at {lightState} {debugTrafficLightID} " +
                              $"({debugDistanceToTrafficLight:F1}m away)");
            }

            if (isStoppedAtRedLight || closeEnoughToStop) return true;
        }

        return false;
    }

    // ===================================================
    // VEHICLE-AHEAD DETECTION (unchanged)
    // ===================================================

    private void DetectVehicleAhead()
    {
        if (!enableVehicleAheadDetection)
        {
            detectedVehicleAhead        = null;
            distanceToVehicleAhead      = 0f;
            debugDistanceToVehicleAhead = 0f;
            return;
        }

        Vector3 rayStart = transform.position + Vector3.up * 1f;
        Vector3 forward  = transform.forward;
        Vector3 right    = transform.right;

        GameObject closestVehicle = null;
        float closestDistance     = float.MaxValue;

        for (int i = 0; i < multiRayCount; i++)
        {
            Vector3 rayDir = forward;
            if (i == 1) rayDir = (forward + -right * lateralDetectionWidth).normalized;
            else if (i == 2) rayDir = (forward +  right * lateralDetectionWidth).normalized;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, rayDir, vehicleDetectionRange, obstacleLayer);
            foreach (RaycastHit hit in hits)
            {
                GameObject vo = FindVehicleInHierarchy(hit.collider.gameObject);
                if (vo != null && vo != gameObject && hit.distance < closestDistance)
                {
                    closestVehicle  = vo;
                    closestDistance = hit.distance;
                }
            }

            if (showDebugGizmos)
                Debug.DrawRay(rayStart, rayDir * vehicleDetectionRange,
                              closestVehicle != null ? Color.red : Color.cyan);
        }

        if (closestVehicle != null)
        {
            detectedVehicleAhead        = closestVehicle;
            distanceToVehicleAhead      = closestDistance;
            debugDistanceToVehicleAhead = closestDistance;
        }
        else
        {
            detectedVehicleAhead        = null;
            distanceToVehicleAhead      = 0f;
            debugDistanceToVehicleAhead = 0f;
        }
    }

    private GameObject FindVehicleInHierarchy(GameObject obj)
    {
        TrafficVehicle v = obj.GetComponent<TrafficVehicle>();
        if (v != null) return obj;
        Transform cur = obj.transform;
        while (cur != null)
        {
            v = cur.GetComponent<TrafficVehicle>();
            if (v != null) return cur.gameObject;
            cur = cur.parent;
        }
        return null;
    }

    private bool ShouldStopForVehicleAhead()
    {
        if (!enableVehicleAheadDetection || detectedVehicleAhead == null) return false;

        if (distanceToVehicleAhead < vehicleStoppingDistance)
        {
            TrafficVehicle ahead = detectedVehicleAhead.GetComponent<TrafficVehicle>();
            if (ahead != null && (ahead.currentSpeed < 1f || ahead.isStopped))
            {
                if (showDebugGizmos && Time.frameCount % 60 == 0)
                    Debug.Log($"[{gameObject.name}] 🚗 Stopping for vehicle ahead at {distanceToVehicleAhead:F1}m");
                return true;
            }
            return distanceToVehicleAhead < vehicleStoppingDistance * 0.7f;
        }

        return false;
    }

    // ===================================================
    // ROUTE MANAGEMENT (unchanged)
    // ===================================================

    private void PickNewDestinationAndSaveRoute()
    {
        if (navSystem == null || navSystem.nodeMap.Count < 2)
        {
            Debug.LogError($"[{gameObject.name}] Not enough nodes for pathfinding!");
            return;
        }

        List<int> allNodes = navSystem.nodeMap.Keys.ToList();

        for (int attempt = 0; attempt < maxPathAttempts; attempt++)
        {
            int dest = PickDestinationWithinRange(allNodes);
            if (dest == -1)
            {
                Debug.LogWarning($"[{gameObject.name}] No dest in range, using random node");
                dest = navSystem.GetRandomNode(new HashSet<int> { sourceNodeID });
            }

            List<int> path = navSystem.FindPath(sourceNodeID, dest);
            if (path == null || path.Count == 0) { Debug.LogWarning($"[{gameObject.name}] No path attempt {attempt + 1}"); continue; }
            if (path.Count < minPathLength)       { Debug.LogWarning($"[{gameObject.name}] Path too short attempt {attempt + 1}"); continue; }
            if (path.Count > maxPathLength)       { Debug.LogWarning($"[{gameObject.name}] Path too long attempt {attempt + 1}"); continue; }

            SaveRouteToVehicle(path, dest);
            return;
        }

        Debug.LogError($"[{gameObject.name}] ❌ Failed to find valid path! Fallback.");
        FallbackPath();
    }

    private void SaveRouteToVehicle(List<int> path, int destination)
    {
        savedRoutePath     = new List<int>(path);
        destinationNodeID  = destination;
        pathRecalculations = 0;

        // Start at index 1 — index 0 is the source node the car is already on.
        // Targeting index 0 means the car "reaches" it instantly on the first frame
        // and cascades through nodes without moving, causing single-node looping.
        currentPathIndex = savedRoutePath.Count > 1 ? 1 : 0;
        SetTargetWaypoint(savedRoutePath[currentPathIndex]);

        debugRouteName  = $"Route_{sourceNodeID}_to_{destinationNodeID}";
        debugTotalNodes = savedRoutePath.Count;
        debugSavedRoute = string.Join(" → ", savedRoutePath);

        int previewCount = Mathf.Min(5, savedRoutePath.Count);
        debugNextNodes  = string.Join(" → ", savedRoutePath.Take(previewCount));
        if (savedRoutePath.Count > previewCount) debugNextNodes += "...";

        Debug.Log($"[{gameObject.name}] ========== NEW ROUTE SAVED ==========");
        Debug.Log($"[{gameObject.name}] Source: {sourceNodeID} → Dest: {destinationNodeID}  Nodes: {savedRoutePath.Count}");
        Debug.Log($"[{gameObject.name}] =====================================");
    }

    private int PickDestinationWithinRange(List<int> allNodes)
    {
        if (!navSystem.nodeMap.ContainsKey(sourceNodeID)) return -1;
        Vector3 sourcePos = navSystem.nodeMap[sourceNodeID].worldPosition;

        // Try the configured range first, then progressively relax min distance
        // so cars on small maps (or near map edges) still find a valid long route.
        float[] minDistFallbacks = { minDestinationDistance, minDestinationDistance * 0.5f, 0f };

        foreach (float minDist in minDistFallbacks)
        {
            var valid = new List<int>();
            foreach (int id in allNodes)
            {
                if (id == sourceNodeID || !navSystem.nodeMap.ContainsKey(id)) continue;
                float d = Vector3.Distance(sourcePos, navSystem.nodeMap[id].worldPosition);
                if (d >= minDist && d <= maxDestinationDistance) valid.Add(id);
            }
            if (valid.Count > 0)
                return valid[Random.Range(0, valid.Count)];
        }

        return -1;
    }

    // Guard: only allow one waypoint advance per FixedUpdate tick.
    // Without this, if the car spawns near several nodes the cascade fires
    // multiple times in a single frame and skips the entire route instantly.
    private bool _advancedThisFrame = false;

    private void AdvanceAlongSavedRoute()
    {
        if (_advancedThisFrame) return;
        _advancedThisFrame = true;
        lastAdvanceTime = Time.time;  // Stamp time so MIN_ADVANCE_INTERVAL is enforced

        currentPathIndex++;

        if (currentPathIndex >= savedRoutePath.Count)
        {
            Debug.Log($"[{gameObject.name}] ✅ DESTINATION REACHED: Node {destinationNodeID}");
            sourceNodeID  = destinationNodeID;
            currentNodeID = sourceNodeID;
            PickNewDestinationAndSaveRoute();
            // PickNewDestinationAndSaveRoute already calls SaveRouteToVehicle
            // which sets currentPathIndex = 1 and targetWaypoint correctly.
            return;
        }

        int nextNodeID = savedRoutePath[currentPathIndex];
        int safeNodeID = FindNextReachableWaypoint(currentPathIndex);

        if (safeNodeID != nextNodeID)
        {
            Debug.LogWarning($"[{gameObject.name}] ⚠️ Node {nextNodeID} blocked, skipping to {safeNodeID}");
            int safeIndex = savedRoutePath.IndexOf(safeNodeID, currentPathIndex);
            if (safeIndex >= 0) currentPathIndex = safeIndex;
            nextNodeID = savedRoutePath[currentPathIndex];
        }

        currentNodeID = nextNodeID;
        SetTargetWaypoint(currentNodeID);

        float pct = (float)currentPathIndex / savedRoutePath.Count * 100f;
        Debug.Log($"[{gameObject.name}] Progress: {currentPathIndex}/{savedRoutePath.Count} ({pct:F0}%) → Node {currentNodeID}");
    }

    /// <summary>
    /// Returns the first upcoming node NOT blocked by buildings/walls.
    /// FIX: Uses (obstacleLayer minus groundLayer) so the road/terrain surface
    /// between the car and a hill node is NOT counted as a blocking wall.
    /// </summary>
    private int FindNextReachableWaypoint(int startIndex)
    {
        const int lookahead = 4;
        // Strip groundLayer out of obstacle mask — terrain is not a wall
        int buildingMask = obstacleLayer & ~groundLayer;

        for (int i = startIndex; i < Mathf.Min(startIndex + lookahead, savedRoutePath.Count); i++)
        {
            int nodeID = savedRoutePath[i];
            if (!navSystem.nodeMap.ContainsKey(nodeID)) continue;

            // FIX: Raise origin and target by 1-1.5m so the SphereCast path
            // clears the road surface under the car and the slope ahead of it
            Vector3 nodePos = navSystem.nodeMap[nodeID].transform.position + Vector3.up * 1.5f;
            Vector3 from    = transform.position + Vector3.up * 1.5f;
            Vector3 dir     = (nodePos - from).normalized;
            float   dist    = Vector3.Distance(from, nodePos);

            bool blocked = Physics.SphereCast(from, 0.5f, dir, out _, dist, buildingMask);
            if (!blocked) return nodeID;
        }

        return savedRoutePath[startIndex];
    }

    private void SetTargetWaypoint(int nodeID)
    {
        if (navSystem.nodeMap.ContainsKey(nodeID))
        {
            targetWaypoint = navSystem.nodeMap[nodeID].transform;
            currentNodeID  = nodeID;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Node {nodeID} not found in nodeMap!");
        }
    }

    private void RecoverFromStuck()
    {
        pathRecalculations++;
        stuckCounter = 0;

        if (pathRecalculations >= MAX_RECALCULATIONS)
        {
            Debug.LogError($"[{gameObject.name}] Too many recalculations! Re-anchoring.");
            SnapToNearestReachableNode();
            return;
        }

        int snappedNode = FindNearestNodeWithLineOfSight();
        if (snappedNode != -1) currentNodeID = snappedNode;

        List<int> newPath = navSystem.FindPath(currentNodeID, destinationNodeID);
        if (newPath != null && newPath.Count > 0)
        {
            sourceNodeID = currentNodeID;
            SaveRouteToVehicle(newPath, destinationNodeID);
            Debug.Log($"[{gameObject.name}] ✅ Path recalculated from node {currentNodeID}");
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] ❌ Recalculation failed! New destination.");
            sourceNodeID = currentNodeID;
            PickNewDestinationAndSaveRoute();
        }
    }

    private void FallbackPath()
    {
        int nearestReachable = FindNearestNodeWithLineOfSight();
        if (nearestReachable != -1)
        {
            List<int> path = navSystem.FindPath(sourceNodeID, nearestReachable);
            if (path != null && path.Count > 0)
            {
                SaveRouteToVehicle(path, nearestReachable);
                Debug.LogWarning($"[{gameObject.name}] Fallback: routing to reachable node {nearestReachable}");
                return;
            }
        }

        int nearest = navSystem.GetClosestNode(transform.position);
        if (nearest != -1 && navSystem.nodeMap.ContainsKey(nearest))
        {
            sourceNodeID  = nearest;
            currentNodeID = nearest;
            SetTargetWaypoint(nearest);
            PickNewDestinationAndSaveRoute();
            Debug.LogWarning($"[{gameObject.name}] Fallback: re-anchored to node {nearest}");
        }
    }

    /// <summary>
    /// FIX: Excludes groundLayer so hill terrain between the car and a node
    /// is not treated as a wall, allowing correct re-anchoring on hilly roads.
    /// </summary>
    private int FindNearestNodeWithLineOfSight()
    {
        float bestDist   = float.MaxValue;
        int   bestNode   = -1;
        int   buildingMask = obstacleLayer & ~groundLayer;

        foreach (var kvp in navSystem.nodeMap)
        {
            if (kvp.Value == null) continue;
            Vector3 nodePos = kvp.Value.transform.position;
            float   dist    = Vector3.Distance(transform.position, nodePos);
            if (dist > 60f) continue;

            Vector3 from      = transform.position + Vector3.up * 1.5f;
            Vector3 to        = nodePos + Vector3.up * 1f;
            Vector3 dir       = (to - from).normalized;
            float   checkDist = Vector3.Distance(from, to);

            bool blocked = Physics.Raycast(from, dir, checkDist, buildingMask);
            if (!blocked && dist < bestDist) { bestDist = dist; bestNode = kvp.Key; }
        }

        return bestNode;
    }

    private void SnapToNearestReachableNode()
    {
        int reachable = FindNearestNodeWithLineOfSight();
        if (reachable == -1) reachable = navSystem.GetClosestNode(transform.position);

        if (reachable != -1 && navSystem.nodeMap.ContainsKey(reachable))
        {
            sourceNodeID       = reachable;
            currentNodeID      = reachable;
            pathRecalculations = 0;
            PickNewDestinationAndSaveRoute();
            Debug.LogWarning($"[{gameObject.name}] Re-anchored to node {reachable}");
        }
    }

    // ===================================================
    // FIX: MOVE VEHICLE — full slope-aware movement
    // ===================================================

    private void MoveVehicle()
    {
        if (targetWaypoint == null) return;
        if (rb == null || rb.isKinematic) return;
        if (currentSpeed < 0.1f) return;

        // ── 1. Steering: yaw-only rotation toward waypoint (XZ plane) ──
        Vector3 toTargetXZ = new Vector3(
            targetWaypoint.position.x - transform.position.x,
            0f,
            targetWaypoint.position.z - transform.position.z);

        if (toTargetXZ.sqrMagnitude > 0.01f)
        {
            // Target yaw faces the waypoint on the flat plane
            Quaternion targetYaw   = Quaternion.LookRotation(toTargetXZ, Vector3.up);
            Quaternion currentYaw  = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            // FIX: Scale turn rate by misalignment — sharper turns rotate faster
            // so the car doesn't loop wide when a waypoint is nearly behind it.
            float misalignment    = angleToCurrentWaypoint;          // 0–180°
            float turnMultiplier  = Mathf.Lerp(1f, 2.5f, misalignment / 180f);
            Quaternion smoothedYaw = Quaternion.Slerp(
                currentYaw, targetYaw,
                Time.fixedDeltaTime * turnSpeed * turnMultiplier);

            // ── 2. Slope tilt: rotate pitch/roll to match terrain surface normal ──
            Quaternion slopeTilt = Quaternion.FromToRotation(Vector3.up, groundNormal);
            Quaternion finalRot  = slopeTilt * smoothedYaw;

            rb.MoveRotation(Quaternion.Slerp(rb.rotation, finalRot, Time.fixedDeltaTime * slopeTiltSpeed));
        }

        // ── 3. Alignment-based speed scaling (THE BACK-AND-FORTH FIX) ──
        // When the waypoint is nearly behind the car, driving at full speed in the
        // current forward direction makes it arc wide or oscillate back and forth.
        // We scale drive speed to near-zero when the car needs to turn more than
        // ~60°, letting the rotation catch up before the car accelerates again.
        //
        //   angle  0°  → factor 1.0  (full speed — perfectly aligned)
        //   angle 45°  → factor 0.75
        //   angle 90°  → factor 0.15 (nearly stopped — turning in place)
        //   angle 135° → factor 0.05
        //   angle 180° → factor 0.0  (stopped — target is directly behind)
        float alignAngle   = Mathf.Clamp(angleToCurrentWaypoint, 0f, 180f);
        float alignFactor  = Mathf.Clamp01(1f - alignAngle / 90f);   // linear 0–90°
        alignFactor        = alignFactor * alignFactor;               // square it: gentler at small angles, steeper drop-off past 45°
        alignFactor        = Mathf.Max(alignFactor, 0.02f);           // keep a tiny creep so stuck detection still fires

        float driveSpeed = currentSpeed * alignFactor;

        // ── 4. Drive: move along transform.forward at alignment-scaled speed ──
        Vector3 move = transform.forward * driveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + move);

        // ── 5. Soft Y correction: nudge car toward ride height above ground ──
        if (isGrounded)
        {
            float targetY  = currentGroundY + rideHeight;
            float yError   = targetY - rb.position.y;

            if (Mathf.Abs(yError) > 0.05f)
            {
                Vector3 yCorrection = new Vector3(0f, yError * groundSnapStrength * Time.fixedDeltaTime, 0f);
                rb.MovePosition(rb.position + yCorrection);
            }
        }
    }

    // ===================================================
    // FIX: DETECT OBSTACLE — slope-aware, terrain excluded
    // ===================================================

    private bool DetectObstacle()
    {
        // FIX: Raise ray origin to 1.2f. Original 0.5f was low enough to hit the
        // road surface rising ahead on uphills, treating the road as a wall.
        Vector3 rayStart = transform.position + Vector3.up * 1.2f;

        // FIX: Use mostly-level forward. Cap the downward pitch component at -0.15f
        // so the ray doesn't plunge into the road surface ahead on slopes.
        Vector3 rawFwd      = transform.forward;
        Vector3 levelFwd    = new Vector3(rawFwd.x, Mathf.Max(rawFwd.y, -0.15f), rawFwd.z).normalized;

        // FIX: Exclude groundLayer — road/terrain should never count as an obstacle.
        int buildingMask = obstacleLayer & ~groundLayer;

        if (Physics.Raycast(rayStart, levelFwd, out RaycastHit hit, detectionRange, buildingMask))
        {
            TrafficVehicle otherVehicle = hit.collider.GetComponent<TrafficVehicle>();
            if (otherVehicle != null && hit.distance < stoppingDistance)
            {
                if (showDebugGizmos) Debug.DrawLine(rayStart, hit.point, Color.red);
                return true;
            }

            if (hit.collider.gameObject.layer != gameObject.layer && hit.distance < stoppingDistance * 0.5f)
            {
                if (showDebugGizmos) Debug.DrawLine(rayStart, hit.point, Color.yellow);
                return true;
            }
        }

        if (Physics.SphereCast(rayStart, 0.8f, levelFwd, out hit, detectionRange, buildingMask))
        {
            TrafficVehicle otherVehicle = hit.collider.GetComponent<TrafficVehicle>();
            if (otherVehicle != null && hit.distance < stoppingDistance) return true;
        }

        if (showDebugGizmos) Debug.DrawRay(rayStart, levelFwd * detectionRange, Color.green);
        return false;
    }

    // ===================================================
    // SNAP TO GROUND (kept — used externally at spawn time)
    // ===================================================

    private Vector3 SnapToGround(Vector3 position, float rayDistance = 50f)
    {
        Vector3 rayStart = new Vector3(position.x, position.y + 20f, position.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance + 20f))
            return hit.point + Vector3.up * 0.5f;
        if (Physics.Raycast(position, Vector3.down, out hit, rayDistance))
            return hit.point + Vector3.up * 0.5f;
        return position;
    }

    // ===================================================
    // UTILITY
    // ===================================================

    /// <summary>
    /// Horizontal (XZ-plane only) distance. Used everywhere height on hills
    /// should not affect the distance measurement.
    /// </summary>
    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // ===================================================
    // DEBUG INFO
    // ===================================================

    private void UpdateDebugInfo()
    {
        debugPathProgress    = currentPathIndex;
        debugCurrentSpeed    = currentSpeed;
        debugProgressPercent = savedRoutePath.Count > 0
            ? (float)currentPathIndex / savedRoutePath.Count * 100f
            : 0f;
        debugDistanceToWaypoint = targetWaypoint != null
            ? Vector3.Distance(transform.position, targetWaypoint.position)
            : 0f;

        if (navSystem != null && navSystem.nodeMap.ContainsKey(destinationNodeID))
            debugDistanceToDestination = Vector3.Distance(
                transform.position, navSystem.nodeMap[destinationNodeID].worldPosition);

        if (savedRoutePath != null && savedRoutePath.Count > 0 && currentPathIndex < savedRoutePath.Count)
        {
            int rem     = savedRoutePath.Count - currentPathIndex;
            int preview = Mathf.Min(3, rem);
            debugNextNodes = string.Join(" → ", savedRoutePath.Skip(currentPathIndex).Take(preview));
            if (rem > preview) debugNextNodes += $" ... (+{rem - preview} more)";
        }
    }

    // ===================================================
    // GIZMOS
    // ===================================================

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || navSystem == null) return;

        if (savedRoutePath != null && savedRoutePath.Count > 1)
        {
            for (int i = 0; i < savedRoutePath.Count - 1; i++)
            {
                if (!navSystem.nodeMap.ContainsKey(savedRoutePath[i]) ||
                    !navSystem.nodeMap.ContainsKey(savedRoutePath[i + 1])) continue;

                Vector3 s = navSystem.nodeMap[savedRoutePath[i]].worldPosition + Vector3.up * 1.5f;
                Vector3 e = navSystem.nodeMap[savedRoutePath[i + 1]].worldPosition + Vector3.up * 1.5f;

                Gizmos.color = i < currentPathIndex ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : debugColor;
                Gizmos.DrawLine(s, e);
                if (i >= currentPathIndex) Gizmos.DrawWireSphere(s, 0.5f);
            }
        }

        if (targetWaypoint != null)
        {
            Gizmos.color = isStopped ? Color.red : (debugIsStuck ? Color.magenta : debugColor);
            Gizmos.DrawLine(transform.position + Vector3.up, targetWaypoint.position + Vector3.up);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetWaypoint.position, waypointReachDistanceXZ);
        }

        if (navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            Vector3 dp = navSystem.nodeMap[destinationNodeID].worldPosition;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(dp + Vector3.up * 3f, 3f);
            Gizmos.DrawLine(dp, dp + Vector3.up * 6f);
        }

        if (navSystem.nodeMap.ContainsKey(sourceNodeID))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(navSystem.nodeMap[sourceNodeID].worldPosition + Vector3.up * 3f, 2f);
        }

        if (currentTrafficLight != null)
        {
            Gizmos.color = debugAtRedLight ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up * 2f, currentTrafficLight.transform.position);
            Gizmos.DrawWireSphere(currentTrafficLight.transform.position, 2f);

            if (debugAtRedLight && hasReachedStopPosition)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(redLightStopPosition + Vector3.up * 0.5f, 1f);
                Gizmos.DrawLine(redLightStopPosition, redLightStopPosition + Vector3.up * 3f);
            }
        }

        if (detectedVehicleAhead != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f,
                            detectedVehicleAhead.transform.position + Vector3.up * 1.5f);
            Gizmos.DrawWireSphere(detectedVehicleAhead.transform.position + Vector3.up * 3f, 1.5f);
        }

        if (debugIsStuck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 4f, 2f);
        }

        // Ground normal indicator (white ray = slope direction)
        if (isGrounded)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.3f, groundNormal * 2f);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 5f);
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || targetWaypoint == null) return;

        string status     = debugIsStuck ? "🚫 STUCK" : (isStopped ? "⛔ STOPPED" : "✅ MOVING");
        string stopReason = "";
        if (isStopped)
        {
            if (debugAtRedLight)             stopReason = " [RED LIGHT]";
            else if (debugVehicleAheadDetected) stopReason = " [VEHICLE AHEAD]";
            else if (debugIsObstacleDetected)   stopReason = " [OBSTACLE]";
        }

        Handles.Label(
            transform.position + Vector3.up * 6f,
            $"{gameObject.name} {status}{stopReason}\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            $"Route: {sourceNodeID} → {destinationNodeID}\n" +
            $"Progress: {currentPathIndex}/{savedRoutePath.Count} ({debugProgressPercent:F0}%)\n" +
            $"Current Node: {currentNodeID}\n" +
            $"Next: {debugNextNodes}\n" +
            $"Dist to Dest: {debugDistanceToDestination:F1}m\n" +
            $"Speed: {debugCurrentSpeed:F1} m/s ({debugCurrentSpeed * 3.6f:F0} km/h)\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            $"Grounded: {debugIsGrounded}  Slope: {debugSlopeAngle:F1}°\n" +
            $"Ground Y: {debugGroundY:F2}  Target Y: {(debugGroundY + rideHeight):F2}\n" +
            $"Traffic Light: {debugTrafficLightID} [{debugTrafficLightState}]\n" +
            $"Dist to Light: {debugDistanceToTrafficLight:F1}m\n" +
            $"Vehicle Ahead: {(detectedVehicleAhead != null ? $"YES ({debugDistanceToVehicleAhead:F1}m)" : "NO")}\n" +
            $"Recalculations: {pathRecalculations}/{MAX_RECALCULATIONS}",
            new GUIStyle
            {
                normal    = new GUIStyleState { textColor = Color.white },
                fontSize  = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            });
#endif
    }
}