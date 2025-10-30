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
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor
{
    public static class SpawnObjectPresetDrawerCreation
    {
        public static VisualElement CreatePropertyGUI(SpawnObjectPreset spawnObjectPreset, SerializedProperty property)
        {
            var container = new VisualElement();

            TextField category = new("Category");
            category.BindProperty(property.FindPropertyRelative(nameof(SpawnObjectPreset.category)));
            category.tooltip = "Category to which this object set will be applied.\n\n" +
                               "Multiple categories can be set using commas, for example: Category1,Category2,Category3.";
            
            TextField spawnObjectName = new("Name");
            spawnObjectName.BindProperty(property.FindPropertyRelative(nameof(SpawnObjectPreset.spawnObjectName)));
            spawnObjectName.tooltip = "Name of this object set.";


            var objectClassesProperty = property.FindPropertyRelative(nameof(SpawnObjectPreset.spawnObjects));
            var objectClassesListView = CreateObjectClassesListView(spawnObjectPreset.spawnObjects, objectClassesProperty);


            container.Add(category);
            container.Add(spawnObjectName);
            container.Add(objectClassesListView);

            return container;
        }

        /********************************************************************************************************************************/

        public static ListView CreateObjectClassesListView(List<SpawnObject> spawnObjects, SerializedProperty property)
        {
            var lanePresetIndexesListView = new ListView();

            lanePresetIndexesListView.showBoundCollectionSize = false;
            lanePresetIndexesListView.itemsSource = spawnObjects;
            lanePresetIndexesListView.PGObjectListViewStyle();
            lanePresetIndexesListView.reorderable = true;
            lanePresetIndexesListView.reorderMode = ListViewReorderMode.Animated;
            lanePresetIndexesListView.headerTitle = "Objects";
            lanePresetIndexesListView.showFoldoutHeader = true;

            lanePresetIndexesListView.makeItem = () => MakeObjectClassItem();

            lanePresetIndexesListView.bindItem = (item, j) =>
            {
                property.serializedObject.Update();
                var objectClassItemProperty = property.GetArrayElementAtIndex(j);

                BindObjectClassItem(item, objectClassItemProperty, spawnObjects, j);
            };

            return lanePresetIndexesListView;
        }

        private static VisualElement MakeObjectClassItem()
        {
            var item = new GroupBox();
            item.PGBorderWidth(1);
            item.PGBorderColor(PGColors.CustomBorder());
            item.style.paddingRight = 3f;
            item.style.marginLeft = 0f;
            item.style.marginTop = 3f;
            item.style.marginBottom = 3f;

            var obj = new ObjectField("Object");
            obj.name = nameof(obj);
            obj.objectType = typeof(GameObject);
            obj.style.marginRight = 3f;
            obj.style.flexGrow = 0.1f;

            var objectType = new EnumField("Type");
            objectType.name = nameof(objectType);
            objectType.tooltip = "Type of object being spawned.\n\n" +
                                 "Road: Objects spawned over the full road lenght, for example street lights, props etc.\n\n" +
                                 "Intersection Approach: Approaching an intersection, for example traffic lights.\n\n" +
                                 "Intersection Exit: Exiting an intersection, for example pedestrian crossings.\n\n" +
                                 "Railing: Railings on the outisde of the roads.";


            /********************************************************************************************************************************/
            // Railing

            var RailingWrapper = new VisualElement();
            RailingWrapper.name = nameof(RailingWrapper);

            var railingBoundsButton = new Button();
            railingBoundsButton.name = nameof(railingBoundsButton);
            railingBoundsButton.text = "Calculate Bounds";
            railingBoundsButton.tooltip = "Calculates the max. bounds from the object's mesh renderers.";

            var railingSpacing = new FloatField("Spacing");
            railingSpacing.name = nameof(railingSpacing);

            var railingOffset = new Vector2Field("Offset");
            railingOffset.name = nameof(railingOffset);
            railingOffset.tooltip = "Local offset of the rails (width/height);";
            
            var railingAutoSize = new Toggle("Auto Resize");
            railingAutoSize.name = nameof(railingAutoSize);
            railingAutoSize.tooltip = "Automatically adjusts the railing size.x to fill potential gaps between railings.";
            railingAutoSize.PGToggleStyleDefault();
            
            var railingObjectType = new EnumField("Object Types");
            railingObjectType.name = nameof(railingObjectType);
            railingObjectType.tooltip = "Allows to select which object types are used for the railing.";
            
            var railingElevation = new EnumField("Elevation");
            railingElevation.name = nameof(railingElevation);
            railingElevation.tooltip = "Allow spawning only above or below the elevation height.";
            
            
            /********************************************************************************************************************************/
            // Custom

            var CustomWrapper = new VisualElement();
            CustomWrapper.name = nameof(CustomWrapper);
            
            var spacingWrapper = new VisualElement();
            spacingWrapper.name = nameof(spacingWrapper);
            var spacingType = new EnumField("Spacing Type");
            spacingType.name = nameof(spacingType);
            var spacing = new FloatField("Spacing");
            spacing.name = nameof(spacing);
            spacing.PGClampValue(0.01f);
            spacing.tooltip = "Minimum spacing to the next object in world units.";
            spacingWrapper.Add(spacingType);
            spacingWrapper.Add(spacing);

            var position = new EnumField("Position");
            position.name = nameof(position);
            position.tooltip = "Position on the width of the road.";

            var positionOffsetForward = new FloatField("Pos. Offset Forward");
            positionOffsetForward.name = nameof(positionOffsetForward);
            positionOffsetForward.tooltip = "Position offset local z-direction in world units, backwards from the road.";
            positionOffsetForward.PGClampValue();

            var positionOffsetRight = new FloatField("Pos. Offset Right");
            positionOffsetRight.name = nameof(positionOffsetRight);
            positionOffsetRight.tooltip = "Position offset local x-direction in world units, where positive values move the object to the inside.";

            var rotation = new EnumField("Rotation");
            rotation.name = nameof(rotation);
            rotation.tooltip = "Y-rotation, with the object forward-axis looking towards the inside or outside the road.\n\n" +
                               "Random ranges from 0 to 360 degrees.";

            var alignToNormal = new Toggle("Align Normal");
            alignToNormal.name = nameof(alignToNormal);
            alignToNormal.tooltip = "Aligns the objects to the road up vector.";
            alignToNormal.PGToggleStyleDefault();
            
            var scale = new MinMaxSlider("Scale");
            scale.name = nameof(scale);
            scale.tooltip = "Scale multiplier (random from-to).";
            scale.lowLimit = 0f;
            scale.highLimit = 2f;
            scale.showMixedValue = true;

            var heightOffset = new FloatField("Height Offset");
            heightOffset.name = nameof(heightOffset);
            heightOffset.tooltip = "Height offset from the base height.";

            var requiresDirectionWrapper = new VisualElement();
            requiresDirectionWrapper.name = nameof(requiresDirectionWrapper);

            var requiresDirection = new Toggle("Requires Direction");
            requiresDirection.name = nameof(requiresDirection);
            requiresDirection.tooltip = "Allow only certain intersection directions for this object.";
            requiresDirection.PGToggleStyleDefault();

            var requiresDirectionForward = new Toggle("Forward");
            requiresDirectionForward.name = nameof(requiresDirectionForward);
            requiresDirectionForward.tooltip = "Requires intersection forward direction.";
            requiresDirectionForward.style.marginLeft = 9f;

            var requiresDirectionLeft = new Toggle("Left");
            requiresDirectionLeft.name = nameof(requiresDirectionLeft);
            requiresDirectionLeft.tooltip = "Requires intersection left direction.";
            requiresDirectionLeft.style.marginLeft = 9f;

            var requiresDirectionRight = new Toggle("Right");
            requiresDirectionRight.name = nameof(requiresDirectionRight);
            requiresDirectionRight.tooltip = "Requires intersection right direction.";
            requiresDirectionRight.style.marginLeft = 9f;

            requiresDirectionWrapper.Add(requiresDirection);
            requiresDirectionWrapper.Add(requiresDirectionForward);
            requiresDirectionWrapper.Add(requiresDirectionLeft);
            requiresDirectionWrapper.Add(requiresDirectionRight);

            var elevation = new EnumField("Elevation");
            elevation.name = nameof(elevation);
            elevation.tooltip = "Allow spawning only above or below the elevation height.";

            var heightRange = new Vector2Field("Height Range");
            heightRange.name = nameof(heightRange);
            heightRange.PGVector2ComponentLabel("Min", "Max");
            heightRange.tooltip = "Allows spawning only within a specified range of height, measured as the distance from the ground to the road.";

            var removeOverlap = new Toggle("Remove Overlap");
            removeOverlap.name = nameof(removeOverlap);
            removeOverlap.tooltip = "Checks for overlap to other roads or intersections.\n\n" +
                                    "Useful for elements such as pillars or objects which are larger than the elevation height.";
            removeOverlap.PGToggleStyleDefault();

            var chance = new Slider("Chance");
            chance.name = nameof(chance);
            chance.tooltip = "Chance for each individual object to be spawned.";
            chance.lowValue = 0f;
            chance.highValue = 1f;
            chance.showInputField = true;

            item.Add(objectType);
            item.Add(obj);
            
            RailingWrapper.Add(railingBoundsButton);
            RailingWrapper.Add(railingSpacing);
            RailingWrapper.Add(railingOffset);
            RailingWrapper.Add(railingAutoSize);
            RailingWrapper.Add(railingObjectType);
            RailingWrapper.Add(railingElevation);
            
            CustomWrapper.Add(spacingWrapper);
            CustomWrapper.Add(position);
            CustomWrapper.Add(positionOffsetForward);
            CustomWrapper.Add(positionOffsetRight);
            CustomWrapper.Add(heightOffset);
            CustomWrapper.Add(rotation);
            CustomWrapper.Add(alignToNormal);
            CustomWrapper.Add(scale);
            CustomWrapper.Add(requiresDirectionWrapper);
            CustomWrapper.Add(elevation);
            CustomWrapper.Add(heightRange);
            CustomWrapper.Add(removeOverlap);
            CustomWrapper.Add(chance);
            
            item.Add(RailingWrapper);
            item.Add(CustomWrapper);

            return item;
        }

        private static void BindObjectClassItem(VisualElement item, SerializedProperty objectClassItemProperty,
            List<SpawnObject> spawnObjects, int index)
        {
            var spawnObject = spawnObjects[index];

            ObjectField obj = item.Q<ObjectField>(nameof(obj));
            obj.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.obj)));

            EnumField objectType = item.Q<EnumField>(nameof(objectType));
            objectType.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.objectType)));
            
            VisualElement RailingWrapper = item.Q<VisualElement>(nameof(RailingWrapper));
            VisualElement CustomWrapper = item.Q<VisualElement>(nameof(CustomWrapper));

            objectType.RegisterValueChangedCallback(evt => TypeVisibility());
            TypeVisibility();

            void TypeVisibility()
            {
                RailingWrapper.PGDisplayStyleFlex(spawnObject.objectType == SpawnObjectType.Railing);
                CustomWrapper.PGDisplayStyleFlex(spawnObject.objectType is SpawnObjectType.Road or SpawnObjectType.IntersectionApproach or SpawnObjectType.IntersectionExit);
            }
            
            
            /********************************************************************************************************************************/
            // Railing

            Button railingBoundsButton = RailingWrapper.Q<Button>(nameof(railingBoundsButton));
            railingBoundsButton.clicked += () =>
            {
                var _obj = spawnObject.obj;
                if (_obj == null) return;
                var meshRenderers = _obj.GetComponentsInChildren<MeshRenderer>();
                if (meshRenderers.Length == 0) return;
                var railingBounds = new Bounds();
                for (var i = 0; i < meshRenderers.Length; i++) railingBounds.Encapsulate(meshRenderers[i].bounds);
                spawnObject.railingSpacing = Mathf.Max(railingBounds.size.x, railingBounds.size.y, railingBounds.size.z);
            };

            FloatField railingSpacing = RailingWrapper.Q<FloatField>(nameof(railingSpacing));
            railingSpacing.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.railingSpacing)));
            railingSpacing.PGClampValue();
            
            Vector2Field railingOffset =  RailingWrapper.Q<Vector2Field>(nameof(railingOffset));
            railingOffset.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.railingOffset)));
            
            Toggle railingAutoSize = RailingWrapper.Q<Toggle>(nameof(railingAutoSize));
            railingAutoSize.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.railingAutoSize)));
            
            EnumField railingObjectType = RailingWrapper.Q<EnumField>(nameof(railingObjectType));
            railingObjectType.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.railingObjectType)));
            
            EnumField railingElevation = RailingWrapper.Q<EnumField>(nameof(railingElevation));
            railingElevation.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.railingElevation)));


            /********************************************************************************************************************************/
            // Custom

            VisualElement spacingWrapper = item.Q<VisualElement>(nameof(spacingWrapper));
            EnumField spacingType = item.Q<EnumField>(nameof(spacingType));
            spacingType.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.spacingType)));
            FloatField spacing = item.Q<FloatField>(nameof(spacing));
            spacing.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.spacing)));

            EnumField position = item.Q<EnumField>(nameof(position));
            position.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.position)));

            FloatField positionOffsetForward = item.Q<FloatField>(nameof(positionOffsetForward));
            positionOffsetForward.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.positionOffsetForward)));

            FloatField positionOffsetRight = item.Q<FloatField>(nameof(positionOffsetRight));
            positionOffsetRight.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.positionOffsetRight)));

            EnumField rotation = item.Q<EnumField>(nameof(rotation));
            rotation.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.rotation)));

            MinMaxSlider scale = item.Q<MinMaxSlider>(nameof(scale));
            scale.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.scale)));
            
            FloatField heightOffset = item.Q<FloatField>(nameof(heightOffset));
            heightOffset.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.heightOffset)));

            Toggle alignToNormal = item.Q<Toggle>(nameof(alignToNormal));
            alignToNormal.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.alignToNormal)));

            EnumField elevation = item.Q<EnumField>(nameof(elevation));
            elevation.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.elevation)));

            Vector2Field heightRange = item.Q<Vector2Field>(nameof(heightRange));
            heightRange.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.heightRange)));

            VisualElement requiresDirectionWrapper = item.Q<VisualElement>(nameof(requiresDirectionWrapper));
            Toggle requiresDirection = item.Q<Toggle>(nameof(requiresDirection));
            requiresDirection.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.requiresDirection)));
            Toggle requiresDirectionForward = item.Q<Toggle>(nameof(requiresDirectionForward));
            requiresDirectionForward.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.requiresDirectionForward)));
            Toggle requiresDirectionLeft = item.Q<Toggle>(nameof(requiresDirectionLeft));
            requiresDirectionLeft.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.requiresDirectionLeft)));
            Toggle requiresDirectionRight = item.Q<Toggle>(nameof(requiresDirectionRight));
            requiresDirectionRight.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.requiresDirectionRight)));

            Toggle removeOverlap = item.Q<Toggle>(nameof(removeOverlap));
            removeOverlap.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.removeOverlap)));

            Slider chance = item.Q<Slider>(nameof(chance));
            chance.BindProperty(objectClassItemProperty.FindPropertyRelative(nameof(SpawnObject.chance)));

            positionOffsetRight.PGDisplayStyleFlex(spawnObject.position != SpawnObjectPosition.Middle);
            position.RegisterValueChangedCallback(evt =>
            {
                positionOffsetRight.PGDisplayStyleFlex(spawnObject.position != SpawnObjectPosition.Middle);
            });

            spacingWrapper.PGDisplayStyleFlex(spawnObject.objectType == SpawnObjectType.Road);
            requiresDirectionWrapper.PGDisplayStyleFlex(spawnObject.objectType == SpawnObjectType.IntersectionApproach);
            alignToNormal.PGDisplayStyleFlex(spawnObject.objectType != SpawnObjectType.IntersectionExit);
            positionOffsetForward.PGDisplayStyleFlex(spawnObject.objectType == SpawnObjectType.IntersectionApproach);

            objectType.RegisterValueChangedCallback(evt =>
            {
                spacingWrapper.PGDisplayStyleFlex(spawnObject.objectType == SpawnObjectType.Road);
                requiresDirectionWrapper.PGDisplayStyleFlex(spawnObject.objectType == SpawnObjectType.IntersectionApproach);
                alignToNormal.PGDisplayStyleFlex(spawnObject.objectType != SpawnObjectType.IntersectionExit);
                positionOffsetForward.PGDisplayStyleFlex(spawnObject.objectType == SpawnObjectType.IntersectionApproach);
            });

            spacing.PGDisplayStyleFlex(spawnObject.spacingType == SpacingType.WorldUnits);
            spacingType.RegisterValueChangedCallback(evt => { spacing.PGDisplayStyleFlex(spawnObject.spacingType == SpacingType.WorldUnits); });

            requiresDirectionForward.PGDisplayStyleFlex(spawnObject.requiresDirection);
            requiresDirectionLeft.PGDisplayStyleFlex(spawnObject.requiresDirection);
            requiresDirectionRight.PGDisplayStyleFlex(spawnObject.requiresDirection);
            requiresDirection.RegisterValueChangedCallback(evt =>
            {
                requiresDirectionForward.PGDisplayStyleFlex(spawnObject.requiresDirection);
                requiresDirectionLeft.PGDisplayStyleFlex(spawnObject.requiresDirection);
                requiresDirectionRight.PGDisplayStyleFlex(spawnObject.requiresDirection);
            });

            heightRange.PGDisplayStyleFlex(spawnObject.elevation == Elevation.ElevatedOnly);
            elevation.RegisterValueChangedCallback(evt => { heightRange.PGDisplayStyleFlex(spawnObject.elevation == Elevation.ElevatedOnly); });
        }
    }
}
#endif