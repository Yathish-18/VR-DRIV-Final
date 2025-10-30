// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared;
using PampelGames.Shared.Construction.Editor;
using PampelGames.Shared.Editor;
using PampelGames.Shared.Tools.PGInspector;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor.Editor
{
    [CustomEditor(typeof(RoadConstructor))]
    public class RoadConstructorInspector : UnityEditor.Editor
    {
        public VisualTreeAsset _visualTree;
        private VisualElement container;
        private RoadConstructor roadConstructor;

        /********************************************************************************************************************************/

        private ToolbarButton documentation;

        private ToolbarToggle componentSettingsToggle;
        private GroupBox ComponentSettingsGroup;
        private ToolbarToggle roadSetToggle;
        private GroupBox RoadSetSettingsGroup;
        
        // Integrations
        private ToolbarToggle integrationsToggle;
        private GroupBox IntegrationsGroup;
        private VisualElement IntegrationsParent;

        // Road Set
        private ObjectField roadSet;
        private SerializedObject roadSetSerializedObject;
        private Button createNewSet;
        
        // Road Visualization
        private FloatField roadPreviewLength;
        private Toggle roadPreviewSpawnObjects;
        private Toggle roadPreviewElevated;
        private Button createPreviewButton;
        private Button focusPreviewButton;
        private Button removePreviewButton;
        

        // Settings
        private IntegerField resolution;
        private IntegerField detailResolution;
        private Toggle smartReduce;
        private EnumField addCollider;
        private LayerField addColliderLayer;
        private TagField roadTag;
        private ListView lodList;

        private Label componentConstructionLabel;
        private Label componentVerificationLabel;
        private VisualElement Construction;

        private FloatField baseRoadHeight;
        private Vector3Field grid;
        private Vector3Field gridOffset;
        private FloatField snapDistance;
        private FloatField snapHeight;
        private FloatField snapAngleIntersection;
        private FloatField minAngleIntersection;
        private CurveField distanceRatioAngleCurve;
        private EnumField splineLengthUV;
        private Slider tangentLength;
        private FloatField intersectionDistance;
        private EnumField directConnection;
        private EnumField roadEnd;

        private Vector2Field roadLength;
        private FloatField minOverlapDistance;
        private FloatField maxCurvature;

        public LayerMaskField groundLayers;
        private FloatField elevationStartHeight;
        private Toggle elevatedIntersections;
        public Vector2Field heightRange;
        private FloatField minOverlapHeight;
        private FloatField maxSlope;
        private Toggle smoothSlope;
        
        public IntegerField undoStorageSize;
        public Toggle undoResetsTerrain;
        public Toggle demolishResetsTerrain;

        public Toggle terrainSettings;
        private VisualElement TerrainFields;
        public PropertyField terrains;
        public ObjectField terrain;
        public Toggle removeDetails;
        public Toggle removeTrees;
        public Toggle levelHeight;
        public VisualElement LevelTerrainHeight;
        public IntegerField slopeTextureIndex;
        public TextField slopeTextureName;
        public SliderInt slopeSmooth;
        public Slider slopeTextureStrength;

        private Toggle addTrafficComponent;
        private VisualElement TrafficSystemFields;
        private Toggle updateWaypoints;
        private Vector2Field waypointDistance;
        private EnumField waypointGizmos;
        private EnumField waypointGizmosColor;
        private VisualElement WaypointGizmosFields;
        private FloatField waypointGizmoSize;
        private Toggle waypointConnectionsOnly;

        private ToolbarMenu addPartsMenu;
        private ToolbarMenu partsMenu;
        private ToolbarToggle roadsToggle;
        private VisualElement RoadsParent;
        private ToolbarToggle lanePresetsToggle;
        private VisualElement LanePresetsParent;
        private ToolbarToggle spawnObjectPresetsToggle;
        private VisualElement SpawnObjectPresetsParent;
        private ToolbarToggle trafficLanePresetsToggle;
        private VisualElement TrafficLanePresetsParent;

        private SerializedProperty componentSettingsProperty;

        /********************************************************************************************************************************/
        protected void OnEnable()
        {
            container = new VisualElement();
            _visualTree.CloneTree(container);
            roadConstructor = target as RoadConstructor;

            GetDefaultReferences();
            FindElements(container);
            BindElements();
            VisualizeElements();
        }

        /********************************************************************************************************************************/

        private void GetDefaultReferences()
        {
            if (roadConstructor._DefaultReferences == null)
            {
                roadConstructor._DefaultReferences = PGAssetUtility.LoadAsset<SO_DefaultReferences>(Constants.DefaultReferences);
                EditorUtility.SetDirty(roadConstructor);
            }
        }

        private void FindElements(VisualElement root)
        {
            documentation = root.Q<ToolbarButton>(nameof(documentation));

            componentSettingsToggle = root.Q<ToolbarToggle>(nameof(componentSettingsToggle));
            ComponentSettingsGroup = root.Q<GroupBox>(nameof(ComponentSettingsGroup));
            roadSetToggle = root.Q<ToolbarToggle>(nameof(roadSetToggle));
            RoadSetSettingsGroup = root.Q<GroupBox>(nameof(RoadSetSettingsGroup));
            createNewSet = root.Q<Button>(nameof(createNewSet));
            
            roadPreviewLength = root.Q<FloatField>(nameof(roadPreviewLength));
            roadPreviewSpawnObjects = root.Q<Toggle>(nameof(roadPreviewSpawnObjects));
            roadPreviewElevated = root.Q<Toggle>(nameof(roadPreviewElevated));
            createPreviewButton = root.Q<Button>(nameof(createPreviewButton));
            focusPreviewButton = root.Q<Button>(nameof(focusPreviewButton));
            removePreviewButton = root.Q<Button>(nameof(removePreviewButton));
            
            roadSet = root.Q<ObjectField>(nameof(roadSet));
            Construction = root.Q<VisualElement>(nameof(Construction));

            componentConstructionLabel = root.Q<Label>(nameof(componentConstructionLabel));
            componentVerificationLabel = root.Q<Label>(nameof(componentVerificationLabel));

            resolution = root.Q<IntegerField>(nameof(resolution));
            detailResolution = root.Q<IntegerField>(nameof(detailResolution));
            smartReduce = root.Q<Toggle>(nameof(smartReduce));
            undoStorageSize = root.Q<IntegerField>(nameof(undoStorageSize));
            undoResetsTerrain = root.Q<Toggle>(nameof(undoResetsTerrain));
            demolishResetsTerrain = root.Q<Toggle>(nameof(demolishResetsTerrain));
            addCollider = root.Q<EnumField>(nameof(addCollider));
            addColliderLayer = root.Q<LayerField>(nameof(addColliderLayer));
            roadTag = root.Q<TagField>(nameof(roadTag));
            lodList = root.Q<ListView>(nameof(lodList));

            baseRoadHeight = root.Q<FloatField>(nameof(baseRoadHeight));
            grid = root.Q<Vector3Field>(nameof(grid));
            gridOffset = root.Q<Vector3Field>(nameof(gridOffset));
            snapDistance = root.Q<FloatField>(nameof(snapDistance));
            snapHeight = root.Q<FloatField>(nameof(snapHeight));
            snapAngleIntersection = root.Q<FloatField>(nameof(snapAngleIntersection));
            minAngleIntersection = root.Q<FloatField>(nameof(minAngleIntersection));
            distanceRatioAngleCurve = root.Q<CurveField>(nameof(distanceRatioAngleCurve));
            splineLengthUV = root.Q<EnumField>(nameof(splineLengthUV));
            smoothSlope = root.Q<Toggle>(nameof(smoothSlope));
            tangentLength = root.Q<Slider>(nameof(tangentLength));
            intersectionDistance = root.Q<FloatField>(nameof(intersectionDistance));
            directConnection = root.Q<EnumField>(nameof(directConnection));
            roadEnd = root.Q<EnumField>(nameof(roadEnd));

            elevationStartHeight = root.Q<FloatField>(nameof(elevationStartHeight));
            elevatedIntersections = root.Q<Toggle>(nameof(elevatedIntersections));

            roadLength = root.Q<Vector2Field>(nameof(roadLength));
            minOverlapDistance = root.Q<FloatField>(nameof(minOverlapDistance));
            minOverlapHeight = root.Q<FloatField>(nameof(minOverlapHeight));
            maxCurvature = root.Q<FloatField>(nameof(maxCurvature));
            maxSlope = root.Q<FloatField>(nameof(maxSlope));

            groundLayers = root.Q<LayerMaskField>(nameof(groundLayers));
            heightRange = root.Q<Vector2Field>(nameof(heightRange));


            terrainSettings = root.Q<Toggle>(nameof(terrainSettings));
            TerrainFields = root.Q<VisualElement>(nameof(TerrainFields));
            terrain = root.Q<ObjectField>(nameof(terrain));
            terrains = root.Q<PropertyField>(nameof(terrains));
            removeDetails = root.Q<Toggle>(nameof(removeDetails));
            removeTrees = root.Q<Toggle>(nameof(removeTrees));
            levelHeight = root.Q<Toggle>(nameof(levelHeight));
            LevelTerrainHeight = root.Q<VisualElement>(nameof(LevelTerrainHeight));
            slopeTextureIndex = root.Q<IntegerField>(nameof(slopeTextureIndex));
            slopeTextureName = root.Q<TextField>(nameof(slopeTextureName));
            slopeSmooth = root.Q<SliderInt>(nameof(slopeSmooth));
            slopeTextureStrength = root.Q<Slider>(nameof(slopeTextureStrength));

            addTrafficComponent = root.Q<Toggle>(nameof(addTrafficComponent));
            TrafficSystemFields = root.Q<VisualElement>(nameof(TrafficSystemFields));
            updateWaypoints = root.Q<Toggle>(nameof(updateWaypoints));
            waypointDistance = root.Q<Vector2Field>(nameof(waypointDistance));
            waypointGizmos = root.Q<EnumField>(nameof(waypointGizmos));
            waypointGizmosColor = root.Q<EnumField>(nameof(waypointGizmosColor));
            WaypointGizmosFields = root.Q<VisualElement>(nameof(WaypointGizmosFields));
            waypointGizmoSize = root.Q<FloatField>(nameof(waypointGizmoSize));
            waypointConnectionsOnly = root.Q<Toggle>(nameof(waypointConnectionsOnly));


            partsMenu = root.Q<ToolbarMenu>(nameof(partsMenu));
            RoadsParent = root.Q<VisualElement>(nameof(RoadsParent));
            roadsToggle = root.Q<ToolbarToggle>(nameof(roadsToggle));
            addPartsMenu = root.Q<ToolbarMenu>(nameof(addPartsMenu));
            lanePresetsToggle = root.Q<ToolbarToggle>(nameof(lanePresetsToggle));
            LanePresetsParent = root.Q<VisualElement>(nameof(LanePresetsParent));
            spawnObjectPresetsToggle = root.Q<ToolbarToggle>(nameof(spawnObjectPresetsToggle));
            SpawnObjectPresetsParent = root.Q<VisualElement>(nameof(SpawnObjectPresetsParent));
            trafficLanePresetsToggle = root.Q<ToolbarToggle>(nameof(trafficLanePresetsToggle));
            TrafficLanePresetsParent = root.Q<VisualElement>(nameof(TrafficLanePresetsParent));
        }

        private void BindElements()
        {
            roadPreviewLength.PGSetupBindProperty(serializedObject, nameof(roadPreviewLength));
            roadPreviewSpawnObjects.PGSetupBindProperty(serializedObject, nameof(roadPreviewSpawnObjects));
            roadPreviewElevated.PGSetupBindProperty(serializedObject, nameof(roadPreviewElevated));
            
            componentSettingsProperty = serializedObject.FindProperty(nameof(RoadConstructor.componentSettings));
            
            resolution.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(resolution));
            detailResolution.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(detailResolution));
            smartReduce.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(smartReduce));
            addCollider.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(addCollider));
            addColliderLayer.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(addColliderLayer));
            roadTag.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(roadTag));
            lodList.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(lodList));
            
            baseRoadHeight.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(baseRoadHeight));
            grid.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(grid));
            gridOffset.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(gridOffset));
            snapDistance.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(snapDistance));
            snapHeight.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(snapHeight));
            snapAngleIntersection.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(snapAngleIntersection));
            minAngleIntersection.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(minAngleIntersection));
            distanceRatioAngleCurve.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(distanceRatioAngleCurve));
            splineLengthUV.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(splineLengthUV));
            smoothSlope.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(smoothSlope));
            tangentLength.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(tangentLength));
            intersectionDistance.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(intersectionDistance));
            directConnection.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(directConnection));
            roadEnd.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(roadEnd));

            elevationStartHeight.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(elevationStartHeight));
            elevatedIntersections.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(elevatedIntersections));

            roadLength.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(roadLength));
            minOverlapDistance.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(minOverlapDistance));
            minOverlapHeight.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(minOverlapHeight));
            maxCurvature.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(maxCurvature));
            maxSlope.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(maxSlope));

            groundLayers.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(groundLayers));
            heightRange.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(heightRange));


            terrainSettings.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(terrainSettings));
            terrain.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(terrain));
            terrains.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(terrains));
            removeDetails.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(removeDetails));
            removeTrees.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(removeTrees));
            levelHeight.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(levelHeight));
            slopeSmooth.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(slopeSmooth));
            slopeTextureStrength.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(slopeTextureStrength));
            slopeTextureIndex.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(slopeTextureIndex));

            undoStorageSize.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(undoStorageSize));
            undoResetsTerrain.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(undoResetsTerrain));
            demolishResetsTerrain.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(demolishResetsTerrain));

            addTrafficComponent.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(addTrafficComponent));
            updateWaypoints.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(updateWaypoints));
            waypointDistance.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(waypointDistance));
            waypointGizmos.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(waypointGizmos));
            waypointGizmosColor.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(waypointGizmosColor));
            waypointGizmoSize.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(waypointGizmoSize));
            waypointConnectionsOnly.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(waypointConnectionsOnly));
        }

        private void VisualizeElements()
        {
            documentation.tooltip = "Open the documentation page.";
            componentSettingsToggle.tooltip = "Component Settings";
            roadSetToggle.tooltip = "Road Set";
            roadSet.tooltip = "Road sets contain the road data for construction.";
            createNewSet.tooltip = "Create a new road set.";
            
            roadPreviewLength.tooltip = "Length of a preview road.";
            roadPreviewLength.PGClampValue();
            roadPreviewElevated.tooltip = "Creates the preview roads as elevated.";
            createPreviewButton.tooltip = "For each expanded road in the Road Setup tab, this will create a corresponding road in the scene.\n\n" +
                                                "Objects are parented to the '" + Constants.RoadPreviewParent + "' GameObject.";
            focusPreviewButton.tooltip = "Focuses the scene camera on the '" + Constants.RoadPreviewParent + "' GameObject.";
            removePreviewButton.tooltip = "Removes the '" + Constants.RoadPreviewParent + "' GameObject from the scene.";

            roadSet.objectType = typeof(RoadSet);

            componentConstructionLabel.tooltip = "Settings which directly influence construction.";
            componentVerificationLabel.tooltip = "Settings which are used to verify construction results.";

            groundLayers.tooltip = "Layers of the ground which are used to verify the elevation height.";

            resolution.tooltip =
                "Number of subdivisions for a 10-unit length of road. The subdivisions will be adjusted for roads of differing lengths.";
            resolution.PGClampValue(1);
            detailResolution.tooltip = "Determines the level of detail in creating end-parts, intersections, etc.";
            detailResolution.PGClampValue(1);
            smartReduce.tooltip = "Automatically adjusts resolution based on the curvature and slope. Recommended for most use cases.";
            undoStorageSize.PGClampValue();
            undoStorageSize.tooltip =
                "The number of undoable actions that can be stored in the scene, and also the maximum number of successive undo operations.\n" +
                "\n" +
                "If undo operations are not required, you can set this value to 0 to save on memory usage.";
            undoResetsTerrain.tooltip = "Resets the terrain when undoing construction.";
            demolishResetsTerrain.tooltip = "Resets the terrain when demolishing roads.";
            addCollider.tooltip = "Adds mesh colliders to the roads and intersections.\n\n" +
                                  "Note that if you add colliders, make sure the road layer is different than the ground layer specified below!";
            addColliderLayer.tooltip = "Layer for the roads/intersection. Assigned only to LOD0 objects equipped with mesh filters.";
            roadTag.tooltip = "Tag for the roads/intersections. Assigned only to the main road/intersection object, not its LOD children.";
            lodList.tooltip = "Add additional Level of Detail for intersections, where LOD0 is the original.";

            baseRoadHeight.tooltip = "Default height offset for road and intersection lanes.";
            grid.PGClampValue();
            grid.tooltip = "Snap position on the x/z plane and y height. Useful for grid-style placement.";
            gridOffset.PGClampValue();
            gridOffset.tooltip = "Offset to the position.";
            snapDistance.tooltip = "Snap distance to existing roads/intersections, relative to the nearest road width.";
            snapDistance.PGClampValue();
            snapHeight.tooltip = "Snap distance to existing roads/intersections in y world units.";
            snapHeight.PGClampValue();
            snapAngleIntersection.PGClampValue();
            snapAngleIntersection.tooltip = "Snap angle in degrees to existing roads/intersections.";

            minAngleIntersection.PGClampValue();
            minAngleIntersection.tooltip = "The minimum angle required for roads to exit an intersection towards the nearest existing road.\n" +
                                           "When this angle is reached, the road will curve.";

            distanceRatioAngleCurve.tooltip =
                "Determines how new roads are curved when connected to intersections.\n" + "\n" +
                "X-Axis: Road Length Forward / Road Length Right\n" + "\n" +
                "Y-Axis: Angle in degrees.";

            splineLengthUV.tooltip = "Determines how UVs are handled over the length of the road.\n" + "\n" +
                                   "Stretch: The UVs are stretched to fit the nearest full size of the road.\n" + "\n" +
                                   "Cut: The UVs maintain their original size and are cut when the road ends.";
            smoothSlope.tooltip = "Whether to have hard or smoothed edges on elevated roads.";

            intersectionDistance.tooltip = "Distance to intersections.\n" +
                                           "Space is used for example for crosswalks and mesh connections.";
            intersectionDistance.PGClampValue();
            roadEnd.tooltip = "How road ends are constructed when not connected to intersections.";

            elevationStartHeight.tooltip = "Distance from the road to the ground above this level is considered elevated.";
            elevatedIntersections.tooltip = "Allow elevated intersections.";

            roadLength.PGClampValue();
            roadLength.tooltip = "Min. to Max. length of a road.";
            roadLength.PGVector2ComponentLabel("Min", "Max");
            minOverlapDistance.PGClampValue();
            minOverlapDistance.tooltip = "Minimum required distance between the road and overlapping roads/intersections.";
            minOverlapHeight.PGClampValue();
            minOverlapHeight.tooltip = "Minimum required height difference between the road and overlapping roads/intersections.";
            maxCurvature.PGClampValue(0f, 180f);
            maxCurvature.tooltip = "The maximum curvature of the road.";
            maxSlope.PGClampValue(0f, 90f);
            maxSlope.tooltip = "Maximum slope in degrees.";
            heightRange.tooltip = "Construction is only allowed within this distance to the ground.\n" + "\n" +
                                  "If you have a Min. value below 0, the road could be underneath the ground.\n" +
                                  "Tip: The Terrain options could be helpful.";
            heightRange.PGVector2ComponentLabel("Min", "Max");

            terrainSettings.tooltip = "Apply terrain modifications.";
            terrain.tooltip = "Terrain";
            terrains.tooltip = "Terrains";
            removeDetails.tooltip = "Removes terrain details along the road.";
            removeTrees.tooltip = "Removes terrain trees along the road.";
            levelHeight.tooltip = "Adjusts the terrain height to match the road height, up to the elevation height.";
            slopeSmooth.tooltip = "The width of the slopes resulting from terrain fitting, measured in heightmap pixels.";
            slopeTextureIndex.tooltip = "Terrain texture to be used for slopes resulting from terrain fitting. Set to -1 to disable.";
            slopeTextureIndex.PGClampValue(-1);
            slopeTextureName.tooltip = "Name of the terrain texture being used for slopes.";
            slopeTextureStrength.tooltip = "Strenght of the texture layer on the terrain.";

            addTrafficComponent.tooltip = "Adds the 'Traffic' component to roads/intersections.\n\n" +
                                          "This component can be useful for custom traffic systems as it adds additional splines for each lane," +
                                          "which can be added in the 'Traffic Lanes' on the roads.";
            updateWaypoints.tooltip = "Updates the waypoints automatically for each road/intersection after construction, undo and demolish.\n\n" +
                                      "Waypoints can be retrieved from each road/intersection by calling GetTrafficLanes() first and " +
                                      "then calling GetWaypoints() on each lane.\n\n" +
                                      "Or to get all waypoints from the system, simply call roadConstructor.GetWaypoints()";
            waypointDistance.PGClampValue();
            waypointDistance.PGVector2ComponentLabel("Min", "Max");
            waypointDistance.tooltip = "Maximum distance between each waypoint, based on curvature.\n" +
                                       "Intersections always use the minimum value.";
            waypointGizmoSize.PGClampValue();
            waypointConnectionsOnly.tooltip = "Draw only the start and end waypoints. Helpful for improving editor performance.";

            waypointGizmos.tooltip = "Whether to draw gizmos for waypoints in the scene.";
            waypointGizmosColor.tooltip = "Based on scene objects or a unique color for each traffic lane.";
            waypointConnectionsOnly.PGToggleStyleDefault();

            addPartsMenu.PGRemoveMenuArrow(true, false);
            addPartsMenu.tooltip = "Add a new part.";
            partsMenu.tooltip = "Show additional options.";
            roadsToggle.tooltip = "Road Setup\n" +
                                  "(click to expand/collapse)\n" + "\n" +
                                  "These are the roads that are built into the scene.";
            lanePresetsToggle.tooltip = "Construction Lane Presets\n" +
                                        "(click to expand/collapse)\n" + "\n" +
                                        "One preset can be added to multiple roads.\n\n" +
                                        "Construction lanes are used for the construction of the roads.";
            spawnObjectPresetsToggle.tooltip = "Spawn Object Presets\n" +
                                               "(click to expand/collapse)\n" + "\n" +
                                               "One preset can be added to multiple roads.\n\n" +
                                               "These are additional objects which can be spawned onto the roads.";
            trafficLanePresetsToggle.tooltip = "Traffic Lane Presets\n" +
                                               "(click to expand/collapse)\n" + "\n" +
                                               "One preset can be added to multiple roads.\n\n" +
                                               "Traffic lanes operate independently from construction and can be optionally utilized for custom traffic systems. " +
                                               "Each lane is associated with a Unity Spline.\n\n" +
                                               "Remember to toggle the 'Add Traffic Component' setting if you want to add them automatically to new roads.";

            AddLayerWarningBox(addColliderLayer);
            AddLayerWarningBox(groundLayers);

            void AddLayerWarningBox(VisualElement element)
            {
                var layerWarningBox = new HelpBox(
                    "The road layer should not be included in the ground layers to avoid interference with object spawning and ground detection.",
                    HelpBoxMessageType.Warning);
                layerWarningBox.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                element.Add(layerWarningBox);
            }
        }


        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public override VisualElement CreateInspectorGUI()
        {
            DrawIntegrations();
            DrawTopToolbar();
            DrawRoadPreview();
            DrawColliderWarning();
            DrawLODList();
            DrawTerrain();
            DrawTrafficSystem();
            DrawRoadSet();

            return container;
        }

        /********************************************************************************************************************************/

        private void DrawIntegrations()
        {
            integrationsToggle = container.Q<ToolbarToggle>(nameof(integrationsToggle));
            IntegrationsGroup = container.Q<GroupBox>(nameof(IntegrationsGroup));
            IntegrationsParent = IntegrationsGroup.Q<VisualElement>(nameof(IntegrationsParent));
            
            integrationsToggle.tooltip = "Integrations";
            
            integrationsToggle.RegisterValueChangedCallback(evt =>
            {
                roadConstructor._editorDisplay = EditorDisplay.Integrations;
                EditorDisplayVisibility();
            });
            
            ConstructionEditorUtility.CreateIntegrationsEditor(serializedObject, IntegrationsParent, roadConstructor.integrations, componentSettingsProperty);
        }
        
        /********************************************************************************************************************************/

        private void DrawTopToolbar()
        {
            documentation.clicked += () => { Application.OpenURL(Constants.DocumentationURL); };

            EditorDisplayVisibility();

            componentSettingsToggle.RegisterValueChangedCallback(evt =>
            {
                roadConstructor._editorDisplay = EditorDisplay.ComponentSettings;
                EditorDisplayVisibility();
            });

            roadSetToggle.RegisterValueChangedCallback(evt =>
            {
                roadConstructor._editorDisplay = EditorDisplay.RoadSet;
                EditorDisplayVisibility();
                AddTrafficComponentHelpBox();
            });

            createNewSet.clicked += () =>
            {
                var newFile = CreateInstance<RoadSet>();

                var defaultDirectory = "Assets/";

                var defaultFileName = "New Road Set";
                var extension = "asset";

                var filePath = EditorUtility.SaveFilePanel("Create Road Set", defaultDirectory, defaultFileName, extension);
                if (string.IsNullOrEmpty(filePath)) return;

                var assetPath = "Assets" + filePath.Substring(Application.dataPath.Length);

                AssetDatabase.CreateAsset(newFile, assetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                roadConstructor._RoadSet = newFile;
                EditorUtility.SetDirty(roadConstructor);
            };
        }

        private void EditorDisplayVisibility()
        {
            if (roadConstructor._editorDisplay == EditorDisplay.ComponentSettings)
            {
                componentSettingsToggle.SetValueWithoutNotify(true);
                roadSetToggle.SetValueWithoutNotify(false);
                integrationsToggle.SetValueWithoutNotify(false);
            }

            if (roadConstructor._editorDisplay == EditorDisplay.RoadSet)
            {
                roadSetToggle.SetValueWithoutNotify(true);
                componentSettingsToggle.SetValueWithoutNotify(false);
                integrationsToggle.SetValueWithoutNotify(false);
            }
            
            if (roadConstructor._editorDisplay == EditorDisplay.Integrations)
            {
                roadSetToggle.SetValueWithoutNotify(false);
                componentSettingsToggle.SetValueWithoutNotify(false);
                integrationsToggle.SetValueWithoutNotify(true);
            }

            ComponentSettingsGroup.PGDisplayStyleFlex(roadConstructor._editorDisplay == EditorDisplay.ComponentSettings);
            RoadSetSettingsGroup.PGDisplayStyleFlex(roadConstructor._editorDisplay == EditorDisplay.RoadSet);
            IntegrationsGroup.PGDisplayStyleFlex(roadConstructor._editorDisplay == EditorDisplay.Integrations);
        }

        /********************************************************************************************************************************/

        private void DrawRoadPreview()
        {
            createPreviewButton.clicked += () =>
            {
                var position = Vector3.zero;
                var previewParent = FindFirstObjectByType<PreviewRoadParent>();
                if(previewParent != null)
                {
                    position = previewParent.transform.position;
                    DestroyImmediate(previewParent.gameObject);
                }
                var previewParentObj = new GameObject(Constants.RoadPreviewParent);
                previewParent = previewParentObj.AddComponent<PreviewRoadParent>();
                roadConstructor.ConstructPreviewRoads(previewParent.transform, roadConstructor.roadPreviewLength, 
                    roadConstructor.roadPreviewSpawnObjects, roadConstructor.roadPreviewElevated);
                previewParentObj.transform.position = position;
            };
            focusPreviewButton.clicked += () =>
            {
                var previewParent = FindFirstObjectByType<PreviewRoadParent>();
                if (previewParent == null) return;
                Selection.activeGameObject = previewParent.gameObject;
                SceneView.lastActiveSceneView.FrameSelected();
                Selection.activeGameObject = roadConstructor.gameObject;
            };
            removePreviewButton.clicked += () =>
            {
                var previewParent = FindFirstObjectByType<PreviewRoadParent>();
                if(previewParent != null) DestroyImmediate(previewParent.gameObject);
            };
        }
        
        /********************************************************************************************************************************/

        private void DrawColliderWarning()
        {
            ColliderWarningDisplay();
            addCollider.RegisterValueChangedCallback(evt => { ColliderWarningDisplay(); });
            addColliderLayer.RegisterValueChangedCallback(evt => { ColliderWarningDisplay(); });
            groundLayers.RegisterValueChangedCallback(evt => { ColliderWarningDisplay(); });
        }

        private void ColliderWarningDisplay()
        {
            var displayBox = false;
            var settings = roadConstructor.componentSettings;

            if (settings.addCollider != AddCollider.None)
            {
                var singleLayerMask = 1 << settings.addColliderLayer;
                if ((settings.groundLayers.value & singleLayerMask) != 0) displayBox = true;
            }

            SetBoxDisplay(addColliderLayer);
            SetBoxDisplay(groundLayers);

            void SetBoxDisplay(VisualElement element)
            {
                var layerWarningBox = element.Q<HelpBox>();
                layerWarningBox.PGDisplayStyleFlex(displayBox);
            }
        }

        /********************************************************************************************************************************/
        private void DrawLODList()
        {
            var lodListProperty = componentSettingsProperty.FindPropertyRelative(nameof(ComponentSettings.lodList));

            lodList.showBoundCollectionSize = false;
            lodList.itemsSource = roadConstructor.componentSettings.lodList;
            lodList.PGObjectListViewStyle();
            lodList.reorderable = false;
            lodList.reorderMode = ListViewReorderMode.Animated;
            lodList.headerTitle = "LOD";
            lodList.showFoldoutHeader = true;

            lodList.makeItem = () =>
            {
                var item = new VisualElement();

                var slider = new Slider();

                item.Add(slider);
                return item;
            };

            lodList.bindItem = (element, index) =>
            {
                lodListProperty.serializedObject.Update();
                var slider = element.Q<Slider>();
                slider.tooltip = "Transition (inverted % of screen) to the next item.\n" + "For the last item, this is the transition to cull.";
                slider.showInputField = true;
                slider.lowValue = 0f;
                slider.highValue = 0.999f;
                slider.label = "LOD" + index;
                slider.style.marginRight = 3f;
                slider.BindProperty(lodListProperty.GetArrayElementAtIndex(index));
            };
        }


        /********************************************************************************************************************************/

        private void DrawTerrain()
        {
            var settings = roadConstructor.componentSettings;

            // Obsolete Terrain
            if (settings.terrain == null) terrain.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            TerrainFields.PGDisplayStyleFlex(settings.terrainSettings);
            terrainSettings.RegisterValueChangedCallback(evt => { TerrainFields.PGDisplayStyleFlex(settings.terrainSettings); });

            LevelTerrainHeight.PGDisplayStyleFlex(settings.levelHeight);
            levelHeight.RegisterValueChangedCallback(evt => { LevelTerrainHeight.PGDisplayStyleFlex(settings.levelHeight); });

            SetTextureName();
            slopeTextureIndex.RegisterValueChangedCallback(evt => SetTextureName());
            terrain.RegisterValueChangedCallback(evt => SetTextureName());

            void SetTextureName()
            {
                slopeTextureStrength.PGDisplayStyleFlex(settings.slopeTextureIndex >= 0);
                slopeSmooth.PGDisplayStyleFlex(settings.slopeTextureIndex >= 0);

                if (settings.terrains.Count == 0 || settings.terrains[0] == null || settings.terrains[0].terrainData == null || slopeTextureIndex.value < 0
                                             || slopeTextureIndex.value >= settings.terrains[0].terrainData.terrainLayers.Length)
                {
                    slopeTextureName.value = "n.a.";
                    return;
                }

                slopeTextureName.value = settings.terrains[0].terrainData.terrainLayers[slopeTextureIndex.value]?.name;
            }
        }

        /********************************************************************************************************************************/

        private void DrawTrafficSystem()
        {
            var settings = roadConstructor.componentSettings;

            TrafficSystemFields.PGDisplayStyleFlex(settings.addTrafficComponent);
            addTrafficComponent.RegisterValueChangedCallback(evt => { TrafficSystemFields.PGDisplayStyleFlex(settings.addTrafficComponent); });

            WaypointGizmosFields.PGDisplayStyleFlex(settings.waypointGizmos != DrawGizmos.None);
            waypointGizmos.RegisterValueChangedCallback(evt =>
            {
                WaypointGizmosFields.PGDisplayStyleFlex(settings.waypointGizmos != DrawGizmos.None);
            });
        }

        /********************************************************************************************************************************/
        private void DrawRoadSet()
        {
            roadSet.RegisterValueChangedCallback(evt =>
            {
                if (roadConstructor._RoadSet == null)
                {
                    Construction.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                }
                else
                {
                    Construction.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
                    DrawRoadSetInternal();
                }
            });
        }

        private void DrawRoadSetInternal()
        {
            roadSetSerializedObject = new SerializedObject(roadConstructor._RoadSet);

            DrawPartsToolbar();
            DrawRoads();
            DrawLanePresets();
            DrawSpawnObjectPresets();
            DrawTrafficLanePresets();
        }

        /********************************************************************************************************************************/

        private void DrawPartsToolbar()
        {
            PartsToolbarVisibility();
            DrawAddRemoveParts();


            roadsToggle.RegisterValueChangedCallback(evt =>
            {
                if (roadConstructor._editorActivePartType != PartType.Roads)
                {
                    roadConstructor._editorActivePartType = PartType.Roads;
                    EditorUtility.SetDirty(roadConstructor);
                    roadsToggle.SetValueWithoutNotify(false);
                }
                else
                {
                    for (var i = 0; i < roadConstructor._RoadSet.roads.Count; i++)
                    {
                        var road = roadConstructor._RoadSet.roads[i];
                        road._editorVisible = roadsToggle.value;
                    }
                }

                EditorUtility.SetDirty(roadConstructor._RoadSet);

                DrawRoads();
                PartsToolbarVisibility();
                DrawAddRemoveParts();
            });

            lanePresetsToggle.RegisterValueChangedCallback(evt =>
            {
                if (roadConstructor._editorActivePartType != PartType.LanePreset)
                {
                    roadConstructor._editorActivePartType = PartType.LanePreset;
                    EditorUtility.SetDirty(roadConstructor);
                    lanePresetsToggle.SetValueWithoutNotify(false);
                }
                else
                {
                    for (var i = 0; i < roadConstructor._RoadSet.lanePresets.Count; i++)
                    {
                        var preset = roadConstructor._RoadSet.lanePresets[i];
                        preset._editorVisible = lanePresetsToggle.value;
                    }
                }

                EditorUtility.SetDirty(roadConstructor._RoadSet);

                DrawLanePresets();
                PartsToolbarVisibility();
                DrawAddRemoveParts();
            });

            spawnObjectPresetsToggle.RegisterValueChangedCallback(evt =>
            {
                if (roadConstructor._editorActivePartType != PartType.SpawnObjectPreset)
                {
                    roadConstructor._editorActivePartType = PartType.SpawnObjectPreset;
                    EditorUtility.SetDirty(roadConstructor);
                    spawnObjectPresetsToggle.SetValueWithoutNotify(false);
                }
                else
                {
                    for (var i = 0; i < roadConstructor._RoadSet.spawnObjectPresets.Count; i++)
                    {
                        var preset = roadConstructor._RoadSet.spawnObjectPresets[i];
                        preset._editorVisible = spawnObjectPresetsToggle.value;
                    }
                }

                EditorUtility.SetDirty(roadConstructor._RoadSet);

                DrawSpawnObjectPresets();
                PartsToolbarVisibility();
                DrawAddRemoveParts();
            });

            trafficLanePresetsToggle.RegisterValueChangedCallback(evt =>
            {
                if (roadConstructor._editorActivePartType != PartType.TrafficLanePreset)
                {
                    roadConstructor._editorActivePartType = PartType.TrafficLanePreset;
                    EditorUtility.SetDirty(roadConstructor);
                    trafficLanePresetsToggle.SetValueWithoutNotify(false);
                }
                else
                {
                    for (var i = 0; i < roadConstructor._RoadSet.trafficLanePresets.Count; i++)
                    {
                        var preset = roadConstructor._RoadSet.trafficLanePresets[i];
                        preset._editorVisible = trafficLanePresetsToggle.value;
                    }
                }

                EditorUtility.SetDirty(roadConstructor._RoadSet);

                DrawTrafficLanePresets();
                PartsToolbarVisibility();
                DrawAddRemoveParts();
            });
        }

        private void PartsToolbarVisibility()
        {
            if (roadConstructor._editorActivePartType != PartType.Roads) roadsToggle.SetValueWithoutNotify(false);
            if (roadConstructor._editorActivePartType != PartType.LanePreset) lanePresetsToggle.SetValueWithoutNotify(false);
            if (roadConstructor._editorActivePartType != PartType.SpawnObjectPreset) spawnObjectPresetsToggle.SetValueWithoutNotify(false);
            if (roadConstructor._editorActivePartType != PartType.TrafficLanePreset) trafficLanePresetsToggle.SetValueWithoutNotify(false);

            RoadsParent.PGDisplayStyleFlex(roadConstructor._editorActivePartType == PartType.Roads);
            LanePresetsParent.PGDisplayStyleFlex(roadConstructor._editorActivePartType == PartType.LanePreset);
            SpawnObjectPresetsParent.PGDisplayStyleFlex(roadConstructor._editorActivePartType == PartType.SpawnObjectPreset);
            TrafficLanePresetsParent.PGDisplayStyleFlex(roadConstructor._editorActivePartType == PartType.TrafficLanePreset);

            SetModuleStyle(roadsToggle, roadConstructor._editorActivePartType == PartType.Roads);
            SetModuleStyle(lanePresetsToggle, roadConstructor._editorActivePartType == PartType.LanePreset);
            SetModuleStyle(spawnObjectPresetsToggle, roadConstructor._editorActivePartType == PartType.SpawnObjectPreset);
            SetModuleStyle(trafficLanePresetsToggle, roadConstructor._editorActivePartType == PartType.TrafficLanePreset);

            void SetModuleStyle(ToolbarToggle toggle, bool active)
            {
                toggle.style.backgroundColor = !active ? PGColors.ToolbarButtonBackground() : PGColors.ButtonBackground();
            }
        }

        private void DrawAddRemoveParts()
        {
            addPartsMenu.menu.MenuItems().Clear();
            partsMenu.menu.MenuItems().Clear();

            if (roadConstructor._editorActivePartType == PartType.Roads)
            {
                addPartsMenu.menu.AppendAction("Add Road", _ =>
                {
                    roadConstructor._RoadSet.roads.Add(new Road());
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawRoads();
                });

                partsMenu.menu.AppendAction("Remove All Roads", _ =>
                {
                    if (EditorUtility.DisplayDialog("Remove All Roads", "Are you sure you want to remove all roads from the Road Set: "
                                                                        + roadConstructor._RoadSet.name + "?", "Ok", "Cancel"))
                    {
                        roadConstructor._RoadSet.roads.Clear();
                        EditorUtility.SetDirty(roadConstructor._RoadSet);
                        DrawRoads();
                    }
                });
            }

            else if (roadConstructor._editorActivePartType == PartType.LanePreset)
            {
                addPartsMenu.menu.AppendAction("Add Lane Preset", _ =>
                {
                    roadConstructor._RoadSet.lanePresets.Add(new LanePreset());
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawLanePresets();
                });

                partsMenu.menu.AppendAction("Remove All Lane Presets", _ =>
                {
                    if (EditorUtility.DisplayDialog("Remove All Lane Presets", "Are you sure you want to remove all lane presets from the Road Set: "
                                                                               + roadConstructor._RoadSet.name + "?", "Ok", "Cancel"))
                    {
                        roadConstructor._RoadSet.lanePresets.Clear();
                        EditorUtility.SetDirty(roadConstructor._RoadSet);
                        DrawLanePresets();
                    }
                });
            }

            else if (roadConstructor._editorActivePartType == PartType.SpawnObjectPreset)
            {
                addPartsMenu.menu.AppendAction("Add Object Preset", _ =>
                {
                    roadConstructor._RoadSet.spawnObjectPresets.Add(new SpawnObjectPreset());
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawSpawnObjectPresets();
                });

                partsMenu.menu.AppendAction("Remove All Object Preset", _ =>
                {
                    if (EditorUtility.DisplayDialog("Remove All Object Preset",
                            "Are you sure you want to remove all object presets from the Road Set: "
                            + roadConstructor._RoadSet.name + "?", "Ok", "Cancel"))
                    {
                        roadConstructor._RoadSet.spawnObjectPresets.Clear();
                        EditorUtility.SetDirty(roadConstructor._RoadSet);
                        DrawSpawnObjectPresets();
                    }
                });
            }
            else if (roadConstructor._editorActivePartType == PartType.TrafficLanePreset)
            {
                addPartsMenu.menu.AppendAction("Add Traffic Lane Preset", _ =>
                {
                    roadConstructor._RoadSet.trafficLanePresets.Add(new TrafficLanePreset());
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawTrafficLanePresets();
                });

                partsMenu.menu.AppendAction("Remove All Traffic Lane Presets", _ =>
                {
                    if (EditorUtility.DisplayDialog("Remove All Traffic Lane Presets",
                            "Are you sure you want to remove all traffic lane presets from the Road Set: " + roadConstructor._RoadSet.name + "?",
                            "Ok", "Cancel"))
                    {
                        roadConstructor._RoadSet.trafficLanePresets.Clear();
                        EditorUtility.SetDirty(roadConstructor._RoadSet);
                        DrawTrafficLanePresets();
                    }
                });
            }
        }

        /********************************************************************************************************************************/


        private void DrawRoads()
        {
            RoadsParent.Clear();
            if (roadConstructor._RoadSet == null) return;
            if (roadConstructor._editorActivePartType != PartType.Roads) return;

            var areAllVisible = roadConstructor._RoadSet.roads.All(road => road._editorVisible);
            if (areAllVisible) roadsToggle.value = true;

            var roadsProperty = roadSetSerializedObject.FindProperty(nameof(RoadSet.roads));
            roadsProperty.serializedObject.Update();

            for (var i = 0; i < roadConstructor._RoadSet.roads.Count; i++)
            {
                var road = roadConstructor._RoadSet.roads[i];
                var itemParent = PGModuleEditorUtility.CreateItemParentWithToggle(road, i);
                var toolbar = itemParent.Q<Toolbar>(PGModuleEditorUtility.Toolbar + i);
                var itemToggle = itemParent.Q<ToolbarToggle>(PGModuleEditorUtility.ItemToggle + i);
                var itemMenu = itemParent.Q<ToolbarMenu>(PGModuleEditorUtility.ItemMenu + i);
                var itemPropertyParent = itemParent.Q<GroupBox>(PGModuleEditorUtility.ItemPropertyParent + i);
                toolbar.style.height = 24f;

                var allLanes = road.GetAllLanes(roadConstructor._RoadSet.lanePresets);
                itemToggle.text += road.GetRoadDisplayText(allLanes);

                itemToggle.value = road._editorVisible;
                itemPropertyParent.PGDisplayStyleFlex(road._editorVisible);
                itemToggle.RegisterValueChangedCallback(evt =>
                {
                    road._editorVisible = !road._editorVisible;
                    itemPropertyParent.PGDisplayStyleFlex(road._editorVisible);
                    DrawRoads();
                });

                var i1 = i;

                itemMenu.PGAppendMoveItems(roadConstructor._RoadSet.roads, i1, () =>
                {
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawRoads();
                });
                itemMenu.menu.AppendAction("Duplicate", action =>
                {
                    var roadOld = roadConstructor._RoadSet.roads[i1];
                    var duplicate = PGClassUtility.CopyClass(roadOld) as Road;
                    
                    duplicate!.roadName = roadOld.roadName + " (copy)";

                    duplicate.splineEdgesEditor = new List<SplineEdgeEditor>();
                    for (var j = 0; j < roadOld.splineEdgesEditor.Count; j++)
                    {
                        var splineEdgeCopy = PGClassUtility.CopyClass(roadOld.splineEdgesEditor[j]);
                        duplicate.splineEdgesEditor.Add(splineEdgeCopy as SplineEdgeEditor);
                    }

                    duplicate.spawnObjects = new List<SpawnObject>();
                    for (var j = 0; j < roadOld.spawnObjects.Count; j++)
                    {
                        var spawnObjectCopy = PGClassUtility.CopyClass(roadOld.spawnObjects[j]);
                        duplicate.spawnObjects.Add(spawnObjectCopy as SpawnObject);
                    }

                    duplicate.trafficLanes = new List<TrafficLaneEditor>();
                    for (var j = 0; j < roadOld.trafficLanes.Count; j++)
                    {
                        var spawnObjectCopy = PGClassUtility.CopyClass(roadOld.trafficLanes[j]);
                        duplicate.trafficLanes.Add(spawnObjectCopy as TrafficLaneEditor);
                    }

                    roadConstructor._RoadSet.roads.Insert(i1 + 1, duplicate);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawRoads();
                });
                itemMenu.menu.AppendAction("Remove", action =>
                {
                    roadConstructor._RoadSet.roads.RemoveAt(i1);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawRoads();
                });

                /********************************************************************************************************************************/
                var itemProperty = roadsProperty.GetArrayElementAtIndex(i);
                var roadItem = road.CreatePropertyGUI(road, itemProperty);
                /********************************************************************************************************************************/

                itemPropertyParent.Add(roadItem);
                
                RoadsParent.Add(itemParent);
            }
        }

        /********************************************************************************************************************************/

        private void DrawLanePresets()
        {
            LanePresetsParent.Clear();
            if (roadConstructor._RoadSet == null) return;
            if (roadConstructor._editorActivePartType != PartType.LanePreset) return;

            var areAllVisible = roadConstructor._RoadSet.lanePresets.All(template => template._editorVisible);
            if (areAllVisible) lanePresetsToggle.value = true;

            var templatesProperty = roadSetSerializedObject.FindProperty(nameof(RoadSet.lanePresets));
            templatesProperty.serializedObject.Update();

            for (var i = 0; i < roadConstructor._RoadSet.lanePresets.Count; i++)
            {
                var template = roadConstructor._RoadSet.lanePresets[i];
                var itemParent = PGModuleEditorUtility.CreateItemParentWithToggle(template, i);
                var toolbar = itemParent.Q<Toolbar>(PGModuleEditorUtility.Toolbar + i);
                var itemToggle = itemParent.Q<ToolbarToggle>(PGModuleEditorUtility.ItemToggle + i);
                var itemMenu = itemParent.Q<ToolbarMenu>(PGModuleEditorUtility.ItemMenu + i);
                var itemPropertyParent = itemParent.Q<GroupBox>(PGModuleEditorUtility.ItemPropertyParent + i);
                toolbar.style.height = 24f;

                var _category = template.category;
                if (!string.IsNullOrEmpty(_category)) _category += " | ";
                var _templateName = template.templateName;
                if (string.IsNullOrEmpty(_templateName)) _templateName = "Lane Set";
                itemToggle.text = _category + _templateName;

                itemToggle.value = template._editorVisible;
                itemPropertyParent.PGDisplayStyleFlex(template._editorVisible);
                itemToggle.RegisterValueChangedCallback(evt =>
                {
                    template._editorVisible = !template._editorVisible;
                    itemPropertyParent.PGDisplayStyleFlex(template._editorVisible);
                    DrawLanePresets();
                });

                var i1 = i;

                itemMenu.PGAppendMoveItems(roadConstructor._RoadSet.lanePresets, i1, () =>
                {
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawLanePresets();
                });
                itemMenu.menu.AppendAction("Duplicate", action =>
                {
                    var duplicate = PGClassUtility.CopyClass(roadConstructor._RoadSet.lanePresets[i1]) as LanePreset;
                    duplicate.lanes = new List<SplineEdgeEditor>();
                    for (var j = 0; j < roadConstructor._RoadSet.lanePresets[i1].lanes.Count; j++)
                    {
                        var duplicateItem = PGClassUtility.CopyClass(roadConstructor._RoadSet.lanePresets[i1].lanes[j]) as SplineEdgeEditor;
                        duplicate.lanes.Add(duplicateItem);
                    }

                    roadConstructor._RoadSet.lanePresets.Insert(i1, duplicate);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawLanePresets();
                });
                itemMenu.menu.AppendAction("Remove", action =>
                {
                    roadConstructor._RoadSet.lanePresets.RemoveAt(i1);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawLanePresets();
                });

                /********************************************************************************************************************************/
                var itemProperty = templatesProperty.GetArrayElementAtIndex(i);
                var templateItem = template.CreatePropertyGUI(template, itemProperty);
                /********************************************************************************************************************************/

                itemPropertyParent.Add(templateItem);

                LanePresetsParent.Add(itemParent);
            }
        }


        /********************************************************************************************************************************/

        private void DrawSpawnObjectPresets()
        {
            SpawnObjectPresetsParent.Clear();
            if (roadConstructor._RoadSet == null) return;
            if (roadConstructor._editorActivePartType != PartType.SpawnObjectPreset) return;

            var areAllVisible = roadConstructor._RoadSet.spawnObjectPresets.All(spawnObject => spawnObject._editorVisible);
            if (areAllVisible) spawnObjectPresetsToggle.value = true;

            var spawnObjectsProperty = roadSetSerializedObject.FindProperty(nameof(RoadSet.spawnObjectPresets));
            spawnObjectsProperty.serializedObject.Update();

            for (var i = 0; i < roadConstructor._RoadSet.spawnObjectPresets.Count; i++)
            {
                var spawnObject = roadConstructor._RoadSet.spawnObjectPresets[i];
                var itemParent = PGModuleEditorUtility.CreateItemParentWithToggle(spawnObject, i);
                var toolbar = itemParent.Q<Toolbar>(PGModuleEditorUtility.Toolbar + i);
                var itemToggle = itemParent.Q<ToolbarToggle>(PGModuleEditorUtility.ItemToggle + i);
                var itemMenu = itemParent.Q<ToolbarMenu>(PGModuleEditorUtility.ItemMenu + i);
                var itemPropertyParent = itemParent.Q<GroupBox>(PGModuleEditorUtility.ItemPropertyParent + i);
                toolbar.style.height = 24f;

                var _category = spawnObject.category;
                if (!string.IsNullOrEmpty(_category)) _category += " | ";
                var _templateName = spawnObject.spawnObjectName;
                if (string.IsNullOrEmpty(_templateName)) _templateName = "Object Set";
                itemToggle.text = _category + _templateName;

                itemToggle.value = spawnObject._editorVisible;
                itemPropertyParent.PGDisplayStyleFlex(spawnObject._editorVisible);
                itemToggle.RegisterValueChangedCallback(evt =>
                {
                    spawnObject._editorVisible = !spawnObject._editorVisible;
                    itemPropertyParent.PGDisplayStyleFlex(spawnObject._editorVisible);
                    DrawSpawnObjectPresets();
                });

                var i1 = i;

                itemMenu.PGAppendMoveItems(roadConstructor._RoadSet.spawnObjectPresets, i1, () =>
                {
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawSpawnObjectPresets();
                });
                itemMenu.menu.AppendAction("Duplicate", action =>
                {
                    var duplicate = PGClassUtility.CopyClass(roadConstructor._RoadSet.spawnObjectPresets[i1]) as SpawnObjectPreset;
                    duplicate.spawnObjects = new List<SpawnObject>();
                    for (var j = 0; j < roadConstructor._RoadSet.spawnObjectPresets[i1].spawnObjects.Count; j++)
                    {
                        var duplicateItem = PGClassUtility.CopyClass(roadConstructor._RoadSet.spawnObjectPresets[i1].spawnObjects[j]) as SpawnObject;
                        duplicate.spawnObjects.Add(duplicateItem);
                    }

                    roadConstructor._RoadSet.spawnObjectPresets.Insert(i1, duplicate);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawSpawnObjectPresets();
                });
                itemMenu.menu.AppendAction("Remove", action =>
                {
                    roadConstructor._RoadSet.spawnObjectPresets.RemoveAt(i1);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawSpawnObjectPresets();
                });

                /********************************************************************************************************************************/
                var itemProperty = spawnObjectsProperty.GetArrayElementAtIndex(i);
                var spawnObjectItem = spawnObject.CreatePropertyGUI(spawnObject, itemProperty);
                /********************************************************************************************************************************/

                itemPropertyParent.Add(spawnObjectItem);

                SpawnObjectPresetsParent.Add(itemParent);
            }
        }


        /********************************************************************************************************************************/

        private void DrawTrafficLanePresets()
        {
            TrafficLanePresetsParent.Clear();
            if (roadConstructor._RoadSet == null) return;
            if (roadConstructor._editorActivePartType != PartType.TrafficLanePreset) return;

            AddTrafficComponentHelpBox();

            var areAllVisible = roadConstructor._RoadSet.trafficLanePresets.All(trafficLane => trafficLane._editorVisible);
            if (areAllVisible) trafficLanePresetsToggle.value = true;

            var trafficLanesProperty = roadSetSerializedObject.FindProperty(nameof(RoadSet.trafficLanePresets));
            trafficLanesProperty.serializedObject.Update();

            for (var i = 0; i < roadConstructor._RoadSet.trafficLanePresets.Count; i++)
            {
                var trafficLane = roadConstructor._RoadSet.trafficLanePresets[i];
                var itemParent = PGModuleEditorUtility.CreateItemParentWithToggle(trafficLane, i);
                var toolbar = itemParent.Q<Toolbar>(PGModuleEditorUtility.Toolbar + i);
                var itemToggle = itemParent.Q<ToolbarToggle>(PGModuleEditorUtility.ItemToggle + i);
                var itemMenu = itemParent.Q<ToolbarMenu>(PGModuleEditorUtility.ItemMenu + i);
                var itemPropertyParent = itemParent.Q<GroupBox>(PGModuleEditorUtility.ItemPropertyParent + i);
                toolbar.style.height = 24f;

                var _category = trafficLane.category;
                if (!string.IsNullOrEmpty(_category)) _category += " | ";
                var _templateName = trafficLane.trafficLanePresetName;
                if (string.IsNullOrEmpty(_templateName)) _templateName = "Traffic Lane Set";
                itemToggle.text = _category + _templateName;

                itemToggle.value = trafficLane._editorVisible;
                itemPropertyParent.PGDisplayStyleFlex(trafficLane._editorVisible);
                itemToggle.RegisterValueChangedCallback(evt =>
                {
                    trafficLane._editorVisible = !trafficLane._editorVisible;
                    itemPropertyParent.PGDisplayStyleFlex(trafficLane._editorVisible);
                    DrawTrafficLanePresets();
                });

                var i1 = i;

                itemMenu.PGAppendMoveItems(roadConstructor._RoadSet.trafficLanePresets, i1, () =>
                {
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawTrafficLanePresets();
                });
                itemMenu.menu.AppendAction("Duplicate", action =>
                {
                    var duplicate = PGClassUtility.CopyClass(roadConstructor._RoadSet.trafficLanePresets[i1]) as TrafficLanePreset;
                    duplicate.trafficLanes = new List<TrafficLaneEditor>();
                    for (var j = 0; j < roadConstructor._RoadSet.trafficLanePresets[i1].trafficLanes.Count; j++)
                    {
                        var duplicateItem =
                            PGClassUtility.CopyClass(roadConstructor._RoadSet.trafficLanePresets[i1].trafficLanes[j]) as TrafficLaneEditor;
                        duplicate.trafficLanes.Add(duplicateItem);
                    }

                    roadConstructor._RoadSet.trafficLanePresets.Insert(i1, duplicate);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawTrafficLanePresets();
                });
                itemMenu.menu.AppendAction("Remove", action =>
                {
                    roadConstructor._RoadSet.trafficLanePresets.RemoveAt(i1);
                    EditorUtility.SetDirty(roadConstructor._RoadSet);
                    DrawTrafficLanePresets();
                });

                /********************************************************************************************************************************/
                var itemProperty = trafficLanesProperty.GetArrayElementAtIndex(i);
                var trafficLaneItem = trafficLane.CreatePropertyGUI(trafficLane, itemProperty);
                /********************************************************************************************************************************/

                itemPropertyParent.Add(trafficLaneItem);

                TrafficLanePresetsParent.Add(itemParent);
            }
        }

        private void AddTrafficComponentHelpBox()
        {
            if (roadConstructor._editorActivePartType != PartType.TrafficLanePreset) return;

            if (!roadConstructor.componentSettings.addTrafficComponent)
            {
                var existingHelpBox = TrafficLanePresetsParent.Q<HelpBox>("AddTrafficCompHelpBox");
                if (existingHelpBox != null) return;
                var helpBox = new HelpBox("To add the Traffic component to new roads, the 'Add Traffic Comp.' setting needs to be activated.",
                    HelpBoxMessageType.Warning);
                helpBox.style.marginTop = 6;
                helpBox.name = "AddTrafficCompHelpBox";
                TrafficLanePresetsParent.Insert(0, helpBox);
            }
            else
            {
                var existingHelpBox = TrafficLanePresetsParent.Q<HelpBox>("AddTrafficCompHelpBox");
                if (existingHelpBox != null) TrafficLanePresetsParent.Remove(existingHelpBox);
            }
        }
    }
}