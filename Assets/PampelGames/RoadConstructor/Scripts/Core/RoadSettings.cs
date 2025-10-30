// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.RoadConstructor
{
    /// <summary>
    ///     Settings that can be dynamically applied using the <see cref="RoadConstructor" /> construction methods.
    /// </summary>
    public class RoadSettings
    {
        /// <summary>
        ///     Fixes the outgoing tangent of position01, forcing curvature.
        /// </summary>
        public bool setTangent01;

        /// <summary>
        ///     If <see cref="setTangent01" /> is true, tangent to use for position01.
        /// </summary>
        public Vector3 tangent01;

        /// <summary>
        ///     Fixes the outgoing tangent of position02, forcing curvature.
        /// </summary>
        public bool setTangent02;

        /// <summary>
        ///     If <see cref="setTangent02" /> is true, tangent to use for position02.
        /// </summary>
        public Vector3 tangent02;

        /// <summary>
        ///     Tries to find a suitable road and creates the new road parallel to it.
        ///     Applies only if both the start and end overlap with the same road.
        /// </summary>
        public bool parallelRoad;

        /// <summary>
        ///     The distance between the newly created road and its parallel counterpart
        ///     when <see cref="parallelRoad" /> is enabled.
        /// </summary>
        public float parallelDistance;

        /// <summary>
        ///     Can be used if an overlapping scene object for position01 is already detected.
        /// </summary>
        public Overlap overlap01 = new();

        /// <summary>
        ///     Can be used if an overlapping scene object for position02 is already detected.
        /// </summary>
        public Overlap overlap02 = new();
    }
}