// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    internal static class RoadCreation
    {
        public static RoadObject CreateRoad(RoadDescr roadDescr, Spline spline, bool elevated, bool rampRoad, float lodAmount)
        {
            var newSplineMesh = CreateRoadMesh(roadDescr, spline, lodAmount, elevated, out var _materials);

            var roadObj = ObjectUtility.CreateObj(Constants.PrefixRoad, roadDescr.road.shadowCastingMode, out var roadMeshFilter, out var roadMeshRenderer);

            roadMeshFilter.mesh = newSplineMesh;
            roadMeshRenderer.sharedMaterials = _materials;

            var roadObject = roadObj.AddComponent<RoadObject>();
            var splineContainer = roadObject.gameObject.AddComponent<SplineContainer>();
            splineContainer.Spline = spline;
            roadObject.InitializeRoad(roadDescr, roadMeshFilter, roadMeshRenderer, splineContainer, elevated, rampRoad);
            return roadObject;
        }

        public static Mesh CreateRoadMesh(RoadDescr roadDescr, Spline spline, float lodAmount, bool elevated,
            out Material[] materials)
        {
            var lanes = elevated ? roadDescr.lanesElevated : roadDescr.lanes;
            var road = roadDescr.road;
            var settings = roadDescr.settings;

            var resolution = ConstructionSplineUtility.CalculateResolution(roadDescr.resolution, roadDescr.settings.smartReduce, roadDescr.settings.smoothSlope, spline.Knots.First(),
                spline.Knots.Last(), lodAmount);

            var splineMeshParameter = new SplineMeshParameter(roadDescr.width, road.length, resolution, settings.splineLengthUV, spline, SplineLeftRight.Create);

            return RoadSplineMesh.CreateCombinedSplineMesh(lanes, splineMeshParameter, out materials);
        }

        public static void CreateRoadMesh(RoadDescr roadDescr, Spline spline, float lodAmount, bool elevated,
            out List<Mesh> meshes, out List<Material> materials)
        {
            var lanes = elevated ? roadDescr.lanesElevated : roadDescr.lanes;
            var road = roadDescr.road;
            var settings = roadDescr.settings;

            var resolution = ConstructionSplineUtility.CalculateResolution(roadDescr.resolution, roadDescr.settings.smartReduce, roadDescr.settings.smoothSlope, spline.Knots.First(),
                spline.Knots.Last(), lodAmount);

            var splineMeshParameter = new SplineMeshParameter(roadDescr.width, road.length, resolution, settings.splineLengthUV, spline, SplineLeftRight.Create);
            
            RoadSplineMesh.CreateMultipleSplineMeshes(lanes, splineMeshParameter, out meshes, out materials);
        }


        /********************************************************************************************************************************/

        public static RoadObject CreateReplaceRoadObject(SceneObject oldSceneObject, Spline spline, float lodAmount)
        {
            var roadDescr = oldSceneObject.roadDescr;
            var _lanes = oldSceneObject.elevated ? roadDescr.lanesElevated : roadDescr.lanes;
            return CreateReplaceRoadObject(oldSceneObject, spline, _lanes, lodAmount);
        }

        public static RoadObject CreateReplaceRoadObject(SceneObject oldSceneObject, Spline spline, List<Lane> lanes, float lodAmount)
        {
            var roadDescr = oldSceneObject.roadDescr;

            var rampRoad = false;
            if (oldSceneObject is RoadObject previousRoadObject) rampRoad = previousRoadObject.rampRoad;

            var roadObj = CreateReplaceRoadObj(roadDescr, lanes, spline,
                out var roadMeshFilter, out var roadMeshRenderer, lodAmount);

            var roadObject = roadObj.AddComponent<RoadObject>();
            var splineContainer = roadObject.gameObject.AddComponent<SplineContainer>();
            splineContainer.Spline = spline;
            roadObject.InitializeRoad(roadDescr, roadMeshFilter, roadMeshRenderer, splineContainer, oldSceneObject.elevated, rampRoad);

            return roadObject;
        }

        private static GameObject CreateReplaceRoadObj(RoadDescr roadDescr, List<Lane> lanes, Spline spline,
            out MeshFilter roadMeshFilter, out MeshRenderer roadMeshRenderer, float lodAmount)
        {
            var road = roadDescr.road;
            var settings = roadDescr.settings;

            var resolution = ConstructionSplineUtility.CalculateResolution(roadDescr.resolution, roadDescr.settings.smartReduce, roadDescr.settings.smoothSlope, spline.Knots.First(),
                spline.Knots.Last(),
                lodAmount);

            var splineMeshParameter = new SplineMeshParameter(roadDescr.width, road.length, resolution, settings.splineLengthUV, spline, SplineLeftRight.Create);

            var newSplineMesh = RoadSplineMesh.CreateCombinedSplineMesh(lanes, splineMeshParameter, out var _materials);

            var roadObj = ObjectUtility.CreateObj(Constants.PrefixRoad, road.shadowCastingMode, out roadMeshFilter, out roadMeshRenderer);

            roadMeshFilter.mesh = newSplineMesh;
            roadMeshRenderer.materials = _materials;

            return roadObj;
        }

        /********************************************************************************************************************************/
        
        public static void SplitRoad(RoadObject road, float t, float gapLeft, float gapRight, float lodAmount,
            out List<RoadObject> removableRoads, out List<RoadObject> newRoadsLeft, out List<RoadObject> newRoadsRight,
            bool flattenGap = false, float gapHeight = 0f)
        {
            removableRoads = new List<RoadObject>();
            var newRoadSplinesLeft = new List<Spline>();
            var newRoadSplinesRight = new List<Spline>();
            newRoadsLeft = new List<RoadObject>();
            newRoadsRight = new List<RoadObject>();
            var spline = road.splineContainer.Spline;
            var splitPosition = spline.EvaluatePosition(t);
            const float OffsetLeft = 0.001f;
            var splitPositionLeft = spline.EvaluatePosition(t - OffsetLeft);

            var roadLength = road.length;
            var relativeGapLeft = gapLeft / roadLength;
            var relativeGapRight = gapRight / roadLength;

            var tLeft = t - relativeGapLeft;
            if (tLeft is > 0f and < 1f)
            {
                var splineLeft = new Spline(spline);
                ConstructionSplineUtility.InsertKnotSeamless(splineLeft, tLeft);
                var curveIndexLeft = spline.SplineToCurveT(tLeft, out var curveTLeft);
                for (var i = splineLeft.Count - 1; i >= curveIndexLeft + 2; i--) splineLeft.RemoveAt(i);
                newRoadSplinesLeft.Add(splineLeft);
            }

            var tRight = t + relativeGapRight;
            if (tRight is > 0f and < 1f)
            {
                var splineRight = new Spline(spline);
                ConstructionSplineUtility.InsertKnotSeamless(splineRight, tRight);
                var curveIndexRight = spline.SplineToCurveT(tRight, out var curveTRight);
                for (var i = 0; i < curveIndexRight + 1; i++) splineRight.RemoveAt(0);
                newRoadSplinesRight.Add(splineRight);
            }

            // Split is at start or end of road, need to split connections as well
            if (newRoadSplinesLeft.Count == 0 || newRoadSplinesRight.Count == 0) 
            {
                var roadConnections = new List<RoadObject>(road.RoadConnections);
                for (var i = 0; i < roadConnections.Count; i++)
                {
                    var roadConnection = roadConnections[i];
                    var connectionSpline = roadConnection.splineContainer.Spline;
                    var connectionLength = roadConnection.length;
                    UnityEngine.Splines.SplineUtility.GetNearestPoint(connectionSpline, splitPosition, out var nearestPoint, out var nearestT);
                    var _distance = math.distance(nearestPoint, splitPosition);

                    // Determine if gapLeft or gapRight
                    var _distanceSq = math.distancesq(nearestPoint, splitPosition);
                    var _distanceSqLeft = math.distancesq(nearestPoint, splitPositionLeft);
                    var isGapLeft = true;
                    var gap = gapLeft;
                    if (_distanceSq < _distanceSqLeft)
                    {
                        gap = gapRight;
                        isGapLeft = false;
                    }

                    var missingGap = gap - _distance;
                    if (missingGap <= 0f) continue;

                    if (missingGap >= connectionLength) // Gap is larger than connection, need to add connections to the loop as well
                    {
                        for (var j = 0; j < roadConnection.RoadConnections.Count; j++)
                        {
                            var otherConnection = roadConnection.RoadConnections[j];
                            if (otherConnection.iD == road.iD) continue;
                            if (roadConnections.Contains(otherConnection)) continue;
                            roadConnections.Add(otherConnection);
                        }

                        removableRoads.Add(roadConnection);
                        continue;
                    }

                    var missingGapRatio = missingGap / connectionLength;

                    var newSpline = new Spline(connectionSpline);
                    var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(newSpline, splitPosition);
                    var _t = nearestKnotIndex == 0 ? missingGapRatio : 1f - missingGapRatio;
                    var _insertIndex = ConstructionSplineUtility.InsertKnotSeamless(newSpline, _t);

                    if (nearestKnotIndex == 0)
                        for (var j = _insertIndex - 1; j >= 0; j--)
                            newSpline.RemoveAt(j);
                    else
                        for (var j = _insertIndex + 1; j < newSpline.Count; j++)
                            newSpline.RemoveAt(j);

                    if(isGapLeft) newRoadSplinesLeft.Add(newSpline);
                    else newRoadSplinesRight.Add(newSpline);
                    removableRoads.Add(roadConnection);
                }
            }

            if (flattenGap)
            {
                var combinedSplines = new List<Spline>();
                combinedSplines.AddRange(newRoadSplinesLeft);
                combinedSplines.AddRange(newRoadSplinesRight);
                for (var i = 0; i < combinedSplines.Count; i++)
                {
                    var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(combinedSplines[i], splitPosition);
                    var nearestKnot = combinedSplines[i][nearestKnotIndex];
                    nearestKnot.Position = new Vector3(nearestKnot.Position.x, gapHeight, nearestKnot.Position.z);
                    combinedSplines[i].SetKnot(nearestKnotIndex, nearestKnot);
                }
            }

            removableRoads.Add(road);
            for (var i = 0; i < newRoadSplinesLeft.Count; i++)
            {
                var newRoad = CreateReplaceRoadObject(road, newRoadSplinesLeft[i], lodAmount);
                newRoad.splitOriginalID = road.iD;
                newRoad.splitOriginalSpline = new Spline(spline);
                newRoadsLeft.Add(newRoad);
            }
            for (var i = 0; i < newRoadSplinesRight.Count; i++)
            {
                var newRoad = CreateReplaceRoadObject(road, newRoadSplinesRight[i], lodAmount);
                newRoad.splitOriginalID = road.iD;
                newRoad.splitOriginalSpline = new Spline(spline);
                newRoadsRight.Add(newRoad);
            }
        }

        public static bool TryMergeSplittedRoad(ConstructionObjects constructionObjects, List<RoadObject> roadConnections)
        {
            if (roadConnections.Count != 2) return false;
            
            var road01 = roadConnections[0];
            var road02 = roadConnections[1];
            if (road01.splitOriginalID != string.Empty && road01.splitOriginalID == road02.splitOriginalID && road01.splitOriginalSpline != null)
            {
                var mergedRoad = CreateReplaceRoadObject(road01, road01.splitOriginalSpline, 1f);
                constructionObjects.newRoads.Add(mergedRoad);
                constructionObjects.removableRoads.Add(road01);
                constructionObjects.removableRoads.Add(road02);
                return true;
            }
            return false;
        }
        
                
        /********************************************************************************************************************************/
        
        public static List<Spline> CreateRailingSplines(RoadObject road)
        {
            var railingSplines = new List<Spline>();
            var splineLeft = new Spline(road.splineContainer.Spline);
            ConstructionSplineUtility.OffsetSplineParallel(splineLeft, -road.Width * 0.5f);
            railingSplines.Add(splineLeft);
            var splineRight = new Spline(road.splineContainer.Spline);
            ConstructionSplineUtility.OffsetSplineParallel(splineRight, road.Width * 0.5f);
            ConstructionSplineUtility.InvertSpline(splineRight);
            railingSplines.Add(splineRight);
            return railingSplines;
        }
    }
}