// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    /// <summary>
    ///     Represents the base class for all scene objects within the Pampel Games construction system.
    /// </summary>
    public abstract class SceneObjectBase : MonoBehaviour
    {
        public string iD;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;
        public List<MeshFilter> meshFilterLODs = new();
        public List<MeshRenderer> meshRendererLODs = new();


        /********************************************************************************************************************************/
        // Abstract
        /********************************************************************************************************************************/

        /// <summary>
        ///     Calculates the closest valid snap position for a given point in the scene.
        /// </summary>
        public abstract Vector3 SnapPosition(Vector3 position);
        
        /// <summary>
        ///     Aligns a new track to the scene object.
        ///     Position01 and tangent01 always refer to the nearest knots.
        /// </summary>
        /// <param name="width">Total width of the new track.</param>
        /// <param name="directConnection">The direct connection setting is set to true and the new track overlaps on both sides.</param>
        public abstract void AlignTrack(float width, ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02, bool directConnection);

        /// <summary>
        ///     Validates if a new track can be added to this scene object.
        /// </summary>
        /// <param name="spline">New track spline requesting connection.</param>
        public abstract List<ConstructionFail> ValidateNewConnection(Spline spline);
        
        /// <summary>
        ///     Creates a mesh for the scene object based on its connections and the specified level of detail (LOD).
        /// </summary>
        public abstract Mesh CreateMeshFromConnections(float lodAmount);
    }
}