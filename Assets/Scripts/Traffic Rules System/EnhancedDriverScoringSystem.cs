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
    [SerializeField] private float samplingInterval = 0.2f;
    [SerializeField] private int maxSampleHistory = 500;

    // =========================================================================
    //  SCORING FRAMEWORK  —  100 Points Total, no artificial floor
    // =========================================================================
    //
    //  POSITIVE METRICS  (max 100 pts)
    //    Smooth Driving  50 pts  (was 25 — now full half of the score)
    //    Car Health      50 pts  (was 25 — now full half of the score)
    //
    //  PENALTY DEDUCTIONS  (deducted from positive score directly)
    //    Traffic Light   max -20 pts
    //    Lane Violation  max -15 pts
    //    Speeding        max -15 pts
    //    Turn Indicator  max -10 pts
    //    Total cap:      max -50 pts
    //
    //  FORMULA:
    //    finalScore = Clamp(smoothPts + carHealthPts - totalPenalty, 0, 100)
    //
    //  No hardcoded base offset (+50f removed).
    //  Perfect drive with no violations = 50+50-0 = 100/100.
    //  Worst possible drive = 0+0-50 → clamped to 0/100.
    // =========================================================================

    // Positive metrics (0–100% each, weighted to 50 pts each)
    private float smoothDrivingScore = 100f;
    private float carHealthScore = 100f;

    // Penalty accumulators
    private float trafficLightPenalty = 0f;   // max 20
    private float lanePenalty = 0f;   // max 15
    private float speedingPenalty = 0f;   // max 15
    private float turnIndicatorPenalty = 0f;   // max 10

    // Violation tracking
    private Dictionary<ViolationType, float> lastViolationTime = new Dictionary<ViolationType, float>();
    private Dictionary<ViolationType, int> consecutiveViolations = new Dictionary<ViolationType, int>();
    private Dictionary<ViolationType, float> recoveryTimer = new Dictionary<ViolationType, float>();

    // Time-based tracking
    private float totalDrivingTime = 0f;
    private float laneViolationTime = 0f;
    private float goodBehaviorTime = 0f;
    private const float RECOVERY_TIME_THRESHOLD = 30f;

    // Smooth driving sampling
    private List<float> speedHistory = new List<float>();
    private List<bool> smoothnessHistory = new List<bool>();
    private float lastSpeed = 0f;
    private float lastSampleTime = 0f;
    private int smoothDrivingSamples = 1;
    private int totalDrivingSamples = 1;
    private bool hasStartedMoving = false;
    private float firstMovementTime = 0f;

    private DriverScoreData currentScoreData = new DriverScoreData();
    public System.Action<DriverScoreData> OnScoreUpdated;

    public static EnhancedDriverScoringSystem Instance { get; private set; }

    // =========================================================================
    //  LIFECYCLE
    // =========================================================================

    void Awake()
    {
        if (Instance == null) { Instance = this; InitializeSystem(); }
        else Destroy(gameObject);
    }

    void Start()
    {
        FindComponents();
        ResetScoring();      // sets smoothDrivingScore = carHealthScore = 100
        InitializePerfectStart();
    }

    void Update()
    {
        if (vehicleController == null) return;
        CheckSmoothDrivingSampling();
        UpdateCarHealthMetrics();
        UpdateTimeBasedSystems();
        CalculateFinalScore();
    }

    void InitializeSystem()
    {
        foreach (ViolationType t in System.Enum.GetValues(typeof(ViolationType)))
        {
            lastViolationTime[t] = 0f;
            consecutiveViolations[t] = 0;
            recoveryTimer[t] = 0f;
        }
        Debug.Log($"[ScoringSystem] Init — {1f / samplingInterval:F1} Hz sampling");
    }

    void InitializePerfectStart()
    {
        smoothDrivingScore = 100f;
        carHealthScore = 100f;
        hasStartedMoving = false;
        lastSpeed = 0f;
        lastSampleTime = Time.time;
        smoothDrivingSamples = 1;
        totalDrivingSamples = 1;

        // Force a proper first calculation so currentScoreData is never stale
        CalculateFinalScore();

        if (enableSmoothDrivingDebug)
            Debug.Log($"[ScoringSystem] Perfect start — score={currentScoreData.finalScore:F1}/100");
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

    // =========================================================================
    //  SMOOTH DRIVING  —  time-based sampling
    // =========================================================================

    void CheckSmoothDrivingSampling()
    {
        if (Time.time - lastSampleTime >= samplingInterval)
        {
            TakeSmoothDrivingSample();
            lastSampleTime = Time.time;
        }
    }

    void TakeSmoothDrivingSample()
    {
        float spd = vehicleController.Speed * 3.6f;

        if (!hasStartedMoving && spd > 0.5f)
        {
            hasStartedMoving = true;
            firstMovementTime = Time.time;
            lastSpeed = spd;
            return;
        }

        if (!hasStartedMoving || spd < 2f) return;

        totalDrivingSamples++;
        bool smooth = EvaluateSmoothDriving(spd);

        smoothnessHistory.Add(smooth);
        speedHistory.Add(spd);

        if (smoothnessHistory.Count > maxSampleHistory)
        {
            if (smoothnessHistory[0]) smoothDrivingSamples--;
            smoothnessHistory.RemoveAt(0);
            speedHistory.RemoveAt(0);
            totalDrivingSamples--;
        }

        if (smooth) smoothDrivingSamples++;
        lastSpeed = spd;
        CalculateSmoothDrivingScore();
    }

    bool EvaluateSmoothDriving(float spd)
    {
        if (totalDrivingSamples <= 1) return true;

        bool smooth = true;
        string reason = "";

        float change = Mathf.Abs(spd - lastSpeed);
        float changeRate = change / samplingInterval;

        if (changeRate > 20f) { smooth = false; reason += $"SpeedJump:{changeRate:F1} "; }

        float accel = changeRate / 3.6f;
        bool decelerating = spd < lastSpeed;
        float threshold = decelerating ? 6f : 4f;
        if (accel > threshold) { smooth = false; reason += $"HardAccel:{accel:F2}>{threshold} "; }

        if (spd > 150f) { smooth = false; reason += $"ExtremeSpeed:{spd:F1} "; }

        if (speedHistory.Count >= 3)
        {
            float s1 = speedHistory[speedHistory.Count - 3];
            float s2 = speedHistory[speedHistory.Count - 2];
            float s3 = speedHistory[speedHistory.Count - 1];
            bool osc = ((s1 < s2 && s2 > s3 && s3 < spd) || (s1 > s2 && s2 < s3 && s3 > spd))
                       && Mathf.Abs(s2 - s1) > 5f && Mathf.Abs(s3 - s2) > 5f && Mathf.Abs(spd - s3) > 5f;
            if (osc) { smooth = false; reason += "Oscillating "; }
        }

        if (!smooth && enableSmoothDrivingDebug)
            Debug.Log($"[ScoringSystem] ⚠ NOT SMOOTH {spd:F1}km/h Δ{change:F1} — {reason.Trim()}");

        return smooth;
    }

    void CalculateSmoothDrivingScore()
    {
        if (totalDrivingSamples > 0)
        {
            smoothDrivingScore = Mathf.Clamp((float)smoothDrivingSamples / totalDrivingSamples * 100f, 0f, 100f);

            if (enableSmoothDrivingDebug && totalDrivingSamples % 25 == 0)
            {
                float elapsed = Time.time - firstMovementTime;
                Debug.Log($"[ScoringSystem] Smooth={smoothDrivingScore:F1}% " +
                          $"({smoothDrivingSamples}/{totalDrivingSamples} @ {elapsed:F1}s)");
            }
        }
    }

    // =========================================================================
    //  CAR HEALTH
    // =========================================================================

    void UpdateCarHealthMetrics()
    {
        carHealthScore = damageHandler != null
            ? Mathf.Clamp((1f - damageHandler.Damage) * 100f, 0f, 100f)
            : 100f;
    }

    // =========================================================================
    //  PENALTIES
    // =========================================================================

    public void RegisterTrafficLightViolation(ViolationType violationType, float basePoints = 10f)
    {
        float adjusted = consecutiveViolations[ViolationType.TrafficLight] == 0 ? 5f : basePoints;
        float pts = CalculateAdvancedPenalty(violationType, adjusted);
        trafficLightPenalty = Mathf.Min(trafficLightPenalty + pts, 20f);
        UpdateViolationTracking(violationType);
        Debug.Log($"[ScoringSystem] Traffic violation +{pts:F1} (total {trafficLightPenalty:F1}/20)");
    }

    public void RegisterLaneViolation(float duration)
    {
        float pts = Mathf.Min(duration, 5f) * 1f + Mathf.Max(0f, duration - 5f) * 1.5f;
        lanePenalty = Mathf.Min(lanePenalty + pts, 15f);
        laneViolationTime += duration;
        UpdateViolationTracking(ViolationType.LaneViolation);
        Debug.Log($"[ScoringSystem] Lane violation +{pts:F1} (total {lanePenalty:F1}/15)");
    }

    public void RegisterSpeedingViolation(float speedOverLimit)
    {
        float pts = CalculateSpeedingPenalty(speedOverLimit);
        speedingPenalty = Mathf.Min(speedingPenalty + pts, 15f);
        UpdateViolationTracking(ViolationType.Speeding);
        Debug.Log($"[ScoringSystem] Speeding +{pts:F1} (total {speedingPenalty:F1}/15)");
    }

    public void RegisterTurnIndicatorViolation(ViolationType violationType, float basePoints = 3f)
    {
        float pts = CalculateAdvancedPenalty(violationType, basePoints);
        turnIndicatorPenalty = Mathf.Min(turnIndicatorPenalty + pts, 10f);
        UpdateViolationTracking(violationType);
        Debug.Log($"[ScoringSystem] Turn indicator +{pts:F1} (total {turnIndicatorPenalty:F1}/10)");
    }

    float CalculateSpeedingPenalty(float over)
        => over <= 10f ? 1f : over <= 20f ? 3f : 5f;

    // =========================================================================
    //  ADVANCED FEATURES
    // =========================================================================

    float CalculateAdvancedPenalty(ViolationType type, float basePoints)
    {
        float pts = basePoints;
        if (consecutiveViolations[type] > 1)
            pts *= Mathf.Pow(1.5f, consecutiveViolations[type] - 1);
        if (enableTimeBasedPenalties && Time.time - lastViolationTime[type] < 10f)
            pts *= 1.5f;
        return pts;
    }

    void UpdateViolationTracking(ViolationType type)
    {
        float t = Time.time;
        consecutiveViolations[type] = (t - lastViolationTime[type] < 30f)
            ? consecutiveViolations[type] + 1 : 1;
        lastViolationTime[type] = t;
        recoveryTimer[type] = 0f;
    }

    void UpdateTimeBasedSystems()
    {
        totalDrivingTime += Time.deltaTime;

        bool recentViolation = lastViolationTime.Values.Any(t => Time.time - t < RECOVERY_TIME_THRESHOLD);

        if (!recentViolation)
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
        const float r = 2f;
        trafficLightPenalty = Mathf.Max(0f, trafficLightPenalty - r * 0.4f);
        lanePenalty = Mathf.Max(0f, lanePenalty - r * 0.3f);
        speedingPenalty = Mathf.Max(0f, speedingPenalty - r * 0.3f);
        turnIndicatorPenalty = Mathf.Max(0f, turnIndicatorPenalty - r * 0.2f);
        Debug.Log($"[ScoringSystem] Recovery bonus applied (-{r} total penalty)");
    }

    // =========================================================================
    //  FINAL SCORE  —  no +50 base offset
    // =========================================================================

    void CalculateFinalScore()
    {
        currentScoreData.baseScore = baseScore;

        // Positive metrics: each contributes up to 50 pts (50% each)
        currentScoreData.smoothDrivingPoints = (smoothDrivingScore / 100f) * 50f;
        currentScoreData.carHealthPoints = (carHealthScore / 100f) * 50f;

        // Penalties (unchanged caps: traffic 20, lane 15, speeding 15, turn 10)
        currentScoreData.trafficLightPenalty = trafficLightPenalty;
        currentScoreData.lanePenalty = lanePenalty;
        currentScoreData.speedingPenalty = speedingPenalty;
        currentScoreData.turnIndicatorPenalty = turnIndicatorPenalty;

        float positive = currentScoreData.smoothDrivingPoints + currentScoreData.carHealthPoints;
        float totalPenalty = trafficLightPenalty + lanePenalty + speedingPenalty + turnIndicatorPenalty;

        // Perfect drive: 50+50-0 = 100.  No violations and perfect driving = 100/100.
        // Worst drive:   0+0-50 → clamped to 0.
        currentScoreData.finalScore = Mathf.Clamp(positive - totalPenalty, 0f, 100f);
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
        if (score >= 80f) return DriverGrade.A;
        if (score >= 70f) return DriverGrade.B;
        if (score >= 60f) return DriverGrade.C;
        if (score >= 50f) return DriverGrade.D;
        return DriverGrade.F;
    }

    // =========================================================================
    //  PUBLIC API
    // =========================================================================

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
        laneViolationTime = 0f;
        goodBehaviorTime = 0f;
        totalDrivingTime = 0f;

        foreach (var key in lastViolationTime.Keys.ToList())
        {
            lastViolationTime[key] = 0f;
            consecutiveViolations[key] = 0;
            recoveryTimer[key] = 0f;
        }

        currentScoreData = new DriverScoreData();
        Debug.Log("[ScoringSystem] Reset complete");
    }

    public DriverScoreData GetCurrentScore() => currentScoreData;

    [ContextMenu("Set High Sampling Rate (10 Hz)")]
    public void SetHighSamplingRate() { samplingInterval = 0.1f; }
    [ContextMenu("Set Normal Sampling Rate (5 Hz)")]
    public void SetNormalSamplingRate() { samplingInterval = 0.2f; }
    [ContextMenu("Set Low Sampling Rate (2 Hz)")]
    public void SetLowSamplingRate() { samplingInterval = 0.5f; }

    // =========================================================================
    //  DATA STRUCTURES
    // =========================================================================

    public enum ViolationType { TrafficLight, LaneViolation, Speeding, TurnIndicator }
    public enum DriverGrade { F, D, C, B, A, A_Plus }

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