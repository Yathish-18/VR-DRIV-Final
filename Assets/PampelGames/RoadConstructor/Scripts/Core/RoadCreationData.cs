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
    internal static class RoadCreationData
    {
        public static ConstructionData GenerateRoadData(RoadSettings roadSettings,
            RoadConstructor.SceneData sceneData, RoadDescr roadDescr,
            float3 initialPosition01, float3 initialPosition02,
            bool construct,
            out RoadObjectClass roadObjectClass, out Overlap overlap01, out Overlap overlap02)
        {
            var settings = roadDescr.settings;
            var integrations = roadDescr.roadConstructor.integrations;

            var position01 = initialPosition01;
            var position02 = initialPosition02;
            
            if(roadSettings.overlap01.exists) overlap01 = roadSettings.overlap01;
            else overlap01 = OverlapUtility.GetOverlap(settings, roadDescr.width, settings.snapHeight * 2f, position01, sceneData, integrations); 
            if(roadSettings.overlap02.exists) overlap02 = roadSettings.overlap02;
            else overlap02 = OverlapUtility.GetOverlap(settings, roadDescr.width, settings.snapHeight * 2f, position02, sceneData, integrations); 
            
            if (overlap01.exists) position01 = overlap01.position;
            if (overlap02.exists) position02 = overlap02.position;

            var tangent01 = math.normalizesafe(position02 - position01);
            var tangent02 = -tangent01;

            /********************************************************************************************************************************/
            // User settings
            if (roadSettings.setTangent01)
            {
                tangent01 = math.normalizesafe(roadSettings.tangent01);
                tangent01 = PGTrigonometryUtility.DirectionalTangentToPointXZ(position02, position01, tangent01);
            }

            if (roadSettings.setTangent02)
            {
                tangent02 = math.normalizesafe(roadSettings.tangent02);
                tangent02 = PGTrigonometryUtility.DirectionalTangentToPointXZ(position01, position02, tangent02);
            }

            /********************************************************************************************************************************/

            var directConnection = overlap01.exists && overlap02.exists && settings.directConnection == DirectConnection.Align;
            AlignOverlap(overlap01, ref position01, ref tangent01, ref position02, ref tangent02);
            AlignOverlap(overlap02, ref position02, ref tangent02, ref position01, ref tangent01);
            
            void AlignOverlap(Overlap _overlap, ref float3 _position01, ref float3 _tangent01, ref float3 _position02, ref float3 _tangent02)
            {
                if (!_overlap.exists) return;

                if (_overlap.overlapType == OverlapType.Shared && _overlap.sharedObject != null)
                {
                    var connectionPoints = _overlap.sharedObject.GetConnectionPoints();
                    if (ConnectionPointUtility.TryGetFreeConnection(connectionPoints, _position01, out var connectionPoint))
                    {
                        _position01 = connectionPoint.position;
                        _tangent01 = connectionPoint.tangent;    
                    }
                }
                else
                {
                    _overlap.SceneObjectBase.AlignTrack(roadDescr.width, ref _position01, ref _tangent01, ref _position02, ref _tangent02,
                        directConnection);    
                }
            }

            /********************************************************************************************************************************/
            // Distance Angle Curve

            if (!overlap02.exists && !roadSettings.setTangent02)
            {
                var distanceAngleCurve = roadDescr.settings.distanceRatioAngleCurve;

                var ratio = GetStraightToPerpRatio(position01, tangent01, position02, tangent02);
                if (ratio > 0)
                {
                    var angleDeg = distanceAngleCurve.Evaluate(ratio);
                    tangent02 = tangent01;

                    var tangent02Temp = position02 - position01;
                    var angleTemp = PGTrigonometryUtility.AngleXZ(tangent01, tangent02Temp);
                    var rotationRad = angleTemp > 0f ? math.radians(angleDeg) : -math.radians(angleDeg);
                    tangent02 = PGTrigonometryUtility.RotateTangentXZ(tangent02, rotationRad);
                }
            }

            /********************************************************************************************************************************/
            // Parallel Road

            var isParallelRoad = false;
            if (roadSettings.parallelRoad && overlap01.exists && overlap01.overlapType == OverlapType.Road
                && overlap02.exists && overlap02.overlapType == OverlapType.Road &&
                overlap01.roadObject.iD == overlap02.roadObject.iD)
            {
                var parallelRoad = overlap01.roadObject;

                var parallelSpline = new Spline(parallelRoad.splineContainer.Spline);
                var parallelSplineLength = parallelSpline.GetLength();
                var reducedLength = overlap01.t < overlap02.t ? overlap01.t * parallelSplineLength : (1f - overlap01.t) * parallelSplineLength;
                ConstructionSplineUtility.ReduceSpline(parallelSpline, overlap01.t < overlap02.t, parallelSplineLength, reducedLength);

                SplineUtility.GetNearestPoint(parallelSpline, overlap02.position, out var nearestPoint02, out var nearestT02);

                parallelSplineLength = parallelSpline.GetLength();
                reducedLength = overlap01.t < overlap02.t ? (1f - nearestT02) * parallelSplineLength : nearestT02 * parallelSplineLength;

                ConstructionSplineUtility.ReduceSpline(parallelSpline, overlap01.t >= overlap02.t, parallelSplineLength, reducedLength);

                position01 = parallelSpline[0].Position;
                tangent01 = math.normalizesafe(parallelSpline[0].TangentOut);
                position02 = parallelSpline[^1].Position;
                tangent02 = math.normalizesafe(parallelSpline[^1].TangentIn);

                var tangentPerp01 = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangent01);
                var tangentPerp02 = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangent02);
                if (overlap02.t < overlap01.t)
                {
                    tangentPerp01 *= -1f;
                    tangentPerp02 *= -1f;
                }

                var offsetWidth = roadDescr.width * 0.5f + parallelRoad.roadDescr.width * 0.5f + roadSettings.parallelDistance;
                position01 += tangentPerp01 * offsetWidth;
                position02 -= tangentPerp02 * offsetWidth;

                isParallelRoad = true;
            }

            /********************************************************************************************************************************/
            // Road Data

            var center = (position01 + position02) * 0.5f;
            TangentCalculation.CalculateTangents(settings.smoothSlope, settings.tangentLength, position01, tangent01, position02, tangent02,
                true, center,
                out tangent01, out tangent02);

            var roadSpline = CreateRoadSpline(position01, tangent01, position02, tangent02);
            var angle01 = GetMinAngle(overlap01, tangent01);
            var angle02 = GetMinAngle(overlap02, tangent02);
            var roadAngle = math.abs(math.degrees(PGTrigonometryUtility.AngleXZ(tangent01, -tangent02)));
            var curvature = ConstructionSplineUtility.GetCurvature(position01, tangent01, position02, -tangent02);
            var slope = math.degrees(PGTrigonometryUtility.Slope(position01, position02));
            var height01 = GetHeight(settings, position01);
            var height02 = GetHeight(settings, position02);
            var raycastOffset = Constants.RaycastOffset(settings);
            var elevated = WorldUtility.CheckElevation(roadSpline, roadDescr.road.length, raycastOffset, settings.groundLayers,
                settings.elevationStartHeight);

            /********************************************************************************************************************************/

            var knotFirst = roadSpline.Knots.First();
            if (math.distancesq(knotFirst.Position, position01) > math.distancesq(knotFirst.Position, position02))
                ConstructionSplineUtility.InvertSpline(roadSpline);

            /********************************************************************************************************************************/

            var roadData = new ConstructionData(position01, tangent01, angle01, height01,
                position02, tangent02, angle02, height02,
                roadSpline.GetLength(), roadAngle, curvature, slope, elevated, isParallelRoad);

            /********************************************************************************************************************************/
            // Additionally RoadObjectClass

            roadObjectClass = new RoadObjectClass(roadDescr, roadSpline);

            return roadData;
        }


        /********************************************************************************************************************************/

        private static Spline CreateRoadSpline(float3 position01, float3 tangent01, float3 position02, float3 tangent02)
        {
            var knot01 = new BezierKnot
                {Position = position01, Rotation = quaternion.identity, TangentIn = -tangent01, TangentOut = tangent01};
            var knot02 = new BezierKnot
                {Position = position02, Rotation = quaternion.identity, TangentIn = tangent02, TangentOut = -tangent02};

            var spline = new Spline
            {
                {knot01, TangentMode.Broken},
                {knot02, TangentMode.Broken}
            };
            return spline;
        }

        private static float GetMinAngle(Overlap overlap, float3 tangent)
        {
            var angle = 0f;
            if (!overlap.exists) return angle;

            angle = float.MaxValue;

            if (overlap.overlapType == OverlapType.Intersection || overlap.IsSnappedRoad())
            {
                var roadConnections = overlap.overlapType == OverlapType.Intersection
                    ? overlap.intersectionObject.RoadConnections
                    : overlap.roadObject.RoadConnections;

                for (var i = 0; i < roadConnections.Count; i++)
                {
                    var nearestKnot = ConstructionSplineUtility.GetNearestKnot(roadConnections[i].splineContainer.Spline, overlap.position);
                    var tangentOut =
                        -PGTrigonometryUtility.DirectionalTangentToPointXZ(overlap.position, nearestKnot.Position, nearestKnot.TangentOut);
                    var angleCheck = math.abs(math.degrees(PGTrigonometryUtility.AngleXZ(tangent, tangentOut)));
                    if (angleCheck < angle) angle = angleCheck;
                }
            }
            else if (overlap.overlapType == OverlapType.Road)
            {
                angle = PGTrigonometryUtility.AngleXZ(tangent, overlap.tangent);
                angle = math.abs(math.degrees(angle));
            }

            return angle;
        }

        private static float GetHeight(ComponentSettings settings, float3 position)
        {
            var raycastOffset = (float3) Constants.RaycastOffset(settings);
            var ray = new Ray(position + raycastOffset, Vector3.down);
            var hit = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, settings.groundLayers);
            if (!hit) return 0f;
            return position.y - hitInfo.point.y;
        }

        private static float GetStraightToPerpRatio(float3 position01, float3 tangent01, float3 position02, float3 tangent02)
        {
            var tangentPerp = math.normalizesafe(PGTrigonometryUtility.PerpendicularTangentToPointXZ(position02, position01, tangent01));
            var intersectionPointPerp = PGTrigonometryUtility.IntersectionPointXZ(position01, tangentPerp, position02, -tangent01);
            var distancePerp = math.distance(position01, intersectionPointPerp);
            var intersectionPointStraight = PGTrigonometryUtility.IntersectionPointXZ(position01, tangent01, position02, tangentPerp);
            var distanceStraight = math.distance(position01, intersectionPointStraight);

            if (distanceStraight > 0.001f && distancePerp > 0.001f)
            {
                var ratio = distanceStraight / distancePerp;
                return ratio;
            }

            return -1f;
        }
    }
}