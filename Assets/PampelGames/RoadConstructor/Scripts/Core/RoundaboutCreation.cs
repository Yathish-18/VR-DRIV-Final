// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using SplineUtility = UnityEngine.Splines.SplineUtility;

namespace PampelGames.RoadConstructor
{
    internal static class RoundaboutCreation
    {
        public static void AddRoad(ConstructionObjects constructionObjects,
            Overlap overlap, RoadObject newRoadObject, RoadDescr roadDescr, float3 centerPosition, RoundaboutDesign design, float radius)
        {
            var roadConnections = new List<RoadObject>();

            var oldRoundabout = overlap.intersectionObject as RoundaboutObject;
            roadConnections.AddRange(oldRoundabout!.RoadConnections);
            if (!roadConnections.Contains(newRoadObject)) roadConnections.Add(newRoadObject);

            var meshDatas = IntersectionCreation.CreateKnotDatas(centerPosition, roadConnections);
            var roundabout = CreateRoundabout(roadDescr, meshDatas, centerPosition, design, radius);

            var existingConnections = oldRoundabout.RoadConnections;

            constructionObjects.newReplacedIntersections.Add(roundabout);
            constructionObjects.removableIntersections.Add(oldRoundabout);

            for (var i = 0; i < existingConnections.Count; i++)
            {
                var oldRoadConnection = existingConnections[i];
                var newRoadConnection = RoadCreation.CreateReplaceRoadObject(oldRoadConnection, oldRoadConnection.splineContainer.Spline, 1f);

                var newRoad = newRoadConnection.GetComponent<RoadObject>();
                constructionObjects.newReplacedRoads.Add(newRoad);
                constructionObjects.removableRoads.Add(oldRoadConnection);
            }
        }

        public static void RemoveRoad(RoundaboutObject roundabout, RoadObject removableRoad, ConstructionObjects constructionObjects)
        {
            if (!roundabout.RoadConnections.Contains(removableRoad)) return;
            var roadConnections = new List<RoadObject>(roundabout.RoadConnections);
            roadConnections.Remove(removableRoad);

            var knotDatas = IntersectionCreation.CreateKnotDatas(roundabout.centerPosition, roadConnections);

            var newRoundabout = CreateRoundabout(roundabout.roadDescr, knotDatas, roundabout.centerPosition, roundabout.roundaboutDesign,
                roundabout.radius);
            constructionObjects.newReplacedIntersections.Add(newRoundabout);
        }

        public static RoundaboutObject CreateRoundabout(RoadDescr roadDescr, List<KnotData> knotDatas, float3 centerPosition,
            RoundaboutDesign design, float radius)
        {
            var combinedMesh = CreateRoundaboutMesh(roadDescr, centerPosition, design, radius, 1f,
                knotDatas, out var combinedMaterials, out var roadSplines, out var splineMiddle);

            var roundaboutObj = ObjectUtility.CreateObj(Constants.PrefixRoundabout, roadDescr.road.shadowCastingMode, out var meshFilter,
                out var meshRenderer);
            meshFilter.mesh = combinedMesh;
            meshRenderer.materials = combinedMaterials;

            var _splineContainer = roundaboutObj.AddComponent<SplineContainer>();
            _splineContainer.Spline = splineMiddle;
            for (var i = 0; i < roadSplines.Count; i++) _splineContainer.AddSpline(roadSplines[i]);

            var raycastOffset = Constants.RaycastOffset(roadDescr.settings);
            var elevated = WorldUtility.CheckElevation(meshRenderer.bounds, raycastOffset, roadDescr.settings.groundLayers,
                roadDescr.settings.elevationStartHeight);

            var newRoundabout = roundaboutObj.AddComponent<RoundaboutObject>();
            newRoundabout.Initialize(roadDescr, meshFilter, meshRenderer, _splineContainer, elevated);
            newRoundabout.centerPosition = centerPosition;
            newRoundabout.radius = radius;
            newRoundabout.roundaboutDesign = design;

            return newRoundabout;
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public static Mesh CreateRoundaboutMesh(RoadDescr roundaboutRoadDescr, float3 centerPosition, RoundaboutDesign design, float radius,
            float lodAmount, List<KnotData> meshDatas,
            out Material[] combinedMaterials, out List<Spline> roadSplines, out Spline splineMiddle)
        {
            var settings = roundaboutRoadDescr.settings;
            var road = roundaboutRoadDescr.road;
            roadSplines = new List<Spline>();

            var allMeshes = new List<Mesh>();
            var allMaterials = new List<Material>();

            var resolution = (int) math.round(settings.resolution * lodAmount);
            if (resolution == 0) resolution = 1;

            splineMiddle = SplineCircle.CreateCircleSpline(radius, centerPosition, quaternion.identity, true);
            var splineMiddleLength = splineMiddle.GetLength();
            var radiusInside = math.max(0.01f, radius - roundaboutRoadDescr.sideLanesCenterDistance
                                               + roundaboutRoadDescr.sideLanesWidth * 0.05f); // Small offset to fill gaps
            var splineInside = SplineCircle.CreateCircleSpline(radiusInside, centerPosition, quaternion.identity, true);
            var radiusOutside = radius + roundaboutRoadDescr.sideLanesCenterDistance;
            var splineOutside = SplineCircle.CreateCircleSpline(radiusOutside, centerPosition, quaternion.identity, true);

            /********************************************************************************************************************************/
            // Middle
            var splineMiddleLeft =
                SplineCircle.CreateCircleSpline(radius - roundaboutRoadDescr.width * 0.5f, centerPosition, quaternion.identity, true);
            var splineMiddleRight =
                SplineCircle.CreateCircleSpline(radius + roundaboutRoadDescr.width * 0.5f, centerPosition, quaternion.identity, true);
            var splineMeshParameterMiddle = new SplineMeshParameter(roundaboutRoadDescr.width, road.length, resolution, settings.splineLengthUV,
                splineMiddle,
                SplineLeftRight.Custom, splineMiddleLeft, splineMiddleRight);
            RoadSplineMesh.CreateMultipleSplineMeshes(roundaboutRoadDescr.lanesMiddle, splineMeshParameterMiddle,
                out var _meshes, out var _materials);
            allMeshes.AddRange(_meshes);
            allMaterials.AddRange(_materials);

            /********************************************************************************************************************************/
            // Inside

            if (design == RoundaboutDesign.Default)
            {
                var splineInsideLeft = SplineCircle.CreateCircleSpline(radiusInside - roundaboutRoadDescr.width * 0.5f, centerPosition,
                    quaternion.identity, true);
                var splineInsideRight = SplineCircle.CreateCircleSpline(radiusInside + roundaboutRoadDescr.width * 0.5f, centerPosition,
                    quaternion.identity, true);
                var splineMeshParameterInside =
                    new SplineMeshParameter(roundaboutRoadDescr.width, road.length, resolution, settings.splineLengthUV, splineInside, SplineLeftRight.Custom,
                        splineInsideLeft, splineInsideRight);
                RoadSplineMesh.CreateMultipleSplineMeshes(roundaboutRoadDescr.lanesLeftOffset, splineMeshParameterInside,
                    out var _meshesIn, out var _materialsIn);
                allMeshes.AddRange(_meshesIn);
                allMaterials.AddRange(_materialsIn);
            }
            else
            {
                var circleMesh = new Mesh();
                PGMeshCreation.Circle(circleMesh, radius, 12);
                var offset = centerPosition + new float3(0f, settings.baseRoadHeight + Constants.HeightOffset, 0f);
                var vertices = PGMeshUtility.CreateVertexList(circleMesh);
                PGMeshUtility.PGTranslateVertices(vertices, offset);
                circleMesh.SetVertices(vertices);
                allMeshes.Add(circleMesh);
                allMaterials.Add(roundaboutRoadDescr.intersectionMaterial);
            }


            /********************************************************************************************************************************/
            // Outside
            var outsideTGaps = new List<Vector2>();
            var splineOutsideLength = splineOutside.GetLength();

            for (var i = 0; i < meshDatas.Count; i++)
            {
                var roadDescr = meshDatas[i].roadDescr;
                var deltaWidth = roadDescr.width * 0.5f + settings.intersectionDistance;
                var deltaWidthT = deltaWidth / splineOutsideLength;
                var nearestKnotIndex = meshDatas[i].nearestKnotIndex;
                var nearestKnot = meshDatas[i].nearestKnot;
                var startPart = nearestKnotIndex == 0;
                var tangent = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, nearestKnot.Position, nearestKnot.TangentOut);
                tangent.y = 0f;
                tangent = math.normalizesafe(tangent);
                var tangentPerp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangent);

                SplineUtility.GetNearestPoint(splineOutside, nearestKnot.Position, out var nearestPositionOutside, out var nearestTOutside);

                /********************************************************************************************************************************/
                // For the outer ring (mesh created below)
                var nearestRoundTLeft = nearestTOutside - deltaWidthT;
                var nearestRoundTRight = nearestTOutside + deltaWidthT;
                if (nearestRoundTLeft < 0) outsideTGaps.Add(new Vector2(nearestRoundTLeft + 1, 1));
                if (nearestRoundTRight > 1) outsideTGaps.Add(new Vector2(0, nearestRoundTRight - 1));
                outsideTGaps.Add(new Vector2(Math.Max(0, nearestRoundTLeft), Math.Min(1, nearestRoundTRight)));

                if (nearestRoundTLeft < 0) nearestRoundTLeft += 1;
                if (nearestRoundTRight > 1) nearestRoundTRight -= 1;

                /********************************************************************************************************************************/
                // Side Connections
                splineOutside.Evaluate(nearestRoundTLeft, out var positionLeft, out var tangentLeft, out var upVectorLeft);
                tangentLeft.y = 0f;
                var nearestPosLeft = nearestKnot.Position - tangentPerp * roadDescr.sideLanesCenterDistance;

                var lanesLeft = roundaboutRoadDescr.lanesRightOffset;
                IntersectionCreation.CreateSideConnectionDirect(roundaboutRoadDescr, roadDescr, lanesLeft,
                    positionLeft, tangentLeft, nearestPosLeft, tangent,
                    out var newSideMeshesLeft, out var newSideMaterialsLeft, lodAmount);

                allMeshes.AddRange(newSideMeshesLeft);
                allMaterials.AddRange(newSideMaterialsLeft);

                splineOutside.Evaluate(nearestRoundTRight, out var positionRight, out var tangentRight, out var upVectorRight);
                tangentRight.y = 0f;
                tangentRight *= -1f;
                var nearestPosRight = nearestKnot.Position + tangentPerp * roadDescr.sideLanesCenterDistance;

                var lanesRight = roundaboutRoadDescr.lanesLeftOffset;
                IntersectionCreation.CreateSideConnectionDirect(roundaboutRoadDescr, roadDescr, lanesRight,
                    positionRight, tangentRight, nearestPosRight, tangent,
                    out var newSideMeshesRight, out var newSideMaterialsRight, lodAmount);

                allMeshes.AddRange(newSideMeshesRight);
                allMaterials.AddRange(newSideMaterialsRight);

                /********************************************************************************************************************************/
                // Adding one connecting road mesh
                SplineUtility.GetNearestPoint(splineMiddle, nearestKnot.Position, out var nearestPositionMiddle, out var nearestTMiddle);

                var knot02 = new BezierKnot
                {
                    Position = nearestPositionMiddle + new float3(0f, Constants.HeightOffset, 0f),
                    Rotation = quaternion.identity,
                    TangentOut = tangent,
                    TangentIn = tangent
                };

                var roadSpline = new Spline(new List<BezierKnot> {nearestKnot, knot02});
                TangentCalculation.CalculateTangents(roadSpline, settings.smoothSlope, 0.5f);

                var splineMeshParameter = new SplineMeshParameter(roadDescr.width, roadDescr.road.length, 1, settings.splineLengthUV, roadSpline, SplineLeftRight.Create);
                RoadSplineMesh.CreateMultipleSplineMeshes(roadDescr.lanesIntersection, splineMeshParameter,
                    out var roadMeshes, out var roadMaterials);

                allMeshes.AddRange(roadMeshes);
                allMaterials.AddRange(roadMaterials);


                // Two splines (in and out)
                var widthT = meshDatas[i].roadDescr.width * 0.5f / splineMiddleLength;
                var connectionKnot = meshDatas[i].nearestKnot;
                var tangentIn =
                    PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, connectionKnot.Position, connectionKnot.TangentOut);
                SplineUtility.GetNearestPoint(splineMiddle, connectionKnot.Position, out var nearestRound, out var tAround);

                // Connection Out (Left)
                var leftT = (tAround - widthT) % 1;
                if (leftT < 0) leftT += 1;
                splineMiddle.Evaluate(leftT, out var positionLeftOut, out var tangentLeftOut, out var upVectorLeftOut);
                var leftKnot01 = new BezierKnot(positionLeftOut, -tangentLeftOut, tangentLeftOut, quaternion.identity);
                var leftKnot02 = new BezierKnot(connectionKnot.Position, -tangentIn, tangentIn, quaternion.identity);

                var connectionSplineLeft = new Spline
                {
                    {leftKnot01, TangentMode.Broken},
                    {leftKnot02, TangentMode.Broken}
                };

                TangentCalculation.CalculateTangents(connectionSplineLeft, settings.smoothSlope, Constants.TangentLengthIntersection);
                roadSplines.Add(connectionSplineLeft);

                // Connection In (Right)
                var rightT = (tAround + widthT) % 1;
                if (rightT < 0) rightT += 1;
                splineMiddle.Evaluate(rightT, out var positionRightIn, out var tangentRightIn, out var upVectorRightIn);
                var rightKnot01 = new BezierKnot(positionRightIn, -tangentRightIn, tangentRightIn, quaternion.identity);
                var rightKnot02 = new BezierKnot(connectionKnot.Position, -tangentIn, tangentIn, quaternion.identity);

                var connectionSplineRight = new Spline
                {
                    {rightKnot02, TangentMode.Broken},
                    {rightKnot01, TangentMode.Broken}
                };

                TangentCalculation.CalculateTangents(connectionSplineRight, settings.smoothSlope, Constants.TangentLengthIntersection);
                roadSplines.Add(connectionSplineRight);


                /********************************************************************************************************************************/
                // Closing Rectangles (Middle Lanes)

                var rectangleMaterials = new List<Material>();
                var rectangleCombines = new List<CombineInstance>();

                var closingCombineInstances = RoadEndCreation.ClosingRectangle(roadDescr, roadDescr.lanesMiddle, rectangleMaterials,
                    centerPosition, nearestKnot.Position, nearestKnot.TangentIn, startPart);
                rectangleCombines.AddRange(closingCombineInstances);

                if (rectangleMaterials.Count > 0)
                {
                    var closingRectangleMesh = new Mesh();
                    closingRectangleMesh.CombineMeshes(rectangleCombines.ToArray(), true);
                    allMaterials.Add(rectangleMaterials[0]);
                    allMeshes.Add(closingRectangleMesh);
                }
            }

            /********************************************************************************************************************************/
            /********************************************************************************************************************************/
            // Now creating the outer ring mesh

            var splineOutsideLeft =
                SplineCircle.CreateCircleSpline(radiusOutside - roundaboutRoadDescr.width * 0.5f, centerPosition, quaternion.identity, true);
            var splineOutsideRight =
                SplineCircle.CreateCircleSpline(radiusOutside + roundaboutRoadDescr.width * 0.5f, centerPosition, quaternion.identity, true);

            outsideTGaps.Sort((v1, v2) => v1.x.CompareTo(v2.x));
            var outsideTs = new List<Vector2>(); // Outer ring with gaps for the roads.
            float lastEnd = 0;
            foreach (var val in outsideTGaps) // Inverting the outsideTGaps
            {
                if (val.x > lastEnd) outsideTs.Add(new Vector2(lastEnd, val.x));
                lastEnd = Math.Max(lastEnd, val.y);
            }

            if (lastEnd < 1) outsideTs.Add(new Vector2(lastEnd, 1));

            if (meshDatas.Count == 0) outsideTs.Add(new Vector2(0f, 1f));
            for (var i = 0; i < outsideTs.Count; i++) AddOutsideSplineMesh(outsideTs[i].x, outsideTs[i].y);

            void AddOutsideSplineMesh(float tStart, float tEnd)
            {
                var splineMeshParameter = new SplineMeshParameter(roundaboutRoadDescr.width, road.length, resolution, settings.splineLengthUV,
                    splineOutside,
                    SplineLeftRight.Custom, splineOutsideLeft, splineOutsideRight);
                RoadSplineMesh.CreateMultipleSplineMeshes(roundaboutRoadDescr.lanesRightOffset, splineMeshParameter,
                    out var _meshesOut, out var _materialsOut, tStart, tEnd);
                allMeshes.AddRange(_meshesOut);
                allMaterials.AddRange(_materialsOut);
            }


            /********************************************************************************************************************************/

            PGMeshUtility.CombineAndPackMeshes(allMaterials, allMeshes, out combinedMaterials, out var combinedMesh);
            return combinedMesh;
        }
    }
}