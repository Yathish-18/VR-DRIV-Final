using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Speedometer : MonoBehaviour
{
    [Header("Target Settings")]
    public Rigidbody target;
    public float maxSpeed = 200.0f; // Maximum speed in KM/H

    [Header("Needle Settings")]
    public Transform needle;
    public Vector3 minSpeedRotation = new Vector3(0, 0, -90f); // Rotation at 0 speed
    public Vector3 maxSpeedRotation = new Vector3(0, 0, 90f);  // Rotation at max speed
    public bool useLocalRotation = true;
    public bool reverseRotation = false; // Check this to reverse rotation direction
    public bool useQuaternionLerp = true; // Toggle between Quaternion.Slerp and Vector3.Lerp

    [Header("UI")]
    public TextMeshProUGUI speedLabel;

    [Header("Performance")]
    public bool smoothNeedle = true;
    public float smoothSpeed = 8.0f;

    // Cached values
    private float currentSpeed;
    private float targetNeedleSpeed;
    private Quaternion minRotationQuat;
    private Quaternion maxRotationQuat;
    private Vector3 lastVelocity;

    // Performance optimization
    private const float SPEED_CONVERSION = 3.6f; // m/s to km/h
    private const float UPDATE_FREQUENCY = 0.02f; // 50Hz update rate
    private float updateTimer;

    void Start()
    {
        // Validation
        ValidateComponents();

        // Pre-calculate quaternions for better performance
        minRotationQuat = Quaternion.Euler(minSpeedRotation);
        maxRotationQuat = Quaternion.Euler(maxSpeedRotation);

        // Initialize values
        if (target != null)
        {
            lastVelocity = target.linearVelocity;
        }
    }

    void Update()
    {
        // Throttle updates for performance
        updateTimer += Time.deltaTime;
        if (updateTimer >= UPDATE_FREQUENCY)
        {
            UpdateSpeedometer();
            updateTimer = 0f;
        }

        // Always update needle for smooth movement
        if (needle != null)
        {
            UpdateNeedleRotation();
        }
    }

    private void UpdateSpeedometer()
    {
        if (target == null) return;

        // Calculate speed with optimization check
        Vector3 currentVelocity = target.linearVelocity;
        if (currentVelocity != lastVelocity)
        {
            currentSpeed = currentVelocity.magnitude * SPEED_CONVERSION;
            currentSpeed = Mathf.Clamp(currentSpeed, 0f, maxSpeed);
            lastVelocity = currentVelocity;

            // Update UI
            UpdateSpeedDisplay();
        }
    }

    private void UpdateSpeedDisplay()
    {
        if (speedLabel != null)
        {
            speedLabel.text = Mathf.RoundToInt(currentSpeed).ToString() + " km/h";
        }
    }

    private void UpdateNeedleRotation()
    {
        // Smooth or immediate needle movement
        if (smoothNeedle)
        {
            targetNeedleSpeed = Mathf.Lerp(targetNeedleSpeed, currentSpeed, smoothSpeed * Time.deltaTime);
        }
        else
        {
            targetNeedleSpeed = currentSpeed;
        }

        // Calculate rotation using pre-calculated quaternions
        float speedRatio = maxSpeed > 0 ? Mathf.Clamp01(targetNeedleSpeed / maxSpeed) : 0f;

        // Apply reverse rotation by inverting the speed ratio
        if (reverseRotation)
        {
            speedRatio = 1f - speedRatio;
        }

        Quaternion targetRotation;

        // Choose between Quaternion.Slerp and Vector3.Lerp
        if (useQuaternionLerp)
        {
            // Method 1: Quaternion interpolation (spherical, smooth)
            targetRotation = Quaternion.Slerp(minRotationQuat, maxRotationQuat, speedRatio);
        }
        else
        {
            // Method 2: Vector3 interpolation (linear, direct)
            Vector3 targetEuler = Vector3.Lerp(minSpeedRotation, maxSpeedRotation, speedRatio);
            targetRotation = Quaternion.Euler(targetEuler);
        }

        // Apply rotation
        if (useLocalRotation)
        {
            needle.localRotation = targetRotation;
        }
        else
        {
            needle.rotation = targetRotation;
        }
    }

    private void ValidateComponents()
    {
        if (target == null)
        {
            Debug.LogError($"[Speedometer] Target Rigidbody not assigned on {gameObject.name}!");
        }

        if (needle == null)
        {
            Debug.LogWarning($"[Speedometer] Needle Transform not assigned on {gameObject.name}!");
        }

        if (maxSpeed <= 0)
        {
            Debug.LogWarning($"[Speedometer] MaxSpeed should be greater than 0 on {gameObject.name}! Setting to 200.");
            maxSpeed = 200.0f;
        }
    }

    // Public API
    public float GetCurrentSpeed() => currentSpeed;
    public float GetSpeedRatio() => maxSpeed > 0 ? currentSpeed / maxSpeed : 0f;
    public void SetMaxSpeed(float newMaxSpeed)
    {
        if (newMaxSpeed > 0) maxSpeed = newMaxSpeed;
    }

    // Editor helper - call this to test needle positions
    [ContextMenu("Test Min Rotation")]
    public void TestMinRotation()
    {
        if (needle != null)
        {
            if (useLocalRotation)
                needle.localRotation = Quaternion.Euler(minSpeedRotation);
            else
                needle.rotation = Quaternion.Euler(minSpeedRotation);
        }
    }

    [ContextMenu("Test Max Rotation")]
    public void TestMaxRotation()
    {
        if (needle != null)
        {
            if (useLocalRotation)
                needle.localRotation = Quaternion.Euler(maxSpeedRotation);
            else
                needle.rotation = Quaternion.Euler(maxSpeedRotation);
        }
    }
}