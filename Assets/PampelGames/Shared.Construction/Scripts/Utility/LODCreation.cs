// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using UnityEngine;
using UnityEngine.Rendering;

namespace PampelGames.Shared.Construction
{
    public static class LODCreation
    {
        public static void CreateLODs(SceneObjectBase sceneObject, List<float> lodList, ShadowCastingMode shadowCastingMode)
        {
            var gameObject = sceneObject.gameObject;
            if (gameObject.TryGetComponent<LODGroup>(out _)) return;

            ObjectUtility.CreateLODObj(shadowCastingMode, gameObject, 0,
                out var meshFilter, out var meshRenderer);
            
            meshFilter.sharedMesh = sceneObject.meshFilter.sharedMesh;
            meshRenderer.sharedMaterials = sceneObject.meshRenderer.sharedMaterials;
            ObjectUtility.DestroyObject(sceneObject.meshFilter);
            ObjectUtility.DestroyObject(sceneObject.meshRenderer);
            sceneObject.meshFilter = meshFilter;
            sceneObject.meshRenderer = meshRenderer;
            sceneObject.meshFilterLODs.Add(meshFilter);
            sceneObject.meshRendererLODs.Add(meshRenderer);
            
            var intersectionLodGroup = gameObject.AddComponent<LODGroup>();
            var intersectionLOD = new LOD[lodList.Count + 1];
            intersectionLOD[0] = new LOD(1f - lodList[0], new Renderer[] {meshRenderer});
            
            for (var i = 1; i < lodList.Count; i++)
            {
                var lodAmount = 1f - lodList[i - 1];
                
                var mesh = sceneObject.CreateMeshFromConnections(lodAmount);
                
                ObjectUtility.CreateLODObj(shadowCastingMode, gameObject, i,
                    out var _meshFilter, out var _meshRenderer);

                _meshFilter.sharedMesh = mesh;
                _meshRenderer.sharedMaterials = sceneObject.meshRenderer.sharedMaterials;
                intersectionLOD[i] = new LOD(1f - lodList[i], new Renderer[] {_meshRenderer});
                sceneObject.meshFilterLODs.Add(_meshFilter);
                sceneObject.meshRendererLODs.Add(_meshRenderer);
            }

            intersectionLodGroup.SetLODs(intersectionLOD);
        }
    }
}