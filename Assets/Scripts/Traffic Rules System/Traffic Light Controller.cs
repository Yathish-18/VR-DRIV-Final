using UnityEngine;
using System.Collections;

public class TrafficLightController : MonoBehaviour
{
    [Header("Traffic Light ID")]
    [SerializeField] private string trafficLightID = "";

    [Header("Light Objects")]
    public GameObject redLight;
    public GameObject yellowLight;
    public GameObject greenLight;

    [Header("Emission Colors")]
    public Color redEmissionColor = Color.red;
    public Color yellowEmissionColor = Color.yellow;
    public Color greenEmissionColor = Color.green;

    [Header("Emission Intensity")]
    public float onEmissionIntensity = 2f;
    public float offEmissionIntensity = 0f;

    [Header("Timing Settings (Only used if not managed by intersection)")]
    public float greenDuration = 10f;
    public float yellowDuration = 3f;
    public float redDuration = 8f;

    [Header("Control Settings")]
    public bool autoStart = false; // Disabled by default - let IntersectionManager control
    public bool enableManualControl = false; // Disabled by default for managed lights
    [Tooltip("If true, this light is managed by a TrafficLightIntersectionManager")]
    public bool isManagedByIntersection = true;

    // Current state of the traffic light
    public enum LightState
    {
        Red,
        Yellow,
        Green
    }

    public LightState currentState = LightState.Red;
    private bool isRunning = false;
    private Coroutine lightCycleCoroutine;

    // Renderers for the lights
    private Renderer redRenderer;
    private Renderer yellowRenderer;
    private Renderer greenRenderer;

    // Store original materials to avoid creating new instances
    private Material redMaterial;
    private Material yellowMaterial;
    private Material greenMaterial;

    void Start()
    {
        InitializeLights();

        // Only register with TrafficLightManager if not managed by intersection
        if (!isManagedByIntersection)
        {
            RegisterWithManager();
        }

        if (autoStart && !isManagedByIntersection)
        {
            StartTrafficLight();
        }
        else
        {
            SetLightState(LightState.Red);
        }
    }

    void RegisterWithManager()
    {
        // Wait a frame to ensure TrafficLightManager is initialized
        StartCoroutine(DelayedRegistration());
    }

    IEnumerator DelayedRegistration()
    {
        yield return null; // Wait one frame

        if (TrafficLightManager.Instance != null)
        {
            TrafficLightManager.Instance.RegisterTrafficLight(this);
        }
        else
        {
            Debug.LogWarning($"TrafficLightController {gameObject.name}: TrafficLightManager not found in scene!");
        }
    }

    void OnDestroy()
    {
        // Unregister from TrafficLightManager
        if (TrafficLightManager.Instance != null && !isManagedByIntersection)
        {
            TrafficLightManager.Instance.UnregisterTrafficLight(this);
        }
    }

    void InitializeLights()
    {
        // Get renderers for each light
        if (redLight != null)
            redRenderer = redLight.GetComponent<Renderer>();
        if (yellowLight != null)
            yellowRenderer = yellowLight.GetComponent<Renderer>();
        if (greenLight != null)
            greenRenderer = greenLight.GetComponent<Renderer>();

        // Validate that all required components are present
        if (redRenderer == null || yellowRenderer == null || greenRenderer == null)
        {
            Debug.LogError($"Traffic Light {trafficLightID}: Missing light renderers! Make sure all light GameObjects have Renderer components.");
            return;
        }

        // Store material references and ensure they support emission
        redMaterial = redRenderer.material;
        yellowMaterial = yellowRenderer.material;
        greenMaterial = greenRenderer.material;

        // Enable emission keyword for all materials
        EnableEmissionForMaterial(redMaterial);
        EnableEmissionForMaterial(yellowMaterial);
        EnableEmissionForMaterial(greenMaterial);
    }

    void EnableEmissionForMaterial(Material mat)
    {
        if (mat != null && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
        }
    }

    void Update()
    {
        if (enableManualControl && !isManagedByIntersection)
        {
            HandleManualInput();
        }
    }

    void HandleManualInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isRunning)
                StopTrafficLight();
            else
                StartTrafficLight();
        }

        if (!isRunning)
        {
            if (Input.GetKeyDown(KeyCode.R))
                SetLightState(LightState.Red);
            else if (Input.GetKeyDown(KeyCode.Y))
                SetLightState(LightState.Yellow);
            else if (Input.GetKeyDown(KeyCode.G))
                SetLightState(LightState.Green);
        }
    }

    public void StartTrafficLight()
    {
        if (!isRunning && !isManagedByIntersection)
        {
            isRunning = true;
            lightCycleCoroutine = StartCoroutine(TrafficLightCycle());
        }
    }

    public void StopTrafficLight()
    {
        if (isRunning)
        {
            isRunning = false;
            if (lightCycleCoroutine != null)
            {
                StopCoroutine(lightCycleCoroutine);
                lightCycleCoroutine = null;
            }
        }
    }

    IEnumerator TrafficLightCycle()
    {
        while (isRunning)
        {
            // Red Light
            SetLightState(LightState.Red);
            yield return new WaitForSeconds(redDuration);

            if (!isRunning) break;

            // Green Light (skip yellow for now)
            SetLightState(LightState.Green);
            yield return new WaitForSeconds(greenDuration);

            if (!isRunning) break;

            // Yellow Light
            SetLightState(LightState.Yellow);
            yield return new WaitForSeconds(yellowDuration);
        }
    }

    public void SetLightState(LightState newState)
    {
        currentState = newState;

        // Turn off all lights first
        SetLightEmission(redMaterial, redEmissionColor, offEmissionIntensity);
        SetLightEmission(yellowMaterial, yellowEmissionColor, offEmissionIntensity);
        SetLightEmission(greenMaterial, greenEmissionColor, offEmissionIntensity);

        // Turn on the appropriate light
        switch (currentState)
        {
            case LightState.Red:
                SetLightEmission(redMaterial, redEmissionColor, onEmissionIntensity);
                OnRedLight();
                break;
            case LightState.Yellow:
                SetLightEmission(yellowMaterial, yellowEmissionColor, onEmissionIntensity);
                OnYellowLight();
                break;
            case LightState.Green:
                SetLightEmission(greenMaterial, greenEmissionColor, onEmissionIntensity);
                OnGreenLight();
                break;
        }
    }

    void SetLightEmission(Material material, Color emissionColor, float intensity)
    {
        if (material != null && material.HasProperty("_EmissionColor"))
        {
            if (intensity > 0)
            {
                material.EnableKeyword("_EMISSION");
                Color finalEmissionColor = emissionColor * Mathf.LinearToGammaSpace(intensity);
                material.SetColor("_EmissionColor", finalEmissionColor);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    protected virtual void OnRedLight()
    {
        // Override in child classes or use events
    }

    protected virtual void OnYellowLight()
    {
        // Override in child classes or use events  
    }

    protected virtual void OnGreenLight()
    {
        // Override in child classes or use events
    }

    // ID Management
    public string GetTrafficLightID() => trafficLightID;
    public void SetTrafficLightID(string id)
    {
        trafficLightID = id;
        gameObject.name = $"TrafficLight_{id}"; // Update GameObject name for easy identification
    }

    // Public methods for external control
    public void SetRedLight() => SetLightState(LightState.Red);
    public void SetYellowLight() => SetLightState(LightState.Yellow);
    public void SetGreenLight() => SetLightState(LightState.Green);

    public bool IsRed() => currentState == LightState.Red;
    public bool IsYellow() => currentState == LightState.Yellow;
    public bool IsGreen() => currentState == LightState.Green;

    public void SetTimings(float red, float yellow, float green)
    {
        redDuration = red;
        yellowDuration = yellow;
        greenDuration = green;
        Debug.Log($"Traffic Light {trafficLightID}: Timings updated - Red: {red}s, Yellow: {yellow}s, Green: {green}s");
    }

    public void SetEmissionIntensity(float onIntensity, float offIntensity = 0f)
    {
        onEmissionIntensity = onIntensity;
        offEmissionIntensity = offIntensity;
        SetLightState(currentState);
    }

    public void SetEmissionColors(Color red, Color yellow, Color green)
    {
        redEmissionColor = red;
        yellowEmissionColor = yellow;
        greenEmissionColor = green;
        SetLightState(currentState);
    }

    void OnDisable()
    {
        StopTrafficLight();
    }
}