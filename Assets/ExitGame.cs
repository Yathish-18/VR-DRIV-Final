using UnityEngine;

public class ExitGame : MonoBehaviour
{
    // Call this from your Exit button OnClick
    public void QuitApp()
    {
        // This only works in a built app, not in the Editor
        Application.Quit();

        // Optional: log so you can see it firing in Editor
        Debug.Log("QuitApp called");
    }
}
