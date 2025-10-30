// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public static class Constants
    {
        public const string GlobalSettings = "GlobalSettings";
        public const string DefaultReferences = "DefaultReferences";

#if UNITY_EDITOR
        public const string DocumentationURL = "https://docs.google.com/document/d/1nQ7xd3xlIsZnPzJXfO7mwDC4IeWga9USS0rh8zO9Q4A/edit?usp=sharing";
#endif

        public const string DefaultTag = "Untagged";

        public const string ConstructionParent = "Road Construction";
        public const string DisplayConstructionParent = "Display Construction";
        public const string DisplayDemolishParent = "Display Demolish";
        public const string DisplayMoveIntersectionParent = "Display Move Intersection";
        public const string UndoParent = "Undo";
        public const string RoadPreviewParent = "Road Preview";
        
        public const string PrefixRoad = "Road";
        public const string PrefixIntersection = "Intersection";
        public const string PrefixRoundabout = "Roundabout";
        public const string PrefixRamp = "Ramp";
        public const string PrefixCustom = "Custom";

        // Calculations
        public const float TangentLengthIntersection = 0.5f;
        public const float HeightOffset = -0.001f;

        public const float MaxConnectionDistance = 0.01f;

        public const float RoadMaxSnapLength = 2f;

        public const float AngleDistanceTolerance = 90f; // Degrees
        public const float AngleDistanceFactor = 0.0175f;

        // Gizmos
        public static Color GizmoColorCar = Color.blue;
        public static Color GizmoColorCarIntersection = new Color32(0, 0, 50, 255);
        public static Color GizmoColorPedestrian = Color.green;
        public static Color GizmoColorPedestrianIntersection = new Color32(0, 50, 0, 255);
        
        /********************************************************************************************************************************/

        public static Vector3 RaycastOffset(ComponentSettings settings)
        {
            var raycastOffset = math.abs(settings.elevationStartHeight)
                                + math.abs(settings.minOverlapHeight) +
                                +math.abs(settings.heightRange.x) + math.abs(settings.heightRange.y);
            return new Vector3(0f, raycastOffset * 2f, 0f);
        }

        public static bool ForwardAngle(float angle)
        {
            return math.abs(angle) >= -45f && math.abs(angle) <= 45f;
        }
        public static bool LeftAngle(float angle)
        {
            return angle is >= -135f and <= -45f;
        }
        public static bool RightAngle(float angle)
        {
            return angle is >= 45f and <= 135f;
        }
    }
}