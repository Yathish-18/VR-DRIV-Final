using UnityEngine;

public class KeyboardCarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeft;
    public WheelCollider frontRight;
    public WheelCollider rearLeft;
    public WheelCollider rearRight;

    [Header("Car Settings")]
    public float[] gearRatios = { 0f, 500f, 1000f, 1500f, 2000f, 2500f }; // Gear 0 to 5
    public float brakeForce = 3000f;

    private int currentGear = 0;
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // Clutch must be pressed to change gear
        if (Input.GetKey(KeyCode.LeftShift))
        {
            if (Input.GetKeyDown(KeyCode.Alpha0)) currentGear = 0;
            if (Input.GetKeyDown(KeyCode.Alpha1)) currentGear = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) currentGear = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) currentGear = 3;
            if (Input.GetKeyDown(KeyCode.Alpha4)) currentGear = 4;
            if (Input.GetKeyDown(KeyCode.Alpha5)) currentGear = 5;
        }
    }

    void FixedUpdate()
    {
        float accelerationInput = Input.GetKey(KeyCode.W) ? 1f : 0f;
        float brakeInput = Input.GetKey(KeyCode.S) ? 1f : 0f;
        bool clutchPressed = Input.GetKey(KeyCode.LeftShift);

        // Apply motor torque only if clutch is NOT pressed and gear > 0
        if (!clutchPressed && currentGear > 0)
        {
            float torque = gearRatios[currentGear] * accelerationInput;
            ApplyMotorTorque(torque);
        }
        else
        {
            ApplyMotorTorque(0);
        }

        // Apply brake
        ApplyBrake(brakeInput * brakeForce);
    }

    void ApplyMotorTorque(float torque)
    {
        frontLeft.motorTorque = torque;
        frontRight.motorTorque = torque;
    }

    void ApplyBrake(float brake)
    {
        frontLeft.brakeTorque = brake;
        frontRight.brakeTorque = brake;
        rearLeft.brakeTorque = brake;
        rearRight.brakeTorque = brake;
    }
}
