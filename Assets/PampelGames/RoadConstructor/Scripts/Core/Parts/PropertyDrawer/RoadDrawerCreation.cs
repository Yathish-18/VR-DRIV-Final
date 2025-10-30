// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using PampelGames.Shared;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor
{
    public static class RoadDrawerCreation
    {
        public static VisualElement CreatePropertyGUI(Road _road, SerializedProperty property)
        {
            var container = new VisualElement();

            TextField category = new("Category");
            TextField roadName = new("Name (ID)");
            FloatField length = new("Length");
            var priority = new IntegerField("Priority");
            var elevatable = new Toggle("Elevatable");
            var oneWay = new Toggle("One-Way");
            var shadowCastingMode = new EnumField("Cast Shadows");

            category.BindProperty(property.FindPropertyRelative(nameof(Road.category)));
            roadName.BindProperty(property.FindPropertyRelative(nameof(Road.roadName)));
            length.BindProperty(property.FindPropertyRelative(nameof(Road.length)));
            priority.BindProperty(property.FindPropertyRelative(nameof(Road.priority)));
            elevatable.BindProperty(property.FindPropertyRelative(nameof(Road.elevatable)));
            oneWay.BindProperty(property.FindPropertyRelative(nameof(Road.oneWay)));
            shadowCastingMode.BindProperty(property.FindPropertyRelative(nameof(Road.shadowCastingMode)));

            category.tooltip = "The category that this road belongs to.\n\n" +
                               "Optional, can be used to assign presets.";
            roadName.tooltip = "Name and identifier of this road. Must be unique.";
            length.PGClampValue();
            length.tooltip = "The length of one road part, which fully covers the UV texture map y coordinate.";
            priority.tooltip = "At intersections, roads with higher priority are prioritised.\n" +
                               "If priorities are equal, the road higher on this list takes precedence.";
            elevatable.tooltip = "Determines if the road can be elevated or not.";
            elevatable.PGToggleStyleDefault();
            oneWay.tooltip = "The road only allows a forward direction. This is used to determine which intersection objects are spawned.";
            oneWay.PGToggleStyleDefault();
            shadowCastingMode.tooltip = "Defines how and if shadows are cast from this road.";


            var splineEdgesListProperty = property.FindPropertyRelative(nameof(Road.splineEdgesEditor));
            var splineEdgesListView = LaneDrawerUtility.CreateLaneItemsListView(_road.splineEdgesEditor, splineEdgesListProperty);
            splineEdgesListView.tooltip = "Additional lanes that are only applied to this road.";

            var spawnObjectsProperty = property.FindPropertyRelative(nameof(Road.spawnObjects));
            var spawnObjectsListView = SpawnObjectPresetDrawerCreation.CreateObjectClassesListView(_road.spawnObjects, spawnObjectsProperty);
            spawnObjectsListView.tooltip = "Additional objects to spawn that are only applicable to this road.";


            var trafficLanesProperty = property.FindPropertyRelative(nameof(Road.trafficLanes));
            var trafficLanesListView = TrafficLanePresetDrawerCreation.CreateTrafficLanesListView(_road.trafficLanes, trafficLanesProperty);
            trafficLanesListView.tooltip =
                "Traffic lanes operate independently from construction and can be optionally utilized for custom traffic systems. " +
                "Each lane is associated with a Unity Spline.\n\n" +
                "Remember to toggle the 'Add Traffic Component' setting if you want to add them automatically to new roads.";


            // Additional fields for user convenience
            var AdditionalFields = LaneDrawerUtility.CreateLaneItemAdditionFields(_road.splineEdgesEditor, splineEdgesListView);

            container.Add(category);
            container.Add(roadName);
            container.Add(priority);
            container.Add(length);
            container.Add(elevatable);
            container.Add(oneWay);
            container.Add(shadowCastingMode);
            container.Add(AdditionalFields);
            container.Add(splineEdgesListView);
            container.Add(spawnObjectsListView);
            container.Add(trafficLanesListView);


            return container;
        }
    }
}
#endif