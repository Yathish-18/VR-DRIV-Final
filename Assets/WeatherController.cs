using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;

public enum WeatherType
{
    Sunny,
    Rainy,
    Foggy
}

[Serializable]
public class WeatherSettings
{
    [Header("Visual")]
    public Material skybox;

    [Header("Lighting")]
    [Range(0f, 2f)]
    public float lightIntensity = 1f;

    [Header("Fog")]
    [Range(0f, 0.1f)]
    public float fogDensity = 0.005f;

    [Header("Effects")]
    public bool enableRain = false;
}

public class WeatherController : MonoBehaviour
{
    [Header("Core Components")]
    [SerializeField] private Light sunLight;
    [SerializeField] private ParticleSystem rainParticles;

    [Header("Global Settings")]
    [SerializeField] private Color fogColor = Color.gray;
    [SerializeField] private float transitionDuration = 1.5f;

    [Header("Weather Configurations")]
    [SerializeField]
    private WeatherSettings sunnyWeather = new WeatherSettings
    {
        lightIntensity = 1.2f,
        fogDensity = 0.005f,
        enableRain = false
    };

    [SerializeField]
    private WeatherSettings rainyWeather = new WeatherSettings
    {
        lightIntensity = 0.5f,
        fogDensity = 0.02f,
        enableRain = true
    };

    [SerializeField]
    private WeatherSettings foggyWeather = new WeatherSettings
    {
        lightIntensity = 0.2f,
        fogDensity = 0.05f,
        enableRain = false
    };

    [Header("UI Controls")]
    [SerializeField] private Button sunnyButton;
    [SerializeField] private Button rainyButton;
    [SerializeField] private Button foggyButton;

    // Events
    public static event Action<WeatherType> OnWeatherChanged;

    // Private fields
    private WeatherType currentWeather = WeatherType.Sunny;
    private Coroutine activeTransition;
    private bool isInitialized = false;

    #region Unity Lifecycle

    private void Awake()
    {
        ValidateComponents();
    }

    private void Start()
    {
        InitializeUI();
        SetWeather(WeatherType.Sunny);
        isInitialized = true;
    }

    private void OnValidate()
    {
        // Ensure skybox references are set in inspector
        if (sunnyWeather?.skybox == null) Debug.LogWarning("Sunny skybox not assigned!");
        if (rainyWeather?.skybox == null) Debug.LogWarning("Rainy skybox not assigned!");
        if (foggyWeather?.skybox == null) Debug.LogWarning("Foggy skybox not assigned!");
    }

    #endregion

    #region Public Methods

    public void SetWeather(WeatherType weatherType)
    {
        if (!isInitialized || currentWeather == weatherType) return;

        WeatherSettings targetSettings = GetWeatherSettings(weatherType);
        if (targetSettings == null)
        {
            Debug.LogError($"No settings found for weather type: {weatherType}");
            return;
        }

        ChangeWeather(weatherType, targetSettings);
    }

    public void SetSunny() => SetWeather(WeatherType.Sunny);
    public void SetRainy() => SetWeather(WeatherType.Rainy);
    public void SetFoggy() => SetWeather(WeatherType.Foggy);

    public WeatherType GetCurrentWeather() => currentWeather;

    #endregion

    #region Private Methods

    private void ValidateComponents()
    {
        if (sunLight == null)
        {
            sunLight = FindObjectOfType<Light>();
            if (sunLight == null)
                Debug.LogError("No Light component found! Please assign sunLight in inspector.");
        }

        if (rainParticles == null)
            Debug.LogWarning("Rain particle system not assigned. Rain effects will be disabled.");
    }

    private void InitializeUI()
    {
        if (sunnyButton != null)
        {
            sunnyButton.onClick.RemoveAllListeners();
            sunnyButton.onClick.AddListener(SetSunny);
        }

        if (rainyButton != null)
        {
            rainyButton.onClick.RemoveAllListeners();
            rainyButton.onClick.AddListener(SetRainy);
        }

        if (foggyButton != null)
        {
            foggyButton.onClick.RemoveAllListeners();
            foggyButton.onClick.AddListener(SetFoggy);
        }
    }

    private WeatherSettings GetWeatherSettings(WeatherType weatherType)
    {
        return weatherType switch
        {
            WeatherType.Sunny => sunnyWeather,
            WeatherType.Rainy => rainyWeather,
            WeatherType.Foggy => foggyWeather,
            _ => null
        };
    }

    private void ChangeWeather(WeatherType newWeatherType, WeatherSettings targetSettings)
    {
        // Stop any active transition
        if (activeTransition != null)
        {
            StopCoroutine(activeTransition);
            activeTransition = null;
        }

        activeTransition = StartCoroutine(TransitionWeatherCoroutine(newWeatherType, targetSettings));
    }

    private IEnumerator TransitionWeatherCoroutine(WeatherType targetWeatherType, WeatherSettings targetSettings)
    {
        // Store initial values
        float initialFogDensity = RenderSettings.fogDensity;
        float initialLightIntensity = sunLight != null ? sunLight.intensity : 0f;

        // Apply immediate changes
        ApplyImmediateChanges(targetSettings);

        // Smoothly transition fog and lighting
        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            float normalizedTime = elapsed / transitionDuration;
            float easedTime = EaseInOut(normalizedTime);

            // Interpolate fog density
            RenderSettings.fogDensity = Mathf.Lerp(initialFogDensity, targetSettings.fogDensity, easedTime);

            // Interpolate light intensity
            if (sunLight != null)
                sunLight.intensity = Mathf.Lerp(initialLightIntensity, targetSettings.lightIntensity, easedTime);

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Ensure final values are set
        ApplyFinalSettings(targetSettings);

        // Update current state
        currentWeather = targetWeatherType;
        activeTransition = null;

        // Notify listeners
        OnWeatherChanged?.Invoke(targetWeatherType);

        Debug.Log($"Weather changed to: {targetWeatherType}");
    }

    private void ApplyImmediateChanges(WeatherSettings settings)
    {
        // Set skybox
        if (settings.skybox != null)
            RenderSettings.skybox = settings.skybox;

        // Configure fog
        RenderSettings.fog = true;
        RenderSettings.fogColor = fogColor;

        // Handle rain particles
        HandleRainEffect(settings.enableRain);
    }

    private void ApplyFinalSettings(WeatherSettings settings)
    {
        RenderSettings.fogDensity = settings.fogDensity;

        if (sunLight != null)
            sunLight.intensity = settings.lightIntensity;
    }

    private void HandleRainEffect(bool enableRain)
    {
        if (rainParticles == null) return;

        if (enableRain)
        {
            if (!rainParticles.isPlaying)
                rainParticles.Play();
        }
        else
        {
            if (rainParticles.isPlaying)
                rainParticles.Stop();
        }
    }

    private float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t); // Smooth step function
    }

    #endregion

    #region Editor Helpers

    [ContextMenu("Test Sunny Weather")]
    private void TestSunny() => SetSunny();

    [ContextMenu("Test Rainy Weather")]
    private void TestRainy() => SetRainy();

    [ContextMenu("Test Foggy Weather")]
    private void TestFoggy() => SetFoggy();

    #endregion
}