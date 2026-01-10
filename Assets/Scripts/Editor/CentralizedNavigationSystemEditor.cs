#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(CentralizedNavigationSystem))]
public class CentralizedNavigationSystemEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        CentralizedNavigationSystem nav = (CentralizedNavigationSystem)target;

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
    }
}
#endif
