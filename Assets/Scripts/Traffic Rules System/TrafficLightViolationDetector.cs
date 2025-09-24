using UnityEngine;
using System.Collections.Generic;
using NWH.VehiclePhysics2;

[RequireComponent(typeof(Collider))]
public class EnhancedTrafficLightViolationDetector : MonoBehaviour
{
    [Header("Detection Setup")]
    public TrafficLightController trafficLight;
    public LayerMask vehicleLayerMask = -1;

    [Header("Detection Settings")]
    public float violationGracePeriod = 1f;
    public bool debugMode = false;
    [SerializeField] private bool autoFindTrafficLight = true;

    // Cached components and data
    private Collider detectionCollider;
    private HashSet<GameObject> trackedVehicles = new HashSet<GameObject>();
    private TrafficLightController.LightState previousState;
    private float lastRedLightTime = 0f;

    // Events for violation detection
    public System.Action<GameObject> OnViolationDetected;

    void Start()
    {
        InitializeSystem();
    }

    void InitializeSystem()
    {
        // Auto-find traffic light if not assigned
        if (trafficLight == null && autoFindTrafficLight)
        {
            trafficLight = GetComponent<TrafficLightController>();
        }

        if (trafficLight == null)
        {
            Debug.LogError($"EnhancedTrafficLightViolationDetector on {gameObject.name}: No TrafficLightController found!");
            enabled = false;
            return;
        }

        detectionCollider = GetComponent<Collider>();
        if (detectionCollider == null)
        {
            Debug.LogError($"EnhancedTrafficLightViolationDetector on {gameObject.name}: No Collider component found!");
            enabled = false;
            return;
        }

        detectionCollider.isTrigger = true;
        previousState = trafficLight.currentState;

        Debug.Log($"EnhancedTrafficLightViolationDetector on {gameObject.name} connected to traffic light: {trafficLight.GetTrafficLightID()}");
    }

    void Update()
    {
        if (trafficLight == null) return;

        TrafficLightController.LightState currentState = trafficLight.currentState;
        if (currentState == TrafficLightController.LightState.Red && previousState != currentState)
        {
            lastRedLightTime = Time.time;
            if (debugMode) Debug.Log($"Traffic light {trafficLight.GetTrafficLightID()} turned RED at time: {Time.time}");
        }

        previousState = currentState;
    }

    void OnTriggerEnter(Collider other)
    {
        GameObject vehicle = other.gameObject;
        if (!IsValidVehicle(vehicle)) return;
        if (trackedVehicles.Contains(vehicle)) return;

        trackedVehicles.Add(vehicle);

        if (debugMode) Debug.Log($"Vehicle '{vehicle.name}' entered intersection controlled by {GetTrafficLightID()}");

        if (IsViolation())
        {
            Debug.Log($"TRAFFIC VIOLATION: Vehicle '{vehicle.name}' ran red light at {GetTrafficLightID()}!");
            OnViolationDetected?.Invoke(vehicle);

            // Register with enhanced scoring system
            if (EnhancedDriverScoringSystem.Instance != null)
            {
                // Determine violation severity based on timing
                float timeSinceRed = Time.time - lastRedLightTime;
                EnhancedDriverScoringSystem.ViolationType violationType = EnhancedDriverScoringSystem.ViolationType.TrafficLight;

                EnhancedDriverScoringSystem.Instance.RegisterTrafficLightViolation(violationType, GetViolationPenalty(timeSinceRed));
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        GameObject vehicle = other.gameObject;
        if (!IsValidVehicle(vehicle)) return;

        if (trackedVehicles.Remove(vehicle))
        {
            if (debugMode) Debug.Log($"Vehicle '{vehicle.name}' exited intersection controlled by {GetTrafficLightID()}");
        }
    }

    bool IsValidVehicle(GameObject obj)
    {
        return obj.GetComponent<VehicleController>() != null && IsVehicleLayer(obj);
    }

    bool IsVehicleLayer(GameObject obj)
    {
        return (vehicleLayerMask & (1 << obj.layer)) != 0;
    }

    bool IsViolation()
    {
        if (trafficLight == null || trafficLight.currentState != TrafficLightController.LightState.Red)
            return false;

        bool inGracePeriod = Time.time - lastRedLightTime < violationGracePeriod;

        if (debugMode && !inGracePeriod)
        {
            Debug.Log($"Grace period expired for {GetTrafficLightID()}. Time since red: {Time.time - lastRedLightTime}s");
        }

        return !inGracePeriod;
    }

    float GetViolationPenalty(float timeSinceRed)
    {
        // Progressive penalty based on how blatant the violation is
        if (timeSinceRed < 1f)
            return 15f; // Immediate red light run - severe
        else if (timeSinceRed < 3f)
            return 10f; // Quick red light run - major
        else
            return 5f;  // Late entry - moderate
    }

    // ADDED: Missing SetTrafficLight method
    public void SetTrafficLight(TrafficLightController newTrafficLight)
    {
        trafficLight = newTrafficLight;

        if (trafficLight != null)
        {
            previousState = trafficLight.currentState;
            if (debugMode)
            {
                Debug.Log($"EnhancedTrafficLightViolationDetector: Traffic light set to {trafficLight.GetTrafficLightID()}");
            }
        }
        else
        {
            Debug.LogWarning("EnhancedTrafficLightViolationDetector: Traffic light set to null");
        }
    }

    // ADDED: Additional public API methods
    public TrafficLightController GetTrafficLight()
    {
        return trafficLight;
    }

    public void SetGracePeriod(float newGracePeriod)
    {
        violationGracePeriod = Mathf.Max(0f, newGracePeriod);
        if (debugMode)
        {
            Debug.Log($"EnhancedTrafficLightViolationDetector: Grace period set to {violationGracePeriod}s");
        }
    }

    public void SetDebugMode(bool enabled)
    {
        debugMode = enabled;
        Debug.Log($"EnhancedTrafficLightViolationDetector: Debug mode {(enabled ? "enabled" : "disabled")}");
    }

    // Public API getters
    public bool IsVehicleInIntersection(GameObject vehicle) => trackedVehicles.Contains(vehicle);
    public List<GameObject> GetVehiclesInIntersection() => new List<GameObject>(trackedVehicles);
    public int GetTrackedVehicleCount() => trackedVehicles.Count;
    public bool IsTrafficLightRed() => trafficLight != null && trafficLight.currentState == TrafficLightController.LightState.Red;
    public float GetTimeSinceRedLight() => Time.time - lastRedLightTime;
    public bool IsInGracePeriod() => GetTimeSinceRedLight() < violationGracePeriod;
    public string GetTrafficLightID() => trafficLight?.GetTrafficLightID() ?? "Unknown";

    // Gizmo visualization
    void OnDrawGizmos()
    {
        if (detectionCollider != null)
        {
            Gizmos.color = IsTrafficLightRed() ? Color.red : Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;

            if (detectionCollider is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (detectionCollider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }

        // Draw connection to traffic light
        if (trafficLight != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, trafficLight.transform.position);
        }
    }

    // Context menu helpers
    [ContextMenu("Find Closest Traffic Light")]
    public void FindClosestTrafficLight()
    {
        TrafficLightController[] allLights = FindObjectsOfType<TrafficLightController>();
        TrafficLightController closest = null;
        float closestDistance = float.MaxValue;

        foreach (var light in allLights)
        {
            float distance = Vector3.Distance(transform.position, light.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = light;
            }
        }

        if (closest != null)
        {
            SetTrafficLight(closest);
            Debug.Log($"Found and connected to closest traffic light: {closest.GetTrafficLightID()} at distance {closestDistance:F1}m");
        }
        else
        {
            Debug.LogWarning("No traffic lights found in the scene");
        }
    }

    [ContextMenu("Test Violation")]
    public void TestViolation()
    {
        if (trafficLight != null)
        {
            Debug.Log($"Testing violation detection - Current state: {trafficLight.currentState}, Grace period: {violationGracePeriod}s");
        }
    }
}
