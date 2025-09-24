using UnityEngine;
using System.Collections.Generic;

public class CameraManager : MonoBehaviour
{
    [Header("Camera References")]
    public List<Camera> cameras = new List<Camera>();

    [Header("Camera Names (Optional)")]
    public List<string> cameraNames = new List<string>();

    [Header("Controls")]
    public KeyCode switchCameraKey = KeyCode.C;
    public KeyCode previousCameraKey = KeyCode.V;

    [Header("Transition Settings")]
    public bool useTransition = true;
    public float transitionDuration = 0.5f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip switchSound;

    // Private variables
    private int currentCameraIndex = 0;
    private bool isTransitioning = false;
    private float transitionTimer = 0f;
    private Camera previousCamera;
    private Camera currentCamera;

    void Start()
    {
        InitializeCameras();
    }

    void Update()
    {
        HandleInput();
        UpdateTransition();
    }

    void InitializeCameras()
    {
        // Remove null references
        cameras.RemoveAll(cam => cam == null);

        if (cameras.Count == 0)
        {
            Debug.LogWarning("CameraManager: No cameras assigned!");
            return;
        }

        // Ensure camera names list matches cameras count
        while (cameraNames.Count < cameras.Count)
        {
            cameraNames.Add($"Camera {cameraNames.Count + 1}");
        }

        // Activate first camera, deactivate others
        for (int i = 0; i < cameras.Count; i++)
        {
            cameras[i].enabled = (i == 0);
        }

        currentCamera = cameras[0];

        Debug.Log($"CameraManager: Initialized with {cameras.Count} cameras. Active: {GetCurrentCameraName()}");
    }

    void HandleInput()
    {
        if (cameras.Count <= 1) return;

        if (Input.GetKeyDown(switchCameraKey))
        {
            SwitchToNextCamera();
        }

        if (Input.GetKeyDown(previousCameraKey))
        {
            SwitchToPreviousCamera();
        }

        // Number key switching (1-9)
        for (int i = 1; i <= 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha0 + i))
            {
                SwitchToCamera(i - 1);
            }
        }
    }

    void UpdateTransition()
    {
        if (!isTransitioning || !useTransition) return;

        transitionTimer += Time.deltaTime;
        float progress = transitionTimer / transitionDuration;

        if (progress >= 1f)
        {
            // Transition complete
            isTransitioning = false;
            transitionTimer = 0f;

            // Disable previous camera
            if (previousCamera != null)
            {
                previousCamera.enabled = false;
            }
        }
        else
        {
            // Blend between cameras (if you want fade effect)
            float curveProgress = transitionCurve.Evaluate(progress);
            // You can implement camera blending here if needed
        }
    }

    public void SwitchToNextCamera()
    {
        if (cameras.Count <= 1) return;

        int nextIndex = (currentCameraIndex + 1) % cameras.Count;
        SwitchToCamera(nextIndex);
    }

    public void SwitchToPreviousCamera()
    {
        if (cameras.Count <= 1) return;

        int prevIndex = (currentCameraIndex - 1 + cameras.Count) % cameras.Count;
        SwitchToCamera(prevIndex);
    }

    public void SwitchToCamera(int index)
    {
        if (index < 0 || index >= cameras.Count || index == currentCameraIndex)
            return;

        if (isTransitioning) return; // Prevent switching during transition

        // Store previous camera
        previousCamera = currentCamera;

        // Update index and current camera
        currentCameraIndex = index;
        currentCamera = cameras[currentCameraIndex];

        // Handle transition
        if (useTransition && transitionDuration > 0f)
        {
            StartTransition();
        }
        else
        {
            // Instant switch
            if (previousCamera != null)
                previousCamera.enabled = false;

            currentCamera.enabled = true;
        }

        // Play sound effect
        PlaySwitchSound();

        // Debug info
        Debug.Log($"Switched to camera: {GetCurrentCameraName()} (Index: {currentCameraIndex})");
    }

    void StartTransition()
    {
        isTransitioning = true;
        transitionTimer = 0f;

        // Enable current camera
        currentCamera.enabled = true;

        // Keep previous camera enabled during transition
        if (previousCamera != null)
            previousCamera.enabled = true;
    }

    void PlaySwitchSound()
    {
        if (audioSource != null && switchSound != null)
        {
            audioSource.PlayOneShot(switchSound);
        }
    }

    public void SwitchToCameraByName(string cameraName)
    {
        int index = cameraNames.IndexOf(cameraName);
        if (index >= 0)
        {
            SwitchToCamera(index);
        }
        else
        {
            Debug.LogWarning($"Camera with name '{cameraName}' not found!");
        }
    }

    public void AddCamera(Camera newCamera, string cameraName = "")
    {
        if (newCamera == null) return;

        cameras.Add(newCamera);

        if (string.IsNullOrEmpty(cameraName))
            cameraName = $"Camera {cameras.Count}";

        cameraNames.Add(cameraName);

        // Disable newly added camera
        newCamera.enabled = false;

        Debug.Log($"Added camera: {cameraName}");
    }

    public void RemoveCamera(int index)
    {
        if (index < 0 || index >= cameras.Count) return;

        // Don't remove if it's the only camera
        if (cameras.Count <= 1)
        {
            Debug.LogWarning("Cannot remove the last camera!");
            return;
        }

        // If removing current camera, switch to next
        if (index == currentCameraIndex)
        {
            SwitchToNextCamera();
        }

        cameras.RemoveAt(index);
        if (index < cameraNames.Count)
            cameraNames.RemoveAt(index);

        // Adjust current index if needed
        if (currentCameraIndex >= cameras.Count)
            currentCameraIndex = 0;
    }

    public void RemoveCamera(Camera camera)
    {
        int index = cameras.IndexOf(camera);
        if (index >= 0)
        {
            RemoveCamera(index);
        }
    }

    // Public getters
    public Camera GetCurrentCamera()
    {
        return currentCamera;
    }

    public string GetCurrentCameraName()
    {
        if (currentCameraIndex >= 0 && currentCameraIndex < cameraNames.Count)
            return cameraNames[currentCameraIndex];
        return "Unknown";
    }

    public int GetCurrentCameraIndex()
    {
        return currentCameraIndex;
    }

    public int GetCameraCount()
    {
        return cameras.Count;
    }

    public List<string> GetAllCameraNames()
    {
        return new List<string>(cameraNames);
    }

    // Enable/disable cameras (useful for debugging)
    public void EnableCamera(int index, bool enable)
    {
        if (index >= 0 && index < cameras.Count)
        {
            cameras[index].enabled = enable;
        }
    }

    public void SetCameraActive(int index)
    {
        if (index >= 0 && index < cameras.Count)
        {
            SwitchToCamera(index);
        }
    }

    void OnValidate()
    {
        // Ensure camera names match camera count in inspector
        while (cameraNames.Count < cameras.Count)
        {
            cameraNames.Add($"Camera {cameraNames.Count + 1}");
        }

        while (cameraNames.Count > cameras.Count)
        {
            cameraNames.RemoveAt(cameraNames.Count - 1);
        }
    }

    // GUI for runtime debugging
    void OnGUI()
    {
        if (!Application.isPlaying) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Current Camera: {GetCurrentCameraName()}");
        GUILayout.Label($"Press '{switchCameraKey}' to switch cameras");
        GUILayout.Label($"Press '{previousCameraKey}' for previous camera");
        GUILayout.Label("Press 1-9 for direct camera switch");

        GUILayout.Space(10);

        // Camera buttons
        for (int i = 0; i < cameras.Count; i++)
        {
            bool isActive = (i == currentCameraIndex);
            GUI.backgroundColor = isActive ? Color.green : Color.white;

            if (GUILayout.Button($"{i + 1}. {cameraNames[i]}"))
            {
                SwitchToCamera(i);
            }
        }

        GUI.backgroundColor = Color.white;
        GUILayout.EndArea();
    }
}