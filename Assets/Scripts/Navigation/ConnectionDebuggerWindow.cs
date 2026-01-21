#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class ConnectionDebuggerWindow : EditorWindow
{
    private CentralizedNavigationSystem navSystem;
    private Vector2 scrollPosition;
    private bool showBrokenOnly = true;
    private bool showUnidirectionalOnly = false;
    private bool highlightInScene = true;

    [MenuItem("Tools/Navigation/Connection Debugger")]
    public static void ShowWindow()
    {
        var window = GetWindow<ConnectionDebuggerWindow>("Connection Debugger");
        window.minSize = new Vector2(400, 500);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        FindNavSystem();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void FindNavSystem()
    {
#if UNITY_2023_1_OR_NEWER
        navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
        navSystem = Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif
    }

    private void OnGUI()
    {
        if (navSystem == null)
        {
            FindNavSystem();
        }

        if (navSystem == null)
        {
            EditorGUILayout.HelpBox("❌ No CentralizedNavigationSystem found in scene!", MessageType.Error);
            return;
        }

        // Header
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("🔍 CONNECTION DEBUGGER", headerStyle);

        GUILayout.Space(10);

        // Filters
        GUILayout.Label("Filters:", EditorStyles.boldLabel);
        showBrokenOnly = EditorGUILayout.Toggle("Show Broken Connections Only", showBrokenOnly);
        showUnidirectionalOnly = EditorGUILayout.Toggle("Show Unidirectional Only", showUnidirectionalOnly);
        highlightInScene = EditorGUILayout.Toggle("Highlight in Scene View", highlightInScene);

        GUILayout.Space(10);

        // Analysis
        List<ConnectionDebugInfo> debugInfos = AnalyzeConnections();

        // Stats
        int brokenCount = debugInfos.Count(d => !d.isValid);
        int unidirectionalCount = debugInfos.Count(d => !d.connection.bidirectional);
        int validCount = debugInfos.Count(d => d.isValid);

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 5, 5);

        GUI.backgroundColor = new Color(0.8f, 0.8f, 1f, 0.3f);
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField($"📊 Total Connections: {navSystem.connectionDefinitions.Count}", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"   ✅ Valid: {validCount}");
        EditorGUILayout.LabelField($"   ❌ Broken: {brokenCount}", brokenCount > 0 ? EditorStyles.boldLabel : EditorStyles.label);
        EditorGUILayout.LabelField($"   ➡️ Unidirectional: {unidirectionalCount}");
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(10);

        // Action buttons
        EditorGUILayout.BeginHorizontal();
        
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button($"🗑️ Delete {brokenCount} Broken", GUILayout.Height(30)))
        {
            DeleteBrokenConnections();
        }
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("🔄 Refresh", GUILayout.Height(30)))
        {
            navSystem.RefreshGraph();
        }

        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Connection list
        GUILayout.Label("Connections:", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var info in debugInfos)
        {
            // Apply filters
            if (showBrokenOnly && info.isValid) continue;
            if (showUnidirectionalOnly && info.connection.bidirectional) continue;

            DrawConnectionDebugInfo(info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawConnectionDebugInfo(ConnectionDebugInfo info)
    {
        GUIStyle connStyle = new GUIStyle(GUI.skin.box);
        connStyle.padding = new RectOffset(5, 5, 5, 5);

        Color bgColor;
        if (!info.isValid)
            bgColor = new Color(1f, 0.3f, 0.3f, 0.3f); // Red for broken
        else if (!info.connection.bidirectional)
            bgColor = new Color(1f, 0.7f, 0.3f, 0.3f); // Orange for unidirectional
        else
            bgColor = new Color(0.5f, 1f, 0.5f, 0.2f); // Green for good

        GUI.backgroundColor = bgColor;
        EditorGUILayout.BeginVertical(connStyle);
        EditorGUILayout.BeginHorizontal();

        // Status icon
        string status = info.isValid ? "✓" : "❌";
        EditorGUILayout.LabelField(status, GUILayout.Width(20));

        // From node
        if (info.fromExists)
        {
            EditorGUILayout.LabelField($"{info.connection.fromNodeID}", GUILayout.Width(30));
            EditorGUILayout.LabelField($"({info.fromName})", GUILayout.Width(100));
        }
        else
        {
            GUI.color = Color.red;
            EditorGUILayout.LabelField($"{info.connection.fromNodeID} ❌", GUILayout.Width(30));
            EditorGUILayout.LabelField($"(MISSING)", GUILayout.Width(100));
            GUI.color = Color.white;
        }

        // Arrow
        string arrow = info.connection.bidirectional ? "⟷" : "→";
        EditorGUILayout.LabelField(arrow, GUILayout.Width(30));

        // To node
        if (info.toExists)
        {
            EditorGUILayout.LabelField($"{info.connection.toNodeID}", GUILayout.Width(30));
            EditorGUILayout.LabelField($"({info.toName})", GUILayout.Width(100));
        }
        else
        {
            GUI.color = Color.red;
            EditorGUILayout.LabelField($"{info.connection.toNodeID} ❌", GUILayout.Width(30));
            EditorGUILayout.LabelField($"(MISSING)", GUILayout.Width(100));
            GUI.color = Color.white;
        }

        GUILayout.FlexibleSpace();

        // Distance
        if (info.isValid)
        {
            EditorGUILayout.LabelField($"{info.distance:F1}m", GUILayout.Width(50));
        }

        // Focus button
        if (info.fromExists && GUILayout.Button("👁️ From", GUILayout.Width(60)))
        {
            Selection.activeGameObject = navSystem.nodeMap[info.connection.fromNodeID].gameObject;
            SceneView.lastActiveSceneView.FrameSelected();
        }

        if (info.toExists && GUILayout.Button("👁️ To", GUILayout.Width(60)))
        {
            Selection.activeGameObject = navSystem.nodeMap[info.connection.toNodeID].gameObject;
            SceneView.lastActiveSceneView.FrameSelected();
        }

        // Delete button
        GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
        if (GUILayout.Button("🗑️", GUILayout.Width(30)))
        {
            if (EditorUtility.DisplayDialog(
                "Delete Connection",
                $"Delete: {info.connection.fromNodeID} → {info.connection.toNodeID}?",
                "Delete",
                "Cancel"))
            {
                Undo.RecordObject(navSystem, "Delete Connection");
                navSystem.connectionDefinitions.Remove(info.connection);
                navSystem.RefreshGraph();
                EditorUtility.SetDirty(navSystem);
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        GUILayout.Space(2);
    }

    private List<ConnectionDebugInfo> AnalyzeConnections()
    {
        List<ConnectionDebugInfo> results = new List<ConnectionDebugInfo>();

        foreach (var conn in navSystem.connectionDefinitions)
        {
            var info = new ConnectionDebugInfo();
            info.connection = conn;
            info.fromExists = navSystem.nodeMap.ContainsKey(conn.fromNodeID);
            info.toExists = navSystem.nodeMap.ContainsKey(conn.toNodeID);
            info.isValid = info.fromExists && info.toExists;

            if (info.fromExists)
            {
                info.fromName = navSystem.nodeMap[conn.fromNodeID].name;
                info.fromPosition = navSystem.nodeMap[conn.fromNodeID].transform.position;
            }

            if (info.toExists)
            {
                info.toName = navSystem.nodeMap[conn.toNodeID].name;
                info.toPosition = navSystem.nodeMap[conn.toNodeID].transform.position;
            }

            if (info.isValid)
            {
                info.distance = Vector3.Distance(info.fromPosition, info.toPosition);
            }

            results.Add(info);
        }

        return results;
    }

    private void DeleteBrokenConnections()
    {
        List<ConnectionDefinition> toDelete = new List<ConnectionDefinition>();

        foreach (var conn in navSystem.connectionDefinitions)
        {
            bool fromExists = navSystem.nodeMap.ContainsKey(conn.fromNodeID);
            bool toExists = navSystem.nodeMap.ContainsKey(conn.toNodeID);

            if (!fromExists || !toExists)
            {
                toDelete.Add(conn);
            }
        }

        if (toDelete.Count > 0)
        {
            if (EditorUtility.DisplayDialog(
                "Delete Broken Connections",
                $"Delete {toDelete.Count} broken connections?",
                "Yes",
                "Cancel"))
            {
                Undo.RecordObject(navSystem, "Delete Broken Connections");
                foreach (var conn in toDelete)
                {
                    navSystem.connectionDefinitions.Remove(conn);
                }
                navSystem.RefreshGraph();
                EditorUtility.SetDirty(navSystem);
                Debug.Log($"[ConnectionDebugger] Deleted {toDelete.Count} broken connections");
            }
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!highlightInScene || navSystem == null) return;

        List<ConnectionDebugInfo> debugInfos = AnalyzeConnections();

        foreach (var info in debugInfos)
        {
            if (showBrokenOnly && info.isValid) continue;
            if (showUnidirectionalOnly && info.connection.bidirectional) continue;

            // Draw connections in scene
            if (info.isValid)
            {
                Vector3 start = info.fromPosition + Vector3.up * 0.5f;
                Vector3 end = info.toPosition + Vector3.up * 0.5f;

                if (info.connection.bidirectional)
                {
                    Handles.color = Color.green;
                }
                else
                {
                    Handles.color = Color.red;
                }

                Handles.DrawLine(start, end, 3f);

                // Draw arrow for direction
                Vector3 direction = (end - start).normalized;
                Vector3 midPoint = start + (end - start) * 0.5f;
                DrawArrow(midPoint, direction, info.connection.bidirectional);
            }
            else
            {
                // Draw broken connections
                Handles.color = Color.magenta;

                if (info.fromExists)
                {
                    Vector3 pos = info.fromPosition + Vector3.up * 0.5f;
                    Handles.DrawWireCube(pos, Vector3.one * 2f);
                    Handles.Label(pos + Vector3.up * 2f, $"BROKEN: {info.connection.fromNodeID}→{info.connection.toNodeID}");
                }
            }
        }
    }

    private void DrawArrow(Vector3 position, Vector3 direction, bool bidirectional)
    {
        float arrowSize = 1f;
        Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;

        if (bidirectional)
        {
            // Draw double arrow
            Vector3 tip1 = position + direction * arrowSize;
            Vector3 tip2 = position - direction * arrowSize;

            Handles.DrawLine(position, tip1);
            Handles.DrawLine(position, tip2);

            Handles.DrawLine(tip1, tip1 - direction * 0.5f + right * 0.3f);
            Handles.DrawLine(tip1, tip1 - direction * 0.5f - right * 0.3f);

            Handles.DrawLine(tip2, tip2 + direction * 0.5f + right * 0.3f);
            Handles.DrawLine(tip2, tip2 + direction * 0.5f - right * 0.3f);
        }
        else
        {
            // Draw single arrow
            Vector3 tip = position + direction * arrowSize;
            Handles.DrawLine(position, tip);
            Handles.DrawLine(tip, tip - direction * 0.5f + right * 0.3f);
            Handles.DrawLine(tip, tip - direction * 0.5f - right * 0.3f);
        }
    }

    private class ConnectionDebugInfo
    {
        public ConnectionDefinition connection;
        public bool fromExists;
        public bool toExists;
        public bool isValid;
        public string fromName;
        public string toName;
        public Vector3 fromPosition;
        public Vector3 toPosition;
        public float distance;
    }
}
#endif