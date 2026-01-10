using UnityEngine;
using NWH.VehiclePhysics2.Input;

[DisallowMultipleComponent]
public class SteeringWheelVisualizer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Optional upright reference transform (e.g. driver capsule or steering column root).")]
    [SerializeField] private Transform capsule;

    [Tooltip("3D steering wheel model that will visually rotate.")]
    [SerializeField] private Transform steeringWheel;

    [Header("Input Source")]
    [Tooltip("SteeringWheelInputProvider1 that reads Logitech G29 input.")]
    [SerializeField] private SteeringWheelInputProvider1 inputProvider;

    [Header("Visualization Settings")]
    [Tooltip("Maximum visual steering angle in degrees. Typically matches wheel rotation range (e.g. 450, 540).")]
    [SerializeField] private float maxSteeringAngle = 450f;

    [Tooltip("Local axis around which the steering wheel rotates.")]
    [SerializeField] private Vector3 steeringLocalAxis = new Vector3(0f, 0f, -1f);

    [Tooltip("Smoothing time for visual rotation. Set to 0 for instant response.")]
    [SerializeField] private float smoothing = 0.05f;

    private float _currentAngle;
    private float _targetAngle;
    private float _angleVelocity;

    #region Unity Lifecycle

    private void Reset()
    {
        if (inputProvider == null)
        {
            inputProvider = FindObjectOfType<SteeringWheelInputProvider1>();
        }

        if (steeringWheel == null)
        {
            // Try to auto-detect a child named "SteeringWheel" if not assigned
            Transform found = transform.Find("SteeringWheel");
            if (found != null)
            {
                steeringWheel = found;
            }
        }
    }

    private void Awake()
    {
        if (inputProvider == null)
        {
            inputProvider = FindObjectOfType<SteeringWheelInputProvider1>();
        }
    }

    private void Update()
    {
        if (steeringWheel == null || inputProvider == null)
        {
            return;
        }

        float steeringInput = Mathf.Clamp(inputProvider.Steering(), -1f, 1f);
        _targetAngle = steeringInput * maxSteeringAngle;

        if (smoothing > 0f)
        {
            _currentAngle = Mathf.SmoothDamp(_currentAngle, _targetAngle, ref _angleVelocity, smoothing);
        }
        else
        {
            _currentAngle = _targetAngle;
        }

        ApplyRotation(_currentAngle);
    }

    #endregion

    #region Rotation Logic

    private void ApplyRotation(float angle)
    {
        Quaternion steeringRotation = Quaternion.AngleAxis(angle, steeringLocalAxis.normalized);

        if (capsule != null)
        {
            capsule.localRotation = Quaternion.Euler(0f, angle, 0f);
            steeringWheel.localRotation = capsule.localRotation * steeringRotation;
        }
        else
        {
            steeringWheel.localRotation = steeringRotation;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// Manually sets the visual steering angle in degrees.
    /// </summary>
    public void SetVisualAngle(float angle)
    {
        _targetAngle = Mathf.Clamp(angle, -maxSteeringAngle, maxSteeringAngle);
        _currentAngle = _targetAngle;
        ApplyRotation(_currentAngle);
    }

    /// <summary>
    /// Returns the current visual steering angle in degrees.
    /// </summary>
    public float GetCurrentVisualAngle()
    {
        return _currentAngle;
    }

    #endregion
}
