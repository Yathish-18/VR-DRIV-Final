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

    [Header("Player Settings")]
    [SerializeField] private TMP_InputField playerNameInput;

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

    // Cache the original skybox so we can restore it on scene exit
    private Material originalSkybox;

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (gameDatabase == null)
        {
            Debug.LogError("[TrackSelectionManager] RacingGameDatabaseSO is not assigned!");
            return;
        }

        GameSessionData = new TrackSelectionData();
        originalSkybox = RenderSettings.skybox;

        if (enableDebugLogs) Debug.Log("[TrackSelectionManager] Initialized.");
    }

    private void Start()
    {
        if (gameDatabase == null) return;

        if (ensureEventSystemOnStart) StartCoroutine(EnsureEventSystemRoutine());

        ValidateDatabase();
        RestorePreviousSelection();
        SetupButtonListeners();
        InitializeUI();

        UpdateTrackDisplay();
        UpdateSessionStatus();

        // Print skybox info for every weather SO so missing materials are obvious
        DebugWeatherSetup();

        // Apply skybox for the default / restored weather selection
        ApplySkyboxDirect(selectedWeatherIndex);

        // Also notify EnvironmentManager if one is wired up
        UpdateMenuEnvironment();

        // Load persisted player name
        if (playerNameInput != null && GamePersistenceManager.Instance != null)
            playerNameInput.text = GamePersistenceManager.Instance.playerName;
    }

    private void OnDestroy()
    {
        // Restore original skybox when leaving the menu so other scenes aren't affected
        if (originalSkybox != null)
        {
            RenderSettings.skybox = originalSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }

    // ─────────────────────────────────────────────
    // SKYBOX — DIRECT APPLICATION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Directly swaps RenderSettings.skybox to the material on the WeatherConditionSO.
    /// Works even when no EnvironmentManager is present in the scene.
    /// </summary>
    private void ApplySkyboxDirect(int weatherIndex)
    {
        if (gameDatabase == null)
        {
            Debug.LogError("[TrackSelectionManager] Cannot apply skybox — gameDatabase is null.");
            return;
        }

        WeatherConditionSO weather = gameDatabase.GetWeather(weatherIndex);

        if (weather == null)
        {
            Debug.LogError($"[TrackSelectionManager] GetWeather({weatherIndex}) returned null.");
            return;
        }

        if (weather.skyboxMaterial == null)
        {
            Debug.LogWarning($"[TrackSelectionManager] Weather '{weather.weatherName}' has no skybox material. " +
                             "Open the WeatherConditionSO asset and assign a Skybox/* material.");
            return;
        }

        // ── Swap the skybox ───────────────────────────────────────────────────
        RenderSettings.skybox = weather.skyboxMaterial;

        // Apply tint colour — shader property name varies by skybox type:
        //   Skybox/6 Sided      → "_Tint"
        //   Skybox/Procedural   → "_SkyTint"
        //   Skybox/Cubemap      → "_Tint"
        if (weather.skyboxMaterial.HasProperty("_Tint"))
            weather.skyboxMaterial.SetColor("_Tint", weather.skyboxTint);
        else if (weather.skyboxMaterial.HasProperty("_SkyTint"))
            weather.skyboxMaterial.SetColor("_SkyTint", weather.skyboxTint);

        // Apply fog settings
        RenderSettings.fogDensity = weather.fogDensity;
        RenderSettings.fogColor = weather.skyboxTint;

        // Recalculate ambient GI from the new skybox
        DynamicGI.UpdateEnvironment();

        if (enableDebugLogs)
            Debug.Log($"[TrackSelectionManager] ✓ Skybox applied → '{weather.skyboxMaterial.name}'  (weather: {weather.weatherName})");
    }

    // ─────────────────────────────────────────────
    // ENVIRONMENT MANAGER NOTIFICATION
    // ─────────────────────────────────────────────

    /// <summary>
    /// Forwards the current weather + time selection to the optional EnvironmentManager.
    /// The skybox swap is handled separately by ApplySkyboxDirect so it always works.
    /// </summary>
    private void UpdateMenuEnvironment()
    {
        if (menuEnvironmentManager == null || gameDatabase == null) return;

        WeatherConditionSO weather = gameDatabase.GetWeather(selectedWeatherIndex);
        TimeOfDaySettingsSO timeOfDay = gameDatabase.GetTimeOfDay(selectedTimeIndex);

        menuEnvironmentManager.UpdateEnvironment(weather, timeOfDay);
    }

    // ─────────────────────────────────────────────
    // DEBUG
    // ─────────────────────────────────────────────

    private void DebugWeatherSetup()
    {
        if (!enableDebugLogs) return;
        if (gameDatabase == null) { Debug.LogError("[TrackSelectionManager] gameDatabase is NULL"); return; }

        Debug.Log($"[TrackSelectionManager] --- Weather Debug ({gameDatabase.WeatherConditions.Count} entries) ---");

        for (int i = 0; i < gameDatabase.WeatherConditions.Count; i++)
        {
            WeatherConditionSO w = gameDatabase.WeatherConditions[i];

            if (w == null)
            {
                Debug.LogError($"[TrackSelectionManager]   [{i}] NULL entry — remove the empty slot from the database list.");
                continue;
            }

            string matInfo = w.skyboxMaterial != null
                ? $"'{w.skyboxMaterial.name}'  shader: {w.skyboxMaterial.shader.name}"
                : "NULL  ← ASSIGN A SKYBOX MATERIAL IN THIS WeatherConditionSO";

            Debug.Log($"[TrackSelectionManager]   [{i}] '{w.weatherName}'  |  SkyboxMat: {matInfo}");
        }

        string currentMat = RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL";
        Debug.Log($"[TrackSelectionManager] RenderSettings.skybox before swap: '{currentMat}'");
        Debug.Log($"[TrackSelectionManager] -------------------------------------------------");
    }

    // ─────────────────────────────────────────────
    // INITIALISATION HELPERS
    // ─────────────────────────────────────────────

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
        if (GamePersistenceManager.Instance == null) return;

        TrackDataSO persistedTrack = GamePersistenceManager.Instance.GetSelectedTrack();
        WeatherConditionSO persistedWeather = GamePersistenceManager.Instance.GetSelectedWeather();
        TimeOfDaySettingsSO persistedTime = GamePersistenceManager.Instance.GetSelectedTime();

        if (persistedTrack != null)
        {
            var found = gameDatabase.AvailableTracks.Find(t => t.trackName == persistedTrack.trackName);
            if (found != null) currentTrackIndex = gameDatabase.AvailableTracks.IndexOf(found);
        }

        if (persistedWeather != null)
        {
            var found = gameDatabase.WeatherConditions.Find(w => w.weatherName == persistedWeather.weatherName);
            if (found != null) selectedWeatherIndex = gameDatabase.WeatherConditions.IndexOf(found);
        }

        if (persistedTime != null)
        {
            var found = gameDatabase.TimeSettings.Find(t => t.timeName == persistedTime.timeName);
            if (found != null) selectedTimeIndex = gameDatabase.TimeSettings.IndexOf(found);
        }
    }

    private void ValidateDatabase()
    {
        if (gameDatabase == null) return;
        if (gameDatabase.AvailableTracks.Count == 0) Debug.LogWarning("[TrackSelectionManager] No tracks in database.");
        if (gameDatabase.WeatherConditions.Count == 0) Debug.LogWarning("[TrackSelectionManager] No weather conditions in database.");
        if (gameDatabase.TimeSettings.Count == 0) Debug.LogWarning("[TrackSelectionManager] No time-of-day settings in database.");
    }

    private void SetupButtonListeners()
    {
        if (previousTrackButton != null) previousTrackButton.onClick.AddListener(SelectPreviousTrack);
        if (nextTrackButton != null) nextTrackButton.onClick.AddListener(SelectNextTrack);
        if (driveButton != null) driveButton.onClick.AddListener(StartDriving);

        for (int i = 0; i < weatherButtons.Length; i++)
        {
            int index = i;
            if (weatherButtons[i] != null)
                weatherButtons[i].onClick.AddListener(() => SelectWeather(index));
        }

        for (int i = 0; i < timeOfDayButtons.Length; i++)
        {
            int index = i;
            if (timeOfDayButtons[i] != null)
                timeOfDayButtons[i].onClick.AddListener(() => SelectTimeOfDay(index));
        }
    }

    private void InitializeUI()
    {
        UpdateNavigationButtons();
        UpdateWeatherButtons();
        UpdateTimeOfDayButtons();
    }

    // ─────────────────────────────────────────────
    // TRACK NAVIGATION
    // ─────────────────────────────────────────────

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

    // ─────────────────────────────────────────────
    // WEATHER & TIME SELECTION
    // ─────────────────────────────────────────────

    public void SelectWeather(int weatherIndex)
    {
        if (gameDatabase == null || weatherIndex < 0 || weatherIndex >= gameDatabase.WeatherConditions.Count) return;

        selectedWeatherIndex = weatherIndex;
        UpdateWeatherButtons();
        UpdateSessionStatus();

        ApplySkyboxDirect(selectedWeatherIndex); // swap skybox immediately
        UpdateMenuEnvironment();                  // also notify EnvironmentManager
    }

    public void SelectTimeOfDay(int timeIndex)
    {
        if (gameDatabase == null || timeIndex < 0 || timeIndex >= gameDatabase.TimeSettings.Count) return;

        selectedTimeIndex = timeIndex;
        UpdateTimeOfDayButtons();
        UpdateSessionStatus();
        UpdateMenuEnvironment();
    }

    // ─────────────────────────────────────────────
    // UI UPDATES
    // ─────────────────────────────────────────────

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
            if (weatherButtons[i] == null) continue;
            ColorBlock colors = weatherButtons[i].colors;
            colors.normalColor = (i == selectedWeatherIndex) ? Color.cyan : Color.white;
            colors.selectedColor = (i == selectedWeatherIndex) ? Color.cyan : Color.white;
            weatherButtons[i].colors = colors;
        }
    }

    private void UpdateTimeOfDayButtons()
    {
        for (int i = 0; i < timeOfDayButtons.Length && i < gameDatabase.TimeSettings.Count; i++)
        {
            if (timeOfDayButtons[i] == null) continue;
            ColorBlock colors = timeOfDayButtons[i].colors;
            colors.normalColor = (i == selectedTimeIndex) ? Color.yellow : Color.white;
            colors.selectedColor = (i == selectedTimeIndex) ? Color.yellow : Color.white;
            timeOfDayButtons[i].colors = colors;
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

    // ─────────────────────────────────────────────
    // DRIVE / SESSION START
    // ─────────────────────────────────────────────

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
            Debug.LogError("[TrackSelectionManager] Track scene name is invalid or empty!");
        }
    }

    private void PrepareGameSessionData()
    {
        GameSessionData.selectedTrack = gameDatabase.GetTrack(currentTrackIndex);
        GameSessionData.weatherCondition = gameDatabase.GetWeather(selectedWeatherIndex);
        GameSessionData.timeOfDay = gameDatabase.GetTimeOfDay(selectedTimeIndex);
        GameSessionData.timestamp = System.DateTime.Now;

        if (GamePersistenceManager.Instance != null)
        {
            if (playerNameInput != null)
                GamePersistenceManager.Instance.SetPlayerName(playerNameInput.text);

            GamePersistenceManager.Instance.SetSessionData(
                GameSessionData.selectedTrack,
                GameSessionData.weatherCondition,
                GameSessionData.timeOfDay
            );
        }
    }

    // ─────────────────────────────────────────────
    // PUBLIC ACCESSORS
    // ─────────────────────────────────────────────

    public TrackDataSO GetCurrentTrack() => gameDatabase?.GetTrack(currentTrackIndex);
    public WeatherConditionSO GetCurrentWeather() => gameDatabase?.GetWeather(selectedWeatherIndex);
    public TimeOfDaySettingsSO GetCurrentTimeOfDay() => gameDatabase?.GetTimeOfDay(selectedTimeIndex);
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA CONTAINER
// ─────────────────────────────────────────────────────────────────────────────

[System.Serializable]
public class TrackSelectionData
{
    public TrackDataSO selectedTrack;
    public WeatherConditionSO weatherCondition;
    public TimeOfDaySettingsSO timeOfDay;
    public System.DateTime timestamp;
}