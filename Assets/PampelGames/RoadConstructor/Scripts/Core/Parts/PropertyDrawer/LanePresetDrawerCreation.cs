// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor
{
    public static class LanePresetDrawerCreation
    {
        public static VisualElement CreatePropertyGUI(LanePreset lanePreset, SerializedProperty property)
        {
            var container = new VisualElement();

            var categoryProperty = property.FindPropertyRelative(nameof(LanePreset.category));
            var category = new TextField("Category");
            category.style.flexGrow = 1f;
            category.BindProperty(categoryProperty);
            category.tooltip = "Category to which this lane set will be applied.\n\n" +
                               "Multiple categories can be set using commas, for example: Category1,Category2,Category3.";

            var templateNameProperty = property.FindPropertyRelative(nameof(LanePreset.templateName));
            var templateName = new TextField("Name");
            templateName.style.flexGrow = 1f;
            templateName.BindProperty(templateNameProperty);

            var lanesProperty = property.FindPropertyRelative(nameof(LanePreset.lanes));
            var lanes = LaneDrawerUtility.CreateLaneItemsListView(lanePreset.lanes, lanesProperty);

            // Additional fields for user convenience
            var AdditionalFields = LaneDrawerUtility.CreateLaneItemAdditionFields(lanePreset.lanes, lanes);

            container.Add(category);
            container.Add(templateName);
            container.Add(AdditionalFields);
            container.Add(lanes);

            return container;
        }
    }
}
#endif