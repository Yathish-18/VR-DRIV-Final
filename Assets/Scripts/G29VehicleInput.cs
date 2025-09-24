using UnityEngine;
using UnityEngine.InputSystem;

public class G29VehicleInput : MonoBehaviour
{
    public float SteeValue { get; private set; }
    public float AccValue { get; private set; }
    public float BrakeValue { get; private set; }
    public float ClutchValue { get; private set; }

    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Wheel Meshes")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Vehicle Settings")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float maxSteerAngle = 30f;

    [Header("Input Actions")]
    public InputActionAsset inputAsset;

    [Header("Physics Enhancements")]
    public Rigidbody rb;
    public Transform centerOfMassOffset;
    public float steeringSmoothing = 5f;
    public float torqueSmoothing = 5f;

    [Header("Box Lift Detection")]
    public Transform box;
    public float liftThreshold = 1.5f;
    private bool boxLiftedNotified = false;

    [Header("Instruction Integration")]
    public ForkliftEvaluator evaluator;
    public ForkliftInstructionManager instructionManager;

    private InputAction accelAction;
    private InputAction brakeAction;
    private InputAction gearUpAction;
    private InputAction gearDownAction;
    private InputAction steerAction;

    private float motorInput;
    private float brakeInput;
    private float steerInput;
    private float currentTorque;
    private float currentSteer;

    private int gearState = 0; // -1 = Reverse, 0 = Neutral, 1 = Forward

    private float flRotation, frRotation, rlRotation, rrRotation;

    void Start()
    {
        var drivingMap = inputAsset.FindActionMap("Driving");

        accelAction = drivingMap.FindAction("Throttle");
        brakeAction = drivingMap.FindAction("Brake1");
        gearUpAction = drivingMap.FindAction("GearUp");
        gearDownAction = drivingMap.FindAction("GearDown");
        steerAction = drivingMap.FindAction("Steer");

        drivingMap.Enable();

        if (rb && centerOfMassOffset)
            rb.centerOfMass = centerOfMassOffset.localPosition;

        AdjustWheelFriction(frontLeft);
        AdjustWheelFriction(frontRight);
        AdjustWheelFriction(rearLeft);
        AdjustWheelFriction(rearRight);
    }

    void Update()
    {
        float rawThrottle = accelAction.ReadValue<float>();
        float rawBrake = brakeAction.ReadValue<float>();

        motorInput = 1f - rawThrottle;
        brakeInput = 1f - rawBrake;
        steerInput = steerAction.ReadValue<float>();

        bool gearUpHeld = gearUpAction.ReadValue<float>() > 0.5f;
        bool gearDownHeld = gearDownAction.ReadValue<float>() > 0.5f;

        if (gearUpHeld) gearState = 1;
        else if (gearDownHeld) gearState = -1;
        else gearState = 0;

        // ✅ Box Lift Check
        if (!boxLiftedNotified && box != null && box.position.y > liftThreshold)
        {
            boxLiftedNotified = true;

            if (evaluator != null)
                evaluator.OnBoxLifted();

            if (instructionManager != null)
                instructionManager.OnBoxLiftedExternally();
        }
        // Steering
        float steering = steerAction.ReadValue<float>();
        SteeValue = steering;

        // Accelerator
        float accel = accelAction.ReadValue<float>();
        AccValue = Mathf.Clamp01(accel);

        // Brake
        float brake = brakeAction.ReadValue<float>();
        BrakeValue = Mathf.Clamp01(brake);

        

    }

    void FixedUpdate()
    {
        float targetTorque = motorInput * motorForce * gearState * -1f;
        currentTorque = Mathf.Lerp(currentTorque, targetTorque, Time.fixedDeltaTime * torqueSmoothing);

        float braking = brakeInput * brakeForce;

        float targetSteer = steerInput * maxSteerAngle;
        currentSteer = Mathf.Lerp(currentSteer, targetSteer, Time.fixedDeltaTime * steeringSmoothing);

        ApplyToAllWheels(currentTorque, braking, currentSteer);
        UpdateWheelVisuals();
    }

    void ApplyToAllWheels(float torque, float brake, float steer)
    {
        frontLeft.motorTorque = torque;
        frontRight.motorTorque = torque;
        rearLeft.motorTorque = torque;
        rearRight.motorTorque = torque;

        frontLeft.brakeTorque = brake;
        frontRight.brakeTorque = brake;
        rearLeft.brakeTorque = brake;
        rearRight.brakeTorque = brake;

        frontLeft.steerAngle = steer;
        frontRight.steerAngle = steer;
    }

    void UpdateWheelVisuals()
    {
        UpdateWheel(frontLeft, frontLeftMesh, ref flRotation, true);
        UpdateWheel(frontRight, frontRightMesh, ref frRotation, true);
        UpdateWheel(rearLeft, rearLeftMesh, ref rlRotation, false);
        UpdateWheel(rearRight, rearRightMesh, ref rrRotation, false);
    }

    void UpdateWheel(WheelCollider collider, Transform mesh, ref float rotVal, bool isFront)
    {
        collider.GetWorldPose(out Vector3 pos, out Quaternion _);
        mesh.position = pos;

        float rpm = collider.rpm;
        rotVal += rpm * 6f * Time.deltaTime;

        float steerAngle = isFront ? collider.steerAngle : 0f;
        mesh.localRotation = Quaternion.Euler(0f, 90f + steerAngle, rotVal);
    }

    void AdjustWheelFriction(WheelCollider wc)
    {
        WheelFrictionCurve forwardFriction = wc.forwardFriction;
        forwardFriction.extremumSlip = 0.4f;
        forwardFriction.asymptoteSlip = 0.8f;
        forwardFriction.stiffness = 1.5f;

        WheelFrictionCurve sidewaysFriction = wc.sidewaysFriction;
        sidewaysFriction.extremumSlip = 0.2f;
        sidewaysFriction.asymptoteSlip = 0.5f;
        sidewaysFriction.stiffness = 2.0f;

        wc.forwardFriction = forwardFriction;
        wc.sidewaysFriction = sidewaysFriction;
    }

    // ✅ Public accessors for instruction manager
    public int GearState => gearState;
    public float AcceleratorValue => 1f - accelAction.ReadValue<float>();
    public float SteeringValue => steerInput;
}
