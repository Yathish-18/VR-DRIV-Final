#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;

[CustomEditor(typeof(TrafficVehicle))]
public class TrafficVehicleEditor : Editor
{
    private Vector2 routeScrollPosition;

    public override void OnInspectorGUI()
    {
        TrafficVehicle vehicle = (TrafficVehicle)target;

        // Only show debug in play mode
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("⚠️ Debug info only available in Play Mode", MessageType.Info);
            DrawDefaultInspector();
            return;
        }

        EditorGUILayout.Space(10);

        // Header
        GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel);
        headerStyle.fontSize = 14;
        headerStyle.normal.textColor = Color.cyan;
        EditorGUILayout.LabelField("🚗 TRAFFIC VEHICLE DEBUG PANEL", headerStyle);

        EditorGUILayout.Space(5);

        // Status Box
        DrawStatusBox(vehicle);

        EditorGUILayout.Space(10);

        // Movement Info
        DrawMovementInfo(vehicle);

        EditorGUILayout.Space(10);

        // Waypoint Info
        DrawWaypointInfo(vehicle);

        EditorGUILayout.Space(10);

        // NEW: Full Route Display
        DrawFullRoute(vehicle);

        EditorGUILayout.Space(10);

        // Actions
        DrawActions(vehicle);

        EditorGUILayout.Space(10);

        // Default inspector at bottom
        DrawDefaultInspector();

        // Auto-refresh in play mode
        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private TrafficWaypointChain GetCurrentChain(TrafficVehicle vehicle)
    {
        // Use reflection to get private field
        FieldInfo chainField = typeof(TrafficVehicle).GetField("currentChain", BindingFlags.NonPublic | BindingFlags.Instance);
        if (chainField != null)
        {
            return chainField.GetValue(vehicle) as TrafficWaypointChain;
        }
        return null;
    }

    private void DrawStatusBox(TrafficVehicle vehicle)
    {
        SerializedProperty isObstacleDetected = serializedObject.FindProperty("debugIsObstacleDetected");
        SerializedProperty currentSpeed = serializedObject.FindProperty("debugCurrentSpeed");

        bool hasObstacle = isObstacleDetected != null && isObstacleDetected.boolValue;
        float speed = currentSpeed != null ? currentSpeed.floatValue : 0f;

        // Status color
        Color statusColor = hasObstacle ? Color.red : (speed > 0.5f ? Color.green : Color.yellow);
        string statusText = hasObstacle ? "⛔ STOPPED (Obstacle)" : (speed > 0.5f ? "✅ MOVING" : "⏸️ IDLE");

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.textColor = statusColor;
        boxStyle.fontStyle = FontStyle.Bold;
        boxStyle.fontSize = 12;
        boxStyle.alignment = TextAnchor.MiddleCenter;

        GUI.backgroundColor = statusColor * 0.3f;
        EditorGUILayout.BeginVertical(boxStyle);
        EditorGUILayout.LabelField(statusText, EditorStyles.boldLabel);
        EditorGUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
    }

    private void DrawMovementInfo(TrafficVehicle vehicle)
    {
        EditorGUILayout.LabelField("📊 Movement Data", EditorStyles.boldLabel);

        SerializedProperty currentSpeed = serializedObject.FindProperty("debugCurrentSpeed");
        SerializedProperty distanceToWaypoint = serializedObject.FindProperty("debugDistanceToWaypoint");

        EditorGUI.BeginDisabledGroup(true);

        float speed = currentSpeed != null ? currentSpeed.floatValue : 0f;
        float distance = distanceToWaypoint != null ? distanceToWaypoint.floatValue : 0f;

        EditorGUILayout.FloatField("Current Speed (m/s)", speed);
        EditorGUILayout.FloatField("Speed (km/h)", speed * 3.6f);
        EditorGUILayout.FloatField("Distance to Waypoint (m)", distance);

        EditorGUI.EndDisabledGroup();
    }

    private void DrawWaypointInfo(TrafficVehicle vehicle)
    {
        EditorGUILayout.LabelField("🎯 Waypoint Navigation", EditorStyles.boldLabel);

        SerializedProperty chainName = serializedObject.FindProperty("debugChainName");
        SerializedProperty currentWaypoint = serializedObject.FindProperty("debugCurrentWaypoint");
        SerializedProperty currentNodeID = serializedObject.FindProperty("debugCurrentNodeID");
        SerializedProperty totalWaypoints = serializedObject.FindProperty("debugTotalWaypoints");
        SerializedProperty targetPosition = serializedObject.FindProperty("debugTargetPosition");
        SerializedProperty spawnPosition = serializedObject.FindProperty("debugSpawnPosition");

        EditorGUI.BeginDisabledGroup(true);

        if (chainName != null)
            EditorGUILayout.TextField("Chain Name", chainName.stringValue);

        if (currentWaypoint != null && totalWaypoints != null)
        {
            int current = currentWaypoint.intValue;
            int nodeID = currentNodeID != null ? currentNodeID.intValue : -1;
            int total = totalWaypoints.intValue;
            float progress = total > 0 ? (float)current / total : 0f;

            // Show both chain index AND node ID
            EditorGUILayout.LabelField("Current Waypoint", $"Chain Index: {current} | Node ID: {nodeID}");
            EditorGUILayout.IntField("Total Waypoints", total);
            
            // Progress bar
            Rect rect = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(rect, progress, $"{current}/{total} ({progress * 100:F0}%)");
        }

        if (spawnPosition != null)
            EditorGUILayout.Vector3Field("Spawn Position", spawnPosition.vector3Value);

        if (targetPosition != null)
            EditorGUILayout.Vector3Field("Target Position", targetPosition.vector3Value);

        EditorGUI.EndDisabledGroup();

        // Show next waypoint
        DrawNextWaypoint(vehicle);
    }

    private void DrawNextWaypoint(TrafficVehicle vehicle)
    {
        SerializedProperty currentWaypoint = serializedObject.FindProperty("debugCurrentWaypoint");
        SerializedProperty nextWaypointIndex = serializedObject.FindProperty("debugNextWaypointIndex");
        SerializedProperty nextNodeID = serializedObject.FindProperty("debugNextNodeID");
        SerializedProperty totalWaypoints = serializedObject.FindProperty("debugTotalWaypoints");
        TrafficWaypointChain chain = GetCurrentChain(vehicle);

        if (currentWaypoint != null && totalWaypoints != null && chain != null)
        {
            int current = currentWaypoint.intValue;
            int total = totalWaypoints.intValue;
            int nextIndex = nextWaypointIndex != null ? nextWaypointIndex.intValue : (current + 1) % total;
            int nodeID = nextNodeID != null ? nextNodeID.intValue : -1;

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("📍 Next Waypoint", EditorStyles.boldLabel);

            GUIStyle nextStyle = new GUIStyle(GUI.skin.box);
            nextStyle.normal.textColor = Color.cyan;
            GUI.backgroundColor = new Color(0, 1, 1, 0.2f);
            
            EditorGUILayout.BeginVertical(nextStyle);
            
            if (nextIndex < chain.waypoints.Count && chain.waypoints[nextIndex] != null)
            {
                Vector3 nextPos = chain.waypoints[nextIndex].position;
                EditorGUILayout.LabelField($"→ Chain Index: {nextIndex} | Node ID: {nodeID}");
                EditorGUILayout.LabelField($"   Position: ({nextPos.x:F1}, {nextPos.y:F1}, {nextPos.z:F1})");
            }
            else
            {
                EditorGUILayout.LabelField($"→ Chain Index: {nextIndex} | Node ID: {nodeID}");
            }
            
            EditorGUILayout.EndVertical();
            
            GUI.backgroundColor = Color.white;
        }
    }

    private void DrawFullRoute(TrafficVehicle vehicle)
    {
        EditorGUILayout.LabelField("🗺️ Full Route Map", EditorStyles.boldLabel);

        TrafficWaypointChain chain = GetCurrentChain(vehicle);
        SerializedProperty currentWaypointProp = serializedObject.FindProperty("debugCurrentWaypoint");

        if (chain == null)
        {
            EditorGUILayout.HelpBox("No chain assigned", MessageType.Warning);
            return;
        }

        if (chain.waypoints == null || chain.waypoints.Count == 0)
        {
            EditorGUILayout.HelpBox("Chain has no waypoints", MessageType.Warning);
            return;
        }

        int currentIndex = currentWaypointProp != null ? currentWaypointProp.intValue : 0;

        // Scrollable route list
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.padding = new RectOffset(10, 10, 10, 10);

        EditorGUILayout.BeginVertical(boxStyle);
        routeScrollPosition = EditorGUILayout.BeginScrollView(routeScrollPosition, GUILayout.MaxHeight(200));

        for (int i = 0; i < chain.waypoints.Count; i++)
        {
            Transform waypoint = chain.waypoints[i];
            if (waypoint == null)
            {
                EditorGUILayout.LabelField($"❌ Waypoint {i}: NULL", EditorStyles.miniLabel);
                continue;
            }

            // Get Node ID
            NavNode node = waypoint.GetComponent<NavNode>();
            int nodeID = node != null ? node.nodeID : -1;

            // Determine status icon and color
            string icon;
            Color bgColor;
            GUIStyle waypointStyle = new GUIStyle(GUI.skin.box);

            if (i == currentIndex)
            {
                // Current waypoint
                icon = "🎯";
                bgColor = new Color(0, 1, 0, 0.3f); // Green
                waypointStyle.fontStyle = FontStyle.Bold;
            }
            else if (i == (currentIndex + 1) % chain.waypoints.Count)
            {
                // Next waypoint
                icon = "➡️";
                bgColor = new Color(0, 1, 1, 0.3f); // Cyan
                waypointStyle.fontStyle = FontStyle.Bold;
            }
            else if (i < currentIndex)
            {
                // Completed waypoints
                icon = "✅";
                bgColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Gray
            }
            else
            {
                // Upcoming waypoints
                icon = "⭕";
                bgColor = new Color(1, 1, 1, 0.1f); // Light
            }

            GUI.backgroundColor = bgColor;
            EditorGUILayout.BeginHorizontal(waypointStyle);

            // Waypoint info with Node ID
            EditorGUILayout.LabelField($"{icon} Chain:{i} | Node:{nodeID}", GUILayout.Width(130));
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector3Field("", waypoint.position, GUILayout.ExpandWidth(true));
            EditorGUI.EndDisabledGroup();

            // Focus button
            if (GUILayout.Button("👁", GUILayout.Width(30)))
            {
                Selection.activeGameObject = waypoint.gameObject;
                SceneView.lastActiveSceneView.FrameSelected();
            }

            EditorGUILayout.EndHorizontal();

            // Draw connector arrow (except for last)
            if (i < chain.waypoints.Count - 1)
            {
                GUIStyle arrowStyle = new GUIStyle(EditorStyles.label);
                arrowStyle.alignment = TextAnchor.MiddleCenter;
                arrowStyle.normal.textColor = Color.gray;
                EditorGUILayout.LabelField("↓", arrowStyle, GUILayout.Height(15));
            }
            else if (chain.loop)
            {
                // Loop indicator
                GUIStyle loopStyle = new GUIStyle(EditorStyles.label);
                loopStyle.alignment = TextAnchor.MiddleCenter;
                loopStyle.normal.textColor = Color.yellow;
                EditorGUILayout.LabelField("🔄 LOOP TO START", loopStyle, GUILayout.Height(15));
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        GUI.backgroundColor = Color.white;

        // Route summary
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Total Waypoints: {chain.waypoints.Count}", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"Loop: {(chain.loop ? "✅ Yes" : "❌ No")}", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawActions(TrafficVehicle vehicle)
    {
        EditorGUILayout.LabelField("⚙️ Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("📍 Focus on Vehicle"))
        {
            Selection.activeGameObject = vehicle.gameObject;
            SceneView.lastActiveSceneView.FrameSelected();
        }

        if (GUILayout.Button("🎨 Toggle Debug Rays"))
        {
            SerializedProperty showDebugRays = serializedObject.FindProperty("showDebugRays");
            if (showDebugRays != null)
            {
                showDebugRays.boolValue = !showDebugRays.boolValue;
                serializedObject.ApplyModifiedProperties();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🗺️ Show Full Chain"))
        {
            TrafficWaypointChain chain = GetCurrentChain(vehicle);
            if (chain != null && chain.waypoints != null && chain.waypoints.Count > 0)
            {
                List<GameObject> chainObjects = new List<GameObject>();
                foreach (var wp in chain.waypoints)
                {
                    if (wp != null)
                        chainObjects.Add(wp.gameObject);
                }
                Selection.objects = chainObjects.ToArray();
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }

        if (GUILayout.Button("➡️ Focus Next Waypoint"))
        {
            TrafficWaypointChain chain = GetCurrentChain(vehicle);
            SerializedProperty currentWaypoint = serializedObject.FindProperty("debugCurrentWaypoint");
            
            if (chain != null && currentWaypoint != null && chain.waypoints != null)
            {
                int nextIndex = (currentWaypoint.intValue + 1) % chain.waypoints.Count;
                if (nextIndex < chain.waypoints.Count && chain.waypoints[nextIndex] != null)
                {
                    Selection.activeGameObject = chain.waypoints[nextIndex].gameObject;
                    SceneView.lastActiveSceneView.FrameSelected();
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }
}
#endif