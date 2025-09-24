using UnityEngine;
using NWH.VehiclePhysics2;
using System.Collections.Generic;

public class EnhancedLaneViolationDetector : MonoBehaviour
{
    public enum LaneMode
    {
        SingleLane,  // No lane violation checking
        DoubleLane   // Lane violation checking enabled
    }

    public enum RaycastAxis
    {
        PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ
    }

    [Header("Lane Detection Settings")]
    [SerializeField] private Transform raycastOrigin;
    [SerializeField] private LaneMode currentLaneMode = LaneMode.DoubleLane;
    [SerializeField] private RaycastAxis raycastDirection = RaycastAxis.NegativeX;
    [SerializeField] private float raycastDistance = 5f;
    [SerializeField] private string barrierTag = "RoadBarrier";
    [SerializeField] private float checkInterval = 0.1f;
    [SerializeField] private float violationCooldown = 1f;

    [Header("Lane Thresholds")]
    [SerializeField] private float validLaneDistance = 3f;
    [SerializeField] private float warningDistance = 4f;

    [Header("No Barrier Violation Settings")]
    [SerializeField] private bool enableNoBarrierViolations = true; // NEW: Enable violations when no barriers present
    [SerializeField] private float noBarrierViolationInterval = 2f; // Generate violation every 2 seconds when no barriers
    [SerializeField] private float noBarrierViolationDuration = 1f; // Each violation lasts 1 second

    [Header("Penalty Settings")]
    [SerializeField] private float minViolationDuration = 0.5f;
    [SerializeField] private float penaltyPerSecond = 1f;
    [SerializeField] private float maxSingleViolationPenalty = 5f;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true; // Enable for testing
    [SerializeField] private bool showGizmos = true;

    // Private variables
    private VehicleController vehicleController;
    private bool isInValidLane = true;
    private bool wasInValidLane = true;
    private float lastCheckTime = 0f;
    private float lastViolationTime = 0f;
    private float currentViolationDuration = 0f;
    private float currentBarrierDistance = 0f;
    private bool isViolationActive = false;

    // NEW: No barrier violation tracking
    private float lastNoBarrierViolationTime = 0f;
    private bool hasDetectedBarriers = false;

    // Events
    public System.Action<float> OnLaneViolation;
    public System.Action<float> OnDistanceUpdate;
    public System.Action<float> OnViolationPenaltyApplied;

    void Start()
    {
        InitializeDetector();
    }

    void InitializeDetector()
    {
        vehicleController = FindObjectOfType<VehicleController>();
        if (vehicleController == null)
        {
            Debug.LogError("EnhancedLaneViolationDetector: No VehicleController found!");
            enabled = false;
            return;
        }

        if (raycastOrigin == null)
            raycastOrigin = vehicleController.transform;

        if (showDebugLogs)
        {
            Debug.Log($"Lane Detector Initialized - Mode: {currentLaneMode}");
            Debug.Log($"No Barrier Violations: {(enableNoBarrierViolations ? "ENABLED" : "DISABLED")}");
        }
    }

    void Update()
    {
        if (Time.time < lastCheckTime + checkInterval) return;
        lastCheckTime = Time.time;

        if (ShouldCheckLaneViolations())
        {
            CheckLaneStatus();

            // NEW: Check for no-barrier violations
            if (enableNoBarrierViolations)
            {
                CheckNoBarrierViolations();
            }
        }
    }

    bool ShouldCheckLaneViolations()
    {
        return currentLaneMode == LaneMode.DoubleLane;
    }

    void CheckLaneStatus()
    {
        wasInValidLane = isInValidLane;
        DetectLaneWithRaycast();

        // Handle violation state changes
        if (!isInValidLane)
        {
            if (!isViolationActive)
            {
                // Start new violation
                isViolationActive = true;
                currentViolationDuration = 0f;
                if (showDebugLogs)
                    Debug.Log($"Lane Violation Started - Barrier Distance: {currentBarrierDistance:F1}m");
            }
            // Accumulate violation duration
            currentViolationDuration += checkInterval;
        }
        else
        {
            // End violation if it was active
            if (isViolationActive)
            {
                EndViolationTracking();
            }
        }
    }

    // NEW: Generate violations when no barriers are detected
    void CheckNoBarrierViolations()
    {
        // Check if we've detected any barriers recently
        if (currentBarrierDistance == 0f)
        {
            // No barriers detected - this means improper lane usage
            if (Time.time - lastNoBarrierViolationTime >= noBarrierViolationInterval)
            {
                // Generate a violation
                if (showDebugLogs)
                    Debug.Log("No Barrier Violation: Generating penalty for driving without lane boundaries");

                ApplyLaneViolationPenalty(noBarrierViolationDuration);
                lastNoBarrierViolationTime = Time.time;
            }
        }
        else
        {
            hasDetectedBarriers = true;
        }
    }

    void DetectLaneWithRaycast()
    {
        DetectBarrierDistance();

        // FIXED: Proper lane logic
        if (enableNoBarrierViolations && !hasDetectedBarriers)
        {
            // If no barriers detected and no-barrier violations enabled, consider as violation
            isInValidLane = false;
        }
        else
        {
            // Normal logic: car is in correct lane if barrier detected within valid distance
            isInValidLane = (currentBarrierDistance > 0f && currentBarrierDistance <= validLaneDistance);
        }

        if (showDebugLogs && isInValidLane != wasInValidLane)
        {
            Debug.Log($"Lane Status: {(isInValidLane ? "Valid" : "VIOLATION")} - Distance: {currentBarrierDistance:F1}m");
        }
    }

    void DetectBarrierDistance()
    {
        Vector3 rayStart = raycastOrigin.position;
        Vector3 rayDirection = GetRaycastDirection();

        bool hitBarrier = Physics.Raycast(rayStart, rayDirection, out RaycastHit raycastHit, raycastDistance);

        if (hitBarrier && raycastHit.collider.CompareTag(barrierTag))
        {
            currentBarrierDistance = raycastHit.distance;
            hasDetectedBarriers = true;
            OnDistanceUpdate?.Invoke(currentBarrierDistance);
        }
        else
        {
            // No barrier detected
            currentBarrierDistance = 0f;
            OnDistanceUpdate?.Invoke(0f);
        }
    }

    void EndViolationTracking()
    {
        if (!isViolationActive) return;
        isViolationActive = false;

        // Apply penalty if violation lasted long enough
        if (currentViolationDuration >= minViolationDuration)
        {
            if (Time.time - lastViolationTime >= violationCooldown)
            {
                ApplyLaneViolationPenalty(currentViolationDuration);
                lastViolationTime = Time.time;
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"Lane Violation Ended - Duration: {currentViolationDuration:F2}s");
        }

        currentViolationDuration = 0f;
    }

    void ApplyLaneViolationPenalty(float violationDuration)
    {
        // Calculate penalty points
        float penaltyPoints = Mathf.Min(violationDuration * penaltyPerSecond, maxSingleViolationPenalty);

        // Trigger events
        OnLaneViolation?.Invoke(violationDuration);
        OnViolationPenaltyApplied?.Invoke(penaltyPoints);

        // FIXED: Properly register with scoring system
        if (EnhancedDriverScoringSystem.Instance != null)
        {
            EnhancedDriverScoringSystem.Instance.RegisterLaneViolation(violationDuration);
            if (showDebugLogs)
                Debug.Log($"Lane Violation Registered: {penaltyPoints:F1} points for {violationDuration:F2}s violation");
        }
        else
        {
            Debug.LogError("Lane Detector: No scoring system found!");
        }
    }

    Vector3 GetRaycastDirection()
    {
        switch (raycastDirection)
        {
            case RaycastAxis.PositiveX: return transform.right;
            case RaycastAxis.NegativeX: return -transform.right;
            case RaycastAxis.PositiveY: return transform.up;
            case RaycastAxis.NegativeY: return -transform.up;
            case RaycastAxis.PositiveZ: return transform.forward;
            case RaycastAxis.NegativeZ: return -transform.forward;
            default: return -transform.right;
        }
    }

    #region Public API
    public bool IsInValidLane => isInValidLane;
    public float GetCurrentViolationDuration() => currentViolationDuration;
    public float GetBarrierDistance() => currentBarrierDistance;
    public bool IsViolationActive => isViolationActive;

    [ContextMenu("Test Lane Violation")]
    public void TestLaneViolation()
    {
        if (Application.isPlaying)
        {
            Debug.Log("Testing lane violation...");
            ApplyLaneViolationPenalty(2f);
        }
    }

    [ContextMenu("Test Current Status")]
    public void TestCurrentStatus()
    {
        DetectBarrierDistance();
        Debug.Log($"=== Lane Detector Status ===\n" +
                 $"Lane Valid: {isInValidLane}\n" +
                 $"Barrier Distance: {currentBarrierDistance:F2}m\n" +
                 $"No Barrier Violations: {enableNoBarrierViolations}\n" +
                 $"Has Detected Barriers: {hasDetectedBarriers}\n" +
                 $"Violation Active: {isViolationActive}\n" +
                 $"Violation Duration: {currentViolationDuration:F2}s");
    }
    #endregion

    void OnDrawGizmos()
    {
        if (!showGizmos || raycastOrigin == null) return;

        Vector3 pos = raycastOrigin.position;
        Vector3 rayDir = GetRaycastDirection();

        // Color based on lane status
        Gizmos.color = isInValidLane ? Color.green : Color.red;
        Gizmos.DrawWireSphere(pos, 0.5f);

        // Draw raycast
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(pos, rayDir * raycastDistance);

        // Draw barrier hit point if detected
        if (currentBarrierDistance > 0f)
        {
            Vector3 hitPoint = pos + rayDir * currentBarrierDistance;
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(hitPoint, 0.2f);
        }
    }
}
