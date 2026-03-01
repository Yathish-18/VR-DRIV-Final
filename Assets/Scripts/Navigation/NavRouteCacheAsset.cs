// ============================================================================
//  NAV ROUTE CACHE ASSET  —  SEGMENT CACHE ONLY
//  ============================================================================
//  Stores pre-baked NavMesh segments (Phase 1 data only).
//
//  WHY segments only, not routes:
//    Segments depend on NavMesh geometry → stale ONLY when roads change
//    Routes   depend on node graph       → stale whenever nodes/connections change
//    Routes compute in < 100ms from cached segments → not worth saving to disk
//
//  RESULT:
//    Asset size ~170 KB  (vs ~3 MB if routes were also stored)
//    Re-bake needed only when NavMesh is rebuilt
//    Moving nodes or editing connections NEVER requires a re-bake
//
//  SETUP:
//    1. Assets → Create → Navigation → Nav Route Cache Asset
//    2. Assign to CentralizedNavigationSystem → Route Cache Asset field
//    3. Press "Bake & Save Segment Cache" in the inspector
// ============================================================================

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// One baked NavMesh segment — dense road-surface Vector3 waypoints
/// between two directly-connected nodes. Saved in editor, loaded at runtime.
/// </summary>
[System.Serializable]
public class SerializedSegment
{
    public int       fromID;
    public int       toID;
    public Vector3[] waypoints;
}

/// <summary>
/// Persistent ScriptableObject containing all pre-baked NavMesh segments.
/// Created once in the editor, loaded instantly at every Play session.
/// </summary>
[CreateAssetMenu(
    fileName = "NavSegmentCache",
    menuName = "Navigation/Nav Route Cache Asset",
    order    = 200)]
public class NavRouteCacheAsset : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    //  BAKE METADATA  (shown in inspector, read-only at runtime)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── Bake Info (read-only) ───")]

    [Tooltip("True once a valid bake has been saved into this asset.")]
    public bool   isValid           = false;

    [Tooltip("Date and time this cache was last baked.")]
    public string bakedAt           = "";

    [Tooltip("Scene that was active when this cache was baked.")]
    public string sceneName         = "";

    [Tooltip("Number of nodes in the graph at bake time.")]
    public int    nodeCount         = 0;

    [Tooltip("Number of connections at bake time.")]
    public int    connectionCount   = 0;

    [Tooltip("Total segments baked (bidirectional connection = 2 segments).")]
    public int    segmentCount      = 0;

    [Tooltip("maxWaypointSpacing at bake time — re-bake if this changes.")]
    public float  bakedWaypointSpacing = 0f;

    [Tooltip("waypointHeightOffset at bake time — re-bake if this changes.")]
    public float  bakedHeightOffset    = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    //  SEGMENT DATA
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── Segment Data ───")]
    public List<SerializedSegment> segments = new List<SerializedSegment>();

    // ─────────────────────────────────────────────────────────────────────────
    //  API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Wipe all data and mark invalid. Called before re-baking.</summary>
    public void Clear()
    {
        segments.Clear();
        isValid               = false;
        bakedAt               = "";
        sceneName             = "";
        nodeCount             = 0;
        connectionCount       = 0;
        segmentCount          = 0;
        bakedWaypointSpacing  = 0f;
        bakedHeightOffset     = 0f;
    }

    /// <summary>
    /// Returns true if bake settings still match current system settings.
    /// A mismatch means segment density or height is wrong — re-bake recommended.
    /// </summary>
    public bool SettingsMatch(float waypointSpacing, float heightOffset)
    {
        return Mathf.Approximately(bakedWaypointSpacing, waypointSpacing)
            && Mathf.Approximately(bakedHeightOffset, heightOffset);
    }
}