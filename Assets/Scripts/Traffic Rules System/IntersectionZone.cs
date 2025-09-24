using UnityEngine;
using NWH.VehiclePhysics2;
[RequireComponent(typeof(Collider))]
public class IntersectionZone : MonoBehaviour
{
    [Header("Intersection Settings")]
    [SerializeField] private string intersectionName = "Intersection";
    [SerializeField] private bool enableDebugLogs = false;

    void Start()
    {
        // Ensure proper setup
        if (!gameObject.CompareTag("Intersection"))
        {
            Debug.LogWarning($"IntersectionZone '{name}' should have 'Intersection' tag!");
        }

        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            if (enableDebugLogs)
                Debug.Log($"IntersectionZone '{name}': Set collider as trigger");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (enableDebugLogs && other.GetComponent<VehicleController>())
        {
            Debug.Log($"Vehicle entered intersection: {intersectionName}");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (enableDebugLogs && other.GetComponent<VehicleController>())
        {
            Debug.Log($"Vehicle exited intersection: {intersectionName}");
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }

        // Draw intersection label
        Vector3 labelPos = transform.position + Vector3.up * 3f;
        UnityEditor.Handles.Label(labelPos, $"INTERSECTION\n{intersectionName}");
    }
}
