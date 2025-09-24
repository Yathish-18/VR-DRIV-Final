using UnityEngine;

public class SteeringWheelRotation : MonoBehaviour
{
    [Header("Steering Wheel")]
    [SerializeField] private Transform steeringWheelTransform;
    [SerializeField] private float maxRotationAngle = 450f;  // Max degrees
    [SerializeField] private float rotationSpeed = 15f;      // How fast it rotates

    [Header("Car Controller")]
    [SerializeField] private Car_Controller carController;   // Your car controller

    private float currentRotation = 0f;

    void Start()
    {
        if (steeringWheelTransform == null)
            steeringWheelTransform = transform;

        if (carController == null)
            carController = FindObjectOfType<Car_Controller>();
    }

    void Update()
    {
        float steerInput = 0f;

        // Get steering input directly from your CarController
        if (carController != null)
        {
            steerInput = carController.SteerInput;  // Uses your SteerInput property
        }
        else
        {
            // Fallback to keyboard if no CarController
            steerInput = Input.GetAxis("Horizontal");
        }

        // Calculate target rotation - FIXED: Added negative sign to reverse direction
        float targetRotation = -steerInput * maxRotationAngle;

        // Smooth rotation
        currentRotation = Mathf.Lerp(currentRotation, targetRotation, rotationSpeed * Time.deltaTime);

        // Apply rotation (Z-axis for most steering wheels)
        steeringWheelTransform.localRotation = Quaternion.Euler(0f, 0f, currentRotation);
    }
}
