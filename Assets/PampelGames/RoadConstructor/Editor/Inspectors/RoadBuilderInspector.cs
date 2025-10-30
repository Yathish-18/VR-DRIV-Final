// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared;
using PampelGames.Shared.Editor;
using PampelGames.Shared.Utility;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor.Editor
{
    [CustomEditor(typeof(RoadBuilder))]
    public class RoadBuilderInspector : UnityEditor.Editor
    {
        public VisualTreeAsset _visualTree;
        private VisualElement container;
        private RoadBuilder _roadBuilder;

        /********************************************************************************************************************************/

        private ToolbarButton documentation;

        private VisualElement Initialized;
        private ToolbarToggle roadConstructorSetup;
        private GroupBox RoadConstructorGroup;
        private ObjectField roadConstructor;
        private Button initializeButton;
        private Button uninitializeButton;
        private Button registerSceneObjectsButton;
        private Button createTrafficLanesButton;
        private Button createWaypointsButton;
        private Button removeTrafficSystemButton;
        private Button updateCollidersButton;
        private Button updateLayersTagsButton;
        private Toggle checkExistingMeshes;
        private Button exportButton;
        private Button cleanUpConnectionsButton;
        private Button recreateRoadSystemButton;

        private ToolbarToggle road;
        private ToolbarToggle roundabout;
        private ToolbarToggle ramp;
        private ToolbarButton undo;
        private ToolbarToggle demolish;
        private ToolbarToggle move;
        private ToolbarToggle reverseDirection;
        private Label constructionInfoLabel;
        
        private FloatField deltaHeight;
        private EnumField roundAboutDesign;
        private FloatField roundAboutRadius;
        private GroupBox RoadTypeSettings;
        private EnumField directConnection;
        private Slider tangentLength;
        private Toggle parallelRoad;
        private FloatField parallelDistance;

        private Label buildingParameter;
        private Label constructionData;
        private Label constructionFails;

        private EnumField increaseHeight;
        private EnumField decreaseHeight;
        private EnumField increaseRadius;
        private EnumField decreaseRadius;
        private FloatField deltaSpeed;
        private EnumField fixTangent1;
        private EnumField fixTangent2;
        private EnumField detachRoad;
        private Toggle continuous;

        private VisualElement Roads;


        /********************************************************************************************************************************/
        protected void OnEnable()
        {
            container = new VisualElement();
            _visualTree.CloneTree(container);
            _roadBuilder = target as RoadBuilder;

            FindElements(container);
            BindElements();
            VisualizeElements();
        }

        /********************************************************************************************************************************/

        private void FindElements(VisualElement root)
        {
            documentation = root.Q<ToolbarButton>(nameof(documentation));

            checkExistingMeshes = root.Q<Toggle>(nameof(checkExistingMeshes));
            exportButton = root.Q<Button>(nameof(exportButton));

            Initialized = root.Q<VisualElement>(nameof(Initialized));

            roadConstructorSetup = root.Q<ToolbarToggle>(nameof(roadConstructorSetup));
            RoadConstructorGroup = root.Q<GroupBox>(nameof(RoadConstructorGroup));

            roadConstructor = root.Q<ObjectField>(nameof(roadConstructor));

            initializeButton = root.Q<Button>(nameof(initializeButton));
            uninitializeButton = root.Q<Button>(nameof(uninitializeButton));
            registerSceneObjectsButton = root.Q<Button>(nameof(registerSceneObjectsButton));
            createTrafficLanesButton = root.Q<Button>(nameof(createTrafficLanesButton));
            createWaypointsButton = root.Q<Button>(nameof(createWaypointsButton));
            removeTrafficSystemButton = root.Q<Button>(nameof(removeTrafficSystemButton));
            updateCollidersButton = root.Q<Button>(nameof(updateCollidersButton));
            updateLayersTagsButton = root.Q<Button>(nameof(updateLayersTagsButton));
            cleanUpConnectionsButton = root.Q<Button>(nameof(cleanUpConnectionsButton));
            recreateRoadSystemButton = root.Q<Button>(nameof(recreateRoadSystemButton));

            road = root.Q<ToolbarToggle>(nameof(road));
            roundabout = root.Q<ToolbarToggle>(nameof(roundabout));
            ramp = root.Q<ToolbarToggle>(nameof(ramp));
            undo = root.Q<ToolbarButton>(nameof(undo));
            demolish = root.Q<ToolbarToggle>(nameof(demolish));
            move = root.Q<ToolbarToggle>(nameof(move));
            reverseDirection = root.Q<ToolbarToggle>(nameof(reverseDirection));
            constructionInfoLabel = root.Q<Label>(nameof(constructionInfoLabel));

            deltaHeight = root.Q<FloatField>(nameof(deltaHeight));
            roundAboutDesign = root.Q<EnumField>(nameof(roundAboutDesign));
            roundAboutRadius = root.Q<FloatField>(nameof(roundAboutRadius));
            RoadTypeSettings = root.Q<GroupBox>(nameof(RoadTypeSettings));
            directConnection = root.Q<EnumField>(nameof(directConnection));
            tangentLength = root.Q<Slider>(nameof(tangentLength));
            parallelRoad = root.Q<Toggle>(nameof(parallelRoad));
            parallelDistance = root.Q<FloatField>(nameof(parallelDistance));

            buildingParameter = root.Q<Label>(nameof(buildingParameter));
            constructionData = root.Q<Label>(nameof(constructionData));
            constructionFails = root.Q<Label>(nameof(constructionFails));

            increaseHeight = root.Q<EnumField>(nameof(increaseHeight));
            decreaseHeight = root.Q<EnumField>(nameof(decreaseHeight));
            increaseRadius = root.Q<EnumField>(nameof(increaseRadius));
            decreaseRadius = root.Q<EnumField>(nameof(decreaseRadius));
            deltaSpeed = root.Q<FloatField>(nameof(deltaSpeed));
            fixTangent1 = root.Q<EnumField>(nameof(fixTangent1));
            fixTangent2 = root.Q<EnumField>(nameof(fixTangent2));
            detachRoad = root.Q<EnumField>(nameof(detachRoad));
            continuous = root.Q<Toggle>(nameof(continuous));


            Roads = root.Q<VisualElement>(nameof(Roads));
        }

        private void BindElements()
        {
            checkExistingMeshes.PGSetupBindProperty(serializedObject, nameof(checkExistingMeshes));

            roadConstructorSetup.PGSetupBindProperty(serializedObject, nameof(RoadBuilder._editorSettingsVisible));
            roadConstructor.PGSetupBindProperty(serializedObject, nameof(roadConstructor));
            
            deltaHeight.PGSetupBindProperty(serializedObject, nameof(deltaHeight));
            roundAboutDesign.PGSetupBindProperty(serializedObject, nameof(RoadBuilder.roundaboutDesign));
            roundAboutRadius.PGSetupBindProperty(serializedObject, nameof(roundAboutRadius));
            parallelRoad.PGSetupBindProperty(serializedObject, nameof(parallelRoad));
            parallelDistance.PGSetupBindProperty(serializedObject, nameof(parallelDistance));

            if (_roadBuilder.roadConstructor)
            {
                var roadConstructorSerializedObject = new SerializedObject(_roadBuilder.roadConstructor);
                var componentSettingsProperty = roadConstructorSerializedObject.FindProperty(nameof(RoadConstructor.componentSettings));
                directConnection.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(RoadConstructor.componentSettings.directConnection));
                tangentLength.PGSetupBindPropertyRelative(componentSettingsProperty, nameof(RoadConstructor.componentSettings.tangentLength));
            }


            increaseHeight.PGSetupBindProperty(serializedObject, nameof(increaseHeight));
            decreaseHeight.PGSetupBindProperty(serializedObject, nameof(decreaseHeight));
            increaseRadius.PGSetupBindProperty(serializedObject, nameof(increaseRadius));
            decreaseRadius.PGSetupBindProperty(serializedObject, nameof(decreaseRadius));
            deltaSpeed.PGSetupBindProperty(serializedObject, nameof(deltaSpeed));
            fixTangent1.PGSetupBindProperty(serializedObject, nameof(fixTangent1));
            fixTangent2.PGSetupBindProperty(serializedObject, nameof(fixTangent2));
            detachRoad.PGSetupBindProperty(serializedObject, nameof(detachRoad));
            continuous.PGSetupBindProperty(serializedObject, nameof(continuous));
        }

        private void VisualizeElements()
        {
            fixTangent1.tooltip =
                "Fixes the start tangent to create curvature. This requires the current road segment to be connected to an existing road or intersection.";
            fixTangent2.tooltip =
                "Fixes the end tangent to create curvature. This requires the current road segment to be connected to an existing road or intersection.";

            roadConstructorSetup.tooltip = "Show/hide builder settings.";

            checkExistingMeshes.tooltip = "Checks the project folder for each mesh before exporting.\n" +
                                          "If set to false, each mesh will be exported regardless of whether one already exists.";
            exportButton.tooltip = "Export road and intersection meshes into the project folder.";

            documentation.tooltip = "Open the documentation page.";

            roadConstructor.objectType = typeof(RoadConstructor);
            roadConstructor.tooltip = "Reference to the Road Constructor component in the scene.";

            initializeButton.tooltip = "Initializes Road Constructor.";
            uninitializeButton.tooltip = "Uninitializes Road Constructor and reparents constructed parts to the scene.";
            registerSceneObjectsButton.tooltip = "Registers existing scene objects for construction.\n" + "\n" +
                                                 "The construction set needs to contain the road for each object to be registered.";
            createTrafficLanesButton.tooltip =
                "Remove existing Traffic components and create new ones for the existing road system.\n" +
                "The traffic component is necessary to create waypoints.";
            createWaypointsButton.tooltip = "Create interconnected waypoints for the existing road system.\n" +
                                            "Requires the traffic component on the constructed roads.";
            removeTrafficSystemButton.tooltip = "Removes all traffic components and waypoints from the system.";
            updateCollidersButton.tooltip = "Update colliders for all registered objects based on the component settings.";
            updateLayersTagsButton.tooltip = "Update layers and tags for registered objects based on the component settings.";
            cleanUpConnectionsButton.tooltip = "Clears missing connections, which can occur if roads are manually removed from the scene.";
            recreateRoadSystemButton.tooltip = "Recreates the entire road system based on the construction set and settings.";

            detachRoad.tooltip = "Resets the positions, which disconnects the displayed roads.\n" +
                                 "Also applicable with right-mouse click.";

            roundAboutRadius.PGClampValue();
            roundAboutDesign.tooltip = "Roundabout design type.\n" +
                                       "The default is a roundabout with an open inner circle, while a cul-de-sac features a continuous ground.";
            parallelRoad.PGToggleStyleDefault();

            road.tooltip = "Creates road and intersection segments.";
            roundabout.tooltip = "Creates roundabouts.";
            ramp.tooltip = "Creates ramps, which are seamless connections to existing roads.";
            move.tooltip = "Moves a constructed intersection in real-time.";
            reverseDirection.tooltip = "Reverses the direction of a constructed road.";
            constructionInfoLabel.PGDisplayStyleFlex(false);
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        private void FocusSceneView()
        {
            if (SceneView.sceneViews.Count <= 0) return;
            var sceneView = (SceneView) SceneView.sceneViews[0];
            sceneView.Focus();
        }

        private void OnSceneGUI()
        {
            Update();
            UpdateMouseSelection();
        }

        private float lastTime;
        private float deltaTime;
        private Vector3 pointerPosition;
        private Vector3 pointerDemolishPosition;

        private bool setTangent01Pressed;
        private bool setTangent02Pressed;

        private void Update()
        {
            if (_roadBuilder.roadConstructor == null) return;
            if (!_roadBuilder.roadConstructor.IsInitialized()) return;

            _roadBuilder.roadConstructor.ClearAllDisplayObjects();

            var ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            if (!Physics.Raycast(ray, out var hit))
            {
                _roadBuilder.SetPointerActive(false);
                _roadBuilder.SetPointerDemolishActive(false);
                return;
            }

            /********************************************************************************************************************************/
            // Demolish
            _roadBuilder.SetPointerDemolishActive(_roadBuilder.IsDemolishActive());

            if (_roadBuilder.IsDemolishActive())
            {
                setTangent01Pressed = false;
                setTangent02Pressed = false;

                var radius = Mathf.Abs(_roadBuilder.roadConstructor.componentSettings.heightRange.y) + 1f;
                pointerDemolishPosition = _roadBuilder.SnapPointerDemolish(radius, hit.point, hit.normal);

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _roadBuilder.roadConstructor.Demolish(pointerDemolishPosition, radius);
                }
                else
                {
                    // Creating display objects, only used for visual representation.
                    var demolishDisplayObjects = _roadBuilder.roadConstructor.DisplayDemolishObjects(pointerDemolishPosition, radius);
                }

                return;
            }

            deltaTime = (float) EditorApplication.timeSinceStartup - lastTime;
            deltaTime *= _roadBuilder.deltaSpeed;
            lastTime = (float) EditorApplication.timeSinceStartup;

            if (Event.current.type == EventType.KeyDown) // Can be used for road construction or move intersection
            {
                var _deltaHeight = _roadBuilder.GetDeltaHeight();
                if (Event.current.keyCode == _roadBuilder.increaseHeight)
                    _roadBuilder.SetDeltaHeight(_deltaHeight + deltaTime);
                else if (Event.current.keyCode == _roadBuilder.decreaseHeight)
                    _roadBuilder.SetDeltaHeight(_deltaHeight - deltaTime);
                else if (Event.current.keyCode == _roadBuilder.increaseRadius)
                    _roadBuilder.SetRadius(_roadBuilder.GetRadius() + deltaTime);
                else if (Event.current.keyCode == _roadBuilder.decreaseRadius)
                    _roadBuilder.SetRadius(_roadBuilder.GetRadius() - deltaTime);
            }

            constructionInfoLabel.PGDisplayStyleFlex(false);
            
            /********************************************************************************************************************************/
            // Move Intersection
            if (_roadBuilder.IsMoveActive())
            {
                constructionInfoLabel.PGDisplayStyleFlex(true);
                
                _roadBuilder.SetPointerActive(true);
                pointerPosition = _roadBuilder.SnapPointer(hit.point);

                if (_roadBuilder.moveStatus == MoveStatus.Select)
                {
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        _roadBuilder.moveStatus = MoveStatus.Move;
                        constructionInfoLabel.text = "Click on the destination";
                        _roadBuilder.movePosition = pointerPosition;
                    }
                }
                else if (_roadBuilder.moveStatus == MoveStatus.Move)
                {
                    var hitPoint = _roadBuilder.ApplyDeltaHeight(hit.point);

                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        var resultMove =
                            _roadBuilder.roadConstructor.MoveIntersection(_roadBuilder.movePosition, _roadBuilder.GetDefaultRadius(), hitPoint);

                        _roadBuilder.SetMoveActive(false);
                        _roadBuilder.SetPointerActive(false);
                        SetConstructionButtonsInactive();
                        SelectRoadBuilderInspector();
                    }
                    else
                    {
                        var resultDisplayMove =
                            _roadBuilder.roadConstructor.DisplayMoveIntersection(_roadBuilder.movePosition, _roadBuilder.GetDefaultRadius(),
                                hitPoint);
                    }
                }

                return;
            }
            
            /********************************************************************************************************************************/
            // Reverse
            if (_roadBuilder.IsReverseDirectionActive())
            {
                constructionInfoLabel.PGDisplayStyleFlex(true);
                
                _roadBuilder.SetPointerActive(true);
                pointerPosition = _roadBuilder.SnapPointer(hit.point);
                
                _roadBuilder.movePosition = pointerPosition;

                if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    _roadBuilder.roadConstructor.ReverseRoadDirection(_roadBuilder.movePosition, _roadBuilder.GetDefaultRadius(), out var reversedRoad);
                        
                    _roadBuilder.SetReverseDirectionActive(false);
                    _roadBuilder.SetPointerActive(false);
                    SetConstructionButtonsInactive();
                    SelectRoadBuilderInspector();
                }

                return;
            }


            /********************************************************************************************************************************/
            // Road Types

            if (_roadBuilder.builderRoadType == BuilderRoadType.None) return;
            var activeRoad = _roadBuilder.GetActiveRoad();
            if (string.IsNullOrEmpty(activeRoad)) return;

            pointerPosition = _roadBuilder.SnapPointer(hit.point);

            ConstructionResult result;
            var roadSettings = new RoadSettings();

            roadSettings.parallelRoad = _roadBuilder.parallelRoad;
            roadSettings.parallelDistance = _roadBuilder.parallelDistance;

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == _roadBuilder.fixTangent1) setTangent01Pressed = true;
                if (Event.current.keyCode == _roadBuilder.fixTangent2) setTangent02Pressed = true;
            }

            if (Event.current.type == EventType.KeyUp)
            {
                if (Event.current.keyCode == _roadBuilder.fixTangent1) setTangent01Pressed = false;
                if (Event.current.keyCode == _roadBuilder.fixTangent2) setTangent02Pressed = false;
            }

            if (setTangent01Pressed)
            {
                roadSettings.setTangent01 = true;
                roadSettings.tangent01 = _roadBuilder.lastTangent01;
            }

            if (setTangent02Pressed)
            {
                roadSettings.setTangent02 = true;
                roadSettings.tangent02 = _roadBuilder.lastTangent02;
            }


            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                if (_roadBuilder.builderRoadType == BuilderRoadType.Road) result = _roadBuilder.ConstructRoad(pointerPosition, roadSettings);
                else if (_roadBuilder.builderRoadType == BuilderRoadType.Roundabout) result = _roadBuilder.ConstructRoundabout(pointerPosition);
                else if (_roadBuilder.builderRoadType == BuilderRoadType.Ramp) result = _roadBuilder.ConstructRamp(pointerPosition, roadSettings);
                else result = null;
            }
            else
            {
                if (_roadBuilder.builderRoadType == BuilderRoadType.Road) result = _roadBuilder.DisplayRoad(pointerPosition, roadSettings);
                else if (_roadBuilder.builderRoadType == BuilderRoadType.Roundabout) result = _roadBuilder.DisplayRoundabout(pointerPosition);
                else if (_roadBuilder.builderRoadType == BuilderRoadType.Ramp) result = _roadBuilder.DisplayRamp(pointerPosition, roadSettings);
                else result = null;
            }

            if (result.isValid && result.GetType() == typeof(ConstructionResultRoad))
            {
                var roadResult = (ConstructionResultRoad) result;
                if (!roadSettings.setTangent01) _roadBuilder.lastTangent01 = roadResult.roadData.tangent01;
                if (!roadSettings.setTangent02) _roadBuilder.lastTangent02 = roadResult.roadData.tangent02;
            }

            if (result.isValid && result.GetType() == typeof(ConstructionResultRamp))
            {
                var rampResult = (ConstructionResultRamp) result;
                if (!roadSettings.setTangent01) _roadBuilder.lastTangent01 = rampResult.roadData.tangent01;
                if (!roadSettings.setTangent02) _roadBuilder.lastTangent02 = rampResult.roadData.tangent02;
            }

            if ((Event.current.type == EventType.MouseDown && Event.current.button == 1) ||
                (Event.current.type == EventType.KeyDown && Event.current.keyCode == _roadBuilder.detachRoad))
                _roadBuilder.ResetValues();

            buildingParameter.text = _roadBuilder.BuildingParameterText();
            constructionData.text = _roadBuilder.ConstructionDataText(result);
            constructionFails.text = _roadBuilder.ConstructionFailText(result);

            InfoLabelsDisplay();
        }

        private void InfoLabelsDisplay()
        {
            buildingParameter.style.display = string.IsNullOrEmpty(buildingParameter.text) || buildingParameter.text == "Elevation: 0"
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            constructionData.style.display = string.IsNullOrEmpty(constructionData.text)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            constructionFails.style.display = string.IsNullOrEmpty(constructionFails.text)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
        }


        // Make sure mouse selection remains on the component.
        private void UpdateMouseSelection()
        {
            var activeRoad = _roadBuilder.GetActiveRoad();
            var demolishActive = _roadBuilder.IsDemolishActive();
            var moveActive = _roadBuilder.IsMoveActive();
            var reverseDirectionActive = _roadBuilder.IsReverseDirectionActive();

            if (string.IsNullOrEmpty(activeRoad) && !demolishActive && !moveActive && !reverseDirectionActive) return;

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                SelectRoadBuilderInspector();
            }
        }

        private void SelectRoadBuilderInspector()
        {
            GUIUtility.hotControl = 0;
            Event.current.Use();
            Selection.activeGameObject = _roadBuilder.gameObject;
        }


        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public override VisualElement CreateInspectorGUI()
        {
            DrawTopToolbar();
            DrawExportSettings();
            DrawButtons();
            InfoLabelsDisplay();
            DrawRoads();

            return container;
        }

        /********************************************************************************************************************************/

        private void DrawTopToolbar()
        {
            documentation.clicked += () => { Application.OpenURL(Constants.DocumentationURL); };

            roadConstructorSetup.RegisterValueChangedCallback(evt =>
            {
                RoadConstructorGroup.PGDisplayStyleFlex(_roadBuilder._editorSettingsVisible);
            });
        }

        /********************************************************************************************************************************/
        private void DrawExportSettings()
        {
            if (!_roadBuilder.roadConstructor) return;
            exportButton.clicked += () => { _roadBuilder.roadConstructor.ExportMeshes(_roadBuilder.checkExistingMeshes); };
        }

        /********************************************************************************************************************************/

        private void DrawButtons()
        {
            InitializedDisplay();

            initializeButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;
                if (_roadBuilder.roadConstructor._RoadSet == null) return;

                _roadBuilder.builderRoadType = BuilderRoadType.Road;
                _roadBuilder.builderOtherType = BuilderOtherType.None;
                EditorUtility.SetDirty(_roadBuilder);

                SetRoadButtonsInactive();
                SetConstructionButtonsInactive();

                _roadBuilder.roadConstructor.Initialize();
                _roadBuilder.InitializePointer();
                InitializedDisplay();
                buildingParameter.text = "";
                constructionData.text = "";
                constructionFails.text = "";
                InfoLabelsDisplay();
            };
            uninitializeButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;

                var constructionParent = _roadBuilder.roadConstructor.GetConstructionParent();
                if (constructionParent && constructionParent.childCount > 0)
                {
                    constructionParent.parent = null;
                    constructionParent.name += " " + DateTime.Now;
                    constructionParent.gameObject.AddComponent<ConstructionParent>();
                }

                SetRoadButtonsInactive();
                SetConstructionButtonsInactive();

                _roadBuilder.roadConstructor.Uninitialize();
                _roadBuilder.DestroyPointers();
                InfoLabelsDisplay();
                InitializedDisplay();
            };
            registerSceneObjectsButton.clicked += () =>
            {
                if (!_roadBuilder.roadConstructor) return;
                _roadBuilder.roadConstructor.RegisterSceneObjects(out var sceneRoadObjects, out var sceneIntersectionObjects);
                Debug.Log(
                    "Registered " + sceneRoadObjects.Count + " roads and " + sceneIntersectionObjects.Count + " intersections for construction.");

                var constructionParents = FindObjectsByType<ConstructionParent>(FindObjectsSortMode.None);
                for (int i = constructionParents.Length - 1; i >= 0; i--)
                {
                    if (constructionParents[i].transform.childCount > 0) continue;
                    DestroyImmediate(constructionParents[i].gameObject);
                }

                CheckConnections();
            };

            createTrafficLanesButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;
                _roadBuilder.roadConstructor.AddTrafficComponents();
                Debug.Log("Traffic components successfully created.");
            };
            createWaypointsButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;
                if (!CheckConnections()) return;
                _roadBuilder.roadConstructor.CreateAllWaypoints();
                Debug.Log("Waypoints successfully created.");
            };
            removeTrafficSystemButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;
                _roadBuilder.roadConstructor.RemoveTrafficSystem();
                Debug.Log("Traffic system cleared successfully.");
            };
            updateCollidersButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;
                var sceneObjects = _roadBuilder.roadConstructor.GetSceneObjects();
                _roadBuilder.roadConstructor.UpdateColliders(sceneObjects);
            };
            updateLayersTagsButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;
                var sceneObjects = _roadBuilder.roadConstructor.GetSceneObjects();
                _roadBuilder.roadConstructor.UpdateLayersAndTags(sceneObjects);
            };

            cleanUpConnectionsButton.clicked += () =>
            {
                if (_roadBuilder.roadConstructor == null) return;
                CleanUpConnections();
            };

            recreateRoadSystemButton.clicked += () =>
            {
                if (EditorUtility.DisplayDialog("Recreate Road System", "Are you sure you want to recreate all registered roads?", "Ok", "Cancel"))
                {
                    var roadSystem = _roadBuilder.roadConstructor.GetSerializableRoadSystem();
                    _roadBuilder.roadConstructor.ClearRoadSystem();
                    _roadBuilder.roadConstructor.AddSerializableRoadSystem(roadSystem);
                }
            };

            /********************************************************************************************************************************/
            // Builder Road Types
            
            road.SetValueWithoutNotify(_roadBuilder.builderRoadType == BuilderRoadType.Road);
            road.RegisterValueChangedCallback(evt =>
            {
                CallbackBuilderRoadType(road, BuilderRoadType.Road);
            });
            
            roundabout.SetValueWithoutNotify(_roadBuilder.builderRoadType == BuilderRoadType.Roundabout);
            roundabout.RegisterValueChangedCallback(evt =>
            {
                CallbackBuilderRoadType(roundabout, BuilderRoadType.Roundabout);
            });
            
            ramp.SetValueWithoutNotify(_roadBuilder.builderRoadType == BuilderRoadType.Ramp);
            ramp.RegisterValueChangedCallback(evt =>
            {
                CallbackBuilderRoadType(ramp, BuilderRoadType.Ramp);
            });
            
            void CallbackBuilderRoadType(ToolbarToggle toolbarToggle, BuilderRoadType _builderRoadType)
            {
                _roadBuilder.builderOtherType = BuilderOtherType.None;
                _roadBuilder.builderRoadType = _builderRoadType;
                EditorUtility.SetDirty(_roadBuilder);
                
                var _active = toolbarToggle.value;
                SetConstructionButtonsInactive();
                toolbarToggle.SetValueWithoutNotify(_active);
                
                constructionInfoLabel.PGDisplayStyleFlex(false);
                
                RoadTypeVisibility();
                FocusSceneView();
            }
            
            
            /********************************************************************************************************************************/
            // Builder Other Types
            
            undo.clicked += () =>
            {
                if (_roadBuilder.roadConstructor) _roadBuilder.roadConstructor.UndoLastConstruction();
            };

            demolish.SetValueWithoutNotify(_roadBuilder.IsDemolishActive());
            demolish.RegisterValueChangedCallback(evt =>
            {
                var _active = demolish.value;
                _roadBuilder.SetDemolishActive(_active);
                CallbackBuilderOtherType(demolish);
            });

            move.SetValueWithoutNotify(_roadBuilder.IsMoveActive());
            move.RegisterValueChangedCallback(evt =>
            {
                var _active = move.value;
                _roadBuilder.SetMoveActive(_active);
                CallbackBuilderOtherType(move);
                
                constructionInfoLabel.PGDisplayStyleFlex(_roadBuilder.IsMoveActive());
                constructionInfoLabel.text = "Select an intersection in the scene";
            });

            reverseDirection.SetValueWithoutNotify(_roadBuilder.IsReverseDirectionActive());
            reverseDirection.RegisterValueChangedCallback(evt =>
            {
                var _active = reverseDirection.value;
                _roadBuilder.SetReverseDirectionActive(_active);
                CallbackBuilderOtherType(reverseDirection);
                
                constructionInfoLabel.PGDisplayStyleFlex(_roadBuilder.IsReverseDirectionActive());
                constructionInfoLabel.text = "Select a road in the scene to invert";
            });
            
            void CallbackBuilderOtherType(ToolbarToggle toolbarToggle)
            {
                var _active = toolbarToggle.value;

                _roadBuilder.builderRoadType = BuilderRoadType.None;
                EditorUtility.SetDirty(_roadBuilder);
                
                SetRoadButtonsInactive();
                SetConstructionButtonsInactive();
                toolbarToggle.SetValueWithoutNotify(_active);
                
                FocusSceneView();
            }
            
            /********************************************************************************************************************************/
            
            RoadTypeVisibility();
            
            parallelRoad.RegisterValueChangedCallback(evt =>
            {
                parallelDistance.PGDisplayStyleFlex(_roadBuilder.builderRoadType == BuilderRoadType.Road && _roadBuilder.parallelRoad);
            });
        }
        
        private void RoadTypeVisibility()
        {
            roundAboutRadius.PGDisplayStyleFlex(_roadBuilder.builderRoadType == BuilderRoadType.Roundabout);
            roundAboutDesign.PGDisplayStyleFlex(_roadBuilder.builderRoadType == BuilderRoadType.Roundabout);
            RoadTypeSettings.PGDisplayStyleFlex(_roadBuilder.builderRoadType == BuilderRoadType.Road);
            parallelDistance.PGDisplayStyleFlex(_roadBuilder.parallelRoad);
        }
        

        private void SetConstructionButtonsInactive()
        {
            if(_roadBuilder.builderRoadType != BuilderRoadType.Road) road.SetValueWithoutNotify(false);
            if(_roadBuilder.builderRoadType != BuilderRoadType.Roundabout) roundabout.SetValueWithoutNotify(false);
            if(_roadBuilder.builderRoadType != BuilderRoadType.Ramp) ramp.SetValueWithoutNotify(false);
            if(_roadBuilder.builderOtherType != BuilderOtherType.Demolish) demolish.SetValueWithoutNotify(false);
            if(_roadBuilder.builderOtherType != BuilderOtherType.Move) move.SetValueWithoutNotify(false);
            if(_roadBuilder.builderOtherType != BuilderOtherType.Reverse) reverseDirection.SetValueWithoutNotify(false);

            constructionInfoLabel.PGDisplayStyleFlex(false);
        }

        void SetRoadButtonsInactive()
        {
            for (var i = 0; i < _roadBuilder.roadConstructor._RoadSet.roads.Count; i++)
            {
                var roadToggle = Roads.Q<ToolbarToggle>("roadToggle" + i);
                if (roadToggle == null) continue;
                roadToggle.value = false;
            }
        }

        private bool CheckConnections()
        {
            bool connectionsValid = true;
            var roads = _roadBuilder.roadConstructor.GetRoads();
            for (int i = 0; i < roads.Count; i++)
            {
                var roadConnections = roads[i].RoadConnections;
                for (int j = 0; j < roadConnections.Count; j++)
                {
                    if (roadConnections[j] == null)
                    {
                        Debug.LogWarning("The road: " + roads[i].iD + " has a missing road connection.\n" +
                                         "Either remove it manually or use the 'Clean Up Connections' button.");
                        connectionsValid = false;
                    }
                }

                var intersectionConnections = roads[i].IntersectionConnections;
                for (int k = 0; k < intersectionConnections.Count; k++)
                {
                    if (intersectionConnections[k] == null)
                    {
                        Debug.LogWarning("The road: " + roads[i].iD + " has a missing intersection connection.\n" +
                                         "Either remove it manually or use the 'Clean Up Connections' button.");
                        connectionsValid = false;
                    }
                }
            }

            var intersections = _roadBuilder.roadConstructor.GetIntersections();
            for (int i = 0; i < intersections.Count; i++)
            {
                var roadConnections = intersections[i].RoadConnections;
                for (int j = 0; j < roadConnections.Count; j++)
                {
                    if (roadConnections[j] == null)
                    {
                        Debug.LogWarning("The intersection: " + intersections[i].iD + " has a missing road connection.\n" +
                                         "Either remove it manually or use the 'Clean Up Connections' button.");
                        connectionsValid = false;
                    }
                }
            }

            return connectionsValid;
        }

        private void CleanUpConnections()
        {
            var sceneObjects = _roadBuilder.roadConstructor.GetSceneObjects();
            ConnectionUtility.CleanNullConnections(sceneObjects);
            for (int i = 0; i < sceneObjects.Count; i++) EditorUtility.SetDirty(sceneObjects[i]);
            Debug.Log("Connections successfully cleaned.");
        }

        private void InitializedDisplay()
        {
            if (_roadBuilder.gameObject.scene.name == null) return;
            var initialized = _roadBuilder.roadConstructor != null && _roadBuilder.roadConstructor.IsInitialized();
            Initialized.PGDisplayStyleFlex(initialized);
            initializeButton.PGDisplayStyleFlex(!initialized);
        }

        /********************************************************************************************************************************/

        private void DrawRoads()
        {
            Roads.Clear();
            var _roadConstructor = _roadBuilder.roadConstructor;
            if (_roadConstructor == null) return;
            var _constructionSet = _roadConstructor._RoadSet;
            if (_constructionSet == null) return;

            for (var i = 0; i < _constructionSet.roads.Count; i++)
            {
                var road = _constructionSet.roads[i];

                var roadToggle = new ToolbarToggle();
                roadToggle.name = nameof(roadToggle) + i;

                var allLanes = road.GetAllLanes(_roadConstructor._RoadSet.lanePresets);
                roadToggle.text += road.GetRoadDisplayText(allLanes);

                SetRoadStyle(roadToggle);

                var i1 = i;
                roadToggle.RegisterValueChangedCallback(evt =>
                {
                    setTangent01Pressed = false;
                    setTangent02Pressed = false;

                    if (roadToggle.value)
                    {
                        SetConstructionButtonsInactive();

                        _roadBuilder.InitializePointer();
                        _roadBuilder.ActivateRoad(road.roadName);
                        EditorUtility.SetDirty(_roadBuilder);
                        FocusSceneView();
                    }
                    else
                    {
                        _roadBuilder.DeactivateRoad();
                    }

                    var activeRoad = _roadBuilder.GetActiveRoad();


                    for (var j = 0; j < _constructionSet.roads.Count; j++)
                    {
                        if (i1 == j) continue;
                        var innerRoadToggle = Roads.Q<ToolbarToggle>(nameof(roadToggle) + j);
                        if (innerRoadToggle == null) continue;
                        if (innerRoadToggle.text == activeRoad) continue;
                        innerRoadToggle.SetValueWithoutNotify(false);
                    }
                });

                Roads.Add(roadToggle);
            }
        }

        private void SetRoadStyle(ToolbarToggle roadToggle)
        {
            roadToggle.style.height = 33;
            roadToggle.style.marginBottom = 3;
            roadToggle.PGBorderWidth(1);
        }


        /********************************************************************************************************************************/

        private void OnDisable()
        {
            if (_roadBuilder == null) return;
            _roadBuilder.DeactivateRoad();
            _roadBuilder.ResetValues();
            _roadBuilder.SetPointerActive(false);
            _roadBuilder.SetPointerDemolishActive(false);
            EditorUtility.SetDirty(_roadBuilder);
        }
    }
}