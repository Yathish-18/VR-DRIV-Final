// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    internal static class RoadValidation
    {
        public static List<ConstructionFail> ValidateRoad(ComponentSettings settings, ConstructionData roadData, RoadDescr roadDescr, Spline trackSpline,
            RoadConstructor.SceneData sceneData, List<Overlap> overlaps)
        {
            var constructionFails = new List<ConstructionFail>();
            var road = roadDescr.road;
            
            /********************************************************************************************************************************/
            // Overlap
            
            if (overlaps.Count > 1 && overlaps[0].exists && overlaps[1].exists)
                if (overlaps[0].SceneObjectBase.iD == overlaps[1].SceneObjectBase.iD)
                    if (!roadData.parallelRoad)
                        constructionFails.Add(new ConstructionFail(FailCause.OverlapTrack));
            
            var ignoreObjects = new List<SceneObjectBase>();
            for (int i = 0; i < overlaps.Count; i++) ignoreObjects.AddRange(OverlapUtility.GetIgnoreObjects(overlaps[i]));    
            
            constructionFails.AddRange(DetectTrackOverlap(settings, sceneData, trackSpline, roadDescr.width, road.length, ignoreObjects,
                out var splinePositions, out var splineTangents));
            
            /********************************************************************************************************************************/
            // Overlap - Integrations

            if(settings.integrationActive && settings.integrationDetectOverlap)
            {
                var integrations = roadDescr.roadConstructor.integrations;
                for (int i = 0; i < integrations.Count; i++)
                {
                    var integration = integrations[i];
                    constructionFails.AddRange(integration.DetectTrackOverlap(trackSpline, roadDescr.width, road.length, ignoreObjects));
                }
            }
            
            /********************************************************************************************************************************/
            // Elevation per road
            if (!road.elevatable && roadData.elevated) constructionFails.Add(new ConstructionFail(FailCause.NotElevatable));

            /********************************************************************************************************************************/
            // Ground
            constructionFails.AddRange(ValidateGround(settings, roadDescr, splinePositions, splineTangents));

            /********************************************************************************************************************************/
            // Road Length
            var length = roadData.length;
            if (length < settings.roadLength.x) constructionFails.Add(new ConstructionFail(FailCause.TrackLength));
            else if (length > settings.roadLength.y) constructionFails.Add(new ConstructionFail(FailCause.TrackLength));

            /********************************************************************************************************************************/
            // Curvature
            if (roadData.curvature > settings.maxCurvature) constructionFails.Add(new ConstructionFail(FailCause.Curvature));

            /********************************************************************************************************************************/
            // Slope
            var slope = math.degrees(PGTrigonometryUtility.Slope(roadData.position01, roadData.position02));
            if (math.abs(slope) > settings.maxSlope) constructionFails.Add(new ConstructionFail(FailCause.Slope));

            /********************************************************************************************************************************/
            // IntersectionRoadLength (Distance to intersection)
            for (var i = 0; i < overlaps.Count; i++)
            {
                var overlap = overlaps[i];
                if (!overlap.exists) continue;
                if (overlap.overlapType != OverlapType.Road) continue;
                if (overlap.IsSnappedRoad()) continue;
                var spaceT = overlap.t < 0.5f ? overlap.t : 1f - overlap.t;
                var space = spaceT * overlap.roadObject.length;

                for (var j = 0; j < trackSpline.Count; j++)
                {
                    var knot = trackSpline[j];

                    var tangentIn = j == 0 ? -knot.TangentOut : -knot.TangentIn;
                    var overlapTangent = overlap.t < 0.5f ? overlap.tangent : -overlap.tangent;

                    var angle = math.abs(PGTrigonometryUtility.AngleXZ(tangentIn, overlapTangent));
                    var requiredDistance =
                        AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, overlap.roadObject.roadDescr.width, roadDescr.width,
                            angle) + settings.roadLength.x;

                    if (space < requiredDistance) constructionFails.Add(new ConstructionFail(FailCause.IntersectionTrackLength));
                }
            }

            return constructionFails;
        }
        
        public static List<ConstructionFail> DetectTrackOverlap(ComponentSettings settings, RoadConstructor.SceneData sceneData, 
            Spline trackSpline, float trackWidth, float trackSpacing, List<SceneObjectBase> ignoreObjects,
            out float3[] splinePositions, out float3[] splineTangents)
        {
            var roadObjects = sceneData.roadObjects;
            var intersectionBounds = sceneData.intersectionBounds;
            
            var constructionFails = new List<ConstructionFail>();
            
            var roadBounds = WorldUtility.ExtendBounds(trackSpline.GetBounds(),
                trackWidth + settings.minOverlapDistance);
            
            OverlapUtility.GetAllOverlapIndexes(roadBounds, ignoreObjects, sceneData,
                out var overlapIntersectionIndexes, out var overlapRoadIndexes);
            
            for (int i = overlapIntersectionIndexes.Count - 1; i >= 0; i--)
            {
                var _intersection = sceneData.intersectionObjects[overlapIntersectionIndexes[i]];
                if (_intersection is ISharedObject) overlapIntersectionIndexes.RemoveAt(i);
            }
            
            var overlapIntersectionBounds = new Bounds[overlapIntersectionIndexes.Count];
            for (var i = 0; i < overlapIntersectionIndexes.Count; i++)
            {
                var index = overlapIntersectionIndexes[i];
                var _bounds = intersectionBounds[index];
                _bounds.size = new Vector3(_bounds.size.x + settings.minOverlapDistance * 2f,
                    _bounds.size.y + settings.minOverlapHeight * 2f, _bounds.size.z + settings.minOverlapDistance * 2f);
                overlapIntersectionBounds[i] = _bounds;
            }
            
            var overlapTrackSplines = new List<Spline>();
            var overlapTrackWidths = new List<float>();
            
            for (var i = 0; i < overlapRoadIndexes.Count; i++)
            {
                var existingRoadObject = roadObjects[overlapRoadIndexes[i]];
                overlapTrackSplines.Add(existingRoadObject.splineContainer.Spline);
                overlapTrackWidths.Add(existingRoadObject.roadDescr.width);
            }

            constructionFails.AddRange(ConstructionOverlapUtility.DetectTrackOverlap(trackSpline, trackWidth, trackSpacing,
                settings.minOverlapHeight, settings.minOverlapDistance, overlapIntersectionBounds, overlapTrackSplines, overlapTrackWidths,
                out  splinePositions, out splineTangents));

            return constructionFails;
        }


        /********************************************************************************************************************************/

        private static List<ConstructionFail> ValidateGround(ComponentSettings settings, RoadDescr roadDescr, float3[] splinePositions,
            float3[] splineTangents)
        {
            var constructionFails = new List<ConstructionFail>();
            if (settings.groundLayers.value == 0) return constructionFails;

            var queryParameters = new QueryParameters
            {
                layerMask = settings.groundLayers
            };

            var raycastOffset = Constants.RaycastOffset(settings);

            var commands = new NativeArray<BoxcastCommand>(splinePositions.Length / 2, Allocator.TempJob);
            var results = new NativeArray<RaycastHit>(splinePositions.Length / 2, Allocator.TempJob);
            var index = 0;
            var extends = Vector3.one * (roadDescr.width * 0.5f);
            for (var i = 0; i < splinePositions.Length; i += 2)
            {
                var center = (Vector3) (splinePositions[i] + splinePositions[i + 1]) * 0.5f + raycastOffset;
                var orientation = quaternion.LookRotationSafe(splineTangents[index], Vector3.up);
                commands[index] = new BoxcastCommand(center, extends, orientation, Vector3.down, queryParameters);

                index++;
            }

            var raycastJobHandle = BoxcastCommand.ScheduleBatch(commands, results, 1);
            raycastJobHandle.Complete();

            var resultsArray = results.ToArray();

            commands.Dispose();
            results.Dispose();

            for (var i = 0; i < resultsArray.Length; i++)
            {
                var point = resultsArray[i].point;

                // Null-check for collider takes 4x the time compared to those two checks.
                if (resultsArray[i].distance == 0 && point == Vector3.zero)
                {
                    constructionFails.Add(new ConstructionFail(FailCause.GroundMissing));
                    break;
                }

                var splinePosition = (splinePositions[i * 2] + splinePositions[i * 2 + 1]) * 0.5f;
                var heightDif = splinePosition.y - point.y;

                if (heightDif > 0 && heightDif > math.abs(settings.heightRange.y))
                {
                    constructionFails.Add(new ConstructionFail(FailCause.HeightRange));
                    break;
                }

                if (heightDif < 0 && math.abs(heightDif) > math.abs(settings.heightRange.x))
                {
                    constructionFails.Add(new ConstructionFail(FailCause.HeightRange));
                    break;
                }
            }

            return constructionFails;
        }
    }
}