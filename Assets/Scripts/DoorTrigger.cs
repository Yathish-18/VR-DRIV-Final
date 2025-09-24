using NWH.VehiclePhysics2;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GenericTriggerTimer : MonoBehaviour
{
    [Header("Trigger Settings")]
    public List<string> targetTags = new List<string> { "Player" };
    public float waitTime = 2f; // Time object must wait in trigger
    public bool allowMultipleObjects = false; // Allow multiple objects to trigger at once

    [Header("Unity Events")]
    [Space(5)]
    [Tooltip("Called when any target object enters the trigger")]
    public UnityEvent OnObjectEnter;

    [Tooltip("Called when any target object exits the trigger")]
    public UnityEvent OnObjectExit;

    [Tooltip("Called when object(s) stay in trigger for the full wait time")]
    public UnityEvent OnTimerComplete;

    [Tooltip("Called when timer is interrupted (object leaves before wait time)")]
    public UnityEvent OnTimerInterrupted;

    [Header("Advanced Events")]
    [Space(5)]
    [Tooltip("Called when timer starts counting")]
    public UnityEvent OnTimerStart;

    [Tooltip("Called when object exits and reset is enabled")]
    public UnityEvent OnReset;

    [Header("Timer Settings")]
    public bool resetOnExit = true; // Call OnReset when object leaves
    public bool repeatTimer = false; // Allow timer to trigger multiple times
    public bool requireContinuousStay = true; // Object must stay for full duration

    [Header("Debug")]
    public bool showDebugMessages = true;
    public bool showTimerProgress = false;

    [Header("Gizmo Settings")]
    public Color gizmoColorEmpty = Color.yellow;
    public Color gizmoColorOccupied = Color.green;
    public Color gizmoColorTimerActive = Color.red;

    private HashSet<Collider> objectsInTrigger = new HashSet<Collider>();
    private bool timerCompleted = false;
    private bool timerActive = false;
    private Coroutine waitCoroutine;

    void Start()
    {
        // Ensure this GameObject has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"{gameObject.name}: No Collider component found! Adding a Box Collider.");
            col = gameObject.AddComponent<BoxCollider>();
        }
        col.isTrigger = true;

        // Initialize with default tag if list is empty
        if (targetTags.Count == 0)
        {
            targetTags.Add("Player");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log(other.tag+other.name);
        if (IsTargetObject(other))
        {
            objectsInTrigger.Add(other);

            if (showDebugMessages)
                Debug.Log($"{other.name} entered trigger zone. Objects in trigger: {objectsInTrigger.Count}");

            // Invoke object enter event
            OnObjectEnter?.Invoke();

            // Start timer if conditions are met
            if (ShouldStartTimer())
            {
                StartTimer();
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsTargetObject(other) && objectsInTrigger.Contains(other))
        {
            objectsInTrigger.Remove(other);

            if (showDebugMessages)
                Debug.Log($"{other.name} left trigger zone. Objects in trigger: {objectsInTrigger.Count}");

            // Invoke object exit event
            OnObjectExit?.Invoke();

            // Handle timer interruption or reset
            if (requireContinuousStay && objectsInTrigger.Count == 0)
            {
                InterruptTimer();
            }

            // Reset if enabled and no objects remain
            if (resetOnExit && objectsInTrigger.Count == 0 && timerCompleted)
            {
                ResetTrigger();
            }
        }
    }

    private bool IsTargetObject(Collider other)
    {
        // Check if the object or its parent has a car_controller component
        var carController = other.GetComponentInParent<VehicleController>();
        return carController != null;
    }


    private bool ShouldStartTimer()
    {
        // Don't start if already completed and not repeatable
        if (timerCompleted && !repeatTimer)
            return false;

        // Don't start if timer is already active
        if (timerActive)
            return false;

        // Check if we have objects in trigger
        if (objectsInTrigger.Count == 0)
            return false;

        // If multiple objects not allowed, only start with exactly one object
        if (!allowMultipleObjects && objectsInTrigger.Count > 1)
            return false;

        return true;
    }

    private void StartTimer()
    {
        if (waitCoroutine != null)
            StopCoroutine(waitCoroutine);

        waitCoroutine = StartCoroutine(WaitCoroutine());
    }

    private void InterruptTimer()
    {
        if (timerActive)
        {
            if (waitCoroutine != null)
            {
                StopCoroutine(waitCoroutine);
                waitCoroutine = null;
            }

            timerActive = false;

            if (showDebugMessages)
                Debug.Log("Timer interrupted!");

            OnTimerInterrupted?.Invoke();
        }
    }

    private void ResetTrigger()
    {
        timerCompleted = false;

        if (showDebugMessages)
            Debug.Log("Trigger reset!");

        OnReset?.Invoke();
    }

    private IEnumerator WaitCoroutine()
    {
        timerActive = true;
        float timer = 0f;

        if (showDebugMessages)
            Debug.Log("Timer started!");

        OnTimerStart?.Invoke();

        while (timer < waitTime)
        {
            // Check if we still have objects in trigger (if required)
            if (requireContinuousStay && objectsInTrigger.Count == 0)
            {
                InterruptTimer();
                yield break;
            }

            timer += Time.deltaTime;

            if (showTimerProgress && showDebugMessages)
                Debug.Log($"Timer progress: {timer:F1}s / {waitTime}s");

            yield return null;
        }

        // Timer completed successfully
        timerActive = false;
        timerCompleted = true;

        if (showDebugMessages)
            Debug.Log("Timer completed! Invoking OnTimerComplete event.");

        OnTimerComplete?.Invoke();

        waitCoroutine = null;
    }

    #region Public Methods

    /// <summary>
    /// Manually trigger the timer completion
    /// </summary>
    public void ManualTriggerComplete()
    {
        if (showDebugMessages)
            Debug.Log("Manual trigger complete called.");

        OnTimerComplete?.Invoke();
    }

    /// <summary>
    /// Manually reset the trigger
    /// </summary>
    public void ManualReset()
    {
        InterruptTimer();
        ResetTrigger();
    }

    /// <summary>
    /// Change the wait time
    /// </summary>
    public void SetWaitTime(float newWaitTime)
    {
        waitTime = Mathf.Max(0f, newWaitTime);
    }

    /// <summary>
    /// Add a new target tag
    /// </summary>
    public void AddTargetTag(string tag)
    {
        if (!targetTags.Contains(tag))
        {
            targetTags.Add(tag);
        }
    }

    /// <summary>
    /// Remove a target tag
    /// </summary>
    public void RemoveTargetTag(string tag)
    {
        targetTags.Remove(tag);
    }

    /// <summary>
    /// Check if any target objects are in trigger
    /// </summary>
    public bool HasObjectsInTrigger()
    {
        return objectsInTrigger.Count > 0;
    }

    /// <summary>
    /// Get count of objects in trigger
    /// </summary>
    public int GetObjectCount()
    {
        return objectsInTrigger.Count;
    }

    /// <summary>
    /// Check if timer is currently active
    /// </summary>
    public bool IsTimerActive()
    {
        return timerActive;
    }

    /// <summary>
    /// Check if timer has been completed
    /// </summary>
    public bool IsTimerCompleted()
    {
        return timerCompleted;
    }

    #endregion

    void OnDrawGizmosSelected()
    {
        // Choose gizmo color based on state
        Color gizmoColor = gizmoColorEmpty;

        if (timerActive)
            gizmoColor = gizmoColorTimerActive;
        else if (objectsInTrigger.Count > 0)
            gizmoColor = gizmoColorOccupied;

        Gizmos.color = gizmoColor;

        // Draw trigger bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            if (col is BoxCollider boxCol)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawWireCube(boxCol.center, boxCol.size);
            }
            else if (col is SphereCollider sphereCol)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawWireSphere(sphereCol.center, sphereCol.radius);
            }
        }

        // Reset matrix
        Gizmos.matrix = Matrix4x4.identity;
    }
}