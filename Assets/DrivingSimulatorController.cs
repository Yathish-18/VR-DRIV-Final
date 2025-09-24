using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class DriveSimulatorUI : MonoBehaviour
{
    [Header("Track Display")]
    public Image trackPreviewImage;
    public TextMeshProUGUI trackNameText;
    public TextMeshProUGUI trackNumberText;
    public TextMeshProUGUI trackLengthText;
    public TextMeshProUGUI totalTurnsText;
    public Button previousTrackBtn;
    public Button nextTrackBtn;
    public Image countryFlag;

    [Header("Session Setup - Time of Day")]
    public Button dayButton;
    public Button nightButton;
    public TextMeshProUGUI timeChooseText;

    [Header("Session Setup - Weather")]
    public Button clearButton;
    public Button rainyButton;
    public TextMeshProUGUI weatherChooseText;

    [Header("Preview Display")]
    public TextMeshProUGUI previewLabel;
    public Image previewTrackImage;

    [Header("Action Buttons")]
    public Button driveButton;
    public Button practiceButton;
    public Button instructionButton;
    public Button settingButton;
    public Button exitButton;

    [Header("Weather Effects")]
    public ParticleSystem rainParticleSystem;
    public ParticleSystem mistParticleSystem;
    public Light sunLight;
    public Light moonLight;
    public Camera mainCamera;

    [Header("Lighting Settings")]
    [Range(0, 3)]
    public float sunnyIntensity = 1.5f;
    [Range(0, 2)]
    public float rainyDayIntensity = 0.6f;
    [Range(0, 1)]
    public float nightIntensity = 0.08f;

    [Header("Fog Settings")]
    [Range(0, 0.1f)]
    public float clearFogDensity = 0.005f;
    [Range(0, 0.2f)]
    public float rainyFogDensity = 0.035f;
    [Range(0, 0.08f)]
    public float nightFogDensity = 0.02f;

    [Header("Environment Colors")]
    public Color sunnyFogColor = new Color(0.76f, 0.85f, 0.95f, 1f);
    public Color rainyFogColor = new Color(0.45f, 0.5f, 0.6f, 1f);
    public Color nightFogColor = new Color(0.15f, 0.15f, 0.35f, 1f);
    public Color sunnySkyColor = new Color(0.53f, 0.81f, 0.92f, 1f);
    public Color rainySkyColor = new Color(0.35f, 0.4f, 0.5f, 1f);
    public Color nightSkyColor = new Color(0.02f, 0.02f, 0.1f, 1f);

    [Header("UI Animation")]
    public float transitionDuration = 1.5f;
    public float buttonAnimDuration = 0.2f;
    public AnimationCurve smoothCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private List<TrackData> tracks = new List<TrackData>();
    private int currentTrackIndex = 0;
    private TimeOfDay selectedTime = TimeOfDay.Day;
    private WeatherType selectedWeather = WeatherType.Clear;
    private Coroutine weatherTransitionCoroutine;

    [System.Serializable]
    public class TrackData
    {
        public string trackName;
        public int trackNumber;
        public string trackLength;
        public int totalTurns;
        public Sprite trackPreviewSprite;
        public Sprite flagSprite;
        public string country;
    }

    public enum TimeOfDay { Day, Night }
    public enum WeatherType { Clear, Rainy }

    void Start()
    {
        InitializeTrackData();
        SetupButtonListeners();
        InitializeEnvironment();
        UpdateTrackDisplay();
        UpdatePreviewDisplay();
        ApplyInitialWeatherSettings();
    }

    void InitializeTrackData()
    {
        tracks.Add(new TrackData
        {
            trackName = "HILLS SIM-CIRCUIT",
            trackNumber = 2,
            trackLength = "4.2KM",
            totalTurns = 15,
            country = "India"
        });

        tracks.Add(new TrackData
        {
            trackName = "MOUNTAIN SPEEDWAY",
            trackNumber = 1,
            trackLength = "3.8KM",
            totalTurns = 12,
            country = "India"
        });

        tracks.Add(new TrackData
        {
            trackName = "COASTAL CIRCUIT",
            trackNumber = 3,
            trackLength = "5.1KM",
            totalTurns = 18,
            country = "India"
        });
    }

    void InitializeEnvironment()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        SetupParticleSystems();

        if (sunLight != null) sunLight.enabled = true;
        if (moonLight != null) moonLight.enabled = false;
    }

    void SetupParticleSystems()
    {
        if (rainParticleSystem != null)
        {
            var main = rainParticleSystem.main;
            main.startLifetime = 3f;
            main.startSpeed = 12f;
            main.gravityModifier = 1.5f;

            var emission = rainParticleSystem.emission;
            emission.rateOverTime = 400f;

            var shape = rainParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(50, 1, 50);

            rainParticleSystem.Stop();
        }

        if (mistParticleSystem != null)
        {
            var main = mistParticleSystem.main;
            main.startLifetime = 8f;
            main.startSpeed = 2f;
            main.startSize = 3f;

            var emission = mistParticleSystem.emission;
            emission.rateOverTime = 60f;

            mistParticleSystem.Stop();
        }
    }

    void SetupButtonListeners()
    {
        // Track navigation
        previousTrackBtn.onClick.AddListener(() => ChangeTrack(-1));
        nextTrackBtn.onClick.AddListener(() => ChangeTrack(1));

        // Time of day selection
        dayButton.onClick.AddListener(() => SelectTimeOfDay(TimeOfDay.Day));
        nightButton.onClick.AddListener(() => SelectTimeOfDay(TimeOfDay.Night));

        // Weather selection
        clearButton.onClick.AddListener(() => SelectWeather(WeatherType.Clear));
        rainyButton.onClick.AddListener(() => SelectWeather(WeatherType.Rainy));

        // Action buttons
        driveButton.onClick.AddListener(StartDriving);
        practiceButton.onClick.AddListener(StartPractice);
        instructionButton.onClick.AddListener(ShowInstructions);
        settingButton.onClick.AddListener(OpenSettings);
        exitButton.onClick.AddListener(ExitApplication);
    }

    void ChangeTrack(int direction)
    {
        StartCoroutine(AnimateTrackChange(direction));
    }

    IEnumerator AnimateTrackChange(int direction)
    {
        RectTransform trackRect = trackPreviewImage.GetComponent<RectTransform>();
        Vector3 originalPos = trackRect.anchoredPosition;
        Vector3 exitPos = originalPos + new Vector3(direction > 0 ? -800f : 800f, 0, 0);

        float elapsed = 0;
        while (elapsed < buttonAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / buttonAnimDuration;
            trackRect.anchoredPosition = Vector3.Lerp(originalPos, exitPos, smoothCurve.Evaluate(progress));
            yield return null;
        }

        currentTrackIndex = (currentTrackIndex + direction + tracks.Count) % tracks.Count;
        UpdateTrackDisplay();

        Vector3 enterPos = originalPos + new Vector3(direction > 0 ? 800f : -800f, 0, 0);
        trackRect.anchoredPosition = enterPos;

        elapsed = 0;
        while (elapsed < buttonAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / buttonAnimDuration;
            trackRect.anchoredPosition = Vector3.Lerp(enterPos, originalPos, smoothCurve.Evaluate(progress));
            yield return null;
        }

        trackRect.anchoredPosition = originalPos;
        UpdatePreviewDisplay();
    }

    void UpdateTrackDisplay()
    {
        if (tracks.Count > 0)
        {
            TrackData current = tracks[currentTrackIndex];
            trackNameText.text = current.trackName;
            trackNumberText.text = $"TRACK NO : {current.trackNumber:D2}";
            trackLengthText.text = $"Track Length : {current.trackLength}";
            totalTurnsText.text = $"Total Turns : {current.totalTurns}";

            if (current.trackPreviewSprite != null)
                trackPreviewImage.sprite = current.trackPreviewSprite;

            if (current.flagSprite != null && countryFlag != null)
                countryFlag.sprite = current.flagSprite;
        }
    }

    void SelectTimeOfDay(TimeOfDay timeOfDay)
    {
        selectedTime = timeOfDay;
        StartCoroutine(AnimateButtonSelection(timeOfDay == TimeOfDay.Day ? dayButton : nightButton));
        ApplyWeatherAndLighting();
        UpdatePreviewDisplay();
    }

    void SelectWeather(WeatherType weather)
    {
        selectedWeather = weather;
        StartCoroutine(AnimateButtonSelection(weather == WeatherType.Clear ? clearButton : rainyButton));
        ApplyWeatherAndLighting();
        UpdatePreviewDisplay();
    }

    void ApplyWeatherAndLighting()
    {
        if (weatherTransitionCoroutine != null)
            StopCoroutine(weatherTransitionCoroutine);

        weatherTransitionCoroutine = StartCoroutine(TransitionEnvironment());
    }

    IEnumerator TransitionEnvironment()
    {
        float currentLightIntensity = sunLight != null ? sunLight.intensity : 0;
        float currentFogDensity = RenderSettings.fogDensity;
        Color currentFogColor = RenderSettings.fogColor;
        Color currentSkyColor = mainCamera.backgroundColor;

        float targetLightIntensity = GetTargetLightIntensity();
        float targetFogDensity = GetTargetFogDensity();
        Color targetFogColor = GetTargetFogColor();
        Color targetSkyColor = GetTargetSkyColor();

        float elapsed = 0;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / transitionDuration;
            float curveProgress = smoothCurve.Evaluate(progress);

            UpdateLighting(currentLightIntensity, targetLightIntensity, curveProgress);

            RenderSettings.fogDensity = Mathf.Lerp(currentFogDensity, targetFogDensity, curveProgress);
            RenderSettings.fogColor = Color.Lerp(currentFogColor, targetFogColor, curveProgress);
            mainCamera.backgroundColor = Color.Lerp(currentSkyColor, targetSkyColor, curveProgress);

            yield return null;
        }

        UpdateParticleEffects();
    }

    void UpdateLighting(float currentIntensity, float targetIntensity, float progress)
    {
        if (selectedTime == TimeOfDay.Day)
        {
            if (sunLight != null)
            {
                sunLight.enabled = true;
                sunLight.intensity = Mathf.Lerp(currentIntensity, targetIntensity, progress);
            }
            if (moonLight != null) moonLight.enabled = false;
        }
        else
        {
            if (sunLight != null) sunLight.enabled = false;
            if (moonLight != null)
            {
                moonLight.enabled = true;
                moonLight.intensity = Mathf.Lerp(currentIntensity, nightIntensity, progress);
            }
        }
    }

    void UpdateParticleEffects()
    {
        if (rainParticleSystem != null)
        {
            if (selectedWeather == WeatherType.Rainy)
            {
                if (!rainParticleSystem.isPlaying)
                {
                    rainParticleSystem.Play();
                }

                var emission = rainParticleSystem.emission;
                emission.rateOverTime = selectedTime == TimeOfDay.Day ? 400f : 250f;

                var main = rainParticleSystem.main;
                main.startColor = selectedTime == TimeOfDay.Day ?
                    new Color(0.85f, 0.85f, 0.95f, 0.7f) :
                    new Color(0.6f, 0.6f, 0.8f, 0.5f);
            }
            else
            {
                if (rainParticleSystem.isPlaying)
                    rainParticleSystem.Stop();
            }
        }

        if (mistParticleSystem != null)
        {
            if (selectedWeather == WeatherType.Rainy)
            {
                if (!mistParticleSystem.isPlaying)
                {
                    mistParticleSystem.Play();
                }

                var emission = mistParticleSystem.emission;
                emission.rateOverTime = selectedTime == TimeOfDay.Day ? 60f : 90f;
            }
            else
            {
                if (mistParticleSystem.isPlaying)
                    mistParticleSystem.Stop();
            }
        }
    }

    float GetTargetLightIntensity()
    {
        if (selectedTime == TimeOfDay.Night)
            return nightIntensity;

        return selectedWeather == WeatherType.Rainy ? rainyDayIntensity : sunnyIntensity;
    }

    float GetTargetFogDensity()
    {
        if (selectedWeather == WeatherType.Rainy)
            return rainyFogDensity;

        return selectedTime == TimeOfDay.Night ? nightFogDensity : clearFogDensity;
    }

    Color GetTargetFogColor()
    {
        if (selectedWeather == WeatherType.Rainy)
            return rainyFogColor;

        return selectedTime == TimeOfDay.Night ? nightFogColor : sunnyFogColor;
    }

    Color GetTargetSkyColor()
    {
        if (selectedTime == TimeOfDay.Night)
            return nightSkyColor;

        return selectedWeather == WeatherType.Rainy ? rainySkyColor : sunnySkyColor;
    }

    IEnumerator AnimateButtonSelection(Button selectedButton)
    {
        Transform buttonTransform = selectedButton.transform;
        Vector3 originalScale = buttonTransform.localScale;

        float elapsed = 0;
        while (elapsed < buttonAnimDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / buttonAnimDuration;
            float scale = 1f + Mathf.Sin(progress * Mathf.PI) * 0.15f;
            buttonTransform.localScale = originalScale * scale;
            yield return null;
        }

        buttonTransform.localScale = originalScale;
    }

    void UpdatePreviewDisplay()
    {
        string timeStr = selectedTime == TimeOfDay.Day ? "DAYLIGHT" : "NIGHT";
        string weatherStr = selectedWeather == WeatherType.Clear ? "CLEAR" : "RAINY";
        previewLabel.text = $"{timeStr} - {weatherStr}";
    }

    void ApplyInitialWeatherSettings()
    {
        RenderSettings.fogDensity = clearFogDensity;
        RenderSettings.fogColor = sunnyFogColor;
        mainCamera.backgroundColor = sunnySkyColor;

        if (sunLight != null)
        {
            sunLight.intensity = sunnyIntensity;
            sunLight.enabled = true;
        }

        if (moonLight != null)
            moonLight.enabled = false;
    }

    // Button Action Methods
    void StartDriving()
    {
        Debug.Log($"Starting Drive - Track: {tracks[currentTrackIndex].trackName}, {selectedTime}, {selectedWeather}");
        StartCoroutine(LaunchDriveMode());
    }

    IEnumerator LaunchDriveMode()
    {
        yield return new WaitForSeconds(0.5f);
        GetComponent<UIManager>().ShowPanel(2);
        // Load driving scene
    }

    void StartPractice()
    {
        Debug.Log("Practice Mode Selected");
    }

    void ShowInstructions()
    {
        Debug.Log("Instructions Panel Opening");
    }

    void OpenSettings()
    {
        Debug.Log("Settings Menu Opening");
    }

    void ExitApplication()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}