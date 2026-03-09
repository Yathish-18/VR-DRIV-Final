using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    private Light mainDirectionalLight;

    public void UpdateEnvironment(WeatherConditionSO weather, TimeOfDaySettingsSO time)
    {
        if (mainDirectionalLight == null)
            FindMainLight();

        // Order matters: time runs first, weather runs second and ALWAYS wins the skybox
        ApplyTimeOfDay(time);
        ApplyWeather(weather);
    }

    private void FindMainLight()
    {
        if (RenderSettings.sun != null)
        {
            mainDirectionalLight = RenderSettings.sun;
            return;
        }

        Light[] allLights = FindObjectsOfType<Light>();
        foreach (Light l in allLights)
        {
            if (l.type == LightType.Directional)
            {
                mainDirectionalLight = l;
                return;
            }
        }

        Debug.LogWarning("[EnvironmentManager] No Directional Light found in scene.");
    }

    private void ApplyWeather(WeatherConditionSO weather)
    {
        if (weather == null)
        {
            Debug.LogWarning("[EnvironmentManager] ApplyWeather called with null weather.");
            return;
        }

        // Fog
        RenderSettings.fog = true;
        RenderSettings.fogDensity = weather.fogDensity;
        RenderSettings.fogColor = weather.skyboxTint;

        // Skybox — weather always wins, called after ApplyTimeOfDay so it overwrites it
        if (weather.skyboxMaterial != null)
        {
            RenderSettings.skybox = weather.skyboxMaterial;

            if (weather.skyboxMaterial.HasProperty("_Tint"))
                weather.skyboxMaterial.SetColor("_Tint", weather.skyboxTint);
            else if (weather.skyboxMaterial.HasProperty("_SkyTint"))
                weather.skyboxMaterial.SetColor("_SkyTint", weather.skyboxTint);

            DynamicGI.UpdateEnvironment();
            Debug.Log($"[EnvironmentManager] ✓ Skybox → '{weather.skyboxMaterial.name}' ({weather.weatherName})");
        }
        else
        {
            Debug.LogWarning($"[EnvironmentManager] Weather '{weather.weatherName}' has no skybox material assigned!");
        }
    }

    private void ApplyTimeOfDay(TimeOfDaySettingsSO time)
    {
        if (time == null) return;

        // Directional light
        if (mainDirectionalLight != null)
        {
            mainDirectionalLight.color = time.lightColor;
            mainDirectionalLight.intensity = time.lightIntensity;
            mainDirectionalLight.shadows = time.enableShadows ? LightShadows.Soft : LightShadows.None;
        }

        // Ambient
        RenderSettings.ambientIntensity = time.ambientIntensity;

        // Time skybox is a fallback only — ApplyWeather will overwrite this if weather has a material
        if (time.skyboxMaterial != null)
        {
            RenderSettings.skybox = time.skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }
    }
}