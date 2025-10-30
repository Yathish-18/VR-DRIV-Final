// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Linq;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    public static class TangentCalculation
    {
        public static void CalculateTangents(Spline spline, bool smoothSlope, float tangentLength,
            bool setDirectionFixPoint = false, float3 directionFixPoint = default)
        {
            for (var i = 0; i < spline.Knots.Count() - 1; i++)
            {
                var knot1 = spline.Knots.ElementAt(i);
                var knot2 = spline.Knots.ElementAt(i + 1);

                float3 center;
                if(setDirectionFixPoint) center = directionFixPoint;
                else center = (knot1.Position + knot2.Position) * 0.5f;
                CalculateTangents(smoothSlope, tangentLength, knot1.Position, knot1.TangentOut,
                    knot2.Position, knot2.TangentOut, true, center,
                    out var tangent1, out var tangent2);

                if (i == 0) knot1.TangentIn = -tangent1;
                knot1.TangentOut = tangent1;
                knot2.TangentIn = tangent2;
                if (i == spline.Knots.Count() - 2) knot2.TangentOut = -tangent2;

                spline.SetKnot(i, knot1);
                spline.SetKnot(i + 1, knot2);
            }
        }

        public static void CalculateTangentsHeight(Spline spline, bool smoothSlope)
        {
            for (int i = 0; i < spline.Count; i++)
            {
                var knot1 = spline[i];
                var isLastKnot = i == spline.Count - 1;
                var knot2 = spline[isLastKnot ? 0 : i + 1]; 
        
                if (smoothSlope)
                {
                    knot1.TangentIn.y = 0f;
                    knot1.TangentOut.y = 0f;
                }
                else
                {
                    var tangentOutLength = math.length(knot1.TangentOut);
                    var tangentInLength = math.length(knot1.TangentIn);
                    var slope = (knot2.Position.y - knot1.Position.y);
                    if(isLastKnot) slope = -slope;
                    knot1.TangentOut.y = slope * (tangentOutLength / math.distance(knot1.Position, knot2.Position));
                    knot1.TangentIn.y = -slope * (tangentInLength / math.distance(knot1.Position, knot2.Position));
                }
        
                spline.SetKnot(i, knot1);
            }
        }

        public static void CalculateTangents(bool smoothSlope, float tangentLength,
            float3 pos1, float3 tan1, float3 pos2, float3 tan2, bool setDirection, float3 directionFixPoint,
            out float3 tangent01, out float3 tangent02)
        {
            tangentLength = math.clamp(tangentLength, 0.01f, 1f);

            if (setDirection)
            {
                tan1 = PGTrigonometryUtility.DirectionalTangentToPointXZ(directionFixPoint, pos1, tan1);
                tan2 = PGTrigonometryUtility.DirectionalTangentToPointXZ(directionFixPoint, pos2, tan2);
            }
            
            var pos1_2D = new float2(pos1.x, pos1.z);
            var pos2_2D = new float2(pos2.x, pos2.z);
            var tan1_2D = new float2(tan1.x, tan1.z);
            var tan2_2D = new float2(tan2.x, tan2.z);

            var angle = math.abs(math.degrees(PGTrigonometryUtility.Angle(tan1_2D, tan2_2D)));
            var tolerance = 0.1f;
            var facingEachOther = Mathf.Abs(angle) < tolerance || Mathf.Abs(angle - 180f) < tolerance;
            var tightCurve = angle < 90f;
            
            if (facingEachOther)
            {
                var distance = DistanceXZ(pos1, pos2);
                tangent01 = math.normalizesafe(tan1) * distance * tangentLength;
                tangent02 = math.normalizesafe(tan2) * distance * tangentLength;
            }
            else if (tightCurve)
            {
                var intersectionPoint_2D = PGTrigonometryUtility.IntersectionPoint(pos1_2D, tan1_2D, pos2_2D, tan2_2D);
                var intersection01 = new float3(intersectionPoint_2D.x, pos1.y, intersectionPoint_2D.y);
                var intersection02 = new float3(intersectionPoint_2D.x, pos2.y, intersectionPoint_2D.y);
                
                tangent01 = math.normalizesafe(tan1) * DistanceXZ(pos1, intersection01) * tangentLength;
                tangent02 = math.normalizesafe(tan2) * DistanceXZ(pos2, intersection02) * tangentLength;
            }
            else
            {
                var intersection1_2D = PerpendicularIntersectionPoint(pos1_2D, tan1_2D, pos2_2D);
                var intersection01 = new float3(intersection1_2D.x, pos1.y, intersection1_2D.y);
                var intersection2_2D = PerpendicularIntersectionPoint(pos2_2D, tan2_2D, pos1_2D);
                var intersection02 = new float3(intersection2_2D.x, pos2.y, intersection2_2D.y);

                tangent01 = math.normalizesafe(tan1) * DistanceXZ(pos1, intersection01) * tangentLength;
                tangent02 = math.normalizesafe(tan2) * DistanceXZ(pos2, intersection02) * tangentLength;
            }

            if (smoothSlope)
            {
                tangent01.y = 0f;
                tangent02.y = 0f;
            }
            else
            {
                tangent01.y = (pos2.y - pos1.y) / 2f;
                tangent02.y = (pos1.y - pos2.y) / 2f;
            }
        }
        
        

        /********************************************************************************************************************************/
        
        private static float DistanceXZ(float3 position01, float3 position02)
        {
            var pos1_2D = new float2(position01.x, position01.z);
            var pos2_2D = new float2(position02.x, position02.z);
            return math.distance(pos1_2D, pos2_2D);
        }
        
        private static float2 PerpendicularIntersectionPoint(float2 pos1, float2 tan1, float2 pos2)
        {
            var tan2 = PGTrigonometryUtility.PerpendicularTangentToPoint(pos2, pos1, tan1);
            var intersection = PGTrigonometryUtility.IntersectionPoint(pos1, tan1, pos2, tan2);
            return intersection;
        }
    }
}