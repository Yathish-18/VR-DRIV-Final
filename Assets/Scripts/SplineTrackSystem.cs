using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// ================== SPLINE SYSTEM ==================



public class TrackSpline : MonoBehaviour
{
    [Header("Spline Settings")]
    public List<SplineWaypoint> waypoints = new List<SplineWaypoint>();
    public bool isLooped = true;
    public int resolution = 50; // Points per segment
    public float totalLength = 0f;

    [Header("Debug Visualization")]
    public bool showSpline = true;
    public bool showWaypoints = true;
    public bool showHandles = true;
    public Color splineColor = Color.green;
    public Color waypointColor = Color.red;
    public Color handleColor = Color.yellow;

    // Cached spline data
    private List<Vector3> splinePoints = new List<Vector3>();
    private List<float> cumulativeDistances = new List<float>();
    private List<Vector3> splineDirections = new List<Vector3>();
    private List<float> splineWidths = new List<float>();
    private List<float> splineSpeedLimits = new List<float>();

    void Start()
    {
        GenerateSpline();
    }

    void OnValidate()
    {
        if (waypoints.Count > 1)
        {
            GenerateSpline();
        }
    }

    public void GenerateSpline()
    {
        if (waypoints.Count < 2) return;

        splinePoints.Clear();
        cumulativeDistances.Clear();
        splineDirections.Clear();
        splineWidths.Clear();
        splineSpeedLimits.Clear();

        totalLength = 0f;

        int segments = isLooped ? waypoints.Count : waypoints.Count - 1;

        for (int i = 0; i < segments; i++)
        {
            int nextIndex = (i + 1) % waypoints.Count;

            SplineWaypoint current = waypoints[i];
            SplineWaypoint next = waypoints[nextIndex];

            // Generate cubic Bezier curve between waypoints
            for (int j = 0; j < resolution; j++)
            {
                float t = (float)j / resolution;

                Vector3 point = CalculateBezierPoint(t,
                    current.position,
                    current.position + current.handleOut,
                    next.position + next.handleIn,
                    next.position);

                splinePoints.Add(point);

                // Calculate cumulative distance
                if (splinePoints.Count > 1)
                {
                    float distance = Vector3.Distance(splinePoints[splinePoints.Count - 2], point);
                    totalLength += distance;
                }
                cumulativeDistances.Add(totalLength);

                // Interpolate properties
                float width = Mathf.Lerp(current.trackWidth, next.trackWidth, t);
                float speedLimit = Mathf.Lerp(current.speedLimit, next.speedLimit, t);

                splineWidths.Add(width);
                splineSpeedLimits.Add(speedLimit);

                // Calculate direction
                Vector3 direction = Vector3.forward;
                if (splinePoints.Count > 1)
                {
                    direction = (point - splinePoints[splinePoints.Count - 2]).normalized;
                }
                splineDirections.Add(direction);
            }
        }
    }

    Vector3 CalculateBezierPoint(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1 - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector3 point = uuu * p0;
        point += 3 * uu * t * p1;
        point += 3 * u * tt * p2;
        point += ttt * p3;

        return point;
    }

    public SplineData GetNearestPointOnSpline(Vector3 worldPosition)
    {
        if (splinePoints.Count == 0) return null;

        float minDistance = float.MaxValue;
        int nearestIndex = 0;

        // Find nearest spline point
        for (int i = 0; i < splinePoints.Count; i++)
        {
            float distance = Vector3.Distance(worldPosition, splinePoints[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        // Get precise point on spline segment
        Vector3 nearestPoint = splinePoints[nearestIndex];
        Vector3 splineDirection = splineDirections[nearestIndex];

        // Calculate lateral offset (perpendicular distance from spline)
        Vector3 toPosition = worldPosition - nearestPoint;
        Vector3 right = Vector3.Cross(splineDirection, Vector3.up).normalized;
        float lateralOffset = Vector3.Dot(toPosition, right);

        // Calculate forward offset along spline
        float forwardOffset = Vector3.Dot(toPosition, splineDirection);

        return new SplineData
        {
            nearestPoint = nearestPoint,
            splineIndex = nearestIndex,
            distanceFromSpline = minDistance,
            lateralOffset = lateralOffset,
            forwardOffset = forwardOffset,
            splineDirection = splineDirection,
            splineRight = right,
            trackWidth = splineWidths[nearestIndex],
            speedLimit = splineSpeedLimits[nearestIndex],
            distanceAlongSpline = cumulativeDistances[nearestIndex],
            normalizedDistance = cumulativeDistances[nearestIndex] / totalLength
        };
    }

    public Vector3 GetSplinePosition(float normalizedDistance)
    {
        if (splinePoints.Count == 0) return Vector3.zero;

        float targetDistance = normalizedDistance * totalLength;

        for (int i = 0; i < cumulativeDistances.Count - 1; i++)
        {
            if (targetDistance <= cumulativeDistances[i + 1])
            {
                float t = (targetDistance - cumulativeDistances[i]) /
                         (cumulativeDistances[i + 1] - cumulativeDistances[i]);
                return Vector3.Lerp(splinePoints[i], splinePoints[i + 1], t);
            }
        }

        return splinePoints[splinePoints.Count - 1];
    }

    void OnDrawGizmos()
    {
        if (!showSpline || splinePoints.Count < 2) return;

        // Draw spline
        Gizmos.color = splineColor;
        for (int i = 0; i < splinePoints.Count - 1; i++)
        {
            Gizmos.DrawLine(splinePoints[i], splinePoints[i + 1]);
        }

        if (isLooped && splinePoints.Count > 0)
        {
            Gizmos.DrawLine(splinePoints[splinePoints.Count - 1], splinePoints[0]);
        }

        // Draw waypoints
        if (showWaypoints)
        {
            Gizmos.color = waypointColor;
            foreach (var waypoint in waypoints)
            {
                Gizmos.DrawWireSphere(waypoint.position, 1f);
            }
        }

        // Draw handles
        if (showHandles)
        {
            Gizmos.color = handleColor;
            foreach (var waypoint in waypoints)
            {
                Gizmos.DrawLine(waypoint.position, waypoint.position + waypoint.handleIn);
                Gizmos.DrawLine(waypoint.position, waypoint.position + waypoint.handleOut);
                Gizmos.DrawWireSphere(waypoint.position + waypoint.handleIn, 0.3f);
                Gizmos.DrawWireSphere(waypoint.position + waypoint.handleOut, 0.3f);
            }
        }

        // Draw track boundaries
        if (splinePoints.Count > 0)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < splinePoints.Count; i++)
            {
                Vector3 right = Vector3.Cross(splineDirections[i], Vector3.up).normalized;
                Vector3 leftBoundary = splinePoints[i] - right * (splineWidths[i] * 0.5f);
                Vector3 rightBoundary = splinePoints[i] + right * (splineWidths[i] * 0.5f);

                if (i % 10 == 0) // Draw every 10th boundary line
                {
                    Gizmos.DrawLine(leftBoundary, rightBoundary);
                }
            }
        }
    }
}

public class SplineData
{
    public Vector3 nearestPoint;
    public int splineIndex;
    public float distanceFromSpline;
    public float lateralOffset; // Positive = right of spline, negative = left
    public float forwardOffset;
    public Vector3 splineDirection;
    public Vector3 splineRight;
    public float trackWidth;
    public float speedLimit;
    public float distanceAlongSpline;
    public float normalizedDistance;

    public bool IsOffTrack()
    {
        return Mathf.Abs(lateralOffset) > trackWidth * 0.5f;
    }

    public float GetOffTrackDistance()
    {
        float halfWidth = trackWidth * 0.5f;
        return Mathf.Max(0, Mathf.Abs(lateralOffset) - halfWidth);
    }
}
[System.Serializable]
public class SplineWaypoint
{
    public Vector3 position;
    public Vector3 handleIn;
    public Vector3 handleOut;
    public float speedLimit = 50f;
    public float trackWidth = 10f;

    public SplineWaypoint(Vector3 pos)
    {
        position = pos;
        handleIn = pos + Vector3.left * 2f;
        handleOut = pos + Vector3.right * 2f;
    }
}