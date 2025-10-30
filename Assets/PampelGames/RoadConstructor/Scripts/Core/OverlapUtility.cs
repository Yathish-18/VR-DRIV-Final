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
    internal static class OverlapUtility
    {
        public static Overlap GetOverlap(ComponentSettings settings, float width, float height, float3 position,
            RoadConstructor.SceneData sceneData, List<ConstructionBase> integrations)
        {
            var snappingDistance = settings.snapDistance * width;
            var bounds = new Bounds(position, new Vector3(snappingDistance, height, snappingDistance));

            return GetOverlap(settings, snappingDistance, bounds, sceneData, integrations);
        }

        public static Overlap GetOverlap(ComponentSettings settings, float snappingDistance, Bounds bounds, RoadConstructor.SceneData sceneData,
            List<ConstructionBase> integrations)
        {
            var overlap = new Overlap();
            
            
            /********************************************************************************************************************************/
            // Intersections
            
            var intersectionIndexes = WorldUtility.FindOverlappingIndexes(bounds, sceneData.intersectionBounds);

            if (intersectionIndexes.Count > 0)
            {
                var intersectionIndex = GetNearestIntersectionIndex(bounds.center, sceneData.intersectionObjects, intersectionIndexes,
                    sceneData.intersectionBounds,
                    snappingDistance);

                if (intersectionIndex >= 0)
                {
                    var intersection = sceneData.intersectionObjects[intersectionIndex];
                    overlap.exists = true;
                    overlap.overlapType = OverlapType.Intersection;
                    overlap.intersectionObject = intersection;
                    overlap.spline = intersection.splineContainer.Spline;

                    if (intersection.GetType() == typeof(RoundaboutObject))
                    {
                        SplineUtility.GetNearestPoint(overlap.spline, bounds.center, out overlap.position, out overlap.t);
                        overlap.tangent = overlap.spline.EvaluateTangent(overlap.t);
                        overlap.tangent.y = 0f;
                    }
                    else // IntersectionObject
                    {
                        overlap.position = intersection.SnapPosition(bounds.center);
                        
                        if (overlap.IsEndObject())
                        {
                            var spline = intersection.RoadConnections[0].splineContainer.Spline;
                            var nearestIndex = ConstructionSplineUtility.GetNearestKnotIndex(spline, overlap.position);
                            var nearestKnot = spline.Knots.ElementAt(nearestIndex);
                            var overlapTangent = math.normalizesafe(nearestKnot.TangentOut);
                            overlapTangent.y = 0f;
                            var boundsCenter = overlap.BoundsCenter;
                            var tangentOut = PGTrigonometryUtility.DirectionalTangentToPointXZ(boundsCenter, overlap.position, overlapTangent);
                            overlap.tangent = tangentOut;
                        }
                    }

                    return overlap;
                }
            }

            
            /********************************************************************************************************************************/
            // Integrations
            
            for (int i = 0; i < integrations.Count; i++)
            {
                if (!settings.integrationActive) break;
                var integration = integrations[i];
                if(!integration.TryGetSceneObject(bounds.center, bounds.extents.x, out var sceneObjectBase)) continue;
                
                if (sceneObjectBase.TryGetComponent<ISharedObject>(out var sharedObject) &&
                    ConnectionPointUtility.TryGetFreeConnection(sharedObject.GetConnectionPoints(), bounds.center, out var connectionPoint))
                {
                    overlap.exists = true;
                    overlap.overlapType = OverlapType.Shared;
                    overlap.sharedSceneObject = sceneObjectBase;
                    overlap.sharedObject = sharedObject;
                    overlap.position = connectionPoint.position;
                    overlap.tangent = connectionPoint.tangent;

                }
                else if (settings.integrationDetectOverlap)
                {
                    overlap.exists = true;
                    overlap.overlapType = OverlapType.Shared;
                    overlap.sharedSceneObject = sceneObjectBase;
                    overlap.position = sceneObjectBase.SnapPosition(bounds.center);
                }
                
                return overlap;
            }
            
            
            /********************************************************************************************************************************/
            // Roads
            
            var roadIndexes = WorldUtility.FindOverlappingIndexes(bounds, sceneData.roadBounds);
            var roadIndexesSnapped = new List<int>();

            for (var i = roadIndexes.Count - 1; i >= 0; i--)
            {
                var index = roadIndexes[i];
                var roadObject = sceneData.roadObjects[index];
                if (!roadObject.snapPositionSet) continue;
                roadIndexesSnapped.Add(index);
                roadIndexes.RemoveAt(i);
            }

            /********************************************************************************************************************************/
            if (roadIndexesSnapped.Count > 0)
            {
                var roadIndex = GetNearestSnappedRoadIndex(bounds.center, sceneData.roadObjects, roadIndexesSnapped, snappingDistance);

                if (roadIndex >= 0)
                {
                    var road = sceneData.roadObjects[roadIndex];
                    overlap.exists = true;
                    overlap.overlapType = OverlapType.Road;
                    overlap.roadObject = road;
                    overlap.spline = road.splineContainer.Spline;
                    overlap.position = road.snapPosition;
                    overlap.tangent = road.splineContainer.Spline.EvaluateTangent(0.5f);

                    return overlap;
                }
            }

            /********************************************************************************************************************************/
            if (roadIndexes.Count > 0)
            {
                var minDistance = float.MaxValue;
                var splinePartFound = false;
                var foundIndex = -1;

                for (var i = 0; i < roadIndexes.Count; i++)
                {
                    var index = roadIndexes[i];
                    var roadObject = sceneData.roadObjects[index];
                    var roadSpline = roadObject.splineContainer.Spline;

                    var nearest = roadObject.SnapRoadPosition(bounds.center, out var tCalc);
                    
                    var distance = math.distance(nearest, bounds.center);

                    if (distance < minDistance && distance < snappingDistance)
                    {
                        minDistance = distance;
                        splinePartFound = true;
                        foundIndex = index;
                        overlap.position = nearest;
                        overlap.t = tCalc;
                        overlap.spline = roadSpline;
                    }
                }

                if (splinePartFound)
                {
                    overlap.exists = true;
                    overlap.overlapType = OverlapType.Road;
                    overlap.spline.Evaluate(overlap.t, out overlap.position, out overlap.tangent, out overlap.upVector);
                    overlap.roadObject = sceneData.roadObjects[foundIndex];
                    return overlap;
                }
            }

            return overlap;
        }


        private static int GetNearestIntersectionIndex(float3 position, List<IntersectionObject> intersectionObjects,
            List<int> intersectionIndexes, List<Bounds> intersectionBounds, float _snapDistance)
        {
            var nearestIndex = -1;
            var minDistance = float.MaxValue;
            for (var i = 0; i < intersectionIndexes.Count; i++)
            {
                var index = intersectionIndexes[i];
                var intersection = intersectionObjects[index];

                if (intersection.GetType() == typeof(RoundaboutObject))
                {
                    SplineUtility.GetNearestPoint(intersection.splineContainer.Spline, position, out var nearest, out var t);
                    var distance = math.distance(nearest, position);

                    if (distance <= minDistance && distance <= _snapDistance)
                    {
                        nearestIndex = index;
                        minDistance = distance;
                    }
                }
                else // Intersection
                {
                    var bounds = intersectionBounds[index];
                    var distance = math.distance(bounds.center, position);

                    if (distance <= minDistance && distance <= _snapDistance)
                    {
                        nearestIndex = index;
                        minDistance = distance;
                    }
                }
            }

            return nearestIndex;
        }

        private static int GetNearestSnappedRoadIndex(float3 position, List<RoadObject> roadObjects, List<int> roadIndexes, float _snapDistance)
        {
            var nearestIndex = -1;
            var minDistance = float.MaxValue;
            for (var i = 0; i < roadIndexes.Count; i++)
            {
                var index = roadIndexes[i];
                var road = roadObjects[index];

                var distance = math.distance(road.snapPosition, position);

                if (distance <= minDistance && distance <= _snapDistance)
                {
                    nearestIndex = index;
                    minDistance = distance;
                }
            }

            return nearestIndex;
        }

        /********************************************************************************************************************************/

        public static void GetAllOverlaps(Bounds bounds, List<SceneObjectBase> ignoreObjects, RoadConstructor.SceneData sceneData,
            out List<IntersectionObject> overlapIntersections, out List<RoadObject> overlapRoads)
        {
            GetAllOverlapIndexes(bounds, ignoreObjects, sceneData,
                out var overlapIntersectionIndexes, out var overlapRoadIndexes);

            overlapIntersections = new List<IntersectionObject>();
            for (var i = 0; i < overlapIntersectionIndexes.Count; i++)
                overlapIntersections.Add(sceneData.intersectionObjects[overlapIntersectionIndexes[i]]);
            overlapRoads = new List<RoadObject>();
            for (var i = 0; i < overlapRoadIndexes.Count; i++) overlapRoads.Add(sceneData.roadObjects[overlapRoadIndexes[i]]);
        }

        public static void GetAllOverlapIndexes(Bounds bounds, List<SceneObjectBase> ignoreObjects, RoadConstructor.SceneData sceneData,
            out List<int> overlapIntersectionIndexes, out List<int> overlapRoadIndexes)
        {
            overlapIntersectionIndexes = WorldUtility.FindOverlappingIndexes(bounds, sceneData.intersectionBounds);
            overlapRoadIndexes = WorldUtility.FindOverlappingIndexes(bounds, sceneData.roadBounds);

            RemoveIgnoreObjects(overlapIntersectionIndexes, overlapRoadIndexes);

            return;

            void RemoveIgnoreObjects(List<int> overlapIntersectionIndexes, List<int> overlapRoadIndexes)
            {
                for (var i = 0; i < ignoreObjects.Count; i++)
                {
                    for (var j = overlapRoadIndexes.Count - 1; j >= 0; j--)
                        if (sceneData.roadObjects[overlapRoadIndexes[j]].iD == ignoreObjects[i].iD)
                            overlapRoadIndexes.RemoveAt(j);

                    for (var j = overlapIntersectionIndexes.Count - 1; j >= 0; j--)
                        if (sceneData.intersectionObjects[overlapIntersectionIndexes[j]].iD == ignoreObjects[i].iD)
                            overlapIntersectionIndexes.RemoveAt(j);
                }
            }
        }

        /********************************************************************************************************************************/

        public static List<SceneObjectBase> GetIgnoreObjects(Overlap overlap)
        {
            var ignoreObjects = new List<SceneObjectBase>();
            if (!overlap.exists) return ignoreObjects;
            ignoreObjects.Add(overlap.SceneObjectBase);
            return ignoreObjects;
        }

        public static void CleanRemovedIndexes<T>(List<T> sceneObjects, List<int> overlapIndexes, List<T> removableObjects) where T : SceneObject
        {
            for (var i = overlapIndexes.Count - 1; i >= 0; i--)
            for (var j = 0; j < removableObjects.Count; j++)
            {
                if (sceneObjects[overlapIndexes[i]].name != removableObjects[j].name) continue;
                overlapIndexes.RemoveAt(i);
                break;
            }
        }

        /********************************************************************************************************************************/
    }
}