// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.RoadConstructor
{
    internal static class WaypointUtility
    {
        public static void CreateWaypoints(List<RoadObject> roadObjects, List<IntersectionObject> intersectionObjects,
            TrafficLaneType trafficLaneType, Vector2 maxDistance)
        {
            CreateWaypointsInternal(roadObjects, intersectionObjects, trafficLaneType, maxDistance);    
            ConnectWaypoints(roadObjects, intersectionObjects, trafficLaneType);
        }
        
        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        private static void CreateWaypointsInternal(List<RoadObject> roadObjects, List<IntersectionObject> intersectionObjects,
            TrafficLaneType trafficLaneType, Vector2 maxDistance)
        {
            var sceneObjects = new List<SceneObject>();
            sceneObjects.AddRange(roadObjects);
            sceneObjects.AddRange(intersectionObjects);

            /********************************************************************************************************************************/
            // Destroying existing (also done when removing traffic component)
            for (var i = 0; i < sceneObjects.Count; i++)
            {
                var trafficLanes = sceneObjects[i].GetTrafficLanes(trafficLaneType);
                for (var j = 0; j < trafficLanes.Count; j++) trafficLanes[j].RemoveWaypoints();
            }

            /********************************************************************************************************************************/
            // Adding adjacent from own road first

            for (var i = 0; i < sceneObjects.Count; i++)
            {
                var trafficLanes = sceneObjects[i].GetTrafficLanes(trafficLaneType);
                
                for (var j = 0; j < trafficLanes.Count; j++)
                {
                    var laneWaypoints = new List<Waypoint>();
                    var trafficSpline = trafficLanes[j].spline;
                    var heightSpline = trafficLanes[j].heightSpline;

                    var tanSpline = heightSpline[^1].Position - heightSpline[0].Position; // Used for getting height from main spline
                    var tanTrafficSpline = trafficSpline[^1].Position - trafficSpline[0].Position;
                    var forwardDirection = PGTrigonometryUtility.IsSameDirectionXZ(tanSpline, tanTrafficSpline);
                    
                    var startEndGap = maxDistance.y * 0.5f;
                    if (trafficSpline.Closed)
                    {
                        startEndGap *= 0.5f;
                    }
                    
                    var splineLength = trafficSpline.GetLength();
                    var trafficSplineLength = splineLength - startEndGap * 2;

                    if (splineLength > 0 && trafficSplineLength <= 0)
                    {
                        trafficSplineLength = splineLength * 0.5f;
                    }

                    var curvature = ConstructionSplineUtility.GetCurvature(trafficSpline.Knots.First(), trafficSpline.Knots.Last());
                    if (trafficSpline.Closed)
                    {
                        curvature = float.MaxValue;
                    }
                    var t = Mathf.Clamp01(curvature / 90f);
                    var distance = Mathf.Lerp(maxDistance.y, maxDistance.x, t);
                    distance = math.max(0.01f, distance);

                    var totalWaypoints = Mathf.Max(Mathf.RoundToInt(trafficSplineLength / distance), 0);
                    totalWaypoints += 2;

                    var adjustedEvaluateBase = startEndGap / (trafficSplineLength + startEndGap * 2);
                    for (var k = 0; k < totalWaypoints; k++)
                    {
                        var evaluate = (float) k / (totalWaypoints - 1);
                        if (totalWaypoints == 2) evaluate = k == 0 ? 0.25f : 0.75f;

                        var adjustedEvaluate =
                            Mathf.Clamp01(evaluate * (trafficSplineLength / (trafficSplineLength + startEndGap * 2)) + adjustedEvaluateBase);
                        var heightPosition = heightSpline.EvaluatePosition(adjustedEvaluate); // Getting height from main spline

                        if (!forwardDirection) adjustedEvaluate = 1f - adjustedEvaluate;

                        var waypointPosition = trafficSpline.EvaluatePosition(adjustedEvaluate);
                        waypointPosition.y = heightPosition.y;

                        var waypointObj = new GameObject();
                        var waypoint = waypointObj.AddComponent<Waypoint>();
                        {
                            waypoint.roadID = sceneObjects[i].name;
                            waypoint.laneType = trafficLanes[j].trafficLaneType;
                            waypoint.direction = trafficLanes[j].direction;
                            waypoint.laneWidth = trafficLanes[j].width;
                        }

                        laneWaypoints.Add(waypoint);
                        waypointObj.name = waypoint.laneType + "_" + j + "_" + (laneWaypoints.Count - 1);
                        waypointObj.transform.position = waypointPosition;
                        waypointObj.transform.SetParent(sceneObjects[i].traffic.gameObject.transform);
                    }

                    if (!forwardDirection) laneWaypoints.Reverse();

                    for (var k = 0; k < laneWaypoints.Count; k++)
                    {
                        var waypoint = laneWaypoints[k];
                        if (k > 0) waypoint.prev.Add(laneWaypoints[k - 1]);
                        if (k < laneWaypoints.Count - 1) waypoint.next.Add(laneWaypoints[k + 1]);
                    }

                    if (!trafficSpline.Closed && laneWaypoints.Count > 0)
                    {
                        laneWaypoints[0].startPoint = true;
                        laneWaypoints[^1].endPoint = true;
                    }

                    if (trafficSpline.Closed && laneWaypoints.Count > 1)
                    {
                        laneWaypoints[0].prev.Add(laneWaypoints[^1]);
                        laneWaypoints[^1].next.Add(laneWaypoints[0]);
                    }

#if UNITY_EDITOR
                    for (var k = 0; k < laneWaypoints.Count; k++) EditorUtility.SetDirty(laneWaypoints[k]);
#endif

                    trafficLanes[j].SetWaypoints(laneWaypoints);
                }
            }
        }

        
        /********************************************************************************************************************************/
        // Now connecting waypoints
        
        private static void ConnectWaypoints(List<RoadObject> roadObjects, List<IntersectionObject> intersectionObjects, TrafficLaneType trafficLaneType)
        {
            var sceneObjects = new List<SceneObject>(roadObjects);
            sceneObjects.AddRange(intersectionObjects);
            
            var checkedIDs = new HashSet<Vector2Int>();

            const float tolerance = Constants.MaxConnectionDistance;
            const float squaredTolerance = tolerance * tolerance;

            for (var i = 0; i < sceneObjects.Count; i++)
            {
                var sceneObject = sceneObjects[i];
                
                var connections = sceneObject.Connections;
                if (connections.Count == 0) continue;
                
                var trafficLanes = sceneObject.GetTrafficLanes(trafficLaneType);
                
                /********************************************************************************************************************************/
                // Connections
                
                for (int j = 0; j < connections.Count; j++)
                {
                    var id1 = sceneObject.GetInstanceID();
                    var id2 = connections[j].GetInstanceID();
                    var connectionID = new Vector2Int(Mathf.Min(id1, id2), Mathf.Max(id1, id2));
                    if (!checkedIDs.Add(connectionID))
                    {
                        continue;
                    }
                    
                    var trafficLanesConnection = connections[j].GetTrafficLanes(trafficLaneType);
                    if (trafficLanesConnection.Count == 0) continue;

                    for (var k = 0; k < trafficLanes.Count; k++)
                    {
                        var trafficLane = trafficLanes[k];
                        var waypoints = trafficLane.GetWaypoints();
                        if (waypoints.Count == 0) continue;

                        var knotFirst = trafficLane.spline[0];
                        var knotLast = trafficLane.spline[^1];
                        var waypointFirst = waypoints[0];
                        var waypointLast = waypoints[^1];

                        for (int l = 0; l < trafficLanesConnection.Count; l++)
                        {
                            var trafficLaneConnection = trafficLanesConnection[l];
                            if (trafficLane.trafficLaneType != trafficLaneConnection.trafficLaneType) continue;

                            var connectionWaypoints = trafficLaneConnection.GetWaypoints();
                            if (connectionWaypoints.Count == 0) continue;

                            // Incoming
                            var connectionWaypointsLast = connectionWaypoints[^1];
                            var connectionKnotLast = trafficLaneConnection.spline[^1];
                            if (math.distancesq(knotFirst.Position, connectionKnotLast.Position) <= squaredTolerance)
                            {
                                ConnectTwoWaypoints(connectionWaypointsLast, waypointFirst);
                            }

                            // Outgoing
                            var connectionWaypointsFirst = connectionWaypoints[0];
                            var connectionKnotFirst = trafficLaneConnection.spline[0];
                            if (math.distancesq(knotLast.Position, connectionKnotFirst.Position) <= squaredTolerance)
                            {
                                ConnectTwoWaypoints(waypointLast, connectionWaypointsFirst);
                            }
                        }
                    }
                }
                
                /********************************************************************************************************************************/
                // Crossings
                    
                for (var k = 0; k < trafficLanes.Count; k++)
                {
                    var trafficLane = trafficLanes[k];
                    if (!trafficLane.crossing) continue;
                    var waypoints = trafficLane.GetWaypoints();
                    if (waypoints.Count == 0) continue;
                    
                    // Incoming
                    
                    var closestFound = false;
                    var closestDistance = float.MaxValue;
                    TrafficLane closestLane = default;
                    var firstWaypoint = waypoints[0];

                    for (int l = 0; l < trafficLanes.Count; l++)
                    {
                        if (l == k) continue;
                        var otherTrafficLane = trafficLanes[l];
                        if (otherTrafficLane.trafficLaneType != TrafficLaneType.Pedestrian) continue;
                        if (otherTrafficLane.crossing) continue;
                        if (otherTrafficLane.waypoints.Count == 0) continue;

                        var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(otherTrafficLane.spline, firstWaypoint.transform.position);
                        var nearestKnot = otherTrafficLane.spline[nearestKnotIndex]; 

                        var distance = math.distancesq(firstWaypoint.transform.position, nearestKnot.Position);
                        if (distance < closestDistance)
                        {
                            closestFound = true;
                            closestLane = otherTrafficLane;
                            closestDistance = distance;
                        }
                    }

                    if (closestFound)
                    {
                        var otherWaypoint = closestLane.GetNearestWaypoint(firstWaypoint.transform.position);
                        ConnectTwoWaypoints(otherWaypoint, firstWaypoint);
                    }
                    
                    // Outgoing
                    
                    closestFound = false;
                    closestDistance = float.MaxValue;
                    closestLane = default;
                    var lastWaypoint = waypoints[^1];

                    for (var l = 0; l < trafficLanes.Count; l++)
                    {
                        if (l == k) continue;
                        var otherTrafficLane = trafficLanes[l];
                        if (otherTrafficLane.trafficLaneType != TrafficLaneType.Pedestrian) continue;
                        if (otherTrafficLane.crossing) continue;
                        if (otherTrafficLane.waypoints.Count == 0) continue;
                        
                        var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(otherTrafficLane.spline, lastWaypoint.transform.position);
                        var nearestKnot = otherTrafficLane.spline[nearestKnotIndex]; 

                        var distance = math.distancesq(lastWaypoint.transform.position, nearestKnot.Position);
                        if (distance < closestDistance)
                        {
                            closestFound = true;
                            closestLane = otherTrafficLane;
                            closestDistance = distance;
                        }
                    }
                    
                    if (closestFound)
                    {
                        var otherWaypoint = closestLane.GetNearestWaypoint(lastWaypoint.transform.position);
                        ConnectTwoWaypoints(lastWaypoint, otherWaypoint);
                    }
                }
                
                /********************************************************************************************************************************/
                // Inner Connections

                // In
                for (var k = 0; k < trafficLanes.Count; k++)
                {
                    if (trafficLanes.Count < 2) break;
                    var trafficLane = trafficLanes[k];
                    if (!trafficLane.waypointInnerConnectionIn) continue;
                    var waypoints = trafficLane.GetWaypoints();
                    if (waypoints.Count == 0) continue;
                    var waypointIn = waypoints[0];
                    if(!waypointIn.startPoint) continue;

                    Waypoint closestWaypoint = default;
                    var minDistance = float.MaxValue;
                    for (int j = 0; j < trafficLanes.Count; j++)
                    {
                        if(k == j) continue;
                        var trafficLaneCheck = trafficLanes[j];
                        if(trafficLane.trafficLaneType != trafficLaneCheck.trafficLaneType) continue;
                        var waypointCheck = trafficLaneCheck.GetNearestWaypoint(waypointIn.transform.position);
                        var distance = Vector3.Distance(waypointIn.transform.position, waypointCheck.transform.position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestWaypoint = waypointCheck;
                        }
                    }

                    if (minDistance <= sceneObject.roadDescr.settings.waypointDistance.y)
                    {
                        if (trafficLane.trafficLaneType == TrafficLaneType.Car && closestWaypoint.prev.Count > 0) closestWaypoint = closestWaypoint.prev[0];
                        ConnectTwoWaypoints(closestWaypoint, waypointIn);
                    }
                }
                
                // Out
                for (var k = 0; k < trafficLanes.Count; k++)
                {
                    if (trafficLanes.Count < 2) break;
                    var trafficLane = trafficLanes[k];
                    if (!trafficLane.waypointInnerConnectionOut) continue;
                    var waypoints = trafficLane.GetWaypoints();
                    if (waypoints.Count == 0) continue;
                    var waypointOut = waypoints[^1];
                    if(!waypointOut.endPoint) continue;

                    Waypoint closestWaypoint = default;
                    var minDistance = float.MaxValue;
                    for (int j = 0; j < trafficLanes.Count; j++)
                    {
                        if(k == j) continue;
                        var trafficLaneCheck = trafficLanes[j];
                        if(trafficLane.trafficLaneType != trafficLaneCheck.trafficLaneType) continue;
                        var waypointCheck = trafficLaneCheck.GetNearestWaypoint(waypointOut.transform.position);
                        var distance = Vector3.Distance(waypointOut.transform.position, waypointCheck.transform.position);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestWaypoint = waypointCheck;
                        }
                    }

                    if (minDistance <= sceneObject.roadDescr.settings.waypointDistance.y)
                    {
                        if (trafficLane.trafficLaneType == TrafficLaneType.Car && closestWaypoint.next.Count > 0) closestWaypoint = closestWaypoint.next[0];
                        ConnectTwoWaypoints(waypointOut, closestWaypoint);
                    }
                }
            }

            return;

            void ConnectTwoWaypoints(Waypoint waypointPrev, Waypoint waypointNext)
            {
            
                if (!waypointPrev.next.Contains(waypointNext)) waypointPrev.next.Add(waypointNext);
                if (!waypointNext.prev.Contains(waypointPrev)) waypointNext.prev.Add(waypointPrev);
#if UNITY_EDITOR
                EditorUtility.SetDirty(waypointPrev);
                EditorUtility.SetDirty(waypointNext);
#endif
            }
        }
        
        

        /********************************************************************************************************************************/

        // Removing waypoints of intersections connecting to these roads
        public static void RemoveConnectingWaypoints(List<RoadObject> roads, TrafficLaneType trafficLaneType)
        {
            for (var i = 0; i < roads.Count; i++)
            {
                var trafficLanes = roads[i].GetTrafficLanes(trafficLaneType);
                for (var j = 0; j < trafficLanes.Count; j++)
                {
                    var waypoints = trafficLanes[j].GetWaypoints();
                    if (waypoints.Count == 0) continue;

                    // Previous
                    for (var l = waypoints[0].prev.Count - 1; l >= 0; l--)
                    {
                        var prevWaypoint = waypoints[0].prev[l];
                        prevWaypoint.next.Remove(waypoints[0]);
#if UNITY_EDITOR
                        if (prevWaypoint != null) EditorUtility.SetDirty(prevWaypoint);
#endif
                    }

                    // Next
                    for (var l = waypoints[^1].next.Count - 1; l >= 0; l--)
                    {
                        var nextWaypoint = waypoints[^1].next[l];
                        nextWaypoint.prev.Remove(waypoints[^1]);
#if UNITY_EDITOR
                        if (nextWaypoint != null) EditorUtility.SetDirty(nextWaypoint);
#endif
                    }
                }
            }
        }
    }
}