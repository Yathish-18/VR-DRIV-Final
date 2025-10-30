// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PampelGames.Shared.Construction
{
    public static class Export
    {
        public static void ExportMeshes(GameObject constructionParent, bool checkExistingMeshes)
        {
            var meshFilters = new List<MeshFilter>();
            GetAllMeshFilters(constructionParent, meshFilters);
            if(checkExistingMeshes) RemoveExistingMeshes(meshFilters);

            var folderPath = EditorUtility.OpenFolderPanel("Export Meshes", "Assets", "");
    
            // User clicked "Cancel".
            if (string.IsNullOrEmpty(folderPath)) return;
    
            var assetPath = "Assets" + folderPath.Substring(Application.dataPath.Length);

            for (var i = 0; i < meshFilters.Count; i++)
            {
                var newMesh = Object.Instantiate(meshFilters[i].sharedMesh);
                newMesh.name = meshFilters[i].name;
                string meshPath = assetPath + "/" + newMesh.name + ".asset";
        
                AssetDatabase.CreateAsset(newMesh, meshPath);
                meshFilters[i].sharedMesh = newMesh;
            }
    
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Exported {meshFilters.Count} meshes to '{assetPath}'");
        }
        
        private static void GetAllMeshFilters(GameObject parent, List<MeshFilter> meshFilterList)
        {
            foreach (Transform child in parent.transform)
            {
                MeshFilter mf = child.GetComponent<MeshFilter>();
                if (mf != null)
                {
                    meshFilterList.Add(mf);
                }

                GetAllMeshFilters(child.gameObject, meshFilterList);
            }
        }

        private static void RemoveExistingMeshes(List<MeshFilter> meshFilters)
        {
            for (var i = meshFilters.Count - 1; i >= 0; i--)
            {
                var meshFilter = meshFilters[i];
                var mesh = meshFilter.sharedMesh;

                if (mesh == null)
                {
                    meshFilters.RemoveAt(i);
                    continue;
                }
 
                string path = AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(path))
                {
                    meshFilters.RemoveAt(i);
                }
            }
        }
    }
}

#endif