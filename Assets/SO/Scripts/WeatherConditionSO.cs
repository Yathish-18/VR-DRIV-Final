using UnityEngine;

[CreateAssetMenu(fileName = "New Weather", menuName = "Racing Game/Weather Condition", order = 2)]
public class WeatherConditionSO : ScriptableObject
{
    [Header("Weather Settings")]
    public string weatherName;
    public Sprite weatherIcon;
    public Color skyboxTint = Color.white;
    public float fogDensity = 0.01f;

    [Header("Additional Weather Properties")]
    public float windStrength = 0f;
    public bool enableRain = false;
    public float visibility = 1f;
}
