using UnityEngine;
using NWH.VehiclePhysics2;

public class EnhancedSpeedViolationDetector : MonoBehaviour
{
    [Header("Vehicle Reference")]
    public VehicleController vehicleController;

    [Header("Speed Limit Settings")]
    public float currentSpeedLimit = 50f; // km/h
    public float speedTolerance = 5f; // km/h tolerance
    public float violationDuration = 2f; // Duration before violation
    public float violationCooldown = 3f; // Cooldown between violations

    [Header("Zone Detection")]
    [SerializeField] private string currentZone = "General";

    // Private variables
    private float currentSpeed;
    private bool isViolating = false;
    private float violationTimer = 0f;
    private float lastViolationTime = 0f;
    private int violationCount = 0;

    // Events
    public System.Action<float> OnSpeedViolation; // Passes speed over limit

    void Start()
    {
        if (vehicleController == null)
            vehicleController = FindObjectOfType<VehicleController>();

        if (vehicleController == null)
        {
            Debug.LogError("EnhancedSpeedViolationDetector: No VehicleController found!");
            return;
        }
    }

    void Update()
    {
        if (vehicleController == null) return;

        // Get speed from NWH vehicle
        currentSpeed = vehicleController.Speed * 3.6f; // Convert m/s to km/h
        CheckSpeedViolation();
    }

    void CheckSpeedViolation()
    {
        float speedOverLimit = currentSpeed - (currentSpeedLimit + speedTolerance);

        if (speedOverLimit > 0)
        {
            if (!isViolating)
            {
                violationTimer = 0f;
                isViolating = true;
            }
            else
            {
                violationTimer += Time.deltaTime;
                if (violationTimer >= violationDuration)
                {
                    if (Time.time - lastViolationTime >= violationCooldown)
                    {
                        RegisterViolation(speedOverLimit);
                        lastViolationTime = Time.time;
                    }
                }
            }
        }
        else
        {
            if (isViolating)
            {
                isViolating = false;
                violationTimer = 0f;
            }
        }
    }

    void RegisterViolation(float speedOverLimit)
    {
        violationCount++;
        Debug.Log($"SPEED VIOLATION #{violationCount} - Exceeded by {speedOverLimit:F1} km/h in {currentZone}");

        OnSpeedViolation?.Invoke(speedOverLimit);

        // Register with scoring system
        if (EnhancedDriverScoringSystem.Instance != null)
        {
            EnhancedDriverScoringSystem.Instance.RegisterSpeedingViolation(speedOverLimit);
        }
    }

    // Public methods for zone management
    public void SetSpeedLimit(float newLimit) => currentSpeedLimit = newLimit;

    public void SetCurrentZone(string zoneName)
    {
        currentZone = zoneName;

        // Auto-adjust speed limits based on zone
        switch (zoneName.ToLower())
        {
            case "school zone":
            case "school":
                SetSpeedLimit(25f);
                break;
            case "urban zone":
            case "urban":
                SetSpeedLimit(50f);
                break;
            case "highway":
                SetSpeedLimit(100f);
                break;
            case "parking":
                SetSpeedLimit(10f);
                break;
            default:
                SetSpeedLimit(50f);
                break;
        }
    }

    // Public getters
    public float GetCurrentSpeed() => currentSpeed;
    public float GetCurrentSpeedLimit() => currentSpeedLimit;
    public int GetViolationCount() => violationCount;
    public bool IsCurrentlyViolating() => isViolating;
    public string GetCurrentZone() => currentZone;
}
