using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using Proyecto26;

public class GamePersistenceManager : MonoBehaviour
{
    public static GamePersistenceManager Instance { get; private set; }

    [Header("Database Settings")]
    public RacingGameDatabaseSO gameDatabase;
    [Tooltip("Your Firebase Realtime Database URL (ends in .firebaseio.com)")]
    public string databaseUrl = "https://YOUR-PROJECT-ID.firebaseio.com";

    [Header("Player Identity")]
    public string playerName = "Driver";

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
        // Only apply environment in game scenes, NOT the track selection menu.
        // TrackSelectionManager handles its own environment in the menu scene.
        if (scene.name == trackSelectionSceneName)
        {
            if (clearDataOnReturnToMenu)
                ResetSessionData();
            return; // ← skip environment apply in menu scene
        }

        // In game scenes: apply environment only if we actually have session data
        if (selectedWeather != null || selectedTime != null)
        {
            EnvironmentManager envManager = FindObjectOfType<EnvironmentManager>();
            if (envManager != null)
            {
                envManager.UpdateEnvironment(selectedWeather, selectedTime);
                if (enableDebugLogs)
                    Debug.Log($"[GamePersistenceManager] Environment applied in scene '{scene.name}' " +
                              $"— Weather: {selectedWeather?.weatherName ?? "none"}, Time: {selectedTime?.timeName ?? "none"}");
            }
        }
        else
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[GamePersistenceManager] Scene '{scene.name}' loaded but no session data set yet — environment not applied.");
        }
    }

    // --- HELPER METHODS ---
    public bool HasTrackData() => selectedTrack != null;
    public bool HasWeatherData() => selectedWeather != null;
    public bool HasTimeData() => selectedTime != null;

    public TrackDataSO GetSelectedTrack() => selectedTrack;
    public WeatherConditionSO GetSelectedWeather() => selectedWeather;
    public TimeOfDaySettingsSO GetSelectedTime() => selectedTime;

    // --- DATA MANAGEMENT ---

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

        if (enableDebugLogs)
            Debug.Log($"[GamePersistenceManager] Session data set — Track: {track?.trackName}, " +
                      $"Weather: {weather?.weatherName}, Time: {time?.timeName}");

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

        SaveResultToFirebase(lapTime, position);

        if (enableDebugLogs) Debug.Log($"[GamePersistenceManager] Race saved. Racer: {playerName}, Time: {lapTime}");
    }

    private void SaveResultToFirebase(float time, int position)
    {
        if (string.IsNullOrEmpty(databaseUrl) || databaseUrl.Contains("YOUR-PROJECT-ID"))
        {
            if (enableDebugLogs) Debug.LogWarning("[GamePersistenceManager] Firebase URL not configured.");
            return;
        }

        RaceData data = new RaceData(playerName, time, position, selectedTrack != null ? selectedTrack.trackName : "Unknown");
        string url = $"{databaseUrl}/race_results.json";

        RestClient.Post(url, data).Then(response => {
            if (enableDebugLogs) Debug.Log($"[GamePersistenceManager] Firebase upload success. Status: {response.StatusCode}");
        }).Catch(err => {
            Debug.LogError($"[GamePersistenceManager] Firebase upload failed: {err.Message}");
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

    // --- SAVE / LOAD ---

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
        username = name;
        lapTime = time;
        position = pos;
        trackName = track;
        timestamp = System.DateTime.Now.ToString();
    }
}