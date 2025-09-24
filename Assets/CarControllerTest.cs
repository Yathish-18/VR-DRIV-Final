using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RealisticCarController : MonoBehaviour
{
    [Header("Car Settings")]
    [SerializeField] private float motorForce = 1500f;
    [SerializeField] private float brakeForce = 3000f;
    [SerializeField] private float maxSteerAngle = 30f;

    [Header("Wheel Physics")]
    [SerializeField] private float downForce = 100f;
    [SerializeField] private float maxSpeed = 200f;
    [SerializeField] private AnimationCurve motorTorqueCurve = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);

    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider frontLeftWheelCollider;
    [SerializeField] private WheelCollider frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider;
    [SerializeField] private WheelCollider rearRightWheelCollider;

    [Header("Wheel Transforms")]
    [SerializeField] private Transform frontLeftWheelTransform;
    [SerializeField] private Transform frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform;
    [SerializeField] private Transform rearRightWheelTransform;

    [Header("Car Body")]
    [SerializeField] private Transform centerOfMass;
    [SerializeField] private float antiRoll = 5000f;

    [Header("Input Settings")]
    [SerializeField] private KeyCode accelerateKey = KeyCode.W;
    [SerializeField] private KeyCode brakeKey = KeyCode.S;
    [SerializeField] private KeyCode leftKey = KeyCode.A;
    [SerializeField] private KeyCode rightKey = KeyCode.D;
    [SerializeField] private KeyCode handbrakeKey = KeyCode.Space;
    [SerializeField] private KeyCode nitroKey = KeyCode.LeftShift;

    [Header("Advanced Features")]
    [SerializeField] private bool enableNitro = true;
    [SerializeField] private float nitroForce = 3000f;
    [SerializeField] private float nitroConsumption = 10f;
    [SerializeField] private float nitroRegenRate = 5f;
    [SerializeField] private float currentNitro = 100f;
    [SerializeField] private bool enableTCS = true;
    [SerializeField] private bool enableABS = true;

    [Header("Audio")]
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private AudioClip engineIdleClip;
    [SerializeField] private AudioClip engineRevClip;
    [SerializeField] private float minPitch = 0.8f;
    [SerializeField] private float maxPitch = 2.0f;

    // Private variables
    private Rigidbody carRigidbody;
    private float horizontalInput;
    private float verticalInput;
    private float currentSteerAngle;
    private float currentMotorTorque;
    private bool isHandbraking;
    private bool isUsingNitro;
    private float currentSpeed;
    private float engineRPM;

    // Wheel friction curves
    private WheelFrictionCurve forwardFriction;
    private WheelFrictionCurve sidewaysFriction;

    private void Start()
    {
        InitializeCar();
        SetupWheelFriction();
        SetupAudio();
    }

    private void InitializeCar()
    {
        carRigidbody = GetComponent<Rigidbody>();

        // Set center of mass for better handling - ensure it's perfectly centered
        if (centerOfMass != null)
            carRigidbody.centerOfMass = centerOfMass.localPosition;
        else
            carRigidbody.centerOfMass = new Vector3(0, -0.5f, 0); // Perfectly centered on X-axis

        // Configure rigidbody
        carRigidbody.mass = 1200f;
        carRigidbody.linearDamping = 0.3f;
        carRigidbody.angularDamping = 3f;

        // Ensure rigidbody constraints are proper
        carRigidbody.freezeRotation = false;

        // Reset any existing forces
        carRigidbody.linearVelocity = Vector3.zero;
        carRigidbody.angularVelocity = Vector3.zero;
    }

    private void SetupWheelFriction()
    {
        // Configure wheel friction for realistic behavior
        forwardFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.4f,
            extremumValue = 1f,
            asymptoteSlip = 0.8f,
            asymptoteValue = 0.5f,
            stiffness = 1f
        };

        sidewaysFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.2f,
            extremumValue = 1f,
            asymptoteSlip = 0.5f,
            asymptoteValue = 0.75f,
            stiffness = 1f
        };

        ApplyFrictionToWheels();
        EnsureWheelAlignment();
    }

    private void EnsureWheelAlignment()
    {
        // Ensure all wheels have identical settings to prevent drift
        WheelCollider[] wheels = { frontLeftWheelCollider, frontRightWheelCollider,
                                  rearLeftWheelCollider, rearRightWheelCollider };

        foreach (var wheel in wheels)
        {
            if (wheel != null)
            {
                // Reset wheel collider positions and ensure they're aligned
                wheel.motorTorque = 0;
                wheel.brakeTorque = 0;
                wheel.steerAngle = 0;

                // Ensure suspension settings are identical
                JointSpring suspensionSpring = wheel.suspensionSpring;
                suspensionSpring.spring = 35000f;
                suspensionSpring.damper = 4500f;
                suspensionSpring.targetPosition = 0.5f;
                wheel.suspensionSpring = suspensionSpring;

                // Set identical suspension distance
                wheel.suspensionDistance = 0.3f;
                wheel.radius = 0.35f;
                wheel.mass = 20f;
            }
        }
    }

    private void ApplyFrictionToWheels()
    {
        WheelCollider[] wheels = { frontLeftWheelCollider, frontRightWheelCollider,
                                  rearLeftWheelCollider, rearRightWheelCollider };

        foreach (var wheel in wheels)
        {
            if (wheel != null)
            {
                wheel.forwardFriction = forwardFriction;
                wheel.sidewaysFriction = sidewaysFriction;
            }
        }
    }

    private void SetupAudio()
    {
        if (engineAudioSource == null)
        {
            engineAudioSource = gameObject.AddComponent<AudioSource>();
            engineAudioSource.loop = true;
            engineAudioSource.volume = 0.5f;
        }
    }

    private void Update()
    {
        GetInput();
        HandleNitro();
        UpdateWheelPoses();
        UpdateEngineAudio();
        UpdateUI();
    }

    private void FixedUpdate()
    {
        HandleMotor();
        HandleSteering();
        HandleBraking();
        ApplyDownforce();
        ApplyAntiRoll();
        CalculateEngineRPM();
        ApplyTractionControl();
    }

    private void GetInput()
    {
        // Keyboard input handling
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Input.GetKey(leftKey))
            horizontalInput = -1f;
        else if (Input.GetKey(rightKey))
            horizontalInput = 1f;

        if (Input.GetKey(accelerateKey))
            verticalInput = 1f;
        else if (Input.GetKey(brakeKey))
            verticalInput = -1f;

        isHandbraking = Input.GetKey(handbrakeKey);
        isUsingNitro = Input.GetKey(nitroKey) && enableNitro && currentNitro > 0;

        currentSpeed = carRigidbody.linearVelocity.magnitude * 3.6f; // Convert to km/h
    }

    private void HandleMotor()
    {
        // Calculate motor torque based on speed curve
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);
        float torqueMultiplier = motorTorqueCurve.Evaluate(speedRatio);

        currentMotorTorque = verticalInput * motorForce * torqueMultiplier;

        // Apply nitro boost
        if (isUsingNitro)
        {
            currentMotorTorque += nitroForce;
        }

        // Apply motor torque to rear wheels (RWD setup)
        rearLeftWheelCollider.motorTorque = currentMotorTorque;
        rearRightWheelCollider.motorTorque = currentMotorTorque;

        // Front wheels for AWD (optional)
        if (Mathf.Abs(currentMotorTorque) > motorForce * 0.7f)
        {
            frontLeftWheelCollider.motorTorque = currentMotorTorque * 0.3f;
            frontRightWheelCollider.motorTorque = currentMotorTorque * 0.3f;
        }
        else
        {
            frontLeftWheelCollider.motorTorque = 0;
            frontRightWheelCollider.motorTorque = 0;
        }
    }

    private void HandleSteering()
    {
        // Progressive steering based on speed
        float speedFactor = Mathf.Clamp01(1 - (currentSpeed / maxSpeed) * 0.7f);
        currentSteerAngle = maxSteerAngle * horizontalInput * speedFactor;

        // Ensure steering is reset to 0 when no input
        if (Mathf.Abs(horizontalInput) < 0.1f)
        {
            currentSteerAngle = 0f;
        }

        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;

        // Ensure rear wheels never steer
        rearLeftWheelCollider.steerAngle = 0f;
        rearRightWheelCollider.steerAngle = 0f;
    }

    private void HandleBraking()
    {
        float currentBrakeForce = 0f;

        // Regular braking
        if (verticalInput < 0 && currentSpeed > 5f)
        {
            currentBrakeForce = brakeForce * Mathf.Abs(verticalInput);
        }

        // Handbrake
        if (isHandbraking)
        {
            currentBrakeForce = brakeForce * 1.5f;
            // Apply more brake force to rear wheels for handbrake
            rearLeftWheelCollider.brakeTorque = currentBrakeForce * 2f;
            rearRightWheelCollider.brakeTorque = currentBrakeForce * 2f;
            frontLeftWheelCollider.brakeTorque = currentBrakeForce * 0.5f;
            frontRightWheelCollider.brakeTorque = currentBrakeForce * 0.5f;
        }
        else
        {
            // Apply ABS if enabled
            if (enableABS && currentBrakeForce > 0)
            {
                currentBrakeForce = ApplyABS(currentBrakeForce);
            }

            ApplyBrakeForceToAllWheels(currentBrakeForce);
        }
    }

    private float ApplyABS(float brakeForce)
    {
        // Simple ABS simulation
        WheelCollider[] wheels = { frontLeftWheelCollider, frontRightWheelCollider,
                                  rearLeftWheelCollider, rearRightWheelCollider };

        foreach (var wheel in wheels)
        {
            WheelHit hit;
            if (wheel.GetGroundHit(out hit))
            {
                if (Mathf.Abs(hit.forwardSlip) > 0.3f)
                {
                    return brakeForce * 0.7f; // Reduce brake force when slipping
                }
            }
        }

        return brakeForce;
    }

    private void ApplyBrakeForceToAllWheels(float force)
    {
        frontLeftWheelCollider.brakeTorque = force;
        frontRightWheelCollider.brakeTorque = force;
        rearLeftWheelCollider.brakeTorque = force;
        rearRightWheelCollider.brakeTorque = force;
    }

    private void ApplyDownforce()
    {
        // Apply downforce for better high-speed stability
        float speedFactor = carRigidbody.linearVelocity.magnitude / 50f;
        Vector3 downforceVector = -transform.up * downForce * speedFactor;
        carRigidbody.AddForce(downforceVector);
    }

    private void ApplyAntiRoll()
    {
        // Anti-roll bars for better cornering
        ApplyAntiRollBar(frontLeftWheelCollider, frontRightWheelCollider);
        ApplyAntiRollBar(rearLeftWheelCollider, rearRightWheelCollider);
    }

    private void ApplyAntiRollBar(WheelCollider leftWheel, WheelCollider rightWheel)
    {
        WheelHit leftHit, rightHit;
        float leftTravel = 1.0f, rightTravel = 1.0f;

        bool leftGrounded = leftWheel.GetGroundHit(out leftHit);
        bool rightGrounded = rightWheel.GetGroundHit(out rightHit);

        if (leftGrounded)
            leftTravel = (-leftWheel.transform.InverseTransformPoint(leftHit.point).y - leftWheel.radius) / leftWheel.suspensionDistance;

        if (rightGrounded)
            rightTravel = (-rightWheel.transform.InverseTransformPoint(rightHit.point).y - rightWheel.radius) / rightWheel.suspensionDistance;

        float antiRollForce = (leftTravel - rightTravel) * antiRoll;

        if (leftGrounded)
            carRigidbody.AddForceAtPosition(leftWheel.transform.up * -antiRollForce, leftWheel.transform.position);

        if (rightGrounded)
            carRigidbody.AddForceAtPosition(rightWheel.transform.up * antiRollForce, rightWheel.transform.position);
    }

    private void ApplyTractionControl()
    {
        if (!enableTCS) return;

        WheelCollider[] driveWheels = { rearLeftWheelCollider, rearRightWheelCollider };

        foreach (var wheel in driveWheels)
        {
            WheelHit hit;
            if (wheel.GetGroundHit(out hit))
            {
                if (Mathf.Abs(hit.forwardSlip) > 0.4f)
                {
                    wheel.motorTorque *= 0.8f; // Reduce power when slipping
                }
            }
        }
    }

    private void HandleNitro()
    {
        if (isUsingNitro && currentNitro > 0)
        {
            currentNitro -= nitroConsumption * Time.deltaTime;
            currentNitro = Mathf.Max(0, currentNitro);
        }
        else if (!isUsingNitro && currentNitro < 100f)
        {
            currentNitro += nitroRegenRate * Time.deltaTime;
            currentNitro = Mathf.Min(100f, currentNitro);
        }
    }

    private void CalculateEngineRPM()
    {
        // Calculate RPM based on wheel speed and gear ratio
        float wheelRPM = (rearLeftWheelCollider.rpm + rearRightWheelCollider.rpm) / 2f;
        engineRPM = Mathf.Abs(wheelRPM) * 10f + (Mathf.Abs(verticalInput) * 1000f);
        engineRPM = Mathf.Clamp(engineRPM, 800f, 7000f);
    }

    private void UpdateWheelPoses()
    {
        UpdateWheelPose(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateWheelPose(frontRightWheelCollider, frontRightWheelTransform);
        UpdateWheelPose(rearLeftWheelCollider, rearLeftWheelTransform);
        UpdateWheelPose(rearRightWheelCollider, rearRightWheelTransform);
    }

    private void UpdateWheelPose(WheelCollider collider, Transform wheelTransform)
    {
        if (collider == null || wheelTransform == null) return;

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);

        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    private void UpdateEngineAudio()
    {
        if (engineAudioSource == null) return;

        float rpmRatio = engineRPM / 7000f;
        engineAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, rpmRatio);

        if (!engineAudioSource.isPlaying)
        {
            if (engineIdleClip != null)
                engineAudioSource.clip = engineIdleClip;
            engineAudioSource.Play();
        }
    }

    private void UpdateUI()
    {
        // This method can be extended to update UI elements
        // For now, it just provides debug information
    }

    // Public methods for external access
    public float GetCurrentSpeed()
    {
        return currentSpeed;
    }

    public float GetEngineRPM()
    {
        return engineRPM;
    }

    public float GetNitroLevel()
    {
        return currentNitro;
    }

    public bool IsHandbraking()
    {
        return isHandbraking;
    }

    public void ResetCar()
    {
        transform.rotation = Quaternion.identity;
        carRigidbody.linearVelocity = Vector3.zero;
        carRigidbody.angularVelocity = Vector3.zero;
        currentNitro = 100f;

        // Reset all wheel forces
        frontLeftWheelCollider.motorTorque = 0;
        frontRightWheelCollider.motorTorque = 0;
        rearLeftWheelCollider.motorTorque = 0;
        rearRightWheelCollider.motorTorque = 0;

        frontLeftWheelCollider.brakeTorque = 0;
        frontRightWheelCollider.brakeTorque = 0;
        rearLeftWheelCollider.brakeTorque = 0;
        rearRightWheelCollider.brakeTorque = 0;

        frontLeftWheelCollider.steerAngle = 0;
        frontRightWheelCollider.steerAngle = 0;
        rearLeftWheelCollider.steerAngle = 0;
        rearRightWheelCollider.steerAngle = 0;
    }

    // Add this method to check for alignment issues
    public void DiagnoseAlignment()
    {
        Debug.Log($"Center of Mass: {carRigidbody.centerOfMass}");
        Debug.Log($"Car Position: {transform.position}");
        Debug.Log($"Car Rotation: {transform.rotation.eulerAngles}");

        WheelCollider[] wheels = { frontLeftWheelCollider, frontRightWheelCollider,
                                  rearLeftWheelCollider, rearRightWheelCollider };
        string[] wheelNames = { "Front Left", "Front Right", "Rear Left", "Rear Right" };

        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] != null)
            {
                Vector3 localPos = transform.InverseTransformPoint(wheels[i].transform.position);
                Debug.Log($"{wheelNames[i]} Wheel Local Position: {localPos}");
                Debug.Log($"{wheelNames[i]} Wheel Steer Angle: {wheels[i].steerAngle}");
            }
        }
    }

    // Gizmos for debugging
    private void OnDrawGizmos()
    {
        if (centerOfMass != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(centerOfMass.localPosition), 0.1f);
        }
    }
}