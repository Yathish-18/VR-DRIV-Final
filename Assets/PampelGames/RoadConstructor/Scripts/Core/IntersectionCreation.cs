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
using SplineMesh = PampelGames.Shared.Construction.SplineMesh;

namespace PampelGames.RoadConstructor
{
    public class KnotData
    {
        public readonly RoadDescr roadDescr;
        public readonly BezierKnot nearestKnot;
        public readonly int nearestKnotIndex;

        public KnotData(RoadDescr roadDescr, BezierKnot nearestKnot, int nearestKnotIndex)
        {
            this.roadDescr = roadDescr;
            this.nearestKnot = nearestKnot;
            this.nearestKnotIndex = nearestKnotIndex;
        }
    }

    internal static class IntersectionCreation
    {
        public static void AddRoad(List<ConstructionFail> constructionFails, ConstructionObjects constructionObjects, Overlap overlap,
            RoadObject newRoadObject)
        {
            var settings = newRoadObject.roadDescr.settings;
            var existingConnections = overlap.SceneObject.RoadConnections;
            var elevated = newRoadObject.elevated || existingConnections.Any(conn => conn.elevated);

            var newRoadSpline = newRoadObject.splineContainer.Spline;
            var newNearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(newRoadSpline, overlap.position);
            var newNearestKnot = newRoadSpline.Knots.ElementAt(newNearestKnotIndex);
            var tangent01 = newNearestKnot.TangentOut;

            var snappedRoad = overlap.IsSnappedRoad();

            var knotDatas = new List<KnotData>();

            /********************************************************************************************************************************/
            /********************************************************************************************************************************/
            if (overlap.overlapType == OverlapType.Intersection || snappedRoad)
            {
                var connections = new List<RoadObject>();
                if (!snappedRoad) connections.AddRange(overlap.intersectionObject.RoadConnections);
                else connections.AddRange(overlap.roadObject.RoadConnections);
                connections.Add(newRoadObject);

                for (var i = 0; i < connections.Count; i++)
                {
                    var roadConnection = connections[i];
                    var connectionSpline = roadConnection.splineContainer.Spline;
                    var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(connectionSpline, overlap.position);
                    var nearestKnot = connectionSpline.Knots.ElementAt(nearestKnotIndex);

                    knotDatas.Add(new KnotData(roadConnection.roadDescr, nearestKnot, nearestKnotIndex));
                }

                /********************************************************************************************************************************/
                // Replacing existing Road Objects (reduced length)

                for (var i = 0; i < knotDatas.Count; i++)
                {
                    if (connections[i].iD == newRoadObject.iD) continue;

                    var data = knotDatas[i];
                    var knot = data.nearestKnot;

                    var otherDatas = new List<KnotData>(knotDatas);
                    otherDatas.RemoveAt(i);
                    if (otherDatas.Count == 0) break;

                    var otherConnections = new List<RoadObject>(connections);
                    otherConnections.RemoveAt(i);

                    var tangentIn = PGTrigonometryUtility.DirectionalTangentToPointXZ(overlap.position, knot.Position, knot.TangentOut);

                    if (overlap.IsEndObject())
                    {
                        var splineEndPosition = overlap.spline[^1].Position;
                        tangentIn = PGTrigonometryUtility.DirectionalTangentToPointXZ(splineEndPosition, knot.Position, knot.TangentOut);
                    }

                    var otherIndex = AngleDistanceUtility.GetIndexWithLeastDegrees(overlap.position, tangentIn, otherDatas);
                    var otherKnot = otherDatas[otherIndex].nearestKnot;

                    var otherTangentIn =
                        PGTrigonometryUtility.DirectionalTangentToPointXZ(overlap.position, otherKnot.Position, otherKnot.TangentOut);

                    var angle = math.abs(PGTrigonometryUtility.AngleXZ(tangentIn, otherTangentIn));

                    var width = knotDatas[i].roadDescr.width;
                    var otherWidth = otherDatas[otherIndex].roadDescr.width;

                    var targetDistance = AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, otherWidth, width, angle);

                    if (otherConnections[otherIndex].iD != newRoadObject.iD) // Don't reduce if tightest neighbour is existing neighbour.
                    {
                        var otherOtherDatas = new List<KnotData>(otherDatas); // Check before if other side is new connection
                        otherOtherDatas.RemoveAt(otherIndex);
                        if (otherOtherDatas.Count > 0)
                        {
                            var otherOtherConnections = new List<RoadObject>(otherConnections);
                            otherOtherConnections.RemoveAt(otherIndex);
                            var otherOtherIndex = AngleDistanceUtility.GetIndexWithLeastDegrees(overlap.position, tangentIn, otherOtherDatas);
                            if (otherOtherConnections[otherOtherIndex].iD == newRoadObject.iD)
                            {
                                var otherOtherWidth = otherOtherDatas[otherOtherIndex].roadDescr.width;
                                var targetDistanceOther =
                                    AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, otherOtherWidth, width, angle);
                                targetDistance = Mathf.Max(targetDistance, targetDistanceOther);
                            }
                            else
                            {
                                continue;
                            }
                        }
                        else
                        {
                            continue;
                        }
                    }

                    var newConnectionSpline = new Spline(connections[i].splineContainer.Spline);

                    var currentDistance = math.distance(overlap.position, knot.Position);
                    var distanceDelta = targetDistance - currentDistance;

                    var newConnectionSplineLength = newConnectionSpline.GetLength();
                    var reducedLengthT = distanceDelta / newConnectionSplineLength;
                    if (reducedLengthT >= 0.99f)
                    {
                        constructionFails.Add(new ConstructionFail(FailCause.IntersectionTrackLength));
                        return;
                    }

                    ConstructionSplineUtility.ReduceSpline(newConnectionSpline, data.nearestKnotIndex == 0, newConnectionSplineLength, distanceDelta);
                    var reducedNearestKnot = newConnectionSpline[data.nearestKnotIndex];
                    reducedNearestKnot.Position = new float3(reducedNearestKnot.Position.x, overlap.position.y, reducedNearestKnot.Position.z);
                    newConnectionSpline.SetKnot(data.nearestKnotIndex, reducedNearestKnot);

                    var slope = math.degrees(PGTrigonometryUtility.Slope(newConnectionSpline[0].Position, newConnectionSpline[^1].Position));
                    if (math.abs(slope) > settings.maxSlope)
                    {
                        constructionFails.Add(new ConstructionFail(FailCause.IntersectionTrackSlope));
                        return;
                    }

                    var newRoadConnection = RoadCreation.CreateReplaceRoadObject(connections[i], newConnectionSpline, 1f);

                    var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(newRoadConnection.splineContainer.Spline, overlap.position);
                    var nearestKnot = newRoadConnection.splineContainer.Spline[nearestKnotIndex];

                    constructionObjects.newReplacedRoads.Add(newRoadConnection);
                    constructionObjects.removableRoads.Add(connections[i]);

                    knotDatas[i] = new KnotData(connections[i].roadDescr, nearestKnot, nearestKnotIndex);
                }

                var oldIntersection = overlap.intersectionObject;
                var oldRoad = overlap.roadObject;

                if (snappedRoad)
                    constructionObjects.removableRoads.Add(oldRoad);
                else
                    constructionObjects.removableIntersections.Add(oldIntersection);
            }
            /********************************************************************************************************************************/
            /********************************************************************************************************************************/
            else if (overlap.overlapType == OverlapType.Road)
            {
                var oldRoadObject = overlap.roadObject;

                /********************************************************************************************************************************/
                // New Road Objects left and right (reduced length)

                var angleLeft = math.abs(PGTrigonometryUtility.AngleXZ(tangent01, -overlap.tangent));
                var angleRight = math.abs(PGTrigonometryUtility.AngleXZ(tangent01, overlap.tangent));

                if (newNearestKnotIndex != 0) (angleLeft, angleRight) = (angleRight, angleLeft);

                var widthExisting = newRoadObject.roadDescr.width; // Need new here
                var widthConnecting = oldRoadObject.roadDescr.width; // Need old here
                var widthGapLeft = AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, widthExisting, widthConnecting, angleLeft);
                var widthGapRight = AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, widthExisting, widthConnecting, angleRight);

                RoadCreation.SplitRoad(oldRoadObject, overlap.t, widthGapLeft, widthGapRight, 1f,
                    out var removableRoads, out var newRoadsLeft, out var newRoadsRight,
                    true, overlap.position.y);

                var newRoads = new List<RoadObject>();
                newRoads.AddRange(newRoadsLeft);
                newRoads.AddRange(newRoadsRight);

                knotDatas.Add(new KnotData(newRoadObject.roadDescr, newNearestKnot, newNearestKnotIndex));
                for (var i = 0; i < newRoads.Count; i++)
                {
                    var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(newRoads[i].splineContainer.Spline, overlap.position);
                    var nearestKnot = newRoads[i].splineContainer.Spline[nearestKnotIndex];
                    knotDatas.Add(new KnotData(newRoads[i].roadDescr, nearestKnot, nearestKnotIndex));
                }

                constructionObjects.removableRoads.AddRange(removableRoads);
                constructionObjects.newReplacedRoads.AddRange(newRoads);
            }

            /********************************************************************************************************************************/

            CreateIntersection(settings, constructionObjects, knotDatas, overlap.position, elevated, true);
        }

        public static void RemoveRoad(IntersectionObject intersection, RoadObject removableRoad, ConstructionObjects constructionObjects)
        {
            if (!intersection.RoadConnections.Contains(removableRoad)) return;
            var roadConnections = new List<RoadObject>(intersection.RoadConnections);
            roadConnections.Remove(removableRoad);

            if (RoadCreation.TryMergeSplittedRoad(constructionObjects, roadConnections)) return; // Check if we can recreate a splitted road

            var knotDatas = CreateKnotDatas(intersection.centerPosition, roadConnections);

            CreateIntersection(intersection.roadDescr.settings, constructionObjects, knotDatas,
                intersection.centerPosition, intersection.elevated, true);
        }

        public static void CreateIntersection(ComponentSettings settings, ConstructionObjects constructionObjects,
            List<KnotData> knotDatas, float3 centerPosition, bool elevated, bool replaced)
        {
            if (knotDatas.Count == 0) return;

            // Missing End Objects are created in FinalizeConstruction 
            if (knotDatas.Count == 1) return;

            // If the intersection is between two roads of the same type, we can merge the intersection into one road.
            if (knotDatas.Count == 2 && knotDatas[0].roadDescr.road.roadName == knotDatas[1].roadDescr.road.roadName)
            {
                var knot01 = knotDatas[0].nearestKnot;
                var knot02 = knotDatas[1].nearestKnot;

                if (knotDatas[0].roadDescr.road.oneWay && knotDatas[0].nearestKnotIndex == 0) (knot01, knot02) = (knot02, knot01);

                var extensionSpline = new Spline(new List<BezierKnot> {knot01, knot02});
                TangentCalculation.CalculateTangents(extensionSpline, settings.smoothSlope, Constants.TangentLengthIntersection,
                    true, centerPosition);

                var extensionRoad = RoadCreation.CreateRoad(knotDatas[0].roadDescr, extensionSpline, elevated, false, 1f);
                constructionObjects.newRoads.Add(extensionRoad);
            }
            else
            {
                var intersectionObject = CreateIntersectionInternal(knotDatas, centerPosition, elevated);
                if (replaced) constructionObjects.newReplacedIntersections.Add(intersectionObject);
                else constructionObjects.newIntersections.Add(intersectionObject);
            }
        }


        private static IntersectionObject CreateIntersectionInternal(List<KnotData> knotDatas, float3 centerPosition, bool elevated)
        {
            var priorityMeshData = GetHighestPriorityMeshData(knotDatas);

            var intersectionMesh = CreateIntersectionMesh(knotDatas,
                centerPosition, elevated, 1f,
                out var newMaterials, out var newSplines, out var newPositions);

            CreateIntersectionObject(priorityMeshData.roadDescr.road,
                out var intersectionObject, out var splineContainer, out var meshFilter, out var meshRenderer);

            meshFilter.sharedMesh = intersectionMesh;
            meshRenderer.sharedMaterials = newMaterials.ToArray();

            intersectionObject.Initialize(priorityMeshData.roadDescr, meshFilter, meshRenderer, splineContainer, elevated);
            intersectionObject.centerPosition = centerPosition;

            var knotPositions = knotDatas.Select(t => t.nearestKnot.Position).ToList();
            var intersectionSplines = ConstructionSplineUtility.CreateIntersectionSplines(centerPosition, knotPositions);

            if (knotDatas.Count == 2 && intersectionSplines.Count == 1)
                if (knotDatas[0].roadDescr.road.oneWay && knotDatas[0].nearestKnotIndex == 0)
                    ConstructionSplineUtility.InvertSpline(intersectionSplines[0]);

            intersectionObject!.splineContainer.RemoveSpline(intersectionObject.splineContainer.Spline);
            for (var i = 0; i < intersectionSplines.Count; i++)
                intersectionObject.splineContainer.AddSpline(intersectionSplines[i]);

            return intersectionObject;
        }

        public static void CreateIntersectionObject(Road road,
            out IntersectionObject intersectionObject, out SplineContainer splineContainer,
            out MeshFilter meshFilter, out MeshRenderer meshRenderer)
        {
            var intersectionObj = ObjectUtility.CreateObj(Constants.PrefixIntersection, road.shadowCastingMode, out meshFilter, out meshRenderer);

            intersectionObject = intersectionObj.AddComponent<IntersectionObject>();
            intersectionObject.meshFilter = meshFilter;
            intersectionObject.meshRenderer = meshRenderer;

            splineContainer = intersectionObj.AddComponent<SplineContainer>();
            splineContainer.RemoveSpline(splineContainer.Spline);
        }

        public static IntersectionObject CreateReplaceIntersectionObject(IntersectionObject intersectionObject)
        {
            var road = intersectionObject.roadDescr.road;
            var newIntersectionObj = ObjectUtility.CreateObj(intersectionObject.NamePrefix(), road.shadowCastingMode, out var meshFilter,
                out var meshRenderer);
            var newIntersectionObject = (IntersectionObject) newIntersectionObj.AddComponent(intersectionObject.GetType());
            var splineContainer = newIntersectionObj.AddComponent<SplineContainer>();
            splineContainer.RemoveSpline(splineContainer.Spline);
            newIntersectionObject.Initialize(intersectionObject.roadDescr, meshFilter, meshRenderer, splineContainer, intersectionObject.elevated);

            foreach (var originalSpline in intersectionObject.splineContainer.Splines)
            {
                var newSpline = new Spline(originalSpline);
                newIntersectionObject.splineContainer.AddSpline(newSpline);
            }

            newIntersectionObject.centerPosition = intersectionObject.centerPosition;
            return newIntersectionObject;
        }

        /********************************************************************************************************************************/

        public static List<KnotData> CreateKnotDatas(Vector3 centerPosition, List<RoadObject> roadConnections)
        {
            var roadSplines = new List<Spline>();
            var roadDescrs = new List<RoadDescr>();
            for (var i = 0; i < roadConnections.Count; i++)
            {
                roadSplines.Add(roadConnections[i].splineContainer.Spline);
                roadDescrs.Add(roadConnections[i].roadDescr);
            }

            return CreateKnotDatas(centerPosition, roadSplines, roadDescrs);
        }

        public static List<KnotData> CreateKnotDatas(Vector3 centerPosition, List<Spline> roadSplines, List<RoadDescr> roadDescrs)
        {
            var createIntersectionMeshDatas = new List<KnotData>();

            for (var i = 0; i < roadSplines.Count; i++)
            {
                var roadConnection = roadSplines[i];
                var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(roadConnection, centerPosition);
                var nearestKnot = roadConnection[nearestKnotIndex];
                createIntersectionMeshDatas.Add(new KnotData(roadDescrs[i], nearestKnot, nearestKnotIndex));
            }

            return createIntersectionMeshDatas;
        }

        // Sorts datas by angle clockwise
        public static void OrderKnotDatas<T>(Vector3 centerPosition, List<KnotData> datas, List<T> otherCollection = null)
        {
            if (otherCollection == null)
            {
                var referenceVector = new float3(0, 0, 1);
                datas.Sort((a, b) =>
                {
                    var tangentA = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, a.nearestKnot.Position,
                        a.nearestKnot.TangentOut);
                    var tangentB = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, b.nearestKnot.Position,
                        b.nearestKnot.TangentOut);
                    var angleA = PGTrigonometryUtility.AngleXZ(referenceVector, tangentA);
                    var angleB = PGTrigonometryUtility.AngleXZ(referenceVector, tangentB);
                    return angleA.CompareTo(angleB);
                });
                return;
            }

            var indices = Enumerable.Range(0, datas.Count).ToList();
            var referenceVectorAll = new float3(0, 0, 1);

            indices.Sort((a, b) =>
            {
                var tangentA = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, datas[a].nearestKnot.Position,
                    datas[a].nearestKnot.TangentOut);
                var tangentB = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, datas[b].nearestKnot.Position,
                    datas[b].nearestKnot.TangentOut);
                var angleA = PGTrigonometryUtility.AngleXZ(referenceVectorAll, tangentA);
                var angleB = PGTrigonometryUtility.AngleXZ(referenceVectorAll, tangentB);
                return angleA.CompareTo(angleB);
            });

            var orderedDatas = indices.Select(i => datas[i]).ToList();
            datas.Clear();
            datas.AddRange(orderedDatas);

            var orderedCollection = indices.Select(i => otherCollection[i]).ToList();
            otherCollection.Clear();
            otherCollection.AddRange(orderedCollection);
        }

        public static Mesh CreateIntersectionMesh(List<KnotData> datas,
            float3 centerPosition, bool isElevated, float lodAmount,
            out List<Material> newMaterials, out List<Spline> newSplines, out List<float3> newPositions)
        {
            var combineMeshes = new List<Mesh>();
            newMaterials = new List<Material>();
            newSplines = new List<Spline>();
            newPositions = new List<float3>();

            var knots = datas.Select(t => t.nearestKnot).ToList();

            var centerAverage = new float3(knots.Average(knot => knot.Position.x), knots.Average(knot => knot.Position.y),
                knots.Average(knot => knot.Position.z));

            /********************************************************************************************************************************/
            // Road End
            if (datas.Count == 1)
            {
                var data = datas[0];
                var nearestKnot = data.nearestKnot;
                var startPart = data.nearestKnotIndex == 0;
                var forward = startPart ? nearestKnot.TangentOut : -nearestKnot.TangentOut;

                var endObjectMesh = RoadEndCreation.CreateEndObjectMesh(data.roadDescr,
                    data.nearestKnotIndex == 0, nearestKnot.Position, forward, lodAmount,
                    out newMaterials, out var spline);
                newSplines.Add(spline);

                return endObjectMesh;
            }

            /********************************************************************************************************************************/
            // One Intersection road (Main Connection)
            // Only for the 2 knots which are looking at each other.

            AngleDistanceUtility.GetFacingIndexes(centerAverage, knots, out var indexLeft, out var indexRight);

            var exitDatas = datas.Where((_, index) => index != indexLeft && index != indexRight).ToList();

            var knotLeft = datas[indexLeft].nearestKnot;
            var knotRight = datas[indexRight].nearestKnot;
            var roadDescrLeft = datas[indexLeft].roadDescr;
            var roadDescrRight = datas[indexRight].roadDescr;

            var leadingRoadDescr = roadDescrLeft;
            var otherRoadDescr = roadDescrRight;

            if (roadDescrLeft.width < roadDescrRight.width)
            {
                (leadingRoadDescr, otherRoadDescr) = (otherRoadDescr, leadingRoadDescr);
                (knotLeft, knotRight) = (knotRight, knotLeft);
            }

            var roadLanesIntersection = isElevated ? leadingRoadDescr.lanesIntersectionElevated : leadingRoadDescr.lanesIntersection;
            var roadPos01 = knotLeft.Position;
            var roadTan01 = knotLeft.TangentOut;
            var roadPos02 = knotRight.Position;
            var roadTan02 = knotRight.TangentOut;

            /********************************************************************************************************************************/
            // Main Connection

            var widthStart = 1f;
            var widthEnd = otherRoadDescr.width / leadingRoadDescr.width;

            CreateIntersectionRoadMesh(roadLanesIntersection, leadingRoadDescr.settings, centerAverage, leadingRoadDescr.width,
                leadingRoadDescr.road.length, leadingRoadDescr.resolution, roadPos01, roadTan01, roadPos02, roadTan02,
                combineMeshes, newMaterials, newSplines, widthStart, widthEnd);

            /********************************************************************************************************************************/
            // Other roads only get an exit road

            var splineMiddle = newSplines[^1];
            var splineMiddlePos = splineMiddle.EvaluatePosition(0.5f);
            var splineMiddleTan = splineMiddle.EvaluateTangent(0.5f);
            splineMiddleTan.y = 0f;
            var splineMiddleTanPerp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(splineMiddleTan);

            for (var i = 0; i < exitDatas.Count; i++)
            {
                var exitRoadDescr = exitDatas[i].roadDescr;
                var pos1 = splineMiddlePos + new float3(0, Constants.HeightOffset * (1 + i), 0);
                var tan1 = splineMiddleTanPerp;
                var pos2 = exitDatas[i].nearestKnot.Position;
                var tan2 = exitDatas[i].nearestKnot.TangentOut;

                var exitLanes = isElevated ? exitRoadDescr.lanesIntersectionElevated : exitRoadDescr.lanesIntersection;

                var center = (pos1 + pos2) * 0.5f;
                CreateIntersectionRoadMesh(exitLanes, leadingRoadDescr.settings, center,
                    exitRoadDescr.width, exitRoadDescr.road.length, exitRoadDescr.resolution, pos1, tan1, pos2, tan2,
                    combineMeshes, newMaterials, newSplines, 1f, 1f);
            }

            /********************************************************************************************************************************/
            // Closing Rectangles (Middle Lanes)

            var rectangleMaterials = new List<Material>();
            var rectangleCombines = new List<CombineInstance>();
            for (var i = 0; i < datas.Count; i++)
            {
                var closingCombineInstances = RoadEndCreation.ClosingRectangle(datas[i].roadDescr, datas[i].roadDescr.lanesMiddle, rectangleMaterials,
                    centerPosition, knots[i].Position, knots[i].TangentIn, datas[i].nearestKnotIndex == 0);
                rectangleCombines.AddRange(closingCombineInstances);
            }

            if (rectangleMaterials.Count > 0)
            {
                var closingRectangleMesh = new Mesh();
                closingRectangleMesh.CombineMeshes(rectangleCombines.ToArray(), true);

                newMaterials.Add(rectangleMaterials[0]);
                combineMeshes.Add(closingRectangleMesh);
            }

            /********************************************************************************************************************************/
            // Side Connections

            var overlap2D = new float2(centerAverage.x, centerAverage.z);
            var pos2D = new List<float2>();
            for (var i = 0; i < knots.Count; i++) pos2D.Add(new float2(knots[i].Position.x, knots[i].Position.z));

            for (var i = 0; i < datas.Count; i++)
            {
                // Connect each item with the next road clockwise
                var lowestClockwiseAngle = float.MaxValue;
                var sideDescrIndex = 0;
                var tan1 = pos2D[i] - overlap2D;
                for (var j = 0; j < datas.Count; j++)
                {
                    if (j == i) continue;
                    var tan2 = pos2D[j] - overlap2D;
                    var angleRad = PGTrigonometryUtility.AngleClockwise(tan1, tan2);
                    if (angleRad < lowestClockwiseAngle)
                    {
                        lowestClockwiseAngle = angleRad;
                        sideDescrIndex = j;
                    }
                }

                var mainDescrIndex = i;
                var laneMainLeftSide = true;
                var laneSideLeftSide = false;

                newPositions.Add(knots[mainDescrIndex].Position);


                var switchRoadDescr = false;
                if (datas[mainDescrIndex].roadDescr.road.priority == datas[sideDescrIndex].roadDescr.road.priority &&
                    mainDescrIndex < sideDescrIndex)
                    switchRoadDescr = true;
                else if (datas[mainDescrIndex].roadDescr.road.priority < datas[sideDescrIndex].roadDescr.road.priority)
                    switchRoadDescr = true;
                if (switchRoadDescr)
                {
                    (mainDescrIndex, sideDescrIndex) = (sideDescrIndex, mainDescrIndex);
                    (laneMainLeftSide, laneSideLeftSide) = (laneSideLeftSide, laneMainLeftSide);
                }

                var roadDescrMain = datas[mainDescrIndex].roadDescr;
                var roadDescrSide = datas[sideDescrIndex].roadDescr;
                var knotMain = knots[mainDescrIndex];
                var knotSide = knots[sideDescrIndex];

                if (roadDescrSide.road.roadName == leadingRoadDescr.road.roadName)
                {
                    (roadDescrMain, roadDescrSide) = (roadDescrSide, roadDescrMain);
                    (knotMain, knotSide) = (knotSide, knotMain);
                    (laneMainLeftSide, laneSideLeftSide) = (laneSideLeftSide, laneMainLeftSide);
                }

                CreateSideConnection(roadDescrMain, laneMainLeftSide,
                    roadDescrSide, laneSideLeftSide, centerAverage,
                    knotMain.Position, knotMain.TangentOut,
                    knotSide.Position, knotSide.TangentOut,
                    out var newSideMeshes, out var newSideMaterials, lodAmount);

                combineMeshes.AddRange(newSideMeshes);
                newMaterials.AddRange(newSideMaterials);
            }

            /********************************************************************************************************************************/
            PGMeshUtility.CombineAndPackMeshes(newMaterials, combineMeshes, out var intersectionMaterials, out var intersectionMesh);
            newMaterials = new List<Material>(intersectionMaterials);
            /********************************************************************************************************************************/

            return intersectionMesh;
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        private static void CreateIntersectionRoadMesh(List<Lane> lanes, ComponentSettings settings, float3 center,
            float partWidth, float partLength, int roadDescrResolution, float3 position01, float3 tangent01, float3 position02, float3 tangent02,
            List<Mesh> newMeshes, List<Material> newMaterials, List<Spline> newSplines, float widthStart, float widthEnd)
        {
            TangentCalculation.CalculateTangents(settings.smoothSlope, Constants.TangentLengthIntersection,
                position01, tangent01, position02, tangent02, true, center,
                out tangent01, out tangent02);

            var knot01 = new BezierKnot
                {Position = position01, Rotation = quaternion.identity, TangentIn = -tangent01, TangentOut = tangent01};
            var knot02 = new BezierKnot
                {Position = position02, Rotation = quaternion.identity, TangentIn = tangent02, TangentOut = -tangent02};

            var newSpline = new Spline
            {
                {knot01, TangentMode.Broken},
                {knot02, TangentMode.Broken}
            };

            var resolution =
                ConstructionSplineUtility.CalculateResolution(roadDescrResolution, settings.smartReduce, settings.smoothSlope, knot01, knot02, 1f);

            var splineMeshParameter =
                new SplineMeshParameter(partWidth, partLength, resolution, settings.splineLengthUV, newSpline, SplineLeftRight.Create);
            RoadSplineMesh.CreateMultipleSplineMeshes(lanes, splineMeshParameter,
                out var _newMeshes, out var _newMaterials, 0f, 1f, widthStart, widthEnd);

            newMeshes.AddRange(_newMeshes);
            newMaterials.AddRange(_newMaterials);
            newSplines.Add(newSpline);
        }


        /********************************************************************************************************************************/


        public static void CreateSideConnection(RoadDescr roadDescrMain,
            bool mainLeftSide, RoadDescr roadDescrSide, bool sideLeftSide, float3 center,
            float3 positionMain, float3 tangentMain, float3 positionSide, float3 tangentSide,
            out List<Mesh> newMeshes, out List<Material> newMaterials, float lodAmount)
        {
            newMeshes = new List<Mesh>();
            newMaterials = new List<Material>();
            var settings = roadDescrMain.settings;

            if (roadDescrMain.lanesLeft.Count == 0 && roadDescrSide.lanesLeft.Count == 0) return;
            if (roadDescrMain.lanesLeft.Count == 0)
            {
                (roadDescrMain, roadDescrSide) = (roadDescrSide, roadDescrMain);
                (mainLeftSide, sideLeftSide) = (sideLeftSide, mainLeftSide);
                (positionMain, positionSide) = (positionSide, positionMain);
                (tangentMain, tangentSide) = (tangentSide, tangentMain);
            }

            var sideLanesMain = mainLeftSide ? roadDescrMain.lanesLeftOffset : roadDescrMain.lanesRightOffset;
            var sideLanesSide = mainLeftSide ? roadDescrSide.lanesRightOffset : roadDescrSide.lanesLeftOffset;

            tangentMain = math.normalizesafe(tangentMain);
            tangentSide = math.normalizesafe(tangentSide);

            var center_2D = new float2(center.x, center.z);
            var positionMain_2D = new float2(positionMain.x, positionMain.z);
            var tangentMain_2D = new float2(tangentMain.x, tangentMain.z);
            var positionSide_2D = new float2(positionSide.x, positionSide.z);
            var tangentSide_2D = new float2(tangentSide.x, tangentSide.z);

            tangentMain_2D = PGTrigonometryUtility.DirectionalTangentToPoint(center_2D, positionMain_2D, tangentMain_2D);
            tangentSide_2D = PGTrigonometryUtility.DirectionalTangentToPoint(center_2D, positionSide_2D, tangentSide_2D);

            var tangentMainPerp_2D = math.normalizesafe(PGTrigonometryUtility.RotateTangent90Clockwise(tangentMain_2D));
            var tangentSidePerp_2D = math.normalizesafe(PGTrigonometryUtility.RotateTangent90Clockwise(tangentSide_2D));
            if (mainLeftSide) tangentMainPerp_2D *= -1f;
            if (sideLeftSide) tangentSidePerp_2D *= -1f;

            positionMain_2D += tangentMainPerp_2D * roadDescrMain.sideLanesCenterDistance;
            positionSide_2D += tangentSidePerp_2D * roadDescrSide.sideLanesCenterDistance;


            var posMain = new float3(positionMain_2D.x, positionMain.y, positionMain_2D.y);
            var posSide = new float3(positionSide_2D.x, positionSide.y, positionSide_2D.y);
            var tanMain = new float3(tangentMain_2D.x, 0f, tangentMain_2D.y);
            var tanSide = new float3(tangentSide_2D.x, 0f, tangentSide_2D.y);


            const float tangentLength = Constants.TangentLengthIntersection;

            tanMain = PGTrigonometryUtility.DirectionalTangentToPointXZ(center, posMain, tanMain);
            tanSide = PGTrigonometryUtility.DirectionalTangentToPointXZ(center, posSide, tanSide);

            TangentCalculation.CalculateTangents(settings.smoothSlope, tangentLength, posMain, tanMain, posSide, tanSide, false, float3.zero,
                out tanMain, out tanSide);

            var knots = new List<BezierKnot>
            {
                new(posMain, -tanMain, tanMain, quaternion.identity),
                new(posSide, tanSide, -tanSide, quaternion.identity)
            };

            var relativeWidth = roadDescrSide.sideLanesWidth / roadDescrMain.sideLanesWidth;

            if (sideLanesMain.Count != 0 && sideLanesSide.Count != 0)
            {
                var spline = new Spline(knots);

                var detailResolution =
                    ConstructionSplineUtility.CalculateResolution(roadDescrMain.detailResolution, roadDescrMain.settings.smartReduce,
                        roadDescrMain.settings.smoothSlope, knots[0], knots[1], lodAmount);

                for (var i = 0; i < sideLanesMain.Count; i++)
                {
                    var newMesh = new Mesh();

                    var splineMeshParameter = new SplineMeshParameter(roadDescrMain.sideLanesWidth, roadDescrMain.road.length, detailResolution,
                        SplineLengthUV.Cut, spline, SplineLeftRight.Create);
                    splineMeshParameter.widthRange = new Vector2(0f, 1f);
                    splineMeshParameter.widthStart = 1f;
                    splineMeshParameter.widthEnd = relativeWidth;
                    SplineMesh.CreateSplineMesh(newMesh, sideLanesMain[i].splineEdges, splineMeshParameter);

                    newMeshes.Add(newMesh);
                    newMaterials.Add(sideLanesMain[i].material);
                }
            }

            if (sideLanesSide.Count == 0) // Closing rectangle
            {
                var closingCombineInstances = new List<CombineInstance>();
                for (var i = 0; i < sideLanesMain.Count; i++)
                {
                    var lane = sideLanesMain[i];
                    if (lane.height <= 0f) continue;

                    var rectangleCombine = RoadEndCreation.ClosingRectangleCombine(roadDescrMain, lane, posMain, tanMain, mainLeftSide);
                    closingCombineInstances.Add(rectangleCombine);

                    var _newMesh = new Mesh();
                    _newMesh.CombineMeshes(closingCombineInstances.ToArray(), true);
                    newMeshes.Add(_newMesh);
                    newMaterials.Add(lane.material);
                    closingCombineInstances.Clear();
                }
            }
        }

        public static void CreateSideConnectionDirect(RoadDescr roadDescrMain, RoadDescr roadDescrSide, List<Lane> lanes,
            float3 positionMain, float3 tangentMain, float3 positionSide, float3 tangentSide,
            out List<Mesh> newMeshes, out List<Material> newMaterials, float lodAmount)
        {
            newMeshes = new List<Mesh>();
            newMaterials = new List<Material>();

            if (lanes.Count == 0) return;

            if (roadDescrSide.lanesLeft.Count == 0) // Closing rectangle
            {
                var closingCombineInstances = new List<CombineInstance>();
                for (var i = 0; i < lanes.Count; i++)
                {
                    var lane = lanes[i];
                    if (lane.height <= 0f) continue;

                    var rectangleCombine = RoadEndCreation.ClosingRectangleCombine(roadDescrMain, lane, positionMain, tangentMain, false);
                    closingCombineInstances.Add(rectangleCombine);

                    var _newMesh = new Mesh();
                    _newMesh.CombineMeshes(closingCombineInstances.ToArray(), true);
                    newMeshes.Add(_newMesh);
                    newMaterials.Add(lane.material);
                    closingCombineInstances.Clear();
                }

                return;
            }

            var tangentLength = Constants.TangentLengthIntersection;

            TangentCalculation.CalculateTangents(roadDescrMain.settings.smoothSlope, tangentLength, positionMain, tangentMain, positionSide,
                tangentSide, false, float3.zero,
                out tangentMain, out tangentSide);

            var knots = new List<BezierKnot>
            {
                new(positionMain, -tangentMain, tangentMain, quaternion.identity),
                new(positionSide, tangentSide, -tangentSide, quaternion.identity)
            };

            var relativeWidth = roadDescrSide.sideLanesWidth / roadDescrMain.sideLanesWidth;

            var spline = new Spline(knots);
            spline.SetTangentMode(TangentMode.Broken);

            var detailResolution =
                ConstructionSplineUtility.CalculateResolution(roadDescrMain.detailResolution, roadDescrMain.settings.smartReduce,
                    roadDescrMain.settings.smoothSlope, knots[0], knots[1], lodAmount);

            for (var i = 0; i < lanes.Count; i++)
            {
                var newMesh = new Mesh();

                var splineMeshParameter = new SplineMeshParameter(roadDescrMain.sideLanesWidth, roadDescrMain.road.length, detailResolution,
                    SplineLengthUV.Cut, spline, SplineLeftRight.Create);
                splineMeshParameter.widthRange = new Vector2(0f, 1f);
                splineMeshParameter.widthStart = 1f;
                splineMeshParameter.widthEnd = relativeWidth;

                SplineMesh.CreateSplineMesh(newMesh, lanes[i].splineEdges, splineMeshParameter);

                newMeshes.Add(newMesh);
                newMaterials.Add(lanes[i].material);
            }
        }

        /********************************************************************************************************************************/

        private static KnotData GetHighestPriorityMeshData(List<KnotData> createIntersectionMeshDatas)
        {
            return createIntersectionMeshDatas
                .OrderByDescending(data => data.roadDescr.road.priority)
                .ThenByDescending(data => data.roadDescr.width)
                .FirstOrDefault();
        }

        private static RoadDescr GetHighestPriorityRoadDescr(List<RoadDescr> roadDescrs)
        {
            return roadDescrs
                .OrderByDescending(data => data.road.priority)
                .ThenByDescending(data => data.width)
                .FirstOrDefault();
        }

        /********************************************************************************************************************************/

        public static List<Spline> CreateRailingSplines(IntersectionObject intersection)
        {
            var railingSplines = new List<Spline>();
            if (intersection.RoadConnections.Count < 2) return railingSplines;

            var roadDescr = intersection.roadDescr;
            var settings = roadDescr.settings;
            var centerPosition = intersection.centerPosition;

            var knotDatas = CreateKnotDatas(centerPosition, intersection.RoadConnections);
            OrderKnotDatas<bool>(centerPosition, knotDatas);

            for (var i = 0; i < knotDatas.Count; i++)
            {
                var data01 = knotDatas[i];
                var knot01 = data01.nearestKnot;
                var tangent01 = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, knot01.Position, data01.nearestKnot.TangentOut);
                tangent01.y = 0f;
                var tangentPerp01 = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(math.normalizesafe(tangent01));
                var offset01 = data01.roadDescr.width * 0.5f;
                knot01.Position -= tangentPerp01 * offset01;

                var data02 = knotDatas[i == knotDatas.Count - 1 ? 0 : i + 1];
                var knot02 = data02.nearestKnot;
                var tangent02 = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, knot02.Position, data02.nearestKnot.TangentOut);
                tangent02.y = 0f;
                var tangentPerp02 = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(math.normalizesafe(tangent02));
                var offset02 = data02.roadDescr.width * 0.5f;
                knot02.Position += tangentPerp02 * offset02;

                var spline = new Spline
                {
                    {knot01, TangentMode.Broken},
                    {knot02, TangentMode.Broken}
                };

                TangentCalculation.CalculateTangents(spline, settings.smoothSlope, Constants.TangentLengthIntersection, true, centerPosition);
                railingSplines.Add(spline);
            }

            return railingSplines;
        }
    }
}