#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// Menu items for easy access
public class CentralizedNavigationMenuItems
{
    [MenuItem("Navigation/Centralized/Create Navigation System")]
    static void CreateNavigationSystem()
    {
        GameObject navSysObj = new GameObject("CentralizedNavigationSystem");
        CentralizedNavigationSystem navSystem = navSysObj.AddComponent<CentralizedNavigationSystem>();

        Selection.activeGameObject = navSysObj;
        EditorGUIUtility.PingObject(navSysObj);
    }

    [MenuItem("Navigation/Centralized/Create Nav Node")]
    static void CreateNavNode()
    {
        CentralizedNavigationSystem navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();

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
    static void CreateTestCar()
    {
        GameObject carObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        carObj.name = "CentralizedTestCar";
        carObj.transform.localScale = new Vector3(2f, 1f, 3f); // Make it look more car-like
        carObj.GetComponent<Renderer>().material.color = Color.blue;

        CentralizedCarController carController = carObj.AddComponent<CentralizedCarController>();

        // Create target
        GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        targetObj.name = "CarTarget";
        targetObj.transform.position = Vector3.forward * 10f;
        targetObj.GetComponent<Renderer>().material.color = Color.green;

        // Link them
        carController.target = targetObj.transform;

        // Find navigation system
        CentralizedNavigationSystem navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
        if (navSystem != null)
        {
            carController.navSystem = navSystem;
            // Force setup the line renderer
            navSystem.TestLineRendererVisibility();
        }
        else
        {
            Debug.LogWarning("No CentralizedNavigationSystem found in scene. Create one first!");
        }

        Selection.activeGameObject = carObj;
        EditorGUIUtility.PingObject(carObj);
    }

    [MenuItem("Navigation/Centralized/Setup Demo Scene")]
    static void SetupDemoScene()
    {
        // Create navigation system
        GameObject navSysObj = new GameObject("CentralizedNavigationSystem");
        CentralizedNavigationSystem navSystem = navSysObj.AddComponent<CentralizedNavigationSystem>();

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
        carObj.GetComponent<Renderer>().material.color = Color.blue;

        CentralizedCarController carController = carObj.AddComponent<CentralizedCarController>();

        // Create target at far corner
        GameObject targetObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        targetObj.name = "CarTarget";
        targetObj.transform.position = new Vector3((gridSize - 1) * spacing, 0.5f, (gridSize - 1) * spacing);
        targetObj.GetComponent<Renderer>().material.color = Color.green;

        // Link them
        carController.target = targetObj.transform;
        carController.navSystem = navSystem;
        carController.autoFindPath = true;

        // Force LineRenderer setup
        EditorApplication.delayCall += () => {
            navSystem.TestLineRendererVisibility();
        };

        Debug.Log($"Demo scene created with {gridSize * gridSize} nodes in a {gridSize}x{gridSize} grid");
        Debug.Log("Press Play to see the car automatically pathfind to the target, or press Space in Play mode to recalculate path");

        Selection.activeGameObject = navSysObj;
    }

    [MenuItem("Navigation/Centralized/Organize All Nodes")]
    static void OrganizeAllNodes()
    {
        CentralizedNavigationSystem navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
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
    static void TestLineRenderer()
    {
        CentralizedNavigationSystem navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
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
    static void ForceLineRendererSetup()
    {
        CentralizedNavigationSystem navSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
        if (navSystem != null)
        {
            // Access the private method via reflection or make it public
            var method = typeof(CentralizedNavigationSystem).GetMethod("ForceLineRendererSetup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(navSystem, null);
                EditorUtility.SetDirty(navSystem);
                Debug.Log("Forced LineRenderer setup completed");
            }
            else
            {
                Debug.LogWarning("ForceLineRendererSetup method not found. Make sure the method is public or accessible.");
            }
        }
        else
        {
            Debug.LogWarning("No CentralizedNavigationSystem found in scene");
        }
    }

    [MenuItem("Navigation/Centralized/Debug Car Pathfinding")]
    static void DebugCarPathfinding()
    {
        CentralizedCarController car = Object.FindFirstObjectByType<CentralizedCarController>();
        if (car != null)
        {
            car.showDebugLogs = true;
            if (car.target != null)
            {
                car.FindAndFollowPath();
                Debug.Log("Triggered car pathfinding with debug logs enabled");
            }
            else
            {
                Debug.LogWarning("Car has no target assigned");
            }
        }
        else
        {
            Debug.LogWarning("No CentralizedCarController found in scene");
        }
    }
}
#endif