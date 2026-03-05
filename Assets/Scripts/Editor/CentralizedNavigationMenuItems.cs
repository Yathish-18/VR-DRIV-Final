#if UNITY_EDITOR
// ============================================================================
//  CENTRALIZED NAVIGATION MENU ITEMS  v8.0
//  ============================================================================
//  Top-menu shortcuts for the CentralizedNavigationSystem workflow.
//
//  IMPORTANT — CentralizedCarController v3.0 is VISUALIZATION ONLY.
//  It has NO targetNode, NO autoFindPath, NO FindAndFollowPath().
//  VehicleController (NWH VehiclePhysics2) owns all movement physics.
// ============================================================================

using UnityEngine;
using UnityEditor;

public static class CentralizedNavigationMenuItems
{
    // =========================================================================
    //  SCENE SETUP
    // =========================================================================

    [MenuItem("Navigation/Centralized/Create Navigation System")]
    private static void CreateNavigationSystem()
    {
        var obj = new GameObject("CentralizedNavigationSystem");
        obj.AddComponent<CentralizedNavigationSystem>();
        Selection.activeGameObject = obj;
        EditorGUIUtility.PingObject(obj);
        Debug.Log("[NavMenu] CentralizedNavigationSystem created.");
    }

    [MenuItem("Navigation/Centralized/Create Nav Node")]
    private static void CreateNavNode()
    {
        var nav = FindNavSystem();

        Vector3 pos = Vector3.zero;
        if (SceneView.lastActiveSceneView != null)
            pos = SceneView.lastActiveSceneView.pivot;

        NavNode created;
        if (nav != null)
        {
            created = nav.CreateNode(pos);
        }
        else
        {
            var nodeObj = new GameObject("NavNode");
            created = nodeObj.AddComponent<NavNode>();
            nodeObj.transform.position = pos;
            created.nodeID = 0;
        }

        Selection.activeGameObject = created.gameObject;
        EditorGUIUtility.PingObject(created.gameObject);
    }

    /// <summary>
    /// Creates a test car with CentralizedCarController (VISUALIZATION ONLY).
    /// VehicleController handles all movement — this just shows the planned route.
    /// </summary>
    [MenuItem("Navigation/Centralized/Create Test Car (Visualization Only)")]
    private static void CreateTestCar()
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "TestCar_Visualization";
        obj.transform.localScale = new Vector3(2f, 1f, 4f);

        var rend = obj.GetComponent<Renderer>();
        if (rend != null)
        {
            rend.sharedMaterial = new Material(Shader.Find("Standard"));
            rend.sharedMaterial.color = Color.blue;
        }

        var ctrl = obj.AddComponent<CentralizedCarController>();

        var nav = FindNavSystem();
        if (nav != null)
        {
            ctrl.navSystem = nav;
            Debug.Log("[NavMenu] TestCar created and linked to CentralizedNavigationSystem.\n" +
                      "followPath=false (default). Attach VehicleController for physics.");
        }
        else
        {
            Debug.LogWarning("[NavMenu] No CentralizedNavigationSystem found.\n" +
                             "Create one first, then assign it to the car's navSystem field.");
        }

        Selection.activeGameObject = obj;
        EditorGUIUtility.PingObject(obj);
    }

    [MenuItem("Navigation/Centralized/Setup Demo Scene (6 nodes)")]
    private static void SetupDemoScene()
    {
        var navObj = new GameObject("CentralizedNavigationSystem");
        var nav = navObj.AddComponent<CentralizedNavigationSystem>();

        Vector3[] positions =
        {
            new Vector3(  0f, 0.5f,  0f),
            new Vector3( 10f, 0.5f,  0f),
            new Vector3( 15f, 0.5f, 10f),
            new Vector3( 10f, 0.5f, 20f),
            new Vector3(  0f, 0.5f, 20f),
            new Vector3(-10f, 0.5f, 10f),
        };
        foreach (var p in positions) nav.CreateNode(p);
        nav.AutoConnectNodes();

        var carObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carObj.name = "TestCar_Visualization";
        carObj.transform.position = new Vector3(0f, 0.5f, 0f);
        carObj.transform.localScale = new Vector3(2f, 1f, 4f);

        var rend = carObj.GetComponent<Renderer>();
        if (rend != null) { rend.sharedMaterial = new Material(Shader.Find("Standard")); rend.sharedMaterial.color = Color.blue; }

        var ctrl = carObj.AddComponent<CentralizedCarController>();
        ctrl.navSystem = nav;
        ctrl.followPath = false;

        Debug.Log("[NavMenu] Demo scene: 6 nodes, connected. CentralizedCarController = visualization only.");
        Selection.activeGameObject = navObj;
        EditorGUIUtility.PingObject(navObj);
    }

    // =========================================================================
    //  GRAPH TOOLS
    // =========================================================================

    [MenuItem("Navigation/Centralized/Graph/Organize All Nodes")]
    private static void OrganizeAllNodes()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.CollectAllNodes();
        EditorUtility.SetDirty(nav);
        Debug.Log($"[NavMenu] Nodes organized under '{nav.name}'.");
    }

    [MenuItem("Navigation/Centralized/Graph/Validate and Rebuild Graph")]
    private static void ValidateGraph()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.ValidateAndRebuildGraph();
        EditorUtility.SetDirty(nav);
        Debug.Log("[NavMenu] Graph validated and rebuilt.");
    }

    [MenuItem("Navigation/Centralized/Graph/Auto Connect All Nodes")]
    private static void AutoConnect()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.AutoConnectNodes();
        EditorUtility.SetDirty(nav);
        Debug.Log("[NavMenu] Auto-connect complete.");
    }

    [MenuItem("Navigation/Centralized/Graph/Clear All Connections")]
    private static void ClearConnections()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        if (EditorUtility.DisplayDialog("Clear All Connections",
            $"Delete all {nav.connectionDefinitions.Count} connections?", "Delete", "Cancel"))
        {
            Undo.RecordObject(nav, "Clear All Connections");
            nav.ClearAllConnections();
            EditorUtility.SetDirty(nav);
        }
    }

    // =========================================================================
    //  DEBUG
    // =========================================================================

    [MenuItem("Navigation/Centralized/Debug/Print All Connections")]
    private static void PrintConnections()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.DebugPrintAllConnections();
    }

    [MenuItem("Navigation/Centralized/Debug/Print Segment Cache")]
    private static void PrintSegments()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.DebugPrintSegmentCache();
    }

    [MenuItem("Navigation/Centralized/Debug/Print Route Pool")]
    private static void PrintRoutePool()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.DebugPrintRoutePool();
    }

    [MenuItem("Navigation/Centralized/Debug/Test Path (First → Last Node)")]
    private static void TestPath()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.TestPathZeroToLast();
    }

    // =========================================================================
    //  CAR CONTROLLER TOOLS
    // =========================================================================

    [MenuItem("Navigation/Centralized/Car/Debug Reaction Times")]
    private static void DebugReactionTimes()
    {
        var car = FindCar();
        if (car == null) { Warn("No CentralizedCarController in scene."); return; }

        float avg = car.GetAverageReactionTime();
        float worst = car.GetWorstReactionTime();

        Debug.Log($"[NavMenu] Reaction Times — " +
                  $"Avg: {(avg >= 0 ? $"{avg * 1000f:F0} ms" : "no data")}  |  " +
                  $"Worst: {(worst >= 0 ? $"{worst * 1000f:F0} ms" : "no data")}  |  " +
                  $"Route: {car.CurrentSourceNode} → {car.CurrentDestNode}");
    }

    [MenuItem("Navigation/Centralized/Car/Force Route Refresh (Play Mode)")]
    private static void ForceRouteRefresh()
    {
        if (!Application.isPlaying) { Warn("Force Route Refresh only works in Play mode."); return; }
        var car = FindCar();
        if (car == null) { Warn("No CentralizedCarController in scene."); return; }
        car.ForceRouteRefresh();
        Debug.Log("[NavMenu] Car route refresh triggered.");
    }

    [MenuItem("Navigation/Centralized/Car/Clear Reaction Data (Play Mode)")]
    private static void ClearReactionData()
    {
        if (!Application.isPlaying) { Warn("Clear Reaction Data only works in Play mode."); return; }
        var car = FindCar();
        if (car == null) { Warn("No CentralizedCarController in scene."); return; }
        car.ClearReactionData();
        Debug.Log("[NavMenu] Reaction time data cleared.");
    }

    // =========================================================================
    //  BAKING SHORTCUTS
    // =========================================================================

    [MenuItem("Navigation/Centralized/Bake/Bake Full Route Cache")]
    private static void BakeCache()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        if (Application.isPlaying) { Warn("Cannot bake in Play mode. Stop Play first."); return; }
        nav.EditorBakeFullCache();
    }

    [MenuItem("Navigation/Centralized/Bake/Clear Route Cache")]
    private static void ClearCache()
    {
        var nav = FindNavSystem();
        if (nav == null) { Warn("No CentralizedNavigationSystem in scene."); return; }
        nav.EditorClearCache();
    }

    // =========================================================================
    //  HELPERS
    // =========================================================================

    private static CentralizedNavigationSystem FindNavSystem()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
        return Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif
    }

    private static CentralizedCarController FindCar()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<CentralizedCarController>();
#else
        return Object.FindObjectOfType<CentralizedCarController>();
#endif
    }

    private static void Warn(string msg) => Debug.LogWarning($"[NavMenu] {msg}");
}
#endif