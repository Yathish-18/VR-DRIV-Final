// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    internal static class RoadSplineMesh
    {
        public static Mesh CreateCombinedSplineMesh(List<Lane> lanes, SplineMeshParameter splineMeshParameter,
            out Material[] _materials)
        {
            CreateMultipleSplineMeshes(lanes, splineMeshParameter,
                out var _meshes, out var _combinedMaterials);
            
            PGMeshUtility.CombineAndPackMeshes(_combinedMaterials, _meshes, out var combinedMaterials, out var combinedMesh);

            _materials = combinedMaterials.ToArray();
            return combinedMesh;
        }

        public static void CreateMultipleSplineMeshes(List<Lane> lanes, SplineMeshParameter splineMeshParameter,
            out List<Mesh> _meshes, out List<Material> _materials, 
            float tStart = 0f, float tEnd = 1f, float widthStart = 1f, float widthEnd = 1f)
        {
            splineMeshParameter.widthRange = new Vector2(0f, 1f);
            splineMeshParameter.widthStart = widthStart;
            splineMeshParameter.widthEnd = widthEnd;
            _meshes = new List<Mesh>();
            _materials = new List<Material>();
            
            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];

                var splineEdges = lane.splineEdges;
                if (splineEdges.Length == 0) continue;

                var mesh = new Mesh();
                SplineMesh.CreateSplineMesh(mesh, splineEdges, splineMeshParameter, tStart, tEnd);

                _meshes.Add(mesh);
                _materials.Add(lane.material);
            }
        }
    }
}