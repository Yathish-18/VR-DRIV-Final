using UnityEngine;

[CreateAssetMenu(fileName = "New Time Setting", menuName = "Racing Game/Time Of Day", order = 3)]
public class TimeOfDaySettingsSO : ScriptableObject
{
    [Header("Time Settings")]
    public string timeName;
    public Sprite timeIcon;
    public Color lightColor = Color.white;
    public float lightIntensity = 1.0f;
    public Material skyboxMaterial;

    [Header("Additional Time Properties")]
    public float ambientIntensity = 0.5f;
    public bool enableShadows = true;
}
