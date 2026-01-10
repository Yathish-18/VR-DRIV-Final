using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(Rigidbody))]
public class NPCVehicleController : MonoBehaviour
{
    [Header("=== REALISTIC DRIVING PARAMETERS ===")]
    [SerializeField] private float maxSpeed = 12f;
    [SerializeField] private float acceleration = 3f;
    [SerializeField] private float brakeForce = 8f;
    [SerializeField] private float turnSmoothness = 3f;
    [SerializeField] private float maxSteerAngle = 35f;
    [SerializeField] private float cornerSlowdownFactor = 0.4f;

    [Header("=== COLLISION AVOIDANCE ===")]
    [SerializeField] private float visionRange = 30f;
    [SerializeField] private float visionAngle = 60f;
    [SerializeField] private float safeDistance = 15f;
    [SerializeField] private float emergencyDistance = 6f;
    [SerializeField] private int raycastDensity = 7;
    [SerializeField] private float laneChangeSpeed = 0.5f;

    [Header("=== PATH SETTINGS ===")]
    [SerializeField] private float waypointReachDistance = 8f;
    [SerializeField] private float pathLookahead = 20f;
    [SerializeField] private int smoothingIterations = 5;
    [SerializeField] private float destinationReachedDistance = 10f;

    [Header("=== TRAFFIC COMPLIANCE ===")]
    [SerializeField] private float stopLineDistance = 5f;
    [SerializeField] private float trafficLightReactionDistance = 25f;
    [SerializeField] private float followDistance = 12f;

    [Header("=== COMPONENTS ===")]
    [SerializeField] private Transform[] wheels;
    [SerializeField] private float wheelRotationMultiplier = 360f;

    [Header("=== DEBUG ===")]
    [SerializeField] private bool debugVisualization = true;
    [SerializeField] private bool showDestination = true;

    // Components
    private Rigidbody rb;
    private CentralizedNavigationSystem navSystem;
    private Collider vehicleCollider;

    // Navigation state - CRITICAL: Track previous destination as new start
    private int currentStartNode = -1;
    private int currentDestinationNode = -1;
    private Vector3 currentDestinationPosition;

    private List<int> currentPath = new List<int>();
    private List<Vector3> smoothedPath = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private Vector3 targetPosition;
    private Vector3 futureTargetPosition;

    // Movement state
    private float currentSpeed = 0f;
    private float targetSpeed = 0f;
    private float desiredSteerAngle = 0f;
    private float currentSteerAngle = 0f;

    // Avoidance
    private Vector3 laneOffset = Vector3.zero;
    private List<ObstacleInfo> detectedObstacles = new List<ObstacleInfo>();

    // Stuck detection
    private bool isStuck = false;
    private Vector3 lastPosition;
    private float stuckTimer = 0f;
    private float stuckCheckInterval = 4f;
    private float minMovementThreshold = 1.5f;

    // Traffic light
    private TrafficLightController currentTrafficLight;
    private LayerMask obstacleLayer;

    // Initialization
    private bool isInitialized = false;
    private float totalDistanceTraveled = 0f;

    private class ObstacleInfo
    {
        public Transform transform;
        public Vector3 position;
        public float distance;
        public Vector3 relativePosition;
        public bool isVehicle;
    }

    #region Initialization

    // Initialize with specific spawn and destination nodes
    public void InitializeWithDestination(CentralizedNavigationSystem nav, int spawnNodeID, int destinationNodeID, int index)
    {
        if (nav == null)
        {
            Debug.LogError($"[{name}] Navigation system is null!");
            return;
        }

        navSystem = nav;
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogError($"[{name}] Missing Rigidbody component!");
            return;
        }

        SetupPhysics();
        FindWheels();
        lastPosition = transform.position;

        // CRITICAL: Set start node (spawn location)
        currentStartNode = spawnNodeID;
        currentDestinationNode = destinationNodeID;

        if (navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            currentDestinationPosition = navSystem.nodeMap[destinationNodeID].worldPosition;
        }

        Debug.Log($"[{name}] 🎯 Initialized: Spawn Node {spawnNodeID} → Destination Node {destinationNodeID}");

        StartCoroutine(InitializeAsync());
    }

    private IEnumerator InitializeAsync()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 2f));

        if (navSystem == null || navSystem.nodeMap == null || navSystem.nodeMap.Count == 0)
        {
            Debug.LogWarning($"[{name}] Navigation system not ready, retrying...");
            yield return new WaitForSeconds(2f);
        }

        // Request path from spawn to destination
        RequestPathToDestination();
        isInitialized = true;
    }

    private void SetupPhysics()
    {
        rb.mass = 1200f;
        rb.linearDamping = 1.2f;
        rb.angularDamping = 15f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.centerOfMass = new Vector3(0, -0.5f, 0);

        obstacleLayer = LayerMask.GetMask("Default", "Vehicle");

        foreach (var mc in GetComponentsInChildren<MeshCollider>())
        {
            if (!mc.isTrigger && mc.sharedMesh != null)
            {
                mc.convex = true;
            }
        }

        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger)
            {
                vehicleCollider = col;
                break;
            }
        }

        if (vehicleCollider == null)
        {
            Debug.LogWarning($"[{name}] No collider found, adding BoxCollider");
            vehicleCollider = gameObject.AddComponent<BoxCollider>();
        }
    }

    private void FindWheels()
    {
        if (wheels == null || wheels.Length == 0)
        {
            List<Transform> foundWheels = new List<Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                string wheelName = child.name.ToLower();
                if (wheelName.Contains("wheel") || wheelName.Contains("tire") || wheelName.Contains("rim"))
                {
                    foundWheels.Add(child);
                }
            }
            wheels = foundWheels.ToArray();
        }
    }

    #endregion

    #region Update Loop

    private void FixedUpdate()
    {
        if (!isInitialized || navSystem == null || smoothedPath.Count == 0 || rb == null)
        {
            return;
        }

        float distThisFrame = Vector3.Distance(transform.position, lastPosition);
        totalDistanceTraveled += distThisFrame;

        CheckDestinationReached();
        CheckStuckState();
        DetectObstacles();
        UpdateTargetWaypoint();
        CalculateLaneOffset();
        CalculateDesiredSteering();
        CalculateSpeed();
        ApplyPhysics();

        lastPosition = transform.position;
    }

    private void Update()
    {
        AnimateWheels();
    }

    #endregion

    #region Destination Management

    // CRITICAL: When destination reached, previous destination becomes the new start
    private void PickNewDestination()
    {
        if (navSystem == null || navSystem.nodeMap == null || navSystem.nodeMap.Count < 2)
        {
            Debug.LogWarning($"[{name}] Cannot pick destination: navigation system not ready");
            StartCoroutine(RetryPickDestination());
            return;
        }

        // IMPORTANT: Use previous destination as new start point
        // If no previous destination, use closest node
        if (currentDestinationNode != -1 && navSystem.nodeMap.ContainsKey(currentDestinationNode))
        {
            currentStartNode = currentDestinationNode;
            Debug.Log($"[{name}] 📍 Using previous destination {currentDestinationNode} as new start");
        }
        else
        {
            // Fallback: use closest node to current position
            currentStartNode = navSystem.GetClosestNode(transform.position);
            Debug.Log($"[{name}] 📍 Using closest node {currentStartNode} as new start");
        }

        if (!navSystem.nodeMap.ContainsKey(currentStartNode))
        {
            Debug.LogError($"[{name}] No valid start node {currentStartNode}");
            StartCoroutine(RetryPickDestination());
            return;
        }

        // Pick random destination (exclude current start node)
        HashSet<int> excludeSet = new HashSet<int> { currentStartNode };
        int newDestination = navSystem.GetRandomNode(excludeSet);

        if (newDestination == -1 || !navSystem.nodeMap.ContainsKey(newDestination))
        {
            Debug.LogWarning($"[{name}] Failed to get random destination");
            StartCoroutine(RetryPickDestination());
            return;
        }

        // Update destination
        currentDestinationNode = newDestination;
        currentDestinationPosition = navSystem.nodeMap[currentDestinationNode].worldPosition;
        totalDistanceTraveled = 0f;

        float routeDistance = Vector3.Distance(
            navSystem.nodeMap[currentStartNode].worldPosition,
            currentDestinationPosition
        );

        Debug.Log($"[{name}] 🎯 NEW ROUTE: Node {currentStartNode} → Node {currentDestinationNode} (Distance: {routeDistance:F1}m)");

        RequestPathToDestination();
    }

    private void CheckDestinationReached()
    {
        if (currentDestinationNode == -1)
        {
            return;
        }

        float distanceToDestination = Vector3.Distance(transform.position, currentDestinationPosition);

        if (distanceToDestination <= destinationReachedDistance)
        {
            Debug.Log($"[{name}] ✅ Reached destination Node {currentDestinationNode}! Distance traveled: {totalDistanceTraveled:F1}m");
            PickNewDestination();
        }
    }

    private IEnumerator RetryPickDestination()
    {
        yield return new WaitForSeconds(Random.Range(3f, 6f));
        PickNewDestination();
    }

    #endregion

    #region Collision Detection & Avoidance

    private void DetectObstacles()
    {
        detectedObstacles.Clear();

        if (vehicleCollider == null)
        {
            return;
        }

        Vector3 origin = vehicleCollider.bounds.center + transform.forward * 2f;
        Vector3 forward = transform.forward;
        int density = Mathf.Max(1, raycastDensity);

        for (int i = 0; i < density; i++)
        {
            float angle = 0f;
            if (density > 1)
            {
                angle = Mathf.Lerp(-visionAngle / 2f, visionAngle / 2f, i / (float)(density - 1));
            }

            Vector3 direction = Quaternion.Euler(0, angle, 0) * forward;
            RaycastHit hit;

            if (Physics.Raycast(origin, direction, out hit, visionRange, obstacleLayer))
            {
                if (hit.collider == vehicleCollider || hit.collider.transform.IsChildOf(transform))
                {
                    continue;
                }

                bool isVehicle = hit.transform.GetComponent<NPCVehicleController>() != null ||
                                hit.transform.GetComponentInParent<NPCVehicleController>() != null;

                ObstacleInfo obstacle = new ObstacleInfo
                {
                    transform = hit.transform,
                    position = hit.point,
                    distance = hit.distance,
                    relativePosition = transform.InverseTransformPoint(hit.point),
                    isVehicle = isVehicle
                };

                detectedObstacles.Add(obstacle);

                if (debugVisualization)
                {
                    Debug.DrawRay(origin, direction * hit.distance, isVehicle ? Color.yellow : Color.red);
                }
            }
            else if (debugVisualization)
            {
                Debug.DrawRay(origin, direction * visionRange, Color.green);
            }
        }
    }

    private void CalculateLaneOffset()
    {
        laneOffset = Vector3.zero;

        if (detectedObstacles.Count == 0)
        {
            return;
        }

        ObstacleInfo closestObstacle = detectedObstacles.OrderBy(o => o.distance).First();

        if (closestObstacle.distance < safeDistance)
        {
            Vector3 toObstacle = closestObstacle.position - transform.position;
            toObstacle.y = 0f;

            Vector3 avoidDirection = Vector3.Cross(Vector3.up, toObstacle).normalized;

            Vector3 rightCheck = transform.position + transform.right * 3f;
            Vector3 leftCheck = transform.position - transform.right * 3f;

            int rightHits = Physics.OverlapSphere(rightCheck, 2f, obstacleLayer).Length;
            int leftHits = Physics.OverlapSphere(leftCheck, 2f, obstacleLayer).Length;

            if (rightHits < leftHits)
            {
                avoidDirection = transform.right;
            }
            else
            {
                avoidDirection = -transform.right;
            }

            float avoidStrength = 1f - Mathf.Clamp01(closestObstacle.distance / safeDistance);
            laneOffset = avoidDirection * avoidStrength * laneChangeSpeed;
        }

        laneOffset = Vector3.Lerp(laneOffset, Vector3.zero, Time.fixedDeltaTime * 0.5f);
    }

    #endregion

    #region Pathfinding

    private void RequestPathToDestination()
    {
        if (navSystem == null || navSystem.nodeMap == null || navSystem.nodeMap.Count < 2)
        {
            Debug.LogWarning($"[{name}] Cannot request path: navigation system not ready");
            StartCoroutine(RetryPathAfterDelay());
            return;
        }

        if (currentDestinationNode == -1)
        {
            PickNewDestination();
            return;
        }

        try
        {
            // Use stored start node or fallback to closest
            int startNode = currentStartNode;
            if (startNode == -1 || !navSystem.nodeMap.ContainsKey(startNode))
            {
                startNode = navSystem.GetClosestNode(transform.position);
                currentStartNode = startNode;
            }

            if (!navSystem.nodeMap.ContainsKey(startNode))
            {
                Debug.LogWarning($"[{name}] Invalid start node {startNode}");
                StartCoroutine(RetryPathAfterDelay());
                return;
            }

            if (!navSystem.nodeMap.ContainsKey(currentDestinationNode))
            {
                Debug.LogWarning($"[{name}] Invalid destination node {currentDestinationNode}");
                PickNewDestination();
                return;
            }

            Debug.Log($"[{name}] 🔍 Requesting path: {startNode} → {currentDestinationNode}");
            currentPath = navSystem.FindPath(startNode, currentDestinationNode);

            // ===== CRITICAL PATH VALIDATION =====
            if (currentPath == null || currentPath.Count == 0)
            {
                Debug.LogError($"[{name}] ❌ No path found from {startNode} to {currentDestinationNode}!");
                Debug.LogError($"[{name}] SOLUTION: Select CentralizedNavigationSystem → 'Auto Connect Nodes'");
                PickNewDestination();
                return;
            }

            if (currentPath.Count == 1)
            {
                Debug.LogWarning($"[{name}] ⚠️ PATH has only 1 node: {currentPath[0]}");
                PickNewDestination();
                return;
            }

            // Path accepted - follow the node path exactly
            GenerateSmoothedPath();
            currentWaypointIndex = 0;

            // ===== DETAILED PATH INFO =====
            string pathString = string.Join(" → ", currentPath);
            Debug.Log($"[{name}] ✅ PATH CREATED! ({currentPath.Count} nodes, {smoothedPath.Count} waypoints)");
            Debug.Log($"[{name}] Full route: {pathString}");

            float totalDist = 0f;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                if (navSystem.nodeMap.ContainsKey(currentPath[i]) &&
                    navSystem.nodeMap.ContainsKey(currentPath[i + 1]))
                {
                    Vector3 pos1 = navSystem.nodeMap[currentPath[i]].worldPosition;
                    Vector3 pos2 = navSystem.nodeMap[currentPath[i + 1]].worldPosition;
                    float segmentDist = Vector3.Distance(pos1, pos2);
                    totalDist += segmentDist;
                }
            }
            Debug.Log($"[{name}] Total route distance: {totalDist:F1}m");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[{name}] ❌ EXCEPTION: {e.Message}\n{e.StackTrace}");
            PickNewDestination();
        }
    }

    private IEnumerator RetryPathAfterDelay()
    {
        yield return new WaitForSeconds(Random.Range(4f, 7f));
        RequestPathToDestination();
    }

    private void GenerateSmoothedPath()
    {
        smoothedPath.Clear();
        List<Vector3> rawPath = new List<Vector3>();

        // Get world positions from node IDs in the path
        foreach (int nodeID in currentPath)
        {
            if (navSystem.nodeMap.ContainsKey(nodeID))
            {
                rawPath.Add(navSystem.nodeMap[nodeID].worldPosition);
            }
        }

        if (rawPath.Count < 2)
        {
            smoothedPath = rawPath;
            return;
        }

        // Catmull-Rom spline smoothing for natural curves
        List<Vector3> splinePoints = new List<Vector3>();

        for (int i = 0; i < rawPath.Count - 1; i++)
        {
            Vector3 p0 = i > 0 ? rawPath[i - 1] : rawPath[i];
            Vector3 p1 = rawPath[i];
            Vector3 p2 = rawPath[i + 1];
            Vector3 p3 = (i + 2 < rawPath.Count) ? rawPath[i + 2] : rawPath[i + 1];

            int segments = Mathf.Max(5, Mathf.CeilToInt(Vector3.Distance(p1, p2) / 3f));

            for (int j = 0; j < segments; j++)
            {
                float t = j / (float)segments;
                Vector3 point = CatmullRom(p0, p1, p2, p3, t);
                splinePoints.Add(point);
            }
        }

        splinePoints.Add(rawPath[rawPath.Count - 1]);
        smoothedPath = splinePoints;
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    #endregion

    #region Navigation

    private void UpdateTargetWaypoint()
    {
        if (smoothedPath.Count == 0)
        {
            RequestPathToDestination();
            return;
        }

        if (currentWaypointIndex >= smoothedPath.Count)
        {
            RequestPathToDestination();
            return;
        }

        Vector3 currentWaypoint = smoothedPath[currentWaypointIndex];
        float distanceToWaypoint = Vector3.Distance(transform.position, currentWaypoint);

        if (distanceToWaypoint < waypointReachDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= smoothedPath.Count)
            {
                RequestPathToDestination();
                return;
            }
        }

        float accumulatedDistance = 0f;
        targetPosition = currentWaypoint;
        futureTargetPosition = currentWaypoint;

        for (int i = currentWaypointIndex; i < smoothedPath.Count - 1; i++)
        {
            Vector3 segmentStart = smoothedPath[i];
            Vector3 segmentEnd = smoothedPath[i + 1];
            float segmentLength = Vector3.Distance(segmentStart, segmentEnd);

            if (accumulatedDistance + segmentLength >= pathLookahead)
            {
                float remaining = pathLookahead - accumulatedDistance;
                float t = Mathf.Clamp01(remaining / segmentLength);
                futureTargetPosition = Vector3.Lerp(segmentStart, segmentEnd, t);
                break;
            }

            accumulatedDistance += segmentLength;
            futureTargetPosition = segmentEnd;
        }

        targetPosition.y = transform.position.y;
        futureTargetPosition.y = transform.position.y;
    }

    #endregion

    #region Steering & Speed

    private void CalculateDesiredSteering()
    {
        Vector3 directionToTarget = (futureTargetPosition - transform.position).normalized;
        directionToTarget.y = 0f;
        directionToTarget += laneOffset;
        directionToTarget = directionToTarget.normalized;

        if (directionToTarget.magnitude > 0.1f)
        {
            float targetAngle = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
            float currentAngle = transform.eulerAngles.y;
            float angleDiff = Mathf.DeltaAngle(currentAngle, targetAngle);

            desiredSteerAngle = Mathf.Clamp(angleDiff, -maxSteerAngle, maxSteerAngle);
        }
        else
        {
            desiredSteerAngle = 0f;
        }
    }

    private void CalculateSpeed()
    {
        targetSpeed = maxSpeed;

        // Slow down for turns
        float turnSharpness = Mathf.Abs(desiredSteerAngle) / maxSteerAngle;
        if (turnSharpness > 0.3f)
        {
            targetSpeed *= Mathf.Lerp(1f, cornerSlowdownFactor, (turnSharpness - 0.3f) / 0.7f);
        }

        // Obstacle avoidance
        if (detectedObstacles.Count > 0)
        {
            float closestDistance = detectedObstacles.Min(o => o.distance);
            ObstacleInfo closestObstacle = detectedObstacles.OrderBy(o => o.distance).First();

            if (closestObstacle.distance < emergencyDistance)
            {
                targetSpeed = 0f;
            }
            else if (closestObstacle.distance < safeDistance)
            {
                if (closestObstacle.isVehicle)
                {
                    NPCVehicleController vehicleAhead = closestObstacle.transform.GetComponent<NPCVehicleController>() ??
                                                       closestObstacle.transform.GetComponentInParent<NPCVehicleController>();
                    if (vehicleAhead != null)
                    {
                        targetSpeed = Mathf.Min(vehicleAhead.currentSpeed * 0.9f, targetSpeed);
                    }
                }
                else
                {
                    float ratio = Mathf.Clamp01(closestObstacle.distance / safeDistance);
                    targetSpeed *= ratio * 0.6f;
                }
            }
        }

        // Traffic light compliance
        if (currentTrafficLight != null)
        {
            float distanceToLight = Vector3.Distance(transform.position, currentTrafficLight.transform.position);

            if (currentTrafficLight.IsRed())
            {
                if (distanceToLight < stopLineDistance)
                {
                    targetSpeed = 0f;
                }
                else if (distanceToLight < trafficLightReactionDistance)
                {
                    float stopRatio = Mathf.Clamp01((distanceToLight - stopLineDistance) / (trafficLightReactionDistance - stopLineDistance));
                    targetSpeed = Mathf.Min(targetSpeed, maxSpeed * stopRatio * 0.5f);
                }
            }
            else if (currentTrafficLight.IsYellow())
            {
                if (distanceToLight < stopLineDistance * 2f)
                {
                    targetSpeed = Mathf.Min(targetSpeed, maxSpeed * 0.5f);
                }
            }
        }

        if (isStuck)
        {
            targetSpeed = maxSpeed * 0.4f;
        }

        targetSpeed = Mathf.Clamp(targetSpeed, 0f, maxSpeed);
    }

    private void ApplyPhysics()
    {
        if (rb == null)
        {
            return;
        }

        // Smooth steering transition
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, desiredSteerAngle, Time.fixedDeltaTime * turnSmoothness);

        // Apply steering with speed-based sensitivity
        float speedFactor = Mathf.Clamp01(currentSpeed / maxSpeed);
        float steeringForce = currentSteerAngle * 0.05f * speedFactor;
        rb.angularVelocity = new Vector3(0, steeringForce, 0);

        // Smooth speed changes
        float speedDiff = targetSpeed - currentSpeed;
        float force = speedDiff > 0 ? acceleration : brakeForce;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, force * Time.fixedDeltaTime);
        currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);

        // Apply velocity
        Vector3 targetVelocity = transform.forward * currentSpeed;
        targetVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, Time.fixedDeltaTime * 4f);
    }

    #endregion

    #region Stuck Detection

    private void CheckStuckState()
    {
        stuckTimer += Time.fixedDeltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            float distanceMoved = Vector3.Distance(transform.position, lastPosition);

            if (distanceMoved < minMovementThreshold && targetSpeed > 1f)
            {
                if (!isStuck)
                {
                    isStuck = true;
                    Debug.LogWarning($"[{name}] ⚠️ Vehicle stuck! Requesting new path...");
                    PickNewDestination();
                }
            }
            else
            {
                isStuck = false;
            }

            stuckTimer = 0f;
        }
    }

    #endregion

    #region Animation

    private void AnimateWheels()
    {
        if (wheels == null || wheels.Length == 0)
        {
            return;
        }

        float rotationAmount = currentSpeed * wheelRotationMultiplier * Time.deltaTime;

        foreach (Transform wheel in wheels)
        {
            if (wheel != null)
            {
                wheel.Rotate(rotationAmount, 0, 0, Space.Self);
            }
        }
    }

    #endregion

    #region Traffic Light

    private void OnTriggerEnter(Collider other)
    {
        TrafficLightController light = other.GetComponent<TrafficLightController>();
        if (light != null)
        {
            currentTrafficLight = light;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        TrafficLightController light = other.GetComponent<TrafficLightController>();
        if (light == currentTrafficLight)
        {
            currentTrafficLight = null;
        }
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!debugVisualization || !Application.isPlaying)
        {
            return;
        }

        // Draw raw node path (RED)
        if (currentPath != null && currentPath.Count > 1 && navSystem != null)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                if (navSystem.nodeMap.ContainsKey(currentPath[i]) &&
                    navSystem.nodeMap.ContainsKey(currentPath[i + 1]))
                {
                    Vector3 start = navSystem.nodeMap[currentPath[i]].worldPosition;
                    Vector3 end = navSystem.nodeMap[currentPath[i + 1]].worldPosition;
                    Gizmos.DrawLine(start + Vector3.up, end + Vector3.up);
                }
            }
        }

        // Draw smoothed path (CYAN)
        if (smoothedPath != null && smoothedPath.Count > 1)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < smoothedPath.Count - 1; i++)
            {
                Gizmos.DrawLine(smoothedPath[i] + Vector3.up * 0.5f, smoothedPath[i + 1] + Vector3.up * 0.5f);
            }
        }

        // Draw current target (YELLOW)
        if (smoothedPath.Count > 0 && currentWaypointIndex < smoothedPath.Count)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetPosition + Vector3.up, 1f);
        }

        // Draw future target (MAGENTA)
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(futureTargetPosition + Vector3.up, 0.7f);
        Gizmos.DrawLine(transform.position, futureTargetPosition);

        // Draw destination (GREEN)
        if (showDestination && currentDestinationNode != -1 && navSystem != null && navSystem.nodeMap.ContainsKey(currentDestinationNode))
        {
            Gizmos.color = Color.green;
            Vector3 dest = currentDestinationPosition + Vector3.up * 3f;
            Gizmos.DrawWireCube(dest, Vector3.one * 2f);
            Gizmos.DrawLine(transform.position, dest);
        }
    }

    #endregion
}