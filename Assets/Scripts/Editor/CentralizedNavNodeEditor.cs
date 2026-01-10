//#if UNITY_EDITOR
//using UnityEngine;
//using UnityEditor;
//using System.Linq;

//[CustomEditor(typeof(NavNode))]
//public class CentralizedNavNodeEditor : Editor
//{
//    private NavNode node;

//    void OnEnable()
//    {
//        node = (NavNode)target;
//    }

//    public override void OnInspectorGUI()
//    {
//        DrawDefaultInspector();

//        EditorGUILayout.Space();
//        EditorGUILayout.LabelField("Centralized Node Info", EditorStyles.boldLabel);

//        // Show parent navigation system info
//        if (node.parentNavSystem != null)
//        {
//            EditorGUILayout.ObjectField(
//                "Parent Nav System",
//                node.parentNavSystem,
//                typeof(CentralizedNavigationSystem),
//                true);

//            // Use connectionDefinitions instead of nonexistent Connections
//            var connections = node.parentNavSystem.connectionDefinitions
//                .Where(c =>
//                    c.fromNodeID == node.nodeID ||
//                    (c.bidirectional && c.toNodeID == node.nodeID))
//                .ToList();

//            EditorGUILayout.LabelField($"Total Connections: {connections.Count}");

//            foreach (var connection in connections)
//            {
//                EditorGUILayout.BeginHorizontal();

//                int otherNodeID = connection.fromNodeID == node.nodeID
//                    ? connection.toNodeID
//                    : connection.fromNodeID;

//                string direction;
//                if (connection.bidirectional)
//                    direction = "↔";
//                else
//                    direction = connection.fromNodeID == node.nodeID ? "→" : "←";

//                EditorGUILayout.LabelField($"{direction} Node {otherNodeID}");

//                if (GUILayout.Button("Remove", GUILayout.Width(60)))
//                {
//                    node.parentNavSystem.RemoveConnection(connection.fromNodeID, connection.toNodeID);
//                    EditorUtility.SetDirty(node.parentNavSystem);
//                }

//                EditorGUILayout.EndHorizontal();
//            }

//        }
//        else
//        {
//            EditorGUILayout.HelpBox("No parent CentralizedNavigationSystem found", MessageType.Warning);

//            if (GUILayout.Button("Find and Assign Parent Nav System"))
//            {
//#if UNITY_2023_1_OR_NEWER
//                CentralizedNavigationSystem navSystem =
//                    Object.FindFirstObjectByType<CentralizedNavigationSystem>();
//#else
//                CentralizedNavigationSystem navSystem =
//                    Object.FindObjectOfType<CentralizedNavigationSystem>();
//#endif
//                if (navSystem != null)
//                {
//                    navSystem.RegisterNode(node);
//                    EditorUtility.SetDirty(navSystem);
//                }
//                else
//                {
//                    Debug.LogWarning("No CentralizedNavigationSystem found in scene.");
//                }
//            }
//        }
//    }

//    void OnSceneGUI()
//    {
//        if (node == null || node.parentNavSystem == null)
//            return;

//        Event e = Event.current;

//        // Handle adding connections by Ctrl+Click
//        if (e.type == EventType.MouseDown && e.button == 0 && e.control)
//        {
//            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
//            RaycastHit hit;
//            if (Physics.Raycast(ray, out hit))
//            {
//                NavNode hitNode = hit.collider.GetComponent<NavNode>();
//                if (hitNode != null && hitNode != node &&
//                    hitNode.parentNavSystem == node.parentNavSystem)
//                {
//                    node.parentNavSystem.AddConnection(node.nodeID, hitNode.nodeID, true);
//                    EditorUtility.SetDirty(node.parentNavSystem);
//                    e.Use();
//                }
//            }
//        }

//        // Draw connection handles for this node
//        Handles.color = Color.cyan;

//        var nodeConnections = node.parentNavSystem.connectionDefinitions
//            .Where(c =>
//                c.fromNodeID == node.nodeID ||
//                (c.bidirectional && c.toNodeID == node.nodeID))
//            .ToList();

//        foreach (var connection in nodeConnections)
//        {
//            int otherNodeID = connection.fromNodeID == node.nodeID
//                ? connection.toNodeID
//                : connection.fromNodeID;

//            if (node.parentNavSystem.nodeMap.ContainsKey(otherNodeID))
//            {
//                Vector3 start = node.transform.position;
//                Vector3 end = node.parentNavSystem.nodeMap[otherNodeID].transform.position;
//                Handles.DrawLine(start, end);

//                // Draw remove handle at midpoint
//                Vector3 midPoint = start + (end - start) * 0.5f;
//                if (Handles.Button(midPoint, Quaternion.identity, 0.5f, 0.5f, Handles.SphereHandleCap))
//                {
//                    node.parentNavSystem.RemoveConnection(connection.fromNodeID, connection.toNodeID);
//                    EditorUtility.SetDirty(node.parentNavSystem);
//                }
//            }
//        }
//    }
//}
//#endif
