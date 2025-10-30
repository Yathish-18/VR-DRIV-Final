// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    internal static class AngleDistanceUtility
    {
        /// <summary>
        ///     Calculates the distance to the intersection center.
        ///     Note that for reduced lengths, widthExisting and widthConnecting must be switched!
        /// </summary>
        /// <param name="intersectionDistance">Additional space before intersection starts, used for crossings etc.</param>
        /// <param name="widthExisting">Width of the existing road.</param>
        /// <param name="widthConnecting">Width of the new connecting road.</param>
        /// <param name="closestAngle">Angle from widthExisting to widthConnecting in absolute radians.</param>
        public static float GetAngleDistance(float intersectionDistance, float widthExisting, float widthConnecting, float closestAngle)
        {
            var distance = widthExisting * 0.5f + intersectionDistance;
            if (math.degrees(closestAngle) >= Constants.AngleDistanceTolerance)
            {
                return distance;
            }
            var additionalDistance = (Constants.AngleDistanceTolerance - math.degrees(closestAngle)) * widthConnecting * Constants.AngleDistanceFactor;
            return distance + additionalDistance;
        }
        
        public static float3 ApplyAngleSettings(float3 tangentIn, float3 tangentInLeft, float3 tangentInRight, float minAngle, float snapAngle)
        {
            var originalTangent = tangentIn;
            var fixTangent = (tangentInLeft + tangentInRight) * 0.5f;

            /********************************************************************************************************************************/
            // Snap Angle (Int.)
            if (snapAngle > 0f) tangentIn = PGTrigonometryUtility.SnapTangentXZ(tangentIn, fixTangent, snapAngle);

            /********************************************************************************************************************************/
            // Min Angle (Int.)
            var angleToLeft = PGTrigonometryUtility.AngleClockwiseXZ(tangentIn, tangentInLeft);
            var angleToRight = -PGTrigonometryUtility.AngleClockwiseXZ(tangentInRight, tangentIn);

            if (snapAngle > 0f) // Snap may not work, can snap to align with existing tangent
            {
                const float tolerance = 0.1f; 
                var degreeLeft = math.abs(math.degrees(angleToLeft));
                var degreeRight = math.abs(math.degrees(angleToRight));

                if (math.abs(0f - degreeLeft) < tolerance || math.abs(180f - degreeLeft) < tolerance || math.abs(360f - degreeLeft) < tolerance)
                {
                    tangentIn = originalTangent;
                    angleToLeft = PGTrigonometryUtility.AngleClockwiseXZ(tangentIn, tangentInLeft);
                }
                else if (math.abs(0f - degreeRight) < tolerance || math.abs(180f - degreeRight) < tolerance || math.abs(360f - degreeRight) < tolerance)
                {
                    tangentIn = originalTangent;
                    angleToRight = -PGTrigonometryUtility.AngleClockwiseXZ(tangentInRight, tangentIn);
                }
            }
            
            if (math.abs(angleToLeft) < minAngle)
                tangentIn = PGTrigonometryUtility.RotateTangentXZ(tangentIn, angleToLeft - minAngle);
            
            if (math.abs(angleToRight) < minAngle)
                tangentIn = PGTrigonometryUtility.RotateTangentXZ(tangentIn, -(math.abs(angleToRight) - minAngle));
            
            return tangentIn;
        }

        /********************************************************************************************************************************/

        public static int GetIndexWithLeastDegrees(float3 centerPosition, float3 tangentIn,
            List<KnotData> knotDatas)
        {
            var knots = knotDatas.Select(t => t.nearestKnot).ToList();
            return GetIndexWithLeastDegrees(centerPosition, tangentIn, knots);
        }

        public static int GetIndexWithLeastDegrees(float3 centerPosition, float3 tangentIn, List<BezierKnot> knots)
        {
            var minDegree = float.MaxValue;
            var nearestIndex = 0;
            
            for (var i = 0; i < knots.Count; i++)
            {
                var knotTangentIn = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, knots[i].Position, knots[i].TangentOut);
                var angleRad = math.abs(PGTrigonometryUtility.AngleXZ(knotTangentIn, tangentIn));
                
                if (angleRad < minDegree)
                {
                    minDegree = angleRad;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }
        
        
        /// <summary>
        ///     Gets the indexes of the two knots that are oriented towards each other with the smallest angle between them.
        /// </summary>
        public static void GetFacingIndexes(float3 centerPosition, List<BezierKnot> knots, out int index1, out int index2)
        {
            index1 = 0;
            index2 = 0;
            var minDegree = float.MaxValue;

            for (var i = 0; i < knots.Count; i++)
            {
                var tan = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, knots[i].Position, knots[i].TangentOut);
                for (var j = i + 1; j < knots.Count; j++)
                {
                    var tanOut = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, knots[j].Position, knots[j].TangentOut) * -1f;

                    var degrees = math.abs(PGTrigonometryUtility.AngleXZ(tan, tanOut));

                    if (degrees < minDegree)
                    {
                        minDegree = degrees;
                        index1 = i;
                        index2 = j;
                    }
                }
            }
        }
        
        public static void GetNeighbourIndexes(float3 centerPosition, float3 tangentIn, List<BezierKnot> knots, 
            out int indexLeft, out int indexRight)
        {
            indexLeft = 0;
            indexRight = 0;
            var biggestRad = float.MinValue;
            var smallestRad = float.MaxValue;

            for (var i = 0; i < knots.Count; i++)
            {
                var tan = PGTrigonometryUtility.DirectionalTangentToPointXZ(centerPosition, knots[i].Position, knots[i].TangentOut);
                var rad = PGTrigonometryUtility.AngleClockwiseXZ(tangentIn, tan);
                
                if (rad > biggestRad)
                {
                    biggestRad = rad;
                    indexRight = i;
                }
                if (rad < smallestRad)
                {
                    smallestRad = rad;
                    indexLeft = i;
                }
            }
        }

    }
    
}