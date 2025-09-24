using UnityEngine;

[System.Serializable]
public class NavNode : MonoBehaviour
{
    [Header("Node Settings")]
    public int nodeID = -1;
    public Color nodeColor = Color.blue;
    public float nodeSize = 1f;

    [Header("Visual Settings")]
    public bool showLabel = true;
    public Vector3 labelOffset = Vector3.up * 2f;

    [Header("Road Detection Integration")]
    [Tooltip("Automatically adjust node position to road surface on creation")]
    public bool snapToRoad = true;
    [Tooltip("Layer mask for detecting road surfaces")]
    public LayerMask roadLayerMask = 1;
    [Tooltip("Distance to raycast down when looking for road")]
    public float roadRaycastDistance = 50f;
    [Tooltip("How high above current position to start raycast")]
    public float roadRaycastUpOffset = 10f;

    // Reference to parent navigation system
    [HideInInspector] public CentralizedNavigationSystem parentNavSystem;

    private void Awake()
    {
        // Find and assign parent navigation system
        FindAndAssignParent();

        // Snap to road if enabled
        if (snapToRoad)
        {
            SnapToRoadSurface();
        }
    }

    private void Start()
    {
        // Ensure we're registered with the navigation system
        FindAndAssignParent();
    }

    void FindAndAssignParent()
    {
        // Find navigation system if not assigned
        if (parentNavSystem == null)
        {
            parentNavSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
        }

        // Register with the navigation system
        if (parentNavSystem != null)
        {
            parentNavSystem.RegisterNode(this);
        }
    }

    /// <summary>
    /// Snaps this node to the road surface for perfect road alignment
    /// </summary>
    [ContextMenu("Snap to Road Surface")]
    public void SnapToRoadSurface()
    {
        Vector3 roadPos = RoadDetectionHelper.GetRoadSurfacePosition(transform.position, roadLayerMask, roadRaycastDistance, roadRaycastUpOffset);
        if (roadPos != transform.position)
        {
            transform.position = roadPos;
            Debug.Log($"Node {nodeID} snapped to road surface at {roadPos}");
        }
    }

    private void OnDrawGizmos()
    {
        // Draw node
        Gizmos.color = nodeColor;
        Gizmos.DrawWireSphere(transform.position, nodeSize);

        // Draw node label
#if UNITY_EDITOR
        if (showLabel)
        {
            string label = nodeID >= 0 ? $"Node {nodeID}" : gameObject.name;
            UnityEditor.Handles.Label(transform.position + labelOffset, label);
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        // Highlight selected node
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position, nodeSize * 1.2f);
    }

    private void OnDestroy()
    {
        // Unregister from parent navigation system
        if (parentNavSystem != null)
        {
            parentNavSystem.UnregisterNode(this);
        }
    }

    // Force this node to find and join a navigation system
    [ContextMenu("Force Join Navigation System")]
    public void ForceJoinNavigationSystem()
    {
        FindAndAssignParent();
    }
}
