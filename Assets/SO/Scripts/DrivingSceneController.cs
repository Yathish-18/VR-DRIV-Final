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

    [Header("Weather Effects")]
    [SerializeField] private GameObject rainParticleSystem;
    [SerializeField] private float defaultLightIntensity = 1f;
    [SerializeField] private float sunnyLightMultiplier = 1.5f;
    [SerializeField] private float defaultFogDensity = 0.01f;
    [SerializeField] private float rainyFogDensity = 0.05f;
    [SerializeField] private float foggyFogDensity = 0.08f;

    [Header("Race Settings")]
    [SerializeField] private float currentLapTime = 0f;
    [SerializeField] private bool raceActive = false;

    // Store original environment settings
    private float originalLightIntensity;
    private float originalFogDensity;
    private Color originalFogColor;
    private bool originalFogEnabled;

    private void Start()
    {
        StoreOriginalSettings();
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

    private void StoreOriginalSettings()
    {
        // Store original light settings
        if (sunLight != null)
            originalLightIntensity = sunLight.intensity;
        else
            originalLightIntensity = defaultLightIntensity;

        // Store original fog settings
        originalFogEnabled = RenderSettings.fog;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogColor = RenderSettings.fogColor;
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

        // Apply weather data with new system
        if (persistenceManager.HasWeatherData())
        {
            var weather = persistenceManager.GetSelectedWeather();
            if (weatherText != null)
                weatherText.SetText($"Weather: {weather.weatherName}");

            ApplyWeatherEffects(weather);
            Debug.Log($"Applied weather: {weather.weatherName}");
        }

        // Apply time data
        if (persistenceManager.HasTimeData())
        {
            var timeData = persistenceManager.GetSelectedTime();
            if (timeText != null)
                timeText.SetText($"Time: {timeData.timeName}");

            // Apply lighting (but don't override weather light changes)
            if (sunLight != null && !IsWeatherAffectingLight())
            {
                sunLight.color = timeData.lightColor;
                sunLight.intensity = timeData.lightIntensity;
            }
            else if (sunLight != null)
            {
                // Only apply color, keep weather-modified intensity
                sunLight.color = timeData.lightColor;
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

    private void ApplyWeatherEffects(WeatherConditionSO weather)
    {
        string weatherName = weather.weatherName.ToLower();

        // Reset all weather effects first
        ResetWeatherEffects();

        switch (weatherName)
        {
            case "sunny":
                ApplySunnyWeather();
                break;
            case "rainy":
            case "rain":
                ApplyRainyWeather(weather);
                break;
            case "foggy":
            case "fog":
                ApplyFoggyWeather(weather);
                break;
            default:
                // Apply default weather data if no specific case matches
                ApplyDefaultWeather(weather);
                break;
        }
    }

    private void ResetWeatherEffects()
    {
        // Reset light intensity
        if (sunLight != null)
            sunLight.intensity = originalLightIntensity;

        // Reset fog to original state
        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogColor = originalFogColor;

        // Disable rain particles
        if (rainParticleSystem != null)
            rainParticleSystem.SetActive(false);
    }

    private void ApplySunnyWeather()
    {
        // Increase directional light intensity
        if (sunLight != null)
        {
            sunLight.intensity = originalLightIntensity * sunnyLightMultiplier;
        }

        // Disable fog for clear sunny weather
        RenderSettings.fog = false;

        Debug.Log($"Applied sunny weather - Light intensity: {sunLight?.intensity}, Fog disabled");
    }

    private void ApplyRainyWeather(WeatherConditionSO weather)
    {
        // Enable fog and increase fog intensity
        RenderSettings.fog = true;
        RenderSettings.fogDensity = rainyFogDensity;

        // Apply fog color if provided
        if (weather.skyboxTint != Color.clear)
            RenderSettings.fogColor = weather.skyboxTint;

        // Enable rain particle system
        if (rainParticleSystem != null)
        {
            rainParticleSystem.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Rain particle system not assigned! Please assign a particle system for rain effects.");
        }

        // Slightly reduce light intensity for rainy atmosphere
        if (sunLight != null)
        {
            sunLight.intensity = originalLightIntensity * 0.7f;
        }

        Debug.Log($"Applied rainy weather - Fog enabled with density: {RenderSettings.fogDensity}, Rain particles: {rainParticleSystem?.activeInHierarchy}");
    }

    private void ApplyFoggyWeather(WeatherConditionSO weather)
    {
        // Enable fog and set high fog intensity for foggy weather
        RenderSettings.fog = true;
        RenderSettings.fogDensity = foggyFogDensity;

        // Apply fog color if provided
        if (weather.skyboxTint != Color.clear)
            RenderSettings.fogColor = weather.skyboxTint;

        // Slightly reduce light intensity for foggy atmosphere
        if (sunLight != null)
        {
            sunLight.intensity = originalLightIntensity * 0.8f;
        }

        Debug.Log($"Applied foggy weather - Fog enabled with density: {RenderSettings.fogDensity}");
    }

    private void ApplyDefaultWeather(WeatherConditionSO weather)
    {
        // Apply original weather system for backwards compatibility
        RenderSettings.fog = true;
        RenderSettings.fogColor = weather.skyboxTint;
        RenderSettings.fogDensity = weather.fogDensity;

        Debug.Log($"Applied default weather effects from WeatherConditionSO - Fog enabled");
    }

    private bool IsWeatherAffectingLight()
    {
        if (GamePersistenceManager.Instance?.HasWeatherData() == true)
        {
            var weather = GamePersistenceManager.Instance.GetSelectedWeather();
            string weatherName = weather.weatherName.ToLower();
            return weatherName == "sunny" || weatherName == "rainy" || weatherName == "rain" ||
                   weatherName == "foggy" || weatherName == "fog";
        }
        return false;
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

    // Public methods for manual weather control (useful for testing)
    [ContextMenu("Apply Sunny Weather")]
    public void SetSunnyWeatherDebug()
    {
        ApplySunnyWeather();
    }

    [ContextMenu("Apply Rainy Weather")]
    public void SetRainyWeatherDebug()
    {
        // This is just for testing - you'll need to create a ScriptableObject for real use
        Debug.LogWarning("Debug rainy weather - create a WeatherConditionSO for actual testing");
        RenderSettings.fog = true;
        RenderSettings.fogDensity = rainyFogDensity;
        if (rainParticleSystem != null) rainParticleSystem.SetActive(true);
    }

    [ContextMenu("Apply Foggy Weather")]
    public void SetFoggyWeatherDebug()
    {
        // This is just for testing - you'll need to create a ScriptableObject for real use
        Debug.LogWarning("Debug foggy weather - create a WeatherConditionSO for actual testing");
        RenderSettings.fog = true;
        RenderSettings.fogDensity = foggyFogDensity;
    }

    // Public methods that can be called from other scripts or UI
    [ContextMenu("Start Race")]
    public void StartRaceDebug() => StartRace();

    [ContextMenu("Finish Race (1st Place)")]
    public void FinishRaceDebug() => OnRaceComplete(1);

    // Reset to original settings (useful for testing)
    [ContextMenu("Reset Weather Effects")]
    public void ResetWeatherEffectsDebug()
    {
        ResetWeatherEffects();
    }
}

// WeatherConditionSO should already exist in your project
// This is just here for reference if you need to see the structure
/*
[System.Serializable]
public class WeatherConditionSO : ScriptableObject
{
    public string weatherName;
    public Color skyboxTint = Color.white;
    public float fogDensity = 0.01f;
    // Add any other weather properties you need
}
*/