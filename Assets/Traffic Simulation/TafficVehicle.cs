// TRAFFIC VEHICLE - DESTINATION-BASED NAVIGATION WITH TRAFFIC LIGHT COMPLIANCE
// Fixed: NPC cars now detect and stop for the player car (no more running-over)
// Fixed: Wheels now rotate (WheelCollider support + visual-only fallback)
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

    // SAVED ROUTE DATA
    [Header("=== SAVED ROUTE DATA ===")]
    [SerializeField] private int sourceNodeID = -1;
    [SerializeField] private int destinationNodeID = -1;
    [SerializeField] private List<int> savedRoutePath = new List<int>();
    [SerializeField] private int currentPathIndex = 0;

    // Path constraints
    [Header("=== PATH SETTINGS ===")]
    [Tooltip("Minimum nodes in a path (including source). 2 = at least one real step. " +
             "Set higher only on large maps with many nodes.")]
    [SerializeField] private int minPathLength = 2;
    [SerializeField] private int maxPathLength = 50;
    [Tooltip("Minimum straight-line distance to destination. " +
             "Lowered automatically if no destination found at this range.")]
    [SerializeField] private float minDestinationDistance = 30f;
    [SerializeField] private float maxDestinationDistance = 500f;
    [Tooltip("Attempts to find a valid path before giving up and using fallback.")]
    [SerializeField] private int maxPathAttempts = 10;

    // Movement state
    private int currentNodeID = -1;
    private float currentSpeed = 0f;
    private bool isStopped = false;
    private Vector3 lastValidPosition;
    private float waypointReachDistance = 5f;
    private int stuckCounter = 0;
    private const int MAX_STUCK_FRAMES = 180;
    private int pathRecalculations = 0;
    private const int MAX_RECALCULATIONS = 3;

    // ===================================================
    // HILL / SLOPE SETTINGS
    // ===================================================
    [Header("=== HILL / SLOPE SETTINGS ===")]
    [Tooltip("Layers considered road/terrain surface for ground-snap raycasts.")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundRayUpOffset = 3f;
    [SerializeField] private float groundRayDistance = 8f;
    [SerializeField] private float rideHeight = 0.5f;
    [SerializeField] private float groundSnapStrength = 8f;
    [SerializeField] private float slopeTiltSpeed = 5f;
    [SerializeField] private float hillClimbBoost = 1.4f;
    [SerializeField] private float waypointReachDistanceXZ = 5f;
    [SerializeField] private float waypointReachDistanceY = 4f;

    private bool isGrounded = false;
    private Vector3 groundNormal = Vector3.up;
    private float currentGroundY = 0f;

    // ===================================================
    // VEHICLE LAYER MASK (set by NavSystem on spawn)
    // Covers ALL vehicle layers — NPC cars, player car, any road vehicle.
    // Set once in CentralizedNavigationSystem → Vehicle Layers.
    // ===================================================
    [Header("=== VEHICLE LAYERS (set by NavSystem on spawn) ===")]
    [Tooltip("All layers that count as a vehicle (NPC + player). Set in CentralizedNavigationSystem.")]
    [SerializeField] private LayerMask vehicleLayerMask = 0;

    // ===================================================
    // WHEEL ROTATION  (FIX #2)
    // ===================================================
    [Header("=== WHEEL ROTATION ===")]
    [Tooltip("WheelColliders on this vehicle (preferred method). " +
             "The script drives them and syncs visual meshes automatically. " +
             "Leave empty to use visual-only rotation instead.")]
    [SerializeField] private WheelCollider[] wheelColliders = new WheelCollider[0];

    [Tooltip("Visual mesh transforms matching each WheelCollider (same array order). " +
             "Their position & rotation are synced from the WheelCollider every frame.")]
    [SerializeField] private Transform[] wheelMeshes = new Transform[0];

    [Space]
    [Tooltip("For car models WITHOUT WheelColliders: assign wheel transforms here directly. " +
             "They spin around visualWheelSpinAxis based on current vehicle speed. " +
             "The script also tries to auto-detect these by searching for transforms " +
             "whose name contains 'wheel', 'tyre', or 'tire'.")]
    [SerializeField] private Transform[] visualOnlyWheels = new Transform[0];

    [Tooltip("Wheel radius used for visual-only spin calculation (metres). " +
             "Match your actual wheel size — usually 0.30–0.45 for cars.")]
    [SerializeField] private float visualWheelRadius = 0.35f;

    [Tooltip("Local axis the visual-only wheels rotate around. Usually Vector3.right (X-axis). " +
             "Change to Vector3.left if wheels spin backwards.")]
    [SerializeField] private Vector3 visualWheelSpinAxis = Vector3.right;

    // ===================================================
    // TRAFFIC LIGHT DETECTION
    // ===================================================
    [Header("=== TRAFFIC LIGHT DETECTION ===")]
    [SerializeField] private float trafficLightDetectionRange = 5f;
    [SerializeField] private float trafficLightStoppingDistance = 7f;
    [SerializeField] private float violationColliderBuffer = 3f;
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
    [SerializeField] private bool debugIsGrounded = false;
    [SerializeField] private float debugSlopeAngle = 0f;
    [SerializeField] private float debugGroundY = 0f;
    [SerializeField] private int debugWheelCollidersFound = 0;
    [SerializeField] private int debugVisualWheelsFound = 0;

    private Color debugColor = Color.green;
    private float targetSpeed = 0f;
    private float speedSmoothVelocity = 0f;
    private float speedSmoothTime = 0.3f;
    private float angleToCurrentWaypoint = 0f;
    private float lastAdvanceTime = -999f;
    private const float MIN_ADVANCE_INTERVAL = 0.15f;

    // ===================================================
    // INITIALIZE
    // ===================================================

    public void Initialize(CentralizedNavigationSystem navSys, int startNodeID, float speed,
                           float stopDist, float detectRange, LayerMask obstacles,
                           CentralizedNavigationSystem.VehicleGroundConfig groundConfig = default,
                           LayerMask vehicleLayers = default)
    {
        navSystem        = navSys;
        maxSpeed         = speed;
        stoppingDistance = stopDist;
        detectionRange   = detectRange;
        obstacleLayer    = obstacles;

        // Apply generic vehicle layer mask from CentralizedNavigationSystem.
        // Covers NPC cars + player car + any other road vehicle — purely layer-based, no tags needed.
        if (vehicleLayers.value != 0)
            vehicleLayerMask = vehicleLayers;

        // Apply centralised ground/slope config
        if (groundConfig.groundLayer != 0)
        {
            groundLayer        = groundConfig.groundLayer;
            rideHeight         = groundConfig.rideHeight;
            groundSnapStrength = groundConfig.groundSnapStrength;
            slopeTiltSpeed     = groundConfig.slopeTiltSpeed;
            hillClimbBoost     = groundConfig.hillClimbBoost;
        }

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

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

        AutoDetectWheels();
        PickNewDestinationAndSaveRoute();

        Debug.Log($"[{gameObject.name}] Init | Node:{sourceNodeID} | Speed:{maxSpeed:F1} m/s | " +
                  $"WheelColliders:{wheelColliders.Length} | VisualWheels:{visualOnlyWheels.Length} | " +
                  $"VehicleLayerMask:{vehicleLayerMask.value}");
    }

    // ===================================================
    // WHEEL AUTO-DETECTION
    // ===================================================

    /// <summary>
    /// If wheels were not assigned in the inspector, tries to find them automatically.
    /// WheelColliders are preferred; falls back to name-matching transforms.
    /// Called once inside Initialize — zero overhead at runtime.
    /// </summary>
    private void AutoDetectWheels()
    {
        // ── WheelCollider path ──
        if (wheelColliders == null || wheelColliders.Length == 0)
        {
            WheelCollider[] found = GetComponentsInChildren<WheelCollider>(true);
            if (found.Length > 0)
            {
                wheelColliders = found;
                Debug.Log($"[{gameObject.name}] Auto-detected {found.Length} WheelColliders.");
            }
        }

        // Pair WheelColliders with matching visual meshes
        if (wheelColliders.Length > 0 && (wheelMeshes == null || wheelMeshes.Length == 0))
        {
            List<Transform> meshList = new List<Transform>();
            foreach (WheelCollider wc in wheelColliders)
                meshList.Add(FindWheelMeshForCollider(wc));
            wheelMeshes = meshList.ToArray();
        }

        // ── Visual-only fallback ──
        if (wheelColliders.Length == 0 && (visualOnlyWheels == null || visualOnlyWheels.Length == 0))
        {
            List<Transform> list = new List<Transform>();
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                string lwr = t.name.ToLower();
                if (lwr.Contains("wheel") || lwr.Contains("tyre") || lwr.Contains("tire"))
                    list.Add(t);
            }
            if (list.Count > 0)
            {
                visualOnlyWheels = list.ToArray();
                Debug.Log($"[{gameObject.name}] Auto-detected {list.Count} visual wheels by name.");
            }
        }

        debugWheelCollidersFound = wheelColliders  != null ? wheelColliders.Length  : 0;
        debugVisualWheelsFound   = visualOnlyWheels != null ? visualOnlyWheels.Length : 0;
    }

    private Transform FindWheelMeshForCollider(WheelCollider wc)
    {
        // Check direct children of the WheelCollider GameObject
        foreach (Transform child in wc.transform)
            if (child.GetComponent<MeshRenderer>() != null) return child;

        // Check siblings under the same parent
        if (wc.transform.parent == null) return null;
        foreach (Transform sib in wc.transform.parent)
        {
            if (sib == wc.transform) continue;
            string sibLower = sib.name.ToLower();
            if (sib.GetComponent<MeshRenderer>() != null &&
                (sibLower.Contains("wheel") || sibLower.Contains("tyre") || sibLower.Contains("tire")))
                return sib;
        }
        return null;
    }

    // ===================================================
    // FIXED UPDATE
    // ===================================================

    private void FixedUpdate()
    {
        if (navSystem == null || savedRoutePath == null || savedRoutePath.Count == 0) return;
        if (rb != null && rb.isKinematic) return;

        UpdateDebugInfo();
        _advancedThisFrame = false;

        SampleGround();
        DetectTrafficLightAhead();
        DetectVehicleAhead();

        bool hasObstacle = DetectObstacle();
        debugIsObstacleDetected = hasObstacle;

        float distXZ = targetWaypoint != null ? HorizontalDistance(transform.position, targetWaypoint.position) : 0f;
        float distY  = targetWaypoint != null ? Mathf.Abs(transform.position.y - targetWaypoint.position.y) : 0f;

        if (targetWaypoint != null)
        {
            Vector3 toWpXZ = new Vector3(
                targetWaypoint.position.x - transform.position.x, 0f,
                targetWaypoint.position.z - transform.position.z);
            angleToCurrentWaypoint = toWpXZ.sqrMagnitude > 0.01f
                ? Vector3.Angle(new Vector3(transform.forward.x, 0f, transform.forward.z), toWpXZ)
                : 0f;
        }

        // Waypoint is reached when the car is close enough in XZ and Y.
        // No angle guard — after a repath the new waypoint may be behind the car;
        // the angle check would prevent advancing and cause circling/stuck loops.
        bool waypointReached = distXZ < waypointReachDistanceXZ
                            && distY  < waypointReachDistanceY
                            && (Time.time - lastAdvanceTime >= MIN_ADVANCE_INTERVAL);
        if (waypointReached)
            AdvanceAlongSavedRoute();

        bool shouldStopForTrafficLight = ShouldStopForTrafficLight();
        bool shouldStopForVehicle      = ShouldStopForVehicleAhead();
        bool shouldStop = hasObstacle || shouldStopForTrafficLight || shouldStopForVehicle;

        float slopeBoost = 1f;
        if (!shouldStop && targetWaypoint != null)
        {
            float heightDiff = targetWaypoint.position.y - transform.position.y;
            if (heightDiff > 0.5f) slopeBoost = hillClimbBoost;
        }

        targetSpeed = shouldStop ? 0f : maxSpeed * slopeBoost;
        isStopped   = shouldStop;

        debugAtRedLight           = shouldStopForTrafficLight;
        debugVehicleAheadDetected = shouldStopForVehicle;

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

        MoveVehicle();

        if (!shouldStopForTrafficLight && !shouldStopForVehicle)
        {
            float movedXZ = HorizontalDistance(transform.position, lastValidPosition);
            if (movedXZ < 0.25f && currentSpeed > 0.5f)
            {
                stuckCounter++;
                debugIsStuck = true;
                if (stuckCounter >= MAX_STUCK_FRAMES)
                {
                    Debug.LogWarning($"[{gameObject.name}] ⚠️ STUCK! Attempting recovery...");
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
    // UPDATE — WHEEL ROTATION (every frame for smooth visuals)
    // ===================================================

    private void Update()
    {
        if (wheelColliders != null && wheelColliders.Length > 0)
            UpdateWheelCollidersVisual();
        else if (visualOnlyWheels != null && visualOnlyWheels.Length > 0)
            UpdateVisualOnlyWheels();
    }

    /// <summary>
    /// Drives WheelColliders with motor/brake torque and syncs visual meshes.
    /// Standard Unity approach — handles suspension travel automatically.
    /// </summary>
    private void UpdateWheelCollidersVisual()
    {
        for (int i = 0; i < wheelColliders.Length; i++)
        {
            if (wheelColliders[i] == null) continue;

            if (isStopped)
            {
                wheelColliders[i].motorTorque = 0f;
                wheelColliders[i].brakeTorque = 3000f;
            }
            else
            {
                float wheelSpeedMs = wheelColliders[i].rpm * (2f * Mathf.PI * wheelColliders[i].radius) / 60f;
                float error        = currentSpeed - wheelSpeedMs;
                wheelColliders[i].motorTorque = Mathf.Clamp(error * 60f, 0f, 800f);
                wheelColliders[i].brakeTorque = 0f;
            }

            if (i < wheelMeshes.Length && wheelMeshes[i] != null)
            {
                wheelColliders[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
                wheelMeshes[i].position = pos;
                wheelMeshes[i].rotation = rot;
            }
        }
    }

    /// <summary>
    /// Visual-only wheel spin without WheelColliders.
    /// Rotates wheel transforms around their local spin axis based on vehicle speed.
    /// Formula: degrees_per_second = (speed_m_s / circumference_m) * 360
    /// </summary>
    private void UpdateVisualOnlyWheels()
    {
        if (visualWheelRadius <= 0f) visualWheelRadius = 0.35f;
        float circumference = 2f * Mathf.PI * visualWheelRadius;
        float rotThisFrame  = (currentSpeed / circumference) * 360f * Time.deltaTime;

        foreach (Transform wheel in visualOnlyWheels)
        {
            if (wheel == null) continue;
            wheel.Rotate(visualWheelSpinAxis, rotThisFrame, Space.Self);
        }
    }

    // ===================================================
    // GROUND SAMPLING
    // ===================================================

    private void SampleGround()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * groundRayUpOffset;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
                            groundRayDistance + groundRayUpOffset, groundLayer))
        {
            isGrounded      = true;
            groundNormal    = hit.normal;
            currentGroundY  = hit.point.y;
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
    // TRAFFIC LIGHT DETECTION
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
            transform.position + Vector3.up * 1f, trafficLightDetectionRange, trafficLightLayerMask);

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

            bool isAhead            = dot > -0.2f;
            bool alreadyAtThisLight = (currentTrafficLight == detector && isStoppedAtRedLight);

            if ((isAhead || alreadyAtThisLight) && dist < closestDistance)
            {
                closestLight    = detector;
                closestDistance = dist;
            }
        }

        if (closestLight != null)
        {
            bool isNewLight             = (currentTrafficLight == null || currentTrafficLight != closestLight);
            currentTrafficLight         = closestLight;
            isInTrafficLightZone        = true;
            debugDistanceToTrafficLight = closestDistance;
            debugTrafficLightID         = currentTrafficLight.GetTrafficLightID();

            TrafficLightController tlc  = currentTrafficLight.GetTrafficLight();
            if (tlc != null)
            {
                debugTrafficLightState = tlc.currentState.ToString();
                if (isNewLight && tlc.currentState == TrafficLightController.LightState.Red)
                {
                    Vector3 dir            = (currentTrafficLight.transform.position - transform.position).normalized;
                    redLightStopPosition   = currentTrafficLight.transform.position - dir * trafficLightStoppingDistance;
                    redLightStopPosition.y = transform.position.y;
                    hasReachedStopPosition = false;
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
            isStoppedAtRedLight    = false;
            hasReachedStopPosition = false;
            return false;
        }

        if (isStoppedAtRedLight && Time.time - timeEnteredRedLightZone > maxRedLightWaitTime)
        {
            isStoppedAtRedLight    = false;
            hasReachedStopPosition = false;
            return false;
        }

        float stopDist = trafficLightStoppingDistance + violationColliderBuffer;

        if (lightState == TrafficLightController.LightState.Red ||
            lightState == TrafficLightController.LightState.Yellow)
        {
            bool closeEnoughToStop = debugDistanceToTrafficLight < stopDist;
            if (lightState == TrafficLightController.LightState.Yellow && !closeEnoughToStop) return false;

            if (!isStoppedAtRedLight && closeEnoughToStop)
            {
                isStoppedAtRedLight     = true;
                hasReachedStopPosition  = true;
                timeEnteredRedLightZone = Time.time;
            }
            if (isStoppedAtRedLight || closeEnoughToStop) return true;
        }

        return false;
    }

    // ===================================================
    // VEHICLE-AHEAD DETECTION
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

        // Use vehicleLayerMask — covers ALL vehicle layers (NPC cars + player car + any other road vehicle).
        // If vehicleLayerMask is 0 (not set), fall back to obstacleLayer so detection still works.
        LayerMask scanMask = vehicleLayerMask.value != 0 ? vehicleLayerMask : obstacleLayer;

        Vector3 rayStart      = transform.position + Vector3.up * 1f;
        Vector3 forward       = transform.forward;
        Vector3 right         = transform.right;
        GameObject closestV   = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < multiRayCount; i++)
        {
            Vector3 rayDir = forward;
            if (i == 1) rayDir = (forward - right * lateralDetectionWidth).normalized;
            else if (i == 2) rayDir = (forward + right * lateralDetectionWidth).normalized;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, rayDir, vehicleDetectionRange, scanMask);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider.transform.IsChildOf(transform)) continue; // skip own colliders
                if (hit.distance < closestDistance)
                {
                    closestV        = hit.collider.transform.root.gameObject;
                    closestDistance = hit.distance;
                }
            }

            if (showDebugGizmos)
                Debug.DrawRay(rayStart, rayDir * vehicleDetectionRange,
                              closestV != null ? Color.red : Color.cyan);
        }

        if (closestV != null)
        {
            detectedVehicleAhead        = closestV;
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

    private bool ShouldStopForVehicleAhead()
    {
        if (!enableVehicleAheadDetection || detectedVehicleAhead == null) return false;

        if (distanceToVehicleAhead < vehicleStoppingDistance)
        {
            // If it's an NPC with a known speed, only stop if it's slow/stopped
            TrafficVehicle npc = detectedVehicleAhead.GetComponent<TrafficVehicle>();
            if (npc != null) return npc.currentSpeed < 1f || npc.isStopped || distanceToVehicleAhead < vehicleStoppingDistance * 0.7f;
            // Player car or unknown vehicle — always stop within stopping distance
            return true;
        }
        return false;
    }

    // ===================================================
    // ROUTE MANAGEMENT
    // ===================================================

    private bool _isPickingDestination = false;

    private void PickNewDestinationAndSaveRoute()
    {
        if (_isPickingDestination) return;
        _isPickingDestination = true;
        try { PickNewDestinationAndSaveRouteInternal(); }
        finally { _isPickingDestination = false; }
    }

    private void PickNewDestinationAndSaveRouteInternal()
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
                dest = navSystem.GetRandomNode(new HashSet<int> { sourceNodeID });

            List<int> path = navSystem.FindPath(sourceNodeID, dest);
            if (path == null || path.Count == 0) continue;
            if (path.Count < minPathLength)       continue;
            if (path.Count > maxPathLength)       continue;

            SaveRouteToVehicle(path, dest);
            return;
        }

        Debug.LogError($"[{gameObject.name}] Failed to find path! Fallback.");
        FallbackPath();
    }

    private void SaveRouteToVehicle(List<int> path, int destination)
    {
        savedRoutePath     = new List<int>(path);
        destinationNodeID  = destination;
        pathRecalculations = 0;

        currentPathIndex = savedRoutePath.Count > 1 ? 1 : 0;
        SetTargetWaypoint(savedRoutePath[currentPathIndex]);

        debugRouteName  = $"Route_{sourceNodeID}_to_{destinationNodeID}";
        debugTotalNodes = savedRoutePath.Count;
        debugSavedRoute = string.Join(" > ", savedRoutePath);

        int previewCount = Mathf.Min(5, savedRoutePath.Count);
        debugNextNodes   = string.Join(" > ", savedRoutePath.Take(previewCount));
        if (savedRoutePath.Count > previewCount) debugNextNodes += "...";
    }

    private int PickDestinationWithinRange(List<int> allNodes)
    {
        if (!navSystem.nodeMap.ContainsKey(sourceNodeID)) return -1;
        Vector3 sourcePos = navSystem.nodeMap[sourceNodeID].worldPosition;

        // Progressive distance relaxation: try full range first, then halve, then any distance.
        // Always exclude sourceNodeID itself and its immediate graph neighbours to prevent
        // single-step back-and-forth oscillation.
        HashSet<int> immediateNeighbours = new HashSet<int>(navSystem.GetNeighbors(sourceNodeID));
        immediateNeighbours.Add(sourceNodeID);

        // First pass: respect minimum distance, exclude neighbours
        // Second pass: half minimum distance, exclude neighbours
        // Third pass: any distance, exclude only sourceNodeID
        float[] minDistFallbacks   = { minDestinationDistance, minDestinationDistance * 0.5f, 0f };
        bool[]  excludeNeighbours  = { true,                   true,                          false };

        for (int pass = 0; pass < minDistFallbacks.Length; pass++)
        {
            float minDist   = minDistFallbacks[pass];
            bool  exclNeigh = excludeNeighbours[pass];
            var valid = new List<int>();

            foreach (int id in allNodes)
            {
                if (!navSystem.nodeMap.ContainsKey(id)) continue;
                if (exclNeigh && immediateNeighbours.Contains(id)) continue;
                else if (id == sourceNodeID) continue;

                float d = Vector3.Distance(sourcePos, navSystem.nodeMap[id].worldPosition);
                if (d >= minDist && d <= maxDestinationDistance) valid.Add(id);
            }
            if (valid.Count > 0)
                return valid[Random.Range(0, valid.Count)];
        }
        return -1;
    }

    private bool _advancedThisFrame = false;

    private void AdvanceAlongSavedRoute()
    {
        if (_advancedThisFrame) return;
        _advancedThisFrame = true;
        lastAdvanceTime    = Time.time;

        currentPathIndex++;

        if (currentPathIndex >= savedRoutePath.Count)
        {
            Debug.Log($"[{gameObject.name}] Destination reached: Node {destinationNodeID}");
            sourceNodeID  = destinationNodeID;
            currentNodeID = sourceNodeID;
            PickNewDestinationAndSaveRoute();
            return;
        }

        int nextNodeID = savedRoutePath[currentPathIndex];
        int safeNodeID = FindNextReachableWaypoint(currentPathIndex);

        if (safeNodeID != nextNodeID)
        {
            int safeIndex = savedRoutePath.IndexOf(safeNodeID, currentPathIndex);
            if (safeIndex >= 0) currentPathIndex = safeIndex;
            nextNodeID = savedRoutePath[currentPathIndex];
        }

        currentNodeID = nextNodeID;
        SetTargetWaypoint(currentNodeID);
    }

    private int FindNextReachableWaypoint(int startIndex)
    {
        const int lookahead = 4;
        // Strip vehicles AND ground from the obstacle mask — only static geometry (buildings,
        // walls, barriers) should count as impassable for waypoint-reachability checks.
        // Vehicles (including parked NPCs or the player) must NOT block path lookahead or
        // the car will orbit a single node whenever another car is stopped on the road.
        int staticMask = obstacleLayer & ~groundLayer & ~vehicleLayerMask;

        for (int i = startIndex; i < Mathf.Min(startIndex + lookahead, savedRoutePath.Count); i++)
        {
            int nodeID = savedRoutePath[i];
            if (!navSystem.nodeMap.ContainsKey(nodeID)) continue;

            Vector3 nodePos = navSystem.nodeMap[nodeID].transform.position + Vector3.up * 1.5f;
            Vector3 from    = transform.position + Vector3.up * 1.5f;
            float   dist    = Vector3.Distance(from, nodePos);

            // If staticMask is 0 (nothing assigned) skip the cast and treat node as reachable
            if (staticMask == 0 || !Physics.SphereCast(from, 0.5f, (nodePos - from).normalized, out _, dist, staticMask))
                return nodeID;
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

        if (pathRecalculations >= MAX_RECALCULATIONS) { SnapToNearestReachableNode(); return; }

        int snappedNode = FindNearestNodeWithLineOfSight();
        if (snappedNode != -1) currentNodeID = snappedNode;

        List<int> newPath = navSystem.FindPath(currentNodeID, destinationNodeID);
        if (newPath != null && newPath.Count > 0)
        {
            sourceNodeID = currentNodeID;
            SaveRouteToVehicle(newPath, destinationNodeID);
        }
        else
        {
            sourceNodeID = currentNodeID;
            PickNewDestinationAndSaveRoute();
        }
    }

    private void FallbackPath()
    {
        // Try nearest reachable node first
        int nearestReachable = FindNearestNodeWithLineOfSight();
        if (nearestReachable != -1)
        {
            List<int> path = navSystem.FindPath(sourceNodeID, nearestReachable);
            if (path != null && path.Count > 0) { SaveRouteToVehicle(path, nearestReachable); return; }
        }

        // Hard fallback: snap to closest node and immediately pick a fresh destination.
        // Build a minimal synthetic route [closest] so savedRoutePath is never empty
        // and the next FixedUpdate does not immediately re-trigger FallbackPath.
        int nearest = navSystem.GetClosestNode(transform.position);
        if (nearest == -1 || !navSystem.nodeMap.ContainsKey(nearest)) return;

        sourceNodeID  = nearest;
        currentNodeID = nearest;

        // Synthetic single-node route keeps savedRoutePath valid while we search
        savedRoutePath   = new List<int> { nearest };
        currentPathIndex = 0;
        SetTargetWaypoint(nearest);

        if (!_isPickingDestination)
            PickNewDestinationAndSaveRoute();
    }

    private int FindNearestNodeWithLineOfSight()
    {
        float bestDist   = float.MaxValue;
        int   bestNode   = -1;
        int   buildingMask = obstacleLayer & ~groundLayer;

        foreach (var kvp in navSystem.nodeMap)
        {
            if (kvp.Value == null) continue;
            float dist = Vector3.Distance(transform.position, kvp.Value.transform.position);
            if (dist > 60f) continue;

            Vector3 from = transform.position + Vector3.up * 1.5f;
            Vector3 to   = kvp.Value.transform.position + Vector3.up * 1f;
            if (!Physics.Raycast(from, (to - from).normalized, Vector3.Distance(from, to), buildingMask) && dist < bestDist)
            {
                bestDist = dist;
                bestNode = kvp.Key;
            }
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
        }
    }

    // ===================================================
    // MOVE VEHICLE — slope-aware
    // ===================================================

    private void MoveVehicle()
    {
        if (targetWaypoint == null || rb == null || rb.isKinematic || currentSpeed < 0.1f) return;

        // 1. Yaw toward waypoint (XZ only)
        Vector3 toTargetXZ = new Vector3(
            targetWaypoint.position.x - transform.position.x, 0f,
            targetWaypoint.position.z - transform.position.z);

        if (toTargetXZ.sqrMagnitude > 0.01f)
        {
            Quaternion targetYaw   = Quaternion.LookRotation(toTargetXZ, Vector3.up);
            Quaternion currentYaw  = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            float turnMultiplier   = Mathf.Lerp(1f, 2.5f, angleToCurrentWaypoint / 180f);
            Quaternion smoothedYaw = Quaternion.Slerp(currentYaw, targetYaw,
                Time.fixedDeltaTime * turnSpeed * turnMultiplier);

            // 2. Blend in slope tilt
            Quaternion slopeTilt = Quaternion.FromToRotation(Vector3.up, groundNormal);
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, slopeTilt * smoothedYaw,
                Time.fixedDeltaTime * slopeTiltSpeed));
        }

        // 3. Alignment scaling — slow when mis-aimed (prevents back-and-forth)
        float alignFactor = Mathf.Clamp01(1f - Mathf.Clamp(angleToCurrentWaypoint, 0f, 180f) / 90f);
        alignFactor       = Mathf.Max(alignFactor * alignFactor, 0.02f);

        // 4. Drive forward
        rb.MovePosition(rb.position + transform.forward * currentSpeed * alignFactor * Time.fixedDeltaTime);

        // 5. Soft Y snap to ride height
        if (isGrounded)
        {
            float yError = (currentGroundY + rideHeight) - rb.position.y;
            if (Mathf.Abs(yError) > 0.05f)
                rb.MovePosition(rb.position + Vector3.up * yError * groundSnapStrength * Time.fixedDeltaTime);
        }
    }

    // ===================================================
    // OBSTACLE DETECTION — slope-aware, terrain excluded
    // ===================================================

    private bool DetectObstacle()
    {
        Vector3 rayStart = transform.position + Vector3.up * 1.2f;
        Vector3 rawFwd   = transform.forward;
        Vector3 levelFwd = new Vector3(rawFwd.x, Mathf.Max(rawFwd.y, -0.15f), rawFwd.z).normalized;
        int buildingMask = obstacleLayer & ~groundLayer;

        if (Physics.Raycast(rayStart, levelFwd, out RaycastHit hit, detectionRange, buildingMask))
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                TrafficVehicle other = hit.collider.GetComponent<TrafficVehicle>();
                if (other != null && hit.distance < stoppingDistance)
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
        }

        if (Physics.SphereCast(rayStart, 0.8f, levelFwd, out hit, detectionRange, buildingMask))
        {
            if (!hit.collider.transform.IsChildOf(transform))
            {
                TrafficVehicle other = hit.collider.GetComponent<TrafficVehicle>();
                if (other != null && hit.distance < stoppingDistance) return true;
            }
        }

        if (showDebugGizmos) Debug.DrawRay(rayStart, levelFwd * detectionRange, Color.green);
        return false;
    }

    // ===================================================
    // UTILITY
    // ===================================================

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
        debugPathProgress       = currentPathIndex;
        debugCurrentSpeed       = currentSpeed;
        debugProgressPercent    = savedRoutePath.Count > 0 ? (float)currentPathIndex / savedRoutePath.Count * 100f : 0f;
        debugDistanceToWaypoint = targetWaypoint != null ? Vector3.Distance(transform.position, targetWaypoint.position) : 0f;

        if (navSystem != null && navSystem.nodeMap.ContainsKey(destinationNodeID))
            debugDistanceToDestination = Vector3.Distance(transform.position, navSystem.nodeMap[destinationNodeID].worldPosition);

        if (savedRoutePath != null && savedRoutePath.Count > 0 && currentPathIndex < savedRoutePath.Count)
        {
            int rem     = savedRoutePath.Count - currentPathIndex;
            int preview = Mathf.Min(3, rem);
            debugNextNodes = string.Join(" > ", savedRoutePath.Skip(currentPathIndex).Take(preview));
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
        }

        if (currentTrafficLight != null)
        {
            Gizmos.color = debugAtRedLight ? Color.red : Color.yellow;
            Gizmos.DrawLine(transform.position + Vector3.up * 2f, currentTrafficLight.transform.position);
        }

        if (detectedVehicleAhead != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f,
                            detectedVehicleAhead.transform.position + Vector3.up * 1.5f);
        }

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

        string status     = debugIsStuck ? "STUCK" : (isStopped ? "STOPPED" : "MOVING");
        string stopReason = "";
        if (isStopped)
        {
            if (debugAtRedLight)                stopReason = " [RED LIGHT]";
            else if (debugVehicleAheadDetected) stopReason = " [VEHICLE AHEAD]";
            else if (debugIsObstacleDetected)   stopReason = " [OBSTACLE]";
        }

        Handles.Label(
            transform.position + Vector3.up * 7f,
            $"{gameObject.name} {status}{stopReason}\n" +
            $"Route: {sourceNodeID} > {destinationNodeID}  ({debugProgressPercent:F0}%)\n" +
            $"Next: {debugNextNodes}\n" +
            $"Speed: {debugCurrentSpeed:F1} m/s ({debugCurrentSpeed * 3.6f:F0} km/h)\n" +
            $"Grounded: {debugIsGrounded}  Slope: {debugSlopeAngle:F1}\n" +
            $"VehicleLayer mask: {vehicleLayerMask.value}\n" +
            $"Traffic: {debugTrafficLightID} [{debugTrafficLightState}]\n" +
            $"Vehicle ahead: {(detectedVehicleAhead != null ? $"YES {debugDistanceToVehicleAhead:F1}m" : "NO")}\n" +
            $"WheelColliders:{debugWheelCollidersFound}  VisualWheels:{debugVisualWheelsFound}",
            new GUIStyle
            {
                normal    = new GUIStyleState { textColor = Color.white },
                fontSize  = 11,
                fontStyle = FontStyle.Bold
            });
#endif
    }
}