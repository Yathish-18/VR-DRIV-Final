using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class DrivingSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI trackNameText;
    [SerializeField] private TextMeshProUGUI weatherText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI lapTimeText;
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private Button backToMenuButton;

    [Header("Environment")]
    [SerializeField] private Light sunLight;
    [SerializeField] private Camera mainCamera;

    [Header("Race Settings")]
    [SerializeField] private float currentLapTime = 0f;
    [SerializeField] private bool raceActive = false;

    private void Start()
    {
        LoadPersistedData();
        SetupUI();
    }

    private void Update()
    {
        if (raceActive)
        {
            currentLapTime += Time.deltaTime;
            UpdateLapTimeUI(currentLapTime);
        }
    }

    private void LoadPersistedData()
    {
        if (GamePersistenceManager.Instance == null)
        {
            Debug.LogWarning("No GamePersistenceManager found!");
            return;
        }

        var persistenceManager = GamePersistenceManager.Instance;

        // Apply track data
        if (persistenceManager.HasTrackData())
        {
            var track = persistenceManager.GetSelectedTrack();
            if (trackNameText != null)
                trackNameText.SetText($"Track: {track.trackName}");
            Debug.Log($"Loaded track: {track.trackName}");
        }

        // Apply weather data
        if (persistenceManager.HasWeatherData())
        {
            var weather = persistenceManager.GetSelectedWeather();
            if (weatherText != null)
                weatherText.SetText($"Weather: {weather.weatherName}");

            // Apply weather effects
            RenderSettings.fogColor = weather.skyboxTint;
            RenderSettings.fogDensity = weather.fogDensity;

            Debug.Log($"Applied weather: {weather.weatherName}");
        }

        // Apply time data
        if (persistenceManager.HasTimeData())
        {
            var timeData = persistenceManager.GetSelectedTime();
            if (timeText != null)
                timeText.SetText($"Time: {timeData.timeName}");

            // Apply lighting
            if (sunLight != null)
            {
                sunLight.color = timeData.lightColor;
                sunLight.intensity = timeData.lightIntensity;
            }

            if (timeData.skyboxMaterial != null)
                RenderSettings.skybox = timeData.skyboxMaterial;

            Debug.Log($"Applied time setting: {timeData.timeName}");
        }

        // Display best time
        if (bestTimeText != null)
        {
            float bestTime = persistenceManager.bestLapTime;
            if (bestTime < float.MaxValue)
                bestTimeText.SetText($"Best: {bestTime:F2}s");
            else
                bestTimeText.SetText("Best: --:--");
        }
    }

    private void SetupUI()
    {
        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(BackToMenu);
    }

    // Call this to start the race
    public void StartRace()
    {
        raceActive = true;
        currentLapTime = 0f;
        Debug.Log("Race started!");
    }

    // Call this when race finishes
    public void OnRaceComplete(int position)
    {
        raceActive = false;

        if (GamePersistenceManager.Instance != null)
        {
            bool isNewRecord = currentLapTime < GamePersistenceManager.Instance.bestLapTime;
            GamePersistenceManager.Instance.UpdateRaceResults(currentLapTime, position, isNewRecord);

            Debug.Log($"Race completed! Time: {currentLapTime:F2}s, Position: {position}" +
                     (isNewRecord ? " (NEW RECORD!)" : ""));
        }
    }

    // Update lap time UI
    private void UpdateLapTimeUI(float lapTime)
    {
        if (lapTimeText != null)
        {
            int minutes = Mathf.FloorToInt(lapTime / 60f);
            int seconds = Mathf.FloorToInt(lapTime % 60f);
            int milliseconds = Mathf.FloorToInt((lapTime * 100f) % 100f);
            lapTimeText.SetText($"{minutes:00}:{seconds:00}.{milliseconds:00}");
        }
    }

    // Back to menu functionality
    public void BackToMenu()
    {
        SceneManager.LoadScene("TrackSelection"); // Replace with your track selection scene name
    }

    // Public methods that can be called from other scripts or UI
    [ContextMenu("Start Race")]
    public void StartRaceDebug() => StartRace();

    [ContextMenu("Finish Race (1st Place)")]
    public void FinishRaceDebug() => OnRaceComplete(1);
}
