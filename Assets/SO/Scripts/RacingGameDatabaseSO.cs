using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Racing Game Database", menuName = "Racing Game/Game Database", order = 0)]
public class RacingGameDatabaseSO : ScriptableObject
{
    [Header("Track Data")]
    [SerializeField] private List<TrackDataSO> availableTracks = new List<TrackDataSO>();

    [Header("Weather Data")]
    [SerializeField] private List<WeatherConditionSO> weatherConditions = new List<WeatherConditionSO>();

    [Header("Time Settings")]
    [SerializeField] private List<TimeOfDaySettingsSO> timeSettings = new List<TimeOfDaySettingsSO>();

    // Public accessors
    public List<TrackDataSO> AvailableTracks => availableTracks;
    public List<WeatherConditionSO> WeatherConditions => weatherConditions;
    public List<TimeOfDaySettingsSO> TimeSettings => timeSettings;

    // Validation methods
    public TrackDataSO GetTrack(int index)
    {
        if (index >= 0 && index < availableTracks.Count)
            return availableTracks[index];
        return null;
    }

    public WeatherConditionSO GetWeather(int index)
    {
        if (index >= 0 && index < weatherConditions.Count)
            return weatherConditions[index];
        return null;
    }

    public TimeOfDaySettingsSO GetTimeOfDay(int index)
    {
        if (index >= 0 && index < timeSettings.Count)
            return timeSettings[index];
        return null;
    }

    // Additional utility methods
    public int GetTrackCount() => availableTracks.Count;
    public int GetWeatherCount() => weatherConditions.Count;
    public int GetTimeSettingsCount() => timeSettings.Count;

    // Validation methods
    public bool IsValidTrackIndex(int index) => index >= 0 && index < availableTracks.Count;
    public bool IsValidWeatherIndex(int index) => index >= 0 && index < weatherConditions.Count;
    public bool IsValidTimeIndex(int index) => index >= 0 && index < timeSettings.Count;
}
