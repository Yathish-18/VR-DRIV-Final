using UnityEngine;

public class SkyboxTest : MonoBehaviour
{
    [SerializeField] private Material testSkybox;

    void Start()
    {
        Debug.Log("SkyboxTest running...");
        Debug.Log("Current skybox: " + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL"));

        if (testSkybox != null)
        {
            RenderSettings.skybox = testSkybox;
            DynamicGI.UpdateEnvironment();
            Debug.Log("Applied: " + testSkybox.name);
        }
        else
        {
            Debug.LogError("testSkybox is NULL - assign a material in Inspector!");
        }
    }
}