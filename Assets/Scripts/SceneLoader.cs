using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class SceneLoader : MonoBehaviour
{
    public GameObject loadingScreen;
    public Slider progressBar;
    public string targetSceneName = "ForkliftDrivingScene";
    public float fakeLoadingSpeed = 0.4f; // Adjust to make progress slower/faster

    private float currentProgress = 0f;

    public void StartGame()
    {
        StartCoroutine(LoadSceneWithFakeProgress());
    }

    IEnumerator LoadSceneWithFakeProgress()
    {
        loadingScreen.SetActive(true);
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(targetSceneName);
        asyncLoad.allowSceneActivation = false;

        while (currentProgress < 1f)
        {
            // Fake slow loading
            currentProgress += Time.deltaTime * fakeLoadingSpeed;
            progressBar.value = Mathf.Clamp01(currentProgress);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f); // Optional delay
        asyncLoad.allowSceneActivation = true;
    }
}
