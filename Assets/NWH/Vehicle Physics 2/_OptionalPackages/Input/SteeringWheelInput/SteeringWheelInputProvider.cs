using NWH.Common.Vehicles;
using UnityEngine;
using UnityEngine.Serialization;
using System.Text;
using System.Collections.Generic;
using Logitech;
#if UNITY_EDITOR
using NWH.NUI;
using UnityEditor;
#endif

namespace NWH.VehiclePhysics2.Input
{
    /// <summary>
    /// Handles input from steeling wheels such as Logitech, Thrustmaster, etc.
    /// Also calculates force feedback.
    /// </summary>
    public class SteeringWheelInputProvider : VehicleInputProviderBase
    {
        public bool[] buttonDown = new bool[128];
        public bool[] buttonPressed = new bool[128];
        public bool[] buttonWasPressed = new bool[128];

        public enum Axis
        {
            XPosition,
            YPosition,
            ZPosition,
            XRotatation,
            YRotation,
            ZRotation,
            rglSlider0,
            rglSlider1,
            rglSlider2,
            rglSlider3,
            rglASlider0,
            rglASlider1,
            rglASlider2,
            rglASlider3,
            rglFSlider0,
            rglFSlider1,
            rglFSlider2,
            rglFSlider3,
            rglVSlider0,
            rglVSlider1,
            rglVSlider2,
            rglVSlider3,
            lArx,
            lAry,
            lArz,
            lAx,
            lAy,
            lAz,
            lFRx,
            lFRy,
            lFRz,
            lFx,
            lFy,
            lFz,
            lVRx,
            lVRy,
            lVRz,
            lVx,
            lVy,
            lVz,
            None
        }

        /// <summary>
        /// Should all steering filtering and smoothing be ignored?
        /// </summary>
        [UnityEngine.Tooltip("Should all steering filtering and smoothing be ignored?")]
        public bool useDirectInput = true;

        /// <summary>
        /// Number by which the input steering value received from the wheel is multiplied.
        /// </summary>
        [UnityEngine.Tooltip("Number by which the input steering value received from the wheel is multiplied.")]
        public float steeringSensitivity = 1f;

        /// <summary>
        /// If enabled gears 3 and 4 will be used for D/N/R shifting. Requires Automatic Transmission Reverse Type 
        /// to be set to 'Require Shift Input' and Transmission Type to 'Automatic' or 'Automatic Sequential'.
        /// </summary>
        [UnityEngine.Tooltip("If enabled gears 3 and 4 will be used for D/N/R shifting. Requires Automatic Transmission Reverse Type " +
            "to be set to 'Require Shift Input' and Transmission Type to 'Automatic' or 'Automatic Sequential'.")]
        public bool hShifterUse34asDR = false;

        /// <summary>
        /// Target VehicleController.
        /// </summary>
        [UnityEngine.Tooltip("Target VehicleController.")]
        public VehicleController vehicleController;

        /// <summary>
        /// Maximum wheel force. This is a hardware settings for the used device.
        /// </summary>
        [Range(0, 100)] public int maximumWheelForce = 100;

        /// <summary>
        /// Multiplier by which the overall force is multiplied. Set to 0 to disable force feedback.
        /// </summary>
        [UnityEngine.Tooltip("Multiplier by which the overall force is multiplied. Set to 0 to disable force feedback.")]
        public float overallEffectStrength = 1f;

        /// <summary>
        /// Smoothing of the forces. Set to 0 for most direct feedback.
        /// </summary>
        [Range(0, 0.1f)] public float smoothing = 0f;

        /// <summary>
        /// Range of rotation of the wheel in degrees. Most wheels are capable of 900 degrees
        /// but this will be too slow for most games (except for truck simulators).
        /// 540 is usually used in sim racing.
        /// </summary>
        [FormerlySerializedAs("wheelRange")]
        [UnityEngine.Tooltip("Range of rotation of the wheel in degrees. Most wheels are capable of 900 degrees\r\nbut this will be too slow for most games (except for truck simulators).\r\n540 is usually used in sim racing.")]
        public int wheelRotationRange = 540;

        /// <summary>
        /// Fix non-linear input from LogitechSDK.
        /// </summary>
        [UnityEngine.Tooltip("Fix non-linear input from LogitechSDK.")]
        public bool linearizeSDKInput = true;

        /// <summary>
        /// Curve that shows how the self aligning torque acts in relation to wheel slip.
        /// Vertical axis is force coefficient, horizontal axis is slip.
        /// </summary>
        [UnityEngine.Tooltip(
            "Curve that shows how the self aligning torque acts in relation to wheel slip.\r\nVertical axis is force coefficient, horizontal axis is slip.")]
        public AnimationCurve slipSATCurve = new AnimationCurve(
            new Keyframe(0, 0, -0.9f, 25f),
            new Keyframe(0.07f, 1),
            new Keyframe(0.16f, 0.93f, -1f, -1f),
            new Keyframe(1f, 0.2f)
        );

        /// <summary>
        /// Maximum force that can be achieved as a result of self aligning torque.
        /// </summary>
        [UnityEngine.Tooltip("Maximum force that can be achieved as a result of self aligning torque.")]
        public float maxSatForce = 80;

        /// <summary>
        /// Slip is multiplied by this value before it is used to evaluate slipSATCurve.
        /// Use to adjust the point at which the wheel will begin to loosen as a result of wheel skid.
        /// </summary>
        [UnityEngine.Tooltip("Slip is multiplied by this value before it is used to evaluate slipSATCurve.\r\nUse to adjust the point at which the wheel will begin to loosen as a result of wheel skid.")]
        public float slipMultiplier = 4.6f;

        /// <summary>
        /// Friction when vehicle is stationary or near stationary.
        /// </summary>
        [UnityEngine.Tooltip("Friction when vehicle is stationary or near stationary.")]
        public float lowSpeedFriction = 70f;

        /// <summary>
        /// Friction when vehicle is moving.
        /// </summary>
        [UnityEngine.Tooltip("Friction when vehicle is moving.")]
        public float friction = 16f;

        /// <summary>
        /// Strength of centering force (the tendency of the steering wheel to center itself).
        /// Also affects the feel of bumps as the steering center moves based on suspension compression (e.g. when
        /// right wheel goes over a bump on the road the steering wheel center moves to the left).
        /// </summary>
        [UnityEngine.Tooltip("Strength of centering force (the tendency of the steering wheel to center itself).\r\nAlso affects the feel of bumps as the steering center moves based on suspension compression (e.g. when\r\nright wheel goes over a bump on the road the steering wheel center moves to the left).")]
        public float centeringForceStrength = 60;

        /// <summary>
        /// How much the steering center will move based on difference on compression of suspension on the
        /// left and right side. 
        /// </summary>
        [Range(0, 1)] public float centerPositionDrift = 0.4f;

        /// <summary>
        /// Axis resolution of the wheel's ADC.
        /// </summary>
        [UnityEngine.Tooltip("Axis resolution of the wheel's ADC.")]
        public int axisResolution = 65536;

        /// <summary>
        /// Flips the sign on the steering input.
        /// </summary>
        [UnityEngine.Tooltip("Flips the sign on the steering input.")]
        public bool flipSteeringInput = false;

        /// <summary>
        /// Flips the sign on the throttle input.
        /// </summary>
        [UnityEngine.Tooltip("Flips the sign on the throttle input.")]
        public bool flipThrottleInput = true;

        /// <summary>
        /// Flips the sign on the brake input.
        /// </summary>
        [UnityEngine.Tooltip("Flips the sign on the brake input.")]
        public bool flipBrakeInput = true;

        /// <summary>
        /// Flips the sign on the clutch input.
        /// </summary>
        [UnityEngine.Tooltip("Flips the sign on the clutch input.")]
        public bool flipClutchInput = true;

        /// <summary>
        /// Flips the sign on the handbrake input.
        /// </summary>
        [UnityEngine.Tooltip("Flips the sign on the handbrake input.")]
        public bool flipHandbrakeInput = true;

        /// <summary>
        /// Determines which wheel axis will be used for steering.
        /// </summary>
        [UnityEngine.Tooltip("Determines which wheel axis will be used for steering.")]
        public Axis steeringAxis = Axis.XPosition;

        /// <summary>
        /// Determines which wheel axis will be used for throttle.
        /// </summary>
        [UnityEngine.Tooltip("Determines which wheel axis will be used for throttle.")]
        public Axis throttleAxis = Axis.YPosition;

        public bool throttleZeroToOne = true;

        /// <summary>
        /// Determines which wheel axis will be used for braking.
        /// </summary>
        [UnityEngine.Tooltip("Determines which wheel axis will be used for braking.")]
        public Axis brakeAxis = Axis.ZRotation;

        public bool brakeZeroToOne = true;

        /// <summary>
        /// Determines which wheel axis will be used for clutch.
        /// </summary>
        [UnityEngine.Tooltip("Determines which wheel axis will be used for clutch.")]
        public Axis clutchAxis = Axis.ZPosition;

        public bool clutchZeroToOne = true;

        /// <summary>
        /// Determines which wheel axis will be used for steering.
        /// If there is no analog axis for handbrake, handbrakeButton mapping can be used instead.
        /// </summary>
        [UnityEngine.Tooltip("Determines which wheel axis will be used for steering.\r\nIf there is no analog axis for handbrake, handbrakeButton mapping can be used instead.")]
        public Axis handbrakeAxis = Axis.None;

        public bool handbrakeZeroToOne = true;

        /// <summary>
        /// Primary shift up button.
        /// </summary>
        [UnityEngine.Tooltip("Primary shift up button.")]
        public int shiftUpButton = 12;

        /// <summary>
        /// Primary shift down button.
        /// </summary>
        [UnityEngine.Tooltip("Primary shift down button.")]
        public int shiftDownButton = 13;

        /// <summary>
        /// Alternative shift up button.
        /// To be used when there is both a sequential stick shifter and paddles.
        /// </summary>
        [UnityEngine.Tooltip("Alternative shift up button.\r\nTo be used when there is both a sequential stick shifter and paddles.")]
        public int altShiftUpButton = 4;

        /// <summary>
        /// Alternative shift down button.
        /// To be used when there is both a sequential stick shifter and paddles.
        /// </summary>
        [UnityEngine.Tooltip("Alternative shift down button.\r\nTo be used when there is both a sequential stick shifter and paddles.")]
        public int altShiftDownButton = 5;

        /// <summary>
        /// Button used to shift into reverse gear.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into reverse gear.")]
        public int shiftIntoReverseButton = -1;

        /// <summary>
        /// Button used to shift into neutral gear.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into neutral gear.")]
        public int shiftIntoNeutralButton = -1;

        /// <summary>
        /// Button used to shift into 1st gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 1st gear. Set to -1 to disable.")]

        public int shiftInto1stButton = -1;

        /// <summary>
        /// Button used to shift into 2nd gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 2nd gear. Set to -1 to disable.")]
        public int shiftInto2ndButton = -1;

        /// <summary>
        /// Button used to shift into 3rd gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 3rd gear. Set to -1 to disable.")]
        public int shiftInto3rdButton = -1;

        /// <summary>
        /// Button used to shift into 4th gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 4th gear. Set to -1 to disable.")]
        public int shiftInto4thButton = -1;

        /// <summary>
        /// Button used to shift into 5th gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 5th gear. Set to -1 to disable.")]
        public int shiftInto5thButton = -1;

        /// <summary>
        /// Button used to shift into 6th gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 6th gear. Set to -1 to disable.")]
        public int shiftInto6thButton = -1;

        /// <summary>
        /// Button used to shift into 7th gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 7th gear. Set to -1 to disable.")]
        public int shiftInto7thButton = -1;

        /// <summary>
        /// Button used to shift into 8th gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 8th gear. Set to -1 to disable.")]
        public int shiftInto8thButton = -1;

        /// <summary>
        /// Button used to shift into 9th gear.
        /// Set to -1 to disable.
        /// </summary>
        [UnityEngine.Tooltip("Button used to shift into 9th gear. Set to -1 to disable.")]
        public int shiftInto9thButton = -1;

        /// <summary>
        /// Button used to trigger handbrake.
        /// For analog input use handbrakeAxis mapping instead.
        /// </summary>
        [UnityEngine.Tooltip("Button used to trigger handbrake.\r\nFor analog input use handbrakeAxis mapping instead.")]
        public int handbrakeButton = -1;

        /// <summary>
        /// Index of the input device that should be used. 
        /// Leave at -1 for automatic detection.
        /// </summary>
        public int deviceIndex = -1;

        /// <summary>
        /// If the automatic device search is enabled (deviceIndex = -1),
        /// should only force-feedback devices be considered?
        /// </summary>
        public bool onlyFFBDevices = true;

        /// <summary>
        /// If the automatic device search is enabled (deviceIndex = -1),
        /// should only the devices containing a string from the list be considered?
        /// </summary>
        public List<string> deviceNameContainsWhitelist = new List<string>();

        /// <summary>
        /// Should the automatic device search show debug info in the console about the found devices.
        /// </summary>
        public bool showDeviceDebugInfo = true;

        public string foundDevicesDebugString = "---";


        // Inputs
        [SerializeField][Range(-1, 1)] private float _steeringInput;
        [SerializeField][Range(0, 1)] private float _throttleInput;
        [SerializeField][Range(0, 1)] private float _brakeInput;
        [SerializeField][Range(0, 1)] private float _clutchInput;
        [SerializeField][Range(0, 1)] private float _handbrakeInput = 0;
        [SerializeField] private int _shiftIntoInput = -999;
        [SerializeField] private bool _shiftUpInput = false;
        [SerializeField] private bool _shiftDownInput = false;
        public float throttleDeadzone = 0.02f;
        public float brakeDeadzone = 0.02f;
        public float clutchDeadzone = 0.02f;
        public float handbrakeDeadzone = 0.02f;
        public float steeringDeadzone = 0.00f;

        //Forces
        [SerializeField][Range(0, 100)] private float _lowSpeedFrictionForce;
        [SerializeField][Range(0, 100)] private float _totalForce = 0;
        [SerializeField][Range(0, 100)] private float _satForce;
        [SerializeField][Range(0, 100)] private float _frictionForce;
        [SerializeField][Range(0, 100)] private float _centeringForce;

        private float _centerPosition;
        private float _prevSteering;
        private float _steerVelocity;
        private ForceFeedbackSettings _ffbSettings;
        private WheelUAPI _leftWheel;
        private WheelUAPI _rightWheel;
        private LogitechGSDK.DIJOYSTATE2ENGINES _wheelInput;
        private float _totalForceVelocity;

        // Vehicle-specific coefficients
        private float _overallCoeff = 1f;
        private float _frictionCoeff = 1f;
        private float _lowSpeedFrictionCoeff = 1f;
        private float _satCoeff = 1f;
        private float _centeringCoeff = 1f;

        // Input detection
        StringBuilder _inputDeviceName;


        public override void Awake()
        {
            base.Awake();

            Vehicle.onActiveVehicleChanged.AddListener(HandleActiveVehicleChange);
        }


        void Start()
        {
            LogitechGSDK.LogiSteeringInitialize(false);

            buttonDown = new bool[128];
            buttonWasPressed = new bool[128];
            buttonPressed = new bool[128];
            _inputDeviceName = new StringBuilder(256);
        }


        private void Update()
        {
            // This has to run in Update() as the devices do not get detected by the LogitechSDK until after Start()
            if (deviceIndex < 0)
            {
                deviceIndex = FindDeviceIndex();
                if (deviceIndex >= 0)
                {
                    GetDeviceName(deviceIndex, ref _inputDeviceName);
                    Debug.Log($"Using {_inputDeviceName}. Initializing.");
                    InitializeWheel();
                }
            }

            if (deviceIndex < 0 || !WheelIsConnected)
            {
                return;
            }

            GetWheelInputs();
            SetVehicleInputs();
        }


        void FixedUpdate()
        {
            if (deviceIndex < 0)
            {
                return;
            }

            if (vehicleController == null)
            {
                Debug.Log("VehicleController is not set.");
                return;
            }

            _ffbSettings = vehicleController.GetComponent<ForceFeedbackSettings>();
            if (_ffbSettings == null)
            {
                _overallCoeff = 1f;
                _frictionCoeff = 1f;
                _lowSpeedFrictionCoeff = 1f;
                _satCoeff = 1f;
                _centeringCoeff = 1f;
            }
            else
            {
                _overallCoeff = _ffbSettings.overallCoeff;
                _frictionCoeff = _ffbSettings.frictionCoeff;
                _lowSpeedFrictionCoeff = _ffbSettings.lowSpeedFrictionCoeff;
                _satCoeff = _ffbSettings.satCoeff;
                _centeringCoeff = _ffbSettings.centeringCoeff;
            }

            if (!vehicleController.enabled)
            {
                ResetForce();
                return;
            }

            float newTotalForce = 0;

            if (WheelIsConnected && LogitechGSDK.LogiUpdate())
            {
                vehicleController.steering.useRawInput = useDirectInput;

                _leftWheel = vehicleController.powertrain.wheels[0].wheelUAPI;
                _rightWheel = vehicleController.powertrain.wheels[1].wheelUAPI;

                // Self Aligning Torque
                float leftFactor = _leftWheel.Load / _leftWheel.MaxLoad * _leftWheel.FrictionPreset.BCDE.z;
                float rightFactor = _rightWheel.Load / _rightWheel.MaxLoad * _rightWheel.FrictionPreset.BCDE.z;
                float combinedFactor = leftFactor + rightFactor;
                float totalSlip = _leftWheel.LateralSlip * leftFactor + _rightWheel.LateralSlip * rightFactor;
                float absSlip = totalSlip < 0 ? -totalSlip : totalSlip;
                float slipSign = totalSlip < 0 ? -1f : 1f;
                _satForce = slipSATCurve.Evaluate(absSlip * slipMultiplier) * -slipSign * maxSatForce * combinedFactor * _satCoeff;
                newTotalForce += Mathf.Lerp(0f, _satForce, vehicleController.Speed - 0.4f);

                // Determine target center  position (changes with spring compression)
                _centerPosition = ((_rightWheel.SpringLength / _rightWheel.SpringMaxLength) - (_leftWheel.SpringLength / _leftWheel.SpringMaxLength)) * centerPositionDrift;

                // Calculate centering force
                _centeringForce = (_steeringInput - _centerPosition) * centeringForceStrength * _centeringCoeff;
                newTotalForce += _centeringForce;

                // Low speed friction
                _lowSpeedFrictionForce = Mathf.Lerp(lowSpeedFriction, 0, vehicleController.Speed - 0.2f) * _lowSpeedFrictionCoeff;

                // Friction 
                _frictionForce = friction * _frictionCoeff;

                // Apply friction
                LogitechGSDK.LogiPlayDamperForce(deviceIndex, (int)(_lowSpeedFrictionForce + _frictionForce));

                newTotalForce *= overallEffectStrength * _overallCoeff;
                if (smoothing < 0.001f)
                {
                    _totalForce = newTotalForce;
                }
                else
                {
                    _totalForce = Mathf.SmoothDamp(_totalForce, newTotalForce, ref _totalForceVelocity, smoothing);
                }

                AddForce(_totalForce);

                _prevSteering = _steeringInput;
            }
            else
            {
                vehicleController.steering.useRawInput = false;
            }
        }



        public override void OnDestroy()
        {
            base.OnDestroy();

            _steeringInput = 0;
            _throttleInput = 0;
            _brakeInput = 0;
            _clutchInput = 0;
            _handbrakeInput = 0;
            _shiftIntoInput = -999;
            _shiftUpInput = false;
            _shiftDownInput = false;
            _lowSpeedFrictionForce = 0;
            _totalForce = 0;
            _satForce = 0;
            _frictionForce = 0;
            _centeringForce = 0;
            _centerPosition = 0;

            LogitechGSDK.LogiSteeringShutdown();
        }


        private void Reset()
        {
            slipSATCurve = new AnimationCurve(
                new Keyframe(0, 0, -0.9f, 25f),
                new Keyframe(0.07f, 1),
                new Keyframe(0.16f, 0.93f, -1f, -1f),
                new Keyframe(1f, 0.2f)
            );
        }


        void InitializeWheel()
        {
            if (deviceIndex < 0) return;
            LogitechGSDK.LogiControllerPropertiesData currentProperties =
                new LogitechGSDK.LogiControllerPropertiesData();
            LogitechGSDK.LogiGetCurrentControllerProperties(deviceIndex, ref currentProperties);
            currentProperties.forceEnable = true;
            currentProperties.combinePedals = false;
            currentProperties.gameSettingsEnabled = true;
            currentProperties.defaultSpringEnabled = false;
            currentProperties.defaultSpringGain = 100;
            currentProperties.springGain = 100;
            currentProperties.damperGain = 100;
            currentProperties.overallGain = (int)(maximumWheelForce * 100);
            currentProperties.wheelRange = wheelRotationRange;
            LogitechGSDK.LogiSetPreferredControllerProperties(currentProperties);
        }


        public int FindDeviceIndex()
        {
            for (int i = 0; i < 16; i++)
            {
                if (LogitechGSDK.LogiIsConnected(i))
                {
                    LogitechGSDK.LogiGetFriendlyProductName(i, _inputDeviceName, 256);

                    if (showDeviceDebugInfo)
                    {
                        foundDevicesDebugString += $"{i}: {_inputDeviceName}; ";
                    }

                    Debug.Log("----");
                    Debug.Log($"Found a connected device: {_inputDeviceName}.");

                    if (onlyFFBDevices && !LogitechGSDK.LogiHasForceFeedback(i))
                    {
                        // Only interested in FFB devices, continue if not one.
                        Debug.Log("Device has no FFB support. Only FFB devices option selected. Skipping.");
                        continue;
                    }

                    if (deviceNameContainsWhitelist.Count > 0)
                    {
                        // There are items in the name filter, check that the device name contains a match.
                        Debug.Log($"Checking if device name contains a string from the whitelist.");
                        string inputDeviceString = _inputDeviceName.ToString();
                        foreach (string match in deviceNameContainsWhitelist)
                        {
                            if (inputDeviceString.Contains(match))
                            {
                                return i;
                            }
                        }
                        Debug.Log($"No match found. Skipping.");
                    }
                    else
                    {
                        // No name filters, return the current index.
                        return i;
                    }

                }
            }

            if (showDeviceDebugInfo)
            {
                foundDevicesDebugString = "No devices found.";
            }

            return -1;
        }


        public void GetDeviceName(int index, ref StringBuilder deviceName)
        {
            if (index < 0)
            {
                Debug.Log("No device selected / found.");
                return;
            }

            LogitechGSDK.LogiGetFriendlyProductName(index, deviceName, 256);
        }




        private bool WheelIsConnected
        {
            get { return LogitechGSDK.LogiIsConnected(deviceIndex); }
        }





        private void HandleCollision(Collision collision)
        {
            int strength = (int)(collision.impulse.magnitude /
                     (vehicleController.fixedDeltaTime * vehicleController.vehicleRigidbody.mass * 5f));
            LogitechGSDK.LogiPlayFrontalCollisionForce(deviceIndex, strength);
        }


        private void HandleActiveVehicleChange(Vehicle previousVehicle, Vehicle currentVehicle)
        {
            VehicleController previousVehicleController = previousVehicle as VehicleController;
            if (previousVehicleController != null)
            {
                previousVehicleController.onCollision.RemoveListener(HandleCollision);
            }

            VehicleController currentVehicleController = currentVehicle as VehicleController;
            if (currentVehicleController != null)
            {
                vehicleController = currentVehicleController;
                currentVehicleController.onCollision.AddListener(HandleCollision);
            }
        }



void SetVehicleInputs()
        {
            // Shift Up
            _shiftUpInput = GetButtonDown(shiftUpButton) ||
                                                     GetButtonDown(altShiftUpButton);

            // Shift Down
            _shiftDownInput = GetButtonDown(shiftDownButton) ||
                                                     GetButtonDown(altShiftDownButton);

            _shiftIntoInput = hShifterUse34asDR ? 0 : -999;
            if (hShifterUse34asDR && (GetButtonPressed(shiftInto3rdButton) || GetButtonPressed(shiftInto4thButton)))
            {
                if (GetButtonPressed(shiftInto3rdButton))
                {
                    _shiftIntoInput = 1;
                }
                else if (GetButtonPressed(shiftInto4thButton))
                {
                    _shiftIntoInput = -1;
                }
                else
                {
                    _shiftIntoInput = 0;
                }
            }
            else
            {
                // H-shifter
                if (GetButtonPressed(shiftIntoReverseButton))
                {
                    _shiftIntoInput = -1;
                }
                else if (GetButtonPressed(shiftIntoNeutralButton))
                {
                    _shiftIntoInput = 0;
                }
                else if (GetButtonPressed(shiftInto1stButton))
                {
                    _shiftIntoInput = 1;
                }
                else if (GetButtonPressed(shiftInto2ndButton))
                {
                    _shiftIntoInput = 2;
                }
                else if (GetButtonPressed(shiftInto3rdButton))
                {
                    _shiftIntoInput = 3;
                }
                else if (GetButtonPressed(shiftInto4thButton))
                {
                    _shiftIntoInput = 4;
                }
                else if (GetButtonPressed(shiftInto5thButton))
                {
                    _shiftIntoInput = 5;
                }
                else if (GetButtonPressed(shiftInto6thButton))
                {
                    _shiftIntoInput = 6;
                }
                else if (GetButtonPressed(shiftInto7thButton))
                {
                    _shiftIntoInput = 7;
                }
                else if (GetButtonPressed(shiftInto8thButton))
                {
                    _shiftIntoInput = 8;
                }
                else if (GetButtonPressed(shiftInto9thButton))
                {
                    _shiftIntoInput = 9;
                }
            }
        }


        void GetWheelInputs()
        {
            _wheelInput = LogitechGSDK.LogiGetStateUnity(deviceIndex);

            // Steer angle
            _steeringInput = GetAxisValue(steeringAxis, _wheelInput, false) * steeringSensitivity;
            if (flipSteeringInput) _steeringInput = -_steeringInput;
            float steerDelta = _steeringInput - _prevSteering;
            _steerVelocity = steerDelta / Time.deltaTime;
            if (_steeringInput < steeringDeadzone && _steeringInput > -steeringDeadzone)
            {
                _steeringInput = 0f;
            }

            // Throttle
            _throttleInput = GetAxisValue(throttleAxis, _wheelInput, throttleZeroToOne);
            if (flipThrottleInput) _throttleInput = -_throttleInput;
            if (_throttleInput < throttleDeadzone) _throttleInput = 0f;

            // Brake
            _brakeInput = GetAxisValue(brakeAxis, _wheelInput, brakeZeroToOne);
            if (flipBrakeInput) _brakeInput = -_brakeInput;
            if (_brakeInput < brakeDeadzone) _brakeInput = 0f;

            // Clutch
            _clutchInput = GetAxisValue(clutchAxis, _wheelInput, clutchZeroToOne);
            if (flipClutchInput) _clutchInput = -_clutchInput;
            if (_clutchInput < clutchDeadzone) _clutchInput = 0f;

            // Handbrake
            if (handbrakeAxis != Axis.None)
            {
                _handbrakeInput = GetAxisValue(handbrakeAxis, _wheelInput, handbrakeZeroToOne);
                if (flipHandbrakeInput) _handbrakeInput = -_handbrakeInput;
            }
            else
            {
                _handbrakeInput = GetButtonPressed(handbrakeButton) ? 1f : 0f;
            }
            if (_handbrakeInput < handbrakeDeadzone) _handbrakeInput = 0f;

            // Buttons
            for (int i = 0; i < 128; i++)
            {
                buttonWasPressed[i] = buttonPressed[i];
                buttonPressed[i] = _wheelInput.rgbButtons[i] == 128;
                buttonDown[i] = !buttonWasPressed[i] && buttonPressed[i];
            }
        }


        bool GetButtonPressed(int buttonIndex)
        {
            if (buttonIndex < 0)
            {
                return false;
            }

            return buttonPressed[buttonIndex];
        }


        bool GetButtonDown(int buttonIndex)
        {
            if (buttonIndex < 0)
            {
                return false;
            }

            return buttonDown[buttonIndex];
        }


        float GetAxisValue(Axis axis, LogitechGSDK.DIJOYSTATE2ENGINES wheelState, bool zeroToOne)
        {
            float rawValue = 0;
            switch (axis)
            {
                case Axis.XPosition:
                    rawValue = wheelState.lX;
                    break;
                case Axis.YPosition:
                    rawValue = wheelState.lY;
                    break;
                case Axis.ZPosition:
                    rawValue = wheelState.lZ;
                    break;
                case Axis.XRotatation:
                    rawValue = wheelState.lRx;
                    break;
                case Axis.YRotation:
                    rawValue = wheelState.lRy;
                    break;
                case Axis.ZRotation:
                    rawValue = wheelState.lRz;
                    break;
                case Axis.rglSlider0:
                    rawValue = wheelState.rglSlider[0];
                    break;
                case Axis.rglSlider1:
                    rawValue = wheelState.rglSlider[1];
                    break;
                case Axis.rglSlider2:
                    rawValue = wheelState.rglSlider[2];
                    break;
                case Axis.rglSlider3:
                    rawValue = wheelState.rglSlider[3];
                    break;
                case Axis.rglASlider0:
                    rawValue = wheelState.rglASlider[0];
                    break;
                case Axis.rglASlider1:
                    rawValue = wheelState.rglASlider[1];
                    break;
                case Axis.rglASlider2:
                    rawValue = wheelState.rglASlider[2];
                    break;
                case Axis.rglASlider3:
                    rawValue = wheelState.rglASlider[3];
                    break;
                case Axis.rglFSlider0:
                    rawValue = wheelState.rglFSlider[0];
                    break;
                case Axis.rglFSlider1:
                    rawValue = wheelState.rglFSlider[1];
                    break;
                case Axis.rglFSlider2:
                    rawValue = wheelState.rglFSlider[2];
                    break;
                case Axis.rglFSlider3:
                    rawValue = wheelState.rglFSlider[3];
                    break;
                case Axis.rglVSlider0:
                    rawValue = wheelState.rglVSlider[0];
                    break;
                case Axis.rglVSlider1:
                    rawValue = wheelState.rglVSlider[1];
                    break;
                case Axis.rglVSlider2:
                    rawValue = wheelState.rglVSlider[2];
                    break;
                case Axis.rglVSlider3:
                    rawValue = wheelState.rglVSlider[3];
                    break;
                case Axis.lArx:
                    rawValue = wheelState.lARx;
                    break;
                case Axis.lAry:
                    rawValue = wheelState.lARy;
                    break;
                case Axis.lArz:
                    rawValue = wheelState.lARz;
                    break;
                case Axis.lAx:
                    rawValue = wheelState.lAX;
                    break;
                case Axis.lAy:
                    rawValue = wheelState.lAY;
                    break;
                case Axis.lAz:
                    rawValue = wheelState.lAZ;
                    break;
                case Axis.lFRx:
                    rawValue = wheelState.lFRx;
                    break;
                case Axis.lFRy:
                    rawValue = wheelState.lFRy;
                    break;
                case Axis.lFRz:
                    rawValue = wheelState.lFRz;
                    break;
                case Axis.lFx:
                    rawValue = wheelState.lFX;
                    break;
                case Axis.lFy:
                    rawValue = wheelState.lFY;
                    break;
                case Axis.lFz:
                    rawValue = wheelState.lFZ;
                    break;
                case Axis.lVRx:
                    rawValue = wheelState.lVRx;
                    break;
                case Axis.lVRy:
                    rawValue = wheelState.lVRy;
                    break;
                case Axis.lVRz:
                    rawValue = wheelState.lVRz;
                    break;
                case Axis.lVx:
                    rawValue = wheelState.lVX;
                    break;
                case Axis.lVy:
                    rawValue = wheelState.lVY;
                    break;
                case Axis.lVz:
                    rawValue = wheelState.lVZ;
                    break;
                default:
                    rawValue = 0;
                    break;
            }

            float halfResolution = axisResolution / 2f;
            if (zeroToOne)
            {
                return (rawValue - halfResolution) / axisResolution;
            }
            else
            {
                return rawValue / halfResolution;
            }
        }


        void AddForce(float force)
        {
            if (deviceIndex < 0) return;
            LogitechGSDK.LogiPlayConstantForce(deviceIndex, (int)force);
        }


        void ResetForce()
        {
            if (deviceIndex < 0) return;
            LogitechGSDK.LogiStopConstantForce(deviceIndex);
        }


        void OnApplicationQuit()
        {
            LogitechGSDK.LogiSteeringShutdown();
        }


        public override bool EngineStartStop()
        {
            return false;
        }


        public override float Clutch()
        {
            return _clutchInput;
        }


        public override bool ExtraLights()
        {
            return false;
        }


        public override bool HighBeamLights()
        {
            return false;
        }


        public override float Handbrake()
        {
            return _handbrakeInput;
        }


        public override bool HazardLights()
        {
            return false;
        }


        public override float Brakes()
        {
            return _brakeInput;
        }


        public override float Steering()
        {
            return _steeringInput;
        }


        public override bool Horn()
        {
            return false;
        }


        public override bool LeftBlinker()
        {
            return false;
        }


        public override bool LowBeamLights()
        {
            return false;
        }


        public override bool RightBlinker()
        {
            return false;
        }


        public override bool ShiftDown()
        {
            return _shiftDownInput;
        }


        public override int ShiftInto()
        {
            return _shiftIntoInput;
        }


        public override bool ShiftUp()
        {
            return _shiftUpInput;
        }


        public override bool TrailerAttachDetach()
        {
            return false;
        }


        public override float Throttle()
        {
            return _throttleInput;
        }


        public override bool FlipOver()
        {
            return false;
        }


        public override bool Boost()
        {
            return false;
        }


        public override bool CruiseControl()
        {
            return false;
        }
    }
}


#if UNITY_EDITOR

namespace NWH.VehiclePhysics2.Input
{
    [CustomEditor(typeof(SteeringWheelInputProvider))]
    public class SteeringWheelInputProviderEditor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI())
            {
                return false;
            }

            drawer.BeginSubsection("Device");
            drawer.Field("deviceIndex");
            drawer.Field("onlyFFBDevices");
            drawer.ReorderableList("deviceNameContainsWhitelist");
            if (drawer.Field("showDeviceDebugInfo").boolValue)
            {
                drawer.Field("foundDevicesDebugString");
            }
            drawer.EndSubsection();

            drawer.BeginSubsection("Forces");
            drawer.Field("overallEffectStrength", true, "%");
            drawer.Field("maximumWheelForce", true, "%");
            drawer.Field("smoothing");
            drawer.Field("useDirectInput");

            drawer.BeginSubsection("Low Speed Friction");
            drawer.Field("lowSpeedFriction");
            drawer.EndSubsection();

            drawer.BeginSubsection("Self Aligning Torque");
            drawer.Field("maxSatForce", true, "%", "Max. Sat Force");
            drawer.Field("slipSATCurve");
            drawer.Field("slipMultiplier");
            drawer.EndSubsection();

            drawer.BeginSubsection("Friction");
            drawer.Field("friction");
            drawer.EndSubsection();

            drawer.BeginSubsection("Centering Force");
            drawer.Field("centeringForceStrength");
            drawer.Field("centerPositionDrift");
            drawer.EndSubsection();

            drawer.BeginSubsection("Debug Values");
            drawer.Field("_lowSpeedFrictionForce", false);
            drawer.Field("_satForce", false);
            drawer.Field("_frictionForce", false);
            drawer.Field("_centeringForce", false);
            drawer.Field("_totalForce", false);
            drawer.Info("Total Force should never exceed 100. This will result in force clipping as wheels can not reproduce forces above 100%.");
            drawer.EndSubsection();
            drawer.EndSubsection();

            drawer.BeginSubsection("Input");
            drawer.Field("steeringSensitivity");
            drawer.Field("hShifterUse34asDR");

            drawer.BeginSubsection("Axes");
            drawer.Field("axisResolution");
            drawer.Field("wheelRotationRange");
            drawer.HorizontalRuler();
            drawer.Field("steeringAxis");
            drawer.Field("flipSteeringInput");
            drawer.Field("steeringDeadzone");
            drawer.HorizontalRuler();
            drawer.Field("throttleAxis");
            drawer.Field("flipThrottleInput");
            drawer.Field("throttleZeroToOne");
            drawer.Field("throttleDeadzone");
            drawer.HorizontalRuler();
            drawer.Field("brakeAxis");
            drawer.Field("flipBrakeInput");
            drawer.Field("brakeZeroToOne");
            drawer.Field("brakeDeadzone");
            drawer.HorizontalRuler();
            drawer.Field("clutchAxis");
            drawer.Field("flipClutchInput");
            drawer.Field("clutchZeroToOne");
            drawer.Field("clutchDeadzone");
            drawer.HorizontalRuler();
            drawer.Field("handbrakeAxis");
            drawer.Field("flipHandbrakeInput");
            drawer.Field("handbrakeZeroToOne");
            drawer.Field("handbrakeDeadzone");
            drawer.EndSubsection();

            drawer.BeginSubsection("Buttons");
            drawer.BeginSubsection("Sequential Shifter");
            drawer.Field("shiftUpButton");
            drawer.Field("altShiftUpButton");
            drawer.Field("shiftDownButton");
            drawer.Field("altShiftDownButton");
            drawer.EndSubsection();
            drawer.BeginSubsection("H-shifter");
            drawer.Field("shiftIntoReverseButton");
            drawer.Field("shiftIntoNeutralButton");
            drawer.Field("shiftInto1stButton");
            drawer.Field("shiftInto2ndButton");
            drawer.Field("shiftInto3rdButton");
            drawer.Field("shiftInto4thButton");
            drawer.Field("shiftInto5thButton");
            drawer.Field("shiftInto6thButton");
            drawer.Field("shiftInto7thButton");
            drawer.Field("shiftInto8thButton");
            drawer.Field("shiftInto9thButton");
            drawer.EndSubsection();
            drawer.EndSubsection();

            drawer.BeginSubsection("Input Debug Values");
            drawer.Field("_steeringInput", false);
            drawer.Field("_throttleInput", false);
            drawer.Field("_brakeInput", false);
            drawer.Field("_clutchInput", false);
            drawer.Field("_shiftUpInput", false);
            drawer.Field("_shiftDownInput", false);
            drawer.Field("_shiftIntoInput", false);
            drawer.EndSubsection();
            drawer.EndSubsection();

            drawer.EndEditor(this);
            return true;
        }

        public override bool UseDefaultMargins()
        {
            return false;
        }
    }
}

#endif

