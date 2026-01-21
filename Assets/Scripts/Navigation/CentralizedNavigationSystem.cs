// ============================================================================
// CENTRALIZED NAVIGATION SYSTEM - COMPLETE
// ============================================================================
// Supports destination-based traffic with A* pathfinding
// No chains needed - vehicles navigate dynamically between nodes
// ============================================================================

using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class CentralizedNavigationSystem : MonoBehaviour
{
    [Header("=== GRAPH DATA ===")]
    public List<NavNode> nodes = new List<NavNode>();
    public List<ConnectionDefinition> connectionDefinitions = new List<ConnectionDefinition>();
    [HideInInspector] public Dictionary<int, NavNode> nodeMap = new Dictionary<int, NavNode>();
    public GameObject nodesParent;

    [Header("=== PATH VISUALIZATION ===")]
    public LineRenderer pathLineRenderer;
    public bool showPathsInEditor = true;
    public bool visualizeAllConnectionsEditor = false;

    [Header("=== AUTO CONNECT ===")]
    public float autoConnectMaxDistance = 20f;

    [Header("=== NODE CREATION ===")]
    public float newNodeDistance = 15f;

    [Header("=== NPC TRAFFIC SYSTEM (DESTINATION-BASED) ===")]
    [SerializeField] private GameObject npcVehiclePrefab;
    [SerializeField] private List<GameObject> npcVariants = new List<GameObject>();
    [SerializeField] private int totalTrafficVehicles = 15;
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField] private float vehicleSpeed = 12f;
    [SerializeField] private float stoppingDistance = 8f;
    [SerializeField] private float detectionRange = 15f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private bool showDebugGizmos = true;
    [SerializeField] private float vehicleSpacing = 15f;

    [Header("=== TRAFFIC CHAINS (DEPRECATED - NOT USED IN DESTINATION MODE) ===")]
    [Tooltip("Legacy chain system - not used with destination-based traffic")]
    public List<TrafficWaypointChain> trafficChains = new List<TrafficWaypointChain>();
    
    private List<TrafficVehicle> activeVehicles = new List<TrafficVehicle>();
    private int nextNodeID = 0;

    // ========================================
    // INITIALIZATION
    // ========================================
    
    private void Awake()
    {
        ValidateAndRebuildGraph();
    }

    private void Start()
    {
        ValidateAndRebuildGraph();
        SetupLineRenderer();

        if (spawnOnStart && Application.isPlaying)
        {
            StartCoroutine(InitializeTraffic());
        }
    }

    private IEnumerator InitializeTraffic()
    {
        yield return new WaitForSeconds(0.5f);

        if (nodes.Count < 2)
        {
            Debug.LogError("[Traffic] Need at least 2 nodes for destination-based traffic!");
            yield break;
        }

        SpawnTrafficVehicles();
    }

    // ========================================
    // GRAPH MANAGEMENT
    // ========================================

    [ContextMenu("Validate And Rebuild Graph")]
    public void ValidateAndRebuildGraph()
    {
        nodes.RemoveAll(n => n == null);

        nextNodeID = 0;
        foreach (var node in nodes)
        {
            if (node.nodeID >= nextNodeID)
                nextNodeID = node.nodeID + 1;
        }

        nodeMap.Clear();
        HashSet<int> usedIDs = new HashSet<int>();

        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (node == null) continue;

            if (usedIDs.Contains(node.nodeID))
            {
                node.nodeID = nextNodeID++;
            }

            usedIDs.Add(node.nodeID);
            node.parentNavSystem = this;
            nodeMap[node.nodeID] = node;
        }

        ValidateConnections();
    }

    private void ValidateConnections()
    {
        List<ConnectionDefinition> validConnections = new List<ConnectionDefinition>();

        foreach (var conn in connectionDefinitions)
        {
            if (nodeMap.ContainsKey(conn.fromNodeID) && nodeMap.ContainsKey(conn.toNodeID))
            {
                validConnections.Add(conn);
            }
        }

        connectionDefinitions = validConnections;
    }

    public void RefreshGraph()
    {
        ValidateAndRebuildGraph();
    }

    public void RegisterNode(NavNode node)
    {
        if (node == null) return;

        if (nodes.Contains(node))
        {
            if (!nodeMap.ContainsKey(node.nodeID))
                nodeMap[node.nodeID] = node;
            node.parentNavSystem = this;
            return;
        }

        if (node.nodeID < 0 || nodeMap.ContainsKey(node.nodeID))
        {
            node.nodeID = nextNodeID++;
        }
        else
        {
            if (node.nodeID >= nextNodeID)
                nextNodeID = node.nodeID + 1;
        }

        nodes.Add(node);
        node.parentNavSystem = this;
        nodeMap[node.nodeID] = node;

#if UNITY_EDITOR
        UpdateEditorConnectionsVisualization();
#endif
    }

    public void AddConnectionDefinition(int fromID, int toID, bool bidirectional)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID))
        {
            return;
        }

        AddConnection(fromID, toID, bidirectional);
        ValidateConnections();
#if UNITY_EDITOR
        UpdateEditorConnectionsVisualization();
#endif
    }

    public void AddConnection(int fromID, int toID, bool bidirectional)
    {
        if (!nodeMap.ContainsKey(fromID) || !nodeMap.ContainsKey(toID))
        {
            return;
        }

        bool exists = connectionDefinitions.Any(c =>
            (c.fromNodeID == fromID && c.toNodeID == toID) ||
            (bidirectional && c.fromNodeID == toID && c.toNodeID == fromID));

        if (!exists)
            connectionDefinitions.Add(new ConnectionDefinition(fromID, toID, bidirectional));
    }

    public NavNode CreateNode(Vector3 position, int id = -1, Quaternion? rotation = null)
    {
        if (nodesParent == null)
        {
            nodesParent = new GameObject("NavigationNodes");
            nodesParent.transform.SetParent(transform);
        }

        int finalID = (id == -1 || nodeMap.ContainsKey(id)) ? nextNodeID++ : id;
        
        if (id >= nextNodeID)
            nextNodeID = id + 1;

        GameObject nodeObj = new GameObject($"NavNode_{finalID}");
        nodeObj.transform.SetParent(nodesParent.transform);
        nodeObj.transform.position = position;
        nodeObj.transform.rotation = rotation ?? Quaternion.identity;

        NavNode node = nodeObj.AddComponent<NavNode>();
        node.parentNavSystem = this;
        node.nodeID = finalID;
        nodes.Add(node);
        nodeMap[finalID] = node;

        return node;
    }

    // ========================================
    // NODE QUERIES
    // ========================================

    public int GetClosestNode(Vector3 worldPosition)
    {
        if (nodeMap.Count == 0) return -1;
        
        float closestDist = float.MaxValue;
        int closestID = -1;
        
        foreach (var kvp in nodeMap)
        {
            if (kvp.Value == null) continue;
            
            float dist = Vector3.Distance(worldPosition, kvp.Value.worldPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestID = kvp.Key;
            }
        }
        
        return closestID;
    }

    public int GetRandomNode()
    {
        if (nodeMap.Count == 0) return -1;
        List<int> nodeIDs = nodeMap.Keys.ToList();
        return nodeIDs[UnityEngine.Random.Range(0, nodeIDs.Count)];
    }

    public int GetRandomNode(HashSet<int> excludeNodes)
    {
        if (nodeMap.Count == 0) return -1;
        List<int> available = nodeMap.Keys.Where(id => !excludeNodes.Contains(id)).ToList();
        return available.Count > 0 ? available[UnityEngine.Random.Range(0, available.Count)] : GetRandomNode();
    }

    public int GetDistantNode(int fromNodeID, float minDistance = 25f)
    {
        if (!nodeMap.ContainsKey(fromNodeID)) return -1;

        var candidates = nodeMap.Keys
            .Where(id => id != fromNodeID && nodeMap.ContainsKey(id))
            .Where(id => Vector3.Distance(nodeMap[fromNodeID].worldPosition, nodeMap[id].worldPosition) >= minDistance)
            .ToList();
        
        return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : fromNodeID;
    }

    // ========================================
    // PATHFINDING (A* ALGORITHM)
    // ========================================

    public List<int> FindPath(int start, int target)
    {
        if (!nodeMap.ContainsKey(start) || !nodeMap.ContainsKey(target))
        {
            Debug.LogWarning($"[NavSystem] FindPath failed: start={start} or target={target} not in nodeMap");
            return new List<int>();
        }

        if (start == target)
            return new List<int> { start };

        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float> { { start, 0f } };
        var fScore = new Dictionary<int, float> { { start, Heuristic(start, target) } };
        var openSet = new PriorityQueue<int>();
        var closedSet = new HashSet<int>();

        openSet.Enqueue(start, fScore[start]);

        while (openSet.Count > 0)
        {
            int current = openSet.Dequeue();
            
            if (current == target) 
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);

            foreach (int neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor)) continue;
                if (!nodeMap.ContainsKey(neighbor)) continue;

                float tentativeG = gScore[current] + 1f;

                if (!gScore.ContainsKey(neighbor) || tentativeG < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeG;
                    fScore[neighbor] = tentativeG + Heuristic(neighbor, target);

                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        Debug.LogWarning($"[NavSystem] No path found from {start} to {target}");
        return new List<int>();
    }

    private float Heuristic(int a, int b)
    {
        if (!nodeMap.ContainsKey(a) || !nodeMap.ContainsKey(b)) return 999999f;
        Vector3 pa = nodeMap[a].worldPosition;
        Vector3 pb = nodeMap[b].worldPosition;
        return Vector3.Distance(new Vector3(pa.x, 0, pa.z), new Vector3(pb.x, 0, pb.z));
    }

    public List<int> GetNeighbors(int nodeID)
    {
        List<int> neighbors = new List<int>();
        
        foreach (var c in connectionDefinitions)
        {
            if (c.fromNodeID == nodeID && nodeMap.ContainsKey(c.toNodeID))
                neighbors.Add(c.toNodeID);
            else if (c.bidirectional && c.toNodeID == nodeID && nodeMap.ContainsKey(c.fromNodeID))
                neighbors.Add(c.fromNodeID);
        }
        
        return neighbors.Distinct().ToList();
    }

    private List<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
    {
        List<int> path = new List<int> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    // ========================================
    // TRAFFIC SPAWNING (DESTINATION-BASED)
    // ========================================

    private void SpawnTrafficVehicles()
    {
        ClearAllTraffic();

        if (nodes.Count < 2)
        {
            Debug.LogError("[Traffic] Need at least 2 nodes for destination-based traffic!");
            return;
        }

        // Collect prefabs
        List<GameObject> prefabs = new List<GameObject>();
        if (npcVehiclePrefab != null) prefabs.Add(npcVehiclePrefab);
        prefabs.AddRange(npcVariants.Where(v => v != null));

        if (prefabs.Count == 0)
        {
            Debug.LogError("[Traffic] No vehicle prefabs assigned!");
            return;
        }

        // Get all available spawn nodes
        List<int> availableNodeIDs = nodeMap.Keys.ToList();
        
        // Shuffle for random distribution
        for (int i = 0; i < availableNodeIDs.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, availableNodeIDs.Count);
            int temp = availableNodeIDs[i];
            availableNodeIDs[i] = availableNodeIDs[r];
            availableNodeIDs[r] = temp;
        }

        List<Vector3> usedPositions = new List<Vector3>();
        int spawned = 0;

        Debug.Log("[Traffic] ========== SPAWNING DESTINATION-BASED TRAFFIC ==========");

        foreach (int nodeID in availableNodeIDs)
        {
            if (spawned >= totalTrafficVehicles) break;
            
            if (!nodeMap.ContainsKey(nodeID)) continue;
            
            Vector3 spawnPosition = nodeMap[nodeID].worldPosition;
            
            // Check spacing to avoid overlap
            bool tooClose = false;
            foreach (Vector3 usedPos in usedPositions)
            {
                if (Vector3.Distance(spawnPosition, usedPos) < vehicleSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            
            if (tooClose) continue;
            
            // Select random prefab
            GameObject prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            GameObject vehicleObj = Instantiate(prefab);
            vehicleObj.name = $"Traffic_{spawned}";
            vehicleObj.transform.position = spawnPosition;
            vehicleObj.transform.rotation = nodeMap[nodeID].transform.rotation;
            
            // Setup TrafficVehicle component
            TrafficVehicle traffic = vehicleObj.GetComponent<TrafficVehicle>();
            if (traffic == null)
                traffic = vehicleObj.AddComponent<TrafficVehicle>();
            
            // Setup Rigidbody
            Rigidbody rb = vehicleObj.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = vehicleObj.AddComponent<Rigidbody>();
                rb.mass = 1200f;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 5f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
            
            rb.position = spawnPosition;
            
            // Randomize speed slightly
            float speed = vehicleSpeed * UnityEngine.Random.Range(0.85f, 1.15f);
            
            // Initialize with destination-based navigation
            traffic.Initialize(
                this,
                nodeID,
                speed,
                stoppingDistance,
                detectionRange,
                obstacleLayer
            );
            
            activeVehicles.Add(traffic);
            usedPositions.Add(spawnPosition);
            spawned++;
            
            Debug.Log($"[Traffic] Spawned vehicle {spawned} at Node {nodeID}");
        }

        Debug.Log($"[Traffic] ========== SPAWNED {spawned} VEHICLES ==========");
    }

    [ContextMenu("Spawn Traffic Now")]
    public void SpawnTrafficNow()
    {
        SpawnTrafficVehicles();
    }

    [ContextMenu("Clear All Traffic")]
    public void ClearAllTraffic()
    {
        foreach (var v in activeVehicles)
        {
            if (v != null && v.gameObject != null)
                Destroy(v.gameObject);
        }
        activeVehicles.Clear();
        
        Debug.Log("[Traffic] All traffic cleared");
    }

    [ContextMenu("Respawn Traffic")]
    public void RespawnTraffic()
    {
        ClearAllTraffic();
        SpawnTrafficVehicles();
    }

    // ========================================
    // PATH VISUALIZATION
    // ========================================

    public void ClearPathVisualization()
    {
        if (pathLineRenderer != null)
        {
            pathLineRenderer.enabled = false;
            pathLineRenderer.positionCount = 0;
        }
    }

    private void SetupLineRenderer()
    {
        if (pathLineRenderer != null) return;

        GameObject lrObj = new GameObject("PathVisualizer");
        lrObj.transform.SetParent(transform);
        pathLineRenderer = lrObj.AddComponent<LineRenderer>();

        pathLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        var gradient = new Gradient();
        gradient.colorKeys = new[] {
            new GradientColorKey(Color.yellow, 0f),
            new GradientColorKey(Color.red, 1f)
        };
        pathLineRenderer.colorGradient = gradient;
        pathLineRenderer.startWidth = 0.2f;
        pathLineRenderer.endWidth = 0.2f;
        pathLineRenderer.enabled = false;
    }

    public void VisualizePath(List<int> path)
    {
        if (path == null || path.Count == 0) return;

        SetupLineRenderer();
        pathLineRenderer.positionCount = path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            if (nodeMap.ContainsKey(path[i]) && nodeMap[path[i]] != null)
                pathLineRenderer.SetPosition(i, nodeMap[path[i]].worldPosition + Vector3.up * 0.5f);
        }

        pathLineRenderer.enabled = true;
    }

    // ========================================
    // GIZMOS
    // ========================================

    private void OnDrawGizmos()
    {
        // Draw nodes
        if (nodes != null && nodes.Count > 0)
        {
            foreach (var node in nodes)
            {
                if (node == null || node.transform == null) continue;
                Gizmos.color = new Color(0f, 1f, 1f, 1f);
                Gizmos.DrawSphere(node.transform.position, 0.6f);

#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    node.transform.position + Vector3.up * 1.2f, 
                    $"Node {node.nodeID}",
                    new GUIStyle() { 
                        normal = new GUIStyleState() { textColor = Color.white }, 
                        fontSize = 14, 
                        fontStyle = FontStyle.Bold, 
                        alignment = TextAnchor.MiddleCenter 
                    }
                );
#endif
            }
        }

        // Draw connections
        if (connectionDefinitions != null && connectionDefinitions.Count > 0)
        {
            foreach (var conn in connectionDefinitions)
            {
                if (!nodeMap.ContainsKey(conn.fromNodeID) || !nodeMap.ContainsKey(conn.toNodeID)) continue;
                
                Vector3 start = nodeMap[conn.fromNodeID].transform.position + Vector3.up * 0.2f;
                Vector3 end = nodeMap[conn.toNodeID].transform.position + Vector3.up * 0.2f;
                
                Gizmos.color = conn.bidirectional 
                    ? new Color(0f, 1f, 0f, 0.8f)  // Green for bidirectional
                    : new Color(1f, 0.5f, 0f, 0.8f); // Orange for one-way
                
                Gizmos.DrawLine(start, end);
                
                // Draw arrow for one-way connections
                if (!conn.bidirectional)
                {
                    Vector3 direction = (end - start).normalized;
                    Vector3 midPoint = start + direction * Vector3.Distance(start, end) * 0.5f;
                    Vector3 right = Vector3.Cross(Vector3.up, direction) * 0.5f;
                    
                    Gizmos.DrawLine(midPoint, midPoint - direction * 1f + right);
                    Gizmos.DrawLine(midPoint, midPoint - direction * 1f - right);
                }
            }
        }

        // Draw active vehicles
        if (showDebugGizmos && Application.isPlaying && activeVehicles != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 1f);
            foreach (var v in activeVehicles)
            {
                if (v != null && v.transform != null)
                    Gizmos.DrawWireSphere(v.transform.position + Vector3.up, 0.8f);
            }
        }
    }

    // ========================================
    // EDITOR UTILITIES
    // ========================================

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ValidateAndRebuildGraph();
            UpdateEditorConnectionsVisualization();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying && visualizeAllConnectionsEditor)
            DrawAllConnectionsIntoLineRenderer();
    }

    [ContextMenu("Collect All Nodes")]
    public void CollectAllNodes()
    {
        nodes.Clear();
        nodeMap.Clear();

        NavNode[] allNodes = nodesParent != null 
            ? nodesParent.GetComponentsInChildren<NavNode>(true) 
            : FindObjectsOfType<NavNode>();
            
        HashSet<int> existingIDs = new HashSet<int>();
        foreach (var node in allNodes)
        {
            if (node != null && node.nodeID >= 0)
                existingIDs.Add(node.nodeID);
        }

        nextNodeID = 0;
        foreach (int id in existingIDs)
        {
            if (id >= nextNodeID)
                nextNodeID = id + 1;
        }

        foreach (var node in allNodes)
        {
            if (node == null) continue;
            if (node.nodeID < 0 || nodeMap.ContainsKey(node.nodeID))
                node.nodeID = nextNodeID++;
            node.parentNavSystem = this;
            nodes.Add(node);
            nodeMap[node.nodeID] = node;
        }

        ValidateConnections();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("Auto Connect Nodes")]
    public void AutoConnectNodes()
    {
        connectionDefinitions.Clear();
        for (int i = 0; i < nodes.Count; i++)
        {
            var ni = nodes[i];
            if (ni == null) continue;
            for (int j = i + 1; j < nodes.Count; j++)
            {
                var nj = nodes[j];
                if (nj == null) continue;
                if (Vector3.Distance(ni.transform.position, nj.transform.position) <= autoConnectMaxDistance)
                    AddConnection(ni.nodeID, nj.nodeID, true);
            }
        }
        ValidateConnections();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("Clear All Connections")]
    public void ClearAllConnections()
    {
        connectionDefinitions.Clear();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("Create Node Forward")]
    public void CreateNodeForward()
    {
        if (nodes.Count == 0)
        {
            CreateNode(transform.position, -1, transform.rotation);
            return;
        }

        NavNode lastNode = nodes.Last();
        Vector3 forwardPos = lastNode.transform.position + lastNode.transform.forward * newNodeDistance;

        NavNode newNode = CreateNode(forwardPos, -1, lastNode.transform.rotation);
        AddConnectionDefinition(lastNode.nodeID, newNode.nodeID, true);

        Selection.activeGameObject = newNode.gameObject;
    }

    [ContextMenu("Create Node From Selected")]
    public void CreateNextNodeFromSelected()
    {
        NavNode selected = GetSelectedNode();
        if (selected == null)
        {
            Debug.LogWarning("No NavNode selected!");
            return;
        }

        Vector3 forwardPos = selected.transform.position + selected.transform.forward * newNodeDistance;

        NavNode newNode = CreateNode(forwardPos, -1, selected.transform.rotation);
        AddConnectionDefinition(selected.nodeID, newNode.nodeID, true);

        Selection.activeGameObject = newNode.gameObject;
    }

    private NavNode GetSelectedNode()
    {
        if (Selection.activeGameObject == null) return null;
        NavNode selected = Selection.activeGameObject.GetComponent<NavNode>();
        return selected?.parentNavSystem == this ? selected : null;
    }

    [ContextMenu("Setup Demo")]
    public void SetupDemo()
    {
        ClearAllConnections();
        nodes.Clear();
        nodeMap.Clear();
        nextNodeID = 0;

        if (nodesParent == null)
        {
            nodesParent = new GameObject("NavigationNodes");
            nodesParent.transform.SetParent(transform);
        }

        Vector3[] demoPositions = {
            new Vector3(0, 0.5f, 0), new Vector3(10, 0.5f, 0),
            new Vector3(15, 0.5f, 10), new Vector3(10, 0.5f, 20),
            new Vector3(0, 0.5f, 20), new Vector3(-10, 0.5f, 10)
        };

        for (int i = 0; i < demoPositions.Length; i++)
        {
            CreateNode(demoPositions[i], -1, Quaternion.identity);
        }

        List<int> nodeIDs = nodes.Select(n => n.nodeID).ToList();
        for (int i = 0; i < nodeIDs.Count - 1; i++)
        {
            AddConnectionDefinition(nodeIDs[i], nodeIDs[i + 1], true);
        }
        AddConnectionDefinition(nodeIDs[nodeIDs.Count - 1], nodeIDs[0], true);

        ValidateAndRebuildGraph();
        UpdateEditorConnectionsVisualization();
        
        Debug.Log("[NavSystem] Demo setup complete with 6 nodes!");
    }

    [ContextMenu("Test Path 0 to Last")]
    public void TestPathZeroToLast()
    {
        if (nodes.Count < 2) return;
        
        int startID = nodes[0].nodeID;
        int endID = nodes[nodes.Count - 1].nodeID;
        
        var path = FindPath(startID, endID);
        
        if (path.Count > 0)
        {
            Debug.Log($"[NavSystem] Path found: {string.Join(" → ", path)}");
            VisualizePath(path);
        }
        else
        {
            Debug.LogError($"[NavSystem] No path found from {startID} to {endID}");
        }
    }

    private void UpdateEditorConnectionsVisualization()
    {
        if (Application.isPlaying || !visualizeAllConnectionsEditor) return;
        DrawAllConnectionsIntoLineRenderer();
    }

    private void DrawAllConnectionsIntoLineRenderer()
    {
        SetupLineRenderer();
        
        List<Vector3> positions = new List<Vector3>();

        foreach (var conn in connectionDefinitions)
        {
            if (!nodeMap.ContainsKey(conn.fromNodeID) || !nodeMap.ContainsKey(conn.toNodeID))
                continue;

            Vector3 a = nodeMap[conn.fromNodeID].transform.position + Vector3.up * 0.3f;
            Vector3 b = nodeMap[conn.toNodeID].transform.position + Vector3.up * 0.3f;

            positions.Add(a);
            positions.Add(b);
        }

        pathLineRenderer.positionCount = positions.Count;
        pathLineRenderer.SetPositions(positions.ToArray());
        pathLineRenderer.enabled = positions.Count > 0;
    }

    [ContextMenu("Debug Print All Nodes")]
    public void DebugPrintAllNodes()
    {
        Debug.Log("========== NODE MAP DEBUG ==========");
        Debug.Log($"Total nodes: {nodes.Count}");
        Debug.Log($"Total in map: {nodeMap.Count}");
        
        foreach (var node in nodes)
        {
            if (node == null)
            {
                Debug.LogWarning("NULL node found!");
                continue;
            }
            
            Debug.Log($"Node '{node.name}' | ID: {node.nodeID} | Position: {node.worldPosition}");
        }
        Debug.Log("====================================");
    }

    [ContextMenu("Debug Print All Connections")]
    public void DebugPrintAllConnections()
    {
        Debug.Log("========== CONNECTION DEBUG ==========");
        Debug.Log($"Total connections: {connectionDefinitions.Count}");
        
        foreach (var conn in connectionDefinitions)
        {
            bool fromExists = nodeMap.ContainsKey(conn.fromNodeID);
            bool toExists = nodeMap.ContainsKey(conn.toNodeID);
            
            string fromName = fromExists ? nodeMap[conn.fromNodeID].name : "INVALID";
            string toName = toExists ? nodeMap[conn.toNodeID].name : "INVALID";
            
            string dir = conn.bidirectional ? "<->" : "->";
            string status = (fromExists && toExists) ? "✓" : "✗";
            
            Debug.Log($"{status} {conn.fromNodeID}({fromName}) {dir} {conn.toNodeID}({toName})");
        }
        Debug.Log("======================================");
    }
#endif

    // ========================================
    // PRIORITY QUEUE FOR A* PATHFINDING
    // ========================================
    
    public class PriorityQueue<T>
    {
        private readonly List<(T item, float priority)> elements = new();
        public int Count => elements.Count;

        public void Enqueue(T item, float priority)
        {
            elements.Add((item, priority));
            int i = elements.Count - 1;
            while (i > 0 && elements[i - 1].priority > elements[i].priority)
            {
                var temp = elements[i - 1];
                elements[i - 1] = elements[i];
                elements[i] = temp;
                i--;
            }
        }

        public T Dequeue()
        {
            var best = elements[0];
            elements.RemoveAt(0);
            return best.item;
        }

        public bool Contains(T item) => elements.Any(e => EqualityComparer<T>.Default.Equals(e.item, item));
    }
}

// ========================================
// TRAFFIC CHAIN (LEGACY - KEPT FOR COMPATIBILITY)
// ========================================

[System.Serializable]
public class TrafficWaypointChain
{
    public string chainName = "Traffic_Chain";
    public List<Transform> waypoints = new List<Transform>();
    public List<int> nodeIDs = new List<int>();
    public bool loop = false;
    [Range(0.5f, 3f)] public float speedMultiplier = 1f;
}