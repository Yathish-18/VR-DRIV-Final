// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.Shared.Construction
{
    public static class ConstructionEnums
    {
        
    }
    
    public enum FailCause
    {
        TrackLength,
        OverlapIntersection,
        OverlapTrack,
        GroundMissing,
        HeightRange,
        ElevatedIntersection,
        Curvature,
        Slope,
        IntersectionTrackLength,
        IntersectionTrackSlope,
        NotElevatable,
        OneWayRequired,
        MissingConnection,
        InvalidConnection,
        InvalidTrackCount,
    }
}
