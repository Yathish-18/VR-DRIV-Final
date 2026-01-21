// TRAFFIC VEHICLE - DESTINATION-BASED NAVIGATION
// Saves complete route and navigates between random destinations

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

        // Randomize speed for variety
        maxSpeed *= Random.Range(0.85f, 1.15f);
        acceleration = maxSpeed * 1.5f;
        turnSpeed = maxSpeed * 0.3f;

        // Snap to ground on spawn
        Vector3 spawnPos = transform.position;
        spawnPos = SnapToGround(spawnPos);
        transform.position = spawnPos;
        rb.position = spawnPos;
        lastValidPosition = spawnPos;

        // Set starting node
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

        // Pick first destination and calculate & SAVE path
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

        UpdateDebugInfo();

        // Check for obstacles
        bool hasObstacle = DetectObstacle();
        debugIsObstacleDetected = hasObstacle;

        float distanceToTarget = targetWaypoint != null ? Vector3.Distance(transform.position, targetWaypoint.position) : 0f;

        // Reached current waypoint?
        if (distanceToTarget < waypointReachDistance)
        {
            AdvanceAlongSavedRoute();
        }

        // Set speed based on obstacles
        targetSpeed = hasObstacle ? 0f : maxSpeed;
        isStopped = hasObstacle;

        // Smooth speed transition
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedSmoothVelocity, speedSmoothTime);

        // Move the vehicle
        MoveVehicle();

        // Stuck detection
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

    /// <summary>
    /// CORE METHOD: Picks destination and saves complete route in vehicle
    /// </summary>
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
            // Pick random destination within distance range
            int randomDestination = PickDestinationWithinRange(allNodes);
            
            if (randomDestination == -1)
            {
                Debug.LogWarning($"[{gameObject.name}] No suitable destination in range, using random node");
                randomDestination = navSystem.GetRandomNode(new HashSet<int> { sourceNodeID });
            }

            // Calculate path using A* pathfinding
            List<int> calculatedPath = navSystem.FindPath(sourceNodeID, randomDestination);

            // Validate path quality
            if (calculatedPath == null || calculatedPath.Count == 0)
            {
                Debug.LogWarning($"[{gameObject.name}] No path exists: {sourceNodeID} → {randomDestination}, attempt {attempt + 1}/{maxPathAttempts}");
                continue;
            }

            if (calculatedPath.Count < minPathLength)
            {
                Debug.LogWarning($"[{gameObject.name}] Path too short ({calculatedPath.Count} < {minPathLength} nodes), attempt {attempt + 1}/{maxPathAttempts}");
                continue;
            }

            if (calculatedPath.Count > maxPathLength)
            {
                Debug.LogWarning($"[{gameObject.name}] Path too long ({calculatedPath.Count} > {maxPathLength} nodes), attempt {attempt + 1}/{maxPathAttempts}");
                continue;
            }

            // ✅ PATH IS VALID - SAVE IT TO VEHICLE
            SaveRouteToVehicle(calculatedPath, randomDestination);
            
            return; // Success!
        }

        // Failed to find good path after all attempts
        Debug.LogError($"[{gameObject.name}] ❌ Failed to find valid path after {maxPathAttempts} attempts! Using fallback.");
        FallbackPath();
    }

    /// <summary>
    /// Saves the calculated route to the vehicle's memory
    /// </summary>
    private void SaveRouteToVehicle(List<int> path, int destination)
    {
        // SAVE THE COMPLETE ROUTE
        savedRoutePath = new List<int>(path); // Create copy of path
        destinationNodeID = destination;
        currentPathIndex = 0;
        pathRecalculations = 0;
        
        // Set first waypoint
        SetTargetWaypoint(savedRoutePath[currentPathIndex]);
        
        // Update debug info
        debugRouteName = $"Route_{sourceNodeID}_to_{destinationNodeID}";
        debugTotalNodes = savedRoutePath.Count;
        debugSavedRoute = string.Join(" → ", savedRoutePath);
        
        // Generate preview of next few nodes
        int previewCount = Mathf.Min(5, savedRoutePath.Count);
        debugNextNodes = string.Join(" → ", savedRoutePath.Take(previewCount));
        if (savedRoutePath.Count > previewCount)
            debugNextNodes += "...";

        Debug.Log($"[{gameObject.name}] ========== NEW ROUTE SAVED ==========");
        Debug.Log($"[{gameObject.name}] Source: Node {sourceNodeID}");
        Debug.Log($"[{gameObject.name}] Destination: Node {destinationNodeID}");
        Debug.Log($"[{gameObject.name}] Route Length: {savedRoutePath.Count} nodes");
        Debug.Log($"[{gameObject.name}] Complete Path: {debugSavedRoute}");
        Debug.Log($"[{gameObject.name}] =====================================");
    }

    /// <summary>
    /// Pick destination node within specified distance range
    /// </summary>
    private int PickDestinationWithinRange(List<int> allNodes)
    {
        if (!navSystem.nodeMap.ContainsKey(sourceNodeID))
            return -1;

        Vector3 sourcePos = navSystem.nodeMap[sourceNodeID].worldPosition;
        
        List<int> validDestinations = new List<int>();
        
        foreach (int nodeID in allNodes)
        {
            if (nodeID == sourceNodeID) continue;
            if (!navSystem.nodeMap.ContainsKey(nodeID)) continue;
            
            Vector3 nodePos = navSystem.nodeMap[nodeID].worldPosition;
            float distance = Vector3.Distance(sourcePos, nodePos);
            
            if (distance >= minDestinationDistance && distance <= maxDestinationDistance)
            {
                validDestinations.Add(nodeID);
            }
        }

        if (validDestinations.Count == 0)
            return -1;

        return validDestinations[Random.Range(0, validDestinations.Count)];
    }

    /// <summary>
    /// Advance to next waypoint along saved route
    /// </summary>
    private void AdvanceAlongSavedRoute()
    {
        currentPathIndex++;

        // Reached final destination?
        if (currentPathIndex >= savedRoutePath.Count)
        {
            Debug.Log($"[{gameObject.name}] ✅ DESTINATION REACHED: Node {destinationNodeID}");
            Debug.Log($"[{gameObject.name}] Completed route: {debugSavedRoute}");
            
            // Make destination the new source
            sourceNodeID = destinationNodeID;
            currentNodeID = sourceNodeID;
            
            // Pick new destination and save new route
            PickNewDestinationAndSaveRoute();
            return;
        }

        // Move to next waypoint in saved route
        int nextNodeID = savedRoutePath[currentPathIndex];
        currentNodeID = nextNodeID;
        SetTargetWaypoint(currentNodeID);
        
        float progressPercent = ((float)currentPathIndex / savedRoutePath.Count) * 100f;
        Debug.Log($"[{gameObject.name}] Route Progress: {currentPathIndex}/{savedRoutePath.Count} ({progressPercent:F0}%) → Node {currentNodeID}");
    }

    /// <summary>
    /// Set target waypoint transform
    /// </summary>
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

    /// <summary>
    /// Recover from being stuck by recalculating path
    /// </summary>
    private void RecoverFromStuck()
    {
        pathRecalculations++;
        
        if (pathRecalculations >= MAX_RECALCULATIONS)
        {
            Debug.LogError($"[{gameObject.name}] Too many recalculations ({pathRecalculations})! Picking new destination.");
            sourceNodeID = currentNodeID;
            PickNewDestinationAndSaveRoute();
            return;
        }

        Debug.LogWarning($"[{gameObject.name}] Recalculating path from current Node {currentNodeID} to destination {destinationNodeID}...");
        
        // Recalculate path from current position to same destination
        List<int> newPath = navSystem.FindPath(currentNodeID, destinationNodeID);
        
        if (newPath != null && newPath.Count > 0)
        {
            // SAVE NEW RECALCULATED ROUTE
            SaveRouteToVehicle(newPath, destinationNodeID);
            stuckCounter = 0;
            
            Debug.Log($"[{gameObject.name}] ✅ Path successfully recalculated!");
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] ❌ Recalculation failed! Picking entirely new destination.");
            sourceNodeID = currentNodeID;
            PickNewDestinationAndSaveRoute();
        }
    }

    /// <summary>
    /// Emergency fallback when pathfinding fails
    /// </summary>
    private void FallbackPath()
    {
        // Try to go to nearest node
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

        // Ultimate fallback: teleport to random node and start fresh
        Debug.LogError($"[{gameObject.name}] EMERGENCY: Teleporting to random node!");
        int randomNode = navSystem.GetRandomNode();
        
        if (navSystem.nodeMap.ContainsKey(randomNode))
        {
            Vector3 newPos = navSystem.nodeMap[randomNode].worldPosition;
            newPos = SnapToGround(newPos);
            transform.position = newPos;
            rb.position = newPos;
            
            sourceNodeID = randomNode;
            currentNodeID = randomNode;
            SetTargetWaypoint(randomNode);
            PickNewDestinationAndSaveRoute();
        }
    }

    /// <summary>
    /// Move vehicle toward target waypoint
    /// </summary>
    private void MoveVehicle()
    {
        if (targetWaypoint == null) return;

        Vector3 targetPosition = targetWaypoint.position;
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; // Keep movement horizontal

        // Rotate toward target
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed);
        }

        // Move forward
        Vector3 forwardMovement = transform.forward * currentSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + forwardMovement);

        // Lock rotation on X and Z axes
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

    /// <summary>
    /// Detect obstacles in front of vehicle
    /// </summary>
    private bool DetectObstacle()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f;
        Vector3 rayDirection = transform.forward;

        // Primary raycast for traffic vehicles
        if (Physics.Raycast(rayStart, rayDirection, out RaycastHit hit, detectionRange, obstacleLayer))
        {
            TrafficVehicle otherVehicle = hit.collider.GetComponent<TrafficVehicle>();
            if (otherVehicle != null && hit.distance < stoppingDistance)
            {
                if (showDebugGizmos)
                    Debug.DrawLine(rayStart, hit.point, Color.red);
                return true;
            }

            // Static obstacles
            if (hit.collider.gameObject.layer != gameObject.layer && hit.distance < stoppingDistance * 0.5f)
            {
                if (showDebugGizmos)
                    Debug.DrawLine(rayStart, hit.point, Color.yellow);
                return true;
            }
        }

        // Spherecast for wider detection
        if (Physics.SphereCast(rayStart, 1f, rayDirection, out hit, detectionRange, obstacleLayer))
        {
            TrafficVehicle otherVehicle = hit.collider.GetComponent<TrafficVehicle>();
            if (otherVehicle != null && hit.distance < stoppingDistance)
                return true;
        }

        if (showDebugGizmos)
            Debug.DrawRay(rayStart, rayDirection * detectionRange, Color.green);

        return false;
    }

    /// <summary>
    /// Snap position to ground surface
    /// </summary>
    private Vector3 SnapToGround(Vector3 position, float rayDistance = 50f)
    {
        // Raycast from above
        Vector3 rayStart = new Vector3(position.x, position.y + 20f, position.z);
        
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayDistance + 20f))
            return hit.point + Vector3.up * 0.5f;
        
        // Raycast from position
        if (Physics.Raycast(position, Vector3.down, out hit, rayDistance))
            return hit.point + Vector3.up * 0.5f;
        
        return position;
    }

    /// <summary>
    /// Update all debug information
    /// </summary>
    private void UpdateDebugInfo()
    {
        debugPathProgress = currentPathIndex;
        debugCurrentSpeed = currentSpeed;
        debugProgressPercent = savedRoutePath.Count > 0 ? ((float)currentPathIndex / savedRoutePath.Count) * 100f : 0f;
        debugDistanceToWaypoint = targetWaypoint != null ? Vector3.Distance(transform.position, targetWaypoint.position) : 0f;
        
        if (navSystem != null && navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            debugDistanceToDestination = Vector3.Distance(
                transform.position, 
                navSystem.nodeMap[destinationNodeID].worldPosition
            );
        }
        
        // Update next nodes preview
        if (savedRoutePath != null && savedRoutePath.Count > 0)
        {
            int remainingNodes = savedRoutePath.Count - currentPathIndex;
            int previewCount = Mathf.Min(3, remainingNodes);
            
            if (currentPathIndex < savedRoutePath.Count)
            {
                debugNextNodes = string.Join(" → ", savedRoutePath.Skip(currentPathIndex).Take(previewCount));
                if (remainingNodes > previewCount)
                    debugNextNodes += $" ... (+{remainingNodes - previewCount} more)";
            }
        }
    }

    // ========================================
    // GIZMOS - VISUALIZE SAVED ROUTE
    // ========================================

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos || !Application.isPlaying || navSystem == null) return;

        // Draw COMPLETE saved route
        if (savedRoutePath != null && savedRoutePath.Count > 1)
        {
            for (int i = 0; i < savedRoutePath.Count - 1; i++)
            {
                if (!navSystem.nodeMap.ContainsKey(savedRoutePath[i]) || !navSystem.nodeMap.ContainsKey(savedRoutePath[i + 1]))
                    continue;

                Vector3 start = navSystem.nodeMap[savedRoutePath[i]].worldPosition + Vector3.up * 1.5f;
                Vector3 end = navSystem.nodeMap[savedRoutePath[i + 1]].worldPosition + Vector3.up * 1.5f;
                
                // Color code: gray for completed, vehicle color for remaining
                Color pathColor = i < currentPathIndex 
                    ? new Color(0.5f, 0.5f, 0.5f, 0.5f)  // Completed sections
                    : debugColor;                         // Remaining route
                
                Gizmos.color = pathColor;
                Gizmos.DrawLine(start, end);
                
                // Draw small spheres at waypoints
                if (i >= currentPathIndex)
                {
                    Gizmos.DrawWireSphere(start, 0.5f);
                }
            }
        }

        // Current target waypoint
        if (targetWaypoint != null)
        {
            Gizmos.color = isStopped ? Color.red : (debugIsStuck ? Color.magenta : debugColor);
            Gizmos.DrawLine(transform.position + Vector3.up, targetWaypoint.position + Vector3.up);
            
            // Waypoint reach radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetWaypoint.position, waypointReachDistance);
        }

        // Destination marker (large magenta sphere)
        if (navSystem.nodeMap.ContainsKey(destinationNodeID))
        {
            Vector3 destPos = navSystem.nodeMap[destinationNodeID].worldPosition;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(destPos + Vector3.up * 3f, 3f);
            Gizmos.DrawLine(destPos, destPos + Vector3.up * 6f);
        }

        // Source marker (green sphere)
        if (navSystem.nodeMap.ContainsKey(sourceNodeID))
        {
            Vector3 srcPos = navSystem.nodeMap[sourceNodeID].worldPosition;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(srcPos + Vector3.up * 3f, 2f);
        }

        // Stuck indicator
        if (debugIsStuck)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 4f, 2f);
        }

        // Forward direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 5f);
    }

    private void OnDrawGizmosSelected()
    {
        #if UNITY_EDITOR
        if (!Application.isPlaying || targetWaypoint == null) return;

        string status = debugIsStuck ? "🚫 STUCK" : (isStopped ? "⛔ STOPPED" : "✅ MOVING");
        
        Handles.Label(
            transform.position + Vector3.up * 6f,
            $"{gameObject.name} {status}\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            $"Route: {sourceNodeID} → {destinationNodeID}\n" +
            $"Progress: {currentPathIndex}/{savedRoutePath.Count} ({debugProgressPercent:F0}%)\n" +
            $"Current Node: {currentNodeID}\n" +
            $"Next: {debugNextNodes}\n" +
            $"Distance to Dest: {debugDistanceToDestination:F1}m\n" +
            $"Speed: {debugCurrentSpeed:F1} m/s ({debugCurrentSpeed * 3.6f:F0} km/h)\n" +
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