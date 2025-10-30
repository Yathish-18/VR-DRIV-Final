// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    /// <summary>
    ///     Used for calculations before creating the actual objects.
    /// </summary>
    public class SceneObjectClass
    {
        public RoadDescr roadDescr;
        public Spline spline;
    }

    public class RoadObjectClass : SceneObjectClass
    {
        public Bounds splineBounds;

        public RoadObjectClass(RoadDescr roadDescr, Spline spline)
        {
            this.roadDescr = roadDescr;
            this.spline = spline;
            splineBounds = spline.GetBounds();
        }
    }
    
    
}