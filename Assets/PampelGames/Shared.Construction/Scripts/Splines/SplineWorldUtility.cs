// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using PampelGames.Shared.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    [BurstCompile]
    public static class SplineWorldUtility
    {
        public static void SplineEvaluationMiddle(Spline spline, float length, out float[] ts, out float3[] positions, out float3[] tangents)
        {
            CreateNativeSplineObjects(length, spline, 
                out var splineTsNative, out var splineNative, out var splinePositionsNative, out var splineTangentsNative);

            var job = new SplinePositionsMiddleJob
            {
                _spline = splineNative,
                _splineTs = splineTsNative,
                _splinePositions = splinePositionsNative,
                _splineTangents = splineTangentsNative
            };

            job.Schedule(splineTsNative.Length, 32).Complete();

            ts = splineTsNative.ToArray();
            positions = splinePositionsNative.ToArray();
            tangents = splineTangentsNative.ToArray();

            splineTsNative.Dispose();
            splineNative.Dispose();
            splinePositionsNative.Dispose();
            splineTangentsNative.Dispose();
        }
        
        public static void SplineEvaluationMiddle(Spline spline, float[] ts, out float3[] positions, out float3[] tangents)
        {
            var splineTsNative = new NativeArray<float>(ts, Allocator.TempJob);
            var splineNative = new NativeSpline(spline, Allocator.TempJob);
            var splinePositionsNative = new NativeArray<float3>(ts.Length, Allocator.TempJob);
            var splineTangentsNative = new NativeArray<float3>(ts.Length, Allocator.TempJob);

            var job = new SplinePositionsMiddleJob
            {
                _spline = splineNative,
                _splineTs = splineTsNative,
                _splinePositions = splinePositionsNative,
                _splineTangents = splineTangentsNative
            };

            job.Schedule(splineTsNative.Length, 32).Complete();

            positions = splinePositionsNative.ToArray();
            tangents = splineTangentsNative.ToArray();

            splineTsNative.Dispose();
            splineNative.Dispose();
            splinePositionsNative.Dispose();
            splineTangentsNative.Dispose();
        }

        [BurstCompile]
        private struct SplinePositionsMiddleJob : IJobParallelFor
        {
            public NativeSpline _spline;
            public NativeArray<float> _splineTs;
            public NativeArray<float3> _splinePositions;
            public NativeArray<float3> _splineTangents;

            public void Execute(int i)
            {
                _spline.Evaluate(_splineTs[i], out var pos, out var tan, out var upVector);
                tan.y = 0f;
                _splineTangents[i] = tan;
                _splinePositions[i] = pos;
            }
        }

        /********************************************************************************************************************************/
        
        // Left and right positions have wrong heights. Use middle spline height, identical to SplineMesh.cs.
        public static void SplineEvaluationCenterToLeftRight(Spline splineMiddle, Spline splineLeft, Spline splineRight, float length,
            out float3[] positionsLeft, out float3[] positionsRight)
        {
            var splineMiddleNative = new NativeSpline(splineMiddle, Allocator.TempJob);
            var splineLeftNative = new NativeSpline(splineLeft, Allocator.TempJob);
            var splineRightNative = new NativeSpline(splineRight, Allocator.TempJob);
            var splinePositionsLeftNative = new NativeList<float3>(Allocator.TempJob);
            var splinePositionsRightNative = new NativeList<float3>(Allocator.TempJob);

            var job = new SplinePositionsJob
            {
                _splineMiddle = splineMiddleNative,
                _splineLeft = splineLeftNative,
                _splineRight = splineRightNative,
                _length = length,
                _splinePositionsLeft = splinePositionsLeftNative,
                _splinePositionsRight = splinePositionsRightNative,
            };

            job.Schedule().Complete();

            positionsLeft = splinePositionsLeftNative.AsArray().ToArray();
            positionsRight = splinePositionsRightNative.AsArray().ToArray();

            splineMiddleNative.Dispose();
            splineLeftNative.Dispose();
            splineRightNative.Dispose();
            splinePositionsLeftNative.Dispose();
            splinePositionsRightNative.Dispose();
        }

        
        [BurstCompile]
        private struct SplinePositionsJob : IJob
        {
            public NativeSpline _splineMiddle;
            public NativeSpline _splineLeft;
            public NativeSpline _splineRight;
            public float _length;
            public NativeList<float3> _splinePositionsLeft;
            public NativeList<float3> _splinePositionsRight;

            public void Execute()
            {
                var normalizedDistance = _length / _splineMiddle.GetLength();
                var initialSize = (int)(1 / normalizedDistance) + 1;

                // Process all points except the last one
                for (var i = 0; i < initialSize; i++)
                {
                    var t = i * normalizedDistance;
                    var positionMiddle = _splineMiddle.EvaluatePosition(t);
                    var positionLeft = _splineLeft.EvaluatePosition(t);
                    var positionRight = _splineRight.EvaluatePosition(t);
                    positionLeft.y = positionMiddle.y;
                    positionRight.y = positionMiddle.y;
            
                    _splinePositionsLeft.Add(positionLeft);
                    _splinePositionsRight.Add(positionRight);
                }

                // Handle t = 1.0 explicitly
                var positionMiddle1 = _splineMiddle.EvaluatePosition(1f);
                var positionLeft1 = _splineLeft.EvaluatePosition(1f);
                var positionRight1 = _splineRight.EvaluatePosition(1f);
                positionLeft1.y = positionMiddle1.y;
                positionRight1.y = positionMiddle1.y;
        
                _splinePositionsLeft.Add(positionLeft1);
                _splinePositionsRight.Add(positionRight1);
            }
        }
        
        /********************************************************************************************************************************/
        
        public static void SplineEvaluationLeftRight(Spline spline, float length, float width,
            out float3[] positionsLeft, out float3[] positionsRight, out float3[] tangents)
        {
            var splineNative = new NativeSpline(spline, Allocator.TempJob);
            var splinePositionsLeftNative = new NativeList<float3>(Allocator.TempJob);
            var splinePositionsRightNative = new NativeList<float3>(Allocator.TempJob);
            var splineTangentsNative = new NativeList<float3>(Allocator.TempJob);

            var job = new SplinePositionsLeftRightJob
            {
                _spline = splineNative,
                _length = length,
                _width = width,
                _splinePositionsLeft = splinePositionsLeftNative,
                _splinePositionsRight = splinePositionsRightNative,
                _splineTangents = splineTangentsNative
            };

            job.Schedule().Complete();

            positionsLeft = splinePositionsLeftNative.AsArray().ToArray();
            positionsRight = splinePositionsRightNative.AsArray().ToArray();
            tangents = splineTangentsNative.AsArray().ToArray();

            splineNative.Dispose();
            splinePositionsLeftNative.Dispose();
            splinePositionsRightNative.Dispose();
            splineTangentsNative.Dispose();
        }

        [BurstCompile]
        private struct SplinePositionsLeftRightJob : IJob
        {
            public NativeSpline _spline;
            public float _length;
            public float _width;
            public NativeList<float3> _splinePositionsLeft;
            public NativeList<float3> _splinePositionsRight;
            public NativeList<float3> _splineTangents;

            public void Execute()
            {
                var normalizedDistance = _length / _spline.GetLength();
                for (var t = 0f; t <= 1f; t += normalizedDistance)
                {
                    _spline.Evaluate(t, out var pos, out var tan, out var upVector);
                    tan.y = 0f;
                    _splineTangents.Add(tan);
                    var tanPerp = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tan);
                    tanPerp = math.normalizesafe(tanPerp);
                    var positionLeft = pos + tanPerp * _width;
                    var positionRight = pos - tanPerp * _width;
                    _splinePositionsLeft.Add(positionLeft);
                    _splinePositionsRight.Add(positionRight);
                }

                // Manually evaluate at t = 1.0
                _spline.Evaluate(1f, out var pos1, out var tan1, out var upVector1);
                tan1.y = 0f;
                _splineTangents.Add(tan1);
                var tanPerp1 = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tan1);
                tanPerp1 = math.normalizesafe(tanPerp1);
                var positionLeft1 = pos1 + tanPerp1 * _width;
                var positionRight1 = pos1 - tanPerp1 * _width;
                _splinePositionsLeft.Add(positionLeft1);
                _splinePositionsRight.Add(positionRight1);
            }
        }
        
        /********************************************************************************************************************************/
        
        private static void CreateNativeSplineObjects(float length, Spline spline, 
            out NativeArray<float> splineTsNative, out NativeSpline splineNative,
            out NativeArray<float3> splinePositionsNative, out NativeArray<float3> splineTangentsNative)
        {
            var normalizedDistance = length / spline.GetLength();
            var initialSize = (int)(1 / normalizedDistance) + 1;
            var size = initialSize + 1; // Add one to size for t = 1f
            var splineTs = new float[size];

            for (var i = 0; i < initialSize; i++) splineTs[i] = i * normalizedDistance;
            splineTs[^1] = 1f;

            splineTsNative = new NativeArray<float>(splineTs, Allocator.TempJob);
            splineNative = new NativeSpline(spline, Allocator.TempJob);
            splinePositionsNative = new NativeArray<float3>(size, Allocator.TempJob);
            splineTangentsNative = new NativeArray<float3>(size, Allocator.TempJob);
        }
        

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public static float3[] CheckPositionsInsideBounds(float3[] positions, Bounds[] bounds)
        {
            var boundsNative = new NativeArray<Bounds>(bounds, Allocator.TempJob);
            var positionsNative = new NativeArray<float3>(positions, Allocator.TempJob);
            var overlapPositionsNative = new NativeList<float3>(Allocator.TempJob);

            var job = new CheckPositionsInsideBoundsJob
            {
                _bounds = boundsNative,
                _positions = positionsNative,
                _overlapPositions = overlapPositionsNative
            };

            job.Schedule().Complete();

            var indexes = overlapPositionsNative.AsArray().ToArray();

            boundsNative.Dispose();
            positionsNative.Dispose();
            overlapPositionsNative.Dispose();

            return indexes;
        }

        [BurstCompile]
        private struct CheckPositionsInsideBoundsJob : IJob
        {
            public NativeArray<Bounds> _bounds;
            public NativeArray<float3> _positions;
            public NativeList<float3> _overlapPositions;

            public void Execute()
            {
                for (var i = 0; i < _bounds.Length; i++)
                {
                    var bound = _bounds[i];
                    for (var j = 0; j < _positions.Length; j++)
                    {
                        var position = _positions[j];
                        if (bound.Contains(position)) _overlapPositions.Add(position);
                    }
                }
            }
        }

        /********************************************************************************************************************************/

        public static float3[] CheckPositionsInsideSpline(float3[] positions, Spline spline, float width,
            float overlapDistance, float overlapHeight)
        {
            if (positions.Length < 5) return Array.Empty<float3>();

            var positionsNative = new NativeArray<float3>(positions, Allocator.TempJob);
            var splineNative = new NativeSpline(spline, Allocator.TempJob);
            var overlapPositionsNative = new NativeList<float3>(Allocator.TempJob);

            var job = new CheckPositionsInsideSplineJob
            {
                _positions = positionsNative,
                _spline = splineNative,
                _width = width,
                _overlapDistance = overlapDistance,
                _overlapHeight = overlapHeight,
                _overlapPositions = overlapPositionsNative
            };

            job.Schedule().Complete();

            var overlapPositions = overlapPositionsNative.AsArray().ToArray();

            positionsNative.Dispose();
            splineNative.Dispose();
            overlapPositionsNative.Dispose();

            return overlapPositions;
        }

        [BurstCompile]
        private struct CheckPositionsInsideSplineJob : IJob
        {
            public NativeArray<float3> _positions;
            public NativeSpline _spline;
            public float _width;
            public float _overlapDistance;
            public float _overlapHeight;
            public NativeList<float3> _overlapPositions;

            public void Execute()
            {
                for (var i = 0; i < _positions.Length; i++) 
                {
                    // Don't use positions which may overlap with own connection
                    if (i < 2 || i >= _positions.Length - 3) continue;
                    
                    var pos = _positions[i];
                    UnityEngine.Splines.SplineUtility.GetNearestPoint(_spline, pos, out var nearest, out var t);
                    var pos2D = new float2(pos.x, pos.z);
                    var nearest2D = new float2(nearest.x, nearest.z);
                    if (math.distance(pos2D, nearest2D) < _width + _overlapDistance)
                        if (math.distance(pos.y, nearest.y) < _overlapHeight)
                        {
                            _overlapPositions.Add(nearest);
                            return;
                        }
                }
            }
        }
        
    }
}