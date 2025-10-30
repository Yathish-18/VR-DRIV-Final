// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    public static class ObjectUtility
    {
        public const string PrefixUndo = "UndoObject_";
        public static GameObject CreateObj(string prefix, ShadowCastingMode shadowCastingMode, out MeshFilter meshFilter, out MeshRenderer meshRenderer)
        {
            var obj = new GameObject();
            obj.name = prefix + "_" + obj.GetInstanceID();
            meshFilter = obj.AddComponent<MeshFilter>();
            meshRenderer = obj.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = shadowCastingMode;
            return obj;
        }

        public static GameObject CreateLODObj(ShadowCastingMode shadowCastingMode, GameObject parent, int lod,
            out MeshFilter meshFilter, out MeshRenderer meshRenderer)
        {
            var obj = new GameObject();
            obj.transform.SetParent(parent.transform);
            obj.name = parent.name + "_LOD" + lod;
            meshFilter = obj.AddComponent<MeshFilter>();
            meshRenderer = obj.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = shadowCastingMode;
            return obj;
        }
        
        public static GameObject CreateUndoObj()
        {
            var obj = new GameObject();
            obj.name = PrefixUndo + obj.GetInstanceID();
            return obj;
        }
        
        public static void DestroyObject(Object obj, bool destroyMeshes = true)
        {
            if (destroyMeshes && obj is GameObject gameObject)
            {
                if (gameObject.TryGetComponent<SceneObjectBase>(out var sceneObject))
                {
                    var meshFilters = sceneObject.meshFilterLODs;
                    if(!meshFilters.Contains(sceneObject.meshFilter)) meshFilters.Add(sceneObject.meshFilter);
                    for (int i = 0; i < meshFilters.Count; i++)
                    {
                        DestroyObject(meshFilters[i].sharedMesh);
                    }
                }
            }
            
            if (Application.isPlaying)
                Object.Destroy(obj);
            else
                Object.DestroyImmediate(obj);
        }

#if UNITY_EDITOR
        public static SplineContainer CreateTestObject(ISpline spline, string name = "Test Spline")
        {
            var test = new GameObject(name);
            var cont = test.AddComponent<SplineContainer>();
            var testSpline = new Spline(spline);
            testSpline.SetTangentMode(TangentMode.Broken);
            cont.Spline = testSpline;
            
            var parent = GameObject.Find("TestSplineParent");
            if (parent == null) parent = new GameObject("TestSplineParent");
            test.transform.SetParent(parent.transform);
            
            return cont;
        }
        
        public static void RemoveTestObjects()
        {
            if (Application.isPlaying) Object.Destroy(GameObject.Find("TestSplineParent"));
            else Object.DestroyImmediate(GameObject.Find("TestSplineParent"));
        }
#endif
    }
}
