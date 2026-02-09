using UnityEngine;
using NWH.VehiclePhysics2;

/// <summary>
/// Detects when the player turns without using the correct indicator (blinker).
/// Uses steering input and blinker state from VehicleController (InputProvider1).
/// </summary>
public class TurnIndicatorViolationDetector : MonoBehaviour
{
    [Header("Vehicle Reference")]
    [SerializeField] private VehicleController vehicleController;

    [Header("Detection Settings")]
    [Tooltip("Steering magnitude beyond this = turning (0–1, e.g. 0.2 = 20% steer)")]
    [Range(0.05f, 0.5f)]
    public float steeringThreshold = 0.2f;
    [Tooltip("Minimum speed (km/h) to count violations – ignore when stationary")]
    public float minSpeedKmh = 5f;
    [Tooltip("Seconds between violations to avoid spam")]
    public float violationCooldown = 3f;
    [Tooltip("Penalty points per violation (default 3)")]
    public float penaltyPerViolation = 3f;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    private float _lastViolationTime;

    void Start()
    {
        if (vehicleController == null)
            vehicleController = FindObjectOfType<VehicleController>();

        if (vehicleController == null)
            Debug.LogError("TurnIndicatorViolationDetector: No VehicleController found!");
    }

    void Update()
    {
        if (vehicleController == null) return;

        float speedKmh = vehicleController.Speed * 3.6f;
        if (speedKmh < minSpeedKmh) return;

        float steering = vehicleController.input?.Steering ?? 0f;
        bool leftBlinker = vehicleController.input?.LeftBlinker ?? false;
        bool rightBlinker = vehicleController.input?.RightBlinker ?? false;

        // Turning left: steering < -threshold, need left blinker
        if (steering < -steeringThreshold && !leftBlinker)
        {
            RegisterViolation("left");
            return;
        }

        // Turning right: steering > threshold, need right blinker
        if (steering > steeringThreshold && !rightBlinker)
        {
            RegisterViolation("right");
        }
    }

    void RegisterViolation(string direction)
    {
        if (Time.time - _lastViolationTime < violationCooldown) return;

        _lastViolationTime = Time.time;

        if (EnhancedDriverScoringSystem.Instance != null)
        {
            EnhancedDriverScoringSystem.Instance.RegisterTurnIndicatorViolation(
                EnhancedDriverScoringSystem.ViolationType.TurnIndicator,
                penaltyPerViolation);
        }

        if (enableDebugLogs)
            Debug.Log($"TURN INDICATOR VIOLATION: Turned {direction} without indicator");
    }
}
