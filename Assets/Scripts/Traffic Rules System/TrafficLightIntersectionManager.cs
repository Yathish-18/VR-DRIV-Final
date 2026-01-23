using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages a group of traffic lights at an intersection
/// Ensures safe, realistic traffic light coordination
/// Prevents conflicting green lights that would cause collisions
/// </summary>
public class TrafficLightIntersectionManager : MonoBehaviour
{
    [Header("Intersection Identity")]
    [SerializeField] private string intersectionID = "Intersection_1";

    [Header("Traffic Light Groups")]
    [Tooltip("Group A: Lights that turn green together (e.g., North-South)")]
    [SerializeField] private List<TrafficLightController> groupA = new List<TrafficLightController>();

    [Tooltip("Group B: Lights that turn green together (e.g., East-West)")]
    [SerializeField] private List<TrafficLightController> groupB = new List<TrafficLightController>();

    [Tooltip("Group C: Optional third group (e.g., for T-junctions or complex intersections)")]
    [SerializeField] private List<TrafficLightController> groupC = new List<TrafficLightController>();

    [Header("Timing Configuration")]
    [SerializeField] private float groupAGreenDuration = 15f;
    [SerializeField] private float groupBGreenDuration = 15f;
    [SerializeField] private float groupCGreenDuration = 10f;
    [SerializeField] private float yellowDuration = 3f;
    [SerializeField] private float allRedSafetyDelay = 2f; // All red period between phases for safety

    [Header("Control Settings")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool useGroupC = false;
    [SerializeField] private float cycleStartDelay = 0f; // Offset for desynchronizing intersections

    [Header("Debug")]
    [SerializeField] private bool debugMode = false;
    [SerializeField] private string currentPhase = "Initializing";

    private bool isRunning = false;
    private Coroutine cycleCoroutine;

    private enum IntersectionPhase
    {
        GroupAGreen,
        GroupAYellow,
        AllRed1,
        GroupBGreen,
        GroupBYellow,
        AllRed2,
        GroupCGreen,
        GroupCYellow,
        AllRed3
    }

    private IntersectionPhase currentIntersectionPhase = IntersectionPhase.GroupAGreen;

    void Start()
    {
        InitializeIntersection();

        if (autoStart)
        {
            if (cycleStartDelay > 0f)
            {
                StartCoroutine(DelayedStart());
            }
            else
            {
                StartIntersectionCycle();
            }
        }
    }

    IEnumerator DelayedStart()
    {
        if (debugMode)
            Debug.Log($"[{intersectionID}] Waiting {cycleStartDelay}s before starting cycle...");

        yield return new WaitForSeconds(cycleStartDelay);
        StartIntersectionCycle();
    }

    void InitializeIntersection()
    {
        // Validate groups
        if (groupA.Count == 0 && groupB.Count == 0)
        {
            Debug.LogError($"[{intersectionID}] No traffic lights assigned! Need at least Group A or Group B.");
            enabled = false;
            return;
        }

        // Disable auto-start on individual lights (we manage them)
        DisableIndividualAutoStart(groupA);
        DisableIndividualAutoStart(groupB);
        if (useGroupC)
            DisableIndividualAutoStart(groupC);

        // Set all lights to red initially
        SetGroupLights(groupA, TrafficLightController.LightState.Red);
        SetGroupLights(groupB, TrafficLightController.LightState.Red);
        if (useGroupC)
            SetGroupLights(groupC, TrafficLightController.LightState.Red);

        if (debugMode)
        {
            Debug.Log($"[{intersectionID}] ========== INTERSECTION INITIALIZED ==========");
            Debug.Log($"[{intersectionID}] Group A Lights: {groupA.Count}");
            Debug.Log($"[{intersectionID}] Group B Lights: {groupB.Count}");
            Debug.Log($"[{intersectionID}] Group C Lights: {(useGroupC ? groupC.Count.ToString() : "Disabled")}");
            Debug.Log($"[{intersectionID}] =============================================");
        }
    }

    void DisableIndividualAutoStart(List<TrafficLightController> group)
    {
        foreach (var light in group)
        {
            if (light != null)
            {
                light.autoStart = false;
                light.StopTrafficLight();
            }
        }
    }

    public void StartIntersectionCycle()
    {
        if (!isRunning)
        {
            isRunning = true;
            cycleCoroutine = StartCoroutine(IntersectionCycle());

            if (debugMode)
                Debug.Log($"[{intersectionID}] ✅ Intersection cycle started!");
        }
    }

    public void StopIntersectionCycle()
    {
        if (isRunning)
        {
            isRunning = false;

            if (cycleCoroutine != null)
            {
                StopCoroutine(cycleCoroutine);
                cycleCoroutine = null;
            }

            // Set all lights to red for safety
            SetGroupLights(groupA, TrafficLightController.LightState.Red);
            SetGroupLights(groupB, TrafficLightController.LightState.Red);
            if (useGroupC)
                SetGroupLights(groupC, TrafficLightController.LightState.Red);

            if (debugMode)
                Debug.Log($"[{intersectionID}] ⛔ Intersection cycle stopped - all lights red");
        }
    }

    IEnumerator IntersectionCycle()
    {
        while (isRunning)
        {
            // PHASE 1: Group A Green (e.g., North-South traffic flows)
            if (groupA.Count > 0)
            {
                currentIntersectionPhase = IntersectionPhase.GroupAGreen;
                currentPhase = "Group A: GREEN";
                SetGroupLights(groupA, TrafficLightController.LightState.Green);
                SetGroupLights(groupB, TrafficLightController.LightState.Red);
                if (useGroupC)
                    SetGroupLights(groupC, TrafficLightController.LightState.Red);

                if (debugMode)
                    Debug.Log($"[{intersectionID}] 🟢 Group A GREEN for {groupAGreenDuration}s");

                yield return new WaitForSeconds(groupAGreenDuration);

                // Group A Yellow
                currentIntersectionPhase = IntersectionPhase.GroupAYellow;
                currentPhase = "Group A: YELLOW";
                SetGroupLights(groupA, TrafficLightController.LightState.Yellow);

                if (debugMode)
                    Debug.Log($"[{intersectionID}] 🟡 Group A YELLOW for {yellowDuration}s");

                yield return new WaitForSeconds(yellowDuration);
            }

            // ALL RED safety period
            currentIntersectionPhase = IntersectionPhase.AllRed1;
            currentPhase = "ALL RED (Safety)";
            SetGroupLights(groupA, TrafficLightController.LightState.Red);
            SetGroupLights(groupB, TrafficLightController.LightState.Red);
            if (useGroupC)
                SetGroupLights(groupC, TrafficLightController.LightState.Red);

            if (debugMode)
                Debug.Log($"[{intersectionID}] 🔴 ALL RED safety period {allRedSafetyDelay}s");

            yield return new WaitForSeconds(allRedSafetyDelay);

            // PHASE 2: Group B Green (e.g., East-West traffic flows)
            if (groupB.Count > 0)
            {
                currentIntersectionPhase = IntersectionPhase.GroupBGreen;
                currentPhase = "Group B: GREEN";
                SetGroupLights(groupA, TrafficLightController.LightState.Red);
                SetGroupLights(groupB, TrafficLightController.LightState.Green);
                if (useGroupC)
                    SetGroupLights(groupC, TrafficLightController.LightState.Red);

                if (debugMode)
                    Debug.Log($"[{intersectionID}] 🟢 Group B GREEN for {groupBGreenDuration}s");

                yield return new WaitForSeconds(groupBGreenDuration);

                // Group B Yellow
                currentIntersectionPhase = IntersectionPhase.GroupBYellow;
                currentPhase = "Group B: YELLOW";
                SetGroupLights(groupB, TrafficLightController.LightState.Yellow);

                if (debugMode)
                    Debug.Log($"[{intersectionID}] 🟡 Group B YELLOW for {yellowDuration}s");

                yield return new WaitForSeconds(yellowDuration);
            }

            // ALL RED safety period
            currentIntersectionPhase = IntersectionPhase.AllRed2;
            currentPhase = "ALL RED (Safety)";
            SetGroupLights(groupA, TrafficLightController.LightState.Red);
            SetGroupLights(groupB, TrafficLightController.LightState.Red);
            if (useGroupC)
                SetGroupLights(groupC, TrafficLightController.LightState.Red);

            if (debugMode)
                Debug.Log($"[{intersectionID}] 🔴 ALL RED safety period {allRedSafetyDelay}s");

            yield return new WaitForSeconds(allRedSafetyDelay);

            // PHASE 3: Group C Green (optional - for complex intersections)
            if (useGroupC && groupC.Count > 0)
            {
                currentIntersectionPhase = IntersectionPhase.GroupCGreen;
                currentPhase = "Group C: GREEN";
                SetGroupLights(groupA, TrafficLightController.LightState.Red);
                SetGroupLights(groupB, TrafficLightController.LightState.Red);
                SetGroupLights(groupC, TrafficLightController.LightState.Green);

                if (debugMode)
                    Debug.Log($"[{intersectionID}] 🟢 Group C GREEN for {groupCGreenDuration}s");

                yield return new WaitForSeconds(groupCGreenDuration);

                // Group C Yellow
                currentIntersectionPhase = IntersectionPhase.GroupCYellow;
                currentPhase = "Group C: YELLOW";
                SetGroupLights(groupC, TrafficLightController.LightState.Yellow);

                if (debugMode)
                    Debug.Log($"[{intersectionID}] 🟡 Group C YELLOW for {yellowDuration}s");

                yield return new WaitForSeconds(yellowDuration);

                // ALL RED safety period before cycling back
                currentIntersectionPhase = IntersectionPhase.AllRed3;
                currentPhase = "ALL RED (Safety)";
                SetGroupLights(groupA, TrafficLightController.LightState.Red);
                SetGroupLights(groupB, TrafficLightController.LightState.Red);
                SetGroupLights(groupC, TrafficLightController.LightState.Red);

                if (debugMode)
                    Debug.Log($"[{intersectionID}] 🔴 ALL RED safety period {allRedSafetyDelay}s");

                yield return new WaitForSeconds(allRedSafetyDelay);
            }

            // Cycle complete - loop back to start
            if (debugMode)
                Debug.Log($"[{intersectionID}] 🔄 Cycle complete - restarting...");
        }
    }

    void SetGroupLights(List<TrafficLightController> group, TrafficLightController.LightState state)
    {
        foreach (var light in group)
        {
            if (light != null)
            {
                light.SetLightState(state);
            }
        }
    }

    // ========================================
    // PUBLIC API
    // ========================================

    public void SetGroupADuration(float duration)
    {
        groupAGreenDuration = Mathf.Max(1f, duration);
        if (debugMode)
            Debug.Log($"[{intersectionID}] Group A green duration set to {groupAGreenDuration}s");
    }

    public void SetGroupBDuration(float duration)
    {
        groupBGreenDuration = Mathf.Max(1f, duration);
        if (debugMode)
            Debug.Log($"[{intersectionID}] Group B green duration set to {groupBGreenDuration}s");
    }

    public void SetGroupCDuration(float duration)
    {
        groupCGreenDuration = Mathf.Max(1f, duration);
        if (debugMode)
            Debug.Log($"[{intersectionID}] Group C green duration set to {groupCGreenDuration}s");
    }

    public void SetYellowDuration(float duration)
    {
        yellowDuration = Mathf.Max(1f, duration);
        if (debugMode)
            Debug.Log($"[{intersectionID}] Yellow duration set to {yellowDuration}s");
    }

    public void SetAllRedDelay(float delay)
    {
        allRedSafetyDelay = Mathf.Max(0f, delay);
        if (debugMode)
            Debug.Log($"[{intersectionID}] All-red safety delay set to {allRedSafetyDelay}s");
    }

    public void AddLightToGroupA(TrafficLightController light)
    {
        if (light != null && !groupA.Contains(light))
        {
            groupA.Add(light);
            light.autoStart = false;
            if (debugMode)
                Debug.Log($"[{intersectionID}] Added {light.GetTrafficLightID()} to Group A");
        }
    }

    public void AddLightToGroupB(TrafficLightController light)
    {
        if (light != null && !groupB.Contains(light))
        {
            groupB.Add(light);
            light.autoStart = false;
            if (debugMode)
                Debug.Log($"[{intersectionID}] Added {light.GetTrafficLightID()} to Group B");
        }
    }

    public void AddLightToGroupC(TrafficLightController light)
    {
        if (light != null && !groupC.Contains(light))
        {
            groupC.Add(light);
            light.autoStart = false;
            if (debugMode)
                Debug.Log($"[{intersectionID}] Added {light.GetTrafficLightID()} to Group C");
        }
    }

    public void RemoveLightFromAllGroups(TrafficLightController light)
    {
        groupA.Remove(light);
        groupB.Remove(light);
        groupC.Remove(light);
    }

    public string GetIntersectionID() => intersectionID;
    public string GetCurrentPhase() => currentPhase;
    public bool IsRunning() => isRunning;

    // ========================================
    // GIZMOS - VISUALIZATION
    // ========================================

    void OnDrawGizmos()
    {
        if (!debugMode) return;

        // Draw connections to Group A (Green)
        Gizmos.color = Color.green;
        foreach (var light in groupA)
        {
            if (light != null)
            {
                Gizmos.DrawLine(transform.position, light.transform.position);
                Gizmos.DrawWireSphere(light.transform.position, 1f);
            }
        }

        // Draw connections to Group B (Blue)
        Gizmos.color = Color.blue;
        foreach (var light in groupB)
        {
            if (light != null)
            {
                Gizmos.DrawLine(transform.position, light.transform.position);
                Gizmos.DrawWireSphere(light.transform.position, 1f);
            }
        }

        // Draw connections to Group C (Cyan)
        if (useGroupC)
        {
            Gizmos.color = Color.cyan;
            foreach (var light in groupC)
            {
                if (light != null)
                {
                    Gizmos.DrawLine(transform.position, light.transform.position);
                    Gizmos.DrawWireSphere(light.transform.position, 1f);
                }
            }
        }

        // Draw intersection center
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 3f);
    }

    void OnDrawGizmosSelected()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;

        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 5f,
            $"🚦 {intersectionID}\n" +
            $"━━━━━━━━━━━━━━━━━━━━━━━━\n" +
            $"Phase: {currentPhase}\n" +
            $"Group A: {groupA.Count} lights\n" +
            $"Group B: {groupB.Count} lights\n" +
            $"Group C: {(useGroupC ? groupC.Count.ToString() : "Disabled")} lights\n" +
            $"Running: {(isRunning ? "✅ YES" : "❌ NO")}",
            new GUIStyle()
            {
                normal = new GUIStyleState() { textColor = Color.white },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            }
        );
#endif
    }

    // ========================================
    // CONTEXT MENU HELPERS
    // ========================================

    [ContextMenu("Auto-Assign Nearby Lights")]
    void AutoAssignNearbyLights()
    {
        TrafficLightController[] allLights = FindObjectsOfType<TrafficLightController>();

        groupA.Clear();
        groupB.Clear();
        groupC.Clear();

        List<TrafficLightController> nearbyLights = new List<TrafficLightController>();

        foreach (var light in allLights)
        {
            float distance = Vector3.Distance(transform.position, light.transform.position);
            if (distance < 50f) // Within 50m of intersection center
            {
                nearbyLights.Add(light);
            }
        }

        // Simple distribution: alternate between groups
        for (int i = 0; i < nearbyLights.Count; i++)
        {
            if (i % 2 == 0)
                groupA.Add(nearbyLights[i]);
            else
                groupB.Add(nearbyLights[i]);
        }

        Debug.Log($"[{intersectionID}] Auto-assigned {groupA.Count} lights to Group A, {groupB.Count} to Group B");
    }

    [ContextMenu("Start Cycle")]
    void MenuStartCycle()
    {
        if (Application.isPlaying)
            StartIntersectionCycle();
    }

    [ContextMenu("Stop Cycle")]
    void MenuStopCycle()
    {
        if (Application.isPlaying)
            StopIntersectionCycle();
    }

    void OnDisable()
    {
        StopIntersectionCycle();
    }
}