// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public static class IntersectionValidation
    {
        public static List<ConstructionFail> ValidateIntersectionAddRoad(IntersectionObject intersection)
        {
            var constructionFails = new List<ConstructionFail>();

            var settings = intersection.roadDescr.settings;

            /********************************************************************************************************************************/
            // Height Range
            constructionFails.AddRange(ValidateGroundIntersection(settings, intersection));


            return constructionFails;
        }

        private static List<ConstructionFail> ValidateGroundIntersection(ComponentSettings settings, IntersectionObject intersection)
        {
            var constructionFails = new List<ConstructionFail>();
            if (settings.elevatedIntersections) return constructionFails;
            if (intersection.RoadConnections.Count == 1) return constructionFails;
            if (settings.groundLayers.value == 0) return constructionFails;

            var raycastOffset = Constants.RaycastOffset(settings);
            var halfExtents = intersection.meshRenderer.bounds.extents;

            var hit = Physics.BoxCast(intersection.centerPosition + raycastOffset, halfExtents, Vector3.down, out var hitInfo, Quaternion.identity,
                Mathf.Infinity, settings.groundLayers);
            if (!hit)
            {
                constructionFails.Add(new ConstructionFail(FailCause.GroundMissing));
            }
            else
            {
                var heightDif = intersection.centerPosition.y - hitInfo.point.y;

                if (heightDif > 0 && heightDif > math.abs(settings.elevationStartHeight))
                    constructionFails.Add(new ConstructionFail(FailCause.ElevatedIntersection));

                if (heightDif < 0 && math.abs(heightDif) > math.abs(settings.heightRange.x))
                    constructionFails.Add(new ConstructionFail(FailCause.ElevatedIntersection));
            }

            return constructionFails;
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public static List<ConstructionFail> ValidateNewRoundabout(List<ConstructionBase> integrations, ComponentSettings settings,
            RoadConstructor.SceneData sceneData, RoadDescr roadDescr, float3 position, float radius)
        {
            var constructionFails = new List<ConstructionFail>();

            if (!roadDescr.road.oneWay) constructionFails.Add(new ConstructionFail(FailCause.OneWayRequired));

            var center = new Vector3(position.x, position.y, position.z);
            var boundsSize = (radius + roadDescr.width * 0.5f) * 2f;
            var size = new Vector3(boundsSize, roadDescr.width, boundsSize);
            var bounds = new Bounds(center, size);

            var overlap = OverlapUtility.GetOverlap(settings, boundsSize + settings.snapDistance, bounds, sceneData, integrations);

            if (overlap.exists)
            {
                if (overlap.overlapType == OverlapType.Intersection)
                    constructionFails.Add(new ConstructionFail(FailCause.OverlapIntersection));
                else
                    constructionFails.Add(new ConstructionFail(FailCause.OverlapTrack));
            }

            return constructionFails;
        }

        public static List<ConstructionFail> ValidateRoundaboutAddRoad(RoundaboutObject roundabout)
        {
            var constructionFails = new List<ConstructionFail>();

            /********************************************************************************************************************************/
            // Height Range
            var bounds = roundabout.meshRenderer.bounds;
            constructionFails.AddRange(ValidateGroundRoundabout(roundabout.roadDescr.settings, bounds));
            constructionFails.AddRange(ValidateGroundRoundabout(roundabout.roadDescr.settings, bounds));

            return constructionFails;
        }

        private static List<ConstructionFail> ValidateGroundRoundabout(ComponentSettings settings, Bounds bounds)
        {
            var constructionFails = new List<ConstructionFail>();
            if (settings.elevatedIntersections) return constructionFails;
            if (settings.groundLayers.value == 0) return constructionFails;

            var raycastOffset = Constants.RaycastOffset(settings);

            var hit = Physics.BoxCast(bounds.center + raycastOffset, bounds.extents, Vector3.down, out var hitInfo, Quaternion.identity,
                Mathf.Infinity, settings.groundLayers);
            if (!hit)
            {
                constructionFails.Add(new ConstructionFail(FailCause.GroundMissing));
            }
            else
            {
                var heightDif = bounds.center.y - hitInfo.point.y;

                if (heightDif > 0 && heightDif > math.abs(settings.elevationStartHeight))
                    constructionFails.Add(new ConstructionFail(FailCause.ElevatedIntersection));

                if (heightDif < 0 && math.abs(heightDif) > math.abs(settings.heightRange.x))
                    constructionFails.Add(new ConstructionFail(FailCause.ElevatedIntersection));
            }

            return constructionFails;
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public static List<ConstructionFail> ValidateNewRamp(RoadDescr roadDescr, Overlap overlap)
        {
            var constructionFails = new List<ConstructionFail>();

            if (!overlap.exists || overlap.overlapType != OverlapType.Road) constructionFails.Add(new ConstructionFail(FailCause.MissingConnection));

            return constructionFails;
        }

        public static List<ConstructionFail> ValidateRampAddRoad(RampObject ramp, RoadObject newRoad)
        {
            var constructionFails = new List<ConstructionFail>();


            return constructionFails;
        }
    }
}