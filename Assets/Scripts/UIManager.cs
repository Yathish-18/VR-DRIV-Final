using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject[] uiPanels;

    /// <summary>
    /// Set active a child UI using index and set active false for other UI
    /// </summary>
    /// <param name="index">Index of panel to show</param>
    public void ShowPanel(int index)
    {
        // Set all panels to false
        for (int i = 0; i < uiPanels.Length; i++)
        {
            uiPanels[i].SetActive(false);
        }

        // Set the specified index panel to true
        if (index >= 0 && index < uiPanels.Length)
        {
            uiPanels[index].SetActive(true);
        }
    }

    /// <summary>
    /// Reset or reload the current scene
    /// </summary>
    public void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Exit the application
    /// </summary>
    public void ExitApplication()
    {
        Application.Quit();
    }
    public void Test() 
    {
        Debug.Log("workin ui");
    }
}
