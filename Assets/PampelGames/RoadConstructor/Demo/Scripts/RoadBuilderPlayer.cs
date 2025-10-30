using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor.Demo
{
    /// <summary>
    ///     An example script of a Road Builder that works in play mode.
    ///     Its main goal is to show how to access and use Road Constructor during runtime to help you create your own builder.
    /// </summary>
    public class RoadBuilderPlayer : RoadBuilderBase
    {
        /********************************************************************************************************************************/
        // These fields deal with the UI. 
        // For actual construction, take a look at the Update loop.
        /********************************************************************************************************************************/

        public UIDocument uIDocument;

        private VisualElement root;

        private VisualElement UserPanel;
        private bool mouseInsidePanel;
        private VisualElement RoadMenu;
        private VisualElement InfoMenu;
        private Label moveInfoLabel;

        private Button openRoadMenu_rural;
        private Button openRoadMenu_local;
        private Button openRoadMenu_highway;
        private Button openRoadMenu_oneway;

        private Label buildingParameter;
        private Label constructionData;
        private Label constructionFails;

        private List<Button> roadButtons;

        private Button undo;
        private Button demolish;
        private Button move;
        private Button builderRoadTypeButton;

        private const string USS_Button = "button";
        private const string USS_ButtonSelected = "buttonSelected";
        private const string USS_ButtonHover = "button:hover";

        private const string RoadsRural = "Rural";
        private const string RoadsLocal = "Local";
        private const string RoadsHighway = "Highway";
        private const string RoadsOneWay = "OneWay";

        private Vector3 pointerPosition;
        private Vector3 pointerDemolishPosition;

        /********************************************************************************************************************************/
        private void Awake()
        {
            FindMenuElements();
            InitializePointer();
            
            constructionFails.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            InitializeMousePanelHover();
            InitializeButtons();
            if(registerSceneObjects) roadConstructor.RegisterSceneObjects();
            
            builderRoadType = BuilderRoadType.Road;
        }

        /********************************************************************************************************************************/
        // The construction logic is executed here in the Update loop.
        // With a basic understanding of this part, you should be able to create your own builder from scratch.
        // To make it simple, the 'ConstructRoad' method is the method you need for construction.
        // 'DisplayRoad', 'ClearAllDisplayObjects' and the pointer methods are really only used for visual representation.
        /********************************************************************************************************************************/
        private void Update()
        {
            // Destroying the display objects from the last frame, otherwise they would queue up in the scene.
            roadConstructor.ClearAllDisplayObjects();

            if (mouseInsidePanel) return; // Don't construct while mouse is in the UI

            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit))
            {
                SetPointerActive(false);
                SetPointerDemolishActive(false);
                return;
            }

            // Optional: Logic for demolish.
            SetPointerDemolishActive(IsDemolishActive());

            if (IsDemolishActive())
            {
                var radius = GetDefaultRadius();
                pointerDemolishPosition = SnapPointerDemolish(radius, hit.point, hit.normal);

                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    roadConstructor.Demolish(pointerDemolishPosition, radius);
                }
                else
                {
                    // Creating display objects, only used for visual representation.
                    var demolishDisplayObjects = roadConstructor.DisplayDemolishObjects(pointerDemolishPosition, radius);
                }
            }
            
            if (Input.GetKey(increaseHeight)) SetDeltaHeight(deltaHeight + Time.deltaTime * deltaSpeed);
            if (Input.GetKey(decreaseHeight)) SetDeltaHeight(deltaHeight - Time.deltaTime * deltaSpeed);
            if (Input.GetKey(increaseRadius)) SetRadius(roundAboutRadius + Time.deltaTime * deltaSpeed);
            if (Input.GetKey(decreaseRadius)) SetRadius(roundAboutRadius - Time.deltaTime * deltaSpeed);

            // Optional: Logic for real-time moving of intersections.
            if (IsMoveActive())
            {
                SetPointerActive(true);
                pointerPosition = SnapPointer(hit.point);
                
                if (moveStatus == MoveStatus.Select)
                {
                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        moveStatus = MoveStatus.Move;
                        moveInfoLabel.text = "Select Destination";
                        movePosition = pointerPosition;
                    }
                }
                else if (moveStatus == MoveStatus.Move)
                {
                    var hitPoint = ApplyDeltaHeight(hit.point);

                    if (Input.GetKeyDown(KeyCode.Mouse0))
                    {
                        var resultMove = roadConstructor.MoveIntersection(movePosition, GetDefaultRadius(), hitPoint);

                        SetMoveActive(false);
                        SetPointerActive(false);
                    }
                    else
                    {
                        var resultDisplayMove = roadConstructor.DisplayMoveIntersection(movePosition, GetDefaultRadius(), hitPoint);    
                    }
                }
            }

            if (builderRoadType == BuilderRoadType.None) return;
            if (string.IsNullOrEmpty(activeRoad)) return;

            pointerPosition = SnapPointer(hit.point);

            // Information about the construction.
            // Useful for example for UI, or to directly access the new objects.
            ConstructionResult result = null;

            var roadSettings = new RoadSettings(); // Optional settings which can be applied dynamically via construction methods.
            
            if (Input.GetKey(fixTangent1))
            {
                roadSettings.setTangent01 = true;
                roadSettings.tangent01 = lastTangent01;
            }
            if (Input.GetKey(fixTangent2))
            {
                roadSettings.setTangent02 = true;
                roadSettings.tangent02 = lastTangent02;
            }
            
            if (Input.GetKeyDown(KeyCode.Mouse0)) // Actual construction of the roads & intersections.
            {
                if (builderRoadType == BuilderRoadType.Road) result = ConstructRoad(pointerPosition, roadSettings);
                else if (builderRoadType == BuilderRoadType.Roundabout) result = ConstructRoundabout(pointerPosition);
                else if (builderRoadType == BuilderRoadType.Ramp) result = ConstructRamp(pointerPosition, roadSettings);
            }
            else // Creating display objects, only used for visual representation.
            {
                if (builderRoadType == BuilderRoadType.Road) result = DisplayRoad(pointerPosition, roadSettings);
                else if (builderRoadType == BuilderRoadType.Roundabout) result = DisplayRoundabout(pointerPosition);
                else if (builderRoadType == BuilderRoadType.Ramp) result = DisplayRamp(pointerPosition, roadSettings);
            }

            if(result.isValid && result is ConstructionResultRoad roadResult)
            {
                if (!roadSettings.setTangent01) lastTangent01 = roadResult.roadData.tangent01;
                if (!roadSettings.setTangent02) lastTangent02 = roadResult.roadData.tangent02;
            }
            if(result.isValid && result is ConstructionResultRamp rampResult)
            {
                if (!roadSettings.setTangent01) lastTangent01 = rampResult.roadData.tangent01;
                if (!roadSettings.setTangent02) lastTangent02 = rampResult.roadData.tangent02;
            }
            
            if (Input.GetKeyDown(KeyCode.Mouse1) || Input.GetKeyDown(detachRoad))
            {
                ResetValues();
            }

            buildingParameter.text = BuildingParameterText();
            constructionData.text = ConstructionDataText(result);
            constructionFails.text = ConstructionFailText(result);
            buildingParameter.style.display = string.IsNullOrEmpty(buildingParameter.text)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            constructionData.style.display = string.IsNullOrEmpty(constructionData.text)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            constructionFails.style.display = string.IsNullOrEmpty(constructionFails.text)
                ? new StyleEnum<DisplayStyle>(DisplayStyle.None)
                : new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
        }


        /********************************************************************************************************************************/
        // These sections handle UI logic, utilizing the UI Toolkit. 
        // Understanding this part isn't necessary if you want to create your own UI,
        // although you may find it helpful to take a look.
        /********************************************************************************************************************************/
        private void FindMenuElements()
        {
            root = uIDocument.rootVisualElement;

            UserPanel = root.Q<VisualElement>(nameof(UserPanel));
            RoadMenu = root.Q<VisualElement>(nameof(RoadMenu));
            openRoadMenu_local = root.Q<Button>(nameof(openRoadMenu_local));
            openRoadMenu_highway = root.Q<Button>(nameof(openRoadMenu_highway));
            openRoadMenu_oneway = root.Q<Button>(nameof(openRoadMenu_oneway));
            openRoadMenu_rural = root.Q<Button>(nameof(openRoadMenu_rural));
            
            InfoMenu = root.Q<VisualElement>(nameof(InfoMenu));
            moveInfoLabel = root.Q<Label>(nameof(moveInfoLabel));

            buildingParameter = root.Q<Label>(nameof(buildingParameter));
            constructionData = root.Q<Label>(nameof(constructionData));
            constructionFails = root.Q<Label>(nameof(constructionFails));

            roadButtons = new List<Button>();

            undo = root.Q<Button>(nameof(undo));
            demolish = root.Q<Button>(nameof(demolish));
            move = root.Q<Button>(nameof(move));
            builderRoadTypeButton = root.Q<Button>(nameof(builderRoadTypeButton));
        }

        // Making sure the mouse doesn't interact with the scene when menu items are selected.
        private void InitializeMousePanelHover()
        {
            UserPanel.RegisterCallback<MouseEnterEvent>(evt =>
            {
                mouseInsidePanel = true;
                SetPointerActive(false);
                SetPointerDemolishActive(false);
            });
            UserPanel.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                mouseInsidePanel = false;
                SetPointerActive(true);
                SetPointerDemolishActive(true);
            });
        }

        private void InitializeButtons()
        {
            RoadMenu.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            openRoadMenu_local.clicked += () => { ButtonMenuClicked(RoadsLocal); };
            openRoadMenu_highway.clicked += () => { ButtonMenuClicked(RoadsHighway); };
            openRoadMenu_oneway.clicked += () => { ButtonMenuClicked(RoadsOneWay); };
            openRoadMenu_rural.clicked += () => { ButtonMenuClicked(RoadsRural); };
            
            InfoMenu.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            undo.clicked += UndoLastConstruction;

            demolish.clicked += () =>
            {
                SetMoveActive(false);
                
                SetDemolishActive(!IsDemolishActive());
                ButtonMenuClicked(string.Empty);
                
                InfoMenu.PGDisplayStyleFlex(IsMoveActive());
            };
            
            move.clicked += () =>
            {
                SetDemolishActive(false);

                SetMoveActive(!IsMoveActive());
                ButtonMenuClicked(string.Empty);
                
                InfoMenu.PGDisplayStyleFlex(IsMoveActive());
                moveInfoLabel.text = "Select an Intersection";
            };
            

            builderRoadTypeButton.text = builderRoadType.ToString();
            builderRoadTypeButton.clicked += () =>
            {
                builderRoadType = GetNextEnumValue(builderRoadType);
                builderRoadTypeButton.text = builderRoadType.ToString();
            };
            
            T GetNextEnumValue<T>(T value) where T : Enum
            {
                var values = Enum.GetValues(typeof(T));
                var nextIndex = (Array.IndexOf(values, value) + 1) % values.Length;
                return (T)values.GetValue(nextIndex);
            }
        }

        // Creating the road buttons automatically for convenience.
        private void ButtonMenuClicked(string buttonMenuName)
        {
            activeRoad = string.Empty;
            SetPointerActive(false);
            SetPointerDemolishActive(false);
            RoadMenu.Clear();
            roadButtons.Clear();

            if (activeMenu == buttonMenuName || buttonMenuName == string.Empty)
            {
                RoadMenu.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                activeMenu = string.Empty;
                return;
            }

            var _constructionSet = roadConstructor._RoadSet;

            for (var i = 0; i < _constructionSet.roads.Count; i++)
            {
                var road = _constructionSet.roads[i];
                if (!road.category.StartsWith(buttonMenuName)) continue;

                var roadButton = new Button();
                roadButton.name = road.roadName;
                roadButton.text = road.roadName;
                roadButton.text = string.Concat(roadButton.text.Select((c, i) => 
                    char.IsUpper(c) && i != 0 && roadButton.text[i - 1] != '-' 
                        ? Environment.NewLine + c 
                        : c.ToString()
                ));
                roadButton.AddToClassList(USS_Button);
                roadButton.AddToClassList(USS_ButtonHover);

                roadButtons.Add(roadButton);
                RoadMenu.Add(roadButton);

                roadButton.clicked += () =>
                {
                    SetDemolishActive(false);
                    SetMoveActive(false);
                    InfoMenu.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
                    if (activeRoad == road.roadName) DeactivateRoad();
                    else ActivateRoad(road.roadName);
                    UpdateUI();
                };
            }

            RoadMenu.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            activeMenu = buttonMenuName;
            UpdateUI();
        }

        public override void ActivateRoad(string roadName)
        {
            base.ActivateRoad(roadName);
            UpdateUI();
        }

        private void UpdateUI()
        {
            for (var i = 0; i < roadButtons.Count; i++)
                if (roadButtons[i].name == activeRoad)
                    roadButtons[i].AddToClassList(USS_ButtonSelected);
                else
                    roadButtons[i].RemoveFromClassList(USS_ButtonSelected);
        }
    }
}