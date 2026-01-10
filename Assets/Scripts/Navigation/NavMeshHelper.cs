using UnityEngine;

public static class NavMeshHelper
{
    /// <summary>
    /// Advanced road surface detection with multiple fallback methods
    /// </summary>
    public static Vector3 GetRoadSurfacePosition(Vector3 pos, LayerMask roadLayerMask, float raycastDistance = 50f, float raycastUpOffset = 10f)
    {
        // Method 1: Raycast from above
        Vector3 rayStart = new Vector3(pos.x, pos.y + raycastUpOffset, pos.z);
        RaycastHit hit;
        if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance + raycastUpOffset, roadLayerMask))
            return hit.point;

        // Method 2: Raycast from current position downward
        if (Physics.Raycast(pos, Vector3.down, out hit, raycastDistance, roadLayerMask))
            return hit.point;

        // Method 3: Raycast from current position upward (for elevated roads)
        if (Physics.Raycast(pos, Vector3.up, out hit, raycastDistance, roadLayerMask))
            return hit.point;

        // Method 4: SphereCast for wider detection
        if (Physics.SphereCast(rayStart, 1f, Vector3.down, out hit, raycastDistance + raycastUpOffset, roadLayerMask))
            return hit.point;

        // Method 5: BoxCast for even wider detection
        Vector3 boxSize = new Vector3(2f, 0.1f, 2f);
        if (Physics.BoxCast(rayStart, boxSize / 2f, Vector3.down, out hit, Quaternion.identity, raycastDistance + raycastUpOffset, roadLayerMask))
            return hit.point;

        return pos; // fallback: original position if no road found
    }

    /// <summary>
    /// Get road surface position with additional height offset
    /// </summary>
    public static Vector3 GetRoadSurfacePositionWithOffset(Vector3 pos, LayerMask roadLayerMask, float heightOffset = 0.5f, float raycastDistance = 50f, float raycastUpOffset = 10f)
    {
        Vector3 roadPos = GetRoadSurfacePosition(pos, roadLayerMask, raycastDistance, raycastUpOffset);
        return new Vector3(roadPos.x, roadPos.y + heightOffset, roadPos.z);
    }
}
