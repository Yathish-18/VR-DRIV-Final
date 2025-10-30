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
    internal static class RampCreation
    {
        
        public static void CreateNewRamp(ConstructionObjects constructionObjects, Overlap overlap, RoadObject newRoadObject)
        {
            var roadDescr = newRoadObject.roadDescr;
            var splineRamp = newRoadObject.splineContainer.Spline;

            var oldRoadObject = overlap.roadObject;
            var settings = roadDescr.settings;
            var oldRoadSpline = oldRoadObject.splineContainer.Spline;

            var rampNearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(splineRamp, overlap.position);
            var rampNearestKnot = splineRamp.Knots.ElementAt(rampNearestKnotIndex);
            var tangent01 = rampNearestKnot.TangentOut;

            /********************************************************************************************************************************/
            // Replaced Road Objects left and right (reduced length)

            var angleLeft = math.abs(PGTrigonometryUtility.AngleXZ(tangent01, -overlap.tangent));
            var angleRight = math.abs(PGTrigonometryUtility.AngleXZ(tangent01, overlap.tangent));

            if (rampNearestKnotIndex != 0) (angleLeft, angleRight) = (angleRight, angleLeft);
            
            var existingWidth = oldRoadObject.roadDescr.width;
            var newWidth = roadDescr.width;
            var widthGapLeft = AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, newWidth, existingWidth, angleLeft);
            var widthGapRight = AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, newWidth, existingWidth, angleRight);
            
            RoadCreation.SplitRoad(oldRoadObject, overlap.t, widthGapLeft, widthGapRight, 1f,
                out var removableRoads, out var newRoadsLeft, out var newRoadsRight);
            
            var newRoads = new List<RoadObject>();
            newRoads.AddRange(newRoadsLeft);
            newRoads.AddRange(newRoadsRight);
            
            constructionObjects.removableRoads.AddRange(removableRoads);
            constructionObjects.newReplacedRoads.AddRange(newRoads);
            
            var tLeft = overlap.t - widthGapLeft / oldRoadObject.length;
            var tRight = overlap.t + widthGapRight / oldRoadObject.length;
            
            var splitSplineRight = newRoadsRight[0].splineContainer.Spline;

            /********************************************************************************************************************************/
            // Middle Road Spline

            var splineRoad = new Spline(oldRoadSpline);

            var insertIndexLeft = ConstructionSplineUtility.InsertKnotSeamless(splineRoad, tLeft);
            var insertIndexRight = ConstructionSplineUtility.InsertKnotSeamless(splineRoad, tRight);

            for (var i = splineRoad.Count - 1; i > insertIndexRight; i--) splineRoad.RemoveAt(i);
            for (var i = 0; i < insertIndexLeft; i++) splineRoad.RemoveAt(0);

            var knotRight = splineRoad[^1]; // Right position may not be exact (inserted two knots, right second)
            knotRight.Position = splitSplineRight[0].Position;
            splineRoad[^1] = knotRight;

            /********************************************************************************************************************************/
            // Mesh

            var centerPosition = splineRoad.EvaluatePosition(0.5f);
            var rampKnotData = new KnotData(roadDescr, rampNearestKnot, rampNearestKnotIndex);

            CreateRamp(constructionObjects, oldRoadObject.roadDescr, splineRoad, rampKnotData, centerPosition, oldRoadObject.elevated);
        }

        public static void CreateRamp(ConstructionObjects constructionObjects,
            RoadDescr roadDescr_Road, Spline spline_Road, KnotData rampKnotData,
            float3 centerPosition, bool elevated)
        {
            var rampMesh = CreateRampMesh(roadDescr_Road, spline_Road, rampKnotData, centerPosition, elevated, 1f,
                out var rampMaterials);

            var rampObj = ObjectUtility.CreateObj(Constants.PrefixRamp, roadDescr_Road.road.shadowCastingMode, out var meshFilter,
                out var meshRenderer);
            meshFilter.sharedMesh = rampMesh;
            meshRenderer.sharedMaterials = rampMaterials.ToArray();

            var _splineContainer = rampObj.AddComponent<SplineContainer>();

            var ramp = rampObj.AddComponent<RampObject>();
            ramp.Initialize(roadDescr_Road, meshFilter, meshRenderer, _splineContainer, elevated);
            ramp.centerPosition = centerPosition;

            var knotPositions = new List<float3>
            {
                spline_Road[0].Position,
                spline_Road[^1].Position,
                rampKnotData.nearestKnot.Position
            };
            
            var intersectionSplines = ConstructionSplineUtility.CreateIntersectionSplines(centerPosition, knotPositions);

            _splineContainer.Splines = intersectionSplines;

            /********************************************************************************************************************************/

            constructionObjects.newIntersections.Add(ramp);
        }


        public static Mesh CreateRampMesh(RoadDescr roadDescr_Road, Spline spline_Road,
            KnotData rampKnotData, float3 centerPosition, bool elevated, float lodAmount,
            out List<Material> newMaterials)
        {
            var roadDescr_Ramp = rampKnotData.roadDescr;
            var rampNearestKnot = rampKnotData.nearestKnot;
            var rampIn = rampKnotData.nearestKnotIndex != 0;
            
            newMaterials = new List<Material>();
            var combineMeshes = new List<Mesh>();

            var settings = roadDescr_Road.settings;
            var rampTangentIn =
                PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, rampNearestKnot.Position, rampNearestKnot.TangentOut);
            rampTangentIn = math.normalizesafe(rampTangentIn);
            var tangentCenter = math.normalizesafe(spline_Road.EvaluateTangent(0.5f));
            var tangentCenterPerp =
                PGTrigonometryUtility.RotateTangent90ClockwiseXZ(math.normalizesafe(new float3(tangentCenter.x, 0f, tangentCenter.z)));
            var roadIsForward = !PGTrigonometryUtility.IsSameDirectionXZ(tangentCenterPerp, rampTangentIn);
            var HeightOffset = math.up() * Constants.HeightOffset;

            /********************************************************************************************************************************/
            // Main Road

            var lanesRoad = new List<Lane>(roadDescr_Road.lanesMiddle);
            lanesRoad.AddRange(roadIsForward ? roadDescr_Road.lanesLeft : roadDescr_Road.lanesRight);
            if(elevated) lanesRoad.AddRange(roadDescr_Road.lanesElevatedOnly);

            var resolution =
                ConstructionSplineUtility.CalculateResolution(roadDescr_Road.resolution, settings.smartReduce, settings.smoothSlope, spline_Road[0], spline_Road[^1], lodAmount);
            var splineMeshParameterRoad = new SplineMeshParameter(roadDescr_Road.width, roadDescr_Road.road.length, resolution,
                settings.splineLengthUV, spline_Road, SplineLeftRight.Create);

            RoadSplineMesh.CreateMultipleSplineMeshes(lanesRoad, splineMeshParameterRoad,
                out var startEndRoadMeshes, out var startEndRoadMaterials);

            combineMeshes.AddRange(startEndRoadMeshes);
            newMaterials.AddRange(startEndRoadMaterials);

            /********************************************************************************************************************************/
            // Ramp -> Side Connections

            var rampTangentPerp =
                PGTrigonometryUtility.RotateTangent90ClockwiseXZ(math.normalizesafe(new float3(rampTangentIn.x, 0f, rampTangentIn.z)));
            if (!rampIn) rampTangentPerp *= -1f;
            var positionRampLeft = rampNearestKnot.Position - rampTangentPerp * (roadDescr_Ramp.width * 0.5f);
            var positionRampRight = rampNearestKnot.Position + rampTangentPerp * (roadDescr_Ramp.width * 0.5f);

            var splineRampNearestKnotLeft = rampNearestKnot;
            splineRampNearestKnotLeft.Position = positionRampLeft;
            var splineRampNearestKnotRight = rampNearestKnot;
            splineRampNearestKnotRight.Position = positionRampRight;

            // Side Connection Left
            
            var roadKnotStart = spline_Road[0];
            var roadTangentStart = roadKnotStart.TangentOut;
            
            CreateSideConnection(roadDescr_Road, roadDescr_Ramp, centerPosition, lodAmount, rampIn, roadIsForward, false,
                rampTangentIn, splineRampNearestKnotLeft, splineRampNearestKnotRight,
                rampNearestKnot, roadKnotStart, roadTangentStart, 
                out var rampExitLeftMeshes, out var rampExitLeftMaterials, out var sideSplineLeftMiddle);

            combineMeshes.AddRange(rampExitLeftMeshes);
            newMaterials.AddRange(rampExitLeftMaterials);


            // Side Connection Right

            var roadKnotEnd = spline_Road[^1];
            var roadTangentEnd = roadKnotEnd.TangentIn;
            
            CreateSideConnection(roadDescr_Road, roadDescr_Ramp, centerPosition, lodAmount, rampIn, roadIsForward, true,
                rampTangentIn, splineRampNearestKnotRight, splineRampNearestKnotLeft,
                rampNearestKnot, roadKnotEnd, roadTangentEnd, 
                out var rampExitRightMeshes, out var rampExitRightMaterials, out var sideSplineRightMiddle);

            combineMeshes.AddRange(rampExitRightMeshes);
            newMaterials.AddRange(rampExitRightMaterials);


            /********************************************************************************************************************************/
            // Ramp -> Intersection Exit

            var lanesRampExit = roadDescr_Ramp.lanesIntersection;

            // First from left to right, using the side splines

            if (!roadIsForward) (sideSplineLeftMiddle, sideSplineRightMiddle) = (sideSplineRightMiddle, sideSplineLeftMiddle);

            ConstructionSplineUtility.TranslateSpline(sideSplineLeftMiddle, HeightOffset);
            ConstructionSplineUtility.TranslateSpline(sideSplineRightMiddle, HeightOffset);

            var splineMeshParameterRampExit = new SplineMeshParameter(roadDescr_Ramp.width, roadDescr_Ramp.road.length,
                resolution,
                settings.splineLengthUV, sideSplineLeftMiddle, SplineLeftRight.Custom, sideSplineLeftMiddle, sideSplineRightMiddle, true);

            RoadSplineMesh.CreateMultipleSplineMeshes(lanesRampExit, splineMeshParameterRampExit,
                out var rampExitMeshes, out var rampExitMaterials);

            combineMeshes.AddRange(rampExitMeshes);
            newMaterials.AddRange(rampExitMaterials);

            if (elevated)
            {
                var lanesRampExitElevated = roadDescr_Ramp.lanesIntersectionElevated;

                var knot01 = new BezierKnot(rampNearestKnot.Position, -rampTangentIn, rampTangentIn, quaternion.identity);
                var knot02 = new BezierKnot(centerPosition, -rampTangentIn, rampTangentIn, quaternion.identity);
                var elevatedSpline = new Spline()
                {
                    {knot01, TangentMode.Broken},
                    {knot02, TangentMode.Broken}
                };

                var splineMeshParameterRampExitElevated = new SplineMeshParameter(roadDescr_Ramp.width, roadDescr_Ramp.road.length,
                    resolution, settings.splineLengthUV, elevatedSpline, SplineLeftRight.None);

                RoadSplineMesh.CreateMultipleSplineMeshes(lanesRampExitElevated, splineMeshParameterRampExitElevated,
                    out var rampExitMeshesElevated, out var rampExitMaterialsElevated);

                combineMeshes.AddRange(rampExitMeshesElevated);
                newMaterials.AddRange(rampExitMaterialsElevated);
            }

            // Now closing the gap from ramp to road

            var spline_Road_Offset = new Spline(spline_Road);
            var offsetX = roadDescr_Road.width * 0.5f - roadDescr_Road.sideLanesWidth;
            if (!roadIsForward) offsetX *= -1f;
            ConstructionSplineUtility.OffsetSplineParallel(spline_Road_Offset, offsetX);

            var nearestKnotLeft = spline_Road_Offset[0];
            var nearestKnotRight = spline_Road_Offset[^1];
            var tangentLeftRight = (nearestKnotRight.Position - nearestKnotLeft.Position) * 0.5f;
            nearestKnotLeft.Position += HeightOffset;
            nearestKnotRight.Position += HeightOffset;
            nearestKnotLeft.TangentIn = -tangentLeftRight;
            nearestKnotLeft.TangentOut = tangentLeftRight;
            nearestKnotRight.TangentIn = tangentLeftRight;
            nearestKnotRight.TangentOut = -tangentLeftRight;

            var splineRampLeftRight = new Spline
            {
                {nearestKnotLeft, TangentMode.Broken},
                {nearestKnotRight, TangentMode.Broken}
            };

            if (!roadIsForward) (splineRampLeftRight, spline_Road_Offset) = (spline_Road_Offset, splineRampLeftRight);

            var splineMeshParameterRampClosingExit = new SplineMeshParameter(roadDescr_Ramp.width, roadDescr_Ramp.road.length,
                resolution,
                settings.splineLengthUV, spline_Road_Offset, SplineLeftRight.Custom, spline_Road_Offset, splineRampLeftRight, true);

            RoadSplineMesh.CreateMultipleSplineMeshes(lanesRampExit, splineMeshParameterRampClosingExit,
                out var rampExitClosingMeshes, out var rampExitClosingMaterials);

            combineMeshes.AddRange(rampExitClosingMeshes);
            newMaterials.AddRange(rampExitClosingMaterials);


            /********************************************************************************************************************************/
            // Closing Rectangles (Middle Lanes)

            var rectangleMaterials = new List<Material>();
            var rectangleCombines = new List<CombineInstance>();

            var closingCombineInstances = RoadEndCreation.ClosingRectangle(roadDescr_Ramp, roadDescr_Ramp.lanesMiddle, rectangleMaterials,
                centerPosition, rampNearestKnot.Position, rampTangentIn, !rampIn);
            rectangleCombines.AddRange(closingCombineInstances);

            if (rectangleMaterials.Count > 0)
            {
                var closingRectangleMesh = new Mesh();
                closingRectangleMesh.CombineMeshes(rectangleCombines.ToArray(), true);

                newMaterials.Add(rectangleMaterials[0]);
                combineMeshes.Add(closingRectangleMesh);
            }

            /********************************************************************************************************************************/
            PGMeshUtility.CombineAndPackMeshes(newMaterials, combineMeshes, out var intersectionMaterials, out var intersectionMesh);
            newMaterials = new List<Material>(intersectionMaterials);
            /********************************************************************************************************************************/

            return intersectionMesh;
        }


        private static void CreateSideConnection(RoadDescr roadDescr_Road, RoadDescr roadDescr_Ramp, 
            float3 centerPosition, float lodAmount, bool rampIn, bool roadIsForward, bool rightSide,
            float3 rampTangent, BezierKnot splineRampNearestKnotLeft, BezierKnot splineRampNearestKnotRight,
            BezierKnot rampNearestKnot, BezierKnot roadKnot, float3 roadTangent,
            out List<Mesh> rampSideMeshes, out List<Material> rampSideMaterials, out Spline sideSplineMiddle)
        {
            var settings = roadDescr_Road.settings;
            
            var roadTangentStartPerp =
                PGTrigonometryUtility.RotateTangent90ClockwiseXZ(math.normalizesafe(new float3(roadTangent.x, 0f, roadTangent.z)));
            roadTangentStartPerp =
                PGTrigonometryUtility.DirectionalTangentToPointXZ(rampNearestKnot.Position, roadKnot.Position, roadTangentStartPerp);

            var positionSideRoad01 =
                roadKnot.Position + roadTangentStartPerp * (roadDescr_Road.width * 0.5f - roadDescr_Road.sideLanesWidth);
            var positionSideRoad02 = roadKnot.Position + roadTangentStartPerp * (roadDescr_Road.width * 0.5f);

            var positionSideRamp02 = (!rampIn && roadIsForward) || (rampIn && !roadIsForward)
                ? splineRampNearestKnotRight.Position
                : splineRampNearestKnotLeft.Position;

            var posLerp = roadDescr_Ramp.sideLanesWidth / roadDescr_Ramp.width;
            if (!rampIn) posLerp = 1f - posLerp;
            if (!roadIsForward) posLerp = 1f - posLerp;
            var positionSideRamp01 = math.lerp(splineRampNearestKnotLeft.Position, splineRampNearestKnotRight.Position, posLerp);

            var sideRoadKnot01 = new BezierKnot(positionSideRoad01, -roadTangent, roadTangent, quaternion.identity);
            var sideRoadKnot02 = new BezierKnot(positionSideRoad02, -roadTangent, roadTangent, quaternion.identity);

            var sideRampKnot01 = new BezierKnot(positionSideRamp01, -rampTangent, rampTangent, quaternion.identity);
            var sideRampKnot02 = new BezierKnot(positionSideRamp02, -rampTangent, rampTangent, quaternion.identity);

            var sideSpline01 = new Spline
            {
                {sideRampKnot01, TangentMode.Broken},
                {sideRoadKnot01, TangentMode.Broken}
            };

            var sideSpline02 = new Spline
            {
                {sideRampKnot02, TangentMode.Broken},
                {sideRoadKnot02, TangentMode.Broken}
            };
            
            TangentCalculation.CalculateTangents(sideSpline01, settings.smoothSlope, Constants.TangentLengthIntersection, true, centerPosition);
            TangentCalculation.CalculateTangents(sideSpline02, settings.smoothSlope, Constants.TangentLengthIntersection, true, centerPosition);

            if (!rightSide && !roadIsForward) (sideSpline01, sideSpline02) = (sideSpline02, sideSpline01);
            if (rightSide && roadIsForward) (sideSpline01, sideSpline02) = (sideSpline02, sideSpline01);

            var lanesExitLeft = roadIsForward ? roadDescr_Road.lanesLeftOffset : roadDescr_Road.lanesRightOffset;
            if (rightSide) lanesExitLeft = !roadIsForward ? roadDescr_Road.lanesLeftOffset : roadDescr_Road.lanesRightOffset;
            
            sideSplineMiddle = sideSpline01.GetLength() > sideSpline02.GetLength() ? sideSpline01 : sideSpline02;

            if (roadDescr_Road.lanesLeftOffset.Count == 0 
                || Mathf.Approximately(roadDescr_Ramp.sideLanesWidth, 0f)
                || Mathf.Approximately(roadDescr_Road.sideLanesWidth, 0f))
            {
                rampSideMeshes = new List<Mesh>();
                rampSideMaterials = new List<Material>();
                return;
            }
            
            var detailResolution =
                ConstructionSplineUtility.CalculateResolution(roadDescr_Road.detailResolution, settings.smartReduce, settings.smoothSlope, sideSplineMiddle[0], sideSplineMiddle[1],
                    lodAmount);

            var splineMeshParameterRampExit = new SplineMeshParameter(roadDescr_Road.sideLanesWidth, roadDescr_Road.road.length, detailResolution,
                settings.splineLengthUV, sideSplineMiddle, SplineLeftRight.Custom, sideSpline02, sideSpline01);

            RoadSplineMesh.CreateMultipleSplineMeshes(lanesExitLeft, splineMeshParameterRampExit,
                out rampSideMeshes, out rampSideMaterials);
        }
        
        
        /********************************************************************************************************************************/

        public static void RemoveRoad(RampObject ramp, RoadObject removableRoad, ConstructionObjects constructionObjects)
        {
            var roadConnections = new List<RoadObject>(ramp.RoadConnections);
            roadConnections.Remove(removableRoad);
            
            if (RoadCreation.TryMergeSplittedRoad(constructionObjects, roadConnections)) return; // Check if we can recreate a splitted road
            
            // Replacing ramp with a simple road
            var newRoad = RoadCreation.CreateReplaceRoadObject(ramp, ramp.SplineRoad, 1f);
            constructionObjects.newReplacedRoads.Add(newRoad);
        }
    }
}