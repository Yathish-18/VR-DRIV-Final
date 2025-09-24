using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class EnvironmentalSettings
{
    [Header("Lighting Settings")]
    public Color ambientColor = Color.white;
    public float lightIntensity = 1.0f;
    public Color lightColor = Color.white;
    public float shadowStrength = 1.0f;

    [Header("Skybox Settings")]
    public Material skyboxMaterial;
    public Color skyTint = Color.white;
    public float skyboxExposure = 1.0f;

    [Header("Fog Settings")]
    public bool fogEnabled = false;
    public Color fogColor = Color.gray;
    public float fogDensity = 0.01f;
    public float fogStartDistance = 0f;
    public float fogEndDistance = 300f;
    public FogMode fogMode = FogMode.Exponential;

    [Header("Particle Effects")]
    public GameObject[] weatherParticles;
    public ParticleSystemSettings[] particleSettings;

    [Header("Wind Settings")]
    public float windStrength = 0.5f;
    public Vector3 windDirection = Vector3.right;
    public float windTurbulence = 0.1f;
}

[System.Serializable]
public class ParticleSystemSettings
{
    public string particleName;
    public int emissionRate = 10;
    public float particleSize = 1f;
    public Color particleColor = Color.white;
    public Vector3 velocity = Vector3.down;
    public float lifetime = 5f;
}

[System.Serializable]
public class EnhancedSessionCondition
{
    [Header("Basic Info")]
    public string timeName = "DAY";
    public string weatherName = "CLEAR";
    public Sprite timeIcon;
    public Sprite weatherIcon;

    [Header("Environmental Settings")]
    public EnvironmentalSettings environmentalSettings;
}

// PERSISTENT MANAGER - This will carry data between scenes
public class EnvironmentalDataManager : MonoBehaviour
{
    public static EnvironmentalDataManager Instance { get; private set; }

    [Header("Current Session Data")]
    public HillsTrackData selectedTrack;
    public EnvironmentalSettings currentEnvironment;
    public string selectedTimeOfDay;
    public string selectedWeather;

    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Subscribe to scene loading events
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Apply environmental settings when new scene loads
        if (currentEnvironment != null)
        {
            StartCoroutine(ApplyEnvironmentalSettings());
        }
    }

    private IEnumerator ApplyEnvironmentalSettings()
    {
        yield return new WaitForEndOfFrame(); // Wait for scene to fully load

        ApplyLightingSettings();
        ApplySkyboxSettings();
        ApplyFogSettings();
        ApplyParticleEffects();
        ApplyWindSettings();

        Debug.Log($"Environmental settings applied: {selectedTimeOfDay} - {selectedWeather}");
    }

    private void ApplyLightingSettings()
    {
        Light mainLight = FindMainDirectionalLight();
        if (mainLight != null)
        {
            mainLight.color = currentEnvironment.lightColor;
            mainLight.intensity = currentEnvironment.lightIntensity;
            mainLight.shadowStrength = currentEnvironment.shadowStrength;
        }

        // Apply ambient lighting
        RenderSettings.ambientLight = currentEnvironment.ambientColor;
    }

    private void ApplySkyboxSettings()
    {
        if (currentEnvironment.skyboxMaterial != null)
        {
            RenderSettings.skybox = currentEnvironment.skyboxMaterial;

            // Apply skybox tint if material supports it
            if (RenderSettings.skybox.HasProperty("_Tint"))
            {
                RenderSettings.skybox.SetColor("_Tint", currentEnvironment.skyTint);
            }

            if (RenderSettings.skybox.HasProperty("_Exposure"))
            {
                RenderSettings.skybox.SetFloat("_Exposure", currentEnvironment.skyboxExposure);
            }
        }
    }

    private void ApplyFogSettings()
    {
        RenderSettings.fog = currentEnvironment.fogEnabled;

        if (currentEnvironment.fogEnabled)
        {
            RenderSettings.fogColor = currentEnvironment.fogColor;
            RenderSettings.fogMode = currentEnvironment.fogMode;

            if (currentEnvironment.fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = currentEnvironment.fogStartDistance;
                RenderSettings.fogEndDistance = currentEnvironment.fogEndDistance;
            }
            else
            {
                RenderSettings.fogDensity = currentEnvironment.fogDensity;
            }
        }
    }

    private void ApplyParticleEffects()
    {
        // Remove existing weather particles
        GameObject[] existingParticles = GameObject.FindGameObjectsWithTag("WeatherParticle");
        foreach (GameObject particle in existingParticles)
        {
            Destroy(particle);
        }

        // Spawn new weather particles
        if (currentEnvironment.weatherParticles != null)
        {
            foreach (GameObject particlePrefab in currentEnvironment.weatherParticles)
            {
                if (particlePrefab != null)
                {
                    GameObject spawnedParticle = Instantiate(particlePrefab);
                    spawnedParticle.tag = "WeatherParticle";

                    // Apply particle settings
                    ApplyParticleSystemSettings(spawnedParticle);
                }
            }
        }
    }

    private void ApplyParticleSystemSettings(GameObject particleObject)
    {
        ParticleSystem ps = particleObject.GetComponent<ParticleSystem>();
        if (ps != null && currentEnvironment.particleSettings != null)
        {
            foreach (ParticleSystemSettings settings in currentEnvironment.particleSettings)
            {
                if (particleObject.name.Contains(settings.particleName))
                {
                    var main = ps.main;
                    main.startLifetime = settings.lifetime;
                    main.startSize = settings.particleSize;
                    main.startColor = settings.particleColor;
                    main.startSpeed = settings.velocity.magnitude;

                    var emission = ps.emission;
                    emission.rateOverTime = settings.emissionRate;

                    var velocityOverLifetime = ps.velocityOverLifetime;
                    velocityOverLifetime.enabled = true;
                    velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
                    velocityOverLifetime.x = settings.velocity.x;
                    velocityOverLifetime.y = settings.velocity.y;
                    velocityOverLifetime.z = settings.velocity.z;
                }
            }
        }
    }

    private void ApplyWindSettings()
    {
        WindZone windZone = FindObjectOfType<WindZone>();
        if (windZone != null)
        {
            windZone.windMain = currentEnvironment.windStrength;
            windZone.windTurbulence = currentEnvironment.windTurbulence;
            windZone.transform.rotation = Quaternion.LookRotation(currentEnvironment.windDirection);
        }
    }

    private Light FindMainDirectionalLight()
    {
        Light[] lights = FindObjectsOfType<Light>();
        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }
        return null;
    }

    public void SetEnvironmentalData(HillsTrackData track, EnvironmentalSettings environment, string timeOfDay, string weather)
    {
        selectedTrack = track;
        currentEnvironment = environment;
        selectedTimeOfDay = timeOfDay;
        selectedWeather = weather;

        Debug.Log($"Environmental data set: Track={track.trackName}, Time={timeOfDay}, Weather={weather}");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}

// UPDATED TRACK SELECTION UI
public class EnhancedHillsTrackSelectionUI : MonoBehaviour
{
    [Header("Track Data List")]
    [SerializeField] private List<HillsTrackData> availableTracks = new List<HillsTrackData>();
    [SerializeField] private int currentTrackIndex = 0;

    [Header("Session Conditions with Environmental Settings")]
    [SerializeField] private List<EnhancedSessionCondition> sessionConditions = new List<EnhancedSessionCondition>();
    [SerializeField] private int selectedTimeIndex = 0;
    [SerializeField] private int selectedWeatherIndex = 0;

    [Header("UI References - Same as before")]
    // ... (keep all existing UI references)

    [Header("Environmental Manager")]
    [SerializeField] private GameObject environmentalManagerPrefab;

    private void Awake()
    {
        // Create environmental manager if it doesn't exist
        if (EnvironmentalDataManager.Instance == null && environmentalManagerPrefab != null)
        {
            Instantiate(environmentalManagerPrefab);
        }

        // ... rest of existing Awake code
    }

    public void StartDriving()
    {
        Debug.Log("Starting Driving Mode with Environmental Settings");
        PrepareEnvironmentalData();
        StartCoroutine(LoadDrivingSceneWithEnvironment());
    }

    private void PrepareEnvironmentalData()
    {
        if (currentTrackIndex >= availableTracks.Count) return;

        HillsTrackData currentTrack = availableTracks[currentTrackIndex];

        // Get environmental settings based on selections
        EnvironmentalSettings environmentToApply = GetCombinedEnvironmentalSettings();

        string timeOfDay = selectedTimeIndex == 0 ? "DAY" : "NIGHT";
        string weather = selectedWeatherIndex == 0 ? "CLEAR" : "RAINY";

        // Pass data to persistent manager
        if (EnvironmentalDataManager.Instance != null)
        {
            EnvironmentalDataManager.Instance.SetEnvironmentalData(
                currentTrack,
                environmentToApply,
                timeOfDay,
                weather
            );
        }
    }

    private EnvironmentalSettings GetCombinedEnvironmentalSettings()
    {
        // Combine time of day and weather settings
        EnvironmentalSettings combined = new EnvironmentalSettings();

        if (sessionConditions.Count > selectedTimeIndex)
        {
            var timeSettings = sessionConditions[selectedTimeIndex].environmentalSettings;
            if (timeSettings != null)
            {
                // Copy time-based settings
                combined.lightIntensity = timeSettings.lightIntensity;
                combined.lightColor = timeSettings.lightColor;
                combined.ambientColor = timeSettings.ambientColor;
                combined.skyboxMaterial = timeSettings.skyboxMaterial;
            }
        }

        if (sessionConditions.Count > selectedWeatherIndex)
        {
            var weatherSettings = sessionConditions[selectedWeatherIndex].environmentalSettings;
            if (weatherSettings != null)
            {
                // Copy weather-based settings
                combined.fogEnabled = weatherSettings.fogEnabled;
                combined.fogColor = weatherSettings.fogColor;
                combined.fogDensity = weatherSettings.fogDensity;
                combined.weatherParticles = weatherSettings.weatherParticles;
                combined.particleSettings = weatherSettings.particleSettings;
                combined.windStrength = weatherSettings.windStrength;
            }
        }

        return combined;
    }

    private IEnumerator LoadDrivingSceneWithEnvironment()
    {
        yield return new WaitForSeconds(0.5f);

        if (currentTrackIndex < availableTracks.Count &&
            !string.IsNullOrEmpty(availableTracks[currentTrackIndex].sceneName))
        {
            SceneManager.LoadScene(availableTracks[currentTrackIndex].sceneName);
        }
        else
        {
            Debug.LogWarning("Track scene name not specified!");
        }
    }

    // ... rest of existing methods remain the same
}
