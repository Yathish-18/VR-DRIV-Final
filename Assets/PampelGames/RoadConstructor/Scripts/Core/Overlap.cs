// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    public class Overlap
    {
        /// <summary>
        ///     If the overlap exists.
        /// </summary>
        public bool exists;

        /// <summary>
        ///     Type of the overlapping part.
        /// </summary>
        public OverlapType overlapType;

        /// <summary>
        ///     Position of the overlap.
        /// </summary>
        public float3 position;

        /// <summary>
        ///     Only applicable for <see cref="OverlapType" /> <see cref="IntersectionObject" />.
        /// </summary>
        public IntersectionObject intersectionObject;

        /// <summary>
        ///     Only applicable for <see cref="OverlapType" /> <see cref="RoadObject" />.
        /// </summary>
        public RoadObject roadObject;

        /// <summary>
        ///     Only applicable for <see cref="OverlapType.Shared" />.
        /// </summary>
        public ISharedObject sharedObject;

        /// <summary>
        ///     Only applicable for <see cref="OverlapType.Shared" />.
        /// </summary>
        public SceneObjectBase sharedSceneObject;

        public Spline spline;
        public float t;
        public float3 tangent;
        public float3 upVector;

        public SceneObject SceneObject
            => overlapType == OverlapType.Road ? roadObject
                : overlapType == OverlapType.Intersection ? intersectionObject
                : null;

        public SceneObjectBase SceneObjectBase
            => overlapType == OverlapType.Road ? roadObject
                : overlapType == OverlapType.Intersection ? intersectionObject
                : overlapType == OverlapType.Shared ? sharedSceneObject
                : null;

        public float3 BoundsCenter
            => overlapType == OverlapType.Road ? roadObject.meshRenderer.bounds.center
                : overlapType == OverlapType.Intersection ? intersectionObject.meshRenderer.bounds.center
                : overlapType == OverlapType.Shared ? sharedSceneObject.meshRenderer.bounds.center
                : default;

        public bool IsEndObject()
        {
            return exists && overlapType == OverlapType.Intersection && intersectionObject.IsEndObject();
        }

        public bool IsSnappedRoad()
        {
            return exists && overlapType == OverlapType.Road && roadObject.snapPositionSet;
        }
    }
}