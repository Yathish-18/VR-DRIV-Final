using UnityEngine;
using NWH.VehiclePhysics2;
using NWH.VehiclePhysics2.Powertrain;

public class LogitechClutchGearControl : MonoBehaviour
{
    [Header("Clutch Settings")]
    [Range(0f, 1f)]
    public float clutchDeadZone = 0.2f; // Clutch must be below this for shifting

    [Header("Gear Shift Settings")]
    public float shiftCooldown = 0.3f;
    public bool requireClutchForShifting = true;

    [Header("Audio Feedback")]
    public AudioSource gearGrindAudio;
    public AudioClip gearGrindClip;

    private VehicleController vehicleController;
    private ClutchComponent clutchComponent;
    private TransmissionComponent transmissionComponent;
    private float lastShiftTime = 0f;

    // Track previous input states to detect key presses
    private bool wasShiftUpPressed = false;
    private bool wasShiftDownPressed = false;

    void Start()
    {
        vehicleController = GetComponent<VehicleController>();

        if (vehicleController == null)
        {
            Debug.LogError("VehicleController not found!");
            return;
        }

        clutchComponent = vehicleController.powertrain.clutch;
        transmissionComponent = vehicleController.powertrain.transmission;

        if (clutchComponent == null || transmissionComponent == null)
        {
            Debug.LogError("Required powertrain components not found!");
            return;
        }

        Debug.Log("Logitech Clutch Gear Control initialized successfully");
    }

    void Update()
    {
        print(vehicleController.input.Clutch);
        HandleGearShifting();
    }

    void HandleGearShifting()
    {
        if (vehicleController == null || vehicleController.input == null) return;

        // Check shift cooldown
        if (Time.time - lastShiftTime < shiftCooldown) return;

        // Get current shift inputs
        bool shiftUpInput = vehicleController.input.ShiftUp;
        bool shiftDownInput = vehicleController.input.ShiftDown;

        // Detect key press (not held)
        bool shiftUpPressed = shiftUpInput && !wasShiftUpPressed;
        bool shiftDownPressed = shiftDownInput && !wasShiftDownPressed;

        // Update previous states
        wasShiftUpPressed = shiftUpInput;
        wasShiftDownPressed = shiftDownInput;

        // Handle shift up
        if (shiftUpPressed)
        {
            if (!requireClutchForShifting || CanShiftGear())
            {
                ShiftUp();
                lastShiftTime = Time.time;
            }
            else
            {
                PlayGearGrindSound();
                Debug.Log($"Cannot shift up! Clutch engagement: {clutchComponent.Engagement:F2} (must be below {clutchDeadZone:F2})");
            }
        }

        // Handle shift down
        if (shiftDownPressed)
        {
            if (!requireClutchForShifting || CanShiftGear())
            {
                ShiftDown();
                lastShiftTime = Time.time;
            }
            else
            {
                PlayGearGrindSound();
                Debug.Log($"Cannot shift down! Clutch engagement: {clutchComponent.Engagement:F2} (must be below {clutchDeadZone:F2})");
            }
        }
    }

    bool CanShiftGear()
    {
        // Check if clutch is sufficiently disengaged
        return clutchComponent.Engagement <= clutchDeadZone;
    }

    void ShiftUp()
    {
        if (transmissionComponent.gearIndex < transmissionComponent.gears.Count - 1)
        {
            transmissionComponent.ShiftInto(transmissionComponent.gearIndex + 1);
           
        }
    }

    void ShiftDown()
    {
        if (transmissionComponent.gears.Count > -1) // Include reverse
        {
            transmissionComponent.ShiftInto(transmissionComponent.gearIndex-1);
            
        }
    }

    void PlayGearGrindSound()
    {
        if (gearGrindAudio != null && gearGrindClip != null)
        {
            gearGrindAudio.PlayOneShot(gearGrindClip);
        }
    }

    // Public methods for runtime control
    public void SetClutchDeadZone(float value)
    {
        clutchDeadZone = Mathf.Clamp01(value);
    }

    public void ToggleClutchRequirement(bool enabled)
    {
        requireClutchForShifting = enabled;
    }

    // Debug display
    void OnGUI()
    {
        if (vehicleController == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("=== Clutch Gear Control ===");
        GUILayout.Label($"Clutch Input: {vehicleController.input.Clutch:F2}");
        GUILayout.Label($"Clutch Engagement: {clutchComponent?.Engagement:F2}");
        GUILayout.Label($"Can Shift: {CanShiftGear()}");
        GUILayout.Label($"Current Gear: {transmissionComponent?.gearIndex}");
        GUILayout.Label($"Shift Up: {vehicleController.input.ShiftUp}");
        GUILayout.Label($"Shift Down: {vehicleController.input.ShiftDown}");

        GUILayout.Space(10);
        requireClutchForShifting = GUILayout.Toggle(requireClutchForShifting, "Require Clutch");

        GUILayout.EndArea();
    }
}
