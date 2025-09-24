#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(CentralizedNavigationSystem))]
public class CentralizedNavigationSystemEditor : Editor
{
    private CentralizedNavigationSystem navSystem;
    private int testStartNodeID = -1;
    private int testEndNodeID = -1;
    private Vector2 scrollPosition;

    // Manual connection creation fields
    private int fromNodeID = 0;
    private int toNodeID = 0;
    private bool bidirectional = true;

    void OnEnable()
    {
        navSystem = (CentralizedNavigationSystem)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Hierarchy Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Collect All Nodes"))
        {
            navSystem.CollectAllNodes();
            EditorUtility.SetDirty(navSystem);
        }

        if (GUILayout.Button("Refresh Graph"))
        {
            navSystem.RefreshGraph();
        }
        EditorGUILayout.EndHorizontal();

        // NEW: Assign Parent Button
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Assign as Parent to All Nodes"))
        {
            AssignAsParentToAllNodes();
        }

        if (GUILayout.Button("Fix Null Node References"))
        {
            FixNullNodeReferences();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Graph Management", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto-Connect Nodes"))
        {
            navSystem.AutoConnectNodes();
            EditorUtility.SetDirty(navSystem);
        }

        if (GUILayout.Button("Create Node at Scene Center"))
        {
            CreateNodeAtSceneCenter();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Path Testing", EditorStyles.boldLabel);

        // Node ID input fields
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Start Node ID:", GUILayout.Width(100));
        testStartNodeID = EditorGUILayout.IntField(testStartNodeID);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("End Node ID:", GUILayout.Width(100));
        testEndNodeID = EditorGUILayout.IntField(testEndNodeID);
        EditorGUILayout.EndHorizontal();

        GUI.enabled = testStartNodeID >= 0 && testEndNodeID >= 0 &&
                     navSystem.nodeMap.ContainsKey(testStartNodeID) &&
                     navSystem.nodeMap.ContainsKey(testEndNodeID);

        if (GUILayout.Button("Test Path"))
        {
            TestPath();
        }
        GUI.enabled = true;

        if (GUILayout.Button("Clear Path Visualization"))
        {
            navSystem.ClearPath();
        }

        // Debug button to test line renderer
        if (GUILayout.Button("Test Line Renderer Visibility"))
        {
            TestLineRendererVisibility();
        }

        if (GUILayout.Button("Force URP Material Setup"))
        {
            ForceURPMaterialSetup();
        }

        // Graph Statistics
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Graph Statistics", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Total Nodes: {navSystem.nodes.Count}");
        EditorGUILayout.LabelField($"Total Connections: {navSystem.connections.Count}");
        EditorGUILayout.LabelField($"Bidirectional Connections: {navSystem.connections.Count(c => c.bidirectional)}");

        // NEW: Parent assignment statistics
        int nodesWithParent = navSystem.nodes.Count(n => n != null && n.parentNavSystem == navSystem);
        int nodesWithoutParent = navSystem.nodes.Count(n => n != null && n.parentNavSystem != navSystem);
        EditorGUILayout.LabelField($"Nodes with correct parent: {nodesWithParent}");
        if (nodesWithoutParent > 0)
        {
            EditorGUILayout.LabelField($"Nodes without correct parent: {nodesWithoutParent}", EditorStyles.helpBox);
        }

        // Node List with connections
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Node Overview", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));

        foreach (var node in navSystem.nodes)
        {
            if (node == null) continue;

            EditorGUILayout.BeginHorizontal("box");

            // Node info
            EditorGUILayout.LabelField($"Node {node.nodeID}", GUILayout.Width(80));
            EditorGUILayout.ObjectField(node, typeof(NavNode), true, GUILayout.Width(150));

            // Connection count
            int connectionCount = navSystem.connections.Count(c =>
                c.fromNodeID == node.nodeID || (c.bidirectional && c.toNodeID == node.nodeID));
            EditorGUILayout.LabelField($"Connections: {connectionCount}", GUILayout.Width(100));

            // Parent check
            bool isChild = node.transform.parent == navSystem.nodesParent;
            bool hasCorrectParentRef = node.parentNavSystem == navSystem;

            string parentStatus = "";
            if (isChild && hasCorrectParentRef) parentStatus = "✓✓"; // Both hierarchy and reference correct
            else if (isChild && !hasCorrectParentRef) parentStatus = "✓✗"; // Hierarchy correct, reference wrong
            else if (!isChild && hasCorrectParentRef) parentStatus = "✗✓"; // Hierarchy wrong, reference correct
            else parentStatus = "✗✗"; // Both wrong

            EditorGUILayout.LabelField(parentStatus, GUILayout.Width(30));

            // Quick actions
            if (GUILayout.Button("Focus", GUILayout.Width(50)))
            {
                Selection.activeGameObject = node.gameObject;
                SceneView.FrameLastActiveSceneView();
            }

            if (GUILayout.Button("Set Start", GUILayout.Width(60)))
            {
                testStartNodeID = node.nodeID;
            }

            if (GUILayout.Button("Set End", GUILayout.Width(60)))
            {
                testEndNodeID = node.nodeID;
            }

            // NEW: Individual assign parent button
            if (!hasCorrectParentRef && GUILayout.Button("Fix Parent", GUILayout.Width(70)))
            {
                AssignParentToNode(node);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();

        // Connection Management
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Connection Management", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Recalculate All Weights"))
        {
            RecalculateAllWeights();
        }

        if (GUILayout.Button("Clear All Connections"))
        {
            if (EditorUtility.DisplayDialog("Clear Connections",
                "Are you sure you want to clear all connections?", "Yes", "No"))
            {
                navSystem.connections.Clear();
                navSystem.RefreshGraph();
                EditorUtility.SetDirty(navSystem);
            }
        }
        EditorGUILayout.EndHorizontal();

        // Manual connection creation
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Manual Connection Creation", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        fromNodeID = EditorGUILayout.IntField("From Node ID:", fromNodeID);
        toNodeID = EditorGUILayout.IntField("To Node ID:", toNodeID);
        EditorGUILayout.EndHorizontal();

        bidirectional = EditorGUILayout.Toggle("Bidirectional:", bidirectional);

        GUI.enabled = navSystem.nodeMap.ContainsKey(fromNodeID) && navSystem.nodeMap.ContainsKey(toNodeID) && fromNodeID != toNodeID;
        if (GUILayout.Button("Add Connection"))
        {
            navSystem.AddConnection(fromNodeID, toNodeID, bidirectional);
            EditorUtility.SetDirty(navSystem);
        }
        GUI.enabled = true;
    }

    /// <summary>
    /// NEW: Assigns this CentralizedNavigationSystem as parent to all nodes in the nodes array
    /// </summary>
    void AssignAsParentToAllNodes()
    {
        if (navSystem.nodes == null || navSystem.nodes.Count == 0)
        {
            EditorUtility.DisplayDialog("No Nodes", "No nodes found in the nodes array. Use 'Collect All Nodes' first.", "OK");
            return;
        }

        int assignedCount = 0;
        int alreadyAssignedCount = 0;
        int nullNodeCount = 0;

        foreach (var node in navSystem.nodes)
        {
            if (node == null)
            {
                nullNodeCount++;
                continue;
            }

            if (node.parentNavSystem == navSystem)
            {
                alreadyAssignedCount++;
                continue;
            }

            // Assign this navigation system as parent
            node.parentNavSystem = navSystem;
            assignedCount++;

            // Mark the node as dirty to save the change
            EditorUtility.SetDirty(node);
        }

        // Clean up null references
        if (nullNodeCount > 0)
        {
            FixNullNodeReferences();
        }

        // Mark the navigation system as dirty
        EditorUtility.SetDirty(navSystem);

        // Show results
        string message = $"Parent assignment completed:\n";
        message += $"• Newly assigned: {assignedCount}\n";
        message += $"• Already assigned: {alreadyAssignedCount}\n";
        if (nullNodeCount > 0)
        {
            message += $"• Null references removed: {nullNodeCount}\n";
        }

        Debug.Log($"[CentralizedNavigationSystem] {message}");

        if (assignedCount > 0 || nullNodeCount > 0)
        {
            EditorUtility.DisplayDialog("Parent Assignment Complete", message, "OK");
        }
        else if (alreadyAssignedCount > 0)
        {
            EditorUtility.DisplayDialog("Already Assigned", $"All {alreadyAssignedCount} nodes already have correct parent assignment.", "OK");
        }
    }

    /// <summary>
    /// NEW: Assigns parent to a specific node
    /// </summary>
    void AssignParentToNode(NavNode node)
    {
        if (node == null) return;

        node.parentNavSystem = navSystem;
        EditorUtility.SetDirty(node);
        EditorUtility.SetDirty(navSystem);

        Debug.Log($"[CentralizedNavigationSystem] Assigned parent to Node {node.nodeID}");
    }

    /// <summary>
    /// NEW: Removes null node references from the nodes array
    /// </summary>
    void FixNullNodeReferences()
    {
        if (navSystem.nodes == null) return;

        int originalCount = navSystem.nodes.Count;
        navSystem.nodes.RemoveAll(n => n == null);
        int removedCount = originalCount - navSystem.nodes.Count;

        if (removedCount > 0)
        {
            EditorUtility.SetDirty(navSystem);
            Debug.Log($"[CentralizedNavigationSystem] Removed {removedCount} null node references");
        }
    }

    void CreateNodeAtSceneCenter()
    {
        Vector3 position = Vector3.zero;

        // Position at scene view center
        if (SceneView.lastActiveSceneView != null)
        {
            position = SceneView.lastActiveSceneView.pivot;
        }

        NavNode createdNode = navSystem.CreateNode(position);

        Selection.activeGameObject = createdNode.gameObject;
        EditorGUIUtility.PingObject(createdNode.gameObject);
        EditorUtility.SetDirty(navSystem);
    }

    void TestPath()
    {
        if (navSystem.nodeMap.ContainsKey(testStartNodeID) && navSystem.nodeMap.ContainsKey(testEndNodeID))
        {
            List<int> pathIDs = navSystem.FindPath(testStartNodeID, testEndNodeID);

            if (pathIDs.Count > 0)
            {
                navSystem.VisualizePath(pathIDs);
                Debug.Log($"Path found with {pathIDs.Count} nodes: [{string.Join(" → ", pathIDs)}]");

                // Focus scene view on path
                if (SceneView.lastActiveSceneView != null)
                {
                    Vector3 center = Vector3.zero;
                    foreach (int nodeID in pathIDs)
                    {
                        center += navSystem.nodeMap[nodeID].transform.position;
                    }
                    center /= pathIDs.Count;

                    SceneView.lastActiveSceneView.pivot = center;
                    SceneView.lastActiveSceneView.Repaint();
                }
            }
            else
            {
                Debug.LogWarning($"No path found between Node {testStartNodeID} and Node {testEndNodeID}");
            }
        }
    }

    void RecalculateAllWeights()
    {
        int updatedConnections = 0;

        for (int i = 0; i < navSystem.connections.Count; i++)
        {
            var connection = navSystem.connections[i];

            // Check if both nodes exist
            if (navSystem.nodeMap.ContainsKey(connection.fromNodeID) &&
                navSystem.nodeMap.ContainsKey(connection.toNodeID) &&
                navSystem.nodeMap[connection.fromNodeID] != null &&
                navSystem.nodeMap[connection.toNodeID] != null)
            {
                // Calculate new weight based on current positions
                Vector3 fromPos = navSystem.nodeMap[connection.fromNodeID].transform.position;
                Vector3 toPos = navSystem.nodeMap[connection.toNodeID].transform.position;
                float newWeight = Vector3.Distance(fromPos, toPos);

                // Update the connection weight
                connection.weight = newWeight;
                updatedConnections++;
            }
        }

        // Refresh the graph with updated weights
        navSystem.RefreshGraph();
        EditorUtility.SetDirty(navSystem);

        Debug.Log($"Recalculated weights for {updatedConnections} connections based on current node positions");
    }

    void TestLineRendererVisibility()
    {
        if (navSystem.pathLineRenderer == null)
        {
            Debug.LogWarning("No Line Renderer found - will be created at runtime");
            return;
        }

        // Create a simple test path
        if (navSystem.nodes.Count >= 2)
        {
            List<Vector3> testPositions = new List<Vector3>();
            for (int i = 0; i < Mathf.Min(3, navSystem.nodes.Count); i++)
            {
                if (navSystem.nodes[i] != null)
                {
                    Vector3 pos = navSystem.nodes[i].transform.position;
                    pos.y += 0.5f; // Raise above ground
                    testPositions.Add(pos);
                }
            }

            navSystem.pathLineRenderer.positionCount = testPositions.Count;
            navSystem.pathLineRenderer.SetPositions(testPositions.ToArray());

            Debug.Log($"Line Renderer test: {testPositions.Count} positions set. Check scene view!");
        }
        else
        {
            Debug.LogWarning("Need at least 2 nodes to test Line Renderer");
        }
    }

    void ForceURPMaterialSetup()
    {
        if (navSystem.pathLineRenderer == null)
        {
            Debug.LogWarning("No Line Renderer found - will be created at runtime");
            return;
        }

        // Create URP-specific material
        Shader urpShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (urpShader == null)
        {
            Debug.LogError("URP Unlit shader not found! Make sure you're using URP.");
            return;
        }

        Material urpMaterial = new Material(urpShader);
        urpMaterial.SetColor("_BaseColor", Color.red);
        urpMaterial.name = "URP_PathMaterial";

        navSystem.pathLineRenderer.material = urpMaterial;
        navSystem.pathLineRenderer.startColor = Color.red;
        navSystem.pathLineRenderer.endColor = Color.red;
        navSystem.pathLineRenderer.startWidth = 2.0f;
        navSystem.pathLineRenderer.endWidth = 2.0f;

        EditorUtility.SetDirty(navSystem);
        Debug.Log("Forced URP material setup with thick red line. Test pathfinding now!");
    }
}
#endif