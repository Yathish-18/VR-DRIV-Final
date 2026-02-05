using UnityEngine;
using NWH.VehiclePhysics2;
using NWH.VehiclePhysics2.Damage;

public class DashboardDataProvider : MonoBehaviour
{
    [Header("Component References")]
    [SerializeField] private VehicleController vehicleController;
    [SerializeField] private DamageHandler damageHandler;
    [SerializeField] private EnhancedLaneViolationDetector laneDetector;

    [Header("Settings")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool enableDetailedDebug = true; // NEW: Detailed debugging

    // Static data storage
    private static DashboardData storedData = new DashboardData();
    private static bool hasData = false;

    // Runtime tracking
    private float startTime;
    private float totalDistance = 0f;
    private float maxSpeedReached = 0f;
    private float totalSpeedSum = 0f;
    private int speedReadingCount = 0;
    private Vector3 lastPosition;
    private bool isInitialized = false;
    private float lastTrackTime = 0f;

    // Enhanced metrics tracking
    private float totalTime = 0f;
    private float smoothDrivingTime = 0f;
    private float laneConsistencyTime = 0f;

    // ADDED: Reference to scoring system for final data capture
    private EnhancedDriverScoringSystem scoringSystem;

    [System.Serializable]
    public class DashboardData
    {
        // Basic metrics
        public float maxSpeed;
        public float totalDistance;
        public float totalTime;
        public float averageSpeed;

        // ADDED: Complete scoring data from EnhancedDriverScoringSystem
        [Header("Final Score Data")]
        public float finalScore = 0f;
        public float finalPercentage = 0f;
        public string performanceGrade = "F";

        [Header("Positive Metrics")]
        public float smoothDrivingPercentage = 100f;
        public float carHealthPercentage = 100f;
        public float smoothDrivingPoints = 0f;
        public float carHealthPoints = 0f;

        [Header("Penalty Data")]
        public float trafficLightPenalty = 0f;
        public float lanePenalty = 0f;
        public float speedingPenalty = 0f;
        public float totalPenalty = 0f;

        [Header("System Data")]
        public float baseScore = 100f;

        // Legacy compatibility
        public float fuelEfficiency = 100f;
        public float roadAdherence = 100f;
        public float laneConsistency = 100f;
        public float speedCompliance = 100f;
        public float vehicleCare = 100f;

        // Performance indicators (counts)
        public int trafficViolations = 0;
        public int speedingIncidents = 0;
        public int laneViolations = 0;

        [Header("Reaction Time (signal change after raycast hit = car near)")]
        public float avgReactionTimeSec = -1f;
        public float worstReactionTimeSec = -1f;
    }

    void Start()
    {
        InitializeTracking();

        // ADDED: Log initial state immediately
        if (enableDetailedDebug)
        {
            Invoke(nameof(LogInitialState), 1f); // Log after 1 second to let everything initialize
        }
    }

    void InitializeTracking()
    {
        // Find components
        if (vehicleController == null)
            vehicleController = FindObjectOfType<VehicleController>();
        if (damageHandler == null && vehicleController != null)
            damageHandler = vehicleController.GetComponent<DamageHandler>();
        if (laneDetector == null)
            laneDetector = FindObjectOfType<EnhancedLaneViolationDetector>();

        // ADDED: Get scoring system reference
        scoringSystem = EnhancedDriverScoringSystem.Instance;

        if (vehicleController == null)
        {
            if (enableDebugLogs)
                Debug.LogError("DashboardDataProvider: No VehicleController found!");
            return;
        }

        startTime = Time.time;
        lastPosition = vehicleController.transform.position;
        isInitialized = true;

        if (enableDebugLogs)
            Debug.Log("Dashboard Data Provider: Initialized successfully - Tracking session data");
    }

    void Update()
    {
        if (!isInitialized || vehicleController == null) return;
        TrackPerformanceMetrics();
    }

    void TrackPerformanceMetrics()
    {
        float currentTime = Time.time;
        float deltaTime = currentTime - lastTrackTime;
        if (deltaTime < 0.1f) return; // Sample every 0.1 seconds

        lastTrackTime = currentTime;

        // Track basic metrics
        TrackBasicMetrics();

        // Track enhanced metrics
        TrackEnhancedMetrics();

        // Update stored data continuously during gameplay
        UpdateStoredData();
    }

    void TrackBasicMetrics()
    {
        // Speed tracking
        float currentSpeed = vehicleController.Speed * 3.6f; // km/h
        totalSpeedSum += currentSpeed;
        speedReadingCount++;

        if (currentSpeed > maxSpeedReached)
            maxSpeedReached = currentSpeed;

        // Distance tracking
        Vector3 currentPosition = vehicleController.transform.position;
        float distanceDelta = Vector3.Distance(lastPosition, currentPosition);
        totalDistance += distanceDelta;
        lastPosition = currentPosition;

        // Time tracking
        totalTime = Time.time - startTime;
    }

    void TrackEnhancedMetrics()
    {
        // Lane consistency tracking
        if (laneDetector != null && laneDetector.IsInValidLane)
        {
            laneConsistencyTime += 0.1f;
        }

        // Smooth driving tracking (basic implementation)
        float currentSpeed = vehicleController.Speed * 3.6f;
        // This is a simplified implementation - the main scoring system handles detailed smooth driving
        if (currentSpeed > 0 && currentSpeed < 80) // Reasonable speed range
        {
            smoothDrivingTime += 0.1f;
        }
    }

    void UpdateStoredData()
    {
        // Basic metrics
        storedData.maxSpeed = maxSpeedReached;
        storedData.totalDistance = totalDistance / 1000f; // Convert to km
        storedData.totalTime = totalTime;
        storedData.averageSpeed = speedReadingCount > 0 ? totalSpeedSum / speedReadingCount : 0f;

        // Legacy metrics (for compatibility)
        storedData.laneConsistency = totalTime > 0 ? (laneConsistencyTime / totalTime) * 100f : 100f;
        storedData.roadAdherence = 100f; // Default - enhanced system handles this
        storedData.speedCompliance = 100f; // Default - enhanced system handles this

        // Vehicle care (damage-based)
        if (damageHandler != null)
        {
            storedData.vehicleCare = (1f - damageHandler.Damage) * 100f;
        }
        else
        {
            storedData.vehicleCare = 100f;
        }

        // ADDED: Get FINAL score from enhanced scoring system
        if (scoringSystem != null)
        {
            var currentScore = scoringSystem.GetCurrentScore();

            // Copy ALL scoring data to DashboardData
            storedData.finalScore = currentScore.finalScore;
            storedData.finalPercentage = currentScore.finalPercentage;
            storedData.performanceGrade = currentScore.grade.ToString().Replace("_Plus", "+");

            // Positive metrics
            storedData.smoothDrivingPercentage = currentScore.smoothDrivingPercentage;
            storedData.carHealthPercentage = currentScore.carHealthPercentage;
            storedData.smoothDrivingPoints = currentScore.smoothDrivingPoints;
            storedData.carHealthPoints = currentScore.carHealthPoints;

            // CRITICAL: Penalty data (this is what was missing!)
            storedData.trafficLightPenalty = currentScore.trafficLightPenalty;
            storedData.lanePenalty = currentScore.lanePenalty;
            storedData.speedingPenalty = currentScore.speedingPenalty;
            storedData.totalPenalty = currentScore.totalPenalty;

            // System data
            storedData.baseScore = currentScore.baseScore;
        }

        // Reaction time from car controller (raycast stores there; we read here)
        var carController = FindObjectOfType<CentralizedCarController>();
        if (carController != null)
        {
            storedData.avgReactionTimeSec = carController.GetAverageReactionTime();
            storedData.worstReactionTimeSec = carController.GetWorstReactionTime();
        }
        else
        {
            storedData.avgReactionTimeSec = -1f;
            storedData.worstReactionTimeSec = -1f;
        }

        hasData = true;

        // UPDATED: Enhanced detailed debug logging
        if (enableDebugLogs && enableDetailedDebug && Time.time % 5f < 0.1f) // Log every 5 seconds
        {
            LogDetailedScoringBreakdown();
        }
        else if (enableDebugLogs && Time.time % 10f < 0.1f) // Basic log every 10 seconds
        {
            Debug.Log($"Dashboard Tracking - Final Score: {storedData.finalScore:F1}/100, " +
                     $"Lane Penalty: {storedData.lanePenalty:F1}, " +
                     $"Traffic Penalty: {storedData.trafficLightPenalty:F1}, " +
                     $"Speeding Penalty: {storedData.speedingPenalty:F1}");
        }
    }

    // NEW: Comprehensive detailed logging method
    void LogDetailedScoringBreakdown()
    {
        if (scoringSystem == null) return;

        var score = scoringSystem.GetCurrentScore();

        // Calculate the expected formula manually for verification
        float expectedPositiveScore = score.smoothDrivingPoints + score.carHealthPoints;
        float expectedTotalPenalty = score.trafficLightPenalty + score.lanePenalty + score.speedingPenalty;
        float expectedFinalScore = Mathf.Clamp(expectedPositiveScore + 50f - expectedTotalPenalty, 0f, 100f);

        //string debugMessage = $"\n========== DETAILED SCORING BREAKDOWN ==========\n" +
        //                     $"🎯 FINAL SCORE: {score.finalScore:F2}/100 (Grade: {score.grade})\n" +
        //                     $"\n📊 POSITIVE METRICS (50% Weight):\n" +
        //                     $"   • Smooth Driving: {score.smoothDrivingPercentage:F1}% → {score.smoothDrivingPoints:F2} points (25% weight)\n" +
        //                     $"   • Car Health: {score.carHealthPercentage:F1}% → {score.carHealthPoints:F2} points (25% weight)\n" +
        //                     $"   • Total Positive: {expectedPositiveScore:F2} points\n" +
        //                     $"\n🚫 PENALTY SYSTEM (Max 50 Points Deduction):\n" +
        //                     $"   • Traffic Light: -{score.trafficLightPenalty:F2} points (max -20)\n" +
        //                     $"   • Lane Violations: -{score.lanePenalty:F2} points (max -15)\n" +
        //                     $"   • Speeding: -{score.speedingPenalty:F2} points (max -15)\n" +
        //                     $"   • Total Penalties: -{expectedTotalPenalty:F2} points\n" +
        //                     $"\n🧮 CALCULATION VERIFICATION:\n" +
        //                     $"   • Formula: Positive({expectedPositiveScore:F2}) + Base(50) - Penalties({expectedTotalPenalty:F2})\n" +
        //                     $"   • Expected: {expectedFinalScore:F2}\n" +
        //                     $"   • Actual: {score.finalScore:F2}\n" +
        //                     $"   • Match: {(Mathf.Abs(expectedFinalScore - score.finalScore) < 0.01f ? "✅ YES" : "❌ NO")}\n" +
        //                     $"\n🔧 SYSTEM STATUS:\n" +
        //                     $"   • Base Score: {score.baseScore:F1}\n" +
        //                     $"   • Vehicle Speed: {(vehicleController != null ? vehicleController.Speed * 3.6f : 0):F1} km/h\n" +
        //                     $"   • Session Time: {totalTime:F1}s\n" +
        //                     $"================================================";

        //Debug.Log(debugMessage);
    }

    // NEW: Log initial state to identify startup issues
    void LogInitialState()
    {
        if (!enableDetailedDebug) return;

        Debug.Log($"\n========== INITIAL STATE DEBUG ==========\n" +
                 $"🔍 COMPONENT STATUS:\n" +
                 $"   • VehicleController: {(vehicleController != null ? "✅ Found" : "❌ Missing")}\n" +
                 $"   • DamageHandler: {(damageHandler != null ? "✅ Found" : "❌ Missing")}\n" +
                 $"   • LaneDetector: {(laneDetector != null ? "✅ Found" : "❌ Missing")}\n" +
                 $"   • ScoringSystem: {(scoringSystem != null ? "✅ Found" : "❌ Missing")}\n" +
                 $"\n📋 INITIAL VALUES:\n" +
                 $"   • Expected Score: 100/100\n" +
                 $"   • Vehicle Damage: {(damageHandler != null ? damageHandler.Damage * 100f : 0):F1}%\n" +
                 $"==========================================");

        // Force a detailed breakdown immediately
        if (scoringSystem != null)
        {
            LogDetailedScoringBreakdown();
        }
    }

    // ADDED: Force final data capture when session ends
    public static void CaptureSessionEndData()
    {
        var provider = FindObjectOfType<DashboardDataProvider>();
        if (provider != null)
        {
            provider.UpdateStoredData(); // Force final update
            if (provider.enableDebugLogs)
            {
                Debug.Log("Dashboard Data Provider: Final session data captured");
                Debug.Log($"Final Penalties - Lane: {storedData.lanePenalty:F1}, " +
                         $"Traffic: {storedData.trafficLightPenalty:F1}, " +
                         $"Speed: {storedData.speedingPenalty:F1}");
            }
        }
    }

    // Public API
    public static DashboardData GetStoredData()
    {
        return hasData ? storedData : new DashboardData();
    }

    public static bool HasStoredData()
    {
        return hasData;
    }

    public static void ClearStoredData()
    {
        storedData = new DashboardData();
        hasData = false;
        var carController = FindObjectOfType<CentralizedCarController>();
        if (carController != null)
            carController.ClearReactionData();
    }

    // Public getters for real-time data
    public float GetCurrentSpeed()
    {
        return vehicleController != null ? vehicleController.Speed * 3.6f : 0f;
    }

    public float GetTotalDistance()
    {
        return totalDistance / 1000f; // km
    }

    public float GetSessionTime()
    {
        return totalTime;
    }

    public float GetAverageSpeed()
    {
        return speedReadingCount > 0 ? totalSpeedSum / speedReadingCount : 0f;
    }

    // Reaction time (from car controller)
    public float GetAvgReactionTimeSec()
    {
        var carController = FindObjectOfType<CentralizedCarController>();
        return carController != null ? carController.GetAverageReactionTime() : -1f;
    }
    public float GetWorstReactionTimeSec()
    {
        var carController = FindObjectOfType<CentralizedCarController>();
        return carController != null ? carController.GetWorstReactionTime() : -1f;
    }

    // UPDATED: Enhanced penalty debugging
    public void LogCurrentPenalties()
    {
        if (scoringSystem != null)
        {
            LogDetailedScoringBreakdown();
        }
    }

    // NEW: Manual trigger for detailed debug
    [ContextMenu("Log Detailed Scoring")]
    public void ManualLogDetailed()
    {
        LogDetailedScoringBreakdown();
    }
}
