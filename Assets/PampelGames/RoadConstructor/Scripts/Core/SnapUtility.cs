// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    internal static class SnapUtility
    {
        public static void AlignRoadToRoad(RoadObject road, float width, ref float3 position01, ref float3 tangent01, ref float3 position02,
            ref float3 tangent02, bool directConnection)
        {
            if (road.snapPositionSet)
            {
                AlignRoadToIntersection(road, road.snapPosition, width, ref position01, ref tangent01, ref position02, ref tangent02,
                    directConnection);
                return;
            }

            var roadDescr = road.roadDescr;
            var spline = road.splineContainer.Spline;
            SplineUtility.GetNearestPoint(spline, position01, out var nearestPosition, out var nearestT);

            position01 = road.snapPositionSet ? road.snapPosition : nearestPosition;
            var tangentMiddle = spline.EvaluateTangent(nearestT);
            var widthT = roadDescr.width / road.length;
            spline.Evaluate(nearestT + widthT, out var positionLeft, out var tangentLeft, out var upVectorLeft);
            spline.Evaluate(nearestT - widthT, out var positionRight, out var tangentRight, out var upVectorRight);

            var tangentIn = -PGTrigonometryUtility.DirectionalTangentToPointXZ(position02, position01, tangent01);
            tangentLeft = PGTrigonometryUtility.DirectionalTangentToPointXZ(position01, positionLeft, tangentLeft);
            tangentRight = PGTrigonometryUtility.DirectionalTangentToPointXZ(position01, positionRight, tangentRight);

            tangentIn.y = 0f;
            tangentLeft.y = 0f;
            tangentRight.y = 0f;
            tangentMiddle.y = 0f;
            tangent01.y = 0f;

            tangentMiddle = math.normalizesafe(tangentMiddle);

            var minAngle = math.radians(roadDescr.settings.minAngleIntersection);
            var snapAngle = math.radians(roadDescr.settings.snapAngleIntersection);
            if (directConnection) snapAngle = math.radians(90f);
            var currentAngle = PGTrigonometryUtility.AngleXZ(tangent01, tangentMiddle);
            if (currentAngle < 0) (tangentLeft, tangentRight) = (tangentRight, tangentLeft);

            tangent01 = AngleDistanceUtility.ApplyAngleSettings(tangentIn, tangentLeft, tangentRight, minAngle, snapAngle);

            var angleLeft = math.abs(PGTrigonometryUtility.AngleXZ(-tangentMiddle, tangent01));
            var angleRight = math.abs(PGTrigonometryUtility.AngleXZ(tangentMiddle, tangent01));
            var closestAngle = math.min(angleLeft, angleRight);

            var distance = AngleDistanceUtility.GetAngleDistance(roadDescr.settings.intersectionDistance, road.Width, width, closestAngle);

            tangent01 *= -1f;
            position01 += tangent01 * distance;
        }

        public static void AlignRoadToIntersection(SceneObject intersection, float3 centerPosition, float width,
            ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02,
            bool directConnection)
        {
            var settings = intersection.roadDescr.settings;
            tangent01 = math.normalizesafe(new float3(tangent01.x, 0f, tangent01.z));

            var RoadConnections = intersection.RoadConnections;

            if (RoadConnections.Count == 0) return;

            position01 = centerPosition;

            var boundsCenter = intersection.meshRenderer.bounds.center;

            var nearestKnots = new List<BezierKnot>();
            for (var i = 0; i < RoadConnections.Count; i++)
            {
                var connectionSpline = RoadConnections[i].splineContainer.Spline;
                var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(connectionSpline, centerPosition);
                nearestKnots.Add(connectionSpline[nearestKnotIndex]);
            }

            var tangentIn = -tangent01;

            AngleDistanceUtility.GetNeighbourIndexes(boundsCenter, tangentIn, nearestKnots, out var indexLeft, out var indexRight);
            var tangentLeft =
                PGTrigonometryUtility.DirectionalTangentToPointXZ(boundsCenter, nearestKnots[indexLeft].Position, nearestKnots[indexLeft].TangentOut);
            var tangentRight =
                PGTrigonometryUtility.DirectionalTangentToPointXZ(boundsCenter, nearestKnots[indexRight].Position,
                    nearestKnots[indexRight].TangentOut);

            var minAngle = math.radians(settings.minAngleIntersection);
            var snapAngle = math.radians(settings.snapAngleIntersection);
            if (directConnection) snapAngle = math.radians(90f);

            tangent01 = AngleDistanceUtility.ApplyAngleSettings(tangentIn, tangentLeft, tangentRight, minAngle, snapAngle);

            var angleLeft = math.abs(PGTrigonometryUtility.AngleXZ(tangent01, tangentLeft));
            var angleRight = math.abs(PGTrigonometryUtility.AngleXZ(tangent01, tangentRight));
            var distance = AngleDistanceUtility.GetAngleDistance(settings.intersectionDistance, intersection.roadDescr.width, width,
                math.min(angleLeft, angleRight));

            tangent01 *= -1f;

            position01 += tangent01 * distance;
        }

        public static void AlignRoadToRoundabout(RoundaboutObject roundabout, ref float3 position01, ref float3 tangent01, ref float3 position02,
            ref float3 tangent02, bool directConnection)
        {
            var roadDescr = roundabout.roadDescr;
            var spline = roundabout.splineContainer.Spline;
            SplineUtility.GetNearestPoint(spline, position01, out var nearestPosition, out var nearestT);
            var nearestTangent = spline.EvaluateTangent(nearestT);
            nearestTangent.y = 0f;

            var overlapTangentPerp = math.normalizesafe(PGTrigonometryUtility.RotateTangent90ClockwiseXZ(nearestTangent));
            tangent01 = PGTrigonometryUtility.DirectionalTangentToPointXZ(roundabout.centerPosition, position01, overlapTangentPerp) * -1f;

            var distance = roadDescr.width * 0.5f + roadDescr.settings.intersectionDistance;
            position01 += tangent01 * distance;
        }

        public static void SnapRoadToRamp(RampObject ramp, ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02,
            bool directConnection)
        {
            // var splineRamp = ramp.SplineRamp;
            // var knot = ramp.rampIn ? splineRamp[0] : splineRamp[^1];
            // position01 = knot.Position;
            // tangent01 = ramp.rampIn ? knot.TangentOut : knot.TangentIn;
        }
    }
}