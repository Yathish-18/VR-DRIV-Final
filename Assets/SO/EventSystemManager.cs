using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages EventSystem across scenes to prevent conflicts with DontDestroyOnLoad objects
/// Attach this to your EventSystem GameObject in the main menu scene
/// </summary>
public class EventSystemManager : MonoBehaviour
{
    public static EventSystemManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Destroy this EventSystem when loading a new scene (recommended for UI scenes)")]
    public bool destroyOnSceneLoad = false;

    [Tooltip("Keep this EventSystem persistent across scenes")]
    public bool persistAcrossScenes = false;

    private EventSystem eventSystem;
    private StandaloneInputModule inputModule;

    private void Awake()
    {
        eventSystem = GetComponent<EventSystem>();
        inputModule = GetComponent<StandaloneInputModule>();

        if (persistAcrossScenes)
        {
            // Singleton pattern with DontDestroyOnLoad
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Find any duplicate EventSystems in the new scene and remove them
        EventSystem[] eventSystems = FindObjectsOfType<EventSystem>();

        foreach (EventSystem es in eventSystems)
        {
            if (es != eventSystem && es.gameObject != this.gameObject)
            {
                Debug.Log($"Destroying duplicate EventSystem: {es.gameObject.name}");
                Destroy(es.gameObject);
            }
        }

        // Reactivate this EventSystem to ensure it's working
        RefreshEventSystem();
    }

    /// <summary>
    /// Forces the EventSystem to refresh - useful when UI isn't responding
    /// </summary>
    public void RefreshEventSystem()
    {
        if (eventSystem != null)
        {
            eventSystem.enabled = false;
            eventSystem.enabled = true;
        }

        if (inputModule != null)
        {
            inputModule.enabled = false;
            inputModule.enabled = true;
        }

        // Clear any selected object
        EventSystem.current?.SetSelectedGameObject(null);

        Debug.Log("EventSystem refreshed");
    }

    /// <summary>
    /// Call this from scripts when returning to UI scenes to ensure EventSystem is working
    /// </summary>
    public static void EnsureEventSystemActive()
    {
        EventSystem current = EventSystem.current;

        if (current == null)
        {
            Debug.LogWarning("No EventSystem found! Creating one...");
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
            return;
        }

        // Refresh the current EventSystem
        current.enabled = false;
        current.enabled = true;

        var module = current.GetComponent<StandaloneInputModule>();
        if (module != null)
        {
            module.enabled = false;
            module.enabled = true;
        }
    }
}