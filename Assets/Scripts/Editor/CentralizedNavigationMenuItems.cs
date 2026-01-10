#if UNITY_EDITOR

using UnityEngine;
using UnityEditor;

// Menu items for easy access
public static class CentralizedNavigationMenuItems
{
    [MenuItem("Navigation/Centralized/Create Navigation System")]
    private static void CreateNavigationSystem()
    {
        GameObject navSysObj = new GameObject("CentralizedNavigationSystem");
        CentralizedNavigationSystem navSystem =
            navSysObj.AddComponent<CentralizedNavigationSystem>();
        Selection.activeGameObject = navSysObj;
        EditorGUIUtility.PingObject(navSysObj);
    }

    [MenuItem("Navigation/Centralized/Create Nav Node")]
    private static void CreateNavNode()
    {
#if UNITY_2023_1_OR_NEWER
        CentralizedNavigationSystem navSystem =
            Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
        CentralizedNavigationSystem navSystem =
            Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif

        Vector3 position = Vector3.zero;
        if (SceneView.lastActiveSceneView != null)
        {
            position = SceneView.lastActiveSceneView.pivot;
        }

        NavNode createdNode = null;

        if (navSystem != null)
        {
            createdNode = navSystem.CreateNode(position);
        }
        else
        {
            // Create standalone node
            GameObject nodeObj = new GameObject("NavNode");
            createdNode = nodeObj.AddComponent<NavNode>();
            nodeObj.transform.position = position;
            createdNode.nodeID = 0;
        }

        Selection.activeGameObject = createdNode.gameObject;
        EditorGUIUtility.PingObject(createdNode.gameObject);
    }

    [MenuItem("Navigation/Centralized/Create Test Car")]
    private static void CreateTestCar()
    {
        GameObject carObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carObj.name = "CentralizedTestCar";
        carObj.transform.localScale = new Vector3(2f, 1f, 3f); // Make it look more car-like

        Renderer carRenderer = carObj.GetComponent<Renderer>();
        if (carRenderer != null && carRenderer.sharedMaterial != null)
        {
            carRenderer.sharedMaterial.color = Color.blue;
        }

        CentralizedCarController carController =
            carObj.AddComponent<CentralizedCarController>();

        // Create target as a NavNode instead of just a sphere
        GameObject targetObj = new GameObject("CarTargetNode");
        NavNode targetNavNode = targetObj.AddComponent<NavNode>();
        targetObj.transform.position = Vector3.forward * 10f;

        // Link them
        carController.targetNode = targetNavNode;

        // Find navigation system
#if UNITY_2023_1_OR_NEWER
        CentralizedNavigationSystem navSystem =
            Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
        CentralizedNavigationSystem navSystem =
            Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif

        if (navSystem != null)
        {
            carController.navSystem = navSystem;
        }
        else
        {
            Debug.LogWarning("No CentralizedNavigationSystem found in scene. Create one first!");
        }

        Selection.activeGameObject = carObj;
        EditorGUIUtility.PingObject(carObj);
    }

    [MenuItem("Navigation/Centralized/Setup Demo Scene")]
    private static void SetupDemoScene()
    {
        // Create navigation system
        GameObject navSysObj = new GameObject("CentralizedNavigationSystem");
        CentralizedNavigationSystem navSystem =
            navSysObj.AddComponent<CentralizedNavigationSystem>();

        // Create a grid of nodes using the centralized system
        int gridSize = 5;
        float spacing = 5f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                Vector3 position = new Vector3(x * spacing, 0, z * spacing);
                int nodeID = x * gridSize + z;
                navSystem.CreateNode(position, nodeID);
            }
        }

        // Auto-connect nodes
        navSystem.AutoConnectNodes();

        // Create test car at position (0, 0, 0)
        GameObject carObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carObj.name = "CentralizedTestCar";
        carObj.transform.position = new Vector3(0, 0.5f, 0);
        carObj.transform.localScale = new Vector3(2f, 1f, 3f);

        Renderer carRenderer = carObj.GetComponent<Renderer>();
        if (carRenderer != null && carRenderer.sharedMaterial != null)
        {
            carRenderer.sharedMaterial.color = Color.blue;
        }

        CentralizedCarController carController =
            carObj.AddComponent<CentralizedCarController>();

        // Create target at far corner AS A NAVNODE
        GameObject targetObj = new GameObject("CarTargetNode");
        NavNode targetNavNode = targetObj.AddComponent<NavNode>();
        targetObj.transform.position =
            new Vector3((gridSize - 1) * spacing, 0.5f, (gridSize - 1) * spacing);

        // Link them
        carController.targetNode = targetNavNode;
        carController.navSystem = navSystem;
        carController.autoFindPath = true;

        Debug.Log(
            $"Demo scene created with {gridSize * gridSize} nodes in a {gridSize}x{gridSize} grid");
        Debug.Log("Press Play to see the car automatically pathfind to the target, " +
                  "or press Space in Play mode to recalculate path");

        Selection.activeGameObject = navSysObj;
        EditorGUIUtility.PingObject(navSysObj);
    }

    [MenuItem("Navigation/Centralized/Organize All Nodes")]
    private static void OrganizeAllNodes()
    {
#if UNITY_2023_1_OR_NEWER
        CentralizedNavigationSystem navSystem =
            Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
        CentralizedNavigationSystem navSystem =
            Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif

        if (navSystem != null)
        {
            navSystem.CollectAllNodes();
            EditorUtility.SetDirty(navSystem);
            Debug.Log($"Organized all nodes under {navSystem.name}");
        }
        else
        {
            Debug.LogWarning("No CentralizedNavigationSystem found in scene");
        }
    }

    [MenuItem("Navigation/Centralized/Test Line Renderer")]
    private static void TestLineRenderer()
    {
#if UNITY_2023_1_OR_NEWER
        CentralizedNavigationSystem navSystem =
            Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
        CentralizedNavigationSystem navSystem =
            Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif

        if (navSystem != null)
        {
            navSystem.TestLineRendererVisibility();
            Debug.Log("Line Renderer test completed. Check Scene view for visibility.");
        }
        else
        {
            Debug.LogWarning("No CentralizedNavigationSystem found in scene");
        }
    }

    [MenuItem("Navigation/Centralized/Force LineRenderer Setup")]
    private static void ForceLineRendererSetup()
    {
#if UNITY_2023_1_OR_NEWER
        CentralizedNavigationSystem navSystem =
            Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
        CentralizedNavigationSystem navSystem =
            Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif

        if (navSystem != null)
        {
            // Access the private method via reflection or make it public
            var method = typeof(CentralizedNavigationSystem).GetMethod(
                "ForceLineRendererSetup",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public);

            if (method != null)
            {
                method.Invoke(navSystem, null);
                EditorUtility.SetDirty(navSystem);
                Debug.Log("Forced LineRenderer setup completed");
            }
            else
            {
                Debug.LogWarning(
                    "ForceLineRendererSetup method not found. Make sure the method is public or accessible.");
            }
        }
        else
        {
            Debug.LogWarning("No CentralizedNavigationSystem found in scene");
        }
    }

    [MenuItem("Navigation/Centralized/Debug Car Pathfinding")]
    private static void DebugCarPathfinding()
    {
#if UNITY_2023_1_OR_NEWER
        CentralizedCarController car =
            Object.FindFirstObjectByType<CentralizedCarController>();
#else
        CentralizedCarController car =
            Object.FindObjectOfType<CentralizedCarController>();
#endif

        if (car != null)
        {
            car.showDebugLogs = true;
            if (car.targetNode != null)
            {
                car.FindAndFollowPath();
                Debug.Log("Triggered car pathfinding with debug logs enabled");
            }
            else
            {
                Debug.LogWarning("Car has no target node assigned");
            }
        }
        else
        {
            Debug.LogWarning("No CentralizedCarController found in scene");
        }
    }
}

#endif
