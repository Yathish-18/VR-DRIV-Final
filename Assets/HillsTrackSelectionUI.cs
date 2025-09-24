using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[System.Serializable]
public class HillsTrackData
{
    [Header("Track Information")]
    public string trackName = "HILLS SIM-CIRCUIT";
    public string trackNumber = "02";
    public string countryName = "India";
    public Texture2D countryFlag;
    public float trackLength = 4.2f; // km
    public string totalTurns = "XX";
    public string sceneName;

    [Header("Track Preview")]
    public Texture2D trackLayoutImage;
    public Texture2D trackPreviewImage;

    [Header("Track Description")]
    [TextArea(3, 5)]
    public string trackDescription = "A challenging hill circuit with elevation changes and technical corners.";
}

[System.Serializable]
public class SessionCondition
{
    [Header("Time of Day")]
    public string timeName = "DAY";
    public Sprite timeIcon;
    public Color lightColor = Color.white;
    public float lightIntensity = 1.2f;
    public Material skyboxMaterial;

    [Header("Weather")]
    public string weatherName = "CLEAR";
    public Sprite weatherIcon;
    public Color skyboxTint = Color.white;
    public float fogDensity = 0.01f;
}

public class HillsTrackSelectionUI : MonoBehaviour
{
    [Header("Track Data List")]
    [SerializeField] private List<HillsTrackData> availableTracks = new List<HillsTrackData>();
    [SerializeField] private int currentTrackIndex = 0;

    [Header("Main UI References")]
    [SerializeField] private TextMeshProUGUI trackNameText;
    [SerializeField] private TextMeshProUGUI trackNumberText;
    [SerializeField] private TextMeshProUGUI trackLengthText;
    [SerializeField] private TextMeshProUGUI totalTurnsText;
    [SerializeField] private RawImage countryFlagImage;
    [SerializeField] private RawImage trackLayoutImage;
    [SerializeField] private RawImage trackPreviewImage;

    [Header("Navigation Controls")]
    [SerializeField] private Button previousTrackButton;
    [SerializeField] private Button nextTrackButton;
    [SerializeField] private Button practiceButton;
    [SerializeField] private Button instructionButton;
    [SerializeField] private Button settingButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button driveButton;

    [Header("Session Setup")]
    [SerializeField] private List<SessionCondition> sessionConditions = new List<SessionCondition>();
    [SerializeField] private Button dayButton;
    [SerializeField] private Button nightButton;
    [SerializeField] private Button clearWeatherButton;
    [SerializeField] private Button rainyWeatherButton;
    [SerializeField] private int selectedTimeIndex = 0; // 0 = Day, 1 = Night
    [SerializeField] private int selectedWeatherIndex = 0; // 0 = Clear, 1 = Rainy

    [Header("Status Display")]
    [SerializeField] private TextMeshProUGUI timeStatusText;
    [SerializeField] private TextMeshProUGUI weatherStatusText;
    [SerializeField] private Image timeStatusIcon;
    [SerializeField] private Image weatherStatusIcon;

    [Header("Track Preview Window")]
    [SerializeField] private GameObject trackPreviewWindow;
    [SerializeField] private TextMeshProUGUI previewStatusText;
    [SerializeField] private Button previewCloseButton;

    [Header("Animation Settings")]
    [SerializeField] private float transitionDuration = 0.5f;
    [SerializeField] private float fadeAnimationDuration = 0.3f;
    [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Animation references
    private RectTransform trackLayoutRect;
    private RectTransform trackPreviewRect;
    private CanvasGroup trackInfoCanvasGroup;
    private Vector2 trackLayoutOriginalPos;
    private Vector2 trackPreviewOriginalPos;

    // Game session data
    public static HillsSessionData CurrentSessionData { get; private set; }

    private void Awake()
    {
        InitializeComponents();
        SetupButtonListeners();
        CurrentSessionData = new HillsSessionData();

        // Initialize with default track if list is empty
        if (availableTracks.Count == 0)
        {
            CreateDefaultTrackData();
        }
    }

    private void Start()
    {
        InitializeUI();
        UpdateTrackDisplay();
        UpdateSessionStatus();
        UpdatePreviewWindow();
    }

    private void InitializeComponents()
    {
        // Get RectTransform references for animations
        if (trackLayoutImage != null)
        {
            trackLayoutRect = trackLayoutImage.GetComponent<RectTransform>();
            trackLayoutOriginalPos = trackLayoutRect.anchoredPosition;
        }

        if (trackPreviewImage != null)
        {
            trackPreviewRect = trackPreviewImage.GetComponent<RectTransform>();
            trackPreviewOriginalPos = trackPreviewRect.anchoredPosition;
        }

        // Setup canvas group for track info animations
        GameObject trackInfoParent = trackNameText?.transform.parent?.gameObject;
        if (trackInfoParent != null)
        {
            trackInfoCanvasGroup = trackInfoParent.GetComponent<CanvasGroup>();
            if (trackInfoCanvasGroup == null)
                trackInfoCanvasGroup = trackInfoParent.AddComponent<CanvasGroup>();
        }
    }

    private void SetupButtonListeners()
    {
        // Track navigation
        previousTrackButton?.onClick.AddListener(SelectPreviousTrack);
        nextTrackButton?.onClick.AddListener(SelectNextTrack);

        // Main menu buttons
        practiceButton?.onClick.AddListener(StartPracticeMode);
        instructionButton?.onClick.AddListener(ShowInstructions);
        settingButton?.onClick.AddListener(OpenSettings);
        exitButton?.onClick.AddListener(ExitApplication);
        driveButton?.onClick.AddListener(StartDriving);

        // Session setup buttons
        dayButton?.onClick.AddListener(() => SelectTimeOfDay(0));
        nightButton?.onClick.AddListener(() => SelectTimeOfDay(1));
        clearWeatherButton?.onClick.AddListener(() => SelectWeather(0));
        rainyWeatherButton?.onClick.AddListener(() => SelectWeather(1));

        // Preview window
        previewCloseButton?.onClick.AddListener(ClosePreviewWindow);
    }

    private void InitializeUI()
    {
        UpdateNavigationButtons();
        UpdateSessionButtons();

        // Set initial preview window state
        if (trackPreviewWindow != null)
            trackPreviewWindow.SetActive(true);
    }

    private void CreateDefaultTrackData()
    {
        HillsTrackData defaultTrack = new HillsTrackData();
        // Set default values as defined in the class
        availableTracks.Add(defaultTrack);

        // Create default session conditions
        if (sessionConditions.Count == 0)
        {
            SessionCondition dayCondition = new SessionCondition();
            dayCondition.timeName = "DAY";
            dayCondition.weatherName = "CLEAR";

            SessionCondition nightCondition = new SessionCondition();
            nightCondition.timeName = "NIGHT";
            nightCondition.weatherName = "CLEAR";
            nightCondition.lightIntensity = 0.3f;

            sessionConditions.Add(dayCondition);
            sessionConditions.Add(nightCondition);
        }
    }

    public void SelectPreviousTrack()
    {
        if (availableTracks.Count <= 1) return;

        currentTrackIndex = (currentTrackIndex - 1 + availableTracks.Count) % availableTracks.Count;
        AnimateTrackChange(-1);
    }

    public void SelectNextTrack()
    {
        if (availableTracks.Count <= 1) return;

        currentTrackIndex = (currentTrackIndex + 1) % availableTracks.Count;
        AnimateTrackChange(1);
    }

    private void AnimateTrackChange(int direction)
    {
        StartCoroutine(TrackChangeAnimation(direction));
    }

    private IEnumerator TrackChangeAnimation(int direction)
    {
        float slideDistance = 400f * direction;
        float elapsedTime = 0f;

        // Store initial positions
        Vector2 layoutStartPos = trackLayoutRect.anchoredPosition;
        Vector2 previewStartPos = trackPreviewRect.anchoredPosition;
        float initialAlpha = trackInfoCanvasGroup.alpha;

        // Animate out
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsedTime / transitionDuration);

            if (trackLayoutRect != null)
                trackLayoutRect.anchoredPosition = Vector2.Lerp(layoutStartPos, layoutStartPos + Vector2.right * slideDistance, t);

            if (trackPreviewRect != null)
                trackPreviewRect.anchoredPosition = Vector2.Lerp(previewStartPos, previewStartPos + Vector2.right * slideDistance, t);

            if (trackInfoCanvasGroup != null)
                trackInfoCanvasGroup.alpha = Mathf.Lerp(initialAlpha, 0f, t);

            yield return null;
        }

        // Update display
        UpdateTrackDisplay();
        UpdatePreviewWindow();

        // Reset positions for slide in
        if (trackLayoutRect != null)
            trackLayoutRect.anchoredPosition = trackLayoutOriginalPos + Vector2.right * (-slideDistance);

        if (trackPreviewRect != null)
            trackPreviewRect.anchoredPosition = trackPreviewOriginalPos + Vector2.right * (-slideDistance);

        // Animate in
        elapsedTime = 0f;
        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = transitionCurve.Evaluate(elapsedTime / transitionDuration);

            if (trackLayoutRect != null)
                trackLayoutRect.anchoredPosition = Vector2.Lerp(trackLayoutOriginalPos + Vector2.right * (-slideDistance), trackLayoutOriginalPos, t);

            if (trackPreviewRect != null)
                trackPreviewRect.anchoredPosition = Vector2.Lerp(trackPreviewOriginalPos + Vector2.right * (-slideDistance), trackPreviewOriginalPos, t);

            if (trackInfoCanvasGroup != null)
                trackInfoCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        UpdateNavigationButtons();
    }

    private void UpdateTrackDisplay()
    {
        if (availableTracks.Count == 0 || currentTrackIndex >= availableTracks.Count) return;

        HillsTrackData currentTrack = availableTracks[currentTrackIndex];

        // Update track information
        trackNameText?.SetText(currentTrack.trackName);
        trackNumberText?.SetText($"TRACK NO : {currentTrack.trackNumber}");
        trackLengthText?.SetText($"Track Length : {currentTrack.trackLength:F1}km");
        totalTurnsText?.SetText($"Total Turns : {currentTrack.totalTurns}");

        // Update images
        if (countryFlagImage != null && currentTrack.countryFlag != null)
            countryFlagImage.texture = currentTrack.countryFlag;

        if (trackLayoutImage != null && currentTrack.trackLayoutImage != null)
            trackLayoutImage.texture = currentTrack.trackLayoutImage;

        if (trackPreviewImage != null && currentTrack.trackPreviewImage != null)
            trackPreviewImage.texture = currentTrack.trackPreviewImage;
    }

    public void SelectTimeOfDay(int timeIndex)
    {
        selectedTimeIndex = Mathf.Clamp(timeIndex, 0, 1);
        UpdateSessionButtons();
        UpdateSessionStatus();
        UpdatePreviewWindow();
        AnimateButtonPress(timeIndex == 0 ? dayButton : nightButton);
    }

    public void SelectWeather(int weatherIndex)
    {
        selectedWeatherIndex = Mathf.Clamp(weatherIndex, 0, 1);
        UpdateSessionButtons();
        UpdateSessionStatus();
        UpdatePreviewWindow();
        AnimateButtonPress(weatherIndex == 0 ? clearWeatherButton : rainyWeatherButton);
    }

    private void AnimateButtonPress(Button button)
    {
        if (button != null)
        {
            StartCoroutine(ButtonPressAnimation(button.transform));
        }
    }

    private IEnumerator ButtonPressAnimation(Transform buttonTransform)
    {
        Vector3 originalScale = buttonTransform.localScale;
        Vector3 pressedScale = originalScale * 0.95f;

        // Scale down
        float elapsedTime = 0f;
        while (elapsedTime < 0.1f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / 0.1f;
            buttonTransform.localScale = Vector3.Lerp(originalScale, pressedScale, t);
            yield return null;
        }

        // Scale back up
        elapsedTime = 0f;
        while (elapsedTime < 0.1f)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / 0.1f;
            buttonTransform.localScale = Vector3.Lerp(pressedScale, originalScale, t);
            yield return null;
        }

        buttonTransform.localScale = originalScale;
    }

    private void UpdateNavigationButtons()
    {
        bool hasMultipleTracks = availableTracks.Count > 1;
        previousTrackButton?.gameObject.SetActive(hasMultipleTracks);
        nextTrackButton?.gameObject.SetActive(hasMultipleTracks);
    }

    private void UpdateSessionButtons()
    {
        // Update time of day buttons
        UpdateButtonState(dayButton, selectedTimeIndex == 0);
        UpdateButtonState(nightButton, selectedTimeIndex == 1);

        // Update weather buttons
        UpdateButtonState(clearWeatherButton, selectedWeatherIndex == 0);
        UpdateButtonState(rainyWeatherButton, selectedWeatherIndex == 1);
    }

    private void UpdateButtonState(Button button, bool isSelected)
    {
        if (button == null) return;

        ColorBlock colors = button.colors;
        if (isSelected)
        {
            colors.normalColor = new Color(0.2f, 0.8f, 1f, 1f); // Cyan highlight
            colors.selectedColor = new Color(0.2f, 0.8f, 1f, 1f);
        }
        else
        {
            colors.normalColor = Color.white;
            colors.selectedColor = Color.white;
        }
        button.colors = colors;
    }

    private void UpdateSessionStatus()
    {
        // Update time status
        string timeText = selectedTimeIndex == 0 ? "DAY" : "NIGHT";
        timeStatusText?.SetText($"TIME OF DAY\nCHOOSE ONE.\n{timeText}");

        // Update weather status  
        string weatherText = selectedWeatherIndex == 0 ? "CLEAR" : "RAINY";
        weatherStatusText?.SetText($"WEATHER\nCHOOSE ONE.\n{weatherText}");

        // Update icons if available
        if (sessionConditions.Count > selectedTimeIndex && timeStatusIcon != null)
        {
            if (sessionConditions[selectedTimeIndex].timeIcon != null)
                timeStatusIcon.sprite = sessionConditions[selectedTimeIndex].timeIcon;
        }

        if (sessionConditions.Count > selectedWeatherIndex && weatherStatusIcon != null)
        {
            if (sessionConditions[selectedWeatherIndex].weatherIcon != null)
                weatherStatusIcon.sprite = sessionConditions[selectedWeatherIndex].weatherIcon;
        }
    }

    private void UpdatePreviewWindow()
    {
        string timeCondition = selectedTimeIndex == 0 ? "DAYLIGHT" : "NIGHTTIME";
        string weatherCondition = selectedWeatherIndex == 0 ? "CLEAR" : "RAINY";

        previewStatusText?.SetText($"{timeCondition} - {weatherCondition}");
    }

    // Menu Functions
    public void StartPracticeMode()
    {
        Debug.Log("Starting Practice Mode");
        PrepareSessionData();
        // Add your practice mode loading logic here
        LoadGameScene("PracticeMode");
    }

    public void ShowInstructions()
    {
        Debug.Log("Showing Instructions");
        // Add your instructions panel logic here
        // InstructionPanel.SetActive(true);
    }

    public void OpenSettings()
    {
        Debug.Log("Opening Settings");
        // Add your settings menu logic here
        // SettingsPanel.SetActive(true);
    }

    public void ExitApplication()
    {
        Debug.Log("Exiting Application");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void StartDriving()
    {
        Debug.Log("Starting Driving Mode");
        PrepareSessionData();
        AnimateButtonPress(driveButton);
        StartCoroutine(LoadDrivingScene());
    }

    public void ClosePreviewWindow()
    {
        if (trackPreviewWindow != null)
            trackPreviewWindow.SetActive(false);
    }

    private void PrepareSessionData()
    {
        if (currentTrackIndex >= availableTracks.Count) return;

        CurrentSessionData.selectedTrack = availableTracks[currentTrackIndex];
        CurrentSessionData.timeOfDayIndex = selectedTimeIndex;
        CurrentSessionData.weatherIndex = selectedWeatherIndex;
        CurrentSessionData.sessionCondition = sessionConditions.Count > selectedTimeIndex ?
            sessionConditions[selectedTimeIndex] : null;
        CurrentSessionData.timestamp = System.DateTime.Now;

        Debug.Log($"Session prepared: Track {CurrentSessionData.selectedTrack.trackName}, " +
                  $"Time: {(selectedTimeIndex == 0 ? "Day" : "Night")}, " +
                  $"Weather: {(selectedWeatherIndex == 0 ? "Clear" : "Rainy")}");
    }

    private IEnumerator LoadDrivingScene()
    {
        yield return new WaitForSeconds(0.5f);
        LoadGameScene(null);
    }

    private void LoadGameScene(string specificScene = null)
    {
        string sceneToLoad = specificScene;

        if (string.IsNullOrEmpty(sceneToLoad) && currentTrackIndex < availableTracks.Count)
        {
            sceneToLoad = availableTracks[currentTrackIndex].sceneName;
        }

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogWarning("No scene specified for loading!");
        }
    }

    // Public methods for external access
    public HillsTrackData GetCurrentTrack()
    {
        if (currentTrackIndex < availableTracks.Count)
            return availableTracks[currentTrackIndex];
        return null;
    }

    public void AddTrack(HillsTrackData newTrack)
    {
        if (newTrack != null)
        {
            availableTracks.Add(newTrack);
            UpdateNavigationButtons();
        }
    }

    public void RemoveTrack(int trackIndex)
    {
        if (trackIndex >= 0 && trackIndex < availableTracks.Count)
        {
            availableTracks.RemoveAt(trackIndex);

            // Adjust current index if necessary
            if (currentTrackIndex >= availableTracks.Count)
                currentTrackIndex = Mathf.Max(0, availableTracks.Count - 1);

            UpdateTrackDisplay();
            UpdateNavigationButtons();
        }
    }

    public void SetTrackIndex(int index)
    {
        if (index >= 0 && index < availableTracks.Count)
        {
            currentTrackIndex = index;
            UpdateTrackDisplay();
        }
    }

    private void OnDestroy()
    {
        // Clean up any running coroutines
        StopAllCoroutines();
    }

#if UNITY_EDITOR
    [ContextMenu("Create Sample Track Data")]
    private void CreateSampleTrackData()
    {
        availableTracks.Clear();

        // Sample Track 1
        HillsTrackData track1 = new HillsTrackData();
        track1.trackName = "HILLS SIM-CIRCUIT";
        track1.trackNumber = "02";
        track1.countryName = "India";
        track1.trackLength = 4.2f;
        track1.totalTurns = "15";
        track1.sceneName = "HillsCircuit";
        availableTracks.Add(track1);

        // Sample Track 2
        HillsTrackData track2 = new HillsTrackData();
        track2.trackName = "MOUNTAIN RALLY";
        track2.trackNumber = "03";
        track2.countryName = "India";
        track2.trackLength = 6.8f;
        track2.totalTurns = "22";
        track2.sceneName = "MountainRally";
        availableTracks.Add(track2);

        Debug.Log("Sample track data created!");
    }
#endif
}

// Data structure to pass session information between scenes
[System.Serializable]
public class HillsSessionData
{
    public HillsTrackData selectedTrack;
    public SessionCondition sessionCondition;
    public int timeOfDayIndex;
    public int weatherIndex;
    public System.DateTime timestamp;
}

// Additional utility class for managing track data
public static class TrackDataUtility
{
    public static HillsTrackData CreateDefaultTrack(string name, string number)
    {
        HillsTrackData track = new HillsTrackData();
        track.trackName = name;
        track.trackNumber = number;
        return track;
    }

    public static SessionCondition CreateSessionCondition(string timeName, string weatherName, float lightIntensity = 1.0f)
    {
        SessionCondition condition = new SessionCondition();
        condition.timeName = timeName;
        condition.weatherName = weatherName;
        condition.lightIntensity = lightIntensity;
        return condition;
    }
}
