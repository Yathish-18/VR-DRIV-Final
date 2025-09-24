using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class CentralizedNavigationSystem : MonoBehaviour
{
    [Header("Graph Data")]
    public List<NavNode> nodes = new List<NavNode>();
    public List<GraphConnection> connections = new List<GraphConnection>();

    [Header("Node Management")]
    public Transform nodesParent; // Container for all nodes
    public string nodeNamePrefix = "NavNode_";
    [SerializeField] private bool autoCollectNodes = true; // Auto-collect nodes on start

    [Header("Pathfinding Settings")]
    public float maxConnectionDistance = 10f;
    public LayerMask obstacleLayer = 1;
    public bool useLineOfSightCheck = true;

    [Header("Visualization")]
    public bool showConnections = true;
    public bool showPathInEditor = true;
    public Color connectionColor = Color.green;
    public Color pathColor = Color.red;
    public float pathWidth = 0.5f;
    public Material pathMaterial;

    [Header("ROAD DETECTION-BASED PATH VISUALIZATION")]
    [Tooltip("Layer mask for road surfaces that the path should stick to")]
    public LayerMask roadLayerMask = 1; // Set this to your road layers
    [Tooltip("Maximum distance to raycast downward to find road")]
    public float roadRaycastDistance = 50f;
    [Tooltip("How high above nodes to start raycast (helps with elevated nodes)")]
    public float roadRaycastUpOffset = 20f;
    [Tooltip("Height offset above road surface for path visualization")]
    public float pathHeightOffset = 0.5f;

    [Header("GAME-STYLE PATH VISUALIZATION")]
    [Tooltip("Use LineRenderer for game-style path with arrows")]
    public bool useLineRendererPath = true;
    [Tooltip("Show directional arrows along the path")]
    public bool showDirectionalArrows = true;
    [Tooltip("Distance between arrow indicators")]
    public float arrowSpacing = 3f;
    [Tooltip("Size of directional arrows")]
    public float arrowSize = 1f;
    [Tooltip("Arrow prefab (optional - will create default if not set)")]
    public GameObject arrowPrefab;

    [Header("Runtime Path Rendering")]
    public LineRenderer pathLineRenderer;

    // Arrow system
    private List<GameObject> pathArrows = new List<GameObject>();
    private GameObject arrowsParent;

    // Internal data structures for fast pathfinding
    private Dictionary<int, List<int>> adjacencyList = new Dictionary<int, List<int>>();
    private Dictionary<int, Dictionary<int, float>> edgeWeights = new Dictionary<int, Dictionary<int, float>>();
    public Dictionary<int, NavNode> nodeMap = new Dictionary<int, NavNode>();
    private List<int> currentPathIDs = new List<int>();

    // Pathfinding data
    private Dictionary<int, float> gCosts = new Dictionary<int, float>();
    private Dictionary<int, float> hCosts = new Dictionary<int, float>();
    private Dictionary<int, int> parents = new Dictionary<int, int>();

    void Awake()
    {
        SetupNodeHierarchy();
        SetupArrowSystem();
    }

    void Start()
    {
        // Force collect all nodes in scene
        if (autoCollectNodes)
        {
            CollectAllNodes();
        }

        RefreshGraph();

        // Setup LineRenderer for game-style visualization
        if (useLineRendererPath)
        {
            SetupLineRenderer();
            ForceLineRendererSetup();
        }
    }

    void SetupNodeHierarchy()
    {
        // Create nodes parent if not specified
        if (nodesParent == null)
        {
            GameObject nodesContainer = new GameObject("Nodes");
            nodesContainer.transform.SetParent(transform);
            nodesParent = nodesContainer.transform;
        }
    }

    void SetupArrowSystem()
    {
        if (arrowsParent == null)
        {
            arrowsParent = new GameObject("PathArrows");
            arrowsParent.transform.SetParent(transform);
        }
    }

    public void CollectAllNodes()
    {
        // Find ALL NavNode objects in the scene
        NavNode[] allNodes = Object.FindObjectsByType<NavNode>(FindObjectsSortMode.None);
        foreach (NavNode node in allNodes)
        {
            RegisterNode(node);
        }

        Debug.Log($"Collected {allNodes.Length} nodes");
    }

    public void RegisterNode(NavNode node)
    {
        if (node == null) return;

        // Set parent reference
        node.parentNavSystem = this;

        // Add to nodes list if not already there
        if (!nodes.Contains(node))
        {
            nodes.Add(node);
        }

        // Auto-assign ID if not set
        if (node.nodeID < 0)
        {
            node.nodeID = GetNextAvailableID();
        }

        // Add to node map
        nodeMap[node.nodeID] = node;
    }

    public NavNode CreateNode(Vector3 position, int? nodeID = null)
    {
        // Ensure nodes parent exists
        if (nodesParent == null)
        {
            SetupNodeHierarchy();
        }

        // Raycast down to find road surface position
        Vector3 roadPos = RoadDetectionHelper.GetRoadSurfacePosition(position, roadLayerMask, roadRaycastDistance, roadRaycastUpOffset);

        // Create new node GameObject
        int id = nodeID.HasValue ? nodeID.Value : GetNextAvailableID();
        GameObject nodeObj = new GameObject($"{nodeNamePrefix}{id}");

        // Set position to road-detected position
        nodeObj.transform.position = roadPos;
        nodeObj.transform.SetParent(nodesParent);

        // Add NavNode component
        NavNode createdNode = nodeObj.AddComponent<NavNode>();
        createdNode.nodeID = id;

        // Register the node
        RegisterNode(createdNode);

        return createdNode;
    }

    public void UnregisterNode(NavNode node)
    {
        if (node == null) return;

        // Remove from lists
        nodes.Remove(node);
        if (nodeMap.ContainsKey(node.nodeID))
        {
            nodeMap.Remove(node.nodeID);
        }

        // Remove connections involving this node
        connections.RemoveAll(c => c.fromNodeID == node.nodeID || c.toNodeID == node.nodeID);

        // Refresh graph
        RefreshGraph();
    }

    int GetNextAvailableID()
    {
        if (nodes.Count == 0) return 0;
        int maxID = nodes.Where(n => n != null).Select(n => n.nodeID).DefaultIfEmpty(-1).Max();
        return maxID + 1;
    }

    [ContextMenu("Refresh Graph")]
    public void RefreshGraph()
    {
        BuildNodeMap();
        BuildAdjacencyList();
        Debug.Log($"Graph refreshed: {nodes.Count} nodes, {connections.Count} connections");
    }

    void BuildNodeMap()
    {
        nodeMap.Clear();
        // Remove null nodes
        nodes.RemoveAll(n => n == null);

        // Build node map
        foreach (var node in nodes)
        {
            if (node != null)
            {
                nodeMap[node.nodeID] = node;
            }
        }
    }

    void BuildAdjacencyList()
    {
        adjacencyList.Clear();
        edgeWeights.Clear();

        // Initialize adjacency lists for all nodes
        foreach (var kvp in nodeMap)
        {
            adjacencyList[kvp.Key] = new List<int>();
            edgeWeights[kvp.Key] = new Dictionary<int, float>();
        }

        // Build connections from the connections list
        foreach (var connection in connections)
        {
            if (nodeMap.ContainsKey(connection.fromNodeID) && nodeMap.ContainsKey(connection.toNodeID))
            {
                // Add forward connection
                if (!adjacencyList[connection.fromNodeID].Contains(connection.toNodeID))
                {
                    adjacencyList[connection.fromNodeID].Add(connection.toNodeID);
                    edgeWeights[connection.fromNodeID][connection.toNodeID] = connection.weight;
                }

                // Add reverse connection if bidirectional
                if (connection.bidirectional && !adjacencyList[connection.toNodeID].Contains(connection.fromNodeID))
                {
                    adjacencyList[connection.toNodeID].Add(connection.fromNodeID);
                    edgeWeights[connection.toNodeID][connection.fromNodeID] = connection.weight;
                }
            }
        }
    }

    public void AutoConnectNodes()
    {
        connections.Clear();

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (nodes[i] != null && nodes[j] != null)
                {
                    float distance = Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position);
                    if (distance <= maxConnectionDistance)
                    {
                        if (!useLineOfSightCheck || HasClearLineOfSight(nodes[i].transform.position, nodes[j].transform.position))
                        {
                            connections.Add(new GraphConnection(nodes[i].nodeID, nodes[j].nodeID, distance, true));
                        }
                    }
                }
            }
        }

        RefreshGraph();
        Debug.Log($"Auto-connected nodes: {connections.Count} connections created");
    }

    bool HasClearLineOfSight(Vector3 start, Vector3 end)
    {
        Vector3 direction = (end - start);
        return !Physics.Raycast(start, direction.normalized, direction.magnitude, obstacleLayer);
    }

    public List<int> FindPath(int startNodeID, int endNodeID)
    {
        if (!nodeMap.ContainsKey(startNodeID) || !nodeMap.ContainsKey(endNodeID))
        {
            Debug.LogWarning($"Invalid node IDs: start={startNodeID}, end={endNodeID}");
            return new List<int>();
        }

        // Reset pathfinding data
        gCosts.Clear();
        hCosts.Clear();
        parents.Clear();

        List<int> openSet = new List<int>();
        HashSet<int> closedSet = new HashSet<int>();

        // Initialize all nodes
        foreach (var nodeID in nodeMap.Keys)
        {
            gCosts[nodeID] = float.MaxValue;
            hCosts[nodeID] = 0f;
            parents[nodeID] = -1;
        }

        gCosts[startNodeID] = 0f;
        hCosts[startNodeID] = GetHeuristic(startNodeID, endNodeID);
        openSet.Add(startNodeID);

        while (openSet.Count > 0)
        {
            // Find node with lowest fCost
            int currentNodeID = openSet[0];
            float lowestFCost = gCosts[currentNodeID] + hCosts[currentNodeID];

            for (int i = 1; i < openSet.Count; i++)
            {
                float fCost = gCosts[openSet[i]] + hCosts[openSet[i]];
                if (fCost < lowestFCost || (fCost == lowestFCost && hCosts[openSet[i]] < hCosts[currentNodeID]))
                {
                    currentNodeID = openSet[i];
                    lowestFCost = fCost;
                }
            }

            openSet.Remove(currentNodeID);
            closedSet.Add(currentNodeID);

            // Path found
            if (currentNodeID == endNodeID)
            {
                return RetracePath(startNodeID, endNodeID);
            }

            // Check neighbors
            if (adjacencyList.ContainsKey(currentNodeID))
            {
                foreach (int neighborID in adjacencyList[currentNodeID])
                {
                    if (closedSet.Contains(neighborID)) continue;

                    float newCostToNeighbor = gCosts[currentNodeID] + edgeWeights[currentNodeID][neighborID];

                    if (newCostToNeighbor < gCosts[neighborID] || !openSet.Contains(neighborID))
                    {
                        gCosts[neighborID] = newCostToNeighbor;
                        hCosts[neighborID] = GetHeuristic(neighborID, endNodeID);
                        parents[neighborID] = currentNodeID;

                        if (!openSet.Contains(neighborID))
                            openSet.Add(neighborID);
                    }
                }
            }
        }

        // No path found
        return new List<int>();
    }

    public List<int> FindPath(Vector3 startPos, Vector3 endPos)
    {
        int startNodeID = GetClosestNodeID(startPos);
        int endNodeID = GetClosestNodeID(endPos);

        if (startNodeID < 0 || endNodeID < 0)
        {
            Debug.LogWarning("Could not find start or end node for pathfinding");
            return new List<int>();
        }

        Debug.Log($"Pathfinding: Start pos {startPos} -> Node {startNodeID}, End pos {endPos} -> Node {endNodeID}");
        return FindPath(startNodeID, endNodeID);
    }

    private List<int> RetracePath(int startNodeID, int endNodeID)
    {
        List<int> path = new List<int>();
        int currentNodeID = endNodeID;

        while (currentNodeID != startNodeID && currentNodeID != -1)
        {
            path.Add(currentNodeID);
            currentNodeID = parents.ContainsKey(currentNodeID) ? parents[currentNodeID] : -1;
        }

        if (currentNodeID == startNodeID)
        {
            path.Add(startNodeID);
            path.Reverse();
        }

        return path;
    }

    private float GetHeuristic(int nodeID1, int nodeID2)
    {
        if (!nodeMap.ContainsKey(nodeID1) || !nodeMap.ContainsKey(nodeID2))
            return 0f;

        return Vector3.Distance(nodeMap[nodeID1].transform.position, nodeMap[nodeID2].transform.position);
    }

    public int GetClosestNodeID(Vector3 position)
    {
        if (nodeMap.Count == 0) return -1;

        int closestID = -1;
        float closestDistance = float.MaxValue;

        foreach (var kvp in nodeMap)
        {
            if (kvp.Value != null)
            {
                float distance = Vector3.Distance(position, kvp.Value.transform.position);
                if (distance < closestDistance)
                {
                    closestID = kvp.Key;
                    closestDistance = distance;
                }
            }
        }

        return closestID;
    }

    /// <summary>
    /// Gets road-detected position for path visualization using advanced raycast
    /// This ensures the path always follows the road surface perfectly
    /// </summary>
    private Vector3 GetRoadPathPosition(Vector3 nodePosition)
    {
        return RoadDetectionHelper.GetRoadSurfacePositionWithOffset(
            nodePosition,
            roadLayerMask,
            pathHeightOffset,
            roadRaycastDistance,
            roadRaycastUpOffset
        );
    }

    public void VisualizePath(List<int> pathIDs)
    {
        currentPathIDs = new List<int>(pathIDs);

        if (useLineRendererPath)
        {
            VisualizeGameStylePath(pathIDs);
        }
        else
        {
            Debug.LogWarning("LineRenderer path visualization is disabled. Enable 'Use Line Renderer Path' to see the path.");
        }
    }

    /// <summary>
    /// GAME-STYLE PATH VISUALIZATION WITH ARROWS
    /// Creates LineRenderer path with directional arrows like in games
    /// </summary>
    private void VisualizeGameStylePath(List<int> pathIDs)
    {
        // Clear existing arrows
        ClearPathArrows();

        // Ensure LineRenderer is set up
        if (pathLineRenderer == null)
        {
            SetupLineRenderer();
            ForceLineRendererSetup();
        }

        if (pathLineRenderer == null || pathIDs.Count < 2)
        {
            if (pathLineRenderer != null)
                pathLineRenderer.positionCount = 0;
            Debug.LogWarning("Cannot visualize path: LineRenderer is null or path too short");
            return;
        }

        // Create positions using advanced road raycast detection
        List<Vector3> positions = new List<Vector3>();
        for (int i = 0; i < pathIDs.Count; i++)
        {
            if (nodeMap.ContainsKey(pathIDs[i]) && nodeMap[pathIDs[i]] != null)
            {
                Vector3 nodePos = nodeMap[pathIDs[i]].transform.position;
                Vector3 roadPathPos = GetRoadPathPosition(nodePos);
                positions.Add(roadPathPos);
            }
        }

        // Set positions to LineRenderer
        pathLineRenderer.positionCount = positions.Count;
        pathLineRenderer.SetPositions(positions.ToArray());
        pathLineRenderer.enabled = true;

        // Create directional arrows along the path
        if (showDirectionalArrows)
        {
            CreatePathArrows(positions);
        }

        Debug.Log($"Game-style LineRenderer path visualized with {pathIDs.Count} points using advanced road detection");
    }

    /// <summary>
    /// Creates directional arrows along the path
    /// </summary>
    private void CreatePathArrows(List<Vector3> pathPositions)
    {
        if (pathPositions.Count < 2) return;

        float totalDistance = 0f;
        List<float> distances = new List<float>();
        distances.Add(0f);

        // Calculate distances between points
        for (int i = 1; i < pathPositions.Count; i++)
        {
            float dist = Vector3.Distance(pathPositions[i - 1], pathPositions[i]);
            totalDistance += dist;
            distances.Add(totalDistance);
        }

        // Place arrows at regular intervals
        float currentDistance = 0f;
        while (currentDistance < totalDistance)
        {
            // Find which segment this distance falls on
            int segmentIndex = 0;
            for (int i = 1; i < distances.Count; i++)
            {
                if (currentDistance <= distances[i])
                {
                    segmentIndex = i - 1;
                    break;
                }
            }

            if (segmentIndex < pathPositions.Count - 1)
            {
                // Interpolate position along the segment
                float segmentStart = distances[segmentIndex];
                float segmentEnd = distances[segmentIndex + 1];
                float segmentLength = segmentEnd - segmentStart;

                if (segmentLength > 0)
                {
                    float t = (currentDistance - segmentStart) / segmentLength;
                    Vector3 arrowPos = Vector3.Lerp(pathPositions[segmentIndex], pathPositions[segmentIndex + 1], t);
                    Vector3 arrowDirection = (pathPositions[segmentIndex + 1] - pathPositions[segmentIndex]).normalized;

                    CreateArrow(arrowPos, arrowDirection);
                }
            }

            currentDistance += arrowSpacing;
        }
    }

    /// <summary>
    /// Creates a single directional arrow
    /// </summary>
    private void CreateArrow(Vector3 position, Vector3 direction)
    {
        GameObject arrow;

        if (arrowPrefab != null)
        {
            arrow = Instantiate(arrowPrefab, position, Quaternion.LookRotation(direction));
        }
        else
        {
            // Create default arrow using primitives
            arrow = CreateDefaultArrow(position, direction);
        }

        arrow.transform.SetParent(arrowsParent.transform);
        pathArrows.Add(arrow);
    }

    /// <summary>
    /// Creates a default arrow using Unity primitives
    /// </summary>
    private GameObject CreateDefaultArrow(Vector3 position, Vector3 direction)
    {
        GameObject arrow = new GameObject("PathArrow");
        arrow.transform.position = position;
        arrow.transform.rotation = Quaternion.LookRotation(direction);

        // Arrow body (cylinder)
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.transform.SetParent(arrow.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(arrowSize * 0.2f, arrowSize * 0.5f, arrowSize * 0.2f);
        body.transform.localRotation = Quaternion.Euler(90, 0, 0);

        // Remove collider
        DestroyImmediate(body.GetComponent<Collider>());

        // Set material
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        if (pathMaterial != null)
        {
            bodyRenderer.material = pathMaterial;
        }
        else
        {
            bodyRenderer.material.color = pathColor;
        }

        // Arrow head (cone)
        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(arrow.transform);
        head.transform.localPosition = new Vector3(0, 0, arrowSize * 0.4f);
        head.transform.localScale = new Vector3(arrowSize * 0.4f, arrowSize * 0.3f, arrowSize * 0.6f);

        // Remove collider
        DestroyImmediate(head.GetComponent<Collider>());

        // Set material
        Renderer headRenderer = head.GetComponent<Renderer>();
        if (pathMaterial != null)
        {
            headRenderer.material = pathMaterial;
        }
        else
        {
            headRenderer.material.color = pathColor;
        }

        return arrow;
    }

    /// <summary>
    /// Clears all path arrows
    /// </summary>
    private void ClearPathArrows()
    {
        foreach (GameObject arrow in pathArrows)
        {
            if (arrow != null)
            {
                DestroyImmediate(arrow);
            }
        }
        pathArrows.Clear();
    }

    public void ClearPath()
    {
        currentPathIDs.Clear();

        // Clear LineRenderer
        if (pathLineRenderer != null)
        {
            pathLineRenderer.positionCount = 0;
            pathLineRenderer.enabled = false;
        }

        // Clear arrows
        ClearPathArrows();
    }

    void SetupLineRenderer()
    {
        if (pathLineRenderer == null)
        {
            GameObject lineObj = new GameObject("GameStylePathRenderer");
            lineObj.transform.SetParent(transform);
            pathLineRenderer = lineObj.AddComponent<LineRenderer>();
        }
    }

    // FIXED: LineRenderer alignment to prevent perpendicular orientation
    void ForceLineRendererSetup()
    {
        if (pathLineRenderer == null) return;

        // Try multiple shader options for compatibility
        if (pathMaterial == null)
        {
            // URP-compatible shaders (in order of preference)
            Shader lineShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (lineShader == null)
                lineShader = Shader.Find("Universal Render Pipeline/Lit");
            if (lineShader == null)
                lineShader = Shader.Find("Sprites/Default");
            if (lineShader == null)
                lineShader = Shader.Find("Unlit/Color");
            if (lineShader == null)
                lineShader = Shader.Find("Legacy Shaders/Unlit/Color");

            if (lineShader != null)
            {
                pathMaterial = new Material(lineShader);
                pathMaterial.color = pathColor;

                // URP-specific properties
                if (pathMaterial.HasProperty("_BaseColor"))
                    pathMaterial.SetColor("_BaseColor", pathColor);
                if (pathMaterial.HasProperty("_Color"))
                    pathMaterial.SetColor("_Color", pathColor);
            }
            else
            {
                Debug.LogError("No suitable shader found for LineRenderer! Path may not be visible.");
            }
        }

        // FIXED: Configure LineRenderer properties to align properly with roads
        pathLineRenderer.material = pathMaterial;
        pathLineRenderer.startColor = pathColor;
        pathLineRenderer.endColor = pathColor;
        pathLineRenderer.startWidth = pathWidth;
        pathLineRenderer.endWidth = pathWidth;
        pathLineRenderer.positionCount = 0;
        pathLineRenderer.useWorldSpace = true;

        // CRITICAL FIX: Use View alignment to make line face camera, not perpendicular to road
        pathLineRenderer.alignment = LineAlignment.View;

        pathLineRenderer.textureMode = LineTextureMode.Tile;
        pathLineRenderer.widthMultiplier = 1f;
        pathLineRenderer.numCornerVertices = 8;
        pathLineRenderer.numCapVertices = 4;

        // Smooth width curve for better appearance
        AnimationCurve widthCurve = new AnimationCurve();
        widthCurve.AddKey(0f, 1f);
        widthCurve.AddKey(1f, 1f);
        for (int i = 0; i < widthCurve.keys.Length; i++)
        {
            widthCurve.keys[i].inTangent = 0f;
            widthCurve.keys[i].outTangent = 0f;
        }
        pathLineRenderer.widthCurve = widthCurve;

        pathLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pathLineRenderer.receiveShadows = false;
        pathLineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        pathLineRenderer.allowOcclusionWhenDynamic = false;
        pathLineRenderer.enabled = true;
        pathLineRenderer.gameObject.SetActive(true);

        Debug.Log($"Game-style LineRenderer setup complete with FIXED alignment. Material: {(pathMaterial != null ? pathMaterial.shader.name : "NULL")}, Width: {pathWidth}");
    }

    public void AddConnection(int fromNodeID, int toNodeID, bool bidirectional = true)
    {
        if (!nodeMap.ContainsKey(fromNodeID) || !nodeMap.ContainsKey(toNodeID))
            return;

        // Check if connection already exists
        bool connectionExists = connections.Any(c =>
            (c.fromNodeID == fromNodeID && c.toNodeID == toNodeID) ||
            (c.bidirectional && c.fromNodeID == toNodeID && c.toNodeID == fromNodeID));

        if (!connectionExists)
        {
            float weight = Vector3.Distance(nodeMap[fromNodeID].transform.position, nodeMap[toNodeID].transform.position);
            connections.Add(new GraphConnection(fromNodeID, toNodeID, weight, bidirectional));
            RefreshGraph();
        }
    }

    public void RemoveConnection(int fromNodeID, int toNodeID)
    {
        connections.RemoveAll(c =>
            (c.fromNodeID == fromNodeID && c.toNodeID == toNodeID) ||
            (c.bidirectional && c.fromNodeID == toNodeID && c.toNodeID == fromNodeID));

        RefreshGraph();
    }

    // Test methods
    [ContextMenu("Test Game-Style Path Visualization")]
    public void TestGameStylePathVisualization()
    {
        if (nodes.Count < 2)
        {
            Debug.LogWarning("Need at least 2 nodes to test path visualization");
            return;
        }

        // Create a test path with first few nodes
        List<int> testPathIDs = new List<int>();
        for (int i = 0; i < Mathf.Min(5, nodes.Count); i++)
        {
            if (nodes[i] != null)
            {
                testPathIDs.Add(nodes[i].nodeID);
            }
        }

        // Force use LineRenderer path and visualize
        bool originalUseLineRenderer = useLineRendererPath;
        useLineRendererPath = true;
        VisualizePath(testPathIDs);

        Debug.Log($"Game-style path test completed with {testPathIDs.Count} nodes. Check scene for LineRenderer path with arrows!");

        // Restore original setting
        useLineRendererPath = originalUseLineRenderer;
    }

    [ContextMenu("Test LineRenderer Visibility")]
    public void TestLineRendererVisibility()
    {
        Debug.Log("Testing LineRenderer visibility...");

        if (pathLineRenderer == null)
        {
            SetupLineRenderer();
            ForceLineRendererSetup();
        }

        if (nodes.Count >= 2)
        {
            List<Vector3> testPositions = new List<Vector3>();
            for (int i = 0; i < Mathf.Min(5, nodes.Count); i++)
            {
                if (nodes[i] != null)
                {
                    Vector3 nodePos = nodes[i].transform.position;
                    Vector3 roadPathPos = GetRoadPathPosition(nodePos);
                    testPositions.Add(roadPathPos);
                }
            }

            pathLineRenderer.positionCount = testPositions.Count;
            pathLineRenderer.SetPositions(testPositions.ToArray());
            pathLineRenderer.enabled = true;

            Debug.Log($"LineRenderer test: {testPositions.Count} positions set using road detection, width: {pathLineRenderer.startWidth}");
        }
        else
        {
            Debug.LogWarning("Not enough nodes to test LineRenderer. Add at least 2 nodes to the scene.");
        }
    }

    // Gizmo visualization
    void OnDrawGizmos()
    {
        if (showConnections)
        {
            DrawConnections();
        }

        if (showPathInEditor && currentPathIDs.Count > 1)
        {
            DrawPath();
        }
    }

    void DrawConnections()
    {
        Gizmos.color = connectionColor;
        foreach (var connection in connections)
        {
            if (nodeMap.ContainsKey(connection.fromNodeID) && nodeMap.ContainsKey(connection.toNodeID) &&
                nodeMap[connection.fromNodeID] != null && nodeMap[connection.toNodeID] != null)
            {
                Vector3 start = nodeMap[connection.fromNodeID].transform.position;
                Vector3 end = nodeMap[connection.toNodeID].transform.position;

                Gizmos.DrawLine(start, end);

                // Draw arrow for direction
                Vector3 direction = (end - start).normalized;
                Vector3 arrowHead = end - direction * 0.5f;
                Vector3 right = Vector3.Cross(Vector3.up, direction) * 0.3f;
                Gizmos.DrawLine(arrowHead + right, end);
                Gizmos.DrawLine(arrowHead - right, end);

                // Show weight at midpoint
#if UNITY_EDITOR
                Vector3 midPoint = (start + end) * 0.5f;
                UnityEditor.Handles.Label(midPoint, connection.weight.ToString("F1"));
#endif
            }
        }
    }

    void DrawPath()
    {
        Gizmos.color = pathColor;

        for (int i = 0; i < currentPathIDs.Count - 1; i++)
        {
            if (nodeMap.ContainsKey(currentPathIDs[i]) && nodeMap.ContainsKey(currentPathIDs[i + 1]) &&
                nodeMap[currentPathIDs[i]] != null && nodeMap[currentPathIDs[i + 1]] != null)
            {
                Vector3 start = nodeMap[currentPathIDs[i]].transform.position;
                Vector3 end = nodeMap[currentPathIDs[i + 1]].transform.position;

                // Draw path at road-detected height
                start = GetRoadPathPosition(start);
                end = GetRoadPathPosition(end);

                Gizmos.DrawLine(start, end);

                // Draw path direction arrows
                Vector3 direction = (end - start).normalized;
                Vector3 arrowPos = Vector3.Lerp(start, end, 0.5f);
                Vector3 right = Vector3.Cross(Vector3.up, direction) * 0.5f;
                Gizmos.DrawLine(arrowPos - direction * 0.3f + right, arrowPos);
                Gizmos.DrawLine(arrowPos - direction * 0.3f - right, arrowPos);
            }
        }
    }
}
