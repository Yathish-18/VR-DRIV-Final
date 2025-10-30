// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    /// <summary>
    ///     Base class for Pampel Games construction systems.
    /// </summary>
    public abstract class ConstructionBase : MonoBehaviour
    {
        /// <summary>
        ///     Attempts to get a <see cref="SceneObjectBase" /> from the scene within a specific search radius at a given position.
        /// </summary>
        /// <param name="position">The position to search around.</param>
        /// <param name="searchRadius">The radius of the search area.</param>
        /// <param name="sceneObject">The retrieved <see cref="SceneObjectBase" /> if found; otherwise, null.</param>
        /// <returns>True if a <see cref="SceneObjectBase" /> is found within the specified radius; otherwise, false.</returns>
        public abstract bool TryGetSceneObject(Vector3 position, float searchRadius, out SceneObjectBase sceneObject);

        /// <summary>
        /// Detects overlaps between a given track spline and constructed scene objects.
        /// </summary>
        /// <param name="trackSpline">The spline representing the track to check for overlaps.</param>
        /// <param name="trackWidth">The width of the track.</param>
        /// <param name="trackSpacing">The spacing between track points for spline evaluation.</param>
        /// <param name="ignoreObjects">Optional scene objects to ignore as overlap.</param>
        public abstract List<ConstructionFail> DetectTrackOverlap(Spline trackSpline, float trackWidth, float trackSpacing,
            List<SceneObjectBase> ignoreObjects);
    }
}