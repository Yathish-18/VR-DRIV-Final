using UnityEngine;
using UnityEngine.UI;

public class RainController : MonoBehaviour
{
    public ParticleSystem rainParticleSystem;
    public Slider intensitySlider;

    private ParticleSystem.EmissionModule emissionModule;

    void Start()
    {
        emissionModule = rainParticleSystem.emission;
        intensitySlider.onValueChanged.AddListener(UpdateRainIntensity);
        UpdateRainIntensity(intensitySlider.value); // Set initial value
    }

    void UpdateRainIntensity(float value)
    {
        emissionModule.rateOverTime = value;
    }
}
