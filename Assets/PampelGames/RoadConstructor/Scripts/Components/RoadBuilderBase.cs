// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Text;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    /// <summary>
    ///     Base class for road builder scripts.
    ///     You may modify it to your liking, or you can create your own base class from scratch.
    /// </summary>
    public abstract class RoadBuilderBase : MonoBehaviour
    {
        public RoadConstructor roadConstructor;
        public GameObject pointerPrefab;
        public GameObject pointerDemolishPrefab;
        
        public RoundaboutDesign roundaboutDesign = RoundaboutDesign.Default;
        
        [Space(10)] public float roundAboutRadius = 10f;
        [Tooltip("Tries to find a suitable road and creates the new road parallel to it.\n" +
                 "Applies only if both the start and end overlap with the same road.")]
        public bool parallelRoad;
        [Tooltip("The distance between the newly created road and its parallel counterpart.")]
        public float parallelDistance;
        
        [Space(10)] public KeyCode increaseHeight = KeyCode.E;
        public KeyCode decreaseHeight = KeyCode.Q;
        public KeyCode increaseRadius = KeyCode.T;
        public KeyCode decreaseRadius = KeyCode.R;
        public float deltaSpeed = 5f;
        
        [Space(10)] public KeyCode fixTangent1 = KeyCode.LeftShift;
        public KeyCode fixTangent2 = KeyCode.LeftControl;
        public KeyCode detachRoad = KeyCode.Escape;

        [Space(10)] [Tooltip("Registers existing objects in the scene for construction.")]
        public bool registerSceneObjects = true;
        
        [Tooltip("When a road has been placed, a new road connects to it immediately.")]
        public bool continuous = true;


        private GameObject pointer;
        private GameObject pointerDemolish;

        protected string activeRoad;
        [HideInInspector] public string activeMenu;
        public float deltaHeight;
        [HideInInspector] public Vector3 lastTangent01;
        [HideInInspector] public Vector3 lastTangent02;

        public bool position01Set;
        private Vector3 position01;
        private Overlap overlap01 = new();
        private Overlap overlap02 = new();
        
        [HideInInspector] public BuilderRoadType builderRoadType = BuilderRoadType.Road;
        [HideInInspector] public BuilderOtherType builderOtherType = BuilderOtherType.None;
        
        [HideInInspector] public MoveStatus moveStatus;
        [HideInInspector] public Vector3 movePosition;

        /********************************************************************************************************************************/

        public void InitializePointer()
        {
            CreatePointer();
            SetPointerActive(false);
            SetPointerDemolishActive(false);
        }

        public void DestroyPointers()
        {
            if (Application.isPlaying) Destroy(pointer);
            else DestroyImmediate(pointer);
            if (Application.isPlaying) Destroy(pointerDemolish);
            else DestroyImmediate(pointerDemolish);
        }

        /********************************************************************************************************************************/

        private void CreatePointer()
        {
            if (pointer == null && pointerPrefab != null)
            {
                pointer = Instantiate(pointerPrefab, roadConstructor.transform, true);
                pointer.name = "Pointer";
            }
            if (pointerDemolish == null && pointerDemolishPrefab != null)
            {
                pointerDemolish = Instantiate(pointerDemolishPrefab, roadConstructor.transform, true);
                pointerDemolish.name = "PointerDemolish";
            }
        }

        public Vector3 SnapPointer(Vector3 position)
        {
            SetPointerActive(true);
            position += Vector3.up * deltaHeight;

            Overlap overlap;

            if(string.IsNullOrEmpty(activeRoad))
            {
                var radius = GetDefaultRadius();
                pointer.transform.position = roadConstructor.SnapPosition(radius, position, out overlap);
                pointer.transform.localScale = new Vector3(radius, radius, radius);
            }
            else
                pointer.transform.position = roadConstructor.SnapPosition(activeRoad, position, out overlap);
            
            if (!position01Set) overlap01 = overlap;
            else overlap02 = overlap;

            var up = Vector3.up;
            var right = Vector3.Cross(up, Vector3.forward);
            var forward = Vector3.Cross(right, up);
            var rotation = quaternion.LookRotationSafe(forward, up);

            pointer.transform.rotation = rotation;

            return pointer.transform.position;
        }
        
        public Vector3 SnapPointerDemolish(float radius, Vector3 position, Vector3 direction)
        {
            SetPointerActive(true);
            position += Vector3.up * deltaHeight;
            
            pointerDemolish.transform.position = roadConstructor.SnapPosition(radius, position, out var _overlap);
            
            var up = direction.normalized;
            var right = Vector3.Cross(up, Vector3.forward);
            var forward = Vector3.Cross(right, up);
            var rotation = quaternion.LookRotationSafe(forward, up);

            pointerDemolish.transform.rotation = rotation;
            
            return pointerDemolish.transform.position;
        }

        public void SetPointerActive(bool active)
        {
            if (pointer == null) return;
            if (builderOtherType != BuilderOtherType.Move && active && activeRoad == string.Empty) return;
            if (builderOtherType != BuilderOtherType.Reverse && active && activeRoad == string.Empty) return;
            pointer.SetActive(active);
        }
        
        public void SetPointerDemolishActive(bool active)
        {
            if (pointerDemolish == null) return;
            pointerDemolish.SetActive(active);
        }

        public float GetDefaultRadius()
        {
            var radius = math.abs(roadConstructor.componentSettings.heightRange.y) + 1f;
            return radius;
        }
        
        /********************************************************************************************************************************/

        public virtual void ActivateRoad(string roadName)
        {
            ResetValues();
            roadConstructor.ClearAllDisplayObjects();

            if (activeRoad == roadName)
            {
                SetActiveRoadData(string.Empty);
                return;
            }

            if (!roadConstructor.TryGetRoadDescr(roadName, out var roadDescr)) return;
            SetActiveRoadData(roadName);
            pointer.transform.localScale = Vector3.one * roadDescr.width;
            pointerDemolish.transform.localScale = Vector3.one * roadDescr.width;
        }

        public void DeactivateRoad()
        {
            SetPointerActive(false);
            activeRoad = string.Empty;
        }

        private void SetActiveRoadData(string roadName)
        {
            activeRoad = roadName;
        }

        public string GetActiveRoad()
        {
            return activeRoad;
        }

        /********************************************************************************************************************************/

        public bool IsDemolishActive()
        {
            return builderOtherType == BuilderOtherType.Demolish;
        }

        public void SetDemolishActive(bool active)
        {
            DeactivateRoad();
            builderOtherType = active ? BuilderOtherType.Demolish : BuilderOtherType.None;
        }
        
        /********************************************************************************************************************************/

        public bool IsMoveActive()
        {
            return builderOtherType == BuilderOtherType.Move;
        }

        public void SetMoveActive(bool active)
        {
            DeactivateRoad();
            builderOtherType = active ? BuilderOtherType.Move : BuilderOtherType.None;
            moveStatus = MoveStatus.Select;
        }

        public bool IsReverseDirectionActive()
        {
            return builderOtherType == BuilderOtherType.Reverse;
        }

        public void SetReverseDirectionActive(bool active)
        {
            DeactivateRoad();
            builderOtherType = active ? BuilderOtherType.Reverse : BuilderOtherType.None;
            moveStatus = MoveStatus.Select;
        }

        /********************************************************************************************************************************/

        public float GetDeltaHeight()
        {
            return deltaHeight;
        }

        public void SetDeltaHeight(float value)
        {
            deltaHeight = value;
        }
        
        public void SetRadius(float value)
        {
            roundAboutRadius = value;
        }
        
        public float GetRadius()
        {
            return roundAboutRadius;
        }

        public Vector3 ApplyDeltaHeight(Vector3 position)
        {
            return position + new Vector3(0f, deltaHeight, 0f);
        }

        /********************************************************************************************************************************/

        public ConstructionResultRoad DisplayRoad(Vector3 position, RoadSettings roadSettings)
        {
            InitializeResult(roadSettings, ref position);

            if (!position01Set) return new ConstructionResultRoad(false);
            
            var result = roadConstructor.DisplayRoad(activeRoad, position01, position, roadSettings);
            return result;
        }

        public ConstructionResultRoad ConstructRoad(Vector3 position, RoadSettings roadSettings)
        {
            InitializeResult(roadSettings, ref position);
            
            if (!position01Set)
            {
                position01Set = true;
            }
            else
            {
                var result = roadConstructor.ConstructRoad(activeRoad, position01, position, roadSettings);
                FinalizeResult(result, position);
                return result;
            }

            return new ConstructionResultRoad(false);
        }
        
        public ConstructionResultRoundabout DisplayRoundabout(Vector3 position)
        {
            position = ApplyDeltaHeight(position);
            
            var result = roadConstructor.DisplayRoundabout(activeRoad, position, roundaboutDesign, roundAboutRadius);
            position01Set = false;
            
            return result;
        }
        
        public ConstructionResultRoundabout ConstructRoundabout(Vector3 position)
        {
            position = ApplyDeltaHeight(position);
            
            var result = roadConstructor.ConstructRoundabout(activeRoad, position, roundaboutDesign, roundAboutRadius);
            position01Set = false;

            return result;
        }
        
        public ConstructionResultRamp DisplayRamp(Vector3 position, RoadSettings roadSettings)
        {
            InitializeResult(roadSettings, ref position);
            
            if (!position01Set) return new ConstructionResultRamp(false);
            
            var result = roadConstructor.DisplayRamp(activeRoad, position01, position, roadSettings);
            return result;
        }

        public ConstructionResultRamp ConstructRamp(Vector3 position, RoadSettings roadSettings)
        {
            InitializeResult(roadSettings, ref position);
            
            if (!position01Set)
            {
                position01Set = true;
            }
            else
            {
                var result = roadConstructor.ConstructRamp(activeRoad, position01, position, roadSettings);
                FinalizeResult(result, position);
                return result;
            }

            return new ConstructionResultRamp(false);
        }

        /********************************************************************************************************************************/
        
        private void InitializeResult(RoadSettings railwaySettings, ref Vector3 position)
        {
            railwaySettings.overlap01 = overlap01;
            railwaySettings.overlap02 = overlap02;
            
            position = ApplyDeltaHeight(position);

            if (!position01Set)
            {
                position01 = position;
            }
        }
        
        private void FinalizeResult(ConstructionResult result, Vector3 position)
        {
            if (result.constructionFails.Count == 0)
            {
                if (!continuous) position01Set = false;
                position01 = position;
                overlap01 = new Overlap();
                overlap02 = new Overlap();
            }
        }
        
        /********************************************************************************************************************************/
        
        public void ResetValues()
        {
            deltaHeight = 0f;
            lastTangent01 = Vector3.zero;
            lastTangent02 = Vector3.zero;
            position01Set = false;
        }

        public void UndoLastConstruction()
        {
            roadConstructor.UndoLastConstruction();
        }

        /********************************************************************************************************************************/

        public string BuildingParameterText()
        {
            var infoText = "Elevation: " + deltaHeight;
            return infoText;
        }

        // For convenience, using reflection to retrieve all available road data fields.
        public string ConstructionDataText(ConstructionResult result)
        {
            if (result is ConstructionResultRoad resultRoad)
            {
                if (!resultRoad.isValid) return string.Empty;
                
                var roadData = resultRoad.roadData;
                var type = roadData.GetType();
                var fields = type.GetFields();
                var infoText = new StringBuilder();

                foreach (var field in fields)
                {
                    var fieldName = field.Name;
                    var fieldValue = field.GetValue(roadData);
                    if (field.FieldType == typeof(float))
                        fieldValue = Math.Round((float) fieldValue, 2); // convert to float and round to 2 decimal places
                    infoText.AppendLine(fieldName + ": " + fieldValue);
                }

                return infoText.ToString();
            }
            else
            {
                return string.Empty;
            }
        }

        public string ConstructionFailText(ConstructionResult result)
        {
            var failText = string.Empty;
            var fails = result.constructionFails;
            for (var i = 0; i < fails.Count; i++)
            {
                failText += "Fail: " + fails[i].failCause;
                if (i != fails.Count - 1) failText += "\n";
            }

            return failText;
        }
    }
    
}