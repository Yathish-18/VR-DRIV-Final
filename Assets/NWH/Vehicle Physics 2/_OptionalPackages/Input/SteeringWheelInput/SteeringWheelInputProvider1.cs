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
    public class SteeringWheelInputProvider1 : VehicleInputProviderBase
    {
        public bool[] buttonDown = new bool[128];
        public bool[] buttonPressed = new bool[128];
        public bool[] buttonWasPressed = new bool[128];

        private static int activeInstances = 0;
        private bool sdkInitialized = false;
        private bool hasShutdown = false;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            activeInstances = 0;
        }
#endif

        public enum Axis
        {
            XPosition, YPosition, ZPosition, XRotatation, YRotation, ZRotation,
            rglSlider0, rglSlider1, rglSlider2, rglSlider3,
            rglASlider0, rglASlider1, rglASlider2, rglASlider3,
            rglFSlider0, rglFSlider1, rglFSlider2, rglFSlider3,
            rglVSlider0, rglVSlider1, rglVSlider2, rglVSlider3,
            lArx, lAry, lArz, lAx, lAy, lAz,
            lFRx, lFRy, lFRz, lFx, lFy, lFz,
            lVRx, lVRy, lVRz, lVx, lVy, lVz, None
        }

        public bool useDirectInput = true;
        public float steeringSensitivity = 1f;
        public bool hShifterUse34asDR = false;
        public VehicleController vehicleController;
        [Range(0, 100)] public int maximumWheelForce = 100;
        public float overallEffectStrength = 1f;
        [Range(0, 0.1f)] public float smoothing = 0f;
        public int wheelRotationRange = 540;
        public bool linearizeSDKInput = true;
        public AnimationCurve slipSATCurve = new AnimationCurve(
            new Keyframe(0, 0, -0.9f, 25f), new Keyframe(0.07f, 1),
            new Keyframe(0.16f, 0.93f, -1f, -1f), new Keyframe(1f, 0.2f));
        public float maxSatForce = 80;
        public float slipMultiplier = 4.6f;
        public float lowSpeedFriction = 70f;
        public float friction = 16f;
        public float centeringForceStrength = 60;
        [Range(0, 1)] public float centerPositionDrift = 0.4f;
        public int axisResolution = 65536;
        public bool flipSteeringInput = false;
        public bool flipThrottleInput = true;
        public bool flipBrakeInput = true;
        public bool flipClutchInput = true;
        public bool flipHandbrakeInput = true;
        public Axis steeringAxis = Axis.XPosition;
        public Axis throttleAxis = Axis.YPosition;
        public bool throttleZeroToOne = true;
        public Axis brakeAxis = Axis.ZRotation;
        public bool brakeZeroToOne = true;
        public Axis clutchAxis = Axis.ZPosition;
        public bool clutchZeroToOne = true;
        public Axis handbrakeAxis = Axis.None;
        public bool handbrakeZeroToOne = true;
        public int shiftUpButton = 12;
        public int shiftDownButton = 13;
        public int altShiftUpButton = 4;
        public int altShiftDownButton = 5;
        public int shiftIntoReverseButton = -1;
        public int shiftIntoNeutralButton = -1;
        public int shiftInto1stButton = -1;
        public int shiftInto2ndButton = -1;
        public int shiftInto3rdButton = -1;
        public int shiftInto4thButton = -1;
        public int shiftInto5thButton = -1;
        public int shiftInto6thButton = -1;
        public int shiftInto7thButton = -1;
        public int shiftInto8thButton = -1;
        public int shiftInto9thButton = -1;
        public int handbrakeButton = -1;
        public int deviceIndex = -1;
        public bool onlyFFBDevices = true;
        public List<string> deviceNameContainsWhitelist = new List<string>();
        public bool showDeviceDebugInfo = true;
        public string foundDevicesDebugString = "---";

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
        private float _overallCoeff = 1f;
        private float _frictionCoeff = 1f;
        private float _lowSpeedFrictionCoeff = 1f;
        private float _satCoeff = 1f;
        private float _centeringCoeff = 1f;
        StringBuilder _inputDeviceName;

        public override void Awake()
        {
            activeInstances++;
            base.Awake();
            Vehicle.onActiveVehicleChanged.AddListener(HandleActiveVehicleChange);
        }

        void Start()
        {
            // Try to initialize SDK, but don't fail if it doesn't work
            try
            {
                if (!sdkInitialized)
                {
                    bool result = LogitechGSDK.LogiSteeringInitialize(false);
                    sdkInitialized = result;
                }
            }
            catch { }

            buttonDown = new bool[128];
            buttonWasPressed = new bool[128];
            buttonPressed = new bool[128];
            _inputDeviceName = new StringBuilder(256);
        }

        private void Update()
        {
            if (hasShutdown) return;

            if (deviceIndex < 0)
            {
                deviceIndex = FindDeviceIndex();
                if (deviceIndex >= 0)
                {
                    GetDeviceName(deviceIndex, ref _inputDeviceName);
                    InitializeWheel();
                }
            }

            if (deviceIndex < 0) return;

            try
            {
                if (LogitechGSDK.LogiIsConnected(deviceIndex))
                {
                    GetWheelInputs();
                    SetVehicleInputs();
                }
            }
            catch { }
        }

        void FixedUpdate()
        {
            if (hasShutdown || deviceIndex < 0 || vehicleController == null) return;

            _ffbSettings = vehicleController.GetComponent<ForceFeedbackSettings>();
            if (_ffbSettings == null)
            {
                _overallCoeff = _frictionCoeff = _lowSpeedFrictionCoeff = _satCoeff = _centeringCoeff = 1f;
            }
            else
            {
                _overallCoeff = _ffbSettings.overallCoeff;
                _frictionCoeff = _ffbSettings.frictionCoeff;
                _lowSpeedFrictionCoeff = _ffbSettings.lowSpeedFrictionCoeff;
                _satCoeff = _ffbSettings.satCoeff;
                _centeringCoeff = _ffbSettings.centeringCoeff;
            }

            if (!vehicleController.enabled) { ResetForce(); return; }

            try
            {
                if (LogitechGSDK.LogiIsConnected(deviceIndex) && LogitechGSDK.LogiUpdate())
                {
                    vehicleController.steering.useRawInput = useDirectInput;
                    _leftWheel = vehicleController.powertrain.wheels[0].wheelUAPI;
                    _rightWheel = vehicleController.powertrain.wheels[1].wheelUAPI;

                    float leftFactor = _leftWheel.Load / _leftWheel.MaxLoad * _leftWheel.FrictionPreset.BCDE.z;
                    float rightFactor = _rightWheel.Load / _rightWheel.MaxLoad * _rightWheel.FrictionPreset.BCDE.z;
                    float combinedFactor = leftFactor + rightFactor;
                    float totalSlip = _leftWheel.LateralSlip * leftFactor + _rightWheel.LateralSlip * rightFactor;
                    float absSlip = totalSlip < 0 ? -totalSlip : totalSlip;
                    float slipSign = totalSlip < 0 ? -1f : 1f;

                    _satForce = slipSATCurve.Evaluate(absSlip * slipMultiplier) * -slipSign * maxSatForce * combinedFactor * _satCoeff;
                    float newForce = Mathf.Lerp(0f, _satForce, vehicleController.Speed - 0.4f);

                    _centerPosition = ((_rightWheel.SpringLength / _rightWheel.SpringMaxLength) -
                        (_leftWheel.SpringLength / _leftWheel.SpringMaxLength)) * centerPositionDrift;
                    _centeringForce = (_steeringInput - _centerPosition) * centeringForceStrength * _centeringCoeff;
                    newForce += _centeringForce;

                    _lowSpeedFrictionForce = Mathf.Lerp(lowSpeedFriction, 0, vehicleController.Speed - 0.2f) * _lowSpeedFrictionCoeff;
                    _frictionForce = friction * _frictionCoeff;

                    LogitechGSDK.LogiPlayDamperForce(deviceIndex, (int)(_lowSpeedFrictionForce + _frictionForce));
                    newForce *= overallEffectStrength * _overallCoeff;

                    _totalForce = smoothing < 0.001f ? newForce :
                        Mathf.SmoothDamp(_totalForce, newForce, ref _totalForceVelocity, smoothing);

                    LogitechGSDK.LogiPlayConstantForce(deviceIndex, (int)_totalForce);
                    _prevSteering = _steeringInput;
                }
                else
                {
                    vehicleController.steering.useRawInput = false;
                }
            }
            catch { }
        }

        public override void OnDestroy()
        {
            if (hasShutdown) return;
            hasShutdown = true;

            base.OnDestroy();
            Vehicle.onActiveVehicleChanged.RemoveListener(HandleActiveVehicleChange);

            activeInstances = Mathf.Max(0, activeInstances - 1);

            // Stop forces only - NEVER call LogiSteeringShutdown in Editor
            if (deviceIndex >= 0)
            {
                try
                {
                    LogitechGSDK.LogiStopConstantForce(deviceIndex);
                    LogitechGSDK.LogiStopDamperForce(deviceIndex);
                    LogitechGSDK.LogiStopSpringForce(deviceIndex);
                }
                catch { }
            }

            // Only shutdown SDK in standalone builds, NEVER in Editor
#if !UNITY_EDITOR
            if (activeInstances <= 0 && sdkInitialized)
            {
                try { LogitechGSDK.LogiSteeringShutdown(); }
                catch { }
            }
#endif
        }

        private void Reset()
        {
            slipSATCurve = new AnimationCurve(
                new Keyframe(0, 0, -0.9f, 25f), new Keyframe(0.07f, 1),
                new Keyframe(0.16f, 0.93f, -1f, -1f), new Keyframe(1f, 0.2f));
        }

        void InitializeWheel()
        {
            if (deviceIndex < 0) return;
            try
            {
                LogitechGSDK.LogiControllerPropertiesData props = new LogitechGSDK.LogiControllerPropertiesData();
                LogitechGSDK.LogiGetCurrentControllerProperties(deviceIndex, ref props);
                props.forceEnable = true;
                props.combinePedals = false;
                props.gameSettingsEnabled = true;
                props.defaultSpringEnabled = false;
                props.defaultSpringGain = props.springGain = props.damperGain = 100;
                props.overallGain = (int)(maximumWheelForce * 100);
                props.wheelRange = wheelRotationRange;
                LogitechGSDK.LogiSetPreferredControllerProperties(props);
            }
            catch { }
        }

        public int FindDeviceIndex()
        {
            try
            {
                for (int i = 0; i < 16; i++)
                {
                    if (LogitechGSDK.LogiIsConnected(i))
                    {
                        LogitechGSDK.LogiGetFriendlyProductName(i, _inputDeviceName, 256);
                        if (onlyFFBDevices && !LogitechGSDK.LogiHasForceFeedback(i)) continue;
                        if (deviceNameContainsWhitelist.Count > 0)
                        {
                            string devStr = _inputDeviceName.ToString();
                            foreach (string match in deviceNameContainsWhitelist)
                                if (devStr.Contains(match)) return i;
                        }
                        else return i;
                    }
                }
            }
            catch { }
            return -1;
        }

        public void GetDeviceName(int index, ref StringBuilder deviceName)
        {
            if (index >= 0)
            {
                try { LogitechGSDK.LogiGetFriendlyProductName(index, deviceName, 256); }
                catch { }
            }
        }

        private void HandleCollision(Collision collision)
        {
            if (deviceIndex < 0) return;
            try
            {
                int strength = (int)(collision.impulse.magnitude /
                    (vehicleController.fixedDeltaTime * vehicleController.vehicleRigidbody.mass * 5f));
                LogitechGSDK.LogiPlayFrontalCollisionForce(deviceIndex, strength);
            }
            catch { }
        }

        private void HandleActiveVehicleChange(Vehicle previousVehicle, Vehicle currentVehicle)
        {
            VehicleController prevVC = previousVehicle as VehicleController;
            if (prevVC != null) prevVC.onCollision.RemoveListener(HandleCollision);

            VehicleController currVC = currentVehicle as VehicleController;
            if (currVC != null)
            {
                vehicleController = currVC;
                currVC.onCollision.AddListener(HandleCollision);
            }
        }

        void SetVehicleInputs()
        {
            _shiftUpInput = GetButtonDown(shiftUpButton) || GetButtonDown(altShiftUpButton);
            _shiftDownInput = GetButtonDown(shiftDownButton) || GetButtonDown(altShiftDownButton);
            _shiftIntoInput = hShifterUse34asDR ? 0 : -999;

            if (hShifterUse34asDR && (GetButtonPressed(shiftInto3rdButton) || GetButtonPressed(shiftInto4thButton)))
            {
                if (GetButtonPressed(shiftInto3rdButton)) _shiftIntoInput = 1;
                else if (GetButtonPressed(shiftInto4thButton)) _shiftIntoInput = -1;
                else _shiftIntoInput = 0;
            }
            else
            {
                if (GetButtonPressed(shiftIntoReverseButton)) _shiftIntoInput = -1;
                else if (GetButtonPressed(shiftIntoNeutralButton)) _shiftIntoInput = 0;
                else if (GetButtonPressed(shiftInto1stButton)) _shiftIntoInput = 1;
                else if (GetButtonPressed(shiftInto2ndButton)) _shiftIntoInput = 2;
                else if (GetButtonPressed(shiftInto3rdButton)) _shiftIntoInput = 3;
                else if (GetButtonPressed(shiftInto4thButton)) _shiftIntoInput = 4;
                else if (GetButtonPressed(shiftInto5thButton)) _shiftIntoInput = 5;
                else if (GetButtonPressed(shiftInto6thButton)) _shiftIntoInput = 6;
                else if (GetButtonPressed(shiftInto7thButton)) _shiftIntoInput = 7;
                else if (GetButtonPressed(shiftInto8thButton)) _shiftIntoInput = 8;
                else if (GetButtonPressed(shiftInto9thButton)) _shiftIntoInput = 9;
            }
        }

        void GetWheelInputs()
        {
            _wheelInput = LogitechGSDK.LogiGetStateUnity(deviceIndex);

            _steeringInput = GetAxisValue(steeringAxis, _wheelInput, false) * steeringSensitivity;
            if (flipSteeringInput) _steeringInput = -_steeringInput;
            if (_steeringInput < steeringDeadzone && _steeringInput > -steeringDeadzone) _steeringInput = 0f;

            _throttleInput = GetAxisValue(throttleAxis, _wheelInput, throttleZeroToOne);
            if (flipThrottleInput) _throttleInput = -_throttleInput;
            if (_throttleInput < throttleDeadzone) _throttleInput = 0f;

            _brakeInput = GetAxisValue(brakeAxis, _wheelInput, brakeZeroToOne);
            if (flipBrakeInput) _brakeInput = -_brakeInput;
            if (_brakeInput < brakeDeadzone) _brakeInput = 0f;

            float rawClutch = GetAxisValue(clutchAxis, _wheelInput, clutchZeroToOne);
            if (flipClutchInput) rawClutch = -rawClutch;
            _clutchInput = rawClutch < clutchDeadzone ? 0f : rawClutch;

            if (handbrakeAxis != Axis.None)
            {
                _handbrakeInput = GetAxisValue(handbrakeAxis, _wheelInput, handbrakeZeroToOne);
                if (flipHandbrakeInput) _handbrakeInput = -_handbrakeInput;
            }
            else _handbrakeInput = GetButtonPressed(handbrakeButton) ? 1f : 0f;

            if (_handbrakeInput < handbrakeDeadzone) _handbrakeInput = 0f;

            for (int i = 0; i < 128; i++)
            {
                buttonWasPressed[i] = buttonPressed[i];
                buttonPressed[i] = _wheelInput.rgbButtons[i] == 128;
                buttonDown[i] = !buttonWasPressed[i] && buttonPressed[i];
            }
        }

        bool GetButtonPressed(int idx) => idx >= 0 && buttonPressed[idx];
        bool GetButtonDown(int idx) => idx >= 0 && buttonDown[idx];

        float GetAxisValue(Axis axis, LogitechGSDK.DIJOYSTATE2ENGINES ws, bool zeroToOne)
        {
            float raw = axis switch
            {
                Axis.XPosition => ws.lX,
                Axis.YPosition => ws.lY,
                Axis.ZPosition => ws.lZ,
                Axis.XRotatation => ws.lRx,
                Axis.YRotation => ws.lRy,
                Axis.ZRotation => ws.lRz,
                Axis.rglSlider0 => ws.rglSlider[0],
                Axis.rglSlider1 => ws.rglSlider[1],
                Axis.rglSlider2 => ws.rglSlider[2],
                Axis.rglSlider3 => ws.rglSlider[3],
                Axis.rglASlider0 => ws.rglASlider[0],
                Axis.rglASlider1 => ws.rglASlider[1],
                Axis.rglASlider2 => ws.rglASlider[2],
                Axis.rglASlider3 => ws.rglASlider[3],
                Axis.rglFSlider0 => ws.rglFSlider[0],
                Axis.rglFSlider1 => ws.rglFSlider[1],
                Axis.rglFSlider2 => ws.rglFSlider[2],
                Axis.rglFSlider3 => ws.rglFSlider[3],
                Axis.rglVSlider0 => ws.rglVSlider[0],
                Axis.rglVSlider1 => ws.rglVSlider[1],
                Axis.rglVSlider2 => ws.rglVSlider[2],
                Axis.rglVSlider3 => ws.rglVSlider[3],
                Axis.lArx => ws.lARx,
                Axis.lAry => ws.lARy,
                Axis.lArz => ws.lARz,
                Axis.lAx => ws.lAX,
                Axis.lAy => ws.lAY,
                Axis.lAz => ws.lAZ,
                Axis.lFRx => ws.lFRx,
                Axis.lFRy => ws.lFRy,
                Axis.lFRz => ws.lFRz,
                Axis.lFx => ws.lFX,
                Axis.lFy => ws.lFY,
                Axis.lFz => ws.lFZ,
                Axis.lVRx => ws.lVRx,
                Axis.lVRy => ws.lVRy,
                Axis.lVRz => ws.lVRz,
                Axis.lVx => ws.lVX,
                Axis.lVy => ws.lVY,
                Axis.lVz => ws.lVZ,
                _ => 0
            };

            float half = axisResolution / 2f;
            return zeroToOne ? (raw - half) / axisResolution : raw / half;
        }

        void ResetForce()
        {
            if (deviceIndex >= 0)
            {
                try { LogitechGSDK.LogiStopConstantForce(deviceIndex); }
                catch { }
            }
        }

        public override bool EngineStartStop() => false;
        public override float Clutch() => _clutchInput;
        public override bool ExtraLights() => false;
        public override bool HighBeamLights() => false;
        public override float Handbrake() => _handbrakeInput;
        public override bool HazardLights() => false;
        public override float Brakes() => _brakeInput;
        public override float Steering() => _steeringInput;
        public override bool Horn() => false;
        public override bool LeftBlinker() => false;
        public override bool LowBeamLights() => false;
        public override bool RightBlinker() => false;
        public override bool ShiftDown() => _shiftDownInput;
        public override int ShiftInto() => _shiftIntoInput;
        public override bool ShiftUp() => _shiftUpInput;
        public override bool TrailerAttachDetach() => false;
        public override float Throttle() => _throttleInput;
        public override bool FlipOver() => false;
        public override bool Boost() => false;
        public override bool CruiseControl() => false;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(SteeringWheelInputProvider1))]
    public class SteeringWheelInputProvider1Editor : NUIEditor
    {
        public override bool OnInspectorNUI()
        {
            if (!base.OnInspectorNUI()) return false;
            drawer.BeginSubsection("Device");
            drawer.Field("deviceIndex");
            drawer.Field("onlyFFBDevices");
            drawer.ReorderableList("deviceNameContainsWhitelist");
            if (drawer.Field("showDeviceDebugInfo").boolValue) drawer.Field("foundDevicesDebugString");
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
            drawer.Field("maxSatForce", true, "%");
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
            drawer.Field("shiftUpButton");
            drawer.Field("altShiftUpButton");
            drawer.Field("shiftDownButton");
            drawer.Field("altShiftDownButton");
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
            drawer.EndEditor(this);
            return true;
        }
        public override bool UseDefaultMargins() => false;
    }
#endif
}
