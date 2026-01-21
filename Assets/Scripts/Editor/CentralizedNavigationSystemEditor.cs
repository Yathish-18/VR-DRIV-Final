#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(CentralizedNavigationSystem))]
public class CentralizedNavigationSystemEditor : Editor
{
    private int searchNodeID = 0;
    private Vector2 connectionsScrollPosition;
    private Vector2 allConnectionsScrollPosition;
    private bool showNodeSearchPanel = true;
    private bool showAllConnectionsPanel = false;
    private List<ConnectionDefinition> connectionsToDelete = new List<ConnectionDefinition>();

    public override void OnInspectorGUI()
    {
        CentralizedNavigationSystem nav = (CentralizedNavigationSystem)target;
        serializedObject.Update();

        // ========================================
        // CONNECTION VALIDATOR (NEW)
        // ========================================
        GUILayout.Space(10);
        DrawConnectionValidator(nav);

        // ========================================
        // NODE SEARCH & CONNECTION MANAGER
        // ========================================
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.7f, 0.9f, 1f);
        showNodeSearchPanel = EditorGUILayout.BeginFoldoutHeaderGroup(showNodeSearchPanel, "🔍 SEARCH NODE CONNECTIONS");
        GUI.backgroundColor = Color.white;

        if (showNodeSearchPanel)
        {
            DrawNodeSearchPanel(nav);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();

        // ========================================
        // ALL CONNECTIONS VIEWER
        // ========================================
        GUILayout.Space(10);
        GUI.backgroundColor = new Color(1f, 0.9f, 0.7f);
        showAllConnectionsPanel = EditorGUILayout.BeginFoldoutHeaderGroup(showAllConnectionsPanel, "📋 VIEW ALL CONNECTIONS");
        GUI.backgroundColor = Color.white;

        if (showAllConnectionsPanel)
        {
            DrawAllConnectionsPanel(nav);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();

        // ========================================
        // ORIGINAL BUTTONS
        // ========================================
        GUILayout.Space(10);
        EditorGUILayout.LabelField("🚗 NODE CREATION", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🆕 Next Node (SELECTED)", GUILayout.Height(35)))
            nav.CreateNextNodeFromSelected();
        GUILayout.Label("Select NavNode first!", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("➡️ Forward (LAST NODE)", GUILayout.Height(35)))
            nav.CreateNodeForward();

        GUILayout.Space(10);
        EditorGUILayout.LabelField("🔧 GRAPH TOOLS", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("🔗 Auto Connect", GUILayout.Height(30))) nav.AutoConnectNodes();
        if (GUILayout.Button("🧹 Clear", GUILayout.Height(30))) nav.ClearAllConnections();
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("🎮 Setup Demo (5 nodes)", GUILayout.Height(30)))
            nav.SetupDemo();

        GUILayout.Space(5);
        if (GUILayout.Button("🎯 Test Path", GUILayout.Height(25)))
            nav.TestPathZeroToLast();

        // ========================================
        // DEFAULT INSPECTOR
        // ========================================
        GUILayout.Space(10);
        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();
    }

    // ========================================
    // CONNECTION VALIDATOR & CLEANER
    // ========================================
    private void DrawConnectionValidator(CentralizedNavigationSystem nav)
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        // Find broken connections
        List<ConnectionDefinition> brokenConnections = new List<ConnectionDefinition>();
        List<ConnectionDefinition> duplicateConnections = new List<ConnectionDefinition>();
        
        foreach (var conn in nav.connectionDefinitions)
        {
            // Check if nodes exist
            bool fromExists = nav.nodeMap.ContainsKey(conn.fromNodeID);
            bool toExists = nav.nodeMap.ContainsKey(conn.toNodeID);
            
            if (!fromExists || !toExists)
            {
                brokenConnections.Add(conn);
            }
        }

        // Find duplicates (A->B and B->A when both are bidirectional)
        for (int i = 0; i < nav.connectionDefinitions.Count; i++)
        {
            var conn1 = nav.connectionDefinitions[i];
            for (int j = i + 1; j < nav.connectionDefinitions.Count; j++)
            {
                var conn2 = nav.connectionDefinitions[j];
                
                // Check for exact duplicates or reversed duplicates
                if ((conn1.fromNodeID == conn2.fromNodeID && conn1.toNodeID == conn2.toNodeID) ||
                    (conn1.fromNodeID == conn2.toNodeID && conn1.toNodeID == conn2.fromNodeID))
                {
                    if (!duplicateConnections.Contains(conn2))
                        duplicateConnections.Add(conn2);
                }
            }
        }

        bool hasIssues = brokenConnections.Count > 0 || duplicateConnections.Count > 0;

        if (hasIssues)
        {
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f, 0.3f);
        }
        else
        {
            GUI.backgroundColor = new Color(0.3f, 1f, 0.3f, 0.3f);
        }

        EditorGUILayout.BeginVertical(boxStyle);

        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 12;
        
        if (hasIssues)
        {
            headerStyle.normal.textColor = Color.red;
            EditorGUILayout.LabelField("⚠️ CONNECTION VALIDATOR - ISSUES FOUND!", headerStyle);
        }
        else
        {
            headerStyle.normal.textColor = Color.green;
            EditorGUILayout.LabelField("✅ CONNECTION VALIDATOR - ALL GOOD", headerStyle);
        }

        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Total Connections: {nav.connectionDefinitions.Count}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Valid Nodes: {nav.nodeMap.Count}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();

        if (brokenConnections.Count > 0)
        {
            GUILayout.Space(5);
            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"❌ BROKEN CONNECTIONS: {brokenConnections.Count}", EditorStyles.boldLabel);
            
            foreach (var conn in brokenConnections.Take(5)) // Show first 5
            {
                bool fromExists = nav.nodeMap.ContainsKey(conn.fromNodeID);
                bool toExists = nav.nodeMap.ContainsKey(conn.toNodeID);
                
                string fromStatus = fromExists ? $"✓ {conn.fromNodeID}" : $"❌ {conn.fromNodeID} (MISSING)";
                string toStatus = toExists ? $"✓ {conn.toNodeID}" : $"❌ {conn.toNodeID} (MISSING)";
                string arrow = conn.bidirectional ? "⟷" : "→";
                
                EditorGUILayout.LabelField($"  {fromStatus} {arrow} {toStatus}", EditorStyles.miniLabel);
            }
            
            if (brokenConnections.Count > 5)
            {
                EditorGUILayout.LabelField($"  ... and {brokenConnections.Count - 5} more", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);
            GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
            if (GUILayout.Button($"🗑️ DELETE {brokenConnections.Count} BROKEN CONNECTIONS", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog(
                    "Delete Broken Connections",
                    $"This will delete {brokenConnections.Count} connections that reference non-existent nodes.\n\nContinue?",
                    "Yes, Delete",
                    "Cancel"))
                {
                    Undo.RecordObject(nav, "Delete Broken Connections");
                    foreach (var conn in brokenConnections)
                    {
                        nav.connectionDefinitions.Remove(conn);
                    }
                    nav.RefreshGraph();
                    EditorUtility.SetDirty(nav);
                    Debug.Log($"[NavSystem] Deleted {brokenConnections.Count} broken connections");
                }
            }
            GUI.backgroundColor = Color.white;
        }

        if (duplicateConnections.Count > 0)
        {
            GUILayout.Space(5);
            GUI.backgroundColor = new Color(1f, 0.9f, 0.5f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"⚠️ DUPLICATE CONNECTIONS: {duplicateConnections.Count}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  (Same connection defined multiple times)", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(5);
            GUI.backgroundColor = new Color(1f, 0.7f, 0.3f);
            if (GUILayout.Button($"🧹 REMOVE {duplicateConnections.Count} DUPLICATES", GUILayout.Height(30)))
            {
                Undo.RecordObject(nav, "Remove Duplicate Connections");
                foreach (var conn in duplicateConnections)
                {
                    nav.connectionDefinitions.Remove(conn);
                }
                nav.RefreshGraph();
                EditorUtility.SetDirty(nav);
                Debug.Log($"[NavSystem] Removed {duplicateConnections.Count} duplicate connections");
            }
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button("🔍 VALIDATE & REBUILD GRAPH", GUILayout.Height(25)))
        {
            Undo.RecordObject(nav, "Validate Graph");
            nav.ValidateAndRebuildGraph();
            EditorUtility.SetDirty(nav);
            Debug.Log("[NavSystem] Graph validated and rebuilt");
        }

        if (GUILayout.Button("📊 DEBUG PRINT CONNECTIONS", GUILayout.Height(25)))
        {
            nav.DebugPrintAllConnections();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
    }

    // ========================================
    // NODE SEARCH PANEL
    // ========================================
    private void DrawNodeSearchPanel(CentralizedNavigationSystem nav)
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        EditorGUILayout.BeginVertical(boxStyle);

        // Search input
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Search Node ID:", GUILayout.Width(100));
        searchNodeID = EditorGUILayout.IntField(searchNodeID, GUILayout.Width(80));

        if (GUILayout.Button("🔍 Search", GUILayout.Width(80)))
        {
            // Force refresh
            Repaint();
        }

        if (GUILayout.Button("🎯 Focus Node", GUILayout.Width(100)))
        {
            FocusOnNode(nav, searchNodeID);
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(5);

        // Check if node exists
        bool nodeExists = nav.nodeMap.ContainsKey(searchNodeID);

        if (!nodeExists)
        {
            EditorGUILayout.HelpBox($"❌ Node ID {searchNodeID} does not exist!", MessageType.Warning);
        }
        else
        {
            NavNode node = nav.nodeMap[searchNodeID];

            // Node info
            GUI.backgroundColor = new Color(0.5f, 1f, 0.5f, 0.3f);
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"✅ Node ID {searchNodeID} Found", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Name: {node.name}");
            EditorGUILayout.LabelField($"Position: {node.transform.position}");
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;

            GUILayout.Space(10);

            // Find all connections for this node
            List<ConnectionDefinition> outgoingConnections = new List<ConnectionDefinition>();
            List<ConnectionDefinition> incomingConnections = new List<ConnectionDefinition>();

            foreach (var conn in nav.connectionDefinitions)
            {
                if (conn.fromNodeID == searchNodeID)
                {
                    outgoingConnections.Add(conn);
                }
                if (conn.toNodeID == searchNodeID)
                {
                    incomingConnections.Add(conn);
                }
            }

            int totalConnections = outgoingConnections.Count + incomingConnections.Count;

            // Connection summary
            EditorGUILayout.LabelField($"🔗 Total Connections: {totalConnections}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"   ➡️ Outgoing: {outgoingConnections.Count}");
            EditorGUILayout.LabelField($"   ⬅️ Incoming: {incomingConnections.Count}");

            GUILayout.Space(5);

            // Scrollable connections list
            connectionsScrollPosition = EditorGUILayout.BeginScrollView(connectionsScrollPosition, GUILayout.MaxHeight(300));

            // OUTGOING CONNECTIONS
            if (outgoingConnections.Count > 0)
            {
                EditorGUILayout.LabelField("➡️ OUTGOING CONNECTIONS", EditorStyles.boldLabel);
                GUILayout.Space(3);

                foreach (var conn in outgoingConnections)
                {
                    DrawConnectionRow(nav, conn, searchNodeID, true);
                }

                GUILayout.Space(10);
            }

            // INCOMING CONNECTIONS
            if (incomingConnections.Count > 0)
            {
                EditorGUILayout.LabelField("⬅️ INCOMING CONNECTIONS", EditorStyles.boldLabel);
                GUILayout.Space(3);

                foreach (var conn in incomingConnections)
                {
                    DrawConnectionRow(nav, conn, searchNodeID, false);
                }
            }

            if (totalConnections == 0)
            {
                EditorGUILayout.HelpBox("This node has no connections.", MessageType.Info);
            }

            EditorGUILayout.EndScrollView();

            // Bulk actions
            if (totalConnections > 0)
            {
                GUILayout.Space(10);
                GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);
                if (GUILayout.Button($"🗑️ DELETE ALL CONNECTIONS FOR NODE {searchNodeID}", GUILayout.Height(35)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Delete All Connections",
                        $"Are you sure you want to delete all {totalConnections} connections for Node {searchNodeID}?",
                        "Yes, Delete All",
                        "Cancel"))
                    {
                        DeleteAllConnectionsForNode(nav, searchNodeID);
                    }
                }
                GUI.backgroundColor = Color.white;
            }
        }

        EditorGUILayout.EndVertical();
    }

    // ========================================
    // DRAW CONNECTION ROW
    // ========================================
    private void DrawConnectionRow(CentralizedNavigationSystem nav, ConnectionDefinition conn, int currentNodeID, bool isOutgoing)
    {
        GUIStyle rowStyle = new GUIStyle(GUI.skin.box);
        rowStyle.padding = new RectOffset(5, 5, 5, 5);

        Color bgColor = conn.bidirectional ? new Color(0.5f, 1f, 0.5f, 0.2f) : new Color(1f, 0.7f, 0.5f, 0.2f);
        GUI.backgroundColor = bgColor;

        EditorGUILayout.BeginVertical(rowStyle);
        EditorGUILayout.BeginHorizontal();

        // Connection info
        int otherNodeID = isOutgoing ? conn.toNodeID : conn.fromNodeID;
        string otherNodeName = nav.nodeMap.ContainsKey(otherNodeID) ? nav.nodeMap[otherNodeID].name : "INVALID";

        string arrow = conn.bidirectional ? "⟷" : (isOutgoing ? "→" : "←");
        string direction = conn.bidirectional ? "Bidirectional" : "One-way";

        EditorGUILayout.LabelField($"{arrow}", GUILayout.Width(30));
        EditorGUILayout.LabelField($"Node {otherNodeID}", GUILayout.Width(70));
        EditorGUILayout.LabelField($"({otherNodeName})", GUILayout.Width(120));
        EditorGUILayout.LabelField($"[{direction}]", GUILayout.Width(100));

        GUILayout.FlexibleSpace();

        // Focus button
        if (GUILayout.Button("👁️", GUILayout.Width(30), GUILayout.Height(20)))
        {
            FocusOnNode(nav, otherNodeID);
        }

        // Delete button
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("🗑️", GUILayout.Width(30), GUILayout.Height(20)))
        {
            if (EditorUtility.DisplayDialog(
                "Delete Connection",
                $"Delete connection: {conn.fromNodeID} → {conn.toNodeID} ({direction})?",
                "Delete",
                "Cancel"))
            {
                DeleteConnection(nav, conn);
            }
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();

        GUI.backgroundColor = Color.white;
        GUILayout.Space(2);
    }

    // ========================================
    // ALL CONNECTIONS PANEL
    // ========================================
    private void DrawAllConnectionsPanel(CentralizedNavigationSystem nav)
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.LabelField($"Total Connections: {nav.connectionDefinitions.Count}", EditorStyles.boldLabel);
        GUILayout.Space(5);

        if (nav.connectionDefinitions.Count == 0)
        {
            EditorGUILayout.HelpBox("No connections exist.", MessageType.Info);
        }
        else
        {
            allConnectionsScrollPosition = EditorGUILayout.BeginScrollView(allConnectionsScrollPosition, GUILayout.MaxHeight(400));

            for (int i = 0; i < nav.connectionDefinitions.Count; i++)
            {
                var conn = nav.connectionDefinitions[i];

                GUIStyle connStyle = new GUIStyle(GUI.skin.box);
                connStyle.padding = new RectOffset(5, 5, 5, 5);

                Color bgColor = conn.bidirectional ? new Color(0.5f, 1f, 0.5f, 0.2f) : new Color(1f, 0.7f, 0.5f, 0.2f);
                GUI.backgroundColor = bgColor;

                EditorGUILayout.BeginVertical(connStyle);
                EditorGUILayout.BeginHorizontal();

                // Validate nodes
                bool fromExists = nav.nodeMap.ContainsKey(conn.fromNodeID);
                bool toExists = nav.nodeMap.ContainsKey(conn.toNodeID);
                bool isValid = fromExists && toExists;

                string fromName = fromExists ? nav.nodeMap[conn.fromNodeID].name : "INVALID";
                string toName = toExists ? nav.nodeMap[conn.toNodeID].name : "INVALID";

                string status = isValid ? "✓" : "❌";
                string arrow = conn.bidirectional ? "⟷" : "→";

                EditorGUILayout.LabelField($"{status}", GUILayout.Width(20));
                EditorGUILayout.LabelField($"{conn.fromNodeID}", GUILayout.Width(30));
                EditorGUILayout.LabelField($"({fromName})", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{arrow}", GUILayout.Width(30));
                EditorGUILayout.LabelField($"{conn.toNodeID}", GUILayout.Width(30));
                EditorGUILayout.LabelField($"({toName})", GUILayout.Width(100));

                GUILayout.FlexibleSpace();

                // Focus From button
                if (GUILayout.Button($"👁️ From", GUILayout.Width(60), GUILayout.Height(20)))
                {
                    FocusOnNode(nav, conn.fromNodeID);
                }

                // Focus To button
                if (GUILayout.Button($"👁️ To", GUILayout.Width(60), GUILayout.Height(20)))
                {
                    FocusOnNode(nav, conn.toNodeID);
                }

                // Delete button
                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                if (GUILayout.Button("🗑️", GUILayout.Width(30), GUILayout.Height(20)))
                {
                    if (EditorUtility.DisplayDialog(
                        "Delete Connection",
                        $"Delete: {conn.fromNodeID} → {conn.toNodeID}?",
                        "Delete",
                        "Cancel"))
                    {
                        DeleteConnection(nav, conn);
                        break; // Exit loop after delete
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                GUI.backgroundColor = Color.white;

                GUILayout.Space(2);
            }

            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.EndVertical();
    }

    // ========================================
    // HELPER FUNCTIONS
    // ========================================

    private void DeleteConnection(CentralizedNavigationSystem nav, ConnectionDefinition conn)
    {
        Undo.RecordObject(nav, "Delete Connection");
        nav.connectionDefinitions.Remove(conn);
        nav.RefreshGraph();
        EditorUtility.SetDirty(nav);
        Debug.Log($"[NavSystem] Deleted connection: {conn.fromNodeID} → {conn.toNodeID}");
    }

    private void DeleteAllConnectionsForNode(CentralizedNavigationSystem nav, int nodeID)
    {
        Undo.RecordObject(nav, "Delete All Connections for Node");

        int deletedCount = nav.connectionDefinitions.RemoveAll(conn =>
            conn.fromNodeID == nodeID || conn.toNodeID == nodeID);

        nav.RefreshGraph();
        EditorUtility.SetDirty(nav);
        Debug.Log($"[NavSystem] Deleted {deletedCount} connections for Node {nodeID}");
    }

    private void FocusOnNode(CentralizedNavigationSystem nav, int nodeID)
    {
        if (nav.nodeMap.ContainsKey(nodeID))
        {
            NavNode node = nav.nodeMap[nodeID];
            Selection.activeGameObject = node.gameObject;
            SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log($"[NavSystem] Focused on Node {nodeID} ({node.name})");
        }
        else
        {
            Debug.LogWarning($"[NavSystem] Node {nodeID} does not exist!");
        }
    }
}
#endif