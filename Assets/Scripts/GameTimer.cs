using UnityEngine;
using TMPro;

public class GameTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private bool startOnAwake = false;
    [SerializeField] private bool countUp = true; // true = count up, false = count down
    [SerializeField] private float startTime = 0f;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private string timeFormat = "mm:ss";

    private float currentTime;
    private bool isRunning = false;
    private bool isPaused = false;

    // Events
    public System.Action<float> OnTimeChanged;
    public System.Action OnTimerStarted;
    public System.Action OnTimerStopped;
    public System.Action OnTimerPaused;
    public System.Action OnTimerResumed;

    void Start()
    {
        currentTime = startTime;
        UpdateTimerDisplay();

        if (startOnAwake)
            StartTimer();
    }

    void Update()
    {
        if (isRunning && !isPaused)
        {
            if (countUp)
                currentTime += Time.deltaTime;
            else
                currentTime -= Time.deltaTime;

            // Clamp time to non-negative for countdown
            if (!countUp && currentTime < 0)
            {
                currentTime = 0;
                StopTimer();
            }

            UpdateTimerDisplay();
            OnTimeChanged?.Invoke(currentTime);
        }
    }

    public void StartTimer()
    {
        isRunning = true;
        isPaused = false;
        OnTimerStarted?.Invoke();
        Debug.Log("Timer started");
    }

    public void StopTimer()
    {
        isRunning = false;
        isPaused = false;
        OnTimerStopped?.Invoke();
        Debug.Log($"Timer stopped at {GetFormattedTime()}");
    }

    public void PauseTimer()
    {
        if (isRunning)
        {
            isPaused = true;
            OnTimerPaused?.Invoke();
        }
    }

    public void ResumeTimer()
    {
        if (isRunning && isPaused)
        {
            isPaused = false;
            OnTimerResumed?.Invoke();
        }
    }

    public void RestartTimer()
    {
        currentTime = startTime;
        isRunning = false;
        isPaused = false;
        UpdateTimerDisplay();
        StartTimer();
    }

    public void ResetTimer()
    {
        currentTime = startTime;
        isRunning = false;
        isPaused = false;
        UpdateTimerDisplay();
    }

    void UpdateTimerDisplay()
    {
        if (timerText != null)
        {
            timerText.text = GetFormattedTime();
        }
    }

    public string GetFormattedTime()
    {
        int minutes = Mathf.FloorToInt(currentTime / 60);
        int seconds = Mathf.FloorToInt(currentTime % 60);

        switch (timeFormat)
        {
            case "mm:ss":
                return $"{minutes:D2}:{seconds:D2}";
            case "ss":
                return $"{Mathf.FloorToInt(currentTime)}";
            case "mm:ss.f":
                int milliseconds = Mathf.FloorToInt((currentTime % 1) * 10);
                return $"{minutes:D2}:{seconds:D2}.{milliseconds}";
            default:
                return $"{minutes:D2}:{seconds:D2}";
        }
    }

    // Public getters
    public float GetCurrentTime() => currentTime;
    public bool IsTimerRunning() => isRunning && !isPaused;
    public bool IsTimerPaused() => isPaused;
    public void SetTime(float time) => currentTime = time;
}
