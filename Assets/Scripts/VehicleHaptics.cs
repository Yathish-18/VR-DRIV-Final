using UnityEngine;
using static LogitechGSDK;  // ✅ Use static because LogitechGSDK is a class

[RequireComponent(typeof(G29VehicleInput))]
public class VehicleHaptics : MonoBehaviour
{
    private G29VehicleInput vehicle;

    void Start()
    {
        vehicle = GetComponent<G29VehicleInput>();

        // ✅ Init Logitech SDK
        if (!LogiSteeringInitialize(false))
            Debug.LogError("Logitech Wheel NOT initialized!");

        Debug.Log("Logitech Wheel Connected: " + LogiIsConnected(0));
    }

    void Update()
    {
        if (LogiUpdate() && LogiIsConnected(0))
        {
            ApplyForces();
        }
    }

    void ApplyForces()
    {
        // ✅ Center spring force (keeps the wheel centered but allows steering)
        LogiPlaySpringForce(0, 0, 50, 50);

        // ✅ Accelerator gives a light damper (resistance)
        if (vehicle.AcceleratorValue > 0.1f)
        {
            LogiPlayDamperForce(0, 20); // Light resistance when accelerating
        }

        // ✅ Brake pedal adds strong resistance
        if (vehicle.BrakeValue > 0.1f)
        {
            int brakeStrength = Mathf.RoundToInt(vehicle.BrakeValue * 100f);
            LogiPlayDamperForce(0, brakeStrength); // Stronger as you press harder
        }

        // ✅ Steering resistance (stronger when turning hard)
        float steer = Mathf.Abs(vehicle.SteeringValue);
        if (steer > 0.1f)
        {
            int strength = Mathf.RoundToInt(steer * 60f);
            LogiPlayConstantForce(0, strength);
        }
        else
        {
            LogiStopConstantForce(0);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log("Collision detected with " + collision.gameObject.name);

        // ✅ Short vibration when hitting something
        LogiPlayDamperForce(0, 100);
        Invoke(nameof(StopForces), 0.5f);
    }

    void StopForces()
    {
        LogiStopDamperForce(0);
        LogiStopConstantForce(0);
    }

    void OnApplicationQuit()
    {
        LogiSteeringShutdown();
    }
}
