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

namespace PampelGames.Shared.Construction
{
    public static class ConstructionSplineUtility
    {
        private const float StraightAngleTolerance = 1f; // Degrees
        private const float TangentLengthIntersection = 0.5f;


        /********************************************************************************************************************************/
        // Offset X
        /********************************************************************************************************************************/
        public static void OffsetSplineParallel(Spline spline, float offsetX, float widthStart = 1f, float widthEnd = 1f)
        {
            if (Mathf.Approximately(0f, offsetX)) return;

            // Handling widths differences manually
            if (!Mathf.Approximately(1f, widthStart) || !Mathf.Approximately(1f, widthEnd))
                for (var i = 0; i < spline.Count; i++)
                {
                    var width = math.lerp(widthStart, widthEnd, (float) i / (spline.Count - 1));
                    var totalOffset = offsetX * width;
                    var initialOffset = totalOffset - offsetX;
                    OffsetKnotX(spline, i, initialOffset);
                }

            /********************************************************************************************************************************/
            // Inserting knots for complicated curves to get better results
            var insertKnotTs = new List<float>();
            for (var i = 0; i < spline.Count - 1; i++)
            {
                var knot01 = spline[i];
                var knot02 = spline[i + 1];

                if (IsSplineStraight(knot01, knot02, StraightAngleTolerance)) continue; // Don't insert knot for simple straight lines

                var splineTemp = new Spline(new List<BezierKnot> {knot01, knot02}); // GetCurve() may give wrong tangents
                var middlePos = splineTemp.EvaluatePosition(0.5f);
                SplineUtility.GetNearestPoint(spline, middlePos, out var nearest, out var nearestT);
                insertKnotTs.Add(nearestT);
            }

            for (var i = 0; i < insertKnotTs.Count; i++) InsertKnotSeamless(spline, insertKnotTs[i]);
            if (spline.Closed && insertKnotTs.Count > 1)
            {
                var closedT = insertKnotTs[^1] + insertKnotTs[^1] - insertKnotTs[^2];
                InsertKnotSeamless(spline, closedT);
            }

            /********************************************************************************************************************************/

            var curves = new List<BezierCurve>();
            for (var i = 0; i < spline.Count - 1; i++) curves.Add(spline.GetCurve(i));

            BezierCurve closedCurve = default;
            var closedKnot01 = spline[^1];
            var closedKnot02 = spline[0];
            if (spline.Closed) closedCurve = new BezierCurve(closedKnot01, closedKnot02);

            for (var i = 0; i < curves.Count; i++)
            {
                var curve = curves[i];
                var knot01 = spline[i]; // Tangents of GetCurve() can be wrong
                var knot02 = spline[i + 1];
                curve.P0 = knot01.Position;
                curve.P1 = knot01.Position + knot01.TangentOut;
                curve.P2 = knot02.Position + knot02.TangentIn;
                curve.P3 = knot02.Position;
                curve = OffsetCurveParallel(curve, offsetX);
                curves[i] = curve;
            }

            for (var i = 0; i < curves.Count; i++)
            {
                var curve = curves[i];
                var knot01 = spline[i];
                var knot02 = spline[i + 1];

                knot01.Position = curve.P0;
                knot01.TangentOut = curve.Tangent0;
                knot02.Position = curve.P3;
                knot02.TangentIn = curve.Tangent1;

                spline.SetKnot(i, knot01);
                spline.SetKnot(i + 1, knot02);

                if (spline.Closed && i == curves.Count - 1)
                {
                    knot01 = spline[^1];
                    knot02 = spline[0];
                    closedCurve = OffsetCurveParallel(closedCurve, offsetX);
                    knot01.Position = closedCurve.P0;
                    knot01.TangentOut = closedCurve.Tangent0;
                    knot02.Position = closedCurve.P3;
                    knot02.TangentIn = closedCurve.Tangent1;
                    spline.SetKnot(spline.Count - 1, knot01);
                    spline.SetKnot(0, knot02);
                }
            }
        }


        /// <summary>
        ///     Simple heuristic approximation method by Tiller and Hanson.
        ///     Graph: https://feirell.github.io/offset-bezier/
        /// </summary>
        private static BezierCurve OffsetCurveParallel(BezierCurve curve, float offsetX)
        {
            var tangent01 = curve.Tangent0;
            var tangent02 = curve.Tangent1;

            var tangent01Flat = math.normalizesafe(new float3(tangent01.x, 0f, tangent01.z));
            var tangent02Flat = math.normalizesafe(new float3(tangent02.x, 0f, tangent02.z));
            var tangent01Perp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangent01Flat);
            var tangent02Perp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(-tangent02Flat);

            var p0 = curve.P0;
            var p1 = curve.P1;
            var p2 = curve.P2;
            var p3 = curve.P3;

            var AngleTolerance = 1f; // Straight lines handled separately
            var angle = math.abs(math.degrees(PGTrigonometryUtility.AngleXZ(tangent01, -tangent02)));
            if (angle <= AngleTolerance)
            {
                curve.P0 = p0 + tangent01Perp * offsetX;
                curve.P1 = p1 + tangent01Perp * offsetX;
                curve.P2 = p2 + tangent02Perp * offsetX;
                curve.P3 = p3 + tangent02Perp * offsetX;
                return curve;
            }

            var tangentConnection = p2 - p1;
            var tangentConnectionPerp = math.normalizesafe(PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangentConnection));

            var p1off = p1 + tangentConnectionPerp * offsetX;
            var p2off = p2 + tangentConnectionPerp * offsetX;

            var intersectionA = p1off;
            var intersectionB = p2off;

            var position01 = p0 + tangent01Perp * offsetX;
            var position02 = p3 + tangent02Perp * offsetX;
            var newTangent01 = intersectionA - position01;
            var newTangent02 = intersectionB - position02;

            curve.P0 = position01;
            curve.P3 = position02;

            // Tangent directions can change with Tiller/Hanson.
            // Using the original direction and a custom approximation to reduce the length.
            var angleDeviation01 = math.degrees(PGTrigonometryUtility.AngleXZ(tangent01, newTangent01));
            var angleDeviation02 = math.degrees(PGTrigonometryUtility.AngleXZ(tangent02, newTangent02));
            var angleReduce01 = math.min(1f, math.abs(angleDeviation01) / 45f);
            var angleReduce02 = math.min(1f, math.abs(angleDeviation02) / 45f);
            var newTangentLength01 = math.length(newTangent01) * (1f - angleReduce01);
            var newTangentLength02 = math.length(newTangent02) * (1f - angleReduce02);

            const float MinTangentLength = 0.5f;
            var intersectionPoint = PGTrigonometryUtility.IntersectionPointXZ(position01, tangent01, position02, tangent02);
            var minLength01 = math.distance(position01, intersectionPoint) * MinTangentLength;
            var minLength02 = math.distance(position02, intersectionPoint) * MinTangentLength;
            newTangentLength01 = math.max(newTangentLength01, minLength01);
            newTangentLength02 = math.max(newTangentLength02, minLength02);
            curve.Tangent0 = math.normalizesafe(tangent01) * newTangentLength01;
            curve.Tangent1 = math.normalizesafe(tangent02) * newTangentLength02;

            // curve.Tangent0 = newTangent01;
            // curve.Tangent1 = newTangent02;

            return curve;
        }

        public static void OffsetKnotX(Spline spline, int knotIndex, float offsetX)
        {
            var knot = spline[knotIndex];
            var tangentOut = math.normalizesafe(new float3(knot.TangentOut.x, 0f, knot.TangentOut.z));
            var tangentPerp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangentOut);
            knot.Position += tangentPerp * offsetX;
            spline.SetKnot(knotIndex, knot);
        }

        private static bool IsSplineStraight(BezierKnot knot01, BezierKnot knot02, float straightAngleTolerance)
        {
            var tangent01 = knot01.TangentOut;
            var tangent02 = knot02.TangentIn;
            var directTangent = knot02.Position - knot01.Position;
            var angleDirect01 = math.abs(math.degrees(PGTrigonometryUtility.AngleXZ(directTangent, tangent01)));
            var angleDirect02 = math.abs(math.degrees(PGTrigonometryUtility.AngleXZ(directTangent, -tangent02)));
            return angleDirect01 <= straightAngleTolerance && angleDirect02 <= straightAngleTolerance;
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/
        
        /// <summary>
        ///     Inserts a knot into a Bezier Curve without affecting the curvature.
        /// </summary>
        /// <param name="t">Clamped from 0.01 to 0.99. Should be verified before calling this method.</param>
        /// <returns>Index of the inserted knot.</returns>
        public static int InsertKnotSeamless(Spline spline, float t)
        {
            t = math.clamp(t, 0.01f, 0.99f);

            var curveIndex = spline.SplineToCurveT(t, out var curveT);
            var nextCurveIndex = curveIndex + 1;
            if (curveIndex == spline.Count - 1) nextCurveIndex = 0; // End of a closed spline

            var knotBefore = spline[curveIndex];
            var knotAfter = spline[nextCurveIndex];
            
            var curve = spline.GetCurve(curveIndex);
            CurveUtility.Split(curve, curveT, out var curveLeft, out var curveRight);
            
            var knotInsert = new BezierKnot
            {
                Position = curveLeft.P3, 
                TangentIn = curveLeft.P2 - curveLeft.P3,  
                TangentOut = curveRight.P1 - curveRight.P0,
                Rotation = quaternion.identity
            };
            
            knotBefore.TangentOut = curveLeft.P1 - curveLeft.P0;
            knotAfter.TangentIn = curveRight.P2 - curveRight.P3;

            spline.SetKnot(curveIndex, knotBefore);
            spline.SetKnot(nextCurveIndex, knotAfter);
            spline.Insert(curveIndex + 1, knotInsert, TangentMode.Broken);
            
            return curveIndex + 1;
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public static void ReduceSpline(Spline spline, bool start, float splineLength, float reducedLength)
        {
            var t = reducedLength / splineLength;
            if (!start) t = 1f - t;

            if (t is >= 0.99f or <= 0.01f) return; // Hard limit for InsertKnotSeamless

            var insertKnotIndex = InsertKnotSeamless(spline, t);
            if (!start)
                for (var i = spline.Count - 1; i > insertKnotIndex; i--)
                    spline.RemoveAt(i);
            else
                for (var i = insertKnotIndex - 1; i >= 0; i--)
                    spline.RemoveAt(i);
        }

        public static Spline CreateIncreasedSpline(Spline spline, bool start, float increasedLength)
        {
            var knots = spline.Knots.ToList();
            if (start)
            {
                var knot = knots.First();
                var tangent = knot.TangentIn;
                knot.Position += tangent * increasedLength;
                knots[0] = knot;
            }
            else
            {
                var knot = knots.Last();
                var tangent = knot.TangentOut;
                knot.Position += tangent * increasedLength;
                knots[^1] = knot;
            }

            return new Spline(knots);
        }

        public static BezierKnot GetNearestKnot(Spline spline, float3 position)
        {
            var nearestKnotIndex = GetNearestKnotIndex(spline, position);
            return spline[nearestKnotIndex];
        }

        public static int GetNearestKnotIndex(Spline spline, float3 position)
        {
            var nearestKnotIndex = 0;
            var nearestDistance = float.MaxValue;
            var index = 0;
            foreach (var knot in spline.Knots)
            {
                var distance = math.distancesq(position, knot.Position);
                if (distance < nearestDistance)
                {
                    nearestKnotIndex = index;
                    nearestDistance = distance;
                }

                index++;
            }

            return nearestKnotIndex;
        }

        public static void TranslateSpline(Spline spline, float3 offset)
        {
            for (var i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                knot.Position += offset;
                spline[i] = knot;
            }
        }

        public static void RotateSpline(Spline spline, float3 pivot, quaternion rotation)
        {
            for (var i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                knot.Position -= pivot;
                knot.Position = math.rotate(rotation, knot.Position);
                knot.Position += pivot;
                knot.TangentIn = math.rotate(rotation, knot.TangentIn);
                knot.TangentOut = math.rotate(rotation, knot.TangentOut);
                spline[i] = knot;
            }
        }

        /// <summary>
        ///     Removes knot rotations while preserving the shape.
        /// </summary>
        public static void NegateRotations(Spline spline)
        {
            for (var i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                var rotation = knot.Rotation;
                var tangentIn = math.rotate(rotation, knot.TangentIn);
                var tangentOut = math.rotate(rotation, knot.TangentOut);
                knot.Rotation = quaternion.identity;
                knot.TangentIn = math.rotate(quaternion.identity, tangentIn);
                knot.TangentOut = math.rotate(quaternion.identity, tangentOut);
                spline[i] = knot;
            }
        }

        public static void LocalToWorld(Transform splineContainer, Spline spline)
        {
            for (var i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                knot.Position = splineContainer.TransformPoint(knot.Position);
                knot.TangentIn = splineContainer.TransformDirection(knot.TangentIn);
                knot.TangentOut = splineContainer.TransformDirection(knot.TangentOut);
                spline[i] = knot;
            }
        }

        public static void WorldToLocal(Transform splineContainer, Spline spline)
        {
            for (var i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                knot.Position = splineContainer.InverseTransformPoint(knot.Position);
                knot.TangentIn = splineContainer.InverseTransformDirection(knot.TangentIn);
                knot.TangentOut = splineContainer.InverseTransformDirection(knot.TangentOut);
                spline[i] = knot;
            }
        }

        public static List<Spline> CreateIntersectionSplines(float3 center, List<float3> positions)
        {
            var newSplines = new List<Spline>();

            for (var i = 0; i < positions.Count; i++)
            for (var j = i + 1; j < positions.Count; j++)
            {
                var pos1 = positions[i];
                var pos2 = positions[j];
                var newSpline = CreateIntersectionSpline(center, pos1, pos2);
                newSplines.Add(newSpline);
            }

            return newSplines;
        }

        public static Spline CreateIntersectionSpline(float3 center, float3 position1, float3 position2)
        {
            var tan1out = new float3(center - position1) * TangentLengthIntersection;
            var tan1in = -tan1out;
            var tan2out = new float3(position2 - center) * TangentLengthIntersection;
            var tan2in = -tan2out;

            var knot1 = new BezierKnot
            {
                Position = position1,
                TangentOut = tan1out,
                TangentIn = tan1in,
                Rotation = quaternion.identity
            };
            var knot2 = new BezierKnot
            {
                Position = position2,
                TangentOut = tan2out,
                TangentIn = tan2in,
                Rotation = quaternion.identity
            };

            var newSpline = new Spline
            {
                {knot1, TangentMode.Broken},
                {knot2, TangentMode.Broken}
            };

            return newSpline;
        }

        // SplineUtility.ReverseFlow creates 180° rotation
        public static void InvertSpline(Spline spline)
        {
            var newKnots = new BezierKnot[spline.Count];
            for (var i = 0; i < spline.Count; i++)
            {
                var oldKnot = spline[spline.Count - 1 - i];
                newKnots[i] = new BezierKnot
                {
                    Position = oldKnot.Position,
                    TangentIn = oldKnot.TangentOut,
                    TangentOut = oldKnot.TangentIn,
                    Rotation = oldKnot.Rotation
                };
            }

            for (var i = 0; i < spline.Count; i++) spline[i] = newKnots[i];
        }

        public static void FlattenSpline(Spline spline, float height)
        {
            var newKnots = new BezierKnot[spline.Count];
            for (var i = 0; i < spline.Count; i++)
            {
                var oldKnot = spline[spline.Count - 1 - i];
                newKnots[i] = new BezierKnot
                {
                    Position = new float3(oldKnot.Position.x, height, oldKnot.Position.z),
                    TangentIn = new float3(oldKnot.TangentOut.x, 0f, oldKnot.TangentOut.z),
                    TangentOut = new float3(oldKnot.TangentIn.x, 0f, oldKnot.TangentIn.z),
                    Rotation = oldKnot.Rotation
                };
            }

            for (var i = 0; i < spline.Count; i++) spline[i] = newKnots[i];
        }

        /// <summary>
        ///     The problem with spline.SetTangentMode(TangentMode.AutoSmooth) is that it doesn't actually set the tangents.
        /// </summary>
        public static void AutoSmooth(Spline spline)
        {
            for (var i = 0; i < spline.Count; i++)
            {
                var knot = spline[i];
                var knotPrevious = i > 0 ? spline[i - 1] : knot;
                var knotNext = i < spline.Count - 1 ? spline[i + 1] : knot;
                var tangent = SplineUtility.GetAutoSmoothTangent(knotPrevious.Position, knot.Position, knotNext.Position);
                knot.TangentIn = -tangent;
                knot.TangentOut = tangent;
                spline[i] = knot;
            }
        }

        /// <summary>
        ///     Splits a multi-knot spline into multiple 2-knot splines.
        /// </summary>
        public static List<Spline> SplitSplineToMultiple(Spline spline)
        {
            var splitSplines = new List<Spline>();

            for (var i = 0; i < spline.Count - 1; i++)
            {
                var knot01 = spline[i];
                var knot02 = spline[i + 1];

                var splitSpline = new Spline
                {
                    {knot01, TangentMode.Broken},
                    {knot02, TangentMode.Broken}
                };

                splitSplines.Add(splitSpline);
            }

            return splitSplines;
        }
        
        public static Spline CurveToSpline(BezierCurve curve)
        {
            var knot01 = new BezierKnot
            {
                Position = curve.P0,
                TangentOut = curve.Tangent0,
                TangentIn = -curve.Tangent0,
                Rotation = quaternion.identity
            };
            var knot02 = new BezierKnot
            {
                Position = curve.P3,
                TangentOut = -curve.Tangent1,
                TangentIn = curve.Tangent1,
                Rotation = quaternion.identity
            };
            
            var spline = new Spline
            {
                {knot01, TangentMode.Broken},
                {knot02, TangentMode.Broken}
            };
            return spline;
        }

        public static float GetCurvature(BezierKnot knot01, BezierKnot knot02)
        {
            return GetCurvature(knot01.Position, knot01.TangentOut, knot02.Position, knot02.TangentOut);
        }

        public static float GetCurvature(Vector3 position01, Vector3 tangent01, Vector3 position02, Vector3 tangent02)
        {
            var vector01 = position01 + tangent01.normalized * (position02 - position01).magnitude;
            var vector02 = position02 + tangent02.normalized * (position02 - position01).magnitude;
            var diff = vector02 - vector01;
            var curvature = Vector3.Angle(vector01 - position01, diff);
            return curvature;
        }

        public static int CalculateResolution(int resolution, bool smartReduce, bool smoothSlope, BezierKnot knot01, BezierKnot knot02,
            float lodAmount)
        {
            if (smartReduce)
            {
                var originalRes = resolution;

                resolution = SmartReduceResolution(resolution, knot01.Position, knot01.TangentOut, knot02.Position,
                    knot02.TangentOut);

                if (smoothSlope)
                {
                    var slope = math.abs(math.degrees(PGTrigonometryUtility.Slope(knot01.Position, knot02.Position)));
                    var slopeRatio = 1 + slope / 2.5f; // Approximation
                    resolution = (int) math.round(resolution * slopeRatio);
                }

                if (resolution > originalRes) resolution = originalRes;
            }

            resolution = (int) math.round(resolution * lodAmount);
            if (resolution == 0) resolution = 1;

            return resolution;
        }

        private static int SmartReduceResolution(int resolution, float3 position01, float3 tangent01, float3 position02, float3 tangent02)
        {
            var angleTan = math.abs(math.degrees(PGTrigonometryUtility.AngleXZ(tangent01, tangent02)));
            var anglePos = math.abs(math.degrees(PGTrigonometryUtility.AngleXZ(position02 - position01, tangent02)));

            var angle = math.max(anglePos, angleTan);

            if (angle > 90f) angle = 90f;
            if (angle > 2f)
            {
                var angleRatio = 1 / (1 + math.exp(-angle * 0.1f));
                resolution = math.max(1, (int) math.round(resolution * angleRatio));
            }
            else
            {
                resolution = 1;
            }

            return resolution;
        }

        public static float GetUnitCircleTangentLength(float radius)
        {
            // https://mechanicalexpressions.com/explore/geometric-modeling/circle-spline-approximation.pdf
            var value = 4f * (math.sqrt(2f) - 1f) / 3f;
            return value * radius * 2.5f;
        }

        public static List<BezierKnot> GetUniqueKnots(SplineContainer splineContainer, float tolerance = 0.01f)
        {
            var uniqueKnots = new List<BezierKnot>();

            var splines = splineContainer.Splines;
            for (var i = 0; i < splines.Count; i++)
            {
                var spline = splines[i];
                for (var j = 0; j < spline.Count; j++)
                {
                    var knot = spline[j];
                    if (!uniqueKnots.Any(existingKnot =>
                            Mathf.Abs(existingKnot.Position.x - knot.Position.x) < tolerance &&
                            Mathf.Abs(existingKnot.Position.z - knot.Position.z) < tolerance))
                        uniqueKnots.Add(knot);
                }
            }

            return uniqueKnots;
        }

        /// <summary>
        ///     Creates a list of normalized T values representing evenly spaced points along the spline.
        /// </summary>
        public static List<float> GetEvenlySpacedTValues(float splineLength, float spacing, out float gap)
        {
            var evaluations = new List<float>();
            gap = 0f;

            if (spacing > splineLength)
            {
                evaluations.Add(0.5f);
                return evaluations;
            }

            var count = Mathf.FloorToInt(splineLength / spacing);
            if (count < 1)
            {
                evaluations.Add(0.5f);
                return evaluations;
            }

            var leftover = splineLength - count * spacing;
            gap = leftover / count;
            var adjustedSpacing = spacing + gap;

            for (var i = 0; i < count; i++)
            {
                var distance = (i + 0.5f) * adjustedSpacing;
                var t = distance / splineLength;
                evaluations.Add(t);
            }

            return evaluations;
        }
    }
}