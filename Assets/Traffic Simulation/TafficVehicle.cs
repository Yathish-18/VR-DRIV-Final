// TRAFFIC VEHICLE - DESTINATION-BASED NAVIGATION WITH TRAFFIC LIGHT COMPLIANCE
// Saves complete route and navigates between random destinations
// Fixed: Cars now properly stop at red lights without reversing

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
    [SerializeField] private List<int> savedRoutePath = new List<int>(); // SAVED PATH
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
    private float waypointReachDistance = 5f;
    private int stuckCounter = 0;
    private const int MAX_STUCK_FRAMES = 180; // 3 seconds at 60fps
    private int pathRecalculations = 0;
    private const int MAX_RECALCULATIONS = 3;

    // ========================================
    // FIXED: TRAFFIC LIGHT DETECTION
    // ========================================
    [Header("=== TRAFFIC LIGHT DETECTION ===")]
    [SerializeField] private float trafficLightDetectionRange = 5f;
    [SerializeField] private float trafficLightStoppingDistance = 7f;
    [SerializeField] private LayerMask trafficLightLayerMask = -1;
    [SerializeField] private bool enableTrafficLightCompliance = true;

    private EnhancedTrafficLightViolationDetector currentTrafficLight = null;
    private bool isInTrafficLightZone = false;
    private bool isStoppedAtRedLight = false;
    private float timeEnteredRedLightZone = 0f;
    private Vector3 redLightStopPosition = Vector3.zero;
    private bool hasReachedStopPosition = false;

    // ========================================
    // IMPROVED VEHICLE-AHEAD DETECTION
    // ========================================
    [Header("=== VEHICLE AHEAD DETECTION ===")]
    [SerializeField] private float vehicleDetectionRange = 20f;
    [SerializeField] private float vehicleStoppingDistance = 10f;
    [SerializeField] private float lateralDetectionWidth = 2.5f;
    [SerializeField] private int multiRayCount = 3;
    [SerializeField] private bool enableVehicleAheadDetection = true;

    private GameObject detectedVehicleAhead = null;
    private float distanceToVehicleAhead = 0f;

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

    private Color debugColor = Color.green;
    private float targetSpeed = 0f;
    private float speedSmoothVelocity = 0f;
    private float speedSmoothTime = 0.3f;

    public void Initialize(CentralizedNavigationSystem navSys, int startNodeID, float speed, float stopDist, float detectRange, LayerMask obstacles)
    {
        navSystem = navSys;
        maxSpeed = speed;
        stoppingDistance = stopDist;
        detectionRange = detectRange;
        obstacleLayer = obstacles;

        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        maxSpeed *= Random.Range(0.85f, 1.15f);
        acceleration = maxSpeed * 1.5f;
        turnSpeed = maxSpeed * 0.3f;

        // Use the position we're already placed at (CentralizedNavigationSystem handled grounding)
        lastValidPosition = transform.position;
        rb.position = transform.position;

        if (startNodeID != -1 && navSystem.nodeMap.ContainsKey(startNodeID))
        {
            sourceNodeID = startNodeID;
            currentNodeID = startNodeID;
        }
        else
        {
            sourceNodeID = navSystem.GetRandomNode();
            currentNodeID = sourceNodeID;
        }

        debugColor = new Color(Random.value, Random.value, Random.value);

        PickNewDestinationAndSaveRoute();

        Debug.Log($"[{gameObject.name}] ========== INITIALIZATION ==========");
        Debug.Log($"[{gameObject.name}] Spawn Node: {sourceNodeID}");
        Debug.Log($"[{gameObject.name}] Position: {transform.position}");
        Debug.Log($"[{gameObject.name}] Max Speed: {maxSpeed:F1} m/s");
        Debug.Log($"[{gameObject.name}] =====================================");
    }

    private void FixedUpdate()
    {
        if (navSystem == null || savedRoutePath == null || savedRoutePath.Count == 0)
            return;

        // Skip all movement logic while kinematic (spawning phase)
        if (rb != null && rb.isKinematic)
            return;

        UpdateDebugInfo();

        DetectTrafficLightAhead();
        DetectVehicleAhead();

        bool hasObstacle = DetectObstacle();
        debugIsObstacleDetected = hasObstacle;

        float distanceToTarget = targetWaypoint != null ? Vector3.Distance(transform.position, targetWaypoint.position) : 0f;

        if (distanceToTarget < waypointReachDistance)
        {
            AdvanceAlongSavedRoute();
        }

        bool shouldStopForTrafficLight = ShouldStopForTrafficLight();
        bool shouldStopForVehicle = ShouldStopForVehicleAhead();

        bool shouldStop = hasObstacle || shouldStopForTrafficLight || shouldStopForVehicle;
        targetSpeed = shouldStop ? 0f : maxSpeed;
        isStopped = shouldStop;

        debugAtRedLight = shouldStopForTrafficLight;
        debugVehicleAheadDetected = shouldStopForVehicle;

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

        MoveVehicle();

        if (!shouldStopForTrafficLight && !shouldStopForVehicle)
        {
            float movedDistance = Vector3.Distance(transform.position, lastValidPosition);
            if (movedDistance < 0.1f && currentSpeed > 0.1f)
            {
                stuckCounter++;
                debugIsStuck = true;

                if (stuckCounter >= MAX_STUCK_FRAMES)
                {
                    Debug.LogWarning($"[{gameObject.name}] ⚠️ STUCK for 3 seconds! Attempting recovery...");
                    RecoverFromStuck();
                }
            }
            else
            {
                stuckCounter = 0;
                debugIsStuck = false;
                lastValidPosition = transform.position;
            }
        }
        else
        {
            stuckCounter = 0;
            debugIsStuck = false;
            lastValidPosition = transform.position;
        }
    }

    // ========================================
    // TRAFFIC LIGHT DETECTION METHODS
    // ========================================

    private void DetectTrafficLightAhead()
    {
        if (!enableTrafficLightCompliance)
        {
            currentTrafficLight = null;
            isInTrafficLightZone = false;
            debugTrafficLightID = "Disabled";
            debugTrafficLightState = "N/A";
            debugDistanceToTrafficLight = 0f;
            return;
        }

        Vector3 rayStart = transform.position + Vector3.up * 1f;
        Vector3 rayDirection = transform.forward;

        RaycastHit[] hits = Physics.RaycastAll(rayStart, rayDirection, trafficLightDetectionRange, trafficLightLayerMask);

        EnhancedTrafficLightViolationDetector closestLight = null;
        float closestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            EnhancedTrafficLightViolationDetector detector = hit.collider.GetComponent<EnhancedTrafficLightViolationDetector>();
            if (detector != null && hit.distance < closestDistance)
            {
                closestLight = detector;
                closestDistance = hit.distance;
            }
        }

        if (closestLight != null)
        {
            bool isNewLight = (currentTrafficLight == null || currentTrafficLight != closestLight);
            currentTrafficLight = closestLight;
            isInTrafficLightZone = true;
            debugDistanceToTrafficLight = closestDistance;
            debugTrafficLightID = currentTrafficLight.GetTrafficLightID();

            TrafficLightController trafficLightController = currentTrafficLight.GetTrafficLight();
            if (trafficLightController != null)
            {
                debugTrafficLightState = trafficLightController.currentState.ToString();

                if (isNewLight && trafficLightController.currentState == TrafficLightController.LightState.Red)
                {
                    Vector3 directionToLight = (currentTrafficLight.transform.position - transform.position).normalized;
                    redLightStopPosition = currentTrafficLight.transform.position - (directionToLight * trafficLightStoppingDistance);
                    redLightStopPosition.y = transform.position.y;
                    hasReachedStopPosition = false;

                    if (showDebugGizmos)
                        Debug.Log($"[{gameObject.name}] 🚦 Red light detected at {debugTrafficLightID}");
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
                isInTrafficLightZone = false;
                isStoppedAtRedLight = false;
                hasReachedStopPosition = false;

                if (showDebugGizmos)
                    Debug.Log($"[{gameObject.name}] ✅ Cleared traffic light zone");
            }

            currentTrafficLight = null;
            debugTrafficLightID = "None";
            debugTrafficLightState = "N/A";
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

        TrafficLightController trafficLightController = currentTrafficLight.GetTrafficLight();
        if (trafficLightController == null)
        {
            isStoppedAtRedLight = false;
            return false;
        }

        TrafficLightController.LightState lightState = trafficLightController.currentState;

        if (lightState == TrafficLightController.LightState.Red)
        {
            float distanceToStopPosition = Vector3.Distance(transform.position, redLightStopPosition);

            if (!isStoppedAtRedLight)
            {
                if (distanceToStopPosition < 2f || debugDistanceToTrafficLight < trafficLightStoppingDistance)
                {
                    isStoppedAtRedLight = true;
                    hasReachedStopPosition = true;
                    timeEnteredRedLightZone = Time.time;

                    if (showDebugGizmos)
                        Debug.Log($"[{gameObject.name}] 🛑 STOPPED at RED light {debugTrafficLightID}");
                }
            }

            if (isStoppedAtRedLight) return true;
            if (debugDistanceToTrafficLight < trafficLightStoppingDistance * 1.5f) return true;
        }

        if (lightState == TrafficLightController.LightState.Yellow)
        {
            if (debugDistanceToTrafficLight < trafficLightStoppingDistance * 0.8f)
            {
                isStoppedAtRedLight = true;
                return true;
            }
        }

        if (lightState == TrafficLightController.LightState.Green)
        {
            if (isStoppedAtRedLight && showDebugGizmos)
                Debug.Log($"[{gameObject.name}] 🟢 GREEN light! Proceeding through {debugTrafficLightID}");

            isStoppedAtRedLight = false;
            hasReachedStopPosition = false;
            return false;
        }

        return false;
    }

    // ========================================
    // VEHICLE-AHEAD DETECTION
    // ========================================

    private void DetectVehicleAhead()
    {
        if (!enableVehicleAheadDetection)
        {
            detectedVehicleAhead = null;
            distanceToVehicleAhead = 0f;
            debugDistanceToVehicleAhead = 0f;
            return;
        }

        Vector3 rayStart = transform.position + Vector3.up * 1f;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        GameObject closestVehicle = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < multiRayCount; i++)
        {
            Vector3 rayDirection = forward;
            if (i == 1) rayDirection = (forward + (-right * lateralDetectionWidth)).normalized;
            else if (i == 2) rayDirection = (forward + (right * lateralDetectionWidth)).normalized;

            RaycastHit[] hits = Physics.RaycastAll(rayStart, rayDirection, vehicleDetectionRange, obstacleLayer);

            foreach (RaycastHit hit in hits)
            {
                GameObject vehicleObject = FindVehicleInHierarchy(hit.collider.gameObject);
                if (vehicleObject != null && vehicleObject != gameObject && hit.distance < closestDistance)
                {
                    closestVehicle = vehicleObject;
                    closestDistance = hit.distance;
                }
            }

            if (showDebugGizmos)
            {
                Color rayColor = closestVehicle != null ? Color.red : Color.cyan;
                Debug.DrawRay(rayStart, rayDirection * vehicleDetectionRange, rayColor);
            }
        }

        if (closestVehicle != null)
        {
            detectedVehicleAhead = closestVehicle;
            distanceToVehicleAhead = closestDistance;
            debugDistanceToVehicleAhead = closestDistance;
        }
        else
        {
            detectedVehicleAhead = null;
            distanceToVehicleAhead = 0f;
            debugDistanceToVehicleAhead = 0f;
        }
    }

    private GameObject FindVehicleInHierarchy(GameObject obj)
    {
        TrafficVehicle vehicle = obj.GetComponent<TrafficVehicle>();
        if (vehicle != null) return obj;

        Transform current = obj.transform;
        while (current != null)
        {
            vehicle = current.GetComponent<TrafficVehicle>();
            if (vehicle != null) return current.gameObject;
            current = current.parent;
        }

        return null;
    }

    private bool ShouldStopForVehicleAhead()
    {
        if (!enableVehicleAheadDetection || detectedVehicleAhead == null)
            return false;

        if (distanceToVehicleAhead < vehicleStoppingDistance)
        {
            TrafficVehicle vehicleAheadScript = detectedVehicleAhead.GetComponent<TrafficVehicle>();

            if (vehicleAheadScript != null)
            {
                if (vehicleAheadScript.currentSpeed < 1f || vehicleAheadScript.isStopped)
                {
                    if (showDebugGizmos && Time.frameCount % 60 == 0)
                        Debug.Log($"[{gameObject.name}] 🚗 Stopping for vehicle ahead '{detectedVehicleAhead.name}' at {distanceToVehicleAhead:F1}m");
                    return true;
                }
            }

            return distanceToVehicleAhead < vehicleStoppingDistance * 0.7f;
        }

        return false;
    }

    // ========================================
    // ROUTE MANAGEMENT
    // ========================================

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
            int randomDestination = PickDestinationWithinRange(allNodes);

            if (randomDestination == -1)
            {
                Debug.LogWarning($"[{gameObject.name}] No suitable destination in range, using random node");
                randomDestination = navSystem.GetRandomNode(new HashSet<int> { sourceNodeID });
            }

            List<int> calculatedPath = navSystem.FindPath(sourceNodeID, randomDestination);

            if (calculatedPath == null || calculatedPath.Count == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] No path: {sourceNodeID} → {randomDestination}, attempt {attempt + 1}/{maxPathAttempts}");
                continue;
            }

            if (calculatedPath.Count < minPathLength)
            {
                Debug.LogWarning($"[{gameObject.name}] Path too short ({calculatedPath.Count}), attempt {attempt + 1}/{maxPathAttempts}");
                continue;
            }

            if (calculatedPath.Count > maxPathLength)
            {
                Debug.LogWarning($"[{gameObject.name}] Path too long ({calculatedPath.Count}), attempt {attempt + 1}/{maxPathAttempts}");
                continue;
            }

            SaveRouteToVehicle(calculatedPath, randomDestination);
            return;
        }

        Debug.LogError($"[{gameObject.name}] ❌ Failed to find valid path after {maxPathAttempts} attempts! Using fallback.");
        FallbackPath();
    }

    private void SaveRouteToVehicle(List<int> path, int destination)
    {
        savedRoutePath = new List<int>(path);
        destinationNodeID = destination;
        currentPathIndex = 0;
        pathRecalculations = 0;

        SetTargetWaypoint(savedRoutePath[currentPathIndex]);

        debugRouteName = $"Route_{sourceNodeID}_to_{destinationNodeID}";
        debugTotalNodes = savedRoutePath.Count;
        debugSavedRoute = string.Join(" → ", savedRoutePath);

        int previewCount = Mathf.Min(5, savedRoutePath.Count);
        debugNextNodes = string.Join(" → ", savedRoutePath.Take(previewCount));
        if (savedRoutePath.Count > previewCount) debugNextNodes += "...";

        Debug.Log($"[{gameObject.name}] ========== NEW ROUTE SAVED ==========");
        Debug.Log($"[{gameObject.name}] Source: Node {sourceNodeID} → Destination: Node {destinationNodeID}");
        Debug.Log($"[{gameObject.name}] Route Length: {savedRoutePath.Count} nodes");
        Debug.Log($"[{gameObject.name}] =====================================");
    }

    private int PickDestinationWithinRange(List<int> allNodes)
    {
        if (!navSystem.nodeMap.ContainsKey(sourceNodeID)) return -1;

        Vector3 sourcePos = navSystem.nodeMap[sourceNodeID].worldPosition;
        List<int> validDestinations = new List<int>();

        foreach (int nodeID in allNodes)
        {
            if (nodeID == sourceNodeID) continue;
            if (!navSystem.nodeMap.ContainsKey(nodeID)) continue;

            float distance = Vector3.Distance(sourcePos, navSystem.nodeMap[nodeID].worldPosition);
            if (distance >= minDestinationDistance && distance <= maxDestinationDistance)
                validDestinations.Add(nodeID);
        }

        return validDestinations.Count == 0 ? -1 : validDestinations[Random.Range(0, validDestinations.Count)];
    }

    private void AdvanceAlongSavedRoute()
    {
        currentPathIndex++;

        if (currentPathIndex >= savedRoutePath.Count)
        {
            Debug.Log($"[{gameObject.name}] ✅ DESTINATION REACHED: Node {destinationNodeID}");
            sourceNodeID = destinationNodeID;
            currentNodeID = sourceNodeID;
            PickNewDestinationAndSaveRoute();
            return;
        }

        int nextNodeID = savedRoutePath[currentPathIndex];
        currentNodeID = nextNodeID;
        SetTargetWaypoint(currentNodeID);

        float progressPercent = ((float)currentPathIndex / savedRoutePath.Count) * 100f;
        Debug.Log($"[{gameObject.name}] Route Progress: {currentPathIndex}/{savedRoutePath.Count} ({progressPercent:F0}%) → Node {currentNodeID}");
    }

    private void SetTargetWaypoint(int nodeID)
    {
        if (navSystem.nodeMap.ContainsKey(nodeID))
        {
            targetWaypoint = navSystem.nodeMap[nodeID].transform;
            currentNodeID = nodeID;
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Node {nodeID} not found in nodeMap!");
        }
    }

    private void RecoverFromStuck()
    {
        pathRecalculations++;

        if (pathRecalculations >= MAX_RECALCULATIONS)
        {
            Debug.LogError($"[{gameObject.name}] Too many recalculations! Picking new destination.");
            sourceNodeID = currentNodeID;
            PickNewDestinationAndSaveRoute();
            return;
        }

        List<int> newPath = navSystem.FindPath(currentNodeID, destinationNodeID);

        if (newPath != null && newPath.Count > 0)
        {
            SaveRouteToVehicle(newPath, destinationNodeID);
            stuckCounter = 0;
            Debug.Log($"[{gameObject.name}] ✅ Path successfully recalculated!");
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] ❌ Recalculation failed! Picking new destination.");
            sourceNodeID = currentNodeID;
            PickNewDestinationAndSaveRoute();
        }
    }

    private void FallbackPath()
    {
        int nearestNode = navSystem.GetClosestNode(transform.position);

        if (nearestNode != -1 && nearestNode != sourceNodeID)
        {
            List<int> path = navSystem.FindPath(sourceNodeID, nearestNode);
            if (path != null && path.Count > 0)
            {
                SaveRouteToVehicle(path, nearestNode);
                Debug.LogWarning($"[{gameObject.name}] Using fallback path to nearest node {nearestNode}");
                return;
            }
        }

        Debug.LogError($"[{gameObject.name}] EMERGENCY: Teleporting to random node!");
        int randomNode = navSystem.GetRandomNode();

        if (navSystem.nodeMap.ContainsKey(randomNode))
        {
            Vector3 newPos = navSystem.nodeMap[randomNode].worldPosition;
            transform.position = newPos;
            if (!rb.isKinematic) rb.position = newPos;

            sourceNodeID = randomNode;
            currentNodeID = randomNode;
            SetTargetWaypoint(randomNode);
            PickNewDestinationAndSaveRoute();
        }
    }

    // ========================================
    // MOVEMENT
    // ========================================

    private void MoveVehicle()
    {
        if (targetWaypoint == null) return;
        if (rb == null || rb.isKinematic) return;  // ← KEY FIX: never touch velocity while kinematic

        if (currentSpeed < 0.1f)
        {
            // Brake to a stop — only set velocity if NOT kinematic
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, Vector3.zero, Time.fixedDeltaTime * 5f);
            return;
        }

        Vector3 direction = (targetWaypoint.position - transform.position);
        direction.y = 0;
        direction.Normalize();

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
        }

        Vector3 forwardMovement = transform.forward * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + forwardMovement);

        // Lock X/Z rotation
        Vector3 euler = transform.eulerAngles;
        euler.x = 0;
        euler.z = 0;
        transform.eulerAngles = euler;

        // Periodic ground snapping
        if (Time.frameCount % 30 == 0)
        {
            Vector3 snappedPos = SnapToGround(transform.position);
            if (Vector3.Distance(transform.position, snappedPos) < 5f)
            {
                transform.position = snappedPos;
                rb.position = snappedPos;
            }
        }
    }

    private bool DetectObstacle()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;

        if (Physics.Raycast(rayStart, rayDirection, out RaycastHit hit, detectionRange, obstacleLayer))
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

        if (Physics.SphereCast(rayStart, 1f, rayDirection, out hit, detectionRange, obstacleLayer))
        {
            TrafficVehicle otherVehicle = hit.collider.GetComponent<TrafficVehicle>();
            if (otherVehicle != null && hit.distance < stoppingDistance) return true;
        }

        if (showDebugGizmos) Debug.DrawRay(rayStart, rayDirection * detectionRange, Color.green);
        return false;
    }

    private Vector3 SnapToGround(Vector3 position, float rayDistance = 50f)
    {
        Vector3 rayStart = new Vector3(position.x, position.y + 20f, position.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance + 20f))
            return hit.point + Vector3.up * 0.5f;
        if (Physics.Raycast(position, Vector3.down, out hit, rayDistance))
            return hit.point + Vector3.up * 0.5f;
        return position;
    }

    private void UpdateDebugInfo()
    {
        debugPathProgress = currentPathIndex;
        debugCurrentSpeed = currentSpeed;
        debugProgressPercent = savedRoutePath.Count > 0 ? ((float)currentPathIndex / savedRoutePath.Count) * 100f : 0f;
        debugDistanceToWaypoint = targetWaypoint != null ? Vector3.Distance(transform.position, targetWaypoint.position) : 0f;

        if (navSystem != null && navSystem.nodeMap.ContainsKey(destinationNodeID))
            debugDistanceToDestination = Vector3.Distance(transform.position, navSystem.nodeMap[destinationNodeID].worldPosition);

        if (savedRoutePath != null && savedRoutePath.Count > 0 && currentPathIndex < savedRoutePath.Count)
        {
            int remainingNodes = savedRoutePath.Count - currentPathIndex;
            int previewCount = Mathf.Min(3, remainingNodes);
            debugNextNodes = string.Join(" → ", savedRoutePath.Skip(currentPathIndex).Take(previewCount));
            if (remainingNodes > previewCount) debugNextNodes += $" ... (+{remainingNodes - previewCount} more)";
        }
    }

    // ========================================
    // GIZMOS
    // ========================================

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || navSystem == null) return;

        if (savedRoutePath != null && savedRoutePath.Count > 1)
        {
            for (int i = 0; i < savedRoutePath.Count - 1; i++)
            {
                if (!navSystem.nodeMap.ContainsKey(savedRoutePath[i]) || !navSystem.nodeMap.ContainsKey(savedRoutePath[i + 1])) continue;

                Vector3 start = navSystem.nodeMap[savedRoutePath[i]].worldPosition + Vector3.up * 1.5f;
                Vector3 end = navSystem.nodeMap[savedRoutePath[i + 1]].worldPosition + Vector3.up * 1.5f;

                Gizmos.color = i < currentPathIndex ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : debugColor;
                Gizmos.DrawLine(start, end);

                if (i >= currentPathIndex) Gizmos.DrawWireSphere(start, 0.5f);
            }
        }

        if (targetWaypoint != null)
        {
            Gizmos.color = isStopped ? Color.red : (debugIsStuck ? Color.magenta : debugColor);
            Gizmos.DrawLine(transform.position + Vector3.up, targetWaypoint.position + Vector3.up);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetWaypoint.position, waypointReachDistance);
        }

        if (navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            Vector3 destPos = navSystem.nodeMap[destinationNodeID].worldPosition;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(destPos + Vector3.up * 3f, 3f);
            Gizmos.DrawLine(destPos, destPos + Vector3.up * 6f);
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
            Gizmos.DrawLine(transform.position + Vector3.up * 1.5f, detectedVehicleAhead.transform.position + Vector3.up * 1.5f);
            Gizmos.DrawWireSphere(detectedVehicleAhead.transform.position + Vector3.up * 3f, 1.5f);
        }

        if (debugIsStuck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 4f, 2f);
        }

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 5f);
    }

    private void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || targetWaypoint == null) return;

        string status = debugIsStuck ? "🚫 STUCK" : (isStopped ? "⛔ STOPPED" : "✅ MOVING");
        string stopReason = "";
        if (isStopped)
        {
            if (debugAtRedLight) stopReason = " [RED LIGHT]";
            else if (debugVehicleAheadDetected) stopReason = " [VEHICLE AHEAD]";
            else if (debugIsObstacleDetected) stopReason = " [OBSTACLE]";
        }

        Handles.Label(
            transform.position + Vector3.up * 6f,
            $"{gameObject.name} {status}{stopReason}\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            $"Route: {sourceNodeID} → {destinationNodeID}\n" +
            $"Progress: {currentPathIndex}/{savedRoutePath.Count} ({debugProgressPercent:F0}%)\n" +
            $"Current Node: {currentNodeID}\n" +
            $"Next: {debugNextNodes}\n" +
            $"Distance to Dest: {debugDistanceToDestination:F1}m\n" +
            $"Speed: {debugCurrentSpeed:F1} m/s ({debugCurrentSpeed * 3.6f:F0} km/h)\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            $"Traffic Light: {debugTrafficLightID} [{debugTrafficLightState}]\n" +
            $"Distance to Light: {debugDistanceToTrafficLight:F1}m\n" +
            $"Vehicle Ahead: {(detectedVehicleAhead != null ? $"YES ({debugDistanceToVehicleAhead:F1}m)" : "NO")}\n" +
            $"Recalculations: {pathRecalculations}/{MAX_RECALCULATIONS}",
            new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.white },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            }
        );
#endif
    }
}