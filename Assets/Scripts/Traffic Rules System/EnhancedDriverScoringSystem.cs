using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using NWH.VehiclePhysics2;
using NWH.VehiclePhysics2.Damage;

public class EnhancedDriverScoringSystem : MonoBehaviour
{
    [Header("Core Scoring Configuration")]
    [SerializeField] private float baseScore = 100f;
    [SerializeField] private bool enableAdvancedFeatures = true;
    [SerializeField] private bool enableTimeBasedPenalties = true;
    [SerializeField] private bool enableRecoveryBonus = true;

    [Header("Component References")]
    [SerializeField] private VehicleController vehicleController;
    [SerializeField] private DamageHandler damageHandler;
    [SerializeField] private EnhancedLaneViolationDetector laneDetector;

    [Header("Debug Settings")]
    [SerializeField] private bool enableSmoothDrivingDebug = true;

    [Header("Sampling Settings")]
    [SerializeField] private float samplingInterval = 0.2f; // FIXED: Sample every 0.2 seconds (5 Hz)
    [SerializeField] private int maxSampleHistory = 500; // FIXED: Keep last 500 samples (100 seconds of history)

    // NEW SCORING FRAMEWORK (100 Points Total)
    // Positive Metrics (50% of total score)
    private float smoothDrivingScore = 100f; // Will contribute 25 points (25% weight)
    private float carHealthScore = 100f; // Will contribute 25 points (25% weight)

    // Penalty System (50% of total score deduction)
    private float trafficLightPenalty = 0f; // Max -20 points
    private float lanePenalty = 0f; // Max -15 points
    private float speedingPenalty = 0f; // Max -15 points
    private float turnIndicatorPenalty = 0f; // Max -10 points

    // Advanced Features
    private Dictionary<ViolationType, float> lastViolationTime = new Dictionary<ViolationType, float>();
    private Dictionary<ViolationType, int> consecutiveViolations = new Dictionary<ViolationType, int>();
    private Dictionary<ViolationType, float> recoveryTimer = new Dictionary<ViolationType, float>();

    // Time-based tracking
    private float totalDrivingTime = 0f;
    private float laneViolationTime = 0f;
    private float goodBehaviorTime = 0f;
    private const float RECOVERY_TIME_THRESHOLD = 30f; // 30 seconds of good behavior

    // FIXED: Proper time-based sampling
    private List<float> speedHistory = new List<float>();
    private List<bool> smoothnessHistory = new List<bool>(); // Track smooth/not smooth decisions
    private float lastSpeed = 0f;
    private float lastSampleTime = 0f; // FIXED: Last time we took a sample
    private int smoothDrivingSamples = 1;
    private int totalDrivingSamples = 1;
    private bool hasStartedMoving = false;
    private float firstMovementTime = 0f;

    // Current score data
    private DriverScoreData currentScoreData = new DriverScoreData();

    // Events
    public System.Action<DriverScoreData> OnScoreUpdated;

    public static EnhancedDriverScoringSystem Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeSystem();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        FindComponents();
        ResetScoring();
        InitializePerfectStart();
    }

    void Update()
    {
        if (vehicleController != null)
        {
            // FIXED: Only update smooth driving at controlled intervals
            CheckSmoothDrivingSampling();
            UpdateCarHealthMetrics();
            UpdateTimeBasedSystems();
            CalculateFinalScore();
        }
    }

    void InitializeSystem()
    {
        // Initialize violation tracking
        foreach (ViolationType type in System.Enum.GetValues(typeof(ViolationType)))
        {
            lastViolationTime[type] = 0f;
            consecutiveViolations[type] = 0;
            recoveryTimer[type] = 0f;
        }

        Debug.Log($"Enhanced Driver Scoring System: Initialized with {1f / samplingInterval:F1} Hz sampling rate");
    }

    void InitializePerfectStart()
    {
        // Perfect initialization
        smoothDrivingScore = 100f;
        carHealthScore = 100f;
        hasStartedMoving = false;
        lastSpeed = 0f;
        lastSampleTime = Time.time;
        smoothDrivingSamples = 1;
        totalDrivingSamples = 1;

        CalculateFinalScore();

        if (enableSmoothDrivingDebug)
            Debug.Log($"🎯 PERFECT START: 100% score - sampling every {samplingInterval}s ({1f / samplingInterval:F1} Hz)");
    }

    void FindComponents()
    {
        if (vehicleController == null)
            vehicleController = FindObjectOfType<VehicleController>();

        if (damageHandler == null && vehicleController != null)
            damageHandler = vehicleController.GetComponent<DamageHandler>();

        if (laneDetector == null)
            laneDetector = FindObjectOfType<EnhancedLaneViolationDetector>();
    }

    #region FIXED: Time-Based Sampling System
    void CheckSmoothDrivingSampling()
    {
        float currentTime = Time.time;

        // FIXED: Only sample at controlled intervals
        if (currentTime - lastSampleTime >= samplingInterval)
        {
            TakeSmoothDrivingSample();
            lastSampleTime = currentTime;
        }
    }

    void TakeSmoothDrivingSample()
    {
        float currentSpeed = vehicleController.Speed * 3.6f; // km/h

        // Track first movement
        if (!hasStartedMoving && currentSpeed > 0.5f)
        {
            hasStartedMoving = true;
            firstMovementTime = Time.time;
            lastSpeed = currentSpeed;
            if (enableSmoothDrivingDebug)
                Debug.Log($"🚗 FIRST MOVEMENT: Speed {currentSpeed:F1} km/h - {1f / samplingInterval:F1} Hz sampling started");
            return;
        }

        // Skip if stationary
        if (!hasStartedMoving && currentSpeed <= 0.5f)
        {
            return;
        }

        // Issue 4: Skip sampling when nearly stopped (e.g. at traffic light) – don't count toward smooth driving
        const float minSpeedToSample = 2f; // km/h
        if (currentSpeed < minSpeedToSample)
        {
            return;
        }

        // Take a driving sample
        totalDrivingSamples++;
        bool isSmoothSample = EvaluateSmoothDriving(currentSpeed);

        // Add to history
        smoothnessHistory.Add(isSmoothSample);
        speedHistory.Add(currentSpeed);

        // FIXED: Maintain reasonable history size
        if (smoothnessHistory.Count > maxSampleHistory)
        {
            // Remove oldest sample and adjust counters
            bool removedSample = smoothnessHistory[0];
            smoothnessHistory.RemoveAt(0);
            speedHistory.RemoveAt(0);
            totalDrivingSamples--;

            if (removedSample)
                smoothDrivingSamples--;
        }

        // Update counters
        if (isSmoothSample)
        {
            smoothDrivingSamples++;
        }

        // Update last speed for next sample
        lastSpeed = currentSpeed;

        // Calculate score
        CalculateSmoothDrivingScore();
    }

    bool EvaluateSmoothDriving(float currentSpeed)
    {
        // Skip evaluation for first sample or if we don't have previous speed
        if (totalDrivingSamples <= 1 || lastSpeed < 0)
        {
            return true; // Give benefit of doubt for first sample
        }

        bool isSmoothSample = true;
        string debugReason = "";

        float speedChange = Mathf.Abs(currentSpeed - lastSpeed);

        // FIXED: Realistic thresholds for time-based sampling

        // 1. Speed change per sampling interval
        float speedChangeRate = speedChange / samplingInterval; // km/h per sampling interval
        bool smoothSpeedChange = speedChangeRate <= 20f; // Max 20 km/h change per interval (at 0.2s = 100 km/h/s max)
        if (!smoothSpeedChange)
        {
            isSmoothSample = false;
            debugReason += $"FastSpeedChange:{speedChangeRate:F1}>20 ";
        }

        // 2. Acceleration check – Issue 4: Allow higher deceleration (emergency braking) but stricter acceleration
        float accelerationMsSquared = (speedChangeRate / 3.6f); // Convert to m/s per sampling interval, then to m/s²
        bool isDeceleration = currentSpeed < lastSpeed;
        float accelThreshold = isDeceleration ? 6f : 4f; // 6 m/s² braking OK (emergency stop), 4 m/s² for acceleration
        bool smoothAcceleration = accelerationMsSquared <= accelThreshold;

        if (!smoothAcceleration)
        {
            isSmoothSample = false;
            debugReason += $"HardAccel:{accelerationMsSquared:F2}>{accelThreshold}m/s² ";
        }

        // 3. Reasonable speed check
        bool reasonableSpeed = currentSpeed <= 150f;
        if (!reasonableSpeed)
        {
            isSmoothSample = false;
            debugReason += $"ExtremeSpeed:{currentSpeed:F1}>150 ";
        }

        // 4. Check for erratic patterns in recent history
        if (speedHistory.Count >= 3)
        {
            // Check last 3 samples for oscillating pattern
            float s1 = speedHistory[speedHistory.Count - 3];
            float s2 = speedHistory[speedHistory.Count - 2];
            float s3 = speedHistory[speedHistory.Count - 1];
            float s4 = currentSpeed;

            // Detect oscillation: up-down-up or down-up-down pattern
            bool isOscillating = ((s1 < s2 && s2 > s3 && s3 < s4) || (s1 > s2 && s2 < s3 && s3 > s4)) &&
                                (Mathf.Abs(s2 - s1) > 5f && Mathf.Abs(s3 - s2) > 5f && Mathf.Abs(s4 - s3) > 5f);

            if (isOscillating)
            {
                isSmoothSample = false;
                debugReason += "Oscillating ";
            }
        }

        // Debug logging for non-smooth samples
        if (!isSmoothSample && enableSmoothDrivingDebug)
        {
            Debug.Log($"⚠️ NOT SMOOTH: Speed {currentSpeed:F1} km/h, Change {speedChange:F1} km/h/{samplingInterval}s, " +
                     $"Accel {accelerationMsSquared:F2} m/s² - {debugReason.Trim()}");
        }

        return isSmoothSample;
    }

    void CalculateSmoothDrivingScore()
    {
        if (totalDrivingSamples > 0)
        {
            float rawPercentage = (float)smoothDrivingSamples / totalDrivingSamples * 100f;
            smoothDrivingScore = Mathf.Clamp(rawPercentage, 0f, 100f);

            // FIXED: Log at reasonable intervals (every 25 samples instead of every 100)
            if (enableSmoothDrivingDebug && totalDrivingSamples % 25 == 0)
            {
                float sessionTime = Time.time - firstMovementTime;
                Debug.Log($"📊 SMOOTH DRIVING SCORE: {smoothDrivingScore:F1}% " +
                         $"({smoothDrivingSamples}/{totalDrivingSamples} samples over {sessionTime:F1}s) " +
                         $"[Rate: {totalDrivingSamples / sessionTime:F1} samples/s]");
            }
        }
    }

    void UpdateCarHealthMetrics()
    {
        if (damageHandler != null)
        {
            float damagePercentage = damageHandler.Damage;
            carHealthScore = Mathf.Clamp((1f - damagePercentage) * 100f, 0f, 100f);
        }
        else
        {
            carHealthScore = 100f;
        }
    }
    #endregion

    #region Penalty System (50 Points Max Deduction)
    public void RegisterTrafficLightViolation(ViolationType violationType, float basePoints = 10f)
    {
        // Issue 2: First-offense reduction – 5 pts for first, 10 for repeat (borderline cases less harsh)
        float adjustedBase = consecutiveViolations[ViolationType.TrafficLight] == 0 ? 5f : basePoints;
        float penaltyPoints = CalculateAdvancedPenalty(violationType, adjustedBase);
        trafficLightPenalty = Mathf.Min(trafficLightPenalty + penaltyPoints, 20f);
        UpdateViolationTracking(violationType);
        Debug.Log($"Traffic Light Violation: +{penaltyPoints} penalty (Total: {trafficLightPenalty}/20)");
    }

    public void RegisterLaneViolation(float duration)
    {
        // Issue 2: Stronger penalty for longer violations – 1 pt/sec first 5 sec, 1.5 pt/sec after
        float penaltyPoints = Mathf.Min(duration, 5f) * 1f + Mathf.Max(0f, duration - 5f) * 1.5f;
        lanePenalty = Mathf.Min(lanePenalty + penaltyPoints, 15f);
        laneViolationTime += duration;
        UpdateViolationTracking(ViolationType.LaneViolation);
        Debug.Log($"Lane Violation: +{penaltyPoints:F1} penalty (Total: {lanePenalty}/15)");
    }

    public void RegisterSpeedingViolation(float speedOverLimit)
    {
        float penaltyPoints = CalculateSpeedingPenalty(speedOverLimit);
        speedingPenalty = Mathf.Min(speedingPenalty + penaltyPoints, 15f);
        UpdateViolationTracking(ViolationType.Speeding);
        Debug.Log($"Speeding Violation: +{penaltyPoints} penalty (Total: {speedingPenalty}/15)");
    }

    public void RegisterTurnIndicatorViolation(ViolationType violationType, float basePoints = 3f)
    {
        // Issue 2: 3 pts base (more meaningful), 12 pts max (better scaling in city driving)
        float penaltyPoints = CalculateAdvancedPenalty(violationType, basePoints);
        turnIndicatorPenalty = Mathf.Min(turnIndicatorPenalty + penaltyPoints, 12f);
        UpdateViolationTracking(violationType);
        Debug.Log($"Turn Indicator Violation: +{penaltyPoints} penalty (Total: {turnIndicatorPenalty}/12)");
    }

    float CalculateSpeedingPenalty(float speedOverLimit)
    {
        if (speedOverLimit <= 10f)
            return 1f;
        else if (speedOverLimit <= 20f)
            return 3f;
        else
            return 5f;
    }
    #endregion

    #region Advanced Features
    float CalculateAdvancedPenalty(ViolationType type, float basePoints)
    {
        float finalPenalty = basePoints;

        if (consecutiveViolations[type] > 1)
        {
            float multiplier = Mathf.Pow(1.5f, consecutiveViolations[type] - 1);
            finalPenalty *= multiplier;
        }

        if (enableTimeBasedPenalties)
        {
            float timeSinceLastViolation = Time.time - lastViolationTime[type];
            if (timeSinceLastViolation < 10f)
            {
                finalPenalty *= 1.5f;
            }
        }

        return finalPenalty;
    }

    void UpdateViolationTracking(ViolationType type)
    {
        float currentTime = Time.time;

        if (currentTime - lastViolationTime[type] < 30f)
        {
            consecutiveViolations[type]++;
        }
        else
        {
            consecutiveViolations[type] = 1;
        }

        lastViolationTime[type] = currentTime;
        recoveryTimer[type] = 0f;
    }

    void UpdateTimeBasedSystems()
    {
        totalDrivingTime += Time.deltaTime;

        bool hasRecentViolations = false;
        foreach (var violationType in lastViolationTime.Keys)
        {
            if (Time.time - lastViolationTime[violationType] < RECOVERY_TIME_THRESHOLD)
            {
                hasRecentViolations = true;
                break;
            }
        }

        if (!hasRecentViolations)
        {
            goodBehaviorTime += Time.deltaTime;

            if (enableRecoveryBonus && goodBehaviorTime >= RECOVERY_TIME_THRESHOLD)
            {
                ApplyRecoveryBonus();
                goodBehaviorTime = 0f;
            }
        }
        else
        {
            goodBehaviorTime = 0f;
        }
    }

    void ApplyRecoveryBonus()
    {
        float recoveryAmount = 2f;

        if (trafficLightPenalty > 0)
        {
            trafficLightPenalty = Mathf.Max(0f, trafficLightPenalty - recoveryAmount * 0.4f);
        }

        if (lanePenalty > 0)
        {
            lanePenalty = Mathf.Max(0f, lanePenalty - recoveryAmount * 0.3f);
        }

        if (speedingPenalty > 0)
        {
            speedingPenalty = Mathf.Max(0f, speedingPenalty - recoveryAmount * 0.3f);
        }

        if (turnIndicatorPenalty > 0)
        {
            turnIndicatorPenalty = Mathf.Max(0f, turnIndicatorPenalty - recoveryAmount * 0.2f);
        }

        Debug.Log($"Recovery Bonus Applied: -{recoveryAmount} total penalty reduction");
    }
    #endregion

    #region Final Score Calculation
    void CalculateFinalScore()
    {
        currentScoreData.baseScore = baseScore;

        currentScoreData.smoothDrivingPoints = (smoothDrivingScore / 100f) * 25f;
        currentScoreData.carHealthPoints = (carHealthScore / 100f) * 25f;

        currentScoreData.trafficLightPenalty = trafficLightPenalty;
        currentScoreData.lanePenalty = lanePenalty;
        currentScoreData.speedingPenalty = speedingPenalty;
        currentScoreData.turnIndicatorPenalty = turnIndicatorPenalty;

        float positiveScore = currentScoreData.smoothDrivingPoints + currentScoreData.carHealthPoints;
        float totalPenalty = currentScoreData.trafficLightPenalty + currentScoreData.lanePenalty + currentScoreData.speedingPenalty + currentScoreData.turnIndicatorPenalty;

        currentScoreData.finalScore = Mathf.Clamp(positiveScore + 50f - totalPenalty, 0f, 100f);
        currentScoreData.finalPercentage = currentScoreData.finalScore;
        currentScoreData.grade = GetGradeFromScore(currentScoreData.finalPercentage);

        currentScoreData.smoothDrivingPercentage = smoothDrivingScore;
        currentScoreData.carHealthPercentage = carHealthScore;
        currentScoreData.totalPenalty = totalPenalty;

        OnScoreUpdated?.Invoke(currentScoreData);
    }

    DriverGrade GetGradeFromScore(float score)
    {
        if (score >= 90f) return DriverGrade.A_Plus;
        else if (score >= 80f) return DriverGrade.A;
        else if (score >= 70f) return DriverGrade.B;
        else if (score >= 60f) return DriverGrade.C;
        else if (score >= 50f) return DriverGrade.D;
        else return DriverGrade.F;
    }
    #endregion

    #region Public API
    public void ResetScoring()
    {
        smoothDrivingScore = 100f;
        carHealthScore = 100f;
        trafficLightPenalty = 0f;
        lanePenalty = 0f;
        speedingPenalty = 0f;
        turnIndicatorPenalty = 0f;

        speedHistory.Clear();
        smoothnessHistory.Clear();

        smoothDrivingSamples = 1;
        totalDrivingSamples = 1;
        hasStartedMoving = false;
        lastSpeed = 0f;
        lastSampleTime = Time.time;

        foreach (var key in lastViolationTime.Keys.ToList())
        {
            lastViolationTime[key] = 0f;
            consecutiveViolations[key] = 0;
            recoveryTimer[key] = 0f;
        }

        currentScoreData = new DriverScoreData();
        InitializePerfectStart();

        Debug.Log($"🔄 SCORING RESET: {1f / samplingInterval:F1} Hz sampling rate");
    }

    public DriverScoreData GetCurrentScore()
    {
        return currentScoreData;
    }

    // ADDED: Method to change sampling rate during runtime
    [ContextMenu("Set High Sampling Rate (10 Hz)")]
    public void SetHighSamplingRate()
    {
        samplingInterval = 0.1f;
        Debug.Log("Sampling rate changed to 10 Hz");
    }

    [ContextMenu("Set Normal Sampling Rate (5 Hz)")]
    public void SetNormalSamplingRate()
    {
        samplingInterval = 0.2f;
        Debug.Log("Sampling rate changed to 5 Hz");
    }

    [ContextMenu("Set Low Sampling Rate (2 Hz)")]
    public void SetLowSamplingRate()
    {
        samplingInterval = 0.5f;
        Debug.Log("Sampling rate changed to 2 Hz");
    }
    #endregion

    // Data structures
    public enum ViolationType
    {
        TrafficLight,
        LaneViolation,
        Speeding,
        TurnIndicator
    }

    public enum DriverGrade
    {
        F, D, C, B, A, A_Plus
    }

    [System.Serializable]
    public class DriverScoreData
    {
        public float baseScore = 100f;
        public float smoothDrivingPoints = 0f;
        public float carHealthPoints = 0f;
        public float trafficLightPenalty = 0f;
        public float lanePenalty = 0f;
        public float speedingPenalty = 0f;
        public float turnIndicatorPenalty = 0f;
        public float totalPenalty = 0f;
        public float finalScore = 0f;
        public float finalPercentage = 0f;
        public DriverGrade grade = DriverGrade.F;
        public float smoothDrivingPercentage = 100f;
        public float carHealthPercentage = 100f;
    }
}
