using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// SeatAnchor — VR Racing Game
/// 
/// Attach this script to the VR Camera Rig (or XR Origin) root object.
/// It smooths positional jitter from physics and applies speed-based dampening
/// so the faster the car goes, the more the seat "absorbs" road vibration.
/// 
/// SETUP:
///   1. Attach SeatAnchor to your XR Origin / Camera Rig GameObject.
///   2. Assign 'carRigidbody' to your car's Rigidbody in the Inspector.
///   3. The rig will automatically follow the car's anchor point each frame.
///   4. Tune the exposed fields in the Inspector to taste.
/// </summary>
public class SeatAnchor : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Rigidbody of the car this seat belongs to.")]
    public Rigidbody carRigidbody;

    [Tooltip("The exact seat/cockpit point the VR rig should track. " +
             "Leave null to use the car Rigidbody's transform.")]
    public Transform seatAnchorPoint;

    // ── Positional Smoothing ────────────────────────────────────────────────

    [Header("Positional Smoothing")]
    [Tooltip("Base smoothing speed (lower = smoother, higher = snappier). " +
             "Good starting range: 8–20.")]
    [Range(1f, 40f)]
    public float baseSmoothSpeed = 12f;

    [Tooltip("Maximum distance the rig is allowed to lag behind the anchor " +
             "before it hard-snaps to catch up (prevents motion sickness drift).")]
    [Range(0.05f, 1f)]
    public float maxLagDistance = 0.15f;

    // ── Speed-Based Dampening ───────────────────────────────────────────────

    [Header("Speed-Based Dampening")]
    [Tooltip("Car speed (m/s) at which dampening begins to increase. " +
             "~72 km/h = 20 m/s")]
    [Range(0f, 50f)]
    public float dampingStartSpeed = 10f;

    [Tooltip("Car speed (m/s) at which dampening is fully maxed out. " +
             "~180 km/h = 50 m/s")]
    [Range(10f, 100f)]
    public float dampingMaxSpeed = 50f;

    [Tooltip("Additional smoothing multiplier applied at max speed. " +
             "1 = no extra dampening, 3 = triple the smoothing at top speed.")]
    [Range(1f, 6f)]
    public float maxSpeedDampingMultiplier = 3f;

    [Tooltip("How quickly the damping factor itself changes (prevents " +
             "jarring pop when speed changes suddenly). Range: 2–8.")]
    [Range(0.5f, 10f)]
    public float dampingSmoothRate = 4f;

    // ── Advanced ────────────────────────────────────────────────────────────

    [Header("Advanced")]
    [Tooltip("Freeze vertical smoothing — keeps the seat height locked to the " +
             "anchor exactly. Useful if your car has suspension bounce you want " +
             "to fully absorb vertically.")]
    public bool smoothVertical = true;

    [Tooltip("Enable to print current speed and damping factor to the console. " +
             "Disable in builds.")]
    public bool debugLog = false;

    // ── Private state ───────────────────────────────────────────────────────

    private float _currentDampingFactor = 1f;   // smoothly interpolated
    private Vector3 _smoothedPosition;
    private bool _initialised = false;

    // ───────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (carRigidbody == null)
        {
            Debug.LogWarning("[SeatAnchor] No carRigidbody assigned. " +
                             "Please assign one in the Inspector.");
        }

        // If no explicit anchor point is set, fall back to the car transform.
        if (seatAnchorPoint == null && carRigidbody != null)
            seatAnchorPoint = carRigidbody.transform;
    }

    private void OnEnable()
    {
        // Snap immediately on enable so there's no initial lerp swoosh.
        if (seatAnchorPoint != null)
        {
            _smoothedPosition = seatAnchorPoint.position;
            transform.position = _smoothedPosition;
            _initialised = true;
        }
    }

    // Use LateUpdate so physics has already settled this frame.
    private void LateUpdate()
    {
        if (!_initialised || seatAnchorPoint == null || carRigidbody == null)
            return;

        // ── 1. Determine current car speed ─────────────────────────────────
        float speed = carRigidbody.linearVelocity.magnitude; // metres per second

        // ── 2. Compute target damping factor ───────────────────────────────
        // Normalise speed into 0-1 range between dampingStartSpeed and dampingMaxSpeed.
        float speedT = Mathf.InverseLerp(dampingStartSpeed, dampingMaxSpeed, speed);
        float targetDamping = Mathf.Lerp(1f, maxSpeedDampingMultiplier, speedT);

        // Smooth the damping factor so it doesn't snap on sudden speed changes.
        _currentDampingFactor = Mathf.Lerp(
            _currentDampingFactor,
            targetDamping,
            Time.deltaTime * dampingSmoothRate
        );

        // ── 3. Calculate effective smooth speed ────────────────────────────
        // Higher damping factor → lower effective smooth speed → more lag absorption.
        float effectiveSmoothSpeed = baseSmoothSpeed / _currentDampingFactor;

        // ── 4. Lerp position toward anchor ─────────────────────────────────
        Vector3 targetPosition = seatAnchorPoint.position;

        if (!smoothVertical)
        {
            // Lock vertical — only smooth X/Z
            float lockedY = targetPosition.y;
            _smoothedPosition = Vector3.Lerp(
                _smoothedPosition,
                targetPosition,
                Time.deltaTime * effectiveSmoothSpeed
            );
            _smoothedPosition.y = lockedY;
        }
        else
        {
            _smoothedPosition = Vector3.Lerp(
                _smoothedPosition,
                targetPosition,
                Time.deltaTime * effectiveSmoothSpeed
            );
        }

        // ── 5. Hard-snap if we've drifted too far (safety net) ─────────────
        if (Vector3.Distance(_smoothedPosition, targetPosition) > maxLagDistance)
        {
            _smoothedPosition = Vector3.MoveTowards(
                _smoothedPosition,
                targetPosition,
                Vector3.Distance(_smoothedPosition, targetPosition) - maxLagDistance
            );
        }

        // ── 6. Apply to the rig ────────────────────────────────────────────
        // Only position is modified; rotation is left to the XR subsystem.
        transform.position = _smoothedPosition;

        // ── 7. Optional debug output ───────────────────────────────────────
        if (debugLog)
        {
            Debug.Log($"[SeatAnchor] Speed: {speed * 3.6f:F1} km/h | " +
                      $"DampFactor: {_currentDampingFactor:F2} | " +
                      $"EffectiveSmooth: {effectiveSmoothSpeed:F2}");
        }
    }

    // ── Editor helpers ──────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        if (seatAnchorPoint == null) return;

        // Draw the max-lag safety sphere
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.DrawSphere(seatAnchorPoint.position, maxLagDistance);

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.9f);
        Gizmos.DrawWireSphere(seatAnchorPoint.position, maxLagDistance);

        // Line from rig to anchor
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, seatAnchorPoint.position);
    }
}