using UnityEngine;
using UnityEngine.InputSystem;

public class SteeringWheelVisualizer : MonoBehaviour
{
    [Header("References")]
    public Transform capsule;       // Capsule reference (upright)
    public Transform steeringWheel; // Visual wheel that will follow the capsule's rotation

    [Header("Input")]
    public InputActionAsset inputAsset;
    public float maxSteeringAngle = 450f;

    private InputAction steerAction;

    void Start()
    {
        var drivingMap = inputAsset.FindActionMap("Driving");
        steerAction = drivingMap.FindAction("Steer");
        drivingMap.Enable();
    }

    void Update()
    {
        float steerInput = steerAction.ReadValue<float>(); // -1 to 1
        float targetAngle = steerInput * maxSteeringAngle;

        // Apply rotation to capsule (around Y-axis)
        capsule.localRotation = Quaternion.Euler(0f, targetAngle, 0f);

        // Make visual wheel follow capsule's Y-rotation only
        Vector3 visualRot = steeringWheel.localEulerAngles;
        visualRot.y = capsule.localEulerAngles.y;
        steeringWheel.localEulerAngles = visualRot;
    }
}
