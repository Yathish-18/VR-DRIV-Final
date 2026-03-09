using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class TrafficLightIntersectionManager : MonoBehaviour
{
    [Header("Intersection Identity")]
    [SerializeField] private string intersectionID = "Intersection_1";

    [Header("Traffic Light Groups")]
    [Tooltip("Group A: Lights that turn green together (e.g., North-South)")]
    [SerializeField] private List<TrafficLightController> groupA = new List<TrafficLightController>();

    [Tooltip("Group B: Lights that turn green together (e.g., East-West)")]
    [SerializeField] private List<TrafficLightController> groupB = new List<TrafficLightController>();

    [Tooltip("Group C: Optional third group (e.g., for T-junctions)")]
    [SerializeField] private List<TrafficLightController> groupC = new List<TrafficLightController>();

    [Header("Timing Configuration")]
    [SerializeField] private float groupAGreenDuration = 15f;
    [SerializeField] private float groupBGreenDuration = 15f;
    [SerializeField] private float groupCGreenDuration = 10f;
    [SerializeField] private float yellowDuration = 3f;
    [SerializeField] private float allRedSafetyDelay = 2f;

    [Header("Control Settings")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool useGroupC = false;
    [SerializeField] private float startDelay = 0f;

    [Header("Debug View")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private string status = "Stopped";

    private bool isRunning = false;
    private Coroutine cycleCoroutine;

    void Start()
    {
        // 1. Clean lists immediately to prevent null reference errors later
        CleanLists();

        // 2. Initialize lights to RED
        InitializeLights();

        if (autoStart)
        {
            if (startDelay > 0)
                StartCoroutine(DelayedStart());
            else
                StartIntersectionCycle();
        }
    }

    // FIX: Reset all state when the object/scene is disabled or unloaded
    void OnDisable()
    {
        isRunning = false;
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }
    }

    // Removes any empty slots in the Inspector lists
    void CleanLists()
    {
        groupA.RemoveAll(item => item == null);
        groupB.RemoveAll(item => item == null);
        groupC.RemoveAll(item => item == null);
    }

    void InitializeLights()
    {
        // Force all known lights to RED and disable their internal auto-start
        SetGroupState(groupA, TrafficLightController.LightState.Red);
        SetGroupState(groupB, TrafficLightController.LightState.Red);
        if (useGroupC) SetGroupState(groupC, TrafficLightController.LightState.Red);
    }

    IEnumerator DelayedStart()
    {
        status = $"Waiting {startDelay}s...";
        yield return new WaitForSeconds(startDelay);
        StartIntersectionCycle();
    }

    public void StartIntersectionCycle()
    {
        // FIX: Kill any stale coroutine before starting fresh
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        // FIX: Always reset isRunning before re-entering so guard never blocks a fresh start
        isRunning = false;

        // Final safety check
        if (groupA.Count == 0 && groupB.Count == 0)
        {
            Debug.LogError($"[{intersectionID}] Cannot start: No traffic lights assigned!");
            return;
        }

        isRunning = true;
        cycleCoroutine = StartCoroutine(RunTrafficCycle());
        if (debugMode) Debug.Log($"[{intersectionID}] Cycle Started");
    }

    public void StopIntersectionCycle()
    {
        isRunning = false;
        if (cycleCoroutine != null)
        {
            StopCoroutine(cycleCoroutine);
            cycleCoroutine = null;
        }

        InitializeLights(); // Reset to all red
        status = "Stopped (All Red)";
    }

    // --- THE MAIN LOOP ---
    IEnumerator RunTrafficCycle()
    {
        while (isRunning)
        {
            // PHASE A
            yield return StartCoroutine(RunTrafficPhase("Group A", groupA, groupAGreenDuration));

            // PHASE B
            yield return StartCoroutine(RunTrafficPhase("Group B", groupB, groupBGreenDuration));

            // PHASE C (Optional)
            if (useGroupC && groupC.Count > 0)
            {
                yield return StartCoroutine(RunTrafficPhase("Group C", groupC, groupCGreenDuration));
            }
        }
    }

    // --- GENERIC PHASE LOGIC ---
    // This handles the Green -> Yellow -> Red transition for ANY group
    IEnumerator RunTrafficPhase(string phaseName, List<TrafficLightController> activeGroup, float greenDuration)
    {
        if (activeGroup.Count == 0) yield break;

        // 1. TURN GREEN
        status = $"{phaseName}: GREEN";
        SetGroupState(activeGroup, TrafficLightController.LightState.Green);

        if (debugMode) Debug.Log($"[{intersectionID}] {phaseName} Green");
        yield return new WaitForSeconds(greenDuration);

        // 2. TURN YELLOW
        status = $"{phaseName}: YELLOW";
        SetGroupState(activeGroup, TrafficLightController.LightState.Yellow);

        if (debugMode) Debug.Log($"[{intersectionID}] {phaseName} Yellow");
        yield return new WaitForSeconds(yellowDuration);

        // 3. TURN RED (Safety Period)
        status = "ALL RED (Safety)";
        SetGroupState(activeGroup, TrafficLightController.LightState.Red);

        if (debugMode) Debug.Log($"[{intersectionID}] All Red Safety");
        yield return new WaitForSeconds(allRedSafetyDelay);
    }

    // --- HELPER FUNCTIONS ---
    void SetGroupState(List<TrafficLightController> group, TrafficLightController.LightState state)
    {
        foreach (var light in group)
        {
            if (light != null)
            {
                light.autoStart = false; // Ensure script overrides individual light settings
                light.SetLightState(state);
            }
        }
    }

    // --- GIZMOS & EDITOR TOOLS ---
    void OnDrawGizmos()
    {
        if (!debugMode) return;

        DrawGroupGizmos(groupA, Color.green);
        DrawGroupGizmos(groupB, Color.blue);
        if (useGroupC) DrawGroupGizmos(groupC, Color.cyan);
    }

    void DrawGroupGizmos(List<TrafficLightController> group, Color color)
    {
        Gizmos.color = color;
        foreach (var light in group)
        {
            if (light != null)
            {
                Gizmos.DrawLine(transform.position, light.transform.position);
                Gizmos.DrawWireSphere(light.transform.position, 1f);
            }
        }
    }

    // Right-click the component in Inspector to use this!
    [ContextMenu("Auto-Assign Nearest Lights")]
    void AutoAssignLights()
    {
        // Clear current lists
        groupA = new List<TrafficLightController>();
        groupB = new List<TrafficLightController>();
        groupC = new List<TrafficLightController>();

        // Find all lights in scene
        var allLights = FindObjectsOfType<TrafficLightController>();
        var nearbyLights = new List<TrafficLightController>();

        // Filter by distance (50 units)
        foreach (var light in allLights)
        {
            if (Vector3.Distance(transform.position, light.transform.position) < 50f)
            {
                nearbyLights.Add(light);
            }
        }

        // Sort by angle or just alternate them for simple setup
        // This simple version just alternates A -> B -> A -> B
        for (int i = 0; i < nearbyLights.Count; i++)
        {
            if (i % 2 == 0) groupA.Add(nearbyLights[i]);
            else groupB.Add(nearbyLights[i]);
        }

        Debug.Log($"[{intersectionID}] Auto-assigned {nearbyLights.Count} lights.");
    }
}