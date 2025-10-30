// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public static class Enums
    {

        
    }

    public enum EditorDisplay
    {
        ComponentSettings,
        RoadSet,
        Integrations
    }
    
    public enum OverlapType
    {
        Intersection,
        Road,
        Shared
    }

    public enum LaneType
    {
        Road,
        LeftSide,
        Intersection,
        ElevatedOnly,
        RoadEnd,
    }
    
    public enum TrafficLaneType
    {
        Car,
        Pedestrian,
    }
    public enum TrafficLaneDirection
    {
        Forward,
        Backwards,
        Both
    }

    public enum DirectConnection
    {
        Free,
        Align
    }

    public enum RoadEnd
    {
        Rounded,
        None
    }
    public enum PartType
    {
        Roads,
        LanePreset,
        SpawnObjectPreset,
        TrafficLanePreset
    }

    public enum ObjectTypeSelection
    {
        Any,
        RoadOnly,
        IntersectionOnly
    }
    public enum Elevation
    {
        Any,
        GroundOnly,
        ElevatedOnly
    }

    public enum SpawnObjectType
    {
        Road,
        IntersectionApproach,
        IntersectionExit,
        Railing
    }

    public enum SpacingType
    {
        WorldUnits,
        Bounds
    }
    public enum SpawnObjectPosition
    {
        Middle,
        Side,
        BothSides
    }
    public enum SpawnObjectRotation
    {
        Inside,
        Outside,
        Forward,
        Backward,
        Random
    }
    
    public enum BuilderRoadType
    {
        None,
        Road,
        Roundabout,
        Ramp
    }
    
    public enum BuilderOtherType
    {
        None,
        Demolish,
        Move,
        Reverse,
    }
    
    public enum RoundaboutDesign
    {
        Default,
        [InspectorName("Cul-de-sac")] CulDeSac
    }

    public enum AddCollider
    {
        None,
        Convex,
        NonConvex
    }

    public enum MoveStatus
    {
        Select,
        Move
    }
    
    public enum DrawGizmos
    {
        None,
        Selected,
        Always
    }
    
    public enum DrawGizmosColor
    {
        Object,
        Lane
    }
    
}
