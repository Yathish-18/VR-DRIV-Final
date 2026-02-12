using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Proyecto26; // Required for RestClient

public class GamePersistenceManager : MonoBehaviour
{
    public static GamePersistenceManager Instance { get; private set; }

    [Header("Database Settings")]
    public RacingGameDatabaseSO gameDatabase;
    [Tooltip("Your Firebase Realtime Database URL (ends in .firebaseio.com)")]
    public string databaseUrl = "https://YOUR-PROJECT-ID.firebaseio.com";

    [Header("Player Identity")]
    public string playerName = "Driver"; // Stores the name safely

    [Header("Current Session Data")]
    public TrackDataSO selectedTrack;
    public WeatherConditionSO selectedWeather;
    public TimeOfDaySettingsSO selectedTime;
    public System.DateTime sessionStartTime;

    [Header("Player Progress")]
    public int totalRaces = 0;
    public float bestLapTime = float.MaxValue;
    public List<string> completedTracks = new List<string>();

    [Header("Race Results")]
    public float lastLapTime = 0f;
    public int lastRacePosition = 0;
    public bool isNewRecord = false;

    [Header("Settings")]
    public bool autoSave = true;
    public bool enableDebugLogs = true;
    [SerializeField] private string trackSelectionSceneName = "TrackSelection";
    [SerializeField] private bool clearDataOnReturnToMenu = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadGameData();
    }

    private void Start()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (autoSave && Instance == this) SaveGameData();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnvironmentManager envManager = FindObjectOfType<EnvironmentManager>();
        if (envManager != null)
        {
            envManager.UpdateEnvironment(selectedWeather, selectedTime);
        }

        if (clearDataOnReturnToMenu && scene.name == trackSelectionSceneName)
        {
            ResetSessionData();
        }
    }

    // --- HELPER METHODS ---
    public bool HasTrackData() => selectedTrack != null;
    public bool HasWeatherData() => selectedWeather != null;
    public bool HasTimeData() => selectedTime != null;

    public TrackDataSO GetSelectedTrack() => selectedTrack;
    public WeatherConditionSO GetSelectedWeather() => selectedWeather;
    public TimeOfDaySettingsSO GetSelectedTime() => selectedTime;

    // --- DATA MANAGEMENT METHODS ---

    // This is called by the UI Script
    public void SetPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name)) return;
        playerName = name;
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.Save();
    }

    public void SetSessionData(TrackDataSO track, WeatherConditionSO weather, TimeOfDaySettingsSO time)
    {
        selectedTrack = track;
        selectedWeather = weather;
        selectedTime = time;
        sessionStartTime = System.DateTime.Now;
        if (autoSave) SaveGameData();
    }

    public void UpdateRaceResults(float lapTime, int position, bool newRecord = false)
    {
        lastLapTime = lapTime;
        lastRacePosition = position;
        isNewRecord = newRecord;

        totalRaces++;
        if (lapTime < bestLapTime && lapTime > 0)
            bestLapTime = lapTime;

        if (selectedTrack != null && !completedTracks.Contains(selectedTrack.trackName))
            completedTracks.Add(selectedTrack.trackName);

        if (autoSave) SaveGameData();

        // --- REST CLIENT SAVE ---
        SaveResultToFirebase(lapTime, position);

        if (enableDebugLogs) Debug.Log($"Race Saved. Racer: {playerName}, Time: {lapTime}");
    }

    // --- REST CLIENT LOGIC ---
    private void SaveResultToFirebase(float time, int position)
    {
        if (string.IsNullOrEmpty(databaseUrl) || databaseUrl.Contains("YOUR-PROJECT-ID"))
        {
            if (enableDebugLogs) Debug.LogWarning("Firebase URL not set properly!");
            return;
        }

        RaceData data = new RaceData(playerName, time, position, selectedTrack != null ? selectedTrack.trackName : "Unknown");
        string url = $"{databaseUrl}/race_results.json";

        RestClient.Post(url, data).Then(response => {
            if (enableDebugLogs) Debug.Log($"Success! Data uploaded. Status: {response.StatusCode}");
        }).Catch(err => {
            Debug.LogError($"Error uploading data: {err.Message}");
        });
    }

    public void ResetSessionData()
    {
        selectedTrack = null;
        selectedWeather = null;
        selectedTime = null;
        PlayerPrefs.DeleteKey("SelectedTrack");
        PlayerPrefs.DeleteKey("SelectedWeather");
        PlayerPrefs.DeleteKey("SelectedTime");
    }

    public void ResetAllData()
    {
        ResetSessionData();
        totalRaces = 0;
        bestLapTime = float.MaxValue;
        completedTracks.Clear();
        lastLapTime = 0f;
        lastRacePosition = 0;
        isNewRecord = false;
        playerName = "Racer";
        PlayerPrefs.DeleteAll();
    }

    // --- SAVE / LOAD SYSTEM ---
    public void SaveGameData()
    {
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetString("SelectedTrack", selectedTrack != null ? selectedTrack.trackName : "");
        PlayerPrefs.SetString("SelectedWeather", selectedWeather != null ? selectedWeather.weatherName : "");
        PlayerPrefs.SetString("SelectedTime", selectedTime != null ? selectedTime.timeName : "");
        PlayerPrefs.SetInt("TotalRaces", totalRaces);
        PlayerPrefs.SetFloat("BestLapTime", bestLapTime);
        PlayerPrefs.SetString("CompletedTracks", string.Join(",", completedTracks));
        PlayerPrefs.SetFloat("LastLapTime", lastLapTime);
        PlayerPrefs.SetInt("LastRacePosition", lastRacePosition);
        PlayerPrefs.Save();
    }

    public void LoadGameData()
    {
        playerName = PlayerPrefs.GetString("PlayerName", "Racer");

        if (gameDatabase != null)
        {
            string tName = PlayerPrefs.GetString("SelectedTrack", "");
            string wName = PlayerPrefs.GetString("SelectedWeather", "");
            string timeName = PlayerPrefs.GetString("SelectedTime", "");
            if (!string.IsNullOrEmpty(tName)) selectedTrack = gameDatabase.AvailableTracks.Find(t => t.trackName == tName);
            if (!string.IsNullOrEmpty(wName)) selectedWeather = gameDatabase.WeatherConditions.Find(w => w.weatherName == wName);
            if (!string.IsNullOrEmpty(timeName)) selectedTime = gameDatabase.TimeSettings.Find(t => t.timeName == timeName);
        }
        totalRaces = PlayerPrefs.GetInt("TotalRaces", 0);
        bestLapTime = PlayerPrefs.GetFloat("BestLapTime", float.MaxValue);
        string tracks = PlayerPrefs.GetString("CompletedTracks", "");
        if (!string.IsNullOrEmpty(tracks)) completedTracks = new List<string>(tracks.Split(','));
        lastLapTime = PlayerPrefs.GetFloat("LastLapTime", 0f);
        lastRacePosition = PlayerPrefs.GetInt("LastRacePosition", 0);
    }
}

[System.Serializable]
public class RaceData
{
    public string username;
    public float lapTime;
    public int position;
    public string trackName;
    public string timestamp;

    public RaceData(string name, float time, int pos, string track)
    {
        this.username = name;
        this.lapTime = time;
        this.position = pos;
        this.trackName = track;
        this.timestamp = System.DateTime.Now.ToString();
    }
}