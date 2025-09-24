#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// Scene view overlay for centralized system
[InitializeOnLoad]
public class CentralizedNavigationSceneOverlay
{
    static CentralizedNavigationSceneOverlay()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        CentralizedNavigationSystem navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
        if (navSystem == null) return;

        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(10, 10, 250, 180));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Centralized Navigation Tools", EditorStyles.boldLabel);

        if (GUILayout.Button("Create Node Here"))
        {
            Vector3 mousePos = Event.current.mousePosition;
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePos);
            Vector3 worldPos = ray.origin + ray.direction * 10f;

            NavNode createdNode = navSystem.CreateNode(worldPos);
            Selection.activeGameObject = createdNode.gameObject;
            EditorUtility.SetDirty(navSystem);
        }

        GUILayout.Space(5);

        if (GUILayout.Button("Auto-Connect All Nodes"))
        {
            navSystem.AutoConnectNodes();
            EditorUtility.SetDirty(navSystem);
        }

        if (GUILayout.Button("Refresh Graph"))
        {
            navSystem.RefreshGraph();
        }

        if (GUILayout.Button("Collect All Nodes"))
        {
            navSystem.CollectAllNodes();
            EditorUtility.SetDirty(navSystem);
        }

        GUILayout.Space(5);
        GUILayout.Label("Ctrl+Click to connect nodes", EditorStyles.miniLabel);
        GUILayout.Label($"Nodes: {navSystem.nodes.Count}", EditorStyles.miniLabel);
        GUILayout.Label($"Connections: {navSystem.connections.Count}", EditorStyles.miniLabel);
        GUILayout.Label($"Hierarchy: {(navSystem.nodesParent != null ? "✓" : "✗")}", EditorStyles.miniLabel);

        GUILayout.EndVertical();
        GUILayout.EndArea();

        Handles.EndGUI();

        // Show node IDs in scene view
        foreach (var node in navSystem.nodes)
        {
            if (node != null)
            {
                // Different color for nodes that aren't properly parented
                bool isProperlyParented = node.transform.parent == navSystem.nodesParent;
                Handles.color = isProperlyParented ? Color.white : Color.red;

                string label = $"ID: {node.nodeID}";
                if (!isProperlyParented) label += " (Unorganized)";

                Handles.Label(node.transform.position + Vector3.up * 1.5f, label);
            }
        }
    }
}
#endif