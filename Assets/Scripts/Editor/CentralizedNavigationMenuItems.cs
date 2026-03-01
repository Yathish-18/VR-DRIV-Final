#if UNITY_EDITOR
// ============================================================================
//  CENTRALIZED NAVIGATION MENU ITEMS
//  ============================================================================
//  Top-menu shortcuts for common Navigation system tasks.
//
//  FIXED v7.0:
//    Removed all references to old CentralizedCarController fields/methods
//    that no longer exist:
//      targetNode, autoFindPath, showDebugLogs, FindAndFollowPath()
//
//  CentralizedCarController v3.0 is VISUALIZATION ONLY.
//  It has no targetNode, no autoFindPath, no FindAndFollowPath.
//  Movement is handled exclusively by VehicleController (NWH VehiclePhysics2).
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
        var navSysObj = new GameObject("CentralizedNavigationSystem");
        navSysObj.AddComponent<CentralizedNavigationSystem>();
        Selection.activeGameObject = navSysObj;
        EditorGUIUtility.PingObject(navSysObj);
        Debug.Log("[NavMenu] CentralizedNavigationSystem created.");
    }

    [MenuItem("Navigation/Centralized/Create Nav Node")]
    private static void CreateNavNode()
    {
        CentralizedNavigationSystem navSystem = FindNavSystem();

        Vector3 position = Vector3.zero;
        if (SceneView.lastActiveSceneView != null)
            position = SceneView.lastActiveSceneView.pivot;

        NavNode createdNode;

        if (navSystem != null)
        {
            createdNode = navSystem.CreateNode(position);
        }
        else
        {
            // Standalone node when no system exists yet
            var nodeObj = new GameObject("NavNode");
            createdNode = nodeObj.AddComponent<NavNode>();
            nodeObj.transform.position = position;
            createdNode.nodeID = 0;
        }

        Selection.activeGameObject = createdNode.gameObject;
        EditorGUIUtility.PingObject(createdNode.gameObject);
    }

    /// <summary>
    /// Creates a test car with CentralizedCarController (VISUALIZATION ONLY).
    /// Movement is handled by VehicleController — this just adds route visualization.
    /// </summary>
    [MenuItem("Navigation/Centralized/Create Test Car (Visualization Only)")]
    private static void CreateTestCar()
    {
        // Create a simple car-shaped cube for testing
        var carObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carObj.name = "TestCar_Visualization";
        carObj.transform.localScale = new Vector3(2f, 1f, 4f);

        var renderer = carObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            renderer.sharedMaterial.color = Color.blue;
        }

        // Add the visualization controller
        var carController = carObj.AddComponent<CentralizedCarController>();

        // Wire up nav system if present
        CentralizedNavigationSystem navSystem = FindNavSystem();
        if (navSystem != null)
        {
            carController.navSystem = navSystem;
            Debug.Log("[NavMenu] TestCar created and linked to CentralizedNavigationSystem.");
        }
        else
        {
            Debug.LogWarning("[NavMenu] No CentralizedNavigationSystem in scene. " +
                             "Create one first, then assign it to the car's navSystem field.");
        }

        Selection.activeGameObject = carObj;
        EditorGUIUtility.PingObject(carObj);
    }

    [MenuItem("Navigation/Centralized/Setup Demo Scene")]
    private static void SetupDemoScene()
    {
        // Navigation system
        var navSysObj = new GameObject("CentralizedNavigationSystem");
        var navSystem = navSysObj.AddComponent<CentralizedNavigationSystem>();

        // 5×5 grid of nodes
        const int   gridSize = 5;
        const float spacing  = 5f;

        for (int x = 0; x < gridSize; x++)
            for (int z = 0; z < gridSize; z++)
                navSystem.CreateNode(new Vector3(x * spacing, 0f, z * spacing), x * gridSize + z);

        navSystem.AutoConnectNodes();

        // Test car — visualization only
        var carObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carObj.name = "TestCar_Visualization";
        carObj.transform.position = new Vector3(0f, 0.5f, 0f);
        carObj.transform.localScale = new Vector3(2f, 1f, 4f);

        var renderer = carObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            renderer.sharedMaterial.color = Color.blue;
        }

        var carController = carObj.AddComponent<CentralizedCarController>();
        carController.navSystem  = navSystem;
        carController.followPath = false; // off by default — VehicleController drives in production

        Debug.Log($"[NavMenu] Demo scene created: {gridSize * gridSize} nodes in a {gridSize}×{gridSize} grid.\n" +
                  "CentralizedCarController is set to visualization-only mode.\n" +
                  "Attach VehicleController for actual driving physics.");

        Selection.activeGameObject = navSysObj;
        EditorGUIUtility.PingObject(navSysObj);
    }

    // =========================================================================
    //  GRAPH TOOLS
    // =========================================================================

    [MenuItem("Navigation/Centralized/Organize All Nodes")]
    private static void OrganizeAllNodes()
    {
        CentralizedNavigationSystem navSystem = FindNavSystem();
        if (navSystem == null)
        {
            Debug.LogWarning("[NavMenu] No CentralizedNavigationSystem in scene.");
            return;
        }
        navSystem.CollectAllNodes();
        EditorUtility.SetDirty(navSystem);
        Debug.Log($"[NavMenu] Organized all nodes under '{navSystem.name}'.");
    }

    [MenuItem("Navigation/Centralized/Validate & Rebuild Graph")]
    private static void ValidateGraph()
    {
        CentralizedNavigationSystem navSystem = FindNavSystem();
        if (navSystem == null)
        {
            Debug.LogWarning("[NavMenu] No CentralizedNavigationSystem in scene.");
            return;
        }
        navSystem.ValidateAndRebuildGraph();
        EditorUtility.SetDirty(navSystem);
        Debug.Log("[NavMenu] Graph validated and rebuilt.");
    }

    [MenuItem("Navigation/Centralized/Debug Print Connections")]
    private static void DebugPrintConnections()
    {
        CentralizedNavigationSystem navSystem = FindNavSystem();
        if (navSystem == null)
        {
            Debug.LogWarning("[NavMenu] No CentralizedNavigationSystem in scene.");
            return;
        }
        navSystem.DebugPrintAllConnections();
    }

    [MenuItem("Navigation/Centralized/Debug Print Segment Cache")]
    private static void DebugPrintSegmentCache()
    {
        CentralizedNavigationSystem navSystem = FindNavSystem();
        if (navSystem == null)
        {
            Debug.LogWarning("[NavMenu] No CentralizedNavigationSystem in scene.");
            return;
        }
        navSystem.DebugPrintSegmentCache();
    }

    [MenuItem("Navigation/Centralized/Debug Print Route Pool")]
    private static void DebugPrintRoutePool()
    {
        CentralizedNavigationSystem navSystem = FindNavSystem();
        if (navSystem == null)
        {
            Debug.LogWarning("[NavMenu] No CentralizedNavigationSystem in scene.");
            return;
        }
        navSystem.DebugPrintRoutePool();
    }

    // =========================================================================
    //  CAR CONTROLLER TOOLS
    // =========================================================================

    /// <summary>
    /// Prints current reaction time stats from CentralizedCarController.
    /// </summary>
    [MenuItem("Navigation/Centralized/Debug Car Reaction Times")]
    private static void DebugCarReactionTimes()
    {
#if UNITY_2023_1_OR_NEWER
        var car = Object.FindFirstObjectByType<CentralizedCarController>();
#else
        var car = Object.FindObjectOfType<CentralizedCarController>();
#endif
        if (car == null)
        {
            Debug.LogWarning("[NavMenu] No CentralizedCarController in scene.");
            return;
        }

        float avg   = car.GetAverageReactionTime();
        float worst = car.GetWorstReactionTime();

        Debug.Log($"[NavMenu] Car Reaction Times — " +
                  $"Avg: {(avg   >= 0 ? $"{avg   * 1000f:F0} ms" : "no data")} | " +
                  $"Worst: {(worst >= 0 ? $"{worst * 1000f:F0} ms" : "no data")} | " +
                  $"Route: {car.CurrentSourceNode} → {car.CurrentDestNode}");
    }

    /// <summary>
    /// Forces a route refresh on the CentralizedCarController (at runtime).
    /// </summary>
    [MenuItem("Navigation/Centralized/Force Car Route Refresh")]
    private static void ForceCarRouteRefresh()
    {
#if UNITY_2023_1_OR_NEWER
        var car = Object.FindFirstObjectByType<CentralizedCarController>();
#else
        var car = Object.FindObjectOfType<CentralizedCarController>();
#endif
        if (car == null)
        {
            Debug.LogWarning("[NavMenu] No CentralizedCarController in scene.");
            return;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[NavMenu] Force Route Refresh only works in Play mode.");
            return;
        }

        car.ForceRouteRefresh();
        Debug.Log("[NavMenu] Car route refresh triggered.");
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
}
#endif