using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class TrackSelectionManager : MonoBehaviour
{
    [Header("Game Database")]
    [SerializeField] private RacingGameDatabaseSO gameDatabase;

    [Header("Live Preview (Optional)")]
    [Tooltip("Drag the EnvironmentManager from your Menu Scene here to see weather change in real-time")]
    [SerializeField] private EnvironmentManager menuEnvironmentManager;

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

    // --- ADDED PLAYER NAME INPUT HERE ---
    [Header("Player Settings")]
    [SerializeField] private TMP_InputField playerNameInput;
    // ------------------------------------

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
    [SerializeField] private bool ensureEventSystemOnStart = true;

    // Game data to pass to next scene
    public TrackSelectionData GameSessionData { get; private set; }

    private void Awake()
    {
        if (gameDatabase == null)
        {
            Debug.LogError("RacingGameDatabaseSO is not assigned! Please assign the database in the inspector.");
            return;
        }

        GameSessionData = new TrackSelectionData();

        if (enableDebugLogs) Debug.Log("TrackSelectionManager initialized");
    }

    private void Start()
    {
        if (gameDatabase == null) return;

        if (ensureEventSystemOnStart) StartCoroutine(EnsureEventSystemRoutine());

        ValidateDatabase();
        RestorePreviousSelection();
        SetupButtonListeners();
        InitializeUI();

        // Initial Updates
        UpdateTrackDisplay();
        UpdateSessionStatus();

        // --- LIVE PREVIEW UPDATE ---
        UpdateMenuEnvironment();

        // --- LOAD PLAYER NAME ---
        if (playerNameInput != null && GamePersistenceManager.Instance != null)
        {
            playerNameInput.text = GamePersistenceManager.Instance.playerName;
        }
    }

    // --- NEW HELPER METHOD FOR LIVE PREVIEW ---
    private void UpdateMenuEnvironment()
    {
        if (menuEnvironmentManager != null && gameDatabase != null)
        {
            WeatherConditionSO w = gameDatabase.GetWeather(selectedWeatherIndex);
            TimeOfDaySettingsSO t = gameDatabase.GetTimeOfDay(selectedTimeIndex);

            // Send data to the local EnvironmentManager to update the background immediately
            menuEnvironmentManager.UpdateEnvironment(w, t);
        }
    }

    private IEnumerator EnsureEventSystemRoutine()
    {
        yield return null;
        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }
        else
        {
            eventSystem.enabled = false;
            yield return null;
            eventSystem.enabled = true;
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }

    private void RestorePreviousSelection()
    {
        if (GamePersistenceManager.Instance != null)
        {
            TrackDataSO persistedTrack = GamePersistenceManager.Instance.GetSelectedTrack();
            WeatherConditionSO persistedWeather = GamePersistenceManager.Instance.GetSelectedWeather();
            TimeOfDaySettingsSO persistedTime = GamePersistenceManager.Instance.GetSelectedTime();

            if (persistedTrack != null)
            {
                var foundTrack = gameDatabase.AvailableTracks.Find(t => t.trackName == persistedTrack.trackName);
                if (foundTrack != null) currentTrackIndex = gameDatabase.AvailableTracks.IndexOf(foundTrack);
            }

            if (persistedWeather != null)
            {
                var foundWeather = gameDatabase.WeatherConditions.Find(w => w.weatherName == persistedWeather.weatherName);
                if (foundWeather != null) selectedWeatherIndex = gameDatabase.WeatherConditions.IndexOf(foundWeather);
            }

            if (persistedTime != null)
            {
                var foundTime = gameDatabase.TimeSettings.Find(t => t.timeName == persistedTime.timeName);
                if (foundTime != null) selectedTimeIndex = gameDatabase.TimeSettings.IndexOf(foundTime);
            }
        }
    }

    private void ValidateDatabase()
    {
        if (gameDatabase == null) return;
        if (gameDatabase.AvailableTracks.Count == 0) Debug.LogWarning("No tracks available");
    }

    private void SetupButtonListeners()
    {
        if (previousTrackButton != null) previousTrackButton.onClick.AddListener(SelectPreviousTrack);
        if (nextTrackButton != null) nextTrackButton.onClick.AddListener(SelectNextTrack);
        if (driveButton != null) driveButton.onClick.AddListener(StartDriving);

        for (int i = 0; i < weatherButtons.Length; i++)
        {
            int index = i;
            if (weatherButtons[i] != null) weatherButtons[i].onClick.AddListener(() => SelectWeather(index));
        }

        for (int i = 0; i < timeOfDayButtons.Length; i++)
        {
            int index = i;
            if (timeOfDayButtons[i] != null) timeOfDayButtons[i].onClick.AddListener(() => SelectTimeOfDay(index));
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
    }

    public void SelectNextTrack()
    {
        if (gameDatabase == null || gameDatabase.AvailableTracks.Count <= 1) return;
        currentTrackIndex = (currentTrackIndex + 1) % gameDatabase.AvailableTracks.Count;
        UpdateTrackDisplay();
        UpdateNavigationButtons();
    }

    private void UpdateTrackDisplay()
    {
        if (gameDatabase == null) return;
        TrackDataSO currentTrack = gameDatabase.GetTrack(currentTrackIndex);
        if (currentTrack == null) return;

        if (trackNameText != null) trackNameText.SetText(currentTrack.trackName ?? "Unknown");
        if (trackNumberText != null) trackNumberText.SetText($"TRACK NO: {currentTrack.trackNumber ?? "N/A"}");
        if (trackLengthText != null) trackLengthText.SetText($"Length: {currentTrack.trackLength}km");
        if (totalTurnsText != null) totalTurnsText.SetText($"Turns: {currentTrack.totalTurns}");

        UpdateImage(countryFlagImage, currentTrack.countryFlag);
        UpdateImage(trackLayoutImage, currentTrack.trackLayoutImage);
        UpdateImage(trackPreviewImage, currentTrack.trackPreviewImage);
    }

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
        if (gameDatabase == null || weatherIndex < 0 || weatherIndex >= gameDatabase.WeatherConditions.Count) return;

        selectedWeatherIndex = weatherIndex;
        UpdateWeatherButtons();
        UpdateSessionStatus();

        // --- UPDATE LIVE PREVIEW ---
        UpdateMenuEnvironment();
    }

    public void SelectTimeOfDay(int timeIndex)
    {
        if (gameDatabase == null || timeIndex < 0 || timeIndex >= gameDatabase.TimeSettings.Count) return;

        selectedTimeIndex = timeIndex;
        UpdateTimeOfDayButtons();
        UpdateSessionStatus();

        // --- UPDATE LIVE PREVIEW ---
        UpdateMenuEnvironment();
    }

    private void UpdateNavigationButtons()
    {
        bool hasMultiple = gameDatabase != null && gameDatabase.AvailableTracks.Count > 1;
        if (previousTrackButton != null) previousTrackButton.gameObject.SetActive(hasMultiple);
        if (nextTrackButton != null) nextTrackButton.gameObject.SetActive(hasMultiple);
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
                weatherButtons[i].colors = colors;
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
                timeOfDayButtons[i].colors = colors;
            }
        }
    }

    private void UpdateSessionStatus()
    {
        if (gameDatabase == null) return;

        var timeData = gameDatabase.GetTimeOfDay(selectedTimeIndex);
        if (timeData != null)
        {
            if (timeStatusText != null) timeStatusText.SetText($"TIME - {timeData.timeName.ToUpper()}");
            if (timeStatusIcon != null) timeStatusIcon.sprite = timeData.timeIcon;
        }

        var weatherData = gameDatabase.GetWeather(selectedWeatherIndex);
        if (weatherData != null)
        {
            if (weatherStatusText != null) weatherStatusText.SetText($"WEATHER - {weatherData.weatherName.ToUpper()}");
            if (weatherStatusIcon != null) weatherStatusIcon.sprite = weatherData.weatherIcon;
        }
    }

    public void StartDriving()
    {
        if (gameDatabase == null) return;
        var currentTrack = gameDatabase.GetTrack(currentTrackIndex);

        if (currentTrack != null && !string.IsNullOrEmpty(currentTrack.sceneName))
        {
            PrepareGameSessionData();
            SceneManager.LoadScene(currentTrack.sceneName);
        }
        else
        {
            Debug.LogError("Track Scene Name invalid!");
        }
    }

    private void PrepareGameSessionData()
    {
        GameSessionData.selectedTrack = gameDatabase.GetTrack(currentTrackIndex);
        GameSessionData.weatherCondition = gameDatabase.GetWeather(selectedWeatherIndex);
        GameSessionData.timeOfDay = gameDatabase.GetTimeOfDay(selectedTimeIndex);
        GameSessionData.timestamp = System.DateTime.Now;

        // Send to Persistence Manager so the NEXT scene can read it
        if (GamePersistenceManager.Instance != null)
        {
            // --- SAVE PLAYER NAME BEFORE STARTING ---
            if (playerNameInput != null)
            {
                GamePersistenceManager.Instance.SetPlayerName(playerNameInput.text);
            }

            GamePersistenceManager.Instance.SetSessionData(
                GameSessionData.selectedTrack,
                GameSessionData.weatherCondition,
                GameSessionData.timeOfDay
            );
        }
    }

    // Accessors
    public TrackDataSO GetCurrentTrack() => gameDatabase?.GetTrack(currentTrackIndex);
    public WeatherConditionSO GetCurrentWeather() => gameDatabase?.GetWeather(selectedWeatherIndex);
    public TimeOfDaySettingsSO GetCurrentTimeOfDay() => gameDatabase?.GetTimeOfDay(selectedTimeIndex);
}

[System.Serializable]
public class TrackSelectionData
{
    public TrackDataSO selectedTrack;
    public WeatherConditionSO weatherCondition;
    public TimeOfDaySettingsSO timeOfDay;
    public System.DateTime timestamp;
}