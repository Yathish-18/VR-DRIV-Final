using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class G29Car_Controller : MonoBehaviour
{
    #region Input Settings
    [Header("Input Settings")]
    public bool useG29Input = false;
    public Key toggleInputKey = Key.F1;
    [Range(0.01f, 0.3f)] public float pedalDeadzone = 0.1f;
    [Range(1f, 3f)] public float pedalSensitivity = 1.5f;
    #endregion

    #region Wheel & Physics
    [Header("Wheel Colliders")]
    public List<WheelCollider> Front_Wheels;
    public List<WheelCollider> Back_Wheels;

    [Header("Wheel Transforms")]
    public List<Transform> Front_Wheel_Transforms;
    public List<Transform> Back_Wheel_Transforms;

    [Header("Suspension & Handling")]
    public float antiRoll = 5000f;
    public float downforce = 100f;
    public float wheelRadius = 0.33f;
    public float wheelMass = 20f;
    public float suspensionDistance = 0.3f;
    public float suspensionSpring = 35000f;
    public float suspensionDamper = 4500f;
    public float suspensionTargetPosition = 0.5f;
    #endregion

    #region Transmission
    [Header("Transmission Settings")]
    public bool useManualTransmission = true;
    public bool enableRevMatching = true;
    public AnimationCurve revMatchingCurve = AnimationCurve.EaseInOut(0, 0.8f, 1, 1.2f);
    public Key gearUpKey = Key.Q;
    public Key gearDownKey = Key.E;
    public Key clutchKey = Key.LeftShift;
    public float[] gearRatios = { 0f, 2.66f, 1.78f, 1.30f, 1.00f, 0.74f, 0.50f };
    public float finalDriveRatio = 3.42f;
    public float clutchEngagement = 1f;
    public float clutchBitePoint = 0.3f;
    public float clutchSlipRPM = 200f;
    public float engineBraking = 0.1f;
    public float revMatchingSpeed = 2.0f;
    public int maxGear = 6;
    #endregion

    #region Engine & Performance
    [Header("Engine Settings")]
    public float Motor_Torque = 1500f;
    public float Max_Steer_Angle = 30f;
    public float BrakeForce = 3000f;
    public float Maximum_Speed = 200f;
    public AnimationCurve motorTorqueCurve = AnimationCurve.EaseInOut(0, 1, 1, 0.3f);
    public AnimationCurve steerCurve = AnimationCurve.Linear(0, 1, 1, 0.5f);
    #endregion

    #region Driver Assists
    [Header("Driver Assists")]
    public bool enableTractionControl = true;
    public float tractionControlSensitivity = 1.2f;
    public bool enableABS = true;
    public float ABSThreshold = 0.8f;
    #endregion

    #region Audio & Effects
    [Header("Audio Settings")]
    public AudioSource Engine_Sound;
    public AudioSource Horn_Source;
    public AudioSource Crash_Sound;
    public float Minimum_Pitch_Value = 0.5f;
    public float Maximum_Pitch_Value = 2.5f;
    #endregion

    #region Private Variables
    private Rigidbody rb;
    private float motorInput, steerInput, brakeInput;
    private bool clutchPressed;
    private int currentGear = 0;
    private float engineRPM, wheelRPM, targetEngineRPM;
    private bool isStalling, canChangeGear = true;
    private WheelHit[] wheelHits;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        wheelHits = new WheelHit[Front_Wheels.Count + Back_Wheels.Count];
        SetupRealisticWheels();
    }

    private void FixedUpdate()
    {
        HandleInput();
        if (useManualTransmission) HandleManualTransmission();
        ApplyMotorTorque();
        ApplySteering();
        ApplyDownforce();
        ApplyAntiRoll();
    }

    private void Update()
    {
        UpdateWheelVisuals();
        UpdateAudio();
    }
    #endregion

    #region Input Handling
    private void HandleInput()
    {
        if (Keyboard.current[toggleInputKey].wasPressedThisFrame)
            useG29Input = !useG29Input;

        if (useG29Input)
        {
            // Throttle: -1 (released) to 1 (fully pressed) → Remap to 0-1
            float rawThrottle = Gamepad.current.rightTrigger.ReadValue();
            motorInput = Mathf.Clamp01((rawThrottle + 1f) * 0.5f);

            // Brake: -1 (released) to 1 (fully pressed) → Remap to 0-1
            float rawBrake = Gamepad.current.leftTrigger.ReadValue();
            brakeInput = Mathf.Clamp01((rawBrake + 1f) * 0.5f);

            // Clutch: Button press (adjust for your setup)
            clutchPressed = Gamepad.current.leftShoulder.isPressed;
        }
        else
        {
            motorInput = Mathf.Max(0f, Keyboard.current.wKey.ReadValue());
            brakeInput = Keyboard.current.sKey.ReadValue();
            steerInput = Keyboard.current.aKey.ReadValue() - Keyboard.current.dKey.ReadValue();
            clutchPressed = Keyboard.current[clutchKey].isPressed;
        }

        // Apply deadzone & sensitivity
        motorInput = ApplyPedalAdjustments(motorInput);
        brakeInput = ApplyPedalAdjustments(brakeInput);
    }

    private float ApplyPedalAdjustments(float input)
    {
        if (input < pedalDeadzone) return 0f;
        return Mathf.Pow((input - pedalDeadzone) / (1f - pedalDeadzone), pedalSensitivity);
    }
    #endregion

    #region Transmission & Gearbox
    private void HandleManualTransmission()
    {
        bool gearUpPressed = useG29Input ? Gamepad.current.yButton.wasPressedThisFrame : Keyboard.current[gearUpKey].wasPressedThisFrame;
        bool gearDownPressed = useG29Input ? Gamepad.current.xButton.wasPressedThisFrame : Keyboard.current[gearDownKey].wasPressedThisFrame;

        if (gearUpPressed && canChangeGear && clutchPressed)
        {
            ShiftGear(1); // Shift up
        }
        else if (gearDownPressed && canChangeGear && clutchPressed)
        {
            ShiftGear(-1); // Shift down
        }

        UpdateClutch();
        CheckStalling();
    }

    private void ShiftGear(int direction)
    {
        int newGear = currentGear + direction;
        if (newGear >= -1 && newGear <= maxGear)
        {
            currentGear = newGear;
            if (enableRevMatching) RevMatch(direction > 0);
            StartCoroutine(GearChangeDelay());
        }
    }

    private void RevMatch(bool upshift)
    {
        float ratio = revMatchingCurve.Evaluate(engineRPM / 7000f);
        targetEngineRPM = (wheelRPM * gearRatios[currentGear] * finalDriveRatio) * ratio;
    }

    private void UpdateClutch()
    {
        float targetClutch = clutchPressed ? 0f : 1f;
        clutchEngagement = Mathf.Lerp(clutchEngagement, targetClutch, Time.fixedDeltaTime * 5f);

        // Simulate clutch bite point
        if (clutchEngagement < clutchBitePoint && currentGear != 0)
        {
            engineRPM = Mathf.Lerp(engineRPM, 1000f, Time.fixedDeltaTime * 2f);
        }
    }

    private void CheckStalling()
    {
        if (currentGear > 0 && CarSpeedKPH < 5f && !clutchPressed && motorInput <= 0 && engineRPM < 600f)
        {
            isStalling = true;
            engineRPM = 0f;
        }
    }

    private IEnumerator GearChangeDelay()
    {
        canChangeGear = false;
        yield return new WaitForSeconds(0.2f);
        canChangeGear = true;
    }
    #endregion

    #region Physics & Handling
    private void ApplyMotorTorque()
    {
        float torquePerWheel = Motor_Torque / Back_Wheels.Count;
        foreach (WheelCollider wheel in Back_Wheels)
        {
            wheel.motorTorque = torquePerWheel * clutchEngagement;
        }
    }

    private void ApplySteering()
    {
        float speedRatio = Mathf.Clamp01(CarSpeedKPH / 100f);
        float steerAngle = steerInput * Max_Steer_Angle * steerCurve.Evaluate(speedRatio);

        foreach (WheelCollider wheel in Front_Wheels)
        {
            wheel.steerAngle = steerAngle;
        }
    }

    private void ApplyDownforce()
    {
        float speedFactor = Mathf.Pow(CarSpeedKPH / 100f, 2);
        rb.AddForce(-transform.up * downforce * speedFactor);
    }

    private void ApplyAntiRoll()
    {
        ApplyAntiRollBar(Front_Wheels);
        ApplyAntiRollBar(Back_Wheels);
    }

    private void ApplyAntiRollBar(List<WheelCollider> wheels)
    {
        if (wheels.Count < 2) return;

        WheelHit hitLeft, hitRight;
        bool groundedLeft = wheels[0].GetGroundHit(out hitLeft);
        bool groundedRight = wheels[1].GetGroundHit(out hitRight);

        float travelLeft = groundedLeft ? (-wheels[0].transform.InverseTransformPoint(hitLeft.point).y - wheels[0].radius) / wheels[0].suspensionDistance : 0f;
        float travelRight = groundedRight ? (-wheels[1].transform.InverseTransformPoint(hitRight.point).y - wheels[1].radius) / wheels[1].suspensionDistance : 0f;

        float antiRollForce = (travelLeft - travelRight) * antiRoll;

        if (groundedLeft) rb.AddForceAtPosition(wheels[0].transform.up * -antiRollForce, wheels[0].transform.position);
        if (groundedRight) rb.AddForceAtPosition(wheels[1].transform.up * antiRollForce, wheels[1].transform.position);
    }
    #endregion

    #region Helper Methods
    private void SetupRealisticWheels()
    {
        foreach (WheelCollider wheel in Front_Wheels.Concat(Back_Wheels))
        {
            wheel.mass = wheelMass;
            wheel.radius = wheelRadius;
            wheel.suspensionDistance = suspensionDistance;

            JointSpring spring = wheel.suspensionSpring;
            spring.spring = suspensionSpring;
            spring.damper = suspensionDamper;
            spring.targetPosition = suspensionTargetPosition;
            wheel.suspensionSpring = spring;
        }
    }

    private void UpdateWheelVisuals()
    {
        for (int i = 0; i < Front_Wheels.Count; i++)
        {
            Front_Wheels[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
            Front_Wheel_Transforms[i].position = pos;
            Front_Wheel_Transforms[i].rotation = rot;
        }

        for (int i = 0; i < Back_Wheels.Count; i++)
        {
            Back_Wheels[i].GetWorldPose(out Vector3 pos, out Quaternion rot);
            Back_Wheel_Transforms[i].position = pos;
            Back_Wheel_Transforms[i].rotation = rot;
        }
    }

    private void UpdateAudio()
    {
        if (!Engine_Sound) return;

        float pitch = Mathf.Lerp(Minimum_Pitch_Value, Maximum_Pitch_Value, engineRPM / 7000f);
        Engine_Sound.pitch = pitch;
    }
    #endregion

    #region Public Properties
    public float CarSpeedKPH => rb.linearVelocity.magnitude * 3.6f;
    public float CarSpeedMPH => rb.linearVelocity.magnitude * 2.237f;
    public float CurrentRPM => engineRPM;
    public int CurrentGear => currentGear;
    public bool IsStalling => isStalling;
    #endregion
}