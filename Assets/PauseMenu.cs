using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenuPanel;   // Panel with Resume / Restart / Exit buttons

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu"; // Set in Inspector

    [Header("Reset Options")]
    [Tooltip("Reset only session data (track/weather/time) when exiting")]
    public bool resetSessionDataOnExit = true;
    [Tooltip("Reset ALL data including progress (races, best times) when exiting")]
    public bool resetAllDataOnExit = false;
    [Tooltip("Destroy any persistent EventSystems when returning to menu")]
    public bool destroyPersistentEventSystemOnExit = true;

    bool isPaused = false;

    void Start()
    {
        // Make sure game starts unpaused
        Time.timeScale = 1f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

    // Call this from the Pause button OnClick
    public void OnPauseButton()
    {
        if (!isPaused)
            PauseGame();
        else
            ResumeGame();
    }

    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(true);
    }

    // Call this from the Resume button OnClick
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f;
        if (pauseMenuPanel != null)
            pauseMenuPanel.SetActive(false);
    }

    // Call this from the Restart button OnClick
    public void RestartLevel()
    {
        Time.timeScale = 1f;
        Scene current = SceneManager.GetActiveScene();
        SceneManager.LoadScene(current.buildIndex);
    }

    // Call this from the Exit button OnClick
    public void ExitToMainMenu()
    {
        Time.timeScale = 1f;

        // Reset GamePersistenceManager data
        if (GamePersistenceManager.Instance != null)
        {
            if (resetAllDataOnExit)
            {
                // Reset everything (session + progress)
                GamePersistenceManager.Instance.ResetAllData();
                Debug.Log("All game data reset on exit");
            }
            else if (resetSessionDataOnExit)
            {
                // Reset only session data (track/weather/time)
                GamePersistenceManager.Instance.ResetSessionData();
                Debug.Log("Session data reset on exit");
            }
        }

        // Handle EventSystem cleanup
        if (destroyPersistentEventSystemOnExit)
        {
            // Find and destroy any DontDestroyOnLoad EventSystems
            EventSystem[] allEventSystems = FindObjectsOfType<EventSystem>(true);
            foreach (EventSystem es in allEventSystems)
            {
                // Check if this EventSystem is in DontDestroyOnLoad
                if (es.gameObject.scene.name == null || es.gameObject.scene.name == "DontDestroyOnLoad")
                {
                    Debug.Log($"Destroying persistent EventSystem: {es.gameObject.name}");
                    Destroy(es.gameObject);
                }
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);

        // For full quit in a build, you could use:
        // Application.Quit();
    }

    // Optional: Add this as an additional button for complete reset
    public void ExitAndResetEverything()
    {
        Time.timeScale = 1f;

        if (GamePersistenceManager.Instance != null)
        {
            GamePersistenceManager.Instance.ResetAllData();
            Debug.Log("Complete reset - all data cleared");
        }

        // Destroy persistent EventSystems
        EventSystem[] allEventSystems = FindObjectsOfType<EventSystem>(true);
        foreach (EventSystem es in allEventSystems)
        {
            if (es.gameObject.scene.name == null || es.gameObject.scene.name == "DontDestroyOnLoad")
            {
                Destroy(es.gameObject);
            }
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}