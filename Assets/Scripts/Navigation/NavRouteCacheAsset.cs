// ============================================================================
//  NAV ROUTE CACHE ASSET  v2.0  —  FULL CACHE (SEGMENTS + ROUTES)
//  ============================================================================
//  Stores BOTH pre-baked NavMesh segments (Phase 1) AND pre-computed A* routes
//  (Phase 2). Designed for large scenes with 500–2000+ nodes where runtime
//  route computation would cause unacceptable startup delay.
//
//  ARCHITECTURE:
//    Editor bakes everything once → saved to this asset → loaded instantly
//    at runtime in ~10ms → NPCs spawn immediately with zero delay.
//    Any edge-case pool miss at runtime triggers ComputeRouteLive() which
//    computes on demand and caches the result for future requests.
//
//  WHEN TO RE-BAKE:
//    ✓ NavMesh rebuilt (road geometry changed)
//    ✓ Major graph restructuring (30%+ connections changed)
//    ✓ maxWaypointSpacing / waypointHeightOffset / routesPerSourceNode changed
//
//  NOT needed when:
//    ✗ Changing NPC speed, detection, or spawn settings
//    ✗ Minor node position tweaks (< 1 m)
//    ✗ Adding a few connections to an already-connected area
//
//  CREATE:  Assets → Create → Navigation → Nav Route Cache Asset
//  ASSIGN:  CentralizedNavigationSystem → Route Cache Asset field
//  BAKE:    Inspector → "Bake & Save Full Route Cache" button
// ============================================================================

using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  SERIALIZABLE DATA TYPES
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// One baked NavMesh segment — dense road-surface Vector3 waypoints between
/// two directly-connected nodes. Computed via NavMesh.CalculatePath().
/// </summary>
[System.Serializable]
public class SerializedSegment
{
    public int       fromID;
    public int       toID;
    public Vector3[] waypoints;
}

/// <summary>
/// One pre-computed full route — all dense waypoints from srcID to dstID,
/// stitched from individual segments by A* pathfinding.
/// </summary>
[System.Serializable]
public class SerializedRoute
{
    public int       srcID;
    public int       dstID;
    public Vector3[] waypoints;
}

// ─────────────────────────────────────────────────────────────────────────────
//  SCRIPTABLE OBJECT
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Persistent ScriptableObject containing the full pre-baked navigation cache:
/// NavMesh segments (Phase 1) + A* routes (Phase 2).
///
/// At runtime the entire cache is loaded into memory in ~10ms.
/// NPCs then request routes via O(1) dictionary lookups — no A*, no NavMesh.
/// Edge-case pool misses are handled by runtime live computation and cached.
/// </summary>
[CreateAssetMenu(
    fileName = "NavRouteCache",
    menuName = "Navigation/Nav Route Cache Asset",
    order    = 200)]
public class NavRouteCacheAsset : ScriptableObject
{
    // ─────────────────────────────────────────────────────────────────────────
    //  BAKE METADATA  (inspector read-only, written at bake time)
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── Bake Info (read-only) ───")]

    [Tooltip("True once a complete bake has been saved into this asset.")]
    public bool   isValid          = false;

    [Tooltip("Date and time this cache was last baked.")]
    public string bakedAt          = "";

    [Tooltip("Scene name at bake time. Used to detect wrong-scene loading.")]
    public string sceneName        = "";

    [Tooltip("Node count at bake time. Mismatch triggers stale warning.")]
    public int    nodeCount        = 0;

    [Tooltip("Connection count at bake time. Mismatch triggers stale warning.")]
    public int    connectionCount  = 0;

    [Tooltip("Total segments baked (bidirectional connection = 2 segments).")]
    public int    segmentCount     = 0;

    [Tooltip("Total routes baked across all source nodes.")]
    public int    routeCount       = 0;

    [Tooltip("Routes baked per source node.")]
    public int    routesPerNode    = 0;

    [Tooltip("maxWaypointSpacing at bake time. Re-bake if changed.")]
    public float  bakedWaypointSpacing = 0f;

    [Tooltip("waypointHeightOffset at bake time. Re-bake if changed.")]
    public float  bakedHeightOffset    = 0f;

    // ─────────────────────────────────────────────────────────────────────────
    //  CACHED DATA
    // ─────────────────────────────────────────────────────────────────────────

    [Header("─── Segment Cache (Phase 1) ───")]
    public List<SerializedSegment> segments = new List<SerializedSegment>();

    [Header("─── Route Cache (Phase 2) ───")]
    public List<SerializedRoute> routes = new List<SerializedRoute>();

    // ─────────────────────────────────────────────────────────────────────────
    //  API
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Wipe all data and mark invalid. Called before re-baking.</summary>
    public void Clear()
    {
        segments.Clear();
        routes.Clear();
        isValid               = false;
        bakedAt               = "";
        sceneName             = "";
        nodeCount             = 0;
        connectionCount       = 0;
        segmentCount          = 0;
        routeCount            = 0;
        routesPerNode         = 0;
        bakedWaypointSpacing  = 0f;
        bakedHeightOffset     = 0f;
    }

    /// <summary>
    /// Returns true if bake settings match current system settings.
    /// A mismatch means waypoint density or height is stale.
    /// </summary>
    public bool SettingsMatch(float waypointSpacing, float heightOffset, int routesPer)
    {
        return Mathf.Approximately(bakedWaypointSpacing, waypointSpacing)
            && Mathf.Approximately(bakedHeightOffset, heightOffset)
            && routesPerNode == routesPer;
    }

    /// <summary>
    /// Returns a human-readable stale-check summary for the inspector.
    /// Empty string = no issues.
    /// </summary>
    public string GetStaleWarning(int currentNodes, int currentConnections,
                                  float waypointSpacing, float heightOffset, int routesPer)
    {
        var issues = new System.Text.StringBuilder();

        if (currentNodes != nodeCount)
            issues.AppendLine($"• Node count changed (was {nodeCount}, now {currentNodes})");

        if (currentConnections != connectionCount)
            issues.AppendLine($"• Connection count changed (was {connectionCount}, now {currentConnections})");

        if (!Mathf.Approximately(bakedWaypointSpacing, waypointSpacing))
            issues.AppendLine($"• maxWaypointSpacing changed ({bakedWaypointSpacing} → {waypointSpacing})");

        if (!Mathf.Approximately(bakedHeightOffset, heightOffset))
            issues.AppendLine($"• waypointHeightOffset changed ({bakedHeightOffset} → {heightOffset})");

        if (routesPerNode != routesPer)
            issues.AppendLine($"• routesPerSourceNode changed ({routesPerNode} → {routesPer})");

        return issues.ToString();
    }
}