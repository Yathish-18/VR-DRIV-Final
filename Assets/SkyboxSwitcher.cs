using UnityEngine;

public class SkyboxSwitcher : MonoBehaviour
{
    [Header("Assign Skybox Materials")]
    [SerializeField] private Material[] skyboxes;

    /// <summary>
    /// Called by UI Button with skybox index.
    /// </summary>
    /// <param name="index">Skybox material index in the array</param>
    public void ChangeSkybox(int index)
    {
        if (skyboxes == null || skyboxes.Length == 0)
        {
            Debug.LogError("Skybox list is empty.");
            return;
        }

        if (index < 0 || index >= skyboxes.Length)
        {
            Debug.LogWarning("Skybox index out of range.");
            return;
        }

        RenderSettings.skybox = skyboxes[index];
        DynamicGI.UpdateEnvironment(); // Update lighting to match new skybox
    }
}
