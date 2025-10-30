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

namespace PampelGames.RoadConstructor
{
    internal static class TrafficUtility
    {
        /// <summary>
        ///     Roads must be added first!
        /// </summary>
        public static void AddTrafficComponent(SceneObject sceneObject)
        {
            var splineContainerSplines = sceneObject.splineContainer.Splines.ToList();
            if (splineContainerSplines.Count == 0) return;

            sceneObject.RemoveTrafficComponent();

            var trafficObj = new GameObject("Traffic");
            trafficObj.transform.SetParent(sceneObject.transform);
            trafficObj.transform.SetAsFirstSibling();
            var traffic = trafficObj.AddComponent<Traffic>();
            sceneObject.traffic = traffic;
            var trafficSplineContainer = trafficObj.AddComponent<SplineContainer>();
            trafficSplineContainer.RemoveSpline(trafficSplineContainer.Spline);
            traffic.splineContainer = trafficSplineContainer;
            traffic.sceneObject = sceneObject;
        }


        public static List<TrafficLane> CreateTrafficLanesRoad(Spline spline, RoadDescr roadDescr)
        {
            var trafficLanes = new List<TrafficLane>();
            var trafficLanesEditor = roadDescr.trafficLanesEditor;

            for (var i = 0; i < trafficLanesEditor.Count; i++)
            {
                var trafficLaneEditor = trafficLanesEditor[i];
                var offsetX = trafficLaneEditor.position - roadDescr.width * 0.5f;

                // No pedestrian lanes inside roundabouts
                if (trafficLaneEditor.trafficLaneType == TrafficLaneType.Pedestrian && spline.Closed && offsetX < 0) continue;

                var newSpline = new Spline(spline);
                ConstructionSplineUtility.OffsetSplineParallel(newSpline, offsetX);
                if (trafficLaneEditor.direction == TrafficLaneDirection.Backwards) ConstructionSplineUtility.InvertSpline(newSpline);
                var trafficLane = new TrafficLane(trafficLaneEditor, newSpline, spline, false);
                trafficLanes.Add(trafficLane);
            }

            return trafficLanes;
        }

        public static List<TrafficLane> CreateTrafficLanesIntersection(SceneObject sceneObject, Traffic traffic)
        {
            var trafficLanes = new List<TrafficLane>();
            var centerPosition = ((IntersectionObject) sceneObject).centerPosition;

            if (sceneObject.IsEndObject()) return CreateTrafficLanesRoadEnd(sceneObject, traffic);

            SortConnectionsByAngle(centerPosition, sceneObject.Connections, out var connections);

            for (var i = 0; i < connections.Count; i++) // Sorted clockwise
            {
                var connectionIn = connections[i];
                var connectionInSpline = connections[i].splineContainer.Spline;

                var nearestKnotIndexIn = ConstructionSplineUtility.GetNearestKnotIndex(connectionInSpline, centerPosition);
                var nearestKnotIn = connectionInSpline[nearestKnotIndexIn];
                nearestKnotIn.TangentOut =
                    PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, nearestKnotIn.Position, nearestKnotIn.TangentOut);

                var connectionsOut = new List<SceneObject>();

                var startIndexOut = (i + 1) % connections.Count;

                for (var j = 0; j < connections.Count - 1; j++)
                {
                    var indexOut = (startIndexOut + j) % connections.Count;
                    if (indexOut == i) continue;
                    connectionsOut.Add(connections[indexOut]);
                }

                var knotDataIn = new KnotData(connectionIn.roadDescr, nearestKnotIn, nearestKnotIndexIn);
                var knotDatasOut = new List<KnotData>();

                for (var j = 0; j < connectionsOut.Count; j++)
                {
                    var splineOut = connectionsOut[j].splineContainer.Spline;
                    var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(splineOut, centerPosition);

                    var nearestKnot = splineOut[nearestKnotIndex];
                    knotDatasOut.Add(new KnotData(connectionsOut[j].roadDescr, nearestKnot, nearestKnotIndex));
                }

                trafficLanes.AddRange(CreateTrafficLanesInternal(centerPosition, true, knotDataIn, knotDatasOut));
            }

            return trafficLanes;
        }

        public static List<TrafficLane> CreateTrafficLanesRoundabout(RoundaboutObject roundabout)
        {
            var splines = roundabout.splineContainer.Splines;

            // Around
            var splineAround = splines[0];
            var trafficLanes = CreateTrafficLanesRoad(splineAround, roundabout.roadDescr);

            // Connections
            var connections = roundabout.Connections;

            for (var i = 1; i < splines.Count; i += 2)
            {
                var splineLeft = splines[i];
                var splineRight = splines[i + 1];

                SceneObject connection = default; // Searching for nearest connection manually (splines may not be ordered the same)
                var nearestDistance = float.MaxValue;
                for (var j = 0; j < connections.Count; j++)
                {
                    var distance = math.distancesq(splineLeft[^1].Position, connections[j].meshRenderer.bounds.center);
                    if (distance < nearestDistance)
                    {
                        connection = connections[j];
                        nearestDistance = distance;
                    }
                }

                var connectionSpline = connection!.splineContainer.Spline;
                var connectionNearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(connectionSpline, roundabout.centerPosition);

                var connectionNearestKnot = connectionSpline[connectionNearestKnotIndex];
                connectionNearestKnot.TangentOut = PGTrigonometryUtility.DirectionalTangentToPointXZ(roundabout.centerPosition,
                    connectionNearestKnot.Position, connectionNearestKnot.TangentOut);

                var knotLeft = splineLeft[0];
                var knotRight = splineRight[^1];

                // Out
                var knotDataLeftIn = new KnotData(roundabout.roadDescr, knotLeft, splineRight.Count - 1);
                var knotDatasLeft = new List<KnotData> {new(connection.roadDescr, connectionNearestKnot, connectionNearestKnotIndex)};
                var connectionTrafficLanesOut = CreateTrafficLanesInternal(roundabout.centerPosition, false,
                    knotDataLeftIn, knotDatasLeft, true, true);
                for (var j = 0; j < connectionTrafficLanesOut.Count; j++) connectionTrafficLanesOut[j].waypointInnerConnectionIn = true;
                trafficLanes.AddRange(connectionTrafficLanesOut);

                // In
                var knotDatasRight = new List<KnotData> {new(roundabout.roadDescr, knotRight, 0)};
                var connectionTrafficLanesIn = CreateTrafficLanesInternal(roundabout.centerPosition, false,
                    knotDatasLeft[0], knotDatasRight, true, true);
                for (var j = 0; j < connectionTrafficLanesIn.Count; j++) connectionTrafficLanesIn[j].waypointInnerConnectionOut = true;
                trafficLanes.AddRange(connectionTrafficLanesIn);
            }

            return trafficLanes;
        }

        public static List<TrafficLane> CreateTrafficLanesRamp(RampObject ramp)
        {
            var trafficLanes = new List<TrafficLane>();
            var centerPosition = ramp.centerPosition;

            SortConnectionsByAngle(centerPosition, ramp.Connections, out var connections);

            for (var i = 0; i < connections.Count; i++) // Sorted clockwise
            {
                var connectionIn = connections[i];
                var connectionInSpline = connections[i].splineContainer.Spline;

                var rampRoadIn = connectionIn is RoadObject {rampRoad: true};

                var nearestKnotIndexIn = ConstructionSplineUtility.GetNearestKnotIndex(connectionInSpline, centerPosition);
                var nearestKnotIn = connectionInSpline[nearestKnotIndexIn];
                nearestKnotIn.TangentOut =
                    PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, nearestKnotIn.Position, nearestKnotIn.TangentOut);

                var connectionsOut = new List<SceneObject>();

                var startIndexOut = (i + 1) % connections.Count;

                for (var j = 0; j < connections.Count - 1; j++)
                {
                    var indexOut = (startIndexOut + j) % connections.Count;
                    if (indexOut == i) continue;
                    connectionsOut.Add(connections[indexOut]);
                }

                var knotDataIn = new KnotData(connectionIn.roadDescr, nearestKnotIn, nearestKnotIndexIn);
                var knotDatasOut = new List<KnotData>();

                for (var j = 0; j < connectionsOut.Count; j++)
                {
                    var splineOut = connectionsOut[j].splineContainer.Spline;
                    var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(splineOut, centerPosition);

                    var nearestKnot = splineOut[nearestKnotIndex];
                    knotDatasOut.Add(new KnotData(connectionsOut[j].roadDescr, nearestKnot, nearestKnotIndex));
                }


                for (var j = 0; j < knotDatasOut.Count; j++) // Manually determining connections
                {
                    if (rampRoadIn && j == 0) continue;
                    var rampRoadOut = connectionsOut[j] is RoadObject {rampRoad: true};
                    if (rampRoadOut && j == 0) continue;

                    trafficLanes.AddRange(CreateTrafficLanesInternal(centerPosition, true, knotDataIn, knotDatasOut[j],
                        knotDatasOut.Count, j,
                        false, false, false));
                }
            }

            return trafficLanes;
        }

        /********************************************************************************************************************************/

        /// <summary>
        ///     Can be used by different intersection types.
        /// </summary>
        /// <param name="isIntersection">If true, in connects to in. If false, in connects to out.</param>
        /// <param name="knotDataIn">IMPORTANT: knot.tangentOut must be pointing towards knotsOut.</param>
        /// <returns></returns>
        private static List<TrafficLane> CreateTrafficLanesInternal(float3 centerPosition, bool isIntersection,
            KnotData knotDataIn, List<KnotData> knotDatasOut, bool crossings = true, bool skipLeftPedestrian = false)
        {
            var trafficLanes = new List<TrafficLane>();
            if (knotDatasOut.Count == 0) return trafficLanes;

            var singleLeftTurn = false;
            var singleRightTurn = false;

            if (knotDatasOut.Count > 1)
            {
                var tangentIn = math.normalizesafe(knotDataIn.nearestKnot.TangentOut);

                var nearestKnotLeft = knotDatasOut[0].nearestKnot;
                var tangentLeftOut =
                    -PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, nearestKnotLeft.Position, nearestKnotLeft.TangentOut);
                var angleLeft = math.degrees(PGTrigonometryUtility.AngleXZ(tangentIn, tangentLeftOut));
                if (Constants.LeftAngle(angleLeft)) singleLeftTurn = true;

                var nearestKnotRight = knotDatasOut[^1].nearestKnot;
                var tangentRightOut =
                    -PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, nearestKnotRight.Position, nearestKnotRight.TangentOut);
                var angleRight = math.degrees(PGTrigonometryUtility.AngleXZ(tangentIn, tangentRightOut));
                if (Constants.RightAngle(angleRight)) singleRightTurn = true;
            }

            for (var i = 0; i < knotDatasOut.Count; i++)
                trafficLanes.AddRange(CreateTrafficLanesInternal(centerPosition, isIntersection, knotDataIn, knotDatasOut[i],
                    knotDatasOut.Count, i,
                    singleLeftTurn, singleRightTurn, crossings, skipLeftPedestrian));

            return trafficLanes;
        }

        private static List<TrafficLane> CreateTrafficLanesInternal(float3 centerPosition, bool isIntersection,
            KnotData knotDataIn, KnotData knotDataOut, int knotDataOutCount, int knotDataOutIndex,
            bool singleLeftTurn = false, bool singleRightTurn = false, bool crossings = true, bool skipLeftPedestrian = false)
        {
            var trafficLanes = new List<TrafficLane>();

            var tangentIn = math.normalizesafe(knotDataIn.nearestKnot.TangentOut);
            var tangentInFlat = math.normalizesafe(new float3(tangentIn.x, 0f, tangentIn.z));
            var tangentInPerp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangentInFlat);

            if (knotDataIn.roadDescr.road.oneWay && knotDataIn.nearestKnotIndex == 0) return trafficLanes;
            if (knotDataOut.roadDescr.road.oneWay && knotDataOut.nearestKnotIndex != 0) return trafficLanes;

            var heightSpline = new Spline
            {
                {knotDataIn.nearestKnot, TangentMode.Broken},
                {knotDataOut.nearestKnot, TangentMode.Broken}
            };

            TangentCalculation.CalculateTangents(heightSpline, knotDataIn.roadDescr.settings.smoothSlope, Constants.TangentLengthIntersection);

            var roadDescrOut = knotDataOut.roadDescr;
            var nearestKnotOut = knotDataOut.nearestKnot;

            var tangentOut =
                -PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, nearestKnotOut.Position, nearestKnotOut.TangentOut);
            tangentOut = math.normalizesafe(tangentOut);
            var tangentOutFlat = math.normalizesafe(new float3(new float3(tangentOut.x, 0f, tangentOut.z)));
            var tangentOutPerp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangentOutFlat);


            /********************************************************************************************************************************/
            // Car

            var lanesEditorIn = knotDataIn.roadDescr.GetSortedTrafficLanesEditor(TrafficLaneType.Car, TrafficLaneDirection.Forward);
            var lanesEditorOut = roadDescrOut.GetSortedTrafficLanesEditor(TrafficLaneType.Car, TrafficLaneDirection.Forward);


            // If there is a single lane into multi-lanes, it chooses the furthest lane by default when going right.
            var angle = PGTrigonometryUtility.AngleXZ(tangentIn, tangentOut);
            if (angle > 0 && lanesEditorIn.Count == 1 && lanesEditorOut.Count > 1) lanesEditorOut.Reverse();


            var checkedIDs = new HashSet<Vector2Int>(); // Making sure we don't create the same lane twice

            for (var j = 0; j < lanesEditorIn.Count; j++)
            {
                if (lanesEditorOut.Count == 0) break;

                var assignedOutLane = Mathf.RoundToInt(j * (float) lanesEditorOut.Count / lanesEditorIn.Count) % lanesEditorOut.Count;

                if (!knotDataOut.roadDescr.road.oneWay)
                {
                    // Only left lane turs left (but also into other lanes)
                    if (singleLeftTurn && knotDataOutIndex == 0 && j > 0) continue;
                    // Only right lane turns right (but also into other lanes)
                    if (singleRightTurn && knotDataOutIndex == knotDataOutCount - 1 && j < lanesEditorIn.Count - 1) continue;
                }

                CalculateAndAddTrafficLane(j, assignedOutLane);
            }

            // Middle lanes are not connected yet if no straight connection. Put them into right lanes.
            if (singleLeftTurn && singleRightTurn && knotDataOutCount == 2 && knotDataOutIndex > 0)
                for (var j = 1; j < lanesEditorIn.Count - 1; j++)
                    CalculateAndAddTrafficLane(j, 0);

            /********************************************************************************************************************************/
            // Pedestrian

            checkedIDs.Clear();
            lanesEditorIn = knotDataIn.roadDescr.GetSortedTrafficLanesEditor(TrafficLaneType.Pedestrian, TrafficLaneDirection.Forward);
            lanesEditorOut = roadDescrOut.GetSortedTrafficLanesEditor(TrafficLaneType.Pedestrian, TrafficLaneDirection.Forward);

            if (lanesEditorIn.Count <= 0 || lanesEditorOut.Count <= 0) return trafficLanes;

            if (!skipLeftPedestrian)
                if (knotDataOutIndex == 0)
                    CalculateAndAddTrafficLane(0, 0); // Connect to left neighbour
            if (knotDataOutIndex == knotDataOutCount - 1)
                CalculateAndAddTrafficLane(lanesEditorIn.Count - 1, lanesEditorOut.Count - 1); // Connect to right neighbour    

            /********************************************************************************************************************************/
            // Pedestrian Crossings

            if (crossings && isIntersection && knotDataOutIndex == knotDataOutCount - 1 && knotDataOutCount > 1) // Crossing to right neighbour
            {
                var distance = knotDataIn.roadDescr.settings.intersectionDistance * 0.5f;
                var distancePerp = knotDataIn.roadDescr.width * 0.5f - knotDataIn.roadDescr.sideLanesWidth;
                var middlePos = knotDataIn.nearestKnot.Position + tangentIn * distance;
                var pos01 = middlePos + tangentInPerp * distancePerp;
                var pos02 = middlePos - tangentInPerp * distancePerp;
                var tan = (pos02 - pos01) * 0.5f;
                var knot01 = new BezierKnot(pos01, -tan, tan, quaternion.identity);
                var knot02 = new BezierKnot(pos02, -tan, tan, quaternion.identity);

                var crossingSpline = new Spline
                {
                    {knot01, TangentMode.Broken},
                    {knot02, TangentMode.Broken}
                };

                var trafficLane = new TrafficLane(lanesEditorIn[0], crossingSpline, crossingSpline, true);
                trafficLanes.Add(trafficLane);

                var crossingSplineInv = new Spline(crossingSpline);
                SplineUtility.ReverseFlow(crossingSplineInv);
                var trafficLaneInv = new TrafficLane(lanesEditorIn[0], crossingSplineInv, crossingSplineInv, true);
                trafficLanes.Add(trafficLaneInv);
            }

            /********************************************************************************************************************************/

            void CalculateAndAddTrafficLane(int trafficLaneEditorInIndex, int trafficLaneEditorOutIndex)
            {
                if (!checkedIDs.Add(new Vector2Int(trafficLaneEditorInIndex, trafficLaneEditorOutIndex))) return;

                var trafficLaneEditorIn = lanesEditorIn[trafficLaneEditorInIndex];
                var trafficLaneEditorOut = lanesEditorOut[trafficLaneEditorOutIndex];

                var offsetInX = trafficLaneEditorIn.position - knotDataIn.roadDescr.width * 0.5f;
                var positionIn = knotDataIn.nearestKnot.Position + tangentInPerp * offsetInX;

                var offsetOutX = trafficLaneEditorOut.position - roadDescrOut.width * 0.5f;
                var positionOut = nearestKnotOut.Position + tangentOutPerp * offsetOutX;

                var knot01 = new BezierKnot(positionIn, -tangentIn, tangentIn, quaternion.identity);
                var knot02 = new BezierKnot(positionOut, -tangentOut, tangentOut, quaternion.identity);

                var trafficSpline = new Spline
                {
                    {knot01, TangentMode.Broken},
                    {knot02, TangentMode.Broken}
                };

                TangentCalculation.CalculateTangents(trafficSpline, knotDataIn.roadDescr.settings.smoothSlope, Constants.TangentLengthIntersection,
                    true, centerPosition);

                var trafficLane = new TrafficLane(trafficLaneEditorIn, trafficSpline, heightSpline, false);
                trafficLanes.Add(trafficLane);
            }

            return trafficLanes;
        }

        private static void SortConnectionsByAngle(float3 centerPosition, List<SceneObject> connections,
            out List<SceneObject> sortedConnections)
        {
            var tangentIn = math.forward();
            sortedConnections = new List<SceneObject>();
            var angleConnectionPairs = new List<(float angle, SceneObject connection)>();

            for (var i = 0; i < connections.Count; i++)
            {
                var connectionSpline = connections[i].splineContainer.Spline;
                var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(connectionSpline, centerPosition);
                var nearestKnot = connectionSpline[nearestKnotIndex];
                var connectionTangentIn =
                    PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, nearestKnot.Position, nearestKnot.TangentOut);
                var angle = PGTrigonometryUtility.AngleXZ(tangentIn, connectionTangentIn);
                angleConnectionPairs.Add((angle, connections[i]));
            }

            angleConnectionPairs = angleConnectionPairs.OrderBy(pair => pair.angle).ToList();
            foreach (var pair in angleConnectionPairs) sortedConnections.Add(pair.connection);
        }


        private static List<TrafficLane> CreateTrafficLanesRoadEnd(SceneObject sceneObject, Traffic traffic)
        {
            var splines = sceneObject.splineContainer.Splines;

            var trafficLanes = new List<TrafficLane>();
            if (sceneObject.road.oneWay) return trafficLanes;

            var trafficSplineContainer = traffic.splineContainer;
            var trafficLanesEditor = sceneObject.roadDescr.trafficLanesEditor;

            for (var i = 0; i < splines.Count; i++)
            for (var j = 0; j < trafficLanesEditor.Count; j++)
            {
                var trafficLaneEditor = trafficLanesEditor[j];
                if (trafficLaneEditor.direction == TrafficLaneDirection.Backwards) continue;

                Spline newSpline = default;
                var offsetX = trafficLaneEditor.position - sceneObject.roadDescr.width * 0.5f;

                newSpline = new Spline(splines[i]);

                ConstructionSplineUtility.OffsetSplineParallel(newSpline, offsetX);

                if (sceneObject.road.oneWay) continue;
                var endObject = (IntersectionObject) sceneObject;
                var centerPosition = endObject.centerPosition;

                var knot01 = newSpline.Knots.First();
                var tangent01 = math.normalizesafe(knot01.TangentOut);
                tangent01.y = 0f;
                var tangent01perp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangent01);
                var centerDirection = (float3) centerPosition - knot01.Position;
                if (!PGTrigonometryUtility.IsSameDirectionXZ(tangent01perp, centerDirection)) tangent01perp *= -1f;
                var centerDistance = math.distance(knot01.Position, centerPosition);
                var knot02 = newSpline.Knots.Last();
                knot02.Position = knot01.Position + tangent01perp * centerDistance * 2f;
                var tangentLength = ConstructionSplineUtility.GetUnitCircleTangentLength(centerDistance);
                tangent01 *= tangentLength;
                knot01.TangentOut = tangent01;
                knot01.TangentIn = tangent01;
                knot02.TangentOut = tangent01;
                knot02.TangentIn = tangent01;

                newSpline.SetKnot(0, knot01);
                newSpline.SetKnot(newSpline.Count - 1, knot02);

                trafficSplineContainer.AddSpline(newSpline);
                var trafficLane = new TrafficLane(trafficLaneEditor, newSpline, newSpline, false);
                trafficLanes.Add(trafficLane);
            }

            return trafficLanes;
        }


        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public static List<TrafficLaneEditor> GetTrafficLanesEditor(List<TrafficLanePreset> trafficLanePresets, RoadDescr roadDescr)
        {
            var road = roadDescr.road;
            var trafficLanesEditor = new List<TrafficLaneEditor>();
            trafficLanesEditor.AddRange(road.trafficLanes);
            var centerPosX = roadDescr.width * 0.5f;

            /********************************************************************************************************************************/
            // Mirror
            for (var i = 0; i < trafficLanePresets.Count; i++)
            {
                var preset = trafficLanePresets[i];

                var categoryList = preset.category.Split(',').ToList();
                var categoryFound = categoryList.Any(t => road.category == t);
                if (!categoryFound) continue;

                trafficLanesEditor.AddRange(preset.trafficLanes);

                for (var j = 0; j < preset.trafficLanes.Count; j++)
                {
                    var lane = preset.trafficLanes[j];

                    if (!lane.mirror) continue;
                    if (Mathf.Approximately(centerPosX, lane.position)) continue;

                    var laneCopy = PGClassUtility.CopyClass(lane) as TrafficLaneEditor;
                    if (lane.direction == TrafficLaneDirection.Forward) laneCopy.direction = TrafficLaneDirection.Backwards;
                    else if (lane.direction == TrafficLaneDirection.Backwards) laneCopy.direction = TrafficLaneDirection.Forward;
                    var dif = math.abs(centerPosX - lane.position);
                    if (lane.position < centerPosX) laneCopy.position = centerPosX + dif;
                    else laneCopy.position = centerPosX - dif;
                    trafficLanesEditor.Add(laneCopy);
                }
            }

            /********************************************************************************************************************************/
            // Direction -> Both
            var count = trafficLanesEditor.Count;
            for (var i = 0; i < count; i++)
            {
                var lane = trafficLanesEditor[i];

                if (lane.direction != TrafficLaneDirection.Both) continue;

                var laneCopy = PGClassUtility.CopyClass(lane) as TrafficLaneEditor;
                laneCopy.direction = TrafficLaneDirection.Backwards;
                trafficLanesEditor.Add(laneCopy);
            }

            return trafficLanesEditor;
        }
    }
}