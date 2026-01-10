using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject pauseMenuPanel;   // Panel with Resume / Restart / Exit buttons

    [Header("Scene Names")]
    public string mainMenuSceneName = "MainMenu"; // Set in Inspector

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
        SceneManager.LoadScene(mainMenuSceneName);
        // For full quit in a build, you could use:
        // Application.Quit();
    }
}
