//using UnityEngine;
//using NWH.VehiclePhysics2;

//public class GamePauseManager : MonoBehaviour
//{
//    [Header("Pause Settings")]
//    [SerializeField] private bool enableDebugLogs = true;
//    [SerializeField] private bool pauseAudio = true;
//    [SerializeField] private bool pauseParticles = true;

//    // Static instance for easy access
//    public static GamePauseManager Instance { get; private set; }

//    // Pause state
//    private static bool isPaused = false;

//    // Component references
//    private VehicleController vehicleController;
//    private TrafficRulesManager trafficRulesManager;
//    private DashboardDataProvider dataProvider;

//    // Original states for restoration
//    private float originalTimeScale;
//    private bool originalVehicleEnabled;
//    private bool originalDataProviderEnabled;

//    // Events
//    public static System.Action<bool> OnPauseStateChanged;

//    void Awake()
//    {
//        // Singleton pattern
//        if (Instance == null)
//        {
//            Instance = this;
//            DontDestroyOnLoad(gameObject);
//        }
//        else
//        {
//            Destroy(gameObject);
//            return;
//        }
//    }

//    void Start()
//    {
//        InitializeComponents();
//        originalTimeScale = Time.timeScale;
//    }

//    void InitializeComponents()
//    {
//        vehicleController = FindObjectOfType<VehicleController>();
//        trafficRulesManager = TrafficRulesManager.Instance;
//        dataProvider = FindObjectOfType<DashboardDataProvider>();

//        if (enableDebugLogs)
//        {
//            Debug.Log($"GamePauseManager: VehicleController found: {vehicleController != null}");
//            Debug.Log($"GamePauseManager: TrafficRulesManager found: {trafficRulesManager != null}");
//            Debug.Log($"GamePauseManager: DataProvider found: {dataProvider != null}");
//        }
//    }

//    /// <summary>
//    /// Pauses the entire game including vehicle, physics, audio, and data collection
//    /// </summary>
//    public static void PauseGame()
//    {
//        if (isPaused) return;

//        isPaused = true;

//        if (Instance != null)
//        {
//            Instance.ExecutePause();
//        }

//        OnPauseStateChanged?.Invoke(true);

//        if (Instance.enableDebugLogs)
//            Debug.Log("GamePauseManager: Game PAUSED");
//    }

//    /// <summary>
//    /// Resumes the game and restores all components to their original state
//    /// </summary>
//    public static void ResumeGame()
//    {
//        if (!isPaused) return;

//        isPaused = false;

//        if (Instance != null)
//        {
//            Instance.ExecuteResume();
//        }

//        OnPauseStateChanged?.Invoke(false);

//        if (Instance.enableDebugLogs)
//            Debug.Log("GamePauseManager: Game RESUMED");
//    }

//    /// <summary>
//    /// Toggles pause state
//    /// </summary>
//    public static void TogglePause()
//    {
//        if (isPaused)
//            ResumeGame();
//        else
//            PauseGame();
//    }

//    /// <summary>
//    /// Returns current pause state
//    /// </summary>
//    public static bool IsPaused()
//    {
//        return isPaused;
//    }

//    void ExecutePause()
//    {
//        // Store original states
//        originalTimeScale = Time.timeScale;

//        if (vehicleController != null)
//            originalVehicleEnabled = vehicleController.enabled;

//        if (dataProvider != null)
//            originalDataProviderEnabled = dataProvider.enabled;

//        // Pause time scale (affects physics, animations, etc.)
//        Time.timeScale = 0f;

//        // Disable vehicle controller to stop all vehicle systems
//        if (vehicleController != null)
//        {
//            vehicleController.enabled = false;

//            // Also disable input to prevent any input processing
//            if (vehicleController.input != null)
//                vehicleController.input.enabled = false;
//        }

//        // Disable data provider to stop data collection
//        if (dataProvider != null)
//            dataProvider.enabled = false;

//        // Pause traffic rules manager if it exists
//        if (trafficRulesManager != null && trafficRulesManager.enabled)
//        {
//            trafficRulesManager.enabled = false;
//        }

//        // Pause audio
//        if (pauseAudio)
//        {
//            AudioListener.pause = true;
//        }

//        // Pause particle systems
//        if (pauseParticles)
//        {
//            PauseAllParticles(true);
//        }
//    }

//    void ExecuteResume()
//    {
//        // Restore time scale
//        Time.timeScale = originalTimeScale;

//        // Re-enable vehicle controller
//        if (vehicleController != null)
//        {
//            vehicleController.enabled = originalVehicleEnabled;

//            // Re-enable input
//            if (vehicleController.input != null)
//                vehicleController.input.enabled = true;
//        }

//        // Re-enable data provider
//        if (dataProvider != null)
//            dataProvider.enabled = originalDataProviderEnabled;

//        // Re-enable traffic rules manager
//        if (trafficRulesManager != null)
//        {
//            trafficRulesManager.enabled = true;
//        }

//        // Resume audio
//        if (pauseAudio)
//        {
//            AudioListener.pause = false;
//        }

//        // Resume particle systems
//        if (pauseParticles)
//        {
//            PauseAllParticles(false);
//        }
//    }

//    void PauseAllParticles(bool pause)
//    {
//        ParticleSystem[] particles = FindObjectsOfType<ParticleSystem>();
//        foreach (var particle in particles)
//        {
//            if (pause)
//                particle.Pause();
//            else
//                particle.Play();
//        }
//    }

//    /// <summary>
//    /// Force stops the vehicle immediately (useful for emergency stops)
//    /// </summary>
//    public static void ForceStopVehicle()
//    {
//        if (Instance?.vehicleController != null)
//        {
//            // Stop the rigidbody
//            var rb = Instance.vehicleController.GetComponent<Rigidbody>();
//            if (rb != null)
//            {
//                rb.linearVelocity = Vector3.zero;
//                rb.angularVelocity = Vector3.zero;
//            }

//            // Reset vehicle input
//            if (Instance.vehicleController.input != null)
//            {
//                Instance.vehicleController.input.Horizontal = 0f;
//                Instance.vehicleController.input.Vertical = 0f;
//            }
//        }
//    }

//    /// <summary>
//    /// Pauses only data collection without affecting gameplay
//    /// </summary>
//    public static void PauseDataCollection()
//    {
//        if (Instance?.dataProvider != null)
//        {
//            Instance.dataProvider.enabled = false;
//        }

//        if (Instance.enableDebugLogs)
//            Debug.Log("GamePauseManager: Data collection paused");
//    }

//    /// <summary>
//    /// Resumes only data collection
//    /// </summary>
//    public static void ResumeDataCollection()
//    {
//        if (Instance?.dataProvider != null)
//        {
//            Instance.dataProvider.enabled = true;
//        }

//        if (Instance.enableDebugLogs)
//            Debug.Log("GamePauseManager: Data collection resumed");
//    }

//    void OnDestroy()
//    {
//        if (Instance == this)
//        {
//            // Ensure game is resumed if this object is destroyed while paused
//            if (isPaused)
//            {
//                Time.timeScale = originalTimeScale;
//                AudioListener.pause = false;
//            }
//            Instance = null;
//        }
//    }

//    void OnApplicationFocus(bool hasFocus)
//    {
//        // Optional: Auto-pause when application loses focus
//        // if (!hasFocus && !isPaused)
//        //     PauseGame();
//    }

//    void OnApplicationPause(bool pauseStatus)
//    {
//        // Auto-pause when application is paused (mobile)
//        if (pauseStatus && !isPaused)
//            PauseGame();
//    }

//    // Debug methods
//    [ContextMenu("Force Pause")]
//    public void ForcePauseDebug()
//    {
//        PauseGame();
//    }

//    [ContextMenu("Force Resume")]
//    public void ForceResumeDebug()
//    {
//        ResumeGame();
//    }

//    [ContextMenu("Log Pause State")]
//    public void LogPauseState()
//    {
//        Debug.Log($"Game Paused: {isPaused}, Time Scale: {Time.timeScale}");
//    }
//}
