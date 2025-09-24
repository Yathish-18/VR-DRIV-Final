using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

public class EnhancedDriverScoringDashboard : MonoBehaviour
{
    [Header("Final Score Display")]
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI percentageText;
    [SerializeField] private TextMeshProUGUI gradeText;
    [SerializeField] private Image gradeBackgroundImage;
    [SerializeField] private Image scoreCircleFill;

    [Header("Speedometer Dial")]
    [SerializeField] private Transform dialNeedle;
    [SerializeField] private float animationSpeed = 2f;
    [SerializeField] private bool smoothAnimation = true;
    [SerializeField] private AnimationCurve dialCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("YOUR EXACT NEEDLE RANGE")]
    [SerializeField] private float needleMinAngle = -219.512f;  // 0% score position
    [SerializeField] private float needleMaxAngle = -37f;      // 100% score position
    [SerializeField] private bool debugNeedleCalc = true;      // Show calculations

    [Header("Display-Only Metrics")]
    [SerializeField] private TextMeshProUGUI maxSpeedText;
    [SerializeField] private TextMeshProUGUI durationText;
    [SerializeField] private TextMeshProUGUI distanceText;

    [Header("Positive Metrics as Percentages")]
    [SerializeField] private TextMeshProUGUI smoothDrivingText;
    [SerializeField] private TextMeshProUGUI carHealthText;
    [SerializeField] private Slider smoothDrivingSlider;
    [SerializeField] private Slider carHealthSlider;

    [Header("Penalty System as Points")]
    [SerializeField] private TextMeshProUGUI trafficLightPenaltyText;
    [SerializeField] private TextMeshProUGUI lanePenaltyText;
    [SerializeField] private TextMeshProUGUI speedingPenaltyText;
    [SerializeField] private TextMeshProUGUI totalPenaltyText;

    [Header("UI Colors")]
    [SerializeField] private Color excellentColor = Color.green;
    [SerializeField] private Color goodColor = Color.yellow;
    [SerializeField] private Color averageColor = Color.gray;
    [SerializeField] private Color poorColor = Color.red;

    [Header("UI Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    [Header("Debug Settings")]
    [SerializeField] private bool showDebugLogs = true;

    // Dial Animation Variables
    private float currentDialAngle;
    private float targetDialAngle;
    private Coroutine dialAnimationCoroutine;
    private bool hasLoadedData = false;

    void Start()
    {
        if (showDebugLogs)
            Debug.Log("Dashboard UI: UI activated at session end - Loading final data...");

        DashboardDataProvider.CaptureSessionEndData();
        LoadAllDataFromProvider();
        SetupButtons();
        InitializeAndAnimateNeedle();

        if (showDebugLogs)
            Debug.Log("Dashboard UI: Initialization complete");
    }

    void LoadAllDataFromProvider()
    {
        if (hasLoadedData)
        {
            if (showDebugLogs)
                Debug.LogWarning("Dashboard UI: Data already loaded!");
            return;
        }

        if (DashboardDataProvider.HasStoredData())
        {
            var finalData = DashboardDataProvider.GetStoredData();

            if (showDebugLogs)
            {
                Debug.Log($"Dashboard UI: Loading final session data:");
                Debug.Log($"  Final Score: {finalData.finalScore:F1}/100");
                Debug.Log($"  Lane Penalty: {finalData.lanePenalty:F1} PTS");
                Debug.Log($"  Traffic Penalty: {finalData.trafficLightPenalty:F1} PTS");
                Debug.Log($"  Speed Penalty: {finalData.speedingPenalty:F1} PTS");
                Debug.Log($"  Total Penalty: {finalData.totalPenalty:F1} PTS");
            }

            UpdateAllUIFromData(finalData);
            hasLoadedData = true;
        }
        else
        {
            if (showDebugLogs)
                Debug.LogError("Dashboard UI: No stored data available!");
            SetDefaultValues();
        }
    }

    void UpdateAllUIFromData(DashboardDataProvider.DashboardData data)
    {
        // Basic metrics
        if (maxSpeedText != null)
            maxSpeedText.text = $"{data.maxSpeed:F1} km/hr";

        if (durationText != null)
        {
            int minutes = Mathf.FloorToInt(data.totalTime / 60f);
            int seconds = Mathf.FloorToInt(data.totalTime % 60f);
            durationText.text = $"{minutes:D2}:{seconds:D2}";
        }

        if (distanceText != null)
            distanceText.text = $"{data.totalDistance:F1}km";

        // Final Score Display
        if (finalScoreText != null)
        {
            int displayScore = Mathf.RoundToInt(data.finalScore);
            finalScoreText.text = $"{displayScore}/100";
            finalScoreText.color = GetScoreColor(displayScore);
        }

        if (percentageText != null)
            percentageText.text = $"{data.finalPercentage:F0}%";

        if (gradeText != null)
            gradeText.text = data.performanceGrade;

        if (gradeBackgroundImage != null)
            gradeBackgroundImage.color = GetScoreColor(data.finalScore);

        if (scoreCircleFill != null)
            scoreCircleFill.fillAmount = data.finalPercentage / 100f;

        // Positive Metrics
        if (smoothDrivingText != null)
        {
            smoothDrivingText.text = $"{data.smoothDrivingPercentage:F0}%";
            smoothDrivingText.color = GetMetricColor(data.smoothDrivingPercentage);
        }
        if (smoothDrivingSlider != null)
            smoothDrivingSlider.value = data.smoothDrivingPercentage / 100f;

        if (carHealthText != null)
        {
            carHealthText.text = $"{data.carHealthPercentage:F0}%";
            carHealthText.color = GetMetricColor(data.carHealthPercentage);
        }
        if (carHealthSlider != null)
            carHealthSlider.value = data.carHealthPercentage / 100f;

        // Penalty System
        if (trafficLightPenaltyText != null)
        {
            int penalty = Mathf.RoundToInt(data.trafficLightPenalty);
            trafficLightPenaltyText.text = penalty > 0 ? $"{penalty} PTS" : "0 PTS";
            trafficLightPenaltyText.color = penalty > 0 ? poorColor : excellentColor;
        }

        if (lanePenaltyText != null)
        {
            int penalty = Mathf.RoundToInt(data.lanePenalty);
            lanePenaltyText.text = penalty > 0 ? $"{penalty} PTS" : "0 PTS";
            lanePenaltyText.color = penalty > 0 ? poorColor : excellentColor;
        }

        if (speedingPenaltyText != null)
        {
            int penalty = Mathf.RoundToInt(data.speedingPenalty);
            speedingPenaltyText.text = penalty > 0 ? $"{penalty} PTS" : "0 PTS";
            speedingPenaltyText.color = penalty > 0 ? poorColor : excellentColor;
        }

        if (totalPenaltyText != null)
        {
            int totalPenalty = Mathf.RoundToInt(data.totalPenalty);
            totalPenaltyText.text = totalPenalty > 0 ? $"Total: {totalPenalty} PTS" : "Total: 0 PTS";
            totalPenaltyText.color = GetPenaltyColor(data.totalPenalty);
        }
    }

    void InitializeAndAnimateNeedle()
    {
        if (dialNeedle == null)
        {
            Debug.LogError("Dashboard UI: Dial needle Transform not assigned!");
            return;
        }

        // Start at 0% score position
        currentDialAngle = GetAngleForScore(0f);
        SetNeedleAngle(currentDialAngle);

        // Animate to final score
        if (DashboardDataProvider.HasStoredData())
        {
            var data = DashboardDataProvider.GetStoredData();
            AnimateNeedleToScore(data.finalScore);
        }
        else
        {
            AnimateNeedleToScore(0f);
        }
    }

    // FIXED: Simple direct calculation for your range
    float GetAngleForScore(float scorePercent)
    {
        // Clamp input
        scorePercent = Mathf.Clamp(scorePercent, 0f, 100f);

        // Your exact angle range
        float minAngle = needleMinAngle;  // -219.512° for 0%
        float maxAngle = needleMaxAngle;  // -37° for 100%

        // Direct calculation
        float scoreRatio = scorePercent / 100f;
        float angleRange = maxAngle - minAngle;  // -37 - (-219.512) = 182.512°
        float resultAngle = minAngle + (scoreRatio * angleRange);

        if (debugNeedleCalc)
        {
            Debug.Log($"=== NEEDLE CALCULATION ===");
            Debug.Log($"Score: {scorePercent:F1}%");
            Debug.Log($"Min Angle (0%): {minAngle:F1}°");
            Debug.Log($"Max Angle (100%): {maxAngle:F1}°");
            Debug.Log($"Angle Range: {angleRange:F1}°");
            Debug.Log($"Score Ratio: {scoreRatio:F3}");
            Debug.Log($"Result: {minAngle:F1} + ({scoreRatio:F3} × {angleRange:F1}) = {resultAngle:F1}°");
        }

        return resultAngle;
    }

    void AnimateNeedleToScore(float scorePercent)
    {
        targetDialAngle = GetAngleForScore(scorePercent);

        if (showDebugLogs)
        {
            Debug.Log($"Animating needle to {scorePercent:F1}% → {targetDialAngle:F1}°");
        }

        if (smoothAnimation && dialAnimationCoroutine == null)
        {
            dialAnimationCoroutine = StartCoroutine(SmoothNeedleAnimation());
        }
        else
        {
            currentDialAngle = targetDialAngle;
            SetNeedleAngle(currentDialAngle);
        }
    }

    IEnumerator SmoothNeedleAnimation()
    {
        float startAngle = currentDialAngle;
        float elapsedTime = 0f;
        float duration = 1f / animationSpeed;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            float curvedProgress = dialCurve.Evaluate(progress);

            // Simple interpolation
            currentDialAngle = startAngle + ((targetDialAngle - startAngle) * curvedProgress);
            SetNeedleAngle(currentDialAngle);

            yield return null;
        }

        currentDialAngle = targetDialAngle;
        SetNeedleAngle(currentDialAngle);
        dialAnimationCoroutine = null;

        if (showDebugLogs)
            Debug.Log($"Animation complete - Needle at {currentDialAngle:F1}°");
    }

    void SetNeedleAngle(float angle)
    {
        if (dialNeedle != null)
        {
            dialNeedle.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    void SetDefaultValues()
    {
        if (finalScoreText != null) finalScoreText.text = "0/100";
        if (percentageText != null) percentageText.text = "0%";
        if (gradeText != null) gradeText.text = "F";
        if (trafficLightPenaltyText != null) trafficLightPenaltyText.text = "0 PTS";
        if (lanePenaltyText != null) lanePenaltyText.text = "0 PTS";
        if (speedingPenaltyText != null) speedingPenaltyText.text = "0 PTS";
        if (totalPenaltyText != null) totalPenaltyText.text = "Total: 0 PTS";
        AnimateNeedleToScore(0f);
    }

    #region Color Helpers
    Color GetScoreColor(float score)
    {
        if (score >= 90f) return excellentColor;
        else if (score >= 75f) return goodColor;
        else if (score >= 60f) return averageColor;
        else return poorColor;
    }

    Color GetMetricColor(float percentage)
    {
        return GetScoreColor(percentage);
    }

    Color GetPenaltyColor(float penalty)
    {
        if (penalty <= 0f) return excellentColor;
        else if (penalty <= 10f) return goodColor;
        else if (penalty <= 25f) return averageColor;
        else return poorColor;
    }
    #endregion

    #region Button Setup
    void SetupButtons()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(() => {
                DashboardDataProvider.ClearStoredData();
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });

        if (menuButton != null)
            menuButton.onClick.AddListener(() => SceneManager.LoadScene("MainMenu"));
    }
    #endregion

    #region Testing Methods
    [ContextMenu("Test 0% Score")]
    public void Test0Percent()
    {
        if (Application.isPlaying)
        {
            Debug.Log("Testing 0% score...");
            AnimateNeedleToScore(0f);
        }
    }

    [ContextMenu("Test 57% Score")]
    public void Test57Percent()
    {
        if (Application.isPlaying)
        {
            Debug.Log("Testing 57% score...");
            AnimateNeedleToScore(57f);
        }
    }

    [ContextMenu("Test 100% Score")]
    public void Test100Percent()
    {
        if (Application.isPlaying)
        {
            Debug.Log("Testing 100% score...");
            AnimateNeedleToScore(100f);
        }
    }

    [ContextMenu("Show Expected Angles")]
    public void ShowExpectedAngles()
    {
        Debug.Log("=== EXPECTED NEEDLE ANGLES ===");
        Debug.Log($"0% Score   → {GetAngleForScore(0f):F1}°");
        Debug.Log($"25% Score  → {GetAngleForScore(25f):F1}°");
        Debug.Log($"50% Score  → {GetAngleForScore(50f):F1}°");
        Debug.Log($"57% Score  → {GetAngleForScore(57f):F1}°");
        Debug.Log($"75% Score  → {GetAngleForScore(75f):F1}°");
        Debug.Log($"100% Score → {GetAngleForScore(100f):F1}°");
    }
    #endregion
}
