// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    [ExecuteAlways]
    public class TreeBending : MonoBehaviour
    {
        private void Reset()
        {
            var _renderer = GetComponentInChildren<MeshRenderer>();
            foreach (var mat in _renderer.sharedMaterials)
            {
                materials.Add(mat);
            }
            OnValidate();
        }

        public List<Material> materials = new();
        
        public float bendStrength = 0.2f;
        public Vector2 bendDirection = new(1,1);


        private void Start()
        {
            OnValidate();
        }

        public void OnValidate()
        {
            SetBendStrength(bendStrength);
            SetBendDirection(bendDirection);
            SetDirty();
        }

        /********************************************************************************************************************************/
        
        public void SetBendStrength(float value)
        {
            bendStrength = value;
            foreach (Material material in materials)
            {
                material.SetFloat("_Bend_Strength", value);
            }
        }
        
        public void SetBendDirection(Vector2 value)
        {
            bendDirection = value;
            foreach (Material material in materials)
            {
                material.SetVector("_Bend_Direction", value);
            }
        }
        
        /********************************************************************************************************************************/

        private void SetDirty()
        {
#if UNITY_EDITOR
            foreach (Material material in materials)
            {
                EditorUtility.SetDirty(material);
            }
#endif
        }
    }
}
