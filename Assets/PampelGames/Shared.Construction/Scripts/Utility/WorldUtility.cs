// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    [BurstCompile]
    public static class WorldUtility
    {
        public static List<int> FindOverlappingIndexes(Bounds bounds, List<Bounds> intersectionBounds)
        {
            var intersectionBoundsNative = new NativeArray<Bounds>(intersectionBounds.ToArray(), Allocator.TempJob);

            var overlappingIndexes = new NativeList<int>(Allocator.TempJob);

            var job = new FindOverlappingIndexesJob
            {
                BoundsArray = intersectionBoundsNative,
                Bounds = bounds,
                OverlappingIndexes = overlappingIndexes
            };

            job.Schedule().Complete();

            var indexList = overlappingIndexes.AsArray().ToList();
            overlappingIndexes.Dispose();
            intersectionBoundsNative.Dispose();

            return indexList;
        }

        [BurstCompile]
        private struct FindOverlappingIndexesJob : IJob
        {
            public NativeArray<Bounds> BoundsArray;
            public Bounds Bounds;
            public NativeList<int> OverlappingIndexes;

            public void Execute()
            {
                for (var i = 0; i < BoundsArray.Length; i++)
                    if (Intersects(BoundsArray[i], Bounds))
                        OverlappingIndexes.Add(i);
            }

            private bool Intersects(Bounds bounds1, Bounds bounds2)
            {
                return bounds1.min.x <= bounds2.max.x && bounds1.max.x >= bounds2.min.x &&
                       bounds1.min.z <= bounds2.max.z && bounds1.max.z >= bounds2.min.z;
            }
        }

        /********************************************************************************************************************************/

        public static RaycastHit[] RaycastBatch(float3[] splinePositions, LayerMask raycastLayers, float3 raycastOffset)
        {
            var queryParameters = new QueryParameters
            {
                layerMask = raycastLayers
            };

            var commands = new NativeArray<RaycastCommand>(splinePositions.Length, Allocator.TempJob);
            var results = new NativeArray<RaycastHit>(splinePositions.Length, Allocator.TempJob);
            for (var i = 0; i < splinePositions.Length; i++)
                commands[i] = new RaycastCommand(splinePositions[i] + raycastOffset, Vector3.down, queryParameters);

            var raycastJobHandle = RaycastCommand.ScheduleBatch(commands, results, 1);
            raycastJobHandle.Complete();
            var resultsArray = results.ToArray();
            commands.Dispose();
            results.Dispose();
            return resultsArray;
        }

        /********************************************************************************************************************************/


        public static bool CheckElevation(Spline spline, float roadLength, Vector3 raycastOffset, LayerMask groundLayers, float elevationStartHeight)
        {
            var distance = roadLength / 2f;

            SplineWorldUtility.SplineEvaluationMiddle(spline, distance, out var ts, out var positions, out var tangents);

            var raycasts = RaycastBatch(positions, groundLayers, raycastOffset);

            for (var i = 0; i < raycasts.Length; i++)
            {
                var position = positions[i];
                var hit = raycasts[i];

                if (hit.distance == 0 && hit.point == Vector3.zero) continue;
                var groundDistance = position.y - hit.point.y;

                if (groundDistance >= 0 && groundDistance > elevationStartHeight) return true;
            }

            return false;
        }

        public static bool CheckElevation(Bounds bounds, Vector3 raycastOffset, LayerMask groundLayers, float elevationStartHeight)
        {
            var positions = new Vector3[5];

            positions[0] = new Vector3(bounds.min.x, bounds.min.y, bounds.min.z);
            positions[1] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
            positions[2] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
            positions[3] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);

            for (var i = 0; i < positions.Length; i++)
            {
                var ray = new Ray(positions[i] + raycastOffset, Vector3.down);
                if (Physics.Raycast(ray, out var hit, float.MaxValue, groundLayers))
                {
                    var groundDistance = positions[i].y - hit.point.y;
                    if (groundDistance >= 0 && groundDistance > elevationStartHeight)
                        return true;
                }
            }

            return false;
        }

        /********************************************************************************************************************************/

        public static Bounds ExtendBounds(Bounds bounds, float width)
        {
            bounds.min -= new Vector3(width, width, width);
            bounds.max += new Vector3(width, width, width);
            return bounds;
        }
    }
}