using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GamePersistenceManager : MonoBehaviour
{
    public static GamePersistenceManager Instance { get; private set; }

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

    private void Awake()
    {
        // Singleton pattern with DontDestroyOnLoad
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Load saved data
        LoadGameData();

        if (enableDebugLogs)
            Debug.Log("GamePersistenceManager initialized and data loaded");
    }

    private void Start()
    {
        // Subscribe to scene change events
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // Unsubscribe and save data
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (autoSave && Instance == this)
            SaveGameData();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (enableDebugLogs)
            Debug.Log($"Scene loaded: {scene.name} - Data available for access");
    }

    // Main method to set track selection data (called from TrackSelectionManager)
    public void SetSessionData(TrackDataSO track, WeatherConditionSO weather, TimeOfDaySettingsSO time)
    {
        selectedTrack = track;
        selectedWeather = weather;
        selectedTime = time;
        sessionStartTime = System.DateTime.Now;

        if (autoSave) SaveGameData();

        if (enableDebugLogs)
            Debug.Log($"Session data set - Track: {track?.trackName}, Weather: {weather?.weatherName}, Time: {time?.timeName}");
    }

    // Method to update race results
    public void UpdateRaceResults(float lapTime, int position, bool newRecord = false)
    {
        lastLapTime = lapTime;
        lastRacePosition = position;
        isNewRecord = newRecord;

        // Update progress
        totalRaces++;
        if (lapTime < bestLapTime && lapTime > 0)
            bestLapTime = lapTime;

        // Track completion
        if (selectedTrack != null && !completedTracks.Contains(selectedTrack.trackName))
            completedTracks.Add(selectedTrack.trackName);

        if (autoSave) SaveGameData();

        if (enableDebugLogs)
            Debug.Log($"Race results updated - Lap: {lapTime:F2}s, Position: {position}, Best: {bestLapTime:F2}s");
    }

    // Save data to PlayerPrefs
    public void SaveGameData()
    {
        try
        {
            // Save session data
            PlayerPrefs.SetString("SelectedTrack", selectedTrack?.trackName ?? "");
            PlayerPrefs.SetString("SelectedWeather", selectedWeather?.weatherName ?? "");
            PlayerPrefs.SetString("SelectedTime", selectedTime?.timeName ?? "");

            // Save progress data
            PlayerPrefs.SetInt("TotalRaces", totalRaces);
            PlayerPrefs.SetFloat("BestLapTime", bestLapTime);
            PlayerPrefs.SetString("CompletedTracks", string.Join(",", completedTracks));

            // Save race results
            PlayerPrefs.SetFloat("LastLapTime", lastLapTime);
            PlayerPrefs.SetInt("LastRacePosition", lastRacePosition);

            PlayerPrefs.Save();

            if (enableDebugLogs)
                Debug.Log("Game data saved successfully");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save game data: {e.Message}");
        }
    }

    // Load data from PlayerPrefs
    public void LoadGameData()
    {
        try
        {
            // Load progress data
            totalRaces = PlayerPrefs.GetInt("TotalRaces", 0);
            bestLapTime = PlayerPrefs.GetFloat("BestLapTime", float.MaxValue);

            string completedTracksStr = PlayerPrefs.GetString("CompletedTracks", "");
            if (!string.IsNullOrEmpty(completedTracksStr))
            {
                completedTracks = new List<string>(completedTracksStr.Split(','));
            }

            // Load race results
            lastLapTime = PlayerPrefs.GetFloat("LastLapTime", 0f);
            lastRacePosition = PlayerPrefs.GetInt("LastRacePosition", 0);

            if (enableDebugLogs)
                Debug.Log($"Game data loaded - Total Races: {totalRaces}, Best Time: {bestLapTime:F2}s");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to load game data: {e.Message}");
        }
    }

    // Public access methods for other scripts
    public TrackDataSO GetSelectedTrack() => selectedTrack;
    public WeatherConditionSO GetSelectedWeather() => selectedWeather;
    public TimeOfDaySettingsSO GetSelectedTime() => selectedTime;

    public bool HasTrackData() => selectedTrack != null;
    public bool HasWeatherData() => selectedWeather != null;
    public bool HasTimeData() => selectedTime != null;

    // Reset methods
    public void ResetSessionData()
    {
        selectedTrack = null;
        selectedWeather = null;
        selectedTime = null;
        lastLapTime = 0f;
        lastRacePosition = 0;
        isNewRecord = false;

        if (enableDebugLogs)
            Debug.Log("Session data reset");
    }

    public void ResetAllData()
    {
        ResetSessionData();
        totalRaces = 0;
        bestLapTime = float.MaxValue;
        completedTracks.Clear();

        // Clear PlayerPrefs
        PlayerPrefs.DeleteKey("SelectedTrack");
        PlayerPrefs.DeleteKey("SelectedWeather");
        PlayerPrefs.DeleteKey("SelectedTime");
        PlayerPrefs.DeleteKey("TotalRaces");
        PlayerPrefs.DeleteKey("BestLapTime");
        PlayerPrefs.DeleteKey("CompletedTracks");
        PlayerPrefs.DeleteKey("LastLapTime");
        PlayerPrefs.DeleteKey("LastRacePosition");

        if (enableDebugLogs)
            Debug.Log("All game data reset");
    }

    // Context menu methods for testing
    [ContextMenu("Save Data Now")]
    public void ForceSave() => SaveGameData();

    [ContextMenu("Load Data Now")]
    public void ForceLoad() => LoadGameData();

    [ContextMenu("Reset All Data")]
    public void ForceReset() => ResetAllData();
}
