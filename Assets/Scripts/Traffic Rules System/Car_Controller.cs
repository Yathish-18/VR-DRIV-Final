using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;

public class Car_Controller : MonoBehaviour
{
    private enum LightType { None, Headlights, Brake, Reverse }

    // FIXED: Enhanced Dashboard Optimization Events with null safety
    public static System.Action OnSpeedChanged;
    public static System.Action OnGearChanged;
    public static System.Action OnRPMChanged;

    private float lastReportedSpeed = 0f;
    private int lastReportedGear = 0;
    private float lastReportedRPM = 0f;
    private float speedChangeThreshold = 0.1f; // FIXED: Much lower threshold for frequent updates

    // Optimized Telemetry Data Structure
    public float SteerInput => steerInput;

    [Header("🎮 INPUT SETTINGS")]
    [Tooltip("Use G29 wheel input instead of keyboard")]
    public bool useG29Input = false;
    [Tooltip("Key to toggle between G29 and keyboard input")]
    public KeyCode toggleInputKey = KeyCode.F1;

    [Header("🎛️ LOGITECH SDK SETTINGS")]
    [Tooltip("Automatically initialize Logitech SDK on start")]
    public bool autoInitializeLogitech = true;
    [Tooltip("Index of the Logitech wheel (usually 0)")]
    public int logitechWheelIndex = 0;

    [Header("🛞 WHEEL COLLIDERS")]
    [SerializeField] public List<WheelCollider> Front_Wheels = new List<WheelCollider>();
    [SerializeField] public List<WheelCollider> Back_Wheels = new List<WheelCollider>();

    [Header("🛞 WHEEL TRANSFORMS")]
    [SerializeField] private List<Transform> Front_Wheel_Transforms = new List<Transform>();
    [SerializeField] private List<Transform> Back_Wheel_Transforms = new List<Transform>();
    [SerializeField] private List<Transform> Front_Wheel_Rotation = new List<Transform>();
    [SerializeField] private List<Transform> Back_Wheel_Rotation = new List<Transform>();

    [Header("🔁 TURN SIGNAL SETTINGS")]
    public KeyCode leftTurnSignalKey = KeyCode.LeftArrow;
    public KeyCode rightTurnSignalKey = KeyCode.RightArrow;

    [Header("⚙️ CAR SPECIFICATIONS")]
    [Tooltip("Car name for identification")]
    public string carName = "Maruti Alto K10";

    [Header("🔥 REALISTIC ENGINE SPECIFICATIONS")]
    [Tooltip("Engine displacement in liters")]
    [Range(0.8f, 6.0f)]
    public float engineDisplacement = 1.0f;
    [Tooltip("Peak engine torque in Nm")]
    [Range(50, 800)]
    public float peakTorqueValue = 90f;
    [Tooltip("RPM where peak torque occurs")]
    [Range(1500, 8000)]
    public float peakTorqueRPM = 3200f;
    [Tooltip("Maximum engine RPM / Redline")]
    [Range(5000, 10000)]
    public float maxEngineRPM = 6000f;
    [Tooltip("Engine idle RPM")]
    [Range(600, 1200)]
    public float idleRPM = 850f;
    [Tooltip("Engine inertia (affects rev response)")]
    [Range(0.05f, 0.4f)]
    public float engineInertia = 0.15f;

    [Header("⚙️ TRANSMISSION MODES")]
    [Tooltip("FIXED - Enable paddle shifter mode (no clutch required)")]
    public bool usePaddleShifters = false;
    [Tooltip("Use manual transmission with clutch (set false for paddle shifters)")]
    public bool useManualTransmission = true;
    [Tooltip("Enable direct gear selection (1,2,3,4,5,R,N keys)")]
    public bool enableDirectGearSelection = true;
    [Tooltip("Number of forward gears")]
    [Range(4, 8)]
    public int maxGear = 5;

    // UPDATED GEAR SPEED MAPPING SYSTEM WITH AUTO-CALCULATED RATIOS
    [Header("🏁 GEAR SPEED MAPPING (ENFORCED LIMITS)")]
    [Tooltip("Target speeds for each gear in KM/H (Gear 1-5) - Ratios auto-calculated")]
    [SerializeField] private float[] gearTargetSpeeds = { 0f, 25f, 50f, 75f, 100f, 130f }; // Index 0 unused, 1-5 for gears
    [Tooltip("Allow small speed overshoot before hard limiting")]
    [Range(0f, 5f)]
    public float speedOvershootTolerance = 2f;
    [Tooltip("How quickly to reduce power when approaching speed limit")]
    [Range(0.5f, 3.0f)]
    public float speedLimitResponse = 1.5f;

    // AUTO-CALCULATED GEAR RATIOS (READ-ONLY)
    [Header("⚙️ AUTO-CALCULATED TRANSMISSION")]
    [Tooltip("Gear ratios (AUTO-CALCULATED from target speeds - READ ONLY)")]
    [SerializeField] private float[] gearRatios = { 0f, 3.545f, 1.904f, 1.280f, 0.966f, 0.756f }; // Will be auto-calculated
    [Tooltip("Reverse gear ratio (negative)")]
    [Range(-4.5f, -2.5f)]
    public float reverseGearRatio = -3.2f;
    [Tooltip("Final drive ratio")]
    [Range(2.0f, 6.0f)]
    public float finalDriveRatio = 4.1f;
    [Tooltip("Transmission efficiency")]
    [Range(0.85f, 0.98f)]
    public float transmissionEfficiency = 0.89f;

    [Header("🔧 VEHICLE PHYSICS")]
    [Tooltip("Vehicle mass in kg")]
    [Range(600, 2500)]
    public float vehicleMass = 890f;
    [Range(15f, 45f)]
    public float Max_Steer_Angle = 35f;
    [Range(1000f, 8000f)]
    public float BrakeForce = 1900f;

    [Header("🛞 PROFESSIONAL WHEEL SPECIFICATIONS")]
    [Tooltip("Tire radius for realistic speeds")]
    [Range(0.2f, 0.4f)]
    public float tireRadius = 0.289f;
    [Tooltip("Wheel mass in kg")]
    [Range(8f, 25f)]
    public float wheelMass = 9f;
    [Tooltip("Rolling resistance coefficient")]
    [Range(0.008f, 0.025f)]
    public float rollingResistance = 0.018f;

    [Header("🔩 SUSPENSION SETTINGS")]
    [Range(0.1f, 0.4f)]
    public float suspensionDistance = 0.18f;
    [Range(10000f, 50000f)]
    public float suspensionSpring = 28000f;
    [Range(1000f, 5000f)]
    public float suspensionDamper = 2200f;
    [Range(0.3f, 0.7f)]
    public float suspensionTargetPosition = 0.48f;

    [Header("🌪️ AERODYNAMICS")]
    [Tooltip("Drag coefficient (Cd)")]
    [Range(0.25f, 0.6f)]
    public float dragCoefficient = 0.35f;
    [Tooltip("Frontal area in square meters")]
    [Range(1.5f, 3.0f)]
    public float frontalArea = 2.15f;
    [Range(0f, 200f)]
    public float downforce = 3f;

    [Header("🛡️ ELECTRONIC SYSTEMS")]
    public bool enableTractionControl = true;
    [Range(0.3f, 2.0f)]
    public float tractionControlThreshold = 0.75f;
    public bool enableABS = true;
    [Range(0.3f, 1.0f)]
    public float ABSThreshold = 0.55f;

    [Header("🎮 CONTROL SETTINGS")]
    public KeyCode gearUpKey = KeyCode.Q;
    public KeyCode gearDownKey = KeyCode.E;
    public KeyCode clutchKey = KeyCode.LeftShift;
    public KeyCode reverseKey = KeyCode.R;
    public KeyCode neutralKey = KeyCode.N;

    [Header("🔢 DIRECT GEAR SELECTION KEYS")]
    public KeyCode gear1Key = KeyCode.Alpha1;
    public KeyCode gear2Key = KeyCode.Alpha2;
    public KeyCode gear3Key = KeyCode.Alpha3;
    public KeyCode gear4Key = KeyCode.Alpha4;
    public KeyCode gear5Key = KeyCode.Alpha5;

    [Header("🚗 HANDBRAKE SETTINGS")]
    public KeyCode handbrakeKey = KeyCode.Space;
    [HideInInspector] public bool handbrakeEngaged = false;
    [Range(2000f, 10000f)]
    public float handbrakeForce = 3500f;

    [Header("🔥 BOOST SETTINGS")]
    public bool enable_boost;
    public float Boost_Cooldown = 15f;
    public float Boost_Amount = 8f;
    public KeyCode Boost_KeyCode;
    public bool Enable_Boost_particles;
    public ParticleSystem[] Boost_particles;

    [Header("🔑 CAR STATE SETTINGS")]
    public bool Use_Car_States;
    public bool Car_Started;
    public KeyCode Car_Start_Key;
    public KeyCode Car_Off_Key;

    [Header("🔊 AUDIO SETTINGS")]
    public bool Enable_Audio;
    public bool Enable_Engine_Audio;
    public AudioSource Engine_Sound;
    [Range(0.3f, 1.0f)]
    public float Minimum_Pitch_Value = 0.35f;
    [Range(1.2f, 2.5f)]
    public float Maximum_Pitch_Value = 1.5f;
    public bool Enable_Horn;
    public AudioSource Horn_Source;
    public KeyCode Car_Horn_Key;

    [Header("💥 CRASH SYSTEM")]
    public bool Enable_Crash_Noise;
    public string[] Crash_Object_Tags;
    public AudioSource Crash_Sound;

    [Header("🌪️ DRIFT SETTINGS")]
    public bool Set_Drift_Settings_Automatically = true;
    [Range(0.1f, 1.0f)]
    public float Forward_Extremium_Value_When_Drifting = 0.35f;
    [Range(0.1f, 1.0f)]
    public float Sideways_Extremium_Value_When_Drifting = 0.25f;

    [Header("💡 LIGHTING SYSTEM")]
    public bool Enable_Headlights_Lights;
    public bool Enable_Brakelights_Lights;
    public bool Enable_Reverselights_Lights;
    public KeyCode Headlights_Key;
    public Light[] HeadLights;
    public Light[] BrakeLights;
    public Light[] ReverseLights;
    public bool Enable_Headlights_MeshRenderers;
    public bool Enable_Brakelights_MeshRenderers;
    public bool Enable_Reverselights_MeshRenderers;
    public MeshRenderer[] HeadLights_MeshRenderers;
    public MeshRenderer[] BrakeLights_MeshRenderers;
    public MeshRenderer[] ReverseLights_MeshRenderers;
    public bool Enable_Headlights_Materials;
    public bool Enable_Brakelights_Materials;
    public bool Enable_Reverselights_Materials;
    public Material HeadLights_Off_Material;
    public Material BrakeLights_Off_Material;
    public Material ReverseLights_Off_Material;
    public Material HeadLights_On_Material;
    public Material BrakeLights_On_Material;
    public Material ReverseLights_On_Material;
    public GameObject[] Headlight_Objects;
    public GameObject[] BrakeLight_Objects;
    public GameObject[] Reverse_Light_Objects;
    public bool Enable_Headlights_Colors;
    public bool Enable_Brakelights_Colors;
    public bool Enable_Reverselights_Colors;
    public Color HeadLights_Off_Color;
    public Color BrakeLights_Off_Color;
    public Color ReverseLights_Off_Color;
    public Color HeadLights_On_Color;
    public Color BrakeLights_On_Color;
    public Color ReverseLights_On_Color;
    public Material HeadLight_Material;
    public Material BrakeLight_Material;
    public Material ReverseLight_Material;

    [Header("💨 PARTICLE EFFECTS")]
    public bool Use_Particle_Systems;
    public ParticleSystem[] Car_Smoke_From_Silencer;

    [Header("🎮 SCENE SETTINGS")]
    public bool Use_Scene_Settings;
    public KeyCode Scene_Reset_Key = KeyCode.T;

    [Header("⚖️ CENTER OF MASS")]
    public bool useManualCenterOfMass = true;
    public Transform Center_of_Mass;
    public Rigidbody Car_Rigidbody;

    [Header("📊 DEBUG VALUES (READ-ONLY)")]
    [SerializeField] private float Car_Speed_KPH;
    [SerializeField] private float Car_Speed_MPH;
    [SerializeField] private float RPM;
    [SerializeField] private int currentGear = 0;
    [SerializeField] private float wheelSlip;
    [SerializeField] private float engineRPM;
    [SerializeField] private float clutchPosition;
    [SerializeField] private bool isStalling;
    [SerializeField] private bool HeadLights_On;
    [SerializeField] private int Car_Speed_In_KPH;
    [SerializeField] private int Car_Speed_In_MPH;
    [SerializeField] private float currentEngineTorque;
    [SerializeField] private float wheelTorque;
    [SerializeField] private float driveForce;
    [SerializeField] private float dragForce;
    [SerializeField] private float rollingResistanceForce;

    // NEW: Speed limiting debug values
    [Header("🏁 SPEED LIMITING DEBUG (READ-ONLY)")]
    [SerializeField] private float currentGearMaxSpeed;
    [SerializeField] private float speedLimitingFactor;
    [SerializeField] private bool isSpeedLimited;

    // FIXED: Debug logging
    [Header("🐛 DEBUG SETTINGS")]
    [SerializeField] private bool enableDebugLogs = false;

    // Public accessors for external scripts
    public List<WheelCollider> FrontWheels => Front_Wheels;
    public List<WheelCollider> BackWheels => Back_Wheels;
    public List<Transform> FrontWheelTransforms => Front_Wheel_Transforms;
    public List<Transform> BackWheelTransforms => Back_Wheel_Transforms;

    // PROFESSIONAL ENGINE & TRANSMISSION VARIABLES
    private bool leftTurnSignalActive = false;
    private bool rightTurnSignalActive = false;
    private float turnSignalTimer = 0f;
    private Rigidbody rb;
    private float Brakes = 0f;
    private float Next_Boost_Time;
    public float steerInput;
    private float motorInput;
    private bool isGrounded;
    private float[] wheelSlipValues;
    private float pitch;
    [HideInInspector] public float currSpeed;

    // REALISTIC ENGINE PHYSICS
    private float engineAngularVelocity;
    private float wheelAngularVelocity;
    private float clutchEngagement = 1f;
    private bool clutchPressed;
    private float gearChangeTimer;
    private bool canChangeGear = true;

    // PROFESSIONAL TRANSMISSION CALCULATIONS
    private float wheelCircumference;
    private bool isRedlining = false;
    private float autoGearChangeTimer = 0f;
    private const float AIR_DENSITY = 1.225f;

    // FIXED - Button state tracking for proper edge detection
    private bool previousGearUpButton = false;
    private bool previousGearDownButton = false;
    private bool previousReverseButton = false;
    private bool previousNeutralButton = false;
    private bool[] previousDirectGearButtons = new bool[6]; // 0-5 for direct gear selection

    // FIXED - Add Logitech button tracking for all buttons
    private bool[] previousLogitechButtons = new bool[32]; // Support up to 32 buttons

    // Logitech SDK variables
    private Logitech.LogitechGSDK.LogiControllerPropertiesData logitechProperties;
    private bool logitechInitialized = false;
    private float g29BrakeInput = 0f;

    // REALISTIC TORQUE CURVE
    private AnimationCurve realisticTorqueCurve;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = vehicleMass;

        InitializeProfessionalEngine();
        CalculateGearRatiosFromTargetSpeeds(); // NEW: Auto-calculate gear ratios
        SetupRealisticTransmission();

        if (autoInitializeLogitech) InitializeLogitechSDK();

        if (useManualCenterOfMass && Center_of_Mass != null)
            rb.centerOfMass = Center_of_Mass.localPosition;
        else
            rb.centerOfMass = new Vector3(0.0f, -0.28f, 0.08f);

        SetupProfessionalWheels();

        int totalWheels = Front_Wheels.Count + Back_Wheels.Count;
        wheelSlipValues = new float[totalWheels];

        // PROFESSIONAL INITIALIZATION
        currentGear = 0; // Start in neutral
        engineRPM = idleRPM;
        engineAngularVelocity = RPMToRadPerSec(idleRPM);
        clutchPosition = 0f;
        clutchEngagement = 1f;
        clutchPressed = false;

        wheelCircumference = 2f * Mathf.PI * tireRadius;
        CreateRealisticTorqueCurve();

        if (Use_Particle_Systems && Car_Smoke_From_Silencer != null)
        {
            foreach (ParticleSystem P in Car_Smoke_From_Silencer)
                if (P != null) P.Play();
        }

        SetupLighting();
        SetupAudio();

        string transmissionMode = usePaddleShifters ? "Paddle Shifters" : "Manual";
        Debug.Log($"🏁 PROFESSIONAL {carName} initialized with {transmissionMode}");
        PrintProfessionalSpecs();

        // FIXED: Initialize dashboard events properly
        StartCoroutine(InitializeDashboardEvents());
    }

    // NEW: Calculate gear ratios automatically from target speeds
    void CalculateGearRatiosFromTargetSpeeds()
    {
        Debug.Log("🔧 AUTO-CALCULATING GEAR RATIOS FROM TARGET SPEEDS...");

        // Ensure gearRatios array is properly sized
        if (gearRatios == null || gearRatios.Length < maxGear + 1)
        {
            gearRatios = new float[maxGear + 1];
        }

        gearRatios[0] = 0f; // Neutral

        for (int gear = 1; gear <= maxGear && gear < gearTargetSpeeds.Length; gear++)
        {
            float targetSpeedKmh = gearTargetSpeeds[gear];
            float targetSpeedMs = targetSpeedKmh / 3.6f; // Convert to m/s

            // Calculate gear ratio so that at max RPM, we reach exactly the target speed
            // Formula: gearRatio = (maxEngineRPM * wheelCircumference) / (60 * targetSpeedMs * finalDriveRatio)
            float gearRatio = (maxEngineRPM * wheelCircumference) / (60f * targetSpeedMs * finalDriveRatio);
            gearRatios[gear] = gearRatio;

            Debug.Log($"Gear {gear}: Target {targetSpeedKmh}km/h -> Calculated Ratio: {gearRatio:F3}");
        }

        // Verification
        Debug.Log("🔍 VERIFICATION - Max speeds at redline:");
        for (int gear = 1; gear <= maxGear && gear < gearRatios.Length; gear++)
        {
            float wheelRpm = maxEngineRPM / (gearRatios[gear] * finalDriveRatio);
            float maxSpeedMs = (wheelRpm * wheelCircumference) / 60f;
            float maxSpeedKmh = maxSpeedMs * 3.6f;
            Debug.Log($"Gear {gear}: {maxSpeedKmh:F1}km/h (Target: {gearTargetSpeeds[gear]}km/h)");
        }
    }

    // FIXED: Coroutine to ensure dashboard events are properly initialized
    IEnumerator InitializeDashboardEvents()
    {
        yield return new WaitForSeconds(0.5f); // Wait for other systems to initialize

        // Force initial dashboard update
        TriggerDashboardUpdate();

        if (enableDebugLogs)
            Debug.Log("✅ Dashboard events initialized and first update triggered");
    }

    // FIXED: Method to trigger dashboard updates
    void TriggerDashboardUpdate()
    {
        try
        {
            OnSpeedChanged?.Invoke();
            OnGearChanged?.Invoke();
            OnRPMChanged?.Invoke();

            if (enableDebugLogs)
                Debug.Log($"Dashboard update triggered - Speed: {Car_Speed_KPH:F1}, RPM: {engineRPM:F0}, Gear: {currentGear}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Dashboard event error: {e.Message}");
        }
    }

    // FIXED: Enhanced significant change detection with much better thresholds
    void CheckForSignificantChanges()
    {
        // FIXED: Much lower thresholds for frequent updates
        bool speedChanged = Mathf.Abs(Car_Speed_KPH - lastReportedSpeed) > speedChangeThreshold;
        bool gearChanged = currentGear != lastReportedGear;
        bool rpmChanged = Mathf.Abs(engineRPM - lastReportedRPM) > 25f; // Lower RPM threshold

        if (speedChanged)
        {
            OnSpeedChanged?.Invoke();
            lastReportedSpeed = Car_Speed_KPH;

            if (enableDebugLogs)
                Debug.Log($"Speed change event: {Car_Speed_KPH:F1} km/h");
        }

        if (gearChanged)
        {
            OnGearChanged?.Invoke();
            lastReportedGear = currentGear;

            if (enableDebugLogs)
                Debug.Log($"Gear change event: {GetGearName(currentGear)}");
        }

        if (rpmChanged)
        {
            OnRPMChanged?.Invoke();
            lastReportedRPM = engineRPM;

            if (enableDebugLogs)
                Debug.Log($"RPM change event: {engineRPM:F0}");
        }
    }

    void InitializeProfessionalEngine()
    {
        if (peakTorqueRPM >= maxEngineRPM)
        {
            Debug.LogWarning("⚠️ Peak torque RPM should be less than max RPM. Adjusting...");
            peakTorqueRPM = maxEngineRPM * 0.6f;
        }
    }

    void SetupRealisticTransmission()
    {
        // Gear ratios are now auto-calculated, just validate
        for (int i = 2; i <= maxGear && i < gearRatios.Length; i++)
        {
            if (gearRatios[i] >= gearRatios[i - 1])
            {
                Debug.LogWarning($"⚠️ Gear {i} ratio should be lower than gear {i - 1}. The auto-calculation should prevent this.");
            }
        }
    }

    void CreateRealisticTorqueCurve()
    {
        realisticTorqueCurve = new AnimationCurve();

        float peakRPMNormalized = (peakTorqueRPM - idleRPM) / (maxEngineRPM - idleRPM);

        realisticTorqueCurve.AddKey(0.0f, 0.4f);
        realisticTorqueCurve.AddKey(0.15f, 0.65f);
        realisticTorqueCurve.AddKey(0.3f, 0.85f);
        realisticTorqueCurve.AddKey(peakRPMNormalized, 1.0f);
        realisticTorqueCurve.AddKey(peakRPMNormalized + 0.15f, 0.95f);
        realisticTorqueCurve.AddKey(0.8f, 0.75f);
        realisticTorqueCurve.AddKey(1.0f, 0.5f);

        for (int i = 0; i < realisticTorqueCurve.length; i++)
        {
            realisticTorqueCurve.SmoothTangents(i, 0.3f);
        }
    }

    void PrintProfessionalSpecs()
    {
        Debug.Log("📋 AUTO-CALCULATED SPECIFICATIONS:");
        Debug.Log($" Engine: {engineDisplacement}L, {peakTorqueValue}Nm @ {peakTorqueRPM}RPM");
        Debug.Log($" Mass: {vehicleMass}kg, Tire: {tireRadius * 2000:F0}mm diameter");
        Debug.Log($" Transmission: {maxGear}-speed, Final drive: {finalDriveRatio:F2}:1");

        Debug.Log("🏁 SPEED-ENFORCED GEAR RANGES:");
        for (int gear = 1; gear <= maxGear && gear < gearTargetSpeeds.Length; gear++)
        {
            Debug.Log($" {GetGearName(gear)}: 0-{gearTargetSpeeds[gear]:F0} KPH (Hard limit enforced)");
        }
    }

    void SetupProfessionalWheels()
    {
        var allWheels = Front_Wheels.Concat(Back_Wheels);
        foreach (WheelCollider wheel in allWheels)
        {
            if (wheel == null) continue;

            wheel.mass = wheelMass;
            wheel.radius = tireRadius;
            wheel.wheelDampingRate = 0.2f;
            wheel.suspensionDistance = suspensionDistance;

            JointSpring suspensionSpring = wheel.suspensionSpring;
            suspensionSpring.spring = this.suspensionSpring;
            suspensionSpring.damper = suspensionDamper;
            suspensionSpring.targetPosition = suspensionTargetPosition;
            wheel.suspensionSpring = suspensionSpring;

            SetRealisticWheelFriction(wheel);
        }
    }

    void SetRealisticWheelFriction(WheelCollider wheel)
    {
        WheelFrictionCurve forwardFriction = wheel.forwardFriction;
        WheelFrictionCurve sidewaysFriction = wheel.sidewaysFriction;

        forwardFriction.extremumSlip = 0.28f;
        forwardFriction.extremumValue = 1.15f;
        forwardFriction.asymptoteSlip = 0.55f;
        forwardFriction.asymptoteValue = 0.85f;
        forwardFriction.stiffness = 1.0f;

        sidewaysFriction.extremumSlip = 0.23f;
        sidewaysFriction.extremumValue = 1.05f;
        sidewaysFriction.asymptoteSlip = 0.48f;
        sidewaysFriction.asymptoteValue = 0.8f;
        sidewaysFriction.stiffness = 1.0f;

        wheel.forwardFriction = forwardFriction;
        wheel.sidewaysFriction = sidewaysFriction;
    }

    void SetupLighting()
    {
        if (HeadLights_On)
            SetLightState(LightType.Headlights, true);
        else
            SetLightState(LightType.Headlights, false);

        SetLightState(LightType.Reverse, false);
        SetLightState(LightType.Brake, false);
    }

    void SetupAudio()
    {
        if (!Enable_Horn && Horn_Source != null) Horn_Source.gameObject.SetActive(false);
        if (!Enable_Engine_Audio && Engine_Sound != null) Engine_Sound.gameObject.SetActive(false);

        if (!Enable_Audio && (Engine_Sound != null || Horn_Source != null))
        {
            if (Horn_Source != null) Horn_Source.gameObject.SetActive(false);
            if (Engine_Sound != null) Engine_Sound.gameObject.SetActive(false);
        }
    }

    // LOGITECH SDK METHODS (Updated with better button handling)
    void InitializeLogitechSDK()
    {
        try
        {
            string dllName = "LogitechSteeringWheelEnginesWrapper.dll";
            string dllPath;
#if UNITY_EDITOR
            dllPath = Path.Combine(Application.dataPath, "Logitech SDK", dllName);
#else
            dllPath = Path.Combine(Application.dataPath, "Plugins", dllName);
#endif

            if (!File.Exists(dllPath))
            {
                Debug.LogError($"❌ Logitech DLL not found at: {dllPath}");
                logitechInitialized = false;
                return;
            }

            logitechInitialized = Logitech.LogitechGSDK.LogiSteeringInitialize(false);

            if (logitechInitialized)
            {
                Debug.Log("✅ Logitech SDK initialized successfully!");
                logitechProperties = new Logitech.LogitechGSDK.LogiControllerPropertiesData();

                if (Logitech.LogitechGSDK.LogiIsConnected(logitechWheelIndex))
                {
                    System.Text.StringBuilder deviceName = new System.Text.StringBuilder(256);
                    if (Logitech.LogitechGSDK.LogiGetFriendlyProductName(logitechWheelIndex, deviceName, 256))
                        Debug.Log($"🎮 Connected device: {deviceName}");
                }
                else Debug.LogWarning("⚠️ No Logitech wheel detected. Make sure G29 is connected and G HUB is installed.");
            }
            else
            {
                Debug.LogWarning("❌ Failed to initialize Logitech SDK");
                logitechInitialized = false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ SDK initialization failed: {ex.Message}");
            logitechInitialized = false;
        }
    }

    private bool IsLogitechWheelReady()
    {
        if (!logitechInitialized) return false;
        try
        {
            if (!Logitech.LogitechGSDK.LogiIsConnected(logitechWheelIndex)) return false;
            return Logitech.LogitechGSDK.LogiUpdate();
        }
        catch (System.Exception) { return false; }
    }

    private float GetLogitechInput(string inputType)
    {
        if (!IsLogitechWheelReady()) return 0f;

        try
        {
            var state = Logitech.LogitechGSDK.LogiGetStateUnity(logitechWheelIndex);
            float rawValue = 0f;

            switch (inputType)
            {
                case "throttle":
                    rawValue = (float)state.lY;
                    break;
                case "brake":
                    rawValue = (float)state.lRz;
                    break;
                case "steering":
                    return (float)state.lX / 32767f;
                case "clutch":
                    bool foundClutchAxis = false;
                    if (state.rglSlider.Length >= 1)
                    {
                        float testValue0 = (float)state.rglSlider[0];
                        if (Mathf.Abs(testValue0) > 1000f)
                        {
                            rawValue = testValue0;
                            foundClutchAxis = true;
                        }
                    }
                    if (!foundClutchAxis)
                    {
                        rawValue = (float)state.lZ;
                    }
                    break;
            }

            if (inputType == "steering") return Mathf.Clamp(rawValue, -1f, 1f);

            if (inputType == "clutch")
            {
                rawValue = Mathf.Clamp01(rawValue / 32767f);
                rawValue = 1f - rawValue;
                if (rawValue > 0.85f) rawValue = 1f;
                if (rawValue < 0.15f) rawValue = 0f;
            }
            else if (inputType == "throttle" || inputType == "brake")
            {
                rawValue = rawValue / 32767f;
                rawValue = Mathf.Clamp01(1f - rawValue);
                if (rawValue < 0.08f) rawValue = 0f;
                if (rawValue > 0.92f) rawValue = 1f;
            }

            return rawValue;
        }
        catch (System.Exception) { return 0f; }
    }

    // FIXED - Better button detection with edge detection
    private bool GetLogitechButtonPressed(int buttonIndex)
    {
        if (!IsLogitechWheelReady()) return false;
        try
        {
            var state = Logitech.LogitechGSDK.LogiGetStateUnity(logitechWheelIndex);
            return buttonIndex >= 0 && buttonIndex < state.rgbButtons.Length && state.rgbButtons[buttonIndex] == 128;
        }
        catch (System.Exception) { return false; }
    }

    // FIXED - New method for edge detection on Logitech buttons
    private bool GetLogitechButtonDown(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= previousLogitechButtons.Length) return false;
        bool currentState = GetLogitechButtonPressed(buttonIndex);
        bool wasPressed = previousLogitechButtons[buttonIndex];
        return currentState && !wasPressed;
    }

    // FIXED - Update button states at end of frame
    private void UpdateLogitechButtonStates()
    {
        if (!IsLogitechWheelReady()) return;
        for (int i = 0; i < previousLogitechButtons.Length; i++)
        {
            previousLogitechButtons[i] = GetLogitechButtonPressed(i);
        }
    }

    void OnDestroy()
    {
        if (logitechInitialized)
        {
            try
            {
                Logitech.LogitechGSDK.LogiSteeringShutdown();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"❌ Error shutting down Logitech SDK: {e.Message}");
            }
        }
    }

    public bool Is_Flying()
    {
        foreach (WheelCollider wheel in Back_Wheels)
            if (wheel != null && wheel.isGrounded) return false;
        foreach (WheelCollider wheel in Front_Wheels)
            if (wheel != null && wheel.isGrounded) return false;
        return true;
    }

    // MAIN UPDATE METHODS
    public void FixedUpdate()
    {
        if (Input.GetKeyDown(toggleInputKey))
        {
            useG29Input = !useG29Input;
            Debug.Log("🎮 Input switched to: " + (useG29Input ? "G29" : "Keyboard"));
        }

        HandleInput();
        CheckGrounded();
        HandleCarStates();
        HandleLighting();
        HandleFixedTransmission(); // FIXED transmission method
        HandleHandbrake();
        HandleTurnSignals();
        CalculateRealisticEnginePhysics();
        ApplyRealisticDriveForces();
        ApplyAerodynamicsAndResistance();
        ApplyRealisticSteering();
        UpdateSpeedAndRPMValues(); // FIXED: Enhanced method
        HandleSpeedLimiting(); // NEW: Handle hard speed limiting
        HandleTractionControl();
        HandleReverseLights();
        CheckForSignificantChanges(); // FIXED: Enhanced change detection

        if (Input.GetKeyDown(Boost_KeyCode) && Car_Started && Next_Boost_Time < Time.time)
        {
            Boost_Function();
            Next_Boost_Time = Time.time + Boost_Cooldown;
        }

        // FIXED - Update button states for proper edge detection
        if (useG29Input)
        {
            UpdateLogitechButtonStates();
        }
    }

    // NEW: Handle hard speed limiting based on gear
    void HandleSpeedLimiting()
    {
        if (currentGear <= 0 || currentGear >= gearTargetSpeeds.Length)
        {
            currentGearMaxSpeed = 0f;
            speedLimitingFactor = 1f;
            isSpeedLimited = false;
            return;
        }

        currentGearMaxSpeed = gearTargetSpeeds[currentGear];
        float currentSpeed = Car_Speed_KPH;
        float speedLimit = currentGearMaxSpeed + speedOvershootTolerance;

        // Check if we're approaching or exceeding the speed limit
        if (currentSpeed >= currentGearMaxSpeed)
        {
            isSpeedLimited = true;

            // Calculate how much we're over the target speed
            float speedExcess = currentSpeed - currentGearMaxSpeed;
            float excessRatio = speedExcess / speedOvershootTolerance;

            // Gradually reduce power as we approach and exceed the limit
            speedLimitingFactor = Mathf.Clamp01(1f - (excessRatio * speedLimitResponse));

            // Hard limit - drastically reduce power if we exceed tolerance
            if (currentSpeed > speedLimit)
            {
                speedLimitingFactor = 0.1f; // Allow only 10% power for engine braking

                // Apply engine braking to slow down
                if (motorInput > 0.1f)
                {
                    foreach (WheelCollider wheel in Back_Wheels)
                    {
                        if (wheel != null)
                        {
                            // Apply negative torque for engine braking
                            wheel.motorTorque = -currentEngineTorque * 0.3f;
                        }
                    }
                }

                if (enableDebugLogs && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"🛑 HARD SPEED LIMIT: {currentSpeed:F1}km/h > {speedLimit:F1}km/h in gear {currentGear} - Engine braking active");
                }
            }
            else if (enableDebugLogs && Time.frameCount % 60 == 0)
            {
                Debug.Log($"🏁 Speed limiting active: {currentSpeed:F1}/{currentGearMaxSpeed:F0}km/h (Power: {speedLimitingFactor * 100:F0}%)");
            }
        }
        else
        {
            isSpeedLimited = false;
            speedLimitingFactor = 1f;
        }
    }

    void HandleInput()
    {
        if (useG29Input)
        {
            motorInput = GetLogitechInput("throttle");
            g29BrakeInput = GetLogitechInput("brake");
            steerInput = GetLogitechInput("steering");
            float clutchValue = GetLogitechInput("clutch");
            clutchPressed = clutchValue > 0.75f;
        }
        else
        {
            motorInput = Mathf.Max(0f, Input.GetAxis("Vertical"));
            steerInput = Input.GetAxis("Horizontal");
            clutchPressed = Input.GetKey(clutchKey);
            g29BrakeInput = 0f;
        }
    }

    void CheckGrounded()
    {
        isGrounded = false;
        var allWheels = Front_Wheels.Concat(Back_Wheels);
        foreach (WheelCollider wheel in allWheels)
        {
            if (wheel != null && wheel.isGrounded)
            {
                isGrounded = true;
                break;
            }
        }
    }

    void HandleCarStates()
    {
        if (Input.GetKeyDown(Car_Off_Key) && (Car_Speed_KPH >= 0 && Car_Speed_KPH <= 1.5f) && Use_Car_States)
            Turn_Off_Car();
        if (Input.GetKeyDown(Car_Start_Key) && Use_Car_States)
            Car_Started = true;
        if (!Use_Car_States)
            Car_Started = true;
    }

    void HandleLighting()
    {
        if (Input.GetKeyDown(Headlights_Key) && Car_Started)
        {
            HeadLights_On = !HeadLights_On;
            SetLightState(LightType.Headlights, HeadLights_On);
        }

        if (!Car_Started)
            SetLightState(LightType.Headlights, false);
    }

    void HandleReverseLights()
    {
        bool reverseActive = (currentGear == -1 && motorInput > 0 && Car_Started);
        SetLightState(LightType.Reverse, reverseActive);
    }

    // COMPLETELY FIXED TRANSMISSION SYSTEM - Using your working logic
    void HandleFixedTransmission()
    {
        if (!Car_Started) return;

        // FIXED - Get button states with proper edge detection for sequential shifting
        bool gearUpButton = false;
        bool gearDownButton = false;
        bool reverseButton = false;
        bool neutralButton = false;

        if (useG29Input)
        {
            // FIXED - Using edge detection for G29 buttons
            gearUpButton = GetLogitechButtonDown(4); // Right paddle
            gearDownButton = GetLogitechButtonDown(5); // Left paddle
            reverseButton = GetLogitechButtonDown(2); // FIXED - Different button for reverse
            neutralButton = GetLogitechButtonDown(3); // FIXED - Different button for neutral
        }
        else
        {
            gearUpButton = Input.GetKeyDown(gearUpKey);
            gearDownButton = Input.GetKeyDown(gearDownKey);
            reverseButton = Input.GetKeyDown(reverseKey);
            neutralButton = Input.GetKeyDown(neutralKey);
        }

        // FIXED - Handle direct gear selection using your working logic
        if (enableDirectGearSelection)
        {
            HandleWorkingDirectGearSelection();
        }

        // FIXED - Handle sequential gear changes with proper paddle shifter logic
        if (gearUpButton && canChangeGear)
        {
            HandleSequentialGearUp();
        }

        if (gearDownButton && canChangeGear)
        {
            HandleSequentialGearDown();
        }

        // FIXED - Handle reverse toggle
        if (reverseButton && canChangeGear && Car_Speed_KPH < 5f)
        {
            HandleReverseToggle();
        }

        if (neutralButton && canChangeGear)
        {
            HandleNeutral();
        }

        // Handle clutch and stalling only for manual transmission
        if (!usePaddleShifters)
        {
            HandleRealisticClutch();
            CheckForStalling();
        }
        else
        {
            // Paddle shifters - no clutch needed
            clutchEngagement = 1f;
            clutchPosition = 0f;
            isStalling = false;
        }
    }

    // FIXED - Using your working direct gear selection logic
    void HandleWorkingDirectGearSelection()
    {
        // FIXED - From your working code logic
        KeyCode[] directGearKeys = { KeyCode.None, gear1Key, gear2Key, gear3Key, gear4Key, gear5Key };
        for (int i = 1; i <= maxGear; i++)
        {
            if (i < directGearKeys.Length)
            {
                bool keyPressed = false;
                bool wasPressed = false;

                if (useG29Input)
                {
                    // FIXED - Map G29 buttons for direct gear selection
                    // Update these button numbers based on your H-shifter
                    int[] g29GearButtons = { -1, 12, 13, 14, 15, 16 }; // Button indices for gears 1-5
                    if (i < g29GearButtons.Length && g29GearButtons[i] != -1)
                    {
                        keyPressed = GetLogitechButtonPressed(g29GearButtons[i]);
                        wasPressed = previousDirectGearButtons[i];
                    }
                }
                else
                {
                    // Keyboard input
                    keyPressed = Input.GetKey(directGearKeys[i]);
                    wasPressed = previousDirectGearButtons[i];
                }

                // FIXED - Edge detection logic from your working code
                if (keyPressed && !wasPressed && canChangeGear)
                {
                    // FIXED - Your working clutch logic
                    if (usePaddleShifters || clutchPressed || currentGear == 0)
                    {
                        ShiftGear(i);
                        Debug.Log($"🔢 DIRECT GEAR SELECT: {GetGearName(i)}");
                    }
                    else
                    {
                        Debug.LogWarning($"⚠️ Manual: Press clutch to select gear {i}!");
                    }
                }

                // FIXED - Store previous state
                previousDirectGearButtons[i] = keyPressed;
            }
        }

        // FIXED - Handle reverse and neutral for direct selection
        bool reverseKeyPressed = false;
        bool neutralKeyPressed = false;
        bool reverseWasPressed = previousReverseButton;
        bool neutralWasPressed = previousNeutralButton;

        if (useG29Input)
        {
            reverseKeyPressed = GetLogitechButtonPressed(18); // Update button index for reverse
            neutralKeyPressed = GetLogitechButtonPressed(19); // Update button index for neutral
        }
        else
        {
            reverseKeyPressed = Input.GetKey(reverseKey);
            neutralKeyPressed = Input.GetKey(neutralKey);
        }

        // Handle reverse direct selection
        if (reverseKeyPressed && !reverseWasPressed && canChangeGear && Car_Speed_KPH < 5f)
        {
            if (usePaddleShifters || clutchPressed || currentGear == 0)
            {
                ShiftGear(-1);
                Debug.Log("🔢 DIRECT GEAR SELECT: R");
            }
            else
            {
                Debug.LogWarning("⚠️ Manual: Press clutch to select reverse!");
            }
        }

        // Handle neutral direct selection
        if (neutralKeyPressed && !neutralWasPressed && canChangeGear)
        {
            if (usePaddleShifters || clutchPressed)
            {
                ShiftGear(0);
                Debug.Log("🔢 DIRECT GEAR SELECT: N");
            }
            else
            {
                Debug.LogWarning("⚠️ Manual: Press clutch to select neutral!");
            }
        }

        // Update previous button states
        previousReverseButton = reverseKeyPressed;
        previousNeutralButton = neutralKeyPressed;
    }

    // FIXED - Better sequential gear up for paddle shifters
    void HandleSequentialGearUp()
    {
        if (usePaddleShifters)
        {
            // FIXED - Paddle shifter logic for proper gear progression
            if (currentGear == -1)
            {
                // From reverse to neutral
                ShiftGear(0);
                return;
            }
            else if (currentGear == 0)
            {
                // From neutral to 1st gear
                ShiftGear(1);
                return;
            }
            else if (currentGear >= maxGear)
            {
                Debug.Log("⚠️ Already in highest gear");
                return;
            }
            else
            {
                // Normal upshift
                ShiftGear(currentGear + 1);
                return;
            }
        }
        else
        {
            // Manual transmission
            if (currentGear >= maxGear)
            {
                Debug.Log("⚠️ Already in highest gear");
                return;
            }

            if (currentGear == 0 || clutchPressed)
            {
                ShiftGear(currentGear + 1);
            }
            else
            {
                Debug.LogWarning("⚠️ Manual: Press clutch to shift up!");
            }
        }
    }

    // FIXED - Better sequential gear down for paddle shifters
    void HandleSequentialGearDown()
    {
        if (usePaddleShifters)
        {
            // FIXED - Paddle shifter logic for proper gear progression
            if (currentGear == 1)
            {
                // From 1st to neutral
                ShiftGear(0);
                return;
            }
            else if (currentGear == 0)
            {
                // From neutral to reverse
                if (Car_Speed_KPH < 5f)
                {
                    ShiftGear(-1);
                }
                else
                {
                    Debug.LogWarning("⚠️ Too fast for reverse!");
                }
                return;
            }
            else if (currentGear == -1)
            {
                Debug.Log("⚠️ Already in reverse");
                return;
            }
            else
            {
                // Normal downshift
                ShiftGear(currentGear - 1);
                return;
            }
        }
        else
        {
            // Manual transmission
            if (currentGear <= 1)
            {
                if (currentGear == 1)
                {
                    // Shift from 1st to neutral
                    if (clutchPressed)
                    {
                        ShiftGear(0);
                    }
                    else
                    {
                        Debug.LogWarning("⚠️ Manual: Press clutch to shift to neutral!");
                    }
                }
                else
                {
                    Debug.Log("⚠️ Already in lowest gear/neutral");
                }
                return;
            }

            if (clutchPressed)
            {
                ShiftGear(currentGear - 1);
            }
            else
            {
                Debug.LogWarning("⚠️ Manual: Press clutch to shift down!");
            }
        }
    }

    // FIXED - Reverse toggle for both modes
    void HandleReverseToggle()
    {
        bool canShift = false;

        if (usePaddleShifters)
        {
            canShift = true; // FIXED - Paddle shifters can access reverse
        }
        else
        {
            // Manual transmission
            if (clutchPressed)
            {
                canShift = true;
            }
            else
            {
                Debug.LogWarning("⚠️ Manual: Press clutch to engage reverse!");
                return;
            }
        }

        if (canShift)
        {
            if (currentGear == -1)
            {
                ShiftGear(0); // From reverse to neutral
                Debug.Log("🔄 REVERSE OFF → Neutral");
            }
            else
            {
                ShiftGear(-1); // To reverse
                Debug.Log("🔄 REVERSE ON");
            }
        }
    }

    void HandleNeutral()
    {
        bool canShift = false;

        if (usePaddleShifters)
        {
            canShift = true; // Paddle shifters don't need clutch
        }
        else
        {
            // Manual transmission
            if (clutchPressed)
            {
                canShift = true;
            }
            else
            {
                Debug.LogWarning("⚠️ Manual: Press clutch to shift to neutral!");
                return;
            }
        }

        if (canShift)
        {
            ShiftGear(0);
        }
    }

    void ShiftGear(int targetGear)
    {
        if (targetGear < -1 || targetGear > maxGear)
        {
            Debug.LogWarning($"⚠️ Invalid gear: {targetGear}");
            return;
        }

        int oldGear = currentGear;
        currentGear = targetGear;

        string transmissionMode = usePaddleShifters ? "PADDLE" : "MANUAL";
        string shiftDirection = targetGear > oldGear ? "⬆️" : "⬇️";

        Debug.Log($"{shiftDirection} {transmissionMode} SHIFT: {GetGearName(oldGear)} → {GetGearName(currentGear)} at {Car_Speed_KPH:F0} KPH");

        StartCoroutine(GearChangeDelay(0.1f));

        // FIXED: Trigger dashboard update immediately after gear change
        OnGearChanged?.Invoke();
    }

    void HandleRealisticClutch()
    {
        float clutchSpeed = clutchPressed ? 18f : 10f;
        float targetClutchPosition = clutchPressed ? 1f : 0f;

        clutchPosition = Mathf.MoveTowards(clutchPosition, targetClutchPosition, clutchSpeed * Time.fixedDeltaTime);
        clutchEngagement = 1f - clutchPosition;

        if (currentGear == 0)
        {
            clutchEngagement = 0f;
        }
    }

    IEnumerator GearChangeDelay(float delayTime)
    {
        canChangeGear = false;
        yield return new WaitForSeconds(delayTime);
        canChangeGear = true;
    }

    void CheckForStalling()
    {
        if (currentGear > 0 && !clutchPressed && Car_Speed_KPH < 6f && motorInput <= 0.1f)
        {
            if (engineRPM < idleRPM * 0.75f)
            {
                isStalling = true;
                Car_Started = false;
                Debug.LogWarning($"🔥 ENGINE STALLED - Speed too low for {GetGearName(currentGear)}!");
            }
        }
        else
        {
            isStalling = false;
        }
    }

    // ENGINE PHYSICS METHODS (keeping the same but with corrected values)
    void CalculateRealisticEnginePhysics()
    {
        if (!Car_Started)
        {
            engineRPM = Mathf.Lerp(engineRPM, 0f, Time.fixedDeltaTime * 2f);
            engineAngularVelocity = RPMToRadPerSec(engineRPM);
            currentEngineTorque = 0f;
            return;
        }

        wheelAngularVelocity = Car_Speed_KPH / 3.6f / tireRadius;

        if (currentGear == 0)
        {
            HandleNeutralEngine();
        }
        else if (currentGear == -1)
        {
            HandleReverseEngine();
        }
        else
        {
            HandleForwardEngine();
        }

        float targetEngineAngularVel = RPMToRadPerSec(engineRPM);
        float engineAcceleration = (targetEngineAngularVel - engineAngularVelocity) / engineInertia;
        engineAngularVelocity += engineAcceleration * Time.fixedDeltaTime;

        engineRPM = RadPerSecToRPM(engineAngularVelocity);
        engineRPM = Mathf.Clamp(engineRPM, 0f, maxEngineRPM * 1.02f);

        isRedlining = engineRPM > maxEngineRPM;
        if (isRedlining)
        {
            currentEngineTorque *= 0.15f;
        }

        RPM = engineRPM;
    }

    void HandleNeutralEngine()
    {
        float targetRPM = idleRPM + (motorInput * (maxEngineRPM - idleRPM) * 0.75f);
        engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.fixedDeltaTime * 8f);
        currentEngineTorque = 0f;
    }

    void HandleReverseEngine()
    {
        if (!usePaddleShifters && clutchPressed)
        {
            float targetRPM = idleRPM + (motorInput * 2500f);
            engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.fixedDeltaTime * 6f);
            currentEngineTorque = 0f;
        }
        else
        {
            float calculatedRPM = Mathf.Abs(wheelAngularVelocity) * Mathf.Abs(reverseGearRatio) * finalDriveRatio * 60f / (2f * Mathf.PI);
            calculatedRPM = Mathf.Max(calculatedRPM, idleRPM);

            if (motorInput > 0.08f)
            {
                float throttleRPM = idleRPM + (motorInput * 2500f);
                engineRPM = Mathf.Max(calculatedRPM, throttleRPM);
                currentEngineTorque = GetRealisticEngineTorque(engineRPM) * motorInput * clutchEngagement;
            }
            else
            {
                engineRPM = calculatedRPM;
                currentEngineTorque = 0f;
            }
        }
    }

    void HandleForwardEngine()
    {
        if (!usePaddleShifters && clutchPressed)
        {
            float targetRPM = idleRPM + (motorInput * (maxEngineRPM - idleRPM) * 0.65f);
            engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.fixedDeltaTime * 6f);
            currentEngineTorque = 0f;
        }
        else
        {
            // Calculate RPM based on current speed and gear
            float wheelRpm = (Car_Speed_KPH / 3.6f * 60f) / wheelCircumference;
            float roadRPM = wheelRpm * gearRatios[currentGear] * finalDriveRatio;
            roadRPM = Mathf.Max(roadRPM, idleRPM);

            if (motorInput > 0.03f)
            {
                float throttleRPM = idleRPM + (motorInput * (maxEngineRPM - idleRPM) * 0.55f);
                engineRPM = Mathf.Max(roadRPM, throttleRPM);

                float baseTorque = GetRealisticEngineTorque(engineRPM);

                // Apply speed limiting factor
                currentEngineTorque = baseTorque * motorInput * clutchEngagement * transmissionEfficiency * speedLimitingFactor;

                if (isSpeedLimited && enableDebugLogs && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"🔧 Speed-limited torque: Base={baseTorque:F0}Nm, Final={currentEngineTorque:F0}Nm (Limit factor: {speedLimitingFactor:F2})");
                }
            }
            else
            {
                engineRPM = roadRPM;

                if (Car_Speed_KPH > 4f && clutchEngagement > 0.45f)
                {
                    float engineBrakingTorque = GetRealisticEngineTorque(engineRPM) * 0.12f;
                    currentEngineTorque = -engineBrakingTorque * clutchEngagement;
                }
                else
                {
                    currentEngineTorque = 0f;
                }
            }
        }
    }

    float GetRealisticEngineTorque(float rpm)
    {
        float normalizedRPM = Mathf.Clamp01((rpm - idleRPM) / (maxEngineRPM - idleRPM));
        float torqueMultiplier = realisticTorqueCurve.Evaluate(normalizedRPM);
        return peakTorqueValue * torqueMultiplier;
    }

    void ApplyRealisticDriveForces()
    {
        if (!Car_Started || currentGear == 0)
        {
            foreach (WheelCollider wheel in Back_Wheels)
                if (wheel != null) wheel.motorTorque = 0f;

            wheelTorque = 0f;
            driveForce = 0f;
            return;
        }

        float gearRatio;
        if (currentGear > 0)
        {
            gearRatio = gearRatios[currentGear];
        }
        else
        {
            gearRatio = Mathf.Abs(reverseGearRatio);
        }

        wheelTorque = currentEngineTorque * gearRatio * finalDriveRatio;
        if (currentGear == -1)
        {
            wheelTorque = -wheelTorque;
        }

        driveForce = wheelTorque / tireRadius;
        ApplyTorqueWithDifferential(wheelTorque);
    }

    void ApplyTorqueWithDifferential(float totalTorque)
    {
        if (Back_Wheels.Count == 0) return;

        float baseTorquePerWheel = totalTorque / Back_Wheels.Count;

        foreach (WheelCollider wheel in Back_Wheels)
        {
            if (wheel == null) continue;

            float wheelTorqueAdjusted = baseTorquePerWheel;

            WheelHit hit;
            if (wheel.GetGroundHit(out hit))
            {
                float totalSlip = Mathf.Abs(hit.forwardSlip) + Mathf.Abs(hit.sidewaysSlip);
                if (totalSlip > 0.28f)
                {
                    wheelTorqueAdjusted *= Mathf.Clamp01(1f - (totalSlip - 0.28f) * 2f);
                }
            }

            wheel.motorTorque = wheelTorqueAdjusted;
        }
    }

    void ApplyAerodynamicsAndResistance()
    {
        if (!isGrounded) return;

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed > 0.1f)
        {
            float dragMagnitude = 0.5f * AIR_DENSITY * dragCoefficient * frontalArea * speed * speed;
            dragForce = dragMagnitude;
            Vector3 dragVector = -velocity.normalized * dragMagnitude;
            rb.AddForce(dragVector);

            float rollingResistanceMagnitude = rollingResistance * vehicleMass * 9.81f;
            rollingResistanceForce = rollingResistanceMagnitude;
            Vector3 rollingResistanceVector = -velocity.normalized * rollingResistanceMagnitude;
            rb.AddForce(rollingResistanceVector);
        }
        else
        {
            dragForce = 0f;
            rollingResistanceForce = 0f;
        }

        if (Car_Speed_KPH > 35f)
        {
            float downforceAmount = downforce * (Car_Speed_KPH / 100f) * (Car_Speed_KPH / 100f);
            rb.AddForce(-transform.up * downforceAmount);
        }
    }

    void ApplyRealisticSteering()
    {
        if (!Car_Started) return;

        float speedRatio = Mathf.Clamp01(Car_Speed_KPH / 100f);
        float steerReduction = Mathf.Lerp(1f, 0.42f, speedRatio);
        float finalSteerAngle = steerInput * Max_Steer_Angle * steerReduction;

        foreach (WheelCollider wheel in Front_Wheels)
            if (wheel != null) wheel.steerAngle = finalSteerAngle;
    }

    // FIXED: Enhanced speed and RPM updates with proper event triggering
    void UpdateSpeedAndRPMValues()
    {
        float previousSpeed = Car_Speed_KPH;
        float previousRPMValue = RPM;

        // FIXED: Enhanced speed calculation with safety checks
        if (rb != null)
        {
            Car_Speed_KPH = rb.linearVelocity.magnitude * 3.6f;
            Car_Speed_MPH = rb.linearVelocity.magnitude * 2.237f;
        }
        else
        {
            Car_Speed_KPH = 0f;
            Car_Speed_MPH = 0f;
        }

        Car_Speed_In_KPH = (int)Car_Speed_KPH;
        Car_Speed_In_MPH = (int)Car_Speed_MPH;

        // Update currSpeed for compatibility
        currSpeed = Car_Speed_KPH;

        // FIXED: Force trigger events on significant changes with better logic
        if (Mathf.Abs(Car_Speed_KPH - previousSpeed) > 0.1f) // Lower threshold for more frequent updates
        {
            OnSpeedChanged?.Invoke();

            if (enableDebugLogs)
                Debug.Log($"Speed changed: {previousSpeed:F1} -> {Car_Speed_KPH:F1} km/h");
        }

        if (Mathf.Abs(RPM - previousRPMValue) > 25f) // Lower RPM threshold
        {
            OnRPMChanged?.Invoke();
        }
    }

    void HandleTractionControl()
    {
        if (!enableTractionControl) return;

        float maxSlip = 0f;
        var allWheels = Front_Wheels.Concat(Back_Wheels).ToList();

        foreach (WheelCollider wheel in allWheels)
        {
            if (wheel == null) continue;

            WheelHit hit;
            if (wheel.GetGroundHit(out hit))
            {
                float totalSlip = Mathf.Sqrt(hit.forwardSlip * hit.forwardSlip + hit.sidewaysSlip * hit.sidewaysSlip);
                maxSlip = Mathf.Max(maxSlip, totalSlip);
            }
        }

        wheelSlip = maxSlip;

        if (maxSlip > tractionControlThreshold && Car_Started)
        {
            float reductionFactor = Mathf.Clamp01(1.5f - (maxSlip * 1.2f));
            foreach (WheelCollider wheel in Back_Wheels)
            {
                if (wheel != null)
                    wheel.motorTorque *= reductionFactor;
            }
        }
    }

    void HandleTurnSignals()
    {
        bool leftPressed = useG29Input ? GetLogitechButtonDown(0) : Input.GetKeyDown(leftTurnSignalKey);
        bool rightPressed = useG29Input ? GetLogitechButtonDown(1) : Input.GetKeyDown(rightTurnSignalKey);

        if (leftPressed)
        {
            leftTurnSignalActive = !leftTurnSignalActive;
            if (leftTurnSignalActive) rightTurnSignalActive = false;
            turnSignalTimer = 0f;
        }

        if (rightPressed)
        {
            rightTurnSignalActive = !rightTurnSignalActive;
            if (rightTurnSignalActive) leftTurnSignalActive = false;
            turnSignalTimer = 0f;
        }

        if (leftTurnSignalActive || rightTurnSignalActive)
        {
            turnSignalTimer += Time.deltaTime;
            float currentSteerInput = useG29Input ? steerInput : Input.GetAxis("Horizontal");

            if (Mathf.Abs(currentSteerInput) < 0.08f && turnSignalTimer > 2.5f)
            {
                leftTurnSignalActive = false;
                rightTurnSignalActive = false;
            }
        }
    }

    void HandleHandbrake()
    {
        if (Input.GetKey(handbrakeKey))
        {
            if (!handbrakeEngaged)
            {
                handbrakeEngaged = true;
                foreach (WheelCollider wheel in Back_Wheels)
                {
                    if (wheel != null)
                    {
                        wheel.brakeTorque = handbrakeForce;
                    }
                }
            }
        }
        else
        {
            if (handbrakeEngaged)
            {
                handbrakeEngaged = false;
                foreach (WheelCollider wheel in Back_Wheels)
                {
                    if (wheel != null)
                    {
                        wheel.brakeTorque = 0f;
                    }
                }
            }
        }

        // Regular braking
        if (useG29Input)
        {
            Brakes = g29BrakeInput;
        }
        else
        {
            Brakes = Input.GetAxis("Vertical") < 0 ? Mathf.Abs(Input.GetAxis("Vertical")) : 0f;
        }

        foreach (WheelCollider wheel in Front_Wheels.Concat(Back_Wheels))
        {
            if (wheel != null && !handbrakeEngaged)
            {
                wheel.brakeTorque = Brakes * BrakeForce;
            }
        }

        // Brake lights
        bool brakeActive = Brakes > 0.1f || handbrakeEngaged;
        SetLightState(LightType.Brake, brakeActive);
    }

    void Boost_Function()
    {
        if (!enable_boost) return;

        rb.AddForce(transform.forward * Boost_Amount * 1000f);

        if (Enable_Boost_particles && Boost_particles != null)
        {
            foreach (ParticleSystem particle in Boost_particles)
            {
                if (particle != null)
                {
                    particle.Play();
                }
            }
        }

        Debug.Log("🚀 BOOST ACTIVATED!");
    }

    void Turn_Off_Car()
    {
        Car_Started = false;
        engineRPM = 0f;
        currentEngineTorque = 0f;
        SetLightState(LightType.Headlights, false);
        HeadLights_On = false;
        Debug.Log("🔑 Car turned off");
    }

    void SetLightState(LightType lightType, bool state)
    {
        switch (lightType)
        {
            case LightType.Headlights:
                if (Enable_Headlights_Lights && HeadLights != null)
                {
                    foreach (Light light in HeadLights)
                        if (light != null) light.enabled = state;
                }

                if (Enable_Headlights_MeshRenderers && HeadLights_MeshRenderers != null)
                {
                    foreach (MeshRenderer renderer in HeadLights_MeshRenderers)
                        if (renderer != null) renderer.enabled = state;
                }

                if (Enable_Headlights_Materials && HeadLight_Material != null)
                {
                    Color emissionColor = state ? HeadLights_On_Color : HeadLights_Off_Color;
                    HeadLight_Material.SetColor("_EmissionColor", emissionColor);
                }

                if (Headlight_Objects != null)
                {
                    foreach (GameObject obj in Headlight_Objects)
                        if (obj != null) obj.SetActive(state);
                }
                break;

            case LightType.Brake:
                if (Enable_Brakelights_Lights && BrakeLights != null)
                {
                    foreach (Light light in BrakeLights)
                        if (light != null) light.enabled = state;
                }

                if (Enable_Brakelights_MeshRenderers && BrakeLights_MeshRenderers != null)
                {
                    foreach (MeshRenderer renderer in BrakeLights_MeshRenderers)
                        if (renderer != null) renderer.enabled = state;
                }

                if (Enable_Brakelights_Materials && BrakeLight_Material != null)
                {
                    Color emissionColor = state ? BrakeLights_On_Color : BrakeLights_Off_Color;
                    BrakeLight_Material.SetColor("_EmissionColor", emissionColor);
                }

                if (BrakeLight_Objects != null)
                {
                    foreach (GameObject obj in BrakeLight_Objects)
                        if (obj != null) obj.SetActive(state);
                }
                break;

            case LightType.Reverse:
                if (Enable_Reverselights_Lights && ReverseLights != null)
                {
                    foreach (Light light in ReverseLights)
                        if (light != null) light.enabled = state;
                }

                if (Enable_Reverselights_MeshRenderers && ReverseLights_MeshRenderers != null)
                {
                    foreach (MeshRenderer renderer in ReverseLights_MeshRenderers)
                        if (renderer != null) renderer.enabled = state;
                }

                if (Enable_Reverselights_Materials && ReverseLight_Material != null)
                {
                    Color emissionColor = state ? ReverseLights_On_Color : ReverseLights_Off_Color;
                    ReverseLight_Material.SetColor("_EmissionColor", emissionColor);
                }

                if (Reverse_Light_Objects != null)
                {
                    foreach (GameObject obj in Reverse_Light_Objects)
                        if (obj != null) obj.SetActive(state);
                }
                break;
        }
    }

    // Utility methods
    private float RPMToRadPerSec(float rpm) => rpm * 2f * Mathf.PI / 60f;
    private float RadPerSecToRPM(float radPerSec) => radPerSec * 60f / (2f * Mathf.PI);

    private string GetGearName(int gear)
    {
        switch (gear)
        {
            case -1: return "R";
            case 0: return "N";
            case 1: return "1st";
            case 2: return "2nd";
            case 3: return "3rd";
            case 4: return "4th";
            case 5: return "5th";
            case 6: return "6th";
            default: return gear.ToString();
        }
    }

    // FIXED: Enhanced public API methods with proper data retrieval and debug logging
    public float GetCurrentSpeed()
    {
        if (enableDebugLogs && Time.frameCount % 60 == 0) // Log every 60 frames
            Debug.Log($"GetCurrentSpeed() returning: {Car_Speed_KPH:F1} km/h");
        return Car_Speed_KPH;
    }

    public float GetCurrentRPM() => engineRPM;
    public int GetCurrentGear() => currentGear;
    public bool IsCarStarted() => Car_Started;

    public void SetCarStarted(bool started)
    {
        Car_Started = started;
        if (!started) Turn_Off_Car();
    }

    // Enhanced getters for dashboard
    public float GetSpeedKPH() => Car_Speed_KPH;
    public float GetSpeedMPH() => Car_Speed_MPH;
    public bool IsEngineRunning() => Car_Started;
    public bool IsGrounded() => isGrounded;
    public bool IsHandbrakeEngaged() => handbrakeEngaged;
    public bool IsClutchPressed() => clutchPressed;
    public float GetClutchPosition() => clutchPosition;
    public bool IsLeftTurnSignalActive() => leftTurnSignalActive;
    public bool IsRightTurnSignalActive() => rightTurnSignalActive;
    public float GetCurrentSteerInput() => steerInput;
    public float GetEngineRPM() => engineRPM;
    public float GetEngineTorque() => currentEngineTorque;
    public float GetWheelTorque() => wheelTorque;
    public float GetDriveForce() => driveForce;
    public bool IsRedlining() => isRedlining;
    public float GetClutchEngagement() => clutchEngagement;
    public bool IsStalling() => isStalling;
    public bool IsUsingPaddleShifters() => usePaddleShifters;

    // NEW: Speed limiting getters
    public float GetCurrentGearMaxSpeed() => currentGearMaxSpeed;
    public float GetSpeedLimitingFactor() => speedLimitingFactor;
    public bool IsSpeedLimited() => isSpeedLimited;
    public float[] GetGearTargetSpeeds() => gearTargetSpeeds;

    // ENHANCED TELEMETRY DATA STRUCTURE
    [System.Serializable]
    public class CarTelemetryData
    {
        [Header("Basic Data")]
        public float currentSpeed;
        public float currentRPM;
        public int currentGear;
        public bool carStarted;

        [Header("Input Data")]
        public float steerInput;
        public float throttleInput;
        public float brakeInput;
        public bool handbrakeEngaged;
        public float clutchPosition;
        public float clutchEngagement;

        [Header("Physics Data")]
        public bool isGrounded;
        public float wheelSlip;
        public bool isRedlining;
        public bool isStalling;

        [Header("Forces")]
        public float engineTorque;
        public float wheelTorque;
        public float driveForce;
        public float dragForce;
        public float rollingResistanceForce;

        [Header("Systems")]
        public bool leftTurnSignal;
        public bool rightTurnSignal;
        public bool useManualTransmission;
        public bool usePaddleShifters;

        // NEW: Speed limiting data
        [Header("Speed Limiting")]
        public float currentGearMaxSpeed;
        public float speedLimitingFactor;
        public bool isSpeedLimited;
    }

    // FIXED: Enhanced telemetry data method with all required fields
    public CarTelemetryData GetCarTelemetryData()
    {
        return new CarTelemetryData
        {
            currentSpeed = Car_Speed_KPH,
            currentRPM = engineRPM,
            currentGear = currentGear,
            carStarted = Car_Started,
            steerInput = steerInput,
            throttleInput = motorInput,
            brakeInput = useG29Input ? g29BrakeInput : (Input.GetKey(KeyCode.B) ? 1f : 0f),
            handbrakeEngaged = handbrakeEngaged,
            isGrounded = isGrounded,
            leftTurnSignal = leftTurnSignalActive,
            rightTurnSignal = rightTurnSignalActive,
            clutchPosition = clutchPosition,
            isStalling = isStalling,
            useManualTransmission = useManualTransmission,
            usePaddleShifters = usePaddleShifters,
            engineTorque = currentEngineTorque,
            wheelTorque = wheelTorque,
            driveForce = driveForce,
            dragForce = dragForce,
            rollingResistanceForce = rollingResistanceForce,
            wheelSlip = wheelSlip,
            isRedlining = isRedlining,
            clutchEngagement = clutchEngagement,
            // NEW: Speed limiting data
            currentGearMaxSpeed = currentGearMaxSpeed,
            speedLimitingFactor = speedLimitingFactor,
            isSpeedLimited = isSpeedLimited
        };
    }
}
