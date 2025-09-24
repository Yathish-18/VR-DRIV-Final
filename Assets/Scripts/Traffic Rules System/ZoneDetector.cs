using UnityEngine;
using NWH.VehiclePhysics2;

public class DrivingZoneDetector : MonoBehaviour
{
    [Header("Zone Settings")]
    [SerializeField] private string zoneName = "General";
    [SerializeField] private float zoneSpeedLimit = 50f;

    [Header("Detection Settings")]
    [SerializeField] private LayerMask vehicleLayerMask = -1;
    [SerializeField] private bool debugMode = false;

    // FIXED: Updated references for enhanced system
    private EnhancedSpeedViolationDetector speedDetector;
    private EnhancedDriverScoringSystem scoringSystem;

    void Start()
    {
        // FIXED: Find the enhanced speed detector
        speedDetector = FindObjectOfType<EnhancedSpeedViolationDetector>();

        // FIXED: Get reference to enhanced scoring system
        scoringSystem = EnhancedDriverScoringSystem.Instance;

        // Ensure this has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            Debug.LogError($"DrivingZoneDetector '{zoneName}': No Collider component found! Please add a Collider and set it as trigger.");
        }

        if (debugMode)
        {
            Debug.Log($"DrivingZoneDetector '{zoneName}' initialized - Speed Limit: {zoneSpeedLimit} km/h");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (IsVehicle(other.gameObject))
        {
            Debug.Log($"Vehicle entered {zoneName} zone (Speed Limit: {zoneSpeedLimit} km/h)");

            // Update speed detector with zone information
            if (speedDetector != null)
            {
                speedDetector.SetCurrentZone(zoneName);
                speedDetector.SetSpeedLimit(zoneSpeedLimit);

                if (debugMode)
                {
                    Debug.Log($"Updated speed detector - Zone: {zoneName}, Limit: {zoneSpeedLimit} km/h");
                }
            }
            else
            {
                Debug.LogWarning("DrivingZoneDetector: No EnhancedSpeedViolationDetector found in scene!");
            }

            // REMOVED: TrafficRulesManager reference (replaced by enhanced system)
            // The EnhancedDriverScoringSystem automatically handles zone-based scoring

            // Optional: Notify scoring system of zone change
            if (scoringSystem != null && debugMode)
            {
                Debug.Log($"Vehicle entered scoring zone: {zoneName}");
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsVehicle(other.gameObject))
        {
            if (debugMode)
            {
                Debug.Log($"Vehicle exited {zoneName} zone");
            }
        }
    }

    bool IsVehicle(GameObject obj)
    {
        // NWH INTEGRATION - Check for NWH VehicleController
        bool hasVehicleController = obj.GetComponent<VehicleController>() != null;
        bool correctLayer = (vehicleLayerMask & (1 << obj.layer)) != 0;

        return hasVehicleController && correctLayer;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = GetZoneColor();
        Gizmos.matrix = transform.localToWorldMatrix;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (col is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);

                // Draw zone label
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                    $"{zoneName}\n{zoneSpeedLimit} km/h");
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);

                // Draw zone label
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f,
                    $"{zoneName}\n{zoneSpeedLimit} km/h");
            }
        }
        else
        {
            // Draw a default cube if no collider
            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        }
    }

    Color GetZoneColor()
    {
        switch (zoneName.ToLower())
        {
            case "school zone":
            case "school":
                return Color.red;
            case "urban zone":
            case "urban":
                return Color.blue;
            case "highway":
                return Color.green;
            case "parking":
                return Color.yellow;
            case "residential":
                return Color.cyan;
            case "construction":
                return new Color(1f, 0.5f, 0f); // Orange
            default:
                return Color.white;
        }
    }

    // Public API for manual zone changes
    public void SetZoneInfo(string newZoneName, float newSpeedLimit)
    {
        zoneName = newZoneName;
        zoneSpeedLimit = newSpeedLimit;

        if (debugMode)
        {
            Debug.Log($"Zone info updated - Name: {zoneName}, Speed Limit: {zoneSpeedLimit} km/h");
        }
    }

    public string GetZoneName() => zoneName;
    public float GetSpeedLimit() => zoneSpeedLimit;

    // Preset zone configurations
    [ContextMenu("Set School Zone")]
    public void SetSchoolZone()
    {
        SetZoneInfo("School Zone", 25f);
    }

    [ContextMenu("Set Urban Zone")]
    public void SetUrbanZone()
    {
        SetZoneInfo("Urban Zone", 50f);
    }

    [ContextMenu("Set Highway Zone")]
    public void SetHighwayZone()
    {
        SetZoneInfo("Highway", 100f);
    }

    [ContextMenu("Set Parking Zone")]
    public void SetParkingZone()
    {
        SetZoneInfo("Parking", 10f);
    }

    [ContextMenu("Set Residential Zone")]
    public void SetResidentialZone()
    {
        SetZoneInfo("Residential", 30f);
    }
}
