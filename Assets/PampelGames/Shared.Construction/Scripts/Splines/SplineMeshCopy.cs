// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using PampelGames.Shared.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Random = Unity.Mathematics.Random;

namespace PampelGames.Shared.Construction
{
    public class SplineMeshCopyParameter
    {
        public readonly Spline spline;
        public readonly float spacing;
        public readonly SplineMeshCopyTemplate meshTemplate;
        public readonly bool stretch;
        public readonly int randomSeed;
        public readonly bool randomFlip;
        public readonly int weightCount;
        public readonly int weightIndex;

        public SplineMeshCopyParameter(Spline spline, float spacing, SplineMeshCopyTemplate meshTemplate,
            bool stretch = true, int randomSeed = 0, bool randomFlip = true, int weightCount = 1, int weightIndex = 0)
        {
            this.spline = spline;
            this.spacing = spacing;
            this.meshTemplate = meshTemplate;
            this.stretch = stretch;
            this.randomSeed = randomSeed;
            this.randomFlip = randomFlip;
            this.weightCount = weightCount;
            this.weightIndex = weightIndex;
        }
    }

    public class SplineMeshCopyTemplate
    {
        public readonly Vector3[] vertices;
        public readonly Vector3[] normals;
        public readonly Vector2[] uvs;
        public readonly int[] triangles;
        public readonly Bounds bounds;

        public SplineMeshCopyTemplate(Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] triangles, Bounds bounds)
        {
            this.vertices = vertices;
            this.normals = normals;
            this.uvs = uvs;
            this.triangles = triangles;
            this.bounds = bounds;
        }

        public SplineMeshCopyTemplate(Mesh mesh)
        {
            vertices = mesh.vertices;
            normals = mesh.normals;
            uvs = mesh.uv;
            triangles = mesh.triangles;
            bounds = mesh.bounds;
        }
    }

    public struct SplineMeshCopyTemplateNative
    {
        public NativeArray<Vector3> verticesTemplate;
        public NativeArray<Vector3> normalsTemplate;
        public NativeArray<Vector2> uvsTemplate;
        public NativeArray<int> trianglesTemplate;
        public Bounds bounds;

        public SplineMeshCopyTemplateNative(SplineMeshCopyTemplate meshTemplate)
        {
            verticesTemplate = new NativeArray<Vector3>(meshTemplate.vertices, Allocator.TempJob);
            normalsTemplate = new NativeArray<Vector3>(meshTemplate.normals, Allocator.TempJob);
            uvsTemplate = new NativeArray<Vector2>(meshTemplate.uvs, Allocator.TempJob);
            trianglesTemplate = new NativeArray<int>(meshTemplate.triangles, Allocator.TempJob);
            bounds = meshTemplate.bounds;
        }

        public void Dispose()
        {
            verticesTemplate.Dispose();
            normalsTemplate.Dispose();
            uvsTemplate.Dispose();
            trianglesTemplate.Dispose();
        }
    }

    public static class SplineMeshCopy
    {
        public static void CreateSplineMeshCopy(Mesh mesh, SplineMeshCopyParameter parameter,
            float tStart = 0f, float tEnd = 1f, Vector3 localOffset = default,
            Vector3 sizeStart = default, Vector3 sizeEnd = default, float sizePower = 1f)
        {
            mesh.Clear();

            var spline = parameter.spline;
            var spacing = parameter.spacing;

            GetCountAndSpacingT(parameter.stretch, spline, spacing, out var totalCount, out var spacingT);

            if (totalCount < 1) return;

            var splineNative = new NativeSpline(spline, Allocator.TempJob);
            var meshTemplate = new SplineMeshCopyTemplateNative(parameter.meshTemplate);
            var vertexStructs = new NativeList<VertexStruct01>(Allocator.TempJob);
            var triangles = new NativeList<int>(Allocator.TempJob);
            var copyCount = new NativeReference<int>(0, Allocator.TempJob);

            CreateSplineMeshCopyJob(totalCount, spacingT, splineNative, meshTemplate, vertexStructs, triangles, copyCount,
                tStart, tEnd, localOffset,
                sizeStart, sizeEnd, sizePower,
                parameter.randomSeed, parameter.randomFlip, parameter.weightCount, parameter.weightIndex);

            PGMeshAPIUtility.SetBufferData01(mesh, vertexStructs.AsArray(), triangles.AsArray());
            PGMeshAPIUtility.SetSubMesh(mesh, 0, vertexStructs.Length, triangles.Length);
            PGMeshUtility.RecalculateMeshData(mesh, false);

            meshTemplate.Dispose();
            splineNative.Dispose();
            vertexStructs.Dispose();
            triangles.Dispose();
            copyCount.Dispose();
        }

        public static void GetCountAndSpacingT(bool stretch, ISpline spline, float spacing, out int totalCount, out float spacingT)
        {
            spacing = math.max(spacing, 0.01f);
            totalCount = 0;
            spacingT = 0f;

            var splineLength = spline.GetLength();
            if (float.IsNaN(splineLength)) return;
            if (splineLength <= 0) return;

            totalCount = Mathf.FloorToInt(splineLength / spacing);
            if (totalCount < 1) return;

            if (stretch)
            {
                if (totalCount == 1) spacingT = 1f;
                else spacingT = 1f / (totalCount - 1);
            }
            else
            {
                spacingT = spacing / splineLength;
            }
        }

        public static void CreateSplineMeshCopyJob(int totalCount, float spacingT, NativeSpline spline, SplineMeshCopyTemplateNative meshTemplate,
            NativeList<VertexStruct01> vertexStructs, NativeList<int> triangles, NativeReference<int> copyCount,
            float tStart = 0f, float tEnd = 1f, Vector3 localOffset = default,
            Vector3 sizeStart = default, Vector3 sizeEnd = default, float sizePower = 1f,
            int randomSeed = 0, bool randomFlip = true, int weightCount = 1, int weightIndex = 0)
        {
            var job = new SplineMeshCopyJob
            {
                totalCount = totalCount,
                spacingT = spacingT,
                spline = spline,
                meshTemplate = meshTemplate,
                vertexStructs = vertexStructs,
                triangles = triangles,
                tStart = tStart,
                tEnd = tEnd,
                sizeStart = sizeStart == default ? new float3(1f) : sizeStart,
                sizeEnd = sizeEnd == default ? new float3(1f) : sizeEnd,
                sizePower = sizePower,
                randomSeed = randomSeed,
                randomFlip = randomFlip,
                weightCount = weightCount,
                weightIndex = weightIndex,
                copyCount = copyCount,
                localOffset = localOffset == default ? new float3(0f) : localOffset
            };

            var handle = job.Schedule();
            handle.Complete();
        }


        [BurstCompile]
        private struct SplineMeshCopyJob : IJob
        {
            public int totalCount;
            public float spacingT;
            public NativeSpline spline;
            public SplineMeshCopyTemplateNative meshTemplate;
            public NativeList<VertexStruct01> vertexStructs;
            public NativeList<int> triangles;
            public NativeReference<int> copyCount; // Used if multiple jobs are executed consecutively

            public float tStart;
            public float tEnd;
            public float3 sizeStart;
            public float3 sizeEnd;
            public float sizePower;
            public int randomSeed;
            public bool randomFlip;
            public int weightCount;
            public int weightIndex;

            public float3 localOffset;

            public void Execute()
            {
                var verticesTemplate = meshTemplate.verticesTemplate;
                var normalsTemplate = meshTemplate.normalsTemplate;
                var uvsTemplate = meshTemplate.uvsTemplate;
                var trianglesTemplate = meshTemplate.trianglesTemplate;

                var isRandomWeight = weightCount > 1;

                var applySize = !Mathf.Approximately(math.length(sizeStart - new float3(1f)), 0f) ||
                                !Mathf.Approximately(math.length(sizeEnd - new float3(1f)), 0f);

                var currentSize = new float3(1f);

                var tOffset = spacingT * 0.5f + tStart;

                for (var count = 0; count < totalCount; count++)
                {
                    Random random01 = default;
                    Random random02 = default;
                    if (isRandomWeight || randomFlip) random01 = Random.CreateFromIndex((uint) (randomSeed + count));
                    if (randomFlip) random02 = Random.CreateFromIndex((uint) (randomSeed + count + 1));

                    if (isRandomWeight)
                    {
                        var randomInt = random01.NextInt(0, weightCount);
                        if (randomInt != weightIndex) continue;
                    }

                    var t = tOffset + count * spacingT;

                    if (t > tEnd) break;

                    spline.Evaluate(t, out var position, out var tangent, out var upVector);
                    tangent = math.normalizesafe(tangent);
                    upVector = math.up();

                    var right = math.normalizesafe(math.cross(upVector, tangent));

                    if (applySize)
                    {
                        var exponentialT = math.pow(t, sizePower);
                        currentSize = math.lerp(sizeStart, sizeEnd, exponentialT);
                    }

                    var isFlipped = randomFlip && random01.NextBool();
                    var isReversed = randomFlip && random02.NextBool();

                    if (isReversed)
                    {
                        right = -right;
                        tangent = -tangent;
                    }

                    var rotationMatrix = new float3x3(right, upVector, tangent);

                    for (var i = 0; i < verticesTemplate.Length; i++)
                    {
                        var vertex = (float3) verticesTemplate[i];
                        var normal = (float3) normalsTemplate[i];
                        var uv = (float2) uvsTemplate[i];

                        if (applySize)
                        {
                            vertex.x *= currentSize.x;
                            vertex.y *= currentSize.y;
                            vertex.z *= currentSize.z;
                        }

                        if (isFlipped)
                        {
                            vertex.z = -vertex.z;
                            normal.z = -normal.z;
                        }

                        vertex = math.mul(rotationMatrix, vertex + localOffset) + position;
                        normal = math.mul(rotationMatrix, normal);

                        vertexStructs.Add(new VertexStruct01
                        {
                            vertex = vertex,
                            normal = normal,
                            uv = uv
                        });
                    }

                    // Triangles
                    var vertexStartIndex = copyCount.Value * verticesTemplate.Length;
                    if (isFlipped)
                        for (var i = 0; i < trianglesTemplate.Length; i += 3)
                        {
                            triangles.Add(trianglesTemplate[i] + vertexStartIndex);
                            triangles.Add(trianglesTemplate[i + 2] + vertexStartIndex);
                            triangles.Add(trianglesTemplate[i + 1] + vertexStartIndex);
                        }
                    else
                        for (var i = 0; i < trianglesTemplate.Length; i++)
                            triangles.Add(trianglesTemplate[i] + vertexStartIndex);

                    copyCount.Value++;
                }
            }
        }
    }
}