using System.Collections.Generic;
using UnityEngine;

public class TrafficLightManager : MonoBehaviour
{
    [Header("Traffic Light Management")]
    [SerializeField] private List<TrafficLightController> allTrafficLights = new List<TrafficLightController>();
    [SerializeField] private List<EnhancedTrafficLightViolationDetector> allViolationDetectors = new List<EnhancedTrafficLightViolationDetector>(); // FIXED: Updated type
    [SerializeField] private bool autoDiscoverTrafficLights = true;
    [SerializeField] private bool logConnectionDetails = true;

    // Static instance for global access
    public static TrafficLightManager Instance { get; private set; }

    // Statistics - FIXED: Updated type references
    private Dictionary<string, TrafficLightController> trafficLightRegistry = new Dictionary<string, TrafficLightController>();
    private Dictionary<string, EnhancedTrafficLightViolationDetector> detectorRegistry = new Dictionary<string, EnhancedTrafficLightViolationDetector>();

    // ADDED: Reference to enhanced scoring system
    private EnhancedDriverScoringSystem scoringSystem;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Get reference to enhanced scoring system
        scoringSystem = EnhancedDriverScoringSystem.Instance;

        if (autoDiscoverTrafficLights)
        {
            DiscoverAllTrafficComponents();
        }

        RegisterAllComponents();
        ConnectDetectorsToTrafficLights();
    }

    void DiscoverAllTrafficComponents()
    {
        // Find all traffic lights
        allTrafficLights.Clear();
        TrafficLightController[] foundLights = FindObjectsOfType<TrafficLightController>();
        allTrafficLights.AddRange(foundLights);

        // FIXED: Find enhanced violation detectors
        allViolationDetectors.Clear();
        EnhancedTrafficLightViolationDetector[] foundDetectors = FindObjectsOfType<EnhancedTrafficLightViolationDetector>();
        allViolationDetectors.AddRange(foundDetectors);

        Debug.Log($"TrafficLightManager: Discovered {allTrafficLights.Count} traffic lights and {allViolationDetectors.Count} enhanced detectors");
    }

    void RegisterAllComponents()
    {
        // Register traffic lights
        for (int i = 0; i < allTrafficLights.Count; i++)
        {
            RegisterTrafficLight(allTrafficLights[i], i);
        }

        // Register detectors
        for (int i = 0; i < allViolationDetectors.Count; i++)
        {
            RegisterViolationDetector(allViolationDetectors[i], i);
        }
    }

    public void RegisterTrafficLight(TrafficLightController trafficLight, int index = -1)
    {
        if (trafficLight == null) return;

        if (!allTrafficLights.Contains(trafficLight))
        {
            allTrafficLights.Add(trafficLight);
        }

        // Assign unique ID if not already assigned
        string currentID = trafficLight.GetTrafficLightID();
        if (string.IsNullOrEmpty(currentID))
        {
            int actualIndex = index >= 0 ? index : allTrafficLights.Count;
            string newID = $"TrafficLight_{actualIndex + 1:D2}";
            trafficLight.SetTrafficLightID(newID);
            currentID = newID;
        }

        // Add to registry
        if (!trafficLightRegistry.ContainsKey(currentID))
        {
            trafficLightRegistry[currentID] = trafficLight;
        }

        if (logConnectionDetails)
        {
            Debug.Log($"Registered traffic light: {currentID} at position {trafficLight.transform.position}");
        }
    }

    // FIXED: Updated method signature for enhanced detector type
    public void RegisterViolationDetector(EnhancedTrafficLightViolationDetector detector, int index = -1)
    {
        if (detector == null) return;

        if (!allViolationDetectors.Contains(detector))
        {
            allViolationDetectors.Add(detector);
        }

        // Assign unique ID
        int actualIndex = index >= 0 ? index : allViolationDetectors.Count;
        string detectorID = $"Detector_{actualIndex + 1:D2}";

        if (!detectorRegistry.ContainsKey(detectorID))
        {
            detectorRegistry[detectorID] = detector;
        }

        if (logConnectionDetails)
        {
            Debug.Log($"Registered enhanced violation detector: {detectorID} at position {detector.transform.position}");
        }
    }

    void ConnectDetectorsToTrafficLights()
    {
        foreach (var detector in allViolationDetectors)
        {
            if (detector.trafficLight == null)
            {
                TrafficLightController closestLight = FindClosestTrafficLight(detector.transform.position);
                if (closestLight != null)
                {
                    // FIXED: Now this method exists in the enhanced detector
                    detector.SetTrafficLight(closestLight);

                    if (logConnectionDetails)
                    {
                        string detectorName = detector.gameObject.name;
                        string lightID = closestLight.GetTrafficLightID();
                        float distance = Vector3.Distance(detector.transform.position, closestLight.transform.position);
                        Debug.Log($"Connected enhanced detector '{detectorName}' to traffic light '{lightID}' (Distance: {distance:F1}m)");
                    }
                }
            }
        }
    }


    public TrafficLightController FindClosestTrafficLight(Vector3 position, float maxDistance = 100f)
    {
        TrafficLightController closest = null;
        float closestDistance = float.MaxValue;

        foreach (var light in allTrafficLights)
        {
            if (light != null)
            {
                float distance = Vector3.Distance(position, light.transform.position);
                if (distance < closestDistance && distance <= maxDistance)
                {
                    closestDistance = distance;
                    closest = light;
                }
            }
        }

        return closest;
    }

    // FIXED: Updated return type for enhanced detector
    public EnhancedTrafficLightViolationDetector FindDetectorForTrafficLight(string trafficLightID)
    {
        TrafficLightController targetLight = GetTrafficLight(trafficLightID);
        if (targetLight == null) return null;

        foreach (var detector in allViolationDetectors)
        {
            if (detector.trafficLight == targetLight)
            {
                return detector;
            }
        }

        return null;
    }

    public void UnregisterTrafficLight(TrafficLightController trafficLight)
    {
        if (trafficLight != null)
        {
            allTrafficLights.Remove(trafficLight);
            string id = trafficLight.GetTrafficLightID();
            if (!string.IsNullOrEmpty(id) && trafficLightRegistry.ContainsKey(id))
            {
                trafficLightRegistry.Remove(id);
            }
        }
    }

    // FIXED: Updated parameter type for enhanced detector
    public void UnregisterViolationDetector(EnhancedTrafficLightViolationDetector detector)
    {
        if (detector != null)
        {
            allViolationDetectors.Remove(detector);

            // Remove from registry
            string keyToRemove = null;
            foreach (var kvp in detectorRegistry)
            {
                if (kvp.Value == detector)
                {
                    keyToRemove = kvp.Key;
                    break;
                }
            }
            if (keyToRemove != null)
            {
                detectorRegistry.Remove(keyToRemove);
            }
        }
    }

    // Public API
    public TrafficLightController GetTrafficLight(string id)
    {
        return trafficLightRegistry.ContainsKey(id) ? trafficLightRegistry[id] : null;
    }

    public List<TrafficLightController> GetAllTrafficLights()
    {
        return new List<TrafficLightController>(allTrafficLights);
    }

    // FIXED: Updated return type for enhanced detectors
    public List<EnhancedTrafficLightViolationDetector> GetAllViolationDetectors()
    {
        return new List<EnhancedTrafficLightViolationDetector>(allViolationDetectors);
    }

    public int GetTrafficLightCount() => allTrafficLights.Count;
    public int GetDetectorCount() => allViolationDetectors.Count;

    public Dictionary<string, TrafficLightController> GetTrafficLightRegistry()
    {
        return new Dictionary<string, TrafficLightController>(trafficLightRegistry);
    }

    // ADDED: Enhanced detector registry access
    public Dictionary<string, EnhancedTrafficLightViolationDetector> GetDetectorRegistry()
    {
        return new Dictionary<string, EnhancedTrafficLightViolationDetector>(detectorRegistry);
    }

    // Control methods
    public void StartAllTrafficLights()
    {
        foreach (var light in allTrafficLights)
        {
            if (light != null) light.StartTrafficLight();
        }

        Debug.Log($"TrafficLightManager: Started {allTrafficLights.Count} traffic lights");
    }

    public void StopAllTrafficLights()
    {
        foreach (var light in allTrafficLights)
        {
            if (light != null) light.StopTrafficLight();
        }

        Debug.Log($"TrafficLightManager: Stopped {allTrafficLights.Count} traffic lights");
    }

    public void SetAllTrafficLights(TrafficLightController.LightState state)
    {
        foreach (var light in allTrafficLights)
        {
            if (light != null) light.SetLightState(state);
        }

        Debug.Log($"TrafficLightManager: Set all traffic lights to {state}");
    }

    // ADDED: Enhanced integration methods
    public void NotifyViolation(string trafficLightID, GameObject vehicle, float severity)
    {
        if (scoringSystem != null)
        {
            // Map severity to violation type for enhanced scoring
            EnhancedDriverScoringSystem.ViolationType violationType = EnhancedDriverScoringSystem.ViolationType.TrafficLight;
            scoringSystem.RegisterTrafficLightViolation(violationType, severity);

            if (logConnectionDetails)
            {
                Debug.Log($"TrafficLightManager: Registered violation at {trafficLightID} with severity {severity}");
            }
        }
    }

    // ADDED: Bulk operations for enhanced detectors
    public void SetAllDetectorGracePeriods(float gracePeriod)
    {
        foreach (var detector in allViolationDetectors)
        {
            if (detector != null)
            {
                detector.violationGracePeriod = gracePeriod;
            }
        }

        Debug.Log($"TrafficLightManager: Set grace period to {gracePeriod}s for all detectors");
    }

    public void EnableDebugModeForAllDetectors(bool enabled)
    {
        foreach (var detector in allViolationDetectors)
        {
            if (detector != null)
            {
                detector.debugMode = enabled;
            }
        }

        Debug.Log($"TrafficLightManager: {(enabled ? "Enabled" : "Disabled")} debug mode for all detectors");
    }

    // Debug visualization
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Draw connections between detectors and traffic lights
        Gizmos.color = Color.cyan;
        foreach (var detector in allViolationDetectors)
        {
            if (detector != null && detector.trafficLight != null)
            {
                Gizmos.DrawLine(detector.transform.position, detector.trafficLight.transform.position);
            }
        }

        // Draw traffic light positions
        Gizmos.color = Color.red;
        foreach (var light in allTrafficLights)
        {
            if (light != null)
            {
                Gizmos.DrawWireSphere(light.transform.position, 2f);

                // Draw light state indicator
                switch (light.currentState)
                {
                    case TrafficLightController.LightState.Red:
                        Gizmos.color = Color.red;
                        break;
                    case TrafficLightController.LightState.Yellow:
                        Gizmos.color = Color.yellow;
                        break;
                    case TrafficLightController.LightState.Green:
                        Gizmos.color = Color.green;
                        break;
                }
                Gizmos.DrawSphere(light.transform.position + Vector3.up * 3f, 0.5f);
            }
        }

        // Draw detector positions
        Gizmos.color = Color.blue;
        foreach (var detector in allViolationDetectors)
        {
            if (detector != null)
            {
                Gizmos.DrawWireCube(detector.transform.position, Vector3.one * 2f);
            }
        }
    }

    // ADDED: Context menu helpers for testing
    [ContextMenu("Start All Traffic Lights")]
    public void MenuStartAllLights()
    {
        StartAllTrafficLights();
    }

    [ContextMenu("Stop All Traffic Lights")]
    public void MenuStopAllLights()
    {
        StopAllTrafficLights();
    }

    [ContextMenu("Set All Lights Red")]
    public void MenuSetAllRed()
    {
        SetAllTrafficLights(TrafficLightController.LightState.Red);
    }

    [ContextMenu("Set All Lights Green")]
    public void MenuSetAllGreen()
    {
        SetAllTrafficLights(TrafficLightController.LightState.Green);
    }

    [ContextMenu("Rediscover Components")]
    public void MenuRediscover()
    {
        DiscoverAllTrafficComponents();
        RegisterAllComponents();
        ConnectDetectorsToTrafficLights();
    }
}
