using UnityEngine;
using System.Collections;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Logitech
{
    public class LogitechSteeringWheel : MonoBehaviour
    {
        LogitechGSDK.LogiControllerPropertiesData properties;
        private string actualState;
        private string activeForces;
        private string propertiesEdit;
        private string buttonStatus;
        private string forcesLabel;
        string[] activeForceAndEffect;

        // ENHANCED: Static variable management from WheelInitialiser
        private static int activeInstances = 0;
        private static bool globalSDKActive = false;
        private static bool staticVariablesReset = false;

        // ENHANCED: Instance tracking
        private bool sdkInitialized = false;
        private bool usingExistingSDK = false;
        private bool ownedSDKInitialization = false;

        // CRITICAL: Reset static variables when entering Play Mode
#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticVariables()
        {
            Debug.Log("[LogitechSteeringWheel] 🔄 RESETTING STATIC VARIABLES");
            activeInstances = 0;
            globalSDKActive = false;
            staticVariablesReset = true;
        }

        // Also register for Play Mode state changes as backup
        [InitializeOnLoadMethod]
        static void RegisterPlayModeCallback()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Debug.Log("[LogitechSteeringWheel] 🔄 Play Mode exiting - resetting static variables");
                activeInstances = 0;
                globalSDKActive = false;
                staticVariablesReset = false;
            }
        }
#endif

        // ENHANCED: Awake with proper static variable handling
        void Awake()
        {
            // FIXED: Always ensure static variables are properly initialized
            if (!staticVariablesReset)
            {
                Debug.LogWarning("[LogitechSteeringWheel] ⚠️ Static variables not reset! Manually resetting...");
                activeInstances = 0;
                globalSDKActive = false;
                staticVariablesReset = true;
            }

            activeInstances++;
            Debug.Log($"[LogitechSteeringWheel] Awake - Instance: {GetInstanceID()} | Active instances: {activeInstances}");

            // SAFETY CHECK: Prevent instance overflow
            if (activeInstances > 100)
            {
                Debug.LogError($"[LogitechSteeringWheel] 🚨 CRITICAL: Instance count too high ({activeInstances})! This indicates a static variable reset issue.");
                Debug.LogError("[LogitechSteeringWheel] 💡 SOLUTION: Check Project Settings > Editor > Enter Play Mode Options");
                Debug.LogError("[LogitechSteeringWheel] 💡 Either disable 'Enter Play Mode Options' OR ensure Domain Reload is enabled");
                // Emergency reset
                activeInstances = 1;
            }
        }

        // Use this for initialization
        void Start()
        {
            activeForces = "";
            propertiesEdit = "";
            actualState = "";
            buttonStatus = "";
            forcesLabel = "Press the following keys to activate forces and effects on the steering wheel / gaming controller \n";
            forcesLabel += "Spring force : S\n";
            forcesLabel += "Constant force : C\n";
            forcesLabel += "Damper force : D\n";
            forcesLabel += "Side collision : Left or Right Arrow\n";
            forcesLabel += "Front collision : Up arrow\n";
            forcesLabel += "Dirt road effect : I\n";
            forcesLabel += "Bumpy road effect : B\n";
            forcesLabel += "Slippery road effect : L\n";
            forcesLabel += "Surface effect : U\n";
            forcesLabel += "Car Airborne effect : A\n";
            forcesLabel += "Soft Stop Force : O\n";
            forcesLabel += "Set example controller properties : PageUp\n";
            forcesLabel += "Play Leds : P\n";

            activeForceAndEffect = new string[9];

            // ENHANCED: Better SDK initialization logic
            InitializeSDK();
        }

        // ENHANCED: Smart SDK initialization
        void InitializeSDK()
        {
            Debug.Log("[LogitechSteeringWheel] Starting SDK initialization process...");

            // First check if SDK is already active (from another component like NWH)
            if (TestExistingSDK())
            {
                Debug.Log("[LogitechSteeringWheel] ✅ Found active SDK! Using existing initialization.");
                sdkInitialized = true;
                usingExistingSDK = true;
                globalSDKActive = true;
                return;
            }

            // Initialize our own SDK if none exists
            if (!globalSDKActive)
            {
                Debug.Log("[LogitechSteeringWheel] No existing SDK found, initializing our own...");
                bool initResult = LogitechGSDK.LogiSteeringInitialize(false);
                Debug.Log("SteeringInit:" + initResult);

                if (initResult)
                {
                    sdkInitialized = true;
                    ownedSDKInitialization = true;
                    globalSDKActive = true;
                    Debug.Log("[LogitechSteeringWheel] ✅ Successfully initialized our own SDK");
                }
                else
                {
                    Debug.LogError("Failed to initialize Logitech Steering SDK");
                }
            }
            else
            {
                Debug.Log("SDK already initialized globally, skipping initialization");
                sdkInitialized = true;
                usingExistingSDK = true;
            }
        }

        // ENHANCED: Test for existing SDK
        bool TestExistingSDK()
        {
            try
            {
                bool updateResult = LogitechGSDK.LogiUpdate();
                if (updateResult)
                {
                    bool connected = LogitechGSDK.LogiIsConnected(0);
                    Debug.Log($"[LogitechSteeringWheel] Existing SDK test - Update: {updateResult}, Connected: {connected}");
                    return true;
                }
            }
            catch (System.Exception e)
            {
                Debug.Log($"[LogitechSteeringWheel] SDK test exception: {e.Message}");
            }
            return false;
        }

        // ENHANCED: Safe shutdown with proper instance management
        void SafeShutdown()
        {
            activeInstances = Mathf.Max(0, activeInstances - 1);
            Debug.Log($"[LogitechSteeringWheel] SafeShutdown - Instances remaining: {activeInstances} | Owned SDK: {ownedSDKInitialization}");

            // Stop all forces first
            StopAllForces();

            // Don't shutdown if we're using someone else's SDK
            if (usingExistingSDK && !ownedSDKInitialization)
            {
                Debug.Log("[LogitechSteeringWheel] Skipping shutdown - using existing SDK from another component");
                return;
            }

            // Only shutdown if we initialized it ourselves and we're the last instance
            if (activeInstances <= 0 && ownedSDKInitialization)
            {
                bool shouldShutdown = true;

#if UNITY_EDITOR
                // Skip shutdown in Editor to prevent crashes (configurable)
                if (true) // You can make this a public bool field if needed
                {
                    Debug.Log("[LogitechSteeringWheel] Skipping shutdown in Editor to prevent crash");
                    shouldShutdown = false;
                }
#endif

                if (shouldShutdown)
                {
                    try
                    {
                        bool shutdownResult = LogitechGSDK.LogiSteeringShutdown();
                        Debug.Log("SteeringShutdown:" + shutdownResult);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[LogitechSteeringWheel] Shutdown error: {e.Message}");
                    }
                    finally
                    {
                        sdkInitialized = false;
                        globalSDKActive = false;
                        ownedSDKInitialization = false;
                    }
                }
            }
        }

        void OnDestroy()
        {
            Debug.Log($"[LogitechSteeringWheel] OnDestroy - Instance: {GetInstanceID()}");
            SafeShutdown();
        }

        void OnApplicationQuit()
        {
            StopAllForces();
            SafeShutdown();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            // Handle focus loss/gain - SDK might need reinitialization
            if (!hasFocus)
            {
                StopAllForces();
            }
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                StopAllForces();
            }
        }

        private void StopAllForces()
        {
            if (sdkInitialized && LogitechGSDK.LogiIsConnected(0))
            {
                // Stop all active forces
                LogitechGSDK.LogiStopSpringForce(0);
                LogitechGSDK.LogiStopConstantForce(0);
                LogitechGSDK.LogiStopDamperForce(0);
                LogitechGSDK.LogiStopDirtRoadEffect(0);
                LogitechGSDK.LogiStopBumpyRoadEffect(0);
                LogitechGSDK.LogiStopSlipperyRoadEffect(0);
                LogitechGSDK.LogiStopSurfaceEffect(0);
                LogitechGSDK.LogiStopCarAirborne(0);
                LogitechGSDK.LogiStopSoftstopForce(0);

                // Clear the active force array
                for (int i = 0; i < activeForceAndEffect.Length; i++)
                {
                    activeForceAndEffect[i] = "";
                }
            }
        }

        void OnGUI()
        {
            activeForces = GUI.TextArea(new Rect(10, 10, 180, 200), activeForces, 400);
            propertiesEdit = GUI.TextArea(new Rect(200, 10, 200, 200), propertiesEdit, 400);
            actualState = GUI.TextArea(new Rect(410, 10, 300, 200), actualState, 1000);
            buttonStatus = GUI.TextArea(new Rect(720, 10, 300, 200), buttonStatus, 1000);
            GUI.Label(new Rect(10, 400, 800, 400), forcesLabel);
        }

        // Update is called once per frame  
        void Update()
        {
            // ENHANCED: Better SDK state checking
            if (!sdkInitialized)
            {
                actualState = "SDK NOT INITIALIZED PROPERLY";
                return;
            }

            //All the test functions are called on the first device plugged in(index = 0)
            if (LogitechGSDK.LogiUpdate() && LogitechGSDK.LogiIsConnected(0))
            {
                //CONTROLLER PROPERTIES
                StringBuilder deviceName = new StringBuilder(256);
                LogitechGSDK.LogiGetFriendlyProductName(0, deviceName, 256);
                propertiesEdit = "Current Controller : " + deviceName + "\n";
                propertiesEdit += "Current controller properties : \n\n";
                LogitechGSDK.LogiControllerPropertiesData actualProperties = new LogitechGSDK.LogiControllerPropertiesData();
                LogitechGSDK.LogiGetCurrentControllerProperties(0, ref actualProperties);
                propertiesEdit += "forceEnable = " + actualProperties.forceEnable + "\n";
                propertiesEdit += "overallGain = " + actualProperties.overallGain + "\n";
                propertiesEdit += "springGain = " + actualProperties.springGain + "\n";
                propertiesEdit += "damperGain = " + actualProperties.damperGain + "\n";
                propertiesEdit += "defaultSpringEnabled = " + actualProperties.defaultSpringEnabled + "\n";
                propertiesEdit += "combinePedals = " + actualProperties.combinePedals + "\n";
                propertiesEdit += "wheelRange = " + actualProperties.wheelRange + "\n";
                propertiesEdit += "gameSettingsEnabled = " + actualProperties.gameSettingsEnabled + "\n";
                propertiesEdit += "allowGameSettings = " + actualProperties.allowGameSettings + "\n";

                //CONTROLLER STATE
                actualState = "Steering wheel current state : \n\n";
                LogitechGSDK.DIJOYSTATE2ENGINES rec;
                rec = LogitechGSDK.LogiGetStateUnity(0);
                actualState += "x-axis position :" + rec.lX + "\n";
                actualState += "y-axis position :" + rec.lY + "\n";
                actualState += "z-axis position :" + rec.lZ + "\n";
                actualState += "x-axis rotation :" + rec.lRx + "\n";
                actualState += "y-axis rotation :" + rec.lRy + "\n";
                actualState += "z-axis rotation :" + rec.lRz + "\n";
                actualState += "extra axes positions 1 :" + rec.rglSlider[0] + "\n";
                actualState += "extra axes positions 2 :" + rec.rglSlider[1] + "\n";

                switch (rec.rgdwPOV[0])
                {
                    case (0): actualState += "POV : UP\n"; break;
                    case (4500): actualState += "POV : UP-RIGHT\n"; break;
                    case (9000): actualState += "POV : RIGHT\n"; break;
                    case (13500): actualState += "POV : DOWN-RIGHT\n"; break;
                    case (18000): actualState += "POV : DOWN\n"; break;
                    case (22500): actualState += "POV : DOWN-LEFT\n"; break;
                    case (27000): actualState += "POV : LEFT\n"; break;
                    case (31500): actualState += "POV : UP-LEFT\n"; break;
                    default: actualState += "POV : CENTER\n"; break;
                }

                //Button status :
                buttonStatus = "Button pressed : \n\n";
                for (int i = 0; i < 128; i++)
                {
                    if (rec.rgbButtons[i] == 128)
                    {
                        buttonStatus += "Button " + i + " pressed\n";
                    }
                }

                int shifterTipe = LogitechGSDK.LogiGetShifterMode(0);
                string shifterString = "";
                if (shifterTipe == 1) shifterString = "Gated";
                else if (shifterTipe == 0) shifterString = "Sequential";
                else shifterString = "Unknown";
                actualState += "\nSHIFTER MODE:" + shifterString;

                // FORCES AND EFFECTS
                activeForces = "Active forces and effects :\n";

                //Spring Force -> S
                if (Input.GetKeyUp(KeyCode.S))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_SPRING))
                    {
                        LogitechGSDK.LogiStopSpringForce(0);
                        activeForceAndEffect[0] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlaySpringForce(0, 50, 50, 50);
                        activeForceAndEffect[0] = "Spring Force\n ";
                    }
                }

                //Constant Force -> C
                if (Input.GetKeyUp(KeyCode.C))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_CONSTANT))
                    {
                        LogitechGSDK.LogiStopConstantForce(0);
                        activeForceAndEffect[1] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlayConstantForce(0, 50);
                        activeForceAndEffect[1] = "Constant Force\n ";
                    }
                }

                //Damper Force -> D
                if (Input.GetKeyUp(KeyCode.D))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_DAMPER))
                    {
                        LogitechGSDK.LogiStopDamperForce(0);
                        activeForceAndEffect[2] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlayDamperForce(0, 50);
                        activeForceAndEffect[2] = "Damper Force\n ";
                    }
                }

                //Side Collision Force -> left or right arrow
                if (Input.GetKeyUp(KeyCode.LeftArrow) || Input.GetKeyUp(KeyCode.RightArrow))
                {
                    LogitechGSDK.LogiPlaySideCollisionForce(0, 60);
                }

                //Front Collision Force -> up arrow
                if (Input.GetKeyUp(KeyCode.UpArrow))
                {
                    LogitechGSDK.LogiPlayFrontalCollisionForce(0, 60);
                }

                //Dirt Road Effect-> I
                if (Input.GetKeyUp(KeyCode.I))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_DIRT_ROAD))
                    {
                        LogitechGSDK.LogiStopDirtRoadEffect(0);
                        activeForceAndEffect[3] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlayDirtRoadEffect(0, 50);
                        activeForceAndEffect[3] = "Dirt Road Effect\n ";
                    }
                }

                //Bumpy Road Effect-> B
                if (Input.GetKeyUp(KeyCode.B))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_BUMPY_ROAD))
                    {
                        LogitechGSDK.LogiStopBumpyRoadEffect(0);
                        activeForceAndEffect[4] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlayBumpyRoadEffect(0, 50);
                        activeForceAndEffect[4] = "Bumpy Road Effect\n";
                    }
                }

                //Slippery Road Effect-> L
                if (Input.GetKeyUp(KeyCode.L))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_SLIPPERY_ROAD))
                    {
                        LogitechGSDK.LogiStopSlipperyRoadEffect(0);
                        activeForceAndEffect[5] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlaySlipperyRoadEffect(0, 50);
                        activeForceAndEffect[5] = "Slippery Road Effect\n ";
                    }
                }

                //Surface Effect-> U
                if (Input.GetKeyUp(KeyCode.U))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_SURFACE_EFFECT))
                    {
                        LogitechGSDK.LogiStopSurfaceEffect(0);
                        activeForceAndEffect[6] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlaySurfaceEffect(0, LogitechGSDK.LOGI_PERIODICTYPE_SQUARE, 50, 1000);
                        activeForceAndEffect[6] = "Surface Effect\n";
                    }
                }

                //Car Airborne -> A
                if (Input.GetKeyUp(KeyCode.A))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_CAR_AIRBORNE))
                    {
                        LogitechGSDK.LogiStopCarAirborne(0);
                        activeForceAndEffect[7] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlayCarAirborne(0);
                        activeForceAndEffect[7] = "Car Airborne\n ";
                    }
                }

                //Soft Stop Force -> O
                if (Input.GetKeyUp(KeyCode.O))
                {
                    if (LogitechGSDK.LogiIsPlaying(0, LogitechGSDK.LOGI_FORCE_SOFTSTOP))
                    {
                        LogitechGSDK.LogiStopSoftstopForce(0);
                        activeForceAndEffect[8] = "";
                    }
                    else
                    {
                        LogitechGSDK.LogiPlaySoftstopForce(0, 20);
                        activeForceAndEffect[8] = "Soft Stop Force\n";
                    }
                }

                //Set preferred controller properties -> PageUp
                if (Input.GetKeyUp(KeyCode.PageUp))
                {
                    //Setting example values
                    properties.wheelRange = 90;
                    properties.forceEnable = true;
                    properties.overallGain = 80;
                    properties.springGain = 80;
                    properties.damperGain = 80;
                    properties.allowGameSettings = true;
                    properties.combinePedals = false;
                    properties.defaultSpringEnabled = true;
                    properties.defaultSpringGain = 80;
                    LogitechGSDK.LogiSetPreferredControllerProperties(properties);
                }

                //Play leds -> P
                if (Input.GetKeyUp(KeyCode.P))
                {
                    LogitechGSDK.LogiPlayLeds(0, 20, 20, 20);
                }

                for (int i = 0; i < 9; i++)
                {
                    activeForces += activeForceAndEffect[i];
                }
            }
            else if (!LogitechGSDK.LogiIsConnected(0))
            {
                actualState = "PLEASE PLUG IN A STEERING WHEEL OR A FORCE FEEDBACK CONTROLLER";
            }
            else
            {
                actualState = "THIS WINDOW NEEDS TO BE IN FOREGROUND IN ORDER FOR THE SDK TO WORK PROPERLY";
            }
        }

        // ENHANCED: Public properties for debugging
        public bool IsInitialized => sdkInitialized;
        public bool IsUsingExistingSDK => usingExistingSDK;
        public bool OwnedSDKInitialization => ownedSDKInitialization;
        public int ActiveInstances => activeInstances;

        // Context menu methods for debugging
        [ContextMenu("Check Static Variables")]
        public void CheckStaticVariables()
        {
            Debug.Log($"[LogitechSteeringWheel] Static Variables Status:");
            Debug.Log($"[LogitechSteeringWheel] - activeInstances: {activeInstances}");
            Debug.Log($"[LogitechSteeringWheel] - globalSDKActive: {globalSDKActive}");
            Debug.Log($"[LogitechSteeringWheel] - staticVariablesReset: {staticVariablesReset}");
            Debug.Log($"[LogitechSteeringWheel] - sdkInitialized: {sdkInitialized}");
            Debug.Log($"[LogitechSteeringWheel] - usingExistingSDK: {usingExistingSDK}");
            Debug.Log($"[LogitechSteeringWheel] - ownedSDKInitialization: {ownedSDKInitialization}");
        }
    }
}
