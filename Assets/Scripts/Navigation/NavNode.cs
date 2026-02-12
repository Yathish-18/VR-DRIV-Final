using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class NavNode : MonoBehaviour
{
    [HideInInspector] public int nodeID;
    [HideInInspector] public CentralizedNavigationSystem parentNavSystem;

    public Vector3 worldPosition => transform.position;

    private void Start()
    {
        if (parentNavSystem == null)
        {
#if UNITY_2023_1_OR_NEWER
            parentNavSystem = Object.FindFirstObjectByType<CentralizedNavigationSystem>();
#else
            parentNavSystem = Object.FindObjectOfType<CentralizedNavigationSystem>();
#endif
        }

        if (parentNavSystem != null)
            parentNavSystem.RegisterNode(this);
        else
            Debug.LogWarning("[NavNode] No CentralizedNavigationSystem found");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (parentNavSystem != null)
            parentNavSystem.RefreshGraph();
    }

    //private void OnDrawGizmos()
    //{
    //    // Node sphere only
    //    Gizmos.color = Selection.activeGameObject == gameObject ? Color.yellow : Color.cyan;
    //    Gizmos.DrawSphere(transform.position, 0.5f);

    //    // Node ID label only
    //    Handles.Label(transform.position + Vector3.up * 1.2f, $"ID: {nodeID}");
    //}

    //private void OnDrawGizmosSelected()
    //{
    //    // Thick highlight only
    //    Gizmos.color = Color.yellow;
    //    Gizmos.DrawSphere(transform.position, 0.8f);

    //    // Show connections only
    //    if (parentNavSystem?.connectionDefinitions != null)
    //    {
    //        foreach (var conn in parentNavSystem.connectionDefinitions)
    //        {
    //            if (conn.fromNodeID == nodeID && parentNavSystem.nodeMap.ContainsKey(conn.toNodeID))
    //            {
    //                Vector3 otherPos = parentNavSystem.nodeMap[conn.toNodeID].transform.position;
    //                Gizmos.color = conn.bidirectional ? Color.green : Color.red;
    //                Gizmos.DrawLine(transform.position, otherPos);
    //            }
    //            else if (conn.toNodeID == nodeID && conn.bidirectional && parentNavSystem.nodeMap.ContainsKey(conn.fromNodeID))
    //            {
    //                Vector3 otherPos = parentNavSystem.nodeMap[conn.fromNodeID].transform.position;
    //                Gizmos.color = Color.green;
    //                Gizmos.DrawLine(transform.position, otherPos);
    //            }
    //        }
    //    }
    //}
#endif
}
