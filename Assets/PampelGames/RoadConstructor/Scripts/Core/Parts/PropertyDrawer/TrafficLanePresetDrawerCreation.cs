// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using System.Collections.Generic;
using PampelGames.Shared;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor
{
    public static class TrafficLanePresetDrawerCreation
    {
        public static VisualElement CreatePropertyGUI(TrafficLanePreset trafficLanePreset, SerializedProperty property)
        {
            var container = new VisualElement();

            TextField category = new("Category");
            category.BindProperty(property.FindPropertyRelative(nameof(TrafficLanePreset.category)));
            category.tooltip = "Category to which this traffic lane set will be applied.\n\n" +
                               "Multiple categories can be set using commas, for example: Category1,Category2,Category3.";

            TextField presetName = new("Name");
            presetName.BindProperty(property.FindPropertyRelative(nameof(TrafficLanePreset.trafficLanePresetName)));
            presetName.tooltip = "Name of this traffic lane set.";


            var trafficLanesProperty = property.FindPropertyRelative(nameof(TrafficLanePreset.trafficLanes));
            var trafficLanesListView = CreateTrafficLanesListView(trafficLanePreset.trafficLanes, trafficLanesProperty);


            container.Add(category);
            container.Add(presetName);
            container.Add(trafficLanesListView);

            return container;
        }

        /********************************************************************************************************************************/

        public static ListView CreateTrafficLanesListView(List<TrafficLaneEditor> trafficLanes, SerializedProperty property)
        {
            var lanePresetIndexesListView = new ListView();

            lanePresetIndexesListView.showBoundCollectionSize = false;
            lanePresetIndexesListView.itemsSource = trafficLanes;
            lanePresetIndexesListView.PGObjectListViewStyle();
            lanePresetIndexesListView.reorderable = true;
            lanePresetIndexesListView.reorderMode = ListViewReorderMode.Animated;
            lanePresetIndexesListView.headerTitle = "Traffic Lanes";
            lanePresetIndexesListView.showFoldoutHeader = true;

            lanePresetIndexesListView.makeItem = () => MakeTrafficLaneItem();

            lanePresetIndexesListView.bindItem = (item, j) =>
            {
                property.serializedObject.Update();
                var objectClassItemProperty = property.GetArrayElementAtIndex(j);

                BindObjectClassItem(item, objectClassItemProperty);
            };

            return lanePresetIndexesListView;
        }

        private static VisualElement MakeTrafficLaneItem()
        {
            var item = new GroupBox();
            item.PGBorderWidth(1);
            item.PGBorderColor(PGColors.CustomBorder());
            item.style.paddingRight = 3f;
            item.style.marginLeft = 0f;
            item.style.marginTop = 3f;
            item.style.marginBottom = 3f;


            var trafficLaneType = new EnumField("Type");
            trafficLaneType.name = nameof(trafficLaneType);
            trafficLaneType.tooltip = "Type of this traffic lane.\n\n" +
                                      "'Pedestrian' is the only type that creates crossing splines on intersections.";

            var positionField = new FloatField("Position X");
            positionField.name = nameof(positionField);
            positionField.tooltip = "Middle position of the traffic lane over the width of the road.";
            positionField.PGClampValue();

            var width = new FloatField("Width");
            width.name = nameof(width);
            width.tooltip = "Width of the lane";
            width.PGClampValue();
            
            var direction = new EnumField("Direction");
            direction.name = nameof(direction);
            direction.tooltip = "Forward indicates that the traffic lane spline follows the road spline direction and can enter intersections.";

            var maxSpeed = new FloatField("Max. Speed");
            maxSpeed.name = nameof(maxSpeed);
            maxSpeed.tooltip = "The maximum allowed speed on the lane.";
            maxSpeed.PGClampValue();

            var mirrorToggle = new Toggle("Mirror");
            mirrorToggle.name = nameof(mirrorToggle);
            mirrorToggle.PGToggleStyleDefault();
            mirrorToggle.tooltip = "Mirrors the traffic lane to the other Position X with reversed forward direction.";
            
            item.Add(trafficLaneType);
            item.Add(positionField);
            item.Add(width);
            item.Add(direction);
            item.Add(maxSpeed);
            item.Add(mirrorToggle);


            return item;
        }

        private static void BindObjectClassItem(VisualElement item, SerializedProperty objectClassItemProperty)
        {
            EnumField trafficLaneType = item.Q<EnumField>(nameof(trafficLaneType));
            trafficLaneType.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(TrafficLaneEditor.trafficLaneType)));

            FloatField positionField = item.Q<FloatField>(nameof(positionField));
            positionField.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(TrafficLaneEditor.position)));
            
            FloatField width = item.Q<FloatField>(nameof(width));
            width.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(TrafficLaneEditor.width)));

            EnumField direction = item.Q<EnumField>(nameof(direction));
            direction.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(TrafficLaneEditor.direction)));

            FloatField maxSpeed = item.Q<FloatField>(nameof(maxSpeed));
            maxSpeed.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(TrafficLaneEditor.maxSpeed)));
            
            Toggle mirrorToggle = item.Q<Toggle>(nameof(mirrorToggle));
            mirrorToggle.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(TrafficLaneEditor.mirror)));
        }
    }
}
#endif