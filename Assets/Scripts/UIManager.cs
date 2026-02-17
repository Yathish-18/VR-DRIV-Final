using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class UIManager : MonoBehaviour
{
    // Singleton Instance
    public static UIManager Instance { get; private set; }

    [Header("UI Panel References")]
    [Tooltip("The main container for the Pause Menu")]
    public GameObject pauseMenuPanel;

    [Tooltip("The container for your new Graph UI")]
    public GameObject graphPanel;

    [Tooltip("General UI Panels (Used by DrivingSimulatorController)")]
    public GameObject[] uiPanels;

    [Header("Scene Settings")]
    public string mainMenuSceneName = "MainMenu";

    [Header("Exit / Reset Options")]
    [Tooltip("Reset only session data (track/weather/time) when exiting")]
    public bool resetSessionDataOnExit = true;
    [Tooltip("Reset ALL data including progress (races, best times) when exiting")]
    public bool resetAllDataOnExit = false;
    [Tooltip("Destroy any persistent EventSystems when returning to menu")]
    public bool destroyPersistentEventSystemOnExit = true;

    // Public property to check state from other scripts
    public bool IsPaused { get; private set; } = false;

    private void Awake()
    {
        // Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (graphPanel != null) graphPanel.SetActive(false);
    }

    // --- PAUSE LOGIC ---

    public void TogglePause()
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
        // Ensure graph is closed when opening pause menu initially
        if (graphPanel != null) graphPanel.SetActive(false);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (graphPanel != null) graphPanel.SetActive(false);
    }

    // --- GRAPH LOGIC ---

    public void OpenGraphPanel()
    {
        // Hide pause menu, show graph
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
        if (graphPanel != null) graphPanel.SetActive(true);
    }
  

    public void CloseGraphPanel()
    {
        // Hide graph, go back to pause menu
        if (graphPanel != null) graphPanel.SetActive(false);
        if (pauseMenuPanel != null) pauseMenuPanel.SetActive(true);
    }

    // --- SCENE & EXIT LOGIC ---

    public void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;
        HandleDataReset();
        HandleEventSystemCleanup();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void ExitApplication()
    {
        Application.Quit();
    }

    // --- HELPER METHODS ---

    // This is the method your DrivingSimulatorController was missing!
    public void ShowPanel(int index)
    {
        if (uiPanels == null) return;

        for (int i = 0; i < uiPanels.Length; i++)
        {
            if (uiPanels[i] != null) uiPanels[i].SetActive(false);
        }

        if (index >= 0 && index < uiPanels.Length)
        {
            if (uiPanels[index] != null) uiPanels[index].SetActive(true);
        }
    }

    private void HandleDataReset()
    {
        if (GamePersistenceManager.Instance != null)
        {
            if (resetAllDataOnExit)
                GamePersistenceManager.Instance.ResetAllData();
            else if (resetSessionDataOnExit)
                GamePersistenceManager.Instance.ResetSessionData();
        }
    }

    private void HandleEventSystemCleanup()
    {
        if (!destroyPersistentEventSystemOnExit) return;

        EventSystem[] allEventSystems = FindObjectsOfType<EventSystem>(true);
        foreach (EventSystem es in allEventSystems)
        {
            if (es.gameObject.scene.name == null || es.gameObject.scene.name == "DontDestroyOnLoad")
            {
                Destroy(es.gameObject);
            }
        }
    }
}