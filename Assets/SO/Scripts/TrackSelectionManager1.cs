using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

public class TrackSelectionManager : MonoBehaviour
{
    // Singleton instance for DontDestroyOnLoad
    public static TrackSelectionManager Instance { get; private set; }

    [Header("Game Database")]
    [SerializeField] private RacingGameDatabaseSO gameDatabase;

    [Header("Selection State")]
    [SerializeField] private int currentTrackIndex = 0;
    [SerializeField] private int selectedWeatherIndex = 0;
    [SerializeField] private int selectedTimeIndex = 0;

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI trackNameText;
    [SerializeField] private TextMeshProUGUI trackNumberText;
    [SerializeField] private TextMeshProUGUI trackLengthText;
    [SerializeField] private TextMeshProUGUI totalTurnsText;
    [SerializeField] private Image countryFlagImage;
    [SerializeField] private Image trackLayoutImage;
    [SerializeField] private Image trackPreviewImage;

    [Header("Navigation Buttons")]
    [SerializeField] private Button previousTrackButton;
    [SerializeField] private Button nextTrackButton;
    [SerializeField] private Button driveButton;

    [Header("Session Setup Buttons")]
    [SerializeField] private Button[] weatherButtons;
    [SerializeField] private Button[] timeOfDayButtons;

    [Header("Status Display")]
    [SerializeField] private TextMeshProUGUI timeStatusText;
    [SerializeField] private TextMeshProUGUI weatherStatusText;
    [SerializeField] private Image timeStatusIcon;
    [SerializeField] private Image weatherStatusIcon;

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugLogs = true;

    // Game data to pass to next scene
    public TrackSelectionData GameSessionData { get; private set; }

    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Validate database reference
        if (gameDatabase == null)
        {
            Debug.LogError("RacingGameDatabaseSO is not assigned! Please assign the database in the inspector.");
            return;
        }

        // Initialize game session data
        GameSessionData = new TrackSelectionData();

        SetupButtonListeners();
    }

    private void Start()
    {
        if (gameDatabase == null) return;

        ValidateDatabase();
        InitializeUI();
        UpdateTrackDisplay();
        UpdateSessionStatus();
    }

    private void ValidateDatabase()
    {
        if (gameDatabase == null)
        {
            Debug.LogError("RacingGameDatabaseSO is not assigned!");
            return;
        }

        if (gameDatabase.AvailableTracks.Count == 0)
        {
            Debug.LogWarning("No tracks available in database");
        }

        if (gameDatabase.WeatherConditions.Count == 0)
        {
            Debug.LogWarning("No weather conditions available in database");
        }

        if (gameDatabase.TimeSettings.Count == 0)
        {
            Debug.LogWarning("No time settings available in database");
        }

        if (enableDebugLogs)
        {
            Debug.Log($"Database validation complete. Tracks: {gameDatabase.AvailableTracks.Count}, Weather: {gameDatabase.WeatherConditions.Count}, Time Settings: {gameDatabase.TimeSettings.Count}");
        }
    }

    private void SetupButtonListeners()
    {
        // Track navigation
        if (previousTrackButton != null)
            previousTrackButton.onClick.AddListener(SelectPreviousTrack);
        if (nextTrackButton != null)
            nextTrackButton.onClick.AddListener(SelectNextTrack);
        if (driveButton != null)
            driveButton.onClick.AddListener(StartDriving);

        // Weather selection
        for (int i = 0; i < weatherButtons.Length; i++)
        {
            int index = i; // Capture for closure
            if (weatherButtons[i] != null)
                weatherButtons[i].onClick.AddListener(() => SelectWeather(index));
        }

        // Time of day selection  
        for (int i = 0; i < timeOfDayButtons.Length; i++)
        {
            int index = i; // Capture for closure
            if (timeOfDayButtons[i] != null)
                timeOfDayButtons[i].onClick.AddListener(() => SelectTimeOfDay(index));
        }

        if (enableDebugLogs)
        {
            Debug.Log("Button listeners setup complete");
        }
    }

    private void InitializeUI()
    {
        UpdateNavigationButtons();
        UpdateWeatherButtons();
        UpdateTimeOfDayButtons();
    }

    public void SelectPreviousTrack()
    {
        if (gameDatabase == null || gameDatabase.AvailableTracks.Count <= 1) return;

        currentTrackIndex = (currentTrackIndex - 1 + gameDatabase.AvailableTracks.Count) % gameDatabase.AvailableTracks.Count;
        UpdateTrackDisplay();
        UpdateNavigationButtons();

        if (enableDebugLogs)
        {
            var track = gameDatabase.GetTrack(currentTrackIndex);
            Debug.Log($"Selected previous track: {(track != null ? track.trackName : "None")}");
        }
    }

    public void SelectNextTrack()
    {
        if (gameDatabase == null || gameDatabase.AvailableTracks.Count <= 1) return;

        currentTrackIndex = (currentTrackIndex + 1) % gameDatabase.AvailableTracks.Count;
        UpdateTrackDisplay();
        UpdateNavigationButtons();

        if (enableDebugLogs)
        {
            var track = gameDatabase.GetTrack(currentTrackIndex);
            Debug.Log($"Selected next track: {(track != null ? track.trackName : "None")}");
        }
    }

    private void UpdateTrackDisplay()
    {
        if (gameDatabase == null || gameDatabase.AvailableTracks.Count == 0)
        {
            Debug.LogWarning("Game database is null or has no tracks available");
            return;
        }

        TrackDataSO currentTrack = gameDatabase.GetTrack(currentTrackIndex);
        if (currentTrack == null)
        {
            Debug.LogError($"Failed to get track at index {currentTrackIndex}");
            return;
        }

        // Update text elements with null checks
        if (trackNameText != null)
            trackNameText.SetText(currentTrack.trackName ?? "Unknown Track");
        if (trackNumberText != null)
            trackNumberText.SetText($"TRACK NO: {currentTrack.trackNumber ?? "N/A"}");
        if (trackLengthText != null)
            trackLengthText.SetText($"Track Length: {currentTrack.trackLength}km");
        if (totalTurnsText != null)
            totalTurnsText.SetText($"Total Turns: {currentTrack.totalTurns}");

        // Update Images with Sprites
        UpdateImage(countryFlagImage, currentTrack.countryFlag);
        UpdateImage(trackLayoutImage, currentTrack.trackLayoutImage);
        UpdateImage(trackPreviewImage, currentTrack.trackPreviewImage);
    }

    // Helper method to safely update images
    private void UpdateImage(Image imageComponent, Sprite sprite)
    {
        if (imageComponent != null)
        {
            imageComponent.sprite = sprite;
            imageComponent.gameObject.SetActive(sprite != null);
        }
    }

    public void SelectWeather(int weatherIndex)
    {
        if (gameDatabase == null)
        {
            Debug.LogError("Game database is null");
            return;
        }

        if (weatherIndex < 0 || weatherIndex >= gameDatabase.WeatherConditions.Count)
        {
            Debug.LogWarning($"Weather index {weatherIndex} is out of range (0-{gameDatabase.WeatherConditions.Count - 1})");
            return;
        }

        selectedWeatherIndex = weatherIndex;
        UpdateWeatherButtons();
        UpdateSessionStatus();

        if (enableDebugLogs)
        {
            var weather = gameDatabase.GetWeather(weatherIndex);
            Debug.Log($"Selected weather: {(weather != null ? weather.weatherName : "None")}");
        }
    }

    public void SelectTimeOfDay(int timeIndex)
    {
        if (gameDatabase == null)
        {
            Debug.LogError("Game database is null");
            return;
        }

        if (timeIndex < 0 || timeIndex >= gameDatabase.TimeSettings.Count)
        {
            Debug.LogWarning($"Time index {timeIndex} is out of range (0-{gameDatabase.TimeSettings.Count - 1})");
            return;
        }

        selectedTimeIndex = timeIndex;
        UpdateTimeOfDayButtons();
        UpdateSessionStatus();

        if (enableDebugLogs)
        {
            var timeData = gameDatabase.GetTimeOfDay(timeIndex);
            Debug.Log($"Selected time of day: {(timeData != null ? timeData.timeName : "None")}");
        }
    }

    private void UpdateNavigationButtons()
    {
        bool hasMultipleTracks = gameDatabase != null && gameDatabase.AvailableTracks.Count > 1;
        if (previousTrackButton != null)
            previousTrackButton.gameObject.SetActive(hasMultipleTracks);
        if (nextTrackButton != null)
            nextTrackButton.gameObject.SetActive(hasMultipleTracks);
    }

    private void UpdateWeatherButtons()
    {
        for (int i = 0; i < weatherButtons.Length && i < gameDatabase.WeatherConditions.Count; i++)
        {
            if (weatherButtons[i] != null)
            {
                ColorBlock colors = weatherButtons[i].colors;
                colors.normalColor = i == selectedWeatherIndex ? Color.cyan : Color.white;
                colors.selectedColor = i == selectedWeatherIndex ? Color.cyan : Color.white;
                colors.highlightedColor = i == selectedWeatherIndex ? Color.cyan * 0.9f : Color.white * 0.9f;
                weatherButtons[i].colors = colors;
                weatherButtons[i].interactable = true;
            }
        }
    }

    private void UpdateTimeOfDayButtons()
    {
        for (int i = 0; i < timeOfDayButtons.Length && i < gameDatabase.TimeSettings.Count; i++)
        {
            if (timeOfDayButtons[i] != null)
            {
                ColorBlock colors = timeOfDayButtons[i].colors;
                colors.normalColor = i == selectedTimeIndex ? Color.yellow : Color.white;
                colors.selectedColor = i == selectedTimeIndex ? Color.yellow : Color.white;
                colors.highlightedColor = i == selectedTimeIndex ? Color.yellow * 0.9f : Color.white * 0.9f;
                timeOfDayButtons[i].colors = colors;
                timeOfDayButtons[i].interactable = true;
            }
        }
    }

    private void UpdateSessionStatus()
    {
        if (gameDatabase == null) return;

        // Update time status
        var timeData = gameDatabase.GetTimeOfDay(selectedTimeIndex);
        if (timeData != null)
        {
            if (timeStatusText != null)
                timeStatusText.SetText($"TIME OF DAY - {timeData.timeName.ToUpper()}");
            if (timeStatusIcon != null && timeData.timeIcon != null)
                timeStatusIcon.sprite = timeData.timeIcon;
        }

        // Update weather status
        var weatherData = gameDatabase.GetWeather(selectedWeatherIndex);
        if (weatherData != null)
        {
            if (weatherStatusText != null)
                weatherStatusText.SetText($"WEATHER - {weatherData.weatherName.ToUpper()}");
            if (weatherStatusIcon != null && weatherData.weatherIcon != null)
                weatherStatusIcon.sprite = weatherData.weatherIcon;
        }
    }

    public void StartDriving()
    {
        if (gameDatabase == null)
        {
            Debug.LogError("Cannot start driving: Game database is null");
            return;
        }

        var currentTrack = gameDatabase.GetTrack(currentTrackIndex);
        if (currentTrack == null)
        {
            Debug.LogError("Cannot start driving: Current track is null");
            return;
        }

        if (string.IsNullOrEmpty(currentTrack.sceneName))
        {
            Debug.LogError($"Cannot start driving: Track '{currentTrack.trackName}' has no scene name assigned");
            return;
        }

        PrepareGameSessionData();
        LoadTrackScene();
    }

    private void PrepareGameSessionData()
    {
        if (gameDatabase == null) return;

        GameSessionData.selectedTrack = gameDatabase.GetTrack(currentTrackIndex);
        GameSessionData.weatherCondition = gameDatabase.GetWeather(selectedWeatherIndex);
        GameSessionData.timeOfDay = gameDatabase.GetTimeOfDay(selectedTimeIndex);
        GameSessionData.timestamp = System.DateTime.Now;

        // PERSISTENCE: Also set data in persistence manager
        if (GamePersistenceManager.Instance != null)
        {
            GamePersistenceManager.Instance.SetSessionData(
                GameSessionData.selectedTrack,
                GameSessionData.weatherCondition,
                GameSessionData.timeOfDay
            );
        }

        if (enableDebugLogs)
        {
            Debug.Log($"Game session data prepared and persisted");
        }
    }

    private void LoadTrackScene()
    {
        var currentTrack = gameDatabase.GetTrack(currentTrackIndex);
        if (currentTrack != null && !string.IsNullOrEmpty(currentTrack.sceneName))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"Loading scene: {currentTrack.sceneName}");
            }

            SceneManager.LoadScene(currentTrack.sceneName);
        }
        else
        {
            Debug.LogWarning("Track scene name not specified or invalid track index!");
        }
    }

    // Public accessors for other scripts
    public TrackDataSO GetCurrentTrack()
    {
        return gameDatabase != null ? gameDatabase.GetTrack(currentTrackIndex) : null;
    }

    public WeatherConditionSO GetCurrentWeather()
    {
        return gameDatabase != null ? gameDatabase.GetWeather(selectedWeatherIndex) : null;
    }

    public TimeOfDaySettingsSO GetCurrentTimeOfDay()
    {
        return gameDatabase != null ? gameDatabase.GetTimeOfDay(selectedTimeIndex) : null;
    }

    public RacingGameDatabaseSO GetGameDatabase()
    {
        return gameDatabase;
    }

    public void ResetForTrackSelection()
    {
        if (enableDebugLogs)
        {
            Debug.Log("Reset for track selection called");
        }
    }

    public void CleanupOnExit()
    {
        if (Instance == this)
        {
            Destroy(gameObject);
            Instance = null;

            if (enableDebugLogs)
            {
                Debug.Log("TrackSelectionManager cleaned up");
            }
        }
    }

    [ContextMenu("Print Current Selection")]
    public void PrintCurrentSelection()
    {
        var track = GetCurrentTrack();
        var weather = GetCurrentWeather();
        var time = GetCurrentTimeOfDay();

        Debug.Log($"Current Selection - Track: {(track != null ? track.trackName : "None")}, Weather: {(weather != null ? weather.weatherName : "None")}, Time: {(time != null ? time.timeName : "None")}");
    }

    [ContextMenu("Validate Setup")]
    public void ValidateSetup()
    {
        ValidateDatabase();
    }
}

[System.Serializable]
public class TrackSelectionData
{
    public TrackDataSO selectedTrack;
    public WeatherConditionSO weatherCondition;
    public TimeOfDaySettingsSO timeOfDay;
    public System.DateTime timestamp;

    public bool IsValid()
    {
        return selectedTrack != null && weatherCondition != null && timeOfDay != null;
    }

    public string GetSummary()
    {
        return $"Track: {(selectedTrack != null ? selectedTrack.trackName : "None")}, Weather: {(weatherCondition != null ? weatherCondition.weatherName : "None")}, Time: {(timeOfDay != null ? timeOfDay.timeName : "None")}";
    }
}
