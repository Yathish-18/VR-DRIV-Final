//using UnityEngine;
//using System.Collections.Generic;
//using System.Linq;
//using System.Collections;

//public class NPCVehicleManager : MonoBehaviour
//{
//    [Header("=== NPC SPAWN SETTINGS ===")]
//    [SerializeField] private List<GameObject> vehiclePrefabs = new List<GameObject>();
//    [SerializeField][Range(5, 100)] private int totalNPCs = 20;

//    [Header("=== NAVIGATION ===")]
//    [SerializeField] private CentralizedNavigationSystem navSystem;

//    [Header("=== SPAWN SETTINGS ===")]
//    [SerializeField] private bool autoCollectNodesOnStart = true;
//    [SerializeField] private bool autoFixCollisions = true;
//    [SerializeField] private float spawnStaggerDelay = 0.2f;
//    [SerializeField] private float minNodeDistance = 5f; // Simple distance between cars

//    [Header("=== DEBUG ===")]
//    [SerializeField] private bool showSpawnZones = true;
//    [SerializeField] private bool logSpawnDetails = true;

//    private List<NPCVehicleInstance> activeNPCs = new List<NPCVehicleInstance>();
//    private HashSet<int> usedNodes = new HashSet<int>();

//    #region Initialization

//    private void Awake()
//    {
//        StartCoroutine(InitializeSystem());
//    }

//    private IEnumerator InitializeSystem()
//    {
//        yield return new WaitForSeconds(1f);

//        // Find nav system
//        if (navSystem == null)
//            navSystem = FindFirstObjectByType<CentralizedNavigationSystem>();

//        if (navSystem == null)
//        {
//            Debug.LogError("[NPCManager] ❌ No CentralizedNavigationSystem found!");
//            yield break;
//        }

//        if (vehiclePrefabs.Count == 0)
//        {
//            Debug.LogError("[NPCManager] ❌ No vehicle prefabs assigned!");
//            yield break;
//        }

//        // Setup nav graph
//        navSystem.RefreshGraph();

//        if (navSystem.nodes.Count == 0 && autoCollectNodesOnStart)
//        {
//            Debug.Log("[NPCManager] 🔍 Collecting nodes...");
//            navSystem.CollectAllNodes();
//            yield return new WaitForSeconds(0.5f);
//            navSystem.RefreshGraph();
//        }

//        if (navSystem.nodeMap.Count == 0)
//        {
//            Debug.LogError("[NPCManager] ❌ No nodes found! Right-click CentralizedNavigationSystem → Collect All Nodes");
//            yield break;
//        }

//        Debug.Log($"[NPCManager] 🚗 Found {navSystem.nodeMap.Count} nodes, spawning {totalNPCs} NPCs...");

//        // SIMPLE SPAWN
//        yield return StartCoroutine(SpawnAllNPCs());

//        Debug.Log($"✅ [NPCManager] Spawned {activeNPCs.Count}/{totalNPCs} NPCs successfully!");
//    }

//    #endregion

//    #region Simple Spawning

//    private IEnumerator SpawnAllNPCs()
//    {
//        activeNPCs.Clear();
//        usedNodes.Clear();

//        // Get all available nodes
//        List<int> availableNodes = navSystem.nodeMap.Keys.ToList();

//        if (availableNodes.Count == 0)
//        {
//            Debug.LogError("[NPCManager] ❌ No nodes in nodeMap!");
//            yield break;
//        }

//        // Calculate step to distribute evenly
//        int step = Mathf.Max(1, availableNodes.Count / totalNPCs);
//        int spawnedCount = 0;

//        for (int i = 0; i < availableNodes.Count && spawnedCount < totalNPCs; i += step)
//        {
//            int nodeID = availableNodes[i];

//            // Skip if too close to already spawned cars
//            if (IsTooCloseToExisting(nodeID))
//                continue;

//            // Spawn car
//            if (SpawnNPCAtNode(nodeID, spawnedCount))
//            {
//                spawnedCount++;
//                usedNodes.Add(nodeID);

//                if (logSpawnDetails)
//                    Debug.Log($"✅ Spawned NPC {spawnedCount}/{totalNPCs} at node {nodeID}");

//                yield return new WaitForSeconds(spawnStaggerDelay);
//            }
//        }

//        // If we still need more cars, fill with random nodes
//        if (spawnedCount < totalNPCs)
//        {
//            Debug.Log($"[NPCManager] Filling remaining {totalNPCs - spawnedCount} spots with random nodes...");

//            List<int> remainingNodes = availableNodes.Where(n => !usedNodes.Contains(n)).ToList();

//            foreach (int nodeID in remainingNodes)
//            {
//                if (spawnedCount >= totalNPCs) break;

//                if (!IsTooCloseToExisting(nodeID) && SpawnNPCAtNode(nodeID, spawnedCount))
//                {
//                    spawnedCount++;
//                    usedNodes.Add(nodeID);

//                    if (logSpawnDetails)
//                        Debug.Log($"✅ Spawned extra NPC {spawnedCount}/{totalNPCs} at node {nodeID}");

//                    yield return new WaitForSeconds(spawnStaggerDelay);
//                }
//            }
//        }
//    }

//    private bool IsTooCloseToExisting(int nodeID)
//    {
//        if (!navSystem.nodeMap.ContainsKey(nodeID))
//            return true;

//        Vector3 nodePos = navSystem.nodeMap[nodeID].worldPosition;

//        foreach (var npc in activeNPCs)
//        {
//            if (npc.transform == null) continue;

//            float distance = Vector3.Distance(nodePos, npc.transform.position);
//            if (distance < minNodeDistance)
//                return true;
//        }

//        return false;
//    }

//    private bool SpawnNPCAtNode(int nodeID, int index)
//    {
//        if (!navSystem.nodeMap.ContainsKey(nodeID))
//        {
//            Debug.LogWarning($"[NPCManager] Node {nodeID} not in map");
//            return false;
//        }

//        // Pick random prefab
//        GameObject prefab = vehiclePrefabs[Random.Range(0, vehiclePrefabs.Count)];
//        GameObject npc = Instantiate(prefab);
//        npc.name = $"NPC_Car_{index}";

//        // Fix collisions
//        if (autoFixCollisions)
//            FixVehicleCollisions(npc);

//        // Position at node
//        NavNode node = navSystem.nodeMap[nodeID];
//        Vector3 spawnPos = node.worldPosition + Vector3.up * 0.3f;
//        npc.transform.SetPositionAndRotation(spawnPos, node.transform.rotation);

//        // Get controller
//        NPCVehicleController controller = npc.GetComponent<NPCVehicleController>();

//        if (controller == null)
//        {
//            Debug.LogError($"[NPCManager] ❌ {npc.name} missing NPCVehicleController!");
//            Destroy(npc);
//            return false;
//        }

//        // Initialize
//        controller.InitializePermanentNPC(this, navSystem, nodeID, index);

//        // Track
//        activeNPCs.Add(new NPCVehicleInstance(npc.transform, controller, nodeID));

//        return true;
//    }

//    #endregion

//    #region Collision Fixes

//    private void FixVehicleCollisions(GameObject vehicle)
//    {
//        // Fix all mesh colliders
//        MeshCollider[] meshColliders = vehicle.GetComponentsInChildren<MeshCollider>();
//        foreach (var mc in meshColliders)
//        {
//            if (mc.sharedMesh != null && !mc.isTrigger)
//                mc.convex = true;
//        }

//        // Ensure rigidbody
//        Rigidbody rb = vehicle.GetComponent<Rigidbody>();
//        if (rb == null)
//        {
//            rb = vehicle.AddComponent<Rigidbody>();
//            rb.mass = 1500f;
//            rb.linearDamping = 0.5f;
//            rb.angularDamping = 5f;
//            rb.interpolation = RigidbodyInterpolation.Interpolate;
//            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
//            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
//        }
//    }

//    #endregion

//    #region Public Methods

//    [ContextMenu("🔄 Respawn All")]
//    public void RespawnAll()
//    {
//        StopAllCoroutines();
//        ClearAllNPCs();
//        StartCoroutine(InitializeSystem());
//    }

//    [ContextMenu("🧹 Clear All NPCs")]
//    public void ClearAllNPCs()
//    {
//        foreach (var npc in activeNPCs)
//        {
//            if (npc != null && npc.controller && npc.controller.gameObject)
//                Destroy(npc.controller.gameObject);
//        }

//        activeNPCs.Clear();
//        usedNodes.Clear();

//        Debug.Log("[NPCManager] Cleared all NPCs");
//    }

//    [ContextMenu("📊 Show Stats")]
//    public void ShowStats()
//    {
//        Debug.Log($"=== NPC MANAGER STATS ===");
//        Debug.Log($"Total Nodes: {navSystem.nodeMap.Count}");
//        Debug.Log($"Active NPCs: {activeNPCs.Count}");
//        Debug.Log($"Used Nodes: {usedNodes.Count}");
//        Debug.Log($"Vehicle Prefabs: {vehiclePrefabs.Count}");
//        Debug.Log($"Min Distance: {minNodeDistance}m");
//    }

//    #endregion

//    #region Debug

//    private void OnDrawGizmos()
//    {
//        if (!showSpawnZones || navSystem == null || !Application.isPlaying) return;

//        // Draw active NPCs
//        Gizmos.color = Color.green;
//        foreach (var npc in activeNPCs)
//        {
//            if (npc != null && npc.transform != null)
//            {
//                Gizmos.DrawWireSphere(npc.transform.position, minNodeDistance);
//            }
//        }

//        // Draw used nodes
//        if (navSystem.nodeMap != null)
//        {
//            Gizmos.color = Color.cyan;
//            foreach (int nodeID in usedNodes)
//            {
//                if (navSystem.nodeMap.ContainsKey(nodeID))
//                {
//                    Vector3 pos = navSystem.nodeMap[nodeID].worldPosition;
//                    Gizmos.DrawWireCube(pos + Vector3.up * 2f, Vector3.one * 1.5f);
//                }
//            }
//        }
//    }

//    #endregion

//    private class NPCVehicleInstance
//    {
//        public Transform transform;
//        public NPCVehicleController controller;
//        public int spawnNodeID;

//        public NPCVehicleInstance(Transform t, NPCVehicleController c, int nodeID)
//        {
//            transform = t;
//            controller = c;
//            spawnNodeID = nodeID;
//        }
//    }
//}
