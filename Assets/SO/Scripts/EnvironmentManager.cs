using UnityEngine;
using System.Linq; // Needed for easier searching

public class EnvironmentManager : MonoBehaviour
{
    [Header("Scene References")]
    // We remove the [SerializeField] requirement because we will find it via code now
    private Light mainDirectionalLight;

    public void UpdateEnvironment(WeatherConditionSO weather, TimeOfDaySettingsSO time)
    {
        // 1. First, try to find the light automatically if we don't have it
        if (mainDirectionalLight == null)
        {
            FindMainLight();
        }

        ApplyWeather(weather);
        ApplyTimeOfDay(time);
    }

    private void FindMainLight()
    {
        // Option A: Check Unity's built-in "Sun" setting (Best if set up in Lighting window)
        if (RenderSettings.sun != null)
        {
            mainDirectionalLight = RenderSettings.sun;
            return;
        }

        // Option B: Find the first Directional Light in the scene (Most reliable fallback)
        Light[] allLights = FindObjectsOfType<Light>();
        foreach (Light l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                mainDirectionalLight = l;
                return;
            }
        }

        Debug.LogWarning("EnvironmentManager: Could not find a Directional Light in this scene!");
    }

    private void ApplyWeather(WeatherConditionSO weather)
    {
        if (weather == null) return;

        // Apply Fog settings
        RenderSettings.fog = true;
        RenderSettings.fogDensity = weather.fogDensity;
        // Optional: Change Skybox tint if your shader supports it
        // RenderSettings.skybox.SetColor("_Tint", weather.skyboxTint); 

        // Debug.Log($"Applied Weather: {weather.weatherName}");
    }

    private void ApplyTimeOfDay(TimeOfDaySettingsSO time)
    {
        if (time == null) return;

        // Apply Light settings (Only if we found the light)
        if (mainDirectionalLight != null)
        {
            mainDirectionalLight.color = time.lightColor;
            mainDirectionalLight.intensity = time.lightIntensity;
            mainDirectionalLight.shadows = time.enableShadows ? LightShadows.Soft : LightShadows.None;
        }

        // Apply Ambient settings (Global settings, doesn't need light reference)
        RenderSettings.ambientIntensity = time.ambientIntensity;

        // Apply Skybox
        if (time.skyboxMaterial != null)
        {
            RenderSettings.skybox = time.skyboxMaterial;
        }

        // Force Unity to update the lighting immediately
        DynamicGI.UpdateEnvironment();

        // Debug.Log($"Applied Time: {time.timeName}");
    }
}