using UnityEngine;
using UnityEngine.UI;

public class DynamicRaceMenu : MonoBehaviour
{
    [System.Serializable]
    public class TrackData
    {
        public string trackName;
        public string trackNumber;
        public string trackLength;
        public string totalTurns;
        public Sprite previewImage;
        public Sprite trackMap;
    }

    [System.Serializable]
    public class TimeOfDayData
    {
        public string timeLabel;
        public Sprite previewImage;
    }

    [System.Serializable]
    public class WeatherData
    {
        public string weatherLabel;
        public Sprite previewImage;
    }

    [Header("Track Settings")]
    public TrackData[] tracks;
    private int currentTrackIndex = 0;

    [Header("Time of Day Settings")]
    public TimeOfDayData[] timesOfDay;
    private int currentTimeIndex = 0;

    [Header("Weather Settings")]
    public WeatherData[] weatherTypes;
    private int currentWeatherIndex = 0;

    [Header("UI References")]
    public Text trackNameText;
    public Text trackNumberText;
    public Text trackLengthText;
    public Text totalTurnsText;
    public Image trackPreviewImage;
    public Image trackMapImage;
    public Text timeOfDayText;
    public Text weatherText;
    public Image previewImage;

    public Button nextTrackButton;
    public Button prevTrackButton;
    public Button changeTimeButton;
    public Button changeWeatherButton;

    void Start()
    {
        UpdateUI();

        nextTrackButton.onClick.AddListener(() => ChangeTrack(1));
        prevTrackButton.onClick.AddListener(() => ChangeTrack(-1));
        changeTimeButton.onClick.AddListener(ChangeTimeOfDay);
        changeWeatherButton.onClick.AddListener(ChangeWeather);
    }

    void ChangeTrack(int direction)
    {
        currentTrackIndex += direction;

        if (currentTrackIndex >= tracks.Length) currentTrackIndex = 0;
        if (currentTrackIndex < 0) currentTrackIndex = tracks.Length - 1;

        UpdateUI();
    }

    void ChangeTimeOfDay()
    {
        currentTimeIndex++;
        if (currentTimeIndex >= timesOfDay.Length) currentTimeIndex = 0;
        UpdateUI();
    }

    void ChangeWeather()
    {
        currentWeatherIndex++;
        if (currentWeatherIndex >= weatherTypes.Length) currentWeatherIndex = 0;
        UpdateUI();
    }

    void UpdateUI()
    {
        // Update Track
        trackNameText.text = tracks[currentTrackIndex].trackName;
        trackNumberText.text = tracks[currentTrackIndex].trackNumber;
        trackLengthText.text = tracks[currentTrackIndex].trackLength;
        totalTurnsText.text = tracks[currentTrackIndex].totalTurns;
        trackPreviewImage.sprite = tracks[currentTrackIndex].previewImage;
        trackMapImage.sprite = tracks[currentTrackIndex].trackMap;

        // Update Time of Day
        timeOfDayText.text = timesOfDay[currentTimeIndex].timeLabel;

        // Update Weather
        weatherText.text = weatherTypes[currentWeatherIndex].weatherLabel;

        // Update Preview Image according to Time + Weather
        // Priority: Weather image → else time image
        if (weatherTypes[currentWeatherIndex].previewImage != null)
        {
            previewImage.sprite = weatherTypes[currentWeatherIndex].previewImage;
        }
        else
        {
            previewImage.sprite = timesOfDay[currentTimeIndex].previewImage;
        }
    }
}
