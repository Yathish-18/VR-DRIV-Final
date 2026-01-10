using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
[ExecuteInEditMode]
#endif

public class CentralizedNavigationSystem : MonoBehaviour
{
    [Header("Graph Data")]
    public List<NavNode> nodes = new List<NavNode>();
    public List<ConnectionDefinition> connectionDefinitions = new List<ConnectionDefinition>();

    [HideInInspector] public Dictionary<int, NavNode> nodeMap = new Dictionary<int, NavNode>();
    public GameObject nodesParent;

    [Header("Path Visualization")]
    public LineRenderer pathLineRenderer;
    public bool showPathsInEditor = true;
    [Tooltip("Editor-only: show ALL connections")]
    public bool visualizeAllConnectionsEditor = false;

    [Header("Auto Connect")]
    public float autoConnectMaxDistance = 20f;

    [Header("Node Creation")]
    public float newNodeDistance = 15f;

    [Header("=== NPC SPAWN SETTINGS ===")]
    [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();
    [SerializeField][Range(5, 100)] private int totalNPCs = 20;

    [Header("=== SPAWN SETTINGS ===")]
    [SerializeField] private bool autoCollectNodesOnStart = true;
    [SerializeField] private bool autoFixCollisions = true;
    [SerializeField] private float spawnStaggerDelay = 0.2f;
    [SerializeField] private float minNodeDistance = 5f; // Simple distance between cars

    [Header("=== DEBUG ===")]
    [SerializeField] private bool showSpawnZones = true;
    [SerializeField] private bool logSpawnDetails = true;

    private List<NPCVehicleInstance> activeNPCs = new List<NPCVehicleInstance>();
    private HashSet<int> usedNodes = new HashSet<int>();

    #region Initialization

    private void Awake()
    {
        StartCoroutine(InitializeSystem());
    }

    private IEnumerator InitializeSystem()
    {
        yield return new WaitForSeconds(1f);

        if (vehiclePrefabs.Count == 0)
        {
            Debug.LogError("[NPCManager] ❌ No vehicle prefabs assigned!");
            yield break;
        }

        // Setup nav graph
        RefreshGraph();

        if (nodes.Count == 0 && autoCollectNodesOnStart)
        {
            Debug.Log("[NPCManager] 🔍 Collecting nodes...");
            CollectAllNodes();
            yield return new WaitForSeconds(0.5f);
            RefreshGraph();
        }

        if (nodeMap.Count == 0)
        {
            Debug.LogError("[NPCManager] ❌ No nodes found! Right-click CentralizedNavigationSystem → Collect All Nodes");
            yield break;
        }

        Debug.Log($"[NPCManager] 🚗 Found {nodeMap.Count} nodes, spawning {totalNPCs} NPCs...");

        // RANDOM SPAWN WITH RANDOM DESTINATIONS
        yield return StartCoroutine(SpawnAllNPCs());

        Debug.Log($"✅ [NPCManager] Spawned {activeNPCs.Count}/{totalNPCs} NPCs successfully!");
    }

    #endregion

    private void Start()
    {
        RefreshGraph();
        SetupLineRenderer();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            RefreshGraph();
            UpdateEditorConnectionsVisualization();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying && visualizeAllConnectionsEditor)
            DrawAllConnectionsIntoLineRenderer();
    }
#endif

    // 🔥 SAFE PATHFINDING METHODS
    public int GetClosestNode(Vector3 worldPosition)
    {
        if (nodeMap.Count == 0) return 0;

        float closestDist = float.MaxValue;
        int closestID = 0;

        foreach (var kvp in nodeMap)
        {
            float dist = Vector3.Distance(worldPosition, kvp.Value.worldPosition);
            if (dist < closestDist)
            {
                closestDist = dist;
                closestID = kvp.Key;
            }
        }
        return closestID;
    }

    // 🎲 GET RANDOM NODE
    public int GetRandomNode()
    {
        if (nodeMap.Count == 0) return -1;

        List<int> nodeIDs = nodeMap.Keys.ToList();
        return nodeIDs[UnityEngine.Random.Range(0, nodeIDs.Count)];
    }

    // 🎲 GET RANDOM NODE (EXCLUDING SPECIFIC NODES)
    public int GetRandomNode(HashSet<int> excludeNodes)
    {
        if (nodeMap.Count == 0) return -1;

        List<int> availableNodes = nodeMap.Keys.Where(id => !excludeNodes.Contains(id)).ToList();

        if (availableNodes.Count == 0)
            return GetRandomNode(); // Fallback to any node

        return availableNodes[UnityEngine.Random.Range(0, availableNodes.Count)];
    }

    [ContextMenu("Test LineRenderer Visibility")]
    public void TestLineRendererVisibility()
    {
        SetupLineRenderer();
        if (nodes.Count > 1)
            VisualizePath(new List<int> { 0, 1 });
    }

    [ContextMenu("➡️ Create Node Forward (Last)")]
    public void CreateNodeForward()
    {
        if (nodes.Count == 0)
        {
            CreateNode(transform.position, -1, transform.rotation);
            return;
        }

        NavNode lastNode = nodes.Last();
        Vector3 forwardPos = lastNode.transform.position + lastNode.transform.forward * newNodeDistance;
        forwardPos.y += 0.5f;
        int newID = nodes.Count;

        NavNode newNode = CreateNode(forwardPos, newID, lastNode.transform.rotation);
        AddConnectionDefinition(lastNode.nodeID, newID, true);

        Debug.Log($"[Nav] Created node {newID} ➡️ from last");
#if UNITY_EDITOR
        Selection.activeGameObject = newNode.gameObject;
#endif
    }

    public void RegisterNode(NavNode node)
    {
        if (node == null) return;

        if (!nodes.Contains(node))
        {
            node.nodeID = nodes.Count;
            nodes.Add(node);
        }

        node.parentNavSystem = this;
        RefreshGraph();

#if UNITY_EDITOR
        UpdateEditorConnectionsVisualization();
#endif
    }

    public int GetDistantNode(int fromNodeID, float minDistance = 25f)
    {
        var candidates = nodeMap.Keys
            .Where(id => id != fromNodeID && nodeMap.ContainsKey(id))
            .Where(id => Vector3.Distance(nodeMap[fromNodeID].worldPosition, nodeMap[id].worldPosition) >= minDistance)
            .ToList();

        return candidates.Count > 0 ? candidates[UnityEngine.Random.Range(0, candidates.Count)] : fromNodeID;
    }

    // 🔥 SAFE A* PATHFINDING - CRITICAL: THIS MUST RETURN A VALID PATH
    public List<int> FindPath(int start, int target)
    {
        // 🔥 SAFETY CHECKS FIRST
        if (!nodeMap.ContainsKey(start) || !nodeMap.ContainsKey(target))
        {
            Debug.LogWarning($"FindPath: Invalid start={start} or target={target}");
            return new List<int>(); // Return empty list instead of null
        }

        if (start == target)
        {
            Debug.LogWarning($"FindPath: Start and target are the same ({start})");
            return new List<int> { start }; // Return single-node path
        }

        RefreshGraph(); // Ensure fresh data

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
            {
                return ReconstructPath(cameFrom, current);
            }

            closedSet.Add(current);

            foreach (int neighbor in GetNeighbors(current))
            {
                if (closedSet.Contains(neighbor))
                    continue;

                float tentativeG = gScore[current] + GetEdgeWeight(current, neighbor);

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

        Debug.LogWarning($"FindPath: No path found from {start} to {target}. Graph may be disconnected!");
        return new List<int>(); // Return empty list if no path found
    }

    // 🔥 SAFE HEURISTIC
    private float Heuristic(int a, int b)
    {
        if (!nodeMap.ContainsKey(a) || !nodeMap.ContainsKey(b))
        {
            Debug.LogWarning($"Heuristic: Missing nodes a={a}, b={b}. Using distance 999f");
            return 999f;
        }

        Vector3 pa = nodeMap[a].worldPosition;
        Vector3 pb = nodeMap[b].worldPosition;
        return Vector3.Distance(new Vector3(pa.x, 0, pa.z), new Vector3(pb.x, 0, pb.z));
    }

    private List<int> GetNeighbors(int nodeID)
    {
        List<int> neighbors = new List<int>();

        foreach (var c in connectionDefinitions)
        {
            if (c.fromNodeID == nodeID)
            {
                neighbors.Add(c.toNodeID);
            }
            else if (c.bidirectional && c.toNodeID == nodeID)
            {
                neighbors.Add(c.fromNodeID);
            }
        }

        return neighbors.Distinct().ToList();
    }

    private float GetEdgeWeight(int from, int to) => 1f;

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

#if UNITY_EDITOR
    [ContextMenu("🎯 Create Next Node From Selected")]
    public void CreateNextNodeFromSelected()
    {
        NavNode selected = GetSelectedNode();
        if (selected == null)
        {
            Debug.LogWarning("[Nav] No NavNode selected!");
            return;
        }

        Vector3 forwardPos = selected.transform.position + selected.transform.forward * newNodeDistance;
        forwardPos.y += 0.5f;
        int newID = nodes.Count;

        NavNode newNode = CreateNode(forwardPos, newID, selected.transform.rotation);
        AddConnectionDefinition(selected.nodeID, newID, true);

        Debug.Log($"[Nav] Created node {newID} ➡️ from {selected.nodeID}");
        Selection.activeGameObject = newNode.gameObject;
    }

    private NavNode GetSelectedNode()
    {
        if (Selection.activeGameObject == null) return null;
        NavNode selected = Selection.activeGameObject.GetComponent<NavNode>();
        return selected?.parentNavSystem == this ? selected : null;
    }

    [ContextMenu("1. Collect All Nodes")]
    public void CollectAllNodes()
    {
        NavNode[] allNodes = FindObjectsOfType<NavNode>();
        nodes.Clear();
        int id = 0;

        foreach (var node in allNodes)
        {
            if (node == null) continue;
            node.parentNavSystem = this;
            node.nodeID = id++;
            if (nodesParent != null) node.transform.SetParent(nodesParent.transform);
            nodes.Add(node);
        }

        RefreshGraph();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("2. Auto Connect Nodes")]
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
        RefreshGraph();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("3. Clear All Connections")]
    public void ClearAllConnections()
    {
        connectionDefinitions.Clear();
        RefreshGraph();
        UpdateEditorConnectionsVisualization();
    }

    [ContextMenu("4. Setup Demo")]
    public void SetupDemo()
    {
        ClearAllConnections();
        nodes.Clear();

        if (nodesParent == null)
        {
            nodesParent = new GameObject("NavigationNodes");
            nodesParent.transform.SetParent(transform);
        }

        Vector3[] demoPositions = {
            new Vector3(0, 0.5f, 0), new Vector3(10, 0.5f, 0),
            new Vector3(15, 0.5f, 10), new Vector3(10, 0.5f, 20),
            new Vector3(0, 0.5f, 20)
        };

        Quaternion demoRotation = Quaternion.Euler(0, 0, 0);

        for (int i = 0; i < demoPositions.Length; i++)
        {
            GameObject nodeObj = new GameObject($"NavNode_{i}");
            nodeObj.transform.SetParent(nodesParent.transform);
            nodeObj.transform.position = demoPositions[i];
            nodeObj.transform.rotation = demoRotation;

            NavNode node = nodeObj.AddComponent<NavNode>();
            node.parentNavSystem = this;
            node.nodeID = i;
            nodes.Add(node);
        }

        AddConnectionDefinition(0, 1, true);
        AddConnectionDefinition(1, 2, true);
        AddConnectionDefinition(2, 3, true);
        AddConnectionDefinition(3, 4, true);
        AddConnectionDefinition(4, 0, true);
        AddConnectionDefinition(1, 3, true);

        RefreshGraph();
        UpdateEditorConnectionsVisualization();
        VisualizePath(new List<int> { 0, 1, 3, 4 });
    }

    [ContextMenu("5. Test Path 0->Last")]
    public void TestPathZeroToLast()
    {
        if (nodes.Count < 2) return;
        var path = FindPath(0, nodes.Count - 1);
        VisualizePath(path);
    }
#endif

    public void AddConnectionDefinition(int fromID, int toID, bool bidirectional)
    {
        AddConnection(fromID, toID, bidirectional);
        RefreshGraph();
#if UNITY_EDITOR
        UpdateEditorConnectionsVisualization();
#endif
    }

    public void AddConnection(int fromID, int toID, bool bidirectional)
    {
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

        int finalID = id == -1 ? nodes.Count : id;
        GameObject nodeObj = new GameObject($"NavNode_{finalID}");
        nodeObj.transform.SetParent(nodesParent.transform);
        nodeObj.transform.position = position;
        nodeObj.transform.rotation = rotation ?? Quaternion.identity;

        NavNode node = nodeObj.AddComponent<NavNode>();
        node.parentNavSystem = this;
        node.nodeID = finalID;
        nodes.Add(node);

        RefreshGraph();
        return node;
    }

    public void RefreshGraph()
    {
        nodeMap.Clear();
        foreach (var node in nodes)
        {
            if (node == null) continue;
            node.parentNavSystem = this;
            nodeMap[node.nodeID] = node;
        }
    }

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
            if (nodeMap.ContainsKey(path[i]))
                pathLineRenderer.SetPosition(i, nodeMap[path[i]].worldPosition + Vector3.up * 0.5f);
        }

        pathLineRenderer.enabled = true;
    }

#if UNITY_EDITOR
    private void UpdateEditorConnectionsVisualization()
    {
        if (Application.isPlaying || !visualizeAllConnectionsEditor) return;
        DrawAllConnectionsIntoLineRenderer();
    }

    private void DrawAllConnectionsIntoLineRenderer()
    {
        SetupLineRenderer();
        int posCount = connectionDefinitions.Count * 2;
        pathLineRenderer.positionCount = posCount;
        int idx = 0;

        foreach (var conn in connectionDefinitions)
        {
            if (!nodeMap.ContainsKey(conn.fromNodeID) || !nodeMap.ContainsKey(conn.toNodeID)) continue;

            Vector3 a = nodeMap[conn.fromNodeID].transform.position + Vector3.up * 0.3f;
            Vector3 b = nodeMap[conn.toNodeID].transform.position + Vector3.up * 0.3f;

            pathLineRenderer.SetPosition(idx++, a);
            pathLineRenderer.SetPosition(idx++, b);
        }
        pathLineRenderer.enabled = true;
    }
#endif

    #region NPC Spawning System

    private IEnumerator SpawnAllNPCs()
    {
        activeNPCs.Clear();
        usedNodes.Clear();

        // Get all available nodes
        List<int> availableNodes = nodeMap.Keys.ToList();

        if (availableNodes.Count == 0)
        {
            Debug.LogError("[NPCManager] ❌ No nodes in nodeMap!");
            yield break;
        }

        // Shuffle for randomness
        for (int i = 0; i < availableNodes.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, availableNodes.Count);
            int temp = availableNodes[i];
            availableNodes[i] = availableNodes[randomIndex];
            availableNodes[randomIndex] = temp;
        }

        int spawnedCount = 0;

        foreach (int spawnNodeID in availableNodes)
        {
            if (spawnedCount >= totalNPCs)
                break;

            // Skip if too close to already spawned cars
            if (IsTooCloseToExisting(spawnNodeID))
                continue;

            // Pick random destination (different from spawn node)
            HashSet<int> excludeSet = new HashSet<int> { spawnNodeID };
            int destinationNodeID = GetRandomNode(excludeSet);

            if (destinationNodeID == -1 || destinationNodeID == spawnNodeID)
            {
                if (logSpawnDetails)
                    Debug.LogWarning($"⚠️ Could not find valid destination for spawn node {spawnNodeID}");
                continue;
            }

            // Spawn car with destination
            if (SpawnNPCAtNode(spawnNodeID, destinationNodeID, spawnedCount))
            {
                spawnedCount++;
                usedNodes.Add(spawnNodeID);

                if (logSpawnDetails)
                    Debug.Log($"✅ Spawned NPC {spawnedCount}/{totalNPCs} at node {spawnNodeID} → destination {destinationNodeID}");

                yield return new WaitForSeconds(spawnStaggerDelay);
            }
        }

        Debug.Log($"[NPCManager] ✅ Total spawned: {spawnedCount}/{totalNPCs}");
    }

    private bool IsTooCloseToExisting(int nodeID)
    {
        if (!nodeMap.ContainsKey(nodeID))
            return true;

        Vector3 nodePos = nodeMap[nodeID].worldPosition;

        foreach (var npc in activeNPCs)
        {
            if (npc.transform == null) continue;

            float distance = Vector3.Distance(nodePos, npc.transform.position);
            if (distance < minNodeDistance)
                return true;
        }

        return false;
    }

    private bool SpawnNPCAtNode(int spawnNodeID, int destinationNodeID, int index)
    {
        if (!nodeMap.ContainsKey(spawnNodeID))
        {
            Debug.LogWarning($"[NPCManager] Spawn node {spawnNodeID} not in map");
            return false;
        }

        if (!nodeMap.ContainsKey(destinationNodeID))
        {
            Debug.LogWarning($"[NPCManager] Destination node {destinationNodeID} not in map");
            return false;
        }

        // Pick random prefab
        GameObject prefab = vehiclePrefabs[UnityEngine.Random.Range(0, vehiclePrefabs.Count)];
        GameObject npc = Instantiate(prefab);
        npc.name = $"NPC_Car_{index}";

        // Fix collisions
        if (autoFixCollisions)
            FixVehicleCollisions(npc);

        // Position at spawn node
        NavNode spawnNode = nodeMap[spawnNodeID];
        Vector3 spawnPos = spawnNode.worldPosition + Vector3.up * 0.3f;
        npc.transform.SetPositionAndRotation(spawnPos, spawnNode.transform.rotation);

        // Get controller
        NPCVehicleController controller = npc.GetComponent<NPCVehicleController>();

        if (controller == null)
        {
            Debug.LogError($"[NPCManager] ❌ {npc.name} missing NPCVehicleController!");
            Destroy(npc);
            return false;
        }

        // Initialize with spawn node and destination node
        controller.InitializeWithDestination(this, spawnNodeID, destinationNodeID, index);

        // Track
        activeNPCs.Add(new NPCVehicleInstance(npc.transform, controller, spawnNodeID));

        return true;
    }

    private void FixVehicleCollisions(GameObject vehicle)
    {
        // Fix all mesh colliders
        MeshCollider[] meshColliders = vehicle.GetComponentsInChildren<MeshCollider>();
        foreach (var mc in meshColliders)
        {
            if (mc.sharedMesh != null && !mc.isTrigger)
                mc.convex = true;
        }

        // Ensure rigidbody
        Rigidbody rb = vehicle.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = vehicle.AddComponent<Rigidbody>();
            rb.mass = 1500f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }

    [ContextMenu("🔄 Respawn All")]
    public void RespawnAll()
    {
        StopAllCoroutines();
        ClearAllNPCs();
        StartCoroutine(InitializeSystem());
    }

    [ContextMenu("🧹 Clear All NPCs")]
    public void ClearAllNPCs()
    {
        foreach (var npc in activeNPCs)
        {
            if (npc != null && npc.controller && npc.controller.gameObject)
                Destroy(npc.controller.gameObject);
        }

        activeNPCs.Clear();
        usedNodes.Clear();

        Debug.Log("[NPCManager] Cleared all NPCs");
    }

    [ContextMenu("📊 Show Stats")]
    public void ShowStats()
    {
        Debug.Log($"=== NPC MANAGER STATS ===");
        Debug.Log($"Total Nodes: {nodeMap.Count}");
        Debug.Log($"Active NPCs: {activeNPCs.Count}");
        Debug.Log($"Used Nodes: {usedNodes.Count}");
        Debug.Log($"Vehicle Prefabs: {vehiclePrefabs.Count}");
        Debug.Log($"Min Distance: {minNodeDistance}m");
    }

    #endregion

    #region Debug Gizmos

    private void OnDrawGizmos()
    {
        if (!showSpawnZones || !Application.isPlaying) return;

        // Draw active NPCs
        Gizmos.color = Color.green;
        foreach (var npc in activeNPCs)
        {
            if (npc != null && npc.transform != null)
            {
                Gizmos.DrawWireSphere(npc.transform.position, minNodeDistance);
            }
        }

        // Draw used nodes
        if (nodeMap != null)
        {
            Gizmos.color = Color.cyan;
            foreach (int nodeID in usedNodes)
            {
                if (nodeMap.ContainsKey(nodeID))
                {
                    Vector3 pos = nodeMap[nodeID].worldPosition;
                    Gizmos.DrawWireCube(pos + Vector3.up * 2f, Vector3.one * 1.5f);
                }
            }
        }
    }

    #endregion

    // 🔥 PRIORITY QUEUE FOR A*
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

    private class NPCVehicleInstance
    {
        public Transform transform;
        public NPCVehicleController controller;
        public int spawnNodeID;

        public NPCVehicleInstance(Transform t, NPCVehicleController c, int nodeID)
        {
            transform = t;
            controller = c;
            spawnNodeID = nodeID;
        }
    }
}