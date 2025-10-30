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
    /// <summary>
    ///     When creating an array, it defines the x and y positions for each edge loop in t (z+) direction of the spline.
    ///     This way it can also be used to create circles, quads etc.
    /// </summary>
    public struct SplineEdge
    {
        /// <summary>
        ///     x,y position for each point on the edge (z is spline forward).
        /// </summary>
        public float2 position;

        /// <summary>
        ///     uv x for each point on the edge.
        ///     uv y is calculated automatically over the length from 0 to 1.
        /// </summary>
        public float uvX;

        /// <summary>
        ///     By default, normals are aligned to the spline upVector at position t.
        ///     Positive (negative) values will rotate the normal (counter-) clockwise in degrees.
        /// </summary>
        public float normalRotation;

        /// <summary>
        ///     If true, faces won't be created to the next SplineEdge array item.
        /// </summary>
        public bool edge;

        public SplineEdge(float2 position, float uvX, float normalRotation, bool edge = false)
        {
            this.position = position;
            this.uvX = uvX;
            this.normalRotation = normalRotation;
            this.edge = edge;
        }

        public SplineEdge(SplineEdge splineEdge)
        {
            position = splineEdge.position;
            uvX = splineEdge.uvX;
            normalRotation = splineEdge.normalRotation;
            edge = splineEdge.edge;
        }
    }

    public enum SplineLengthUV
    {
        Stretch,
        Cut
    }

    public enum SplineLeftRight
    {
        None, // Calculates left right positions from the main spline (most performant, but may produce mesh overlap in tight curves)
        Create, // Method creates offset splines for splineLeft and splineRight
        Custom // Use custom splineLeft and splineRight
    }

    [Serializable]
    public class SplineMeshParameter
    {
        public float partWidth;
        public float partLength;
        public int resolution;
        public SplineLengthUV splineLengthUV;
        public Spline spline;
        public SplineLeftRight splineLeftRight;
        public Spline splineLeft;
        public Spline splineRight;
        public bool uvToWidth; // Adjusts the uv width based on the left to right width for each edge.
        public bool closeStart;
        public bool closeEnd;
        public bool closeVertical; // If false, closes horizontally
        public Vector2 widthRange; // Applies a width multiplier from .x to .y (normalized)
        public float widthStart;
        public float widthEnd;
        public bool invertWidthRange; // Applies the width multiplier from 0 to .x and .y to 1 instead
        public Vector2 flattenRange; // Flattens heights from .x to .y (normalized).
        public float flattenHeight;
        public Vector2 rangedOffsetRange; // Local offset from .x to .y (normalized).
        public Vector3 rangedOffset;

        public SplineMeshParameter(float partWidth, float partLength, int resolution, SplineLengthUV splineLengthUV, Spline spline,
            SplineLeftRight splineLeftRight, Spline splineLeft = null, Spline splineRight = null, bool uvToWidth = false,
            bool closeStart = false, bool closeEnd = false, bool closeVertical = false,
            Vector2 widthRange = default, float widthStart = 1f, float widthEnd = 1f, bool invertWidthRange = false,
            Vector2 flattenRange = default, float flattenHeight = 0f, Vector2 rangedOffsetRange = default, Vector3 rangedOffset = default)
        {
            this.partWidth = partWidth;
            this.partLength = partLength;
            this.resolution = resolution;
            this.splineLengthUV = splineLengthUV;
            this.spline = spline;
            this.splineLeftRight = splineLeftRight;
            this.splineLeft = splineLeft;
            this.splineRight = splineRight;
            this.uvToWidth = uvToWidth;
            this.closeStart = closeStart;
            this.closeEnd = closeEnd;
            this.closeVertical = closeVertical;
            this.widthRange = widthRange;
            this.widthStart = widthStart;
            this.widthEnd = widthEnd;
            this.invertWidthRange = invertWidthRange;
            this.flattenRange = flattenRange;
            this.flattenHeight = flattenHeight;
            this.rangedOffsetRange = rangedOffsetRange;
            this.rangedOffset = rangedOffset;
        }

        public SplineMeshParameter(SplineMeshParameter parameterCopy)
        {
            partWidth = parameterCopy.partWidth;
            partLength = parameterCopy.partLength;
            resolution = parameterCopy.resolution;
            splineLengthUV = parameterCopy.splineLengthUV;
            spline = parameterCopy.spline;
            splineLeftRight = parameterCopy.splineLeftRight;
            splineLeft = parameterCopy.splineLeft;
            splineRight = parameterCopy.splineRight;
            uvToWidth = parameterCopy.uvToWidth;
            closeStart = parameterCopy.closeStart;
            closeEnd = parameterCopy.closeEnd;
            closeVertical = parameterCopy.closeVertical;
            widthRange = parameterCopy.widthRange;
            widthStart = parameterCopy.widthStart;
            widthEnd = parameterCopy.widthEnd;
            invertWidthRange = parameterCopy.invertWidthRange;
            flattenRange = parameterCopy.flattenRange;
            flattenHeight = parameterCopy.flattenHeight;
            rangedOffsetRange = parameterCopy.rangedOffsetRange;
            rangedOffset = parameterCopy.rangedOffset;
        }

        /// <summary>
        ///     Simplified parameters used for ISharedObject.
        /// </summary>
        public SplineMeshParameter(SplineLengthUV splineLengthUV, Spline spline,
            SplineLeftRight splineLeftRight, Spline splineLeft = null, Spline splineRight = null, bool uvToWidth = false,
            bool closeStart = false, bool closeEnd = false, bool closeVertical = false,
            Vector2 widthRange = default, float widthStart = 1f, float widthEnd = 1f, bool invertWidthRange = false,
            Vector2 flattenRange = default, float flattenHeight = 0f, Vector2 rangedOffsetRange = default, Vector3 rangedOffset = default)
        {
            this.splineLengthUV = splineLengthUV;
            this.spline = spline;
            this.splineLeftRight = splineLeftRight;
            this.splineLeft = splineLeft;
            this.splineRight = splineRight;
            this.uvToWidth = uvToWidth;
            this.closeStart = closeStart;
            this.closeEnd = closeEnd;
            this.closeVertical = closeVertical;
            this.widthRange = widthRange;
            this.widthStart = widthStart;
            this.widthEnd = widthEnd;
            this.invertWidthRange = invertWidthRange;
            this.flattenRange = flattenRange;
            this.flattenHeight = flattenHeight;
            this.rangedOffsetRange = rangedOffsetRange;
            this.rangedOffset = rangedOffset;
        }

        public void SetISharedObjectValues(float _partWidth, float _partLength, int _resolution)
        {
            partWidth = _partWidth;
            partLength = _partLength;
            resolution = _resolution;
        }
    }

    public static class SplineMesh
    {
        public static void CreateSplineMesh(Mesh mesh, SplineEdge[] splineEdges, SplineMeshParameter parameter,
            float tStart = 0f, float tEnd = 1f, Vector3 localOffset = default)
        {
            var spline = parameter.spline;
            var partWidth = parameter.partWidth;
            var partLength = parameter.partLength;
            var resolution = parameter.resolution;
            var splineLengthUV = parameter.splineLengthUV;
            var uvToWidth = parameter.uvToWidth;

            mesh.Clear();

            var splineLeft = parameter.splineLeft;
            var splineRight = parameter.splineRight;
            if (parameter.splineLeftRight == SplineLeftRight.Create)
            {
                splineLeft = new Spline(spline);
                splineRight = new Spline(spline);
                ConstructionSplineUtility.OffsetSplineParallel(splineLeft, -partWidth * 0.5f);
                ConstructionSplineUtility.OffsetSplineParallel(splineRight, partWidth * 0.5f);
            }
            else if (parameter.splineLeftRight == SplineLeftRight.None)
            {
                splineLeft = new Spline();
                splineRight = new Spline();
            }

            var vertexStructs = new NativeList<VertexStruct01>(Allocator.TempJob);
            var nativeSpline = new NativeSpline(spline, Allocator.TempJob);
            var nativeSplineLeft = new NativeSpline(splineLeft, Allocator.TempJob);
            var nativeSplineRight = new NativeSpline(splineRight, Allocator.TempJob);

            var trianglesList = new NativeList<int>(Allocator.TempJob);
            var splineEdgesNative = new NativeArray<SplineEdge>(splineEdges, Allocator.TempJob);

            CreateSplineMeshJob(splineEdgesNative, vertexStructs, trianglesList, nativeSpline, nativeSplineLeft, nativeSplineRight,
                parameter.splineLeftRight, partWidth, partLength, resolution, splineLengthUV, tStart, tEnd, localOffset, uvToWidth,
                parameter.closeStart, parameter.closeEnd, parameter.closeVertical,
                parameter.widthRange, parameter.widthStart, parameter.widthEnd, parameter.invertWidthRange,
                parameter.flattenRange, parameter.flattenHeight, parameter.rangedOffsetRange, parameter.rangedOffset);

            PGMeshAPIUtility.SetBufferData01(mesh, vertexStructs.AsArray(), trianglesList.AsArray());
            PGMeshAPIUtility.SetSubMesh(mesh, 0, vertexStructs.Length, trianglesList.Length);
            PGMeshUtility.RecalculateMeshData(mesh, false);

            vertexStructs.Dispose();
            trianglesList.Dispose();
            nativeSpline.Dispose();
            nativeSplineLeft.Dispose();
            nativeSplineRight.Dispose();
            splineEdgesNative.Dispose();
        }

        public static void CreateSplineMeshJob(NativeArray<SplineEdge> splineEdges,
            NativeList<VertexStruct01> vertexStructs, NativeList<int> trianglesList,
            NativeSpline nativeSpline, NativeSpline nativeSplineLeft, NativeSpline nativeSplineRight, SplineLeftRight splineLeftRight,
            float partWidth, float partLength, int partResolution, SplineLengthUV splineLengthUV,
            float tStart = 0f, float tEnd = 1f, Vector3 localOffset = default, bool uvToWidth = false,
            bool closeStart = false, bool closeEnd = false, bool closeVertical = false,
            Vector2 widthRange = default, float widthStart = 1f, float widthEnd = 1f, bool invertWidthRange = false,
            Vector2 flattenRange = default, float flattenHeight = 0f, Vector2 rangedOffsetRange = default, Vector3 rangedOffset = default)
        {
            var job = new SplineMeshJob
            {
                splineEdges = splineEdges,

                partWidth = partWidth,
                partLength = partLength,
                partResolution = partResolution,
                splineLengthUV = splineLengthUV,

                tStart = tStart,
                tEnd = tEnd,
                uvToWidth = uvToWidth,
                closeStart = closeStart,
                closeEnd = closeEnd,
                closeVertical = closeVertical,

                widthRange = widthRange == default ? Vector2.zero : widthRange,
                widthStart = widthStart,
                widthEnd = widthEnd,
                invertWidthRange = invertWidthRange,
                flattenRange = flattenRange == default ? Vector2.zero : flattenRange,
                flattenHeight = flattenHeight,
                rangedOffsetRange = rangedOffsetRange == default ? Vector2.zero : rangedOffsetRange,
                rangedOffset = rangedOffset == default ? Vector3.zero : rangedOffset,
                localOffset = localOffset == default ? Vector3.zero : localOffset,

                vertexStructs = vertexStructs,
                trianglesList = trianglesList,
                nativeSpline = nativeSpline,
                nativeSplineLeft = nativeSplineLeft,
                nativeSplineRight = nativeSplineRight,
                splineLeftRight = splineLeftRight
            };

            var handle = job.Schedule();
            handle.Complete();
        }


        [BurstCompile]
        private struct SplineMeshJob : IJob
        {
            public NativeArray<SplineEdge> splineEdges;
            public float partWidth;
            public float partLength;
            public int partResolution;
            public SplineLengthUV splineLengthUV;
            public float tStart;
            public float tEnd;
            public bool uvToWidth;
            public bool closeStart;
            public bool closeEnd;
            public bool closeVertical;

            public Vector2 widthRange;
            public float widthStart;
            public float widthEnd;
            public bool invertWidthRange;
            public float2 flattenRange;
            public float flattenHeight;
            public float2 rangedOffsetRange;
            public float3 rangedOffset;
            public float3 localOffset;

            public NativeList<VertexStruct01> vertexStructs;
            public NativeList<int> trianglesList;
            public NativeSpline nativeSpline;
            public NativeSpline nativeSplineLeft;
            public NativeSpline nativeSplineRight;
            public SplineLeftRight splineLeftRight;

            public void Execute()
            {
                SplineMeshExecute(splineEdges, partWidth, partLength, partResolution,
                    splineLengthUV, tStart, tEnd, uvToWidth, splineLeftRight,
                    widthRange, widthStart, widthEnd, invertWidthRange,
                    flattenRange, flattenHeight, rangedOffsetRange, rangedOffset,
                    vertexStructs, trianglesList, nativeSpline, nativeSplineLeft, nativeSplineRight, localOffset);

                if (closeStart || closeEnd)
                    SplineMeshClosingExecute(splineEdges, partLength, tStart, tEnd,
                        widthRange, widthStart, widthEnd, invertWidthRange,
                        vertexStructs, trianglesList, nativeSpline,
                        splineLeftRight, nativeSplineLeft, nativeSplineRight,
                        closeStart, closeEnd, closeVertical, localOffset);
            }
        }

        private static void SplineMeshExecute(NativeArray<SplineEdge> splineEdges,
            float partWidth, float partLength, int partResolution, SplineLengthUV splineLengthUV,
            float tStart, float tEnd, bool uvToWidth, SplineLeftRight splineLeftRight,
            Vector2 widthRange, float widthStart, float widthEnd, bool invertWidthRange,
            float2 flattenRange, float flattenHeight, float2 rangedOffsetRange, float3 rangedOffset,
            NativeList<VertexStruct01> vertexStructs, NativeList<int> trianglesList,
            NativeSpline nativeSpline, NativeSpline nativeSplineLeft, NativeSpline nativeSplineRight,
            float3 localOffset)
        {
            const float MaxSplineLength = 10000f;
            var isWidthRange = invertWidthRange || ((widthRange.x > 0f || widthRange.y > 0f) &&
                                                    (!Mathf.Approximately(widthStart, 1f) || !Mathf.Approximately(widthEnd, 1f)));
            var isFlattenRange = flattenRange.x > 0f || flattenRange.y > 0f;
            var isOffsetRange = rangedOffsetRange.x > 0f || rangedOffsetRange.y > 0f;
            var isLocalOffset = localOffset.x != 0f || localOffset.y != 0f || localOffset.z != 0f;

            var splineLength = nativeSpline.GetLength();
            if (float.IsNaN(splineLength)) return;
            if (splineLength <= 0f) return;
            if (splineLength > MaxSplineLength) return;

            if (splineLengthUV == SplineLengthUV.Stretch)
            {
                var adjustedSplineLength = splineLength * (tEnd - tStart);
                var numberOfParts = math.round(adjustedSplineLength / partLength);
                if (numberOfParts == 0) numberOfParts = 1;
                partLength = adjustedSplineLength / numberOfParts;
            }

            var uvCenter = 0f;
            if (uvToWidth)
            {
                var uvXmin = float.MaxValue;
                var uvXmax = float.MinValue;
                for (var i = 0; i < splineEdges.Length; i++)
                {
                    var splineEdge = splineEdges[i];
                    uvXmin = math.min(uvXmin, splineEdge.uvX);
                    uvXmax = math.max(uvXmax, splineEdge.uvX);
                }

                uvCenter = (uvXmin + uvXmax) * 0.5f;
            }

            var segmentLength = partLength / partResolution;
            var distancesAmount = math.floor(splineLength / segmentLength);
            if (distancesAmount == 0) distancesAmount = 1;

            var segmentLengthT = segmentLength / splineLength;
            var resCount = 0;

            var partLengthT = partLength / splineLength; // uv offset if _tStart is not 0.
            var startingT = tStart % partLengthT;
            var uvOffset = startingT / partLengthT;

            for (var i = 0; i < distancesAmount + 2; i++) // +2 to make sure last is added.
            {
                var t = i * segmentLengthT + tStart;
                var uvValueY = (float) resCount / partResolution + uvOffset;
                var last = t >= tEnd;
                if (last)
                {
                    var tCalcPart = tEnd - ((i - 1) * segmentLengthT + tStart);
                    var tCalcRatio = tCalcPart / segmentLengthT;

                    var lastUvValue = (float) (resCount - 1) / partResolution + uvOffset;
                    var uvValuePart = uvValueY - lastUvValue;

                    uvValueY = uvValuePart * tCalcRatio + lastUvValue;
                    t = tEnd;
                }

                AddEdgeLoop(t, uvValueY);
                if (last) break;
            }


            // Triangles
            var verticesPerRing = splineEdges.Length;
            var totalRings = vertexStructs.Length / verticesPerRing;

            for (var i = 0; i < totalRings - 1; i++)
            for (var j = 0; j < verticesPerRing - 1; j++)
            {
                if (splineEdges[j].edge) continue;

                var v0 = i * verticesPerRing + j;
                var v1 = v0 + 1;
                var v2 = v0 + verticesPerRing;
                var v3 = v2 + 1;

                trianglesList.Add(v0);
                trianglesList.Add(v2);
                trianglesList.Add(v1);

                trianglesList.Add(v2);
                trianglesList.Add(v3);
                trianglesList.Add(v1);
            }

            return;

            void AddEdgeLoop(float _t, float uvValueY)
            {
                nativeSpline.Evaluate(_t, out var position, out var tangent, out var upVector);
                upVector = math.up(); // Needed for correct local offset

                var left = math.cross(upVector, tangent) * -1f;
                left = math.normalizesafe(new float3(left.x, 0f, left.z));

                float3 positionLeft;
                float3 positionRight;

                if (splineLeftRight == SplineLeftRight.Create)
                {
                    // Better would be GetNearestPoint with ray, but gives incorrect results.
                    var positionLeftTemp = position + left * partWidth * 0.5f;
                    var positionRightTemp = position - left * partWidth * 0.5f;
                    SplineUtility.GetNearestPoint(nativeSplineLeft, positionLeftTemp, out positionLeft, out _);
                    SplineUtility.GetNearestPoint(nativeSplineRight, positionRightTemp, out positionRight, out _);
                }
                else if (splineLeftRight == SplineLeftRight.Custom)
                {
                    positionLeft = nativeSplineLeft.EvaluatePosition(_t);
                    positionRight = nativeSplineRight.EvaluatePosition(_t);
                    position = (positionLeft + positionRight) * 0.5f;
                }
                else
                {
                    positionLeft = position + left * partWidth * 0.5f;
                    positionRight = position - left * partWidth * 0.5f;
                }

                if (isWidthRange)
                {
                    var currentWidthT = 1f;
                    if (_t < widthRange.x)
                    {
                        var preRangeT = _t / widthRange.x;
                        if (!invertWidthRange) currentWidthT = math.lerp(1f, widthStart, preRangeT);
                        else currentWidthT = math.lerp(widthStart, 1f, preRangeT);
                    }
                    else if (_t > widthRange.y)
                    {
                        var postRangeT = (_t - widthRange.y) / (1f - widthRange.y);
                        if (!invertWidthRange) currentWidthT = math.lerp(widthEnd, 1f, postRangeT);
                        else currentWidthT = math.lerp(1f, widthEnd, postRangeT);
                    }
                    else if (!invertWidthRange)
                    {
                        var remappedT = (_t - widthRange.x) / (widthRange.y - widthRange.x);
                        currentWidthT = math.lerp(widthStart, widthEnd, remappedT);
                    }

                    positionLeft = math.lerp(positionLeft, position, 1f - currentWidthT);
                    positionRight = math.lerp(positionRight, position, 1f - currentWidthT);
                }


                ProcessSplineEdge(uvValueY);

                if (resCount == partResolution) // Need to add additional edge for each part, so the UVs are seperated
                    ProcessSplineEdge(uvOffset);

                resCount++;
                if (resCount > partResolution) resCount = 1;

                return;

                void ProcessSplineEdge(float _uvValueY)
                {
                    for (var i = 0; i < splineEdges.Length; i++)
                    {
                        var splineEdgePositionX = splineEdges[i].position.x;

                        var posX = left * splineEdgePositionX;
                        var posY = splineEdges[i].position.y;

                        if (isFlattenRange)
                        {
                            if (_t < flattenRange.x) posY = math.lerp(posY, flattenHeight, _t / flattenRange.x);
                            else if (_t > flattenRange.y) posY = math.lerp(posY, flattenHeight, (1f - _t) / (1f - flattenRange.y));
                            else posY = flattenHeight;
                        }

                        var positionSide = splineEdges[i].position.x > 0 ? positionRight : positionLeft;
                        var centerDistance = math.distance(position + posX, position) / (partWidth * 0.5f);
                        var point = math.lerp(position, positionSide, centerDistance);

                        if (splineLeftRight == SplineLeftRight.Create) point.y = position.y; // Default side splines have wrong heights in curves.
                        point += new float3(0f, posY, 0f);

                        if (isOffsetRange)
                        {
                            var _tRange = 1f;
                            if (_t < rangedOffsetRange.x) _tRange = _t / rangedOffsetRange.x;
                            else if (_t > rangedOffsetRange.y) _tRange = 1f - (_t - rangedOffsetRange.y) / (1f - rangedOffsetRange.y);
                            var _rangedOffset = math.lerp(float3.zero, rangedOffset, _tRange);
                            point -= left * _rangedOffset.x;
                            point += upVector * _rangedOffset.y;
                            point += tangent * _rangedOffset.z;
                        }

                        if (isLocalOffset)
                        {
                            point -= left * localOffset.x;
                            point += upVector * localOffset.y;
                            point += tangent * localOffset.z;
                        }

                        var _uvValueX = splineEdges[i].uvX;
                        if (uvToWidth)
                        {
                            var currentWidth = math.distance(positionLeft, positionRight);
                            var currentWidthT = currentWidth / partWidth;
                            _uvValueX = math.lerp(uvCenter, splineEdges[i].uvX, currentWidthT);
                        }

                        var uv = new float2(_uvValueX, _uvValueY);

                        var normal = RotateAroundTangent(upVector, tangent, splineEdges[i].normalRotation);
                        AddVertexStruct(point, normal, uv);
                    }
                }
            }

            void AddVertexStruct(float3 vertex, float3 normal, float2 uv)
            {
                vertexStructs.Add(new VertexStruct01
                {
                    vertex = vertex, normal = normal, uv = uv
                });
            }

            float3 RotateAroundTangent(float3 _upVector, float3 _tangent, float _degrees)
            {
                _degrees *= -1f; // Unity spline tangents go backward direction.
                _upVector = math.normalizesafe(_upVector);
                _tangent = math.normalizesafe(_tangent);
                var radians = math.radians(_degrees);
                var rotation = quaternion.AxisAngle(_tangent, radians);
                var rotatedVector = math.mul(rotation, _upVector);
                return rotatedVector;
            }
        }

        private static void SplineMeshClosingExecute(NativeArray<SplineEdge> splineEdges, float partLength, float tStart, float tEnd,
            Vector2 widthRange, float widthStart, float widthEnd, bool invertWidthRange,
            NativeList<VertexStruct01> vertexStructs, NativeList<int> trianglesList, NativeSpline nativeSpline,
            SplineLeftRight splineLeftRight, NativeSpline nativeSplineLeft, NativeSpline nativeSplineRight,
            bool closeStart, bool closeEnd, bool closeVertical, float3 localOffset)
        {
            var splineLength = nativeSpline.GetLength();
            if (float.IsNaN(splineLength)) return;

            const float Tolerance = 0.0001f;
            var isLocalOffset = localOffset.x != 0f || localOffset.y != 0f || localOffset.z != 0f;
            var topHeight = float.MinValue;
            var bottomHeight = float.MaxValue;
            if (closeVertical)
                for (var i = 0; i < splineEdges.Length; i++)
                {
                    bottomHeight = math.min(bottomHeight, splineEdges[i].position.y);
                    topHeight = math.max(topHeight, splineEdges[i].position.y);
                }

            if (Mathf.Approximately(topHeight, bottomHeight) && topHeight > 0f) bottomHeight = 0f;

            var isWidthRangeStart = !Mathf.Approximately(widthStart, 1f) &&
                                    ((!invertWidthRange && Mathf.Approximately(widthRange.x, 0f)) ||
                                     (invertWidthRange && widthRange.x > 0f));

            var isWidthRangeEnd = !Mathf.Approximately(widthEnd, 1f) &&
                                  ((!invertWidthRange && Mathf.Approximately(widthRange.y, 1f)) ||
                                   (invertWidthRange && widthRange.y < 1f));

            if (closeVertical)
            {
                if (closeStart) CloseVertical(true);
                if (closeEnd) CloseVertical(false);
            }
            else
            {
                if (closeStart) CloseHorizontal(true);
                if (closeEnd) CloseHorizontal(false);
            }

            return;

            void CloseVertical(bool isStart)
            {
                for (var i = 0; i < splineEdges.Length - 1; i++)
                {
                    var splineEdgeTop01 = splineEdges[i];
                    var splineEdgeTop02 = splineEdges[i + 1];
                    if (splineEdgeTop01.edge) continue;

                    if (Math.Abs(splineEdgeTop01.position.y - bottomHeight) < Tolerance &&
                        Math.Abs(splineEdgeTop02.position.y - bottomHeight) < Tolerance) continue;

                    EvaluateSpline(isStart, out var position, out var tangent, out var upVector, out var right);

                    var point01 = position + right * splineEdgeTop01.position.x + upVector * splineEdgeTop01.position.y;
                    var point02 = position + right * splineEdgeTop02.position.x + upVector * splineEdgeTop02.position.y;
                    var point03 = position + right * splineEdgeTop01.position.x + upVector * bottomHeight;
                    var point04 = position + right * splineEdgeTop02.position.x + upVector * bottomHeight;

                    var maxHeight = math.max(splineEdgeTop01.position.y, splineEdgeTop02.position.y);
                    var distanceTotal = math.distance(maxHeight, bottomHeight);
                    var distance = distanceTotal / partLength;

                    var uv01 = new float2(splineEdgeTop01.uvX, distance);
                    var uv02 = new float2(splineEdgeTop02.uvX, distance);
                    var uv03 = new float2(splineEdgeTop01.uvX, 0f);
                    var uv04 = new float2(splineEdgeTop02.uvX, 0f);

                    AddPoints(isStart, point01, point02, point03, point04, uv01, uv02, uv03, uv04, tangent);
                }
            }

            void CloseHorizontal(bool isStart)
            {
                var usedEdges = new NativeHashSet<int>(splineEdges.Length, Allocator.Temp);

                for (var i = 0; i < splineEdges.Length - 3; i++)
                {
                    if (usedEdges.Contains(i)) continue;
                    if (usedEdges.Contains(i + 1)) continue;

                    var splineEdgeLeft01 = splineEdges[i];
                    var splineEdgeLeft02 = splineEdges[i + 1];
                    if (splineEdgeLeft01.edge) continue;
                    if (splineEdgeLeft02.edge) continue;

                    SplineEdge splineEdgeRight01 = default;
                    SplineEdge splineEdgeRight02 = default;
                    var rightFound = false;

                    for (var j = i + 2; j < splineEdges.Length - 1; j++)
                    {
                        splineEdgeRight01 = splineEdges[j + 1];
                        splineEdgeRight02 = splineEdges[j];
                        if (splineEdgeRight02.edge) continue;
                        if (splineEdgeRight01.position.x < splineEdgeLeft01.position.x) continue;
                        if (splineEdgeRight02.position.x < splineEdgeLeft02.position.x) continue;
                        if (Math.Abs(splineEdgeRight01.position.y - splineEdgeLeft01.position.y) > Tolerance) continue;
                        if (Math.Abs(splineEdgeRight02.position.y - splineEdgeLeft02.position.y) > Tolerance) continue;
                        usedEdges.Add(j);
                        usedEdges.Add(j + 1);
                        rightFound = true;
                        break;
                    }

                    if (!rightFound) continue;

                    EvaluateSpline(isStart, out var position, out var tangent, out var upVector, out var right);

                    var widthMultiplier = isStart ? widthStart : widthEnd;
                    var shouldApplyMultiplier = isStart ? isWidthRangeStart : isWidthRangeEnd;

                    var point01 = position + right *
                                  (shouldApplyMultiplier ? splineEdgeLeft01.position.x * widthMultiplier : splineEdgeLeft01.position.x) +
                                  upVector * splineEdgeLeft01.position.y;
                    var point02 = position + right *
                                  (shouldApplyMultiplier ? splineEdgeRight01.position.x * widthMultiplier : splineEdgeRight01.position.x) +
                                  upVector * splineEdgeRight01.position.y;
                    var point03 = position + right *
                                  (shouldApplyMultiplier ? splineEdgeLeft02.position.x * widthMultiplier : splineEdgeLeft02.position.x) +
                                  upVector * splineEdgeLeft02.position.y;
                    var point04 = position + right *
                                  (shouldApplyMultiplier ? splineEdgeRight02.position.x * widthMultiplier : splineEdgeRight02.position.x) +
                                  upVector * splineEdgeRight02.position.y;

                    var distanceTotal = math.distance(point01.x, point02.x);
                    var distance = distanceTotal / partLength;

                    var uv01 = new float2(splineEdgeLeft01.uvX, 0f);
                    var uv02 = new float2(splineEdgeLeft01.uvX, distance);
                    var uv03 = new float2(splineEdgeLeft02.uvX, 0f);
                    var uv04 = new float2(splineEdgeLeft02.uvX, distance);

                    AddPoints(!isStart, point01, point02, point03, point04, uv01, uv02, uv03, uv04, tangent);
                }

                usedEdges.Dispose();
            }

            void EvaluateSpline(bool isStart, out float3 position, out float3 tangent, out float3 upVector, out float3 right)
            {
                var _t = isStart ? tStart : tEnd;
                nativeSpline.Evaluate(_t, out position, out tangent, out upVector);

                if (splineLeftRight == SplineLeftRight.Custom)
                {
                    var positionLeft = nativeSplineLeft.EvaluatePosition(_t);
                    var positionRight = nativeSplineRight.EvaluatePosition(_t);
                    position = (positionLeft + positionRight) * 0.5f;
                }

                upVector = math.up();
                tangent.y = 0f;
                tangent = math.normalizesafe(tangent);
                right = PGTrigonometryUtility.RotateTangent90ClockwiseXZ(tangent);

                if (isLocalOffset)
                {
                    position += right * localOffset.x;
                    position += upVector * localOffset.y;
                    position += tangent * localOffset.z;
                }

                if (isStart) tangent *= -1f;
            }

            void AddPoints(bool isStart, float3 point01, float3 point02, float3 point03, float3 point04, float2 uv01, float2 uv02, float2 uv03,
                float2 uv04, float3 tangent)
            {
                AddVertexStruct(point01, tangent, uv01);
                AddVertexStruct(point02, tangent, uv02);
                AddVertexStruct(point03, tangent, uv03);
                AddVertexStruct(point04, tangent, uv04);

                var startIdx = vertexStructs.Length - 4;

                if (!isStart)
                {
                    trianglesList.Add(startIdx);
                    trianglesList.Add(startIdx + 2);
                    trianglesList.Add(startIdx + 1);
                    trianglesList.Add(startIdx + 2);
                    trianglesList.Add(startIdx + 3);
                    trianglesList.Add(startIdx + 1);
                }
                else
                {
                    trianglesList.Add(startIdx);
                    trianglesList.Add(startIdx + 1);
                    trianglesList.Add(startIdx + 2);
                    trianglesList.Add(startIdx + 2);
                    trianglesList.Add(startIdx + 1);
                    trianglesList.Add(startIdx + 3);
                }
            }

            void AddVertexStruct(float3 vertex, float3 normal, float2 uv)
            {
                vertexStructs.Add(new VertexStruct01
                {
                    vertex = vertex, normal = normal, uv = uv
                });
            }
        }
    }

    public static class SplineEdgeUtility
    {
        public static SplineEdge[] CreateLine(float width, bool backface)
        {
            var halfWidth = width * 0.5f;
            var splineEdges = new SplineEdge[backface ? 4 : 2];
            splineEdges[0] = new SplineEdge(new float2(-halfWidth, 0f), 0f, 0f);
            splineEdges[1] = new SplineEdge(new float2(halfWidth, 0f), 1f, 0f, true);
            if (!backface) return splineEdges;
            splineEdges[2] = new SplineEdge(new float2(halfWidth, 0f), 0f, 180f);
            splineEdges[3] = new SplineEdge(new float2(-halfWidth, 0f), 1f, 180f);
            return splineEdges;
        }

        public static SplineEdge[] CreateRectangle(float width, float height, bool bevel)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            var splineEdges = new SplineEdge[bevel ? 9 : 5];

            if (!bevel)
            {
                splineEdges[0] = new SplineEdge(new float2(-halfWidth, -halfHeight), 0f, -135f);
                splineEdges[1] = new SplineEdge(new float2(-halfWidth, halfHeight), 0.25f, -45f);
                splineEdges[2] = new SplineEdge(new float2(halfWidth, halfHeight), 0.5f, 45f);
                splineEdges[3] = new SplineEdge(new float2(halfWidth, -halfHeight), 0.75f, 135f);
                splineEdges[4] = new SplineEdge(new float2(-halfWidth, -halfHeight), 1f, -135f);
            }
            else
            {
                splineEdges[0] = new SplineEdge(new float2(-halfWidth, -halfHeight), 0f, -90f);
                splineEdges[1] = new SplineEdge(new float2(-halfWidth, halfHeight), 0.125f, -90f);
                splineEdges[2] = new SplineEdge(new float2(-halfWidth, halfHeight), 0.25f, 0f);
                splineEdges[3] = new SplineEdge(new float2(halfWidth, halfHeight), 0.375f, 0f);
                splineEdges[4] = new SplineEdge(new float2(halfWidth, halfHeight), 0.5f, 90f);
                splineEdges[5] = new SplineEdge(new float2(halfWidth, -halfHeight), 0.625f, 90f);
                splineEdges[6] = new SplineEdge(new float2(halfWidth, -halfHeight), 0.75f, 180f);
                splineEdges[7] = new SplineEdge(new float2(-halfWidth, -halfHeight), 0.875f, 180f);
                splineEdges[8] = new SplineEdge(new float2(-halfWidth, -halfHeight), 1f, -90f);
            }

            return splineEdges;
        }

        public static SplineEdge[] CreateCircle(float radius, int faceCount)
        {
            if (faceCount <= 1) return CreateLine(radius * 2f, false);
            if (faceCount == 2) return CreateLine(radius * 2f, true);

            var edges = new SplineEdge[faceCount + 1];
            var angleStep = -2 * math.PI / faceCount;
            for (var i = 0; i < faceCount; i++)
            {
                var angle = i * angleStep + math.PI / 2f;
                var x = radius * math.cos(angle);
                var y = radius * math.sin(angle);
                var uvX = (float) i / faceCount;
                var normalRotation = i * (360f / faceCount);
                edges[i] = new SplineEdge(new float2(x, y), uvX, normalRotation);
            }

            edges[faceCount] = edges[0];
            edges[faceCount].uvX = 1f;
            return edges;
        }

        public static SplineEdge[] CopyAndOffsetSplineEdges(SplineEdge[] splineEdges, Vector2 offset)
        {
            var splineEdgesCopy = new SplineEdge[splineEdges.Length];
            for (var i = 0; i < splineEdges.Length; i++)
            {
                var splineEdge = splineEdges[i];
                splineEdge.position += (float2) offset;
                splineEdgesCopy[i] = splineEdge;
            }

            return splineEdgesCopy;
        }

        public static SplineEdge[] CopyAndWidthSplineEdges(SplineEdge[] splineEdges, float width)
        {
            var splineEdgesCopy = new SplineEdge[splineEdges.Length];
            if (splineEdgesCopy.Length < 2) return splineEdgesCopy;

            var leftX = -width * 0.5f;
            var rightX = width * 0.5f;
            var originalLeft = splineEdges[0].position.x;
            var originalRight = splineEdges[^1].position.x;
            if (originalRight < originalLeft) (originalLeft, originalRight) = (originalRight, originalLeft);
            var originalWidth = originalRight - originalLeft;

            for (var i = 0; i < splineEdges.Length; i++)
            {
                var splineEdge = new SplineEdge(splineEdges[i].position, splineEdges[i].uvX, splineEdges[i].normalRotation, splineEdges[i].edge);
                var t = (splineEdge.position.x - originalLeft) / originalWidth;
                var newPosition = splineEdge.position;
                newPosition.x = math.lerp(leftX, rightX, t);
                splineEdge.position = newPosition;
                splineEdgesCopy[i] = splineEdge;
            }

            return splineEdgesCopy;
        }
    }
}