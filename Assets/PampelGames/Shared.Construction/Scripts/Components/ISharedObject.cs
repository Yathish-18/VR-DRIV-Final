// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    /// <summary>
    ///     Represents a scene object that can be shared among Pampel Games construction systems.
    ///     For example a railway crossing.
    /// </summary>
    public interface ISharedObject
    {
        /// <summary>
        ///     The connection points used for attaching scene objects.
        ///     Can be empty.
        /// </summary>
        public List<ConnectionPoint> GetConnectionPoints();

        /// <summary>
        ///     Splines that defines the construction and traffic paths for the shared object.
        ///     Can be empty.
        /// </summary>
        public List<SplineMeshParameter> GetConstructionSplines();

        /// <summary>
        ///     Whether the object is in an elevated state.
        /// </summary>
        public bool IsElevated();
    }
}