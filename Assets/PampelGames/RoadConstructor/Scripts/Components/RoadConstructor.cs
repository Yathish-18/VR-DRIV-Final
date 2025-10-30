// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using GameObject = UnityEngine.GameObject;

namespace PampelGames.RoadConstructor
{
    /// <summary>
    ///     Road Constructor Component.
    /// </summary>
    [AddComponentMenu("Pampel Games/Road Constructor")]
    public class RoadConstructor : ConstructionBase
    {
        private void Reset()
        {
            componentSettings.groundLayers |= 1 << LayerMask.NameToLayer("Default");
        }

        public SO_DefaultReferences _DefaultReferences;
        public RoadSet _RoadSet;
        public ComponentSettings componentSettings = new();
        public List<ConstructionBase> integrations = new();


#if UNITY_EDITOR
        public EditorDisplay _editorDisplay = EditorDisplay.RoadSet;
        public PartType _editorActivePartType = PartType.Roads;
        public float roadPreviewLength = 25f;
        public bool roadPreviewSpawnObjects = true;
        public bool roadPreviewElevated;
#endif

        /********************************************************************************************************************************/
        private SceneData sceneData = new();

        /********************************************************************************************************************************/
        private LinkedList<UndoObject> undoObjects;
        /********************************************************************************************************************************/

        private List<RoadDescr> roadDescrs;

        private Dictionary<string, int> roadIndexDict;

        private Transform displayParent;
        private Transform constructionParent;
        private Transform undoParent;
        private Transform displayDemolishParent;
        private Transform displayMoveIntersectionParent;

        private List<SceneObject> displayedObjects;
        private List<GameObject> displayedDemolishObjects;
        private List<GameObject> displayedMoveIntersectionObjects;
        private List<GameObject> deactivatedObjects;

        private bool initialized;

        private TerrainUpdateSettings terrainUpdateSettings;
        private List<TerrainCache> undoTerrains;

        /* Public Delegates *************************************************************************************************************/

        public event Action<List<RoadObject>, List<IntersectionObject>> OnRoadsAdded = delegate { };
        public event Action<List<RoadObject>, List<IntersectionObject>> OnRoadsRemoved = delegate { };

        /********************************************************************************************************************************/

        public class SceneData
        {
            public readonly List<RoadObject> roadObjects = new();
            public readonly List<Bounds> roadBounds = new();

            public readonly List<IntersectionObject> intersectionObjects = new();
            public readonly List<Bounds> intersectionBounds = new();

            public void AddRoad(RoadObject roadObject)
            {
                roadObjects.Add(roadObject);
                roadBounds.Add(roadObject.meshRenderer.bounds);
            }

            public void AddIntersection(IntersectionObject intersectionObject)
            {
                intersectionObjects.Add(intersectionObject);
                intersectionBounds.Add(intersectionObject.meshRenderer.bounds);
            }

            public void RemoveRoad(int index)
            {
                roadBounds.RemoveAt(index);
                roadObjects.RemoveAt(index);
            }

            public void RemoveIntersection(int index)
            {
                intersectionBounds.RemoveAt(index);
                intersectionObjects.RemoveAt(index);
            }

            public void Clear()
            {
                roadObjects.Clear();
                roadBounds.Clear();
                intersectionObjects.Clear();
                intersectionBounds.Clear();
            }
        }

        /********************************************************************************************************************************/
        private void Awake()
        {
            // Version 1.6: Check for obsolete terrain
            // Can be removed later
            if (componentSettings.terrain && !componentSettings.terrains.Contains(componentSettings.terrain))
                componentSettings.terrains.Add(componentSettings.terrain);

            Initialize();
        }

        private bool VerifyInitialization()
        {
            if (Application.isPlaying && initialized) return false;

#if UNITY_EDITOR
            if (componentSettings.terrainSettings && componentSettings.terrains.Count == 0)
            {
                Debug.LogError("Terrain settings are enabled but no terrains assigned!");
                return false;
            }
#endif
            return true;
        }

        private void InitializeScene()
        {
            displayedObjects = new List<SceneObject>();
            displayParent = transform.Find(Constants.DisplayConstructionParent);
            if (!displayParent) displayParent = new GameObject(Constants.DisplayConstructionParent).transform;
            displayParent.transform.SetParent(transform);
            constructionParent = transform.Find(Constants.ConstructionParent);
            if (!constructionParent) constructionParent = new GameObject(Constants.ConstructionParent).transform;
            constructionParent.transform.SetParent(transform);
            displayDemolishParent = transform.Find(Constants.DisplayDemolishParent);
            if (!displayDemolishParent) displayDemolishParent = new GameObject(Constants.DisplayDemolishParent).transform;
            displayDemolishParent.transform.SetParent(transform);
            displayMoveIntersectionParent = transform.Find(Constants.DisplayMoveIntersectionParent);
            if (!displayMoveIntersectionParent)
                displayMoveIntersectionParent = new GameObject(Constants.DisplayMoveIntersectionParent).transform;
            displayMoveIntersectionParent.transform.SetParent(transform);
            displayedDemolishObjects = new List<GameObject>();
            displayedMoveIntersectionObjects = new List<GameObject>();
            deactivatedObjects = new List<GameObject>();
            undoParent = transform.Find(Constants.UndoParent);
            if (!undoParent) undoParent = new GameObject(Constants.UndoParent).transform;
            undoParent.transform.SetParent(transform);
            sceneData = new SceneData();
            undoObjects = new LinkedList<UndoObject>();
            terrainUpdateSettings = componentSettings.CreateTerrainUpdateSettings();
            undoTerrains = new List<TerrainCache>();
            if (componentSettings.undoStorageSize > 0 && componentSettings.terrainSettings)
                for (var i = 0; i < componentSettings.terrains.Count; i++)
                    undoTerrains.Add(new TerrainCache(componentSettings.terrains[i]));
        }

        private void InitializeRoads()
        {
            roadIndexDict = new Dictionary<string, int>();
            for (var i = 0; i < _RoadSet.roads.Count; i++)
            {
                if (string.IsNullOrEmpty(_RoadSet.roads[i].roadName))
                {
                    Debug.LogWarning("No name is assigned to the road at index " + i + ", which is not supported!");
                    continue;
                }

                if (!roadIndexDict.TryAdd(_RoadSet.roads[i].roadName, i))
                    Debug.LogWarning("The road name: " + _RoadSet.roads[i].roadName + " is not unique!");
            }

            roadDescrs = new List<RoadDescr>();
            for (var i = 0; i < _RoadSet.roads.Count; i++)
            {
                var road = _RoadSet.roads[i];
                var roadDescr = new RoadDescr(this, road, componentSettings, _DefaultReferences, _RoadSet.lanePresets);
                SplineEdgeUtility.CreateRoadLanes(componentSettings, roadDescr, _RoadSet.lanePresets);
                roadDescr.trafficLanesEditor = TrafficUtility.GetTrafficLanesEditor(_RoadSet.trafficLanePresets, roadDescr);
                roadDescrs.Add(roadDescr);
            }
        }

        private void UninitializeInternal()
        {
            for (var i = gameObject.transform.childCount - 1; i >= 0; i--) ObjectUtility.DestroyObject(gameObject.transform.GetChild(i).gameObject);
            if (!IsInitialized()) return;

            sceneData.Clear();
            roadDescrs.Clear();
            roadIndexDict.Clear();
            roadIndexDict = null;
        }

        /********************************************************************************************************************************/
        // ConstructionBase
        /********************************************************************************************************************************/
        
        public override bool TryGetSceneObject(Vector3 position, float searchRadius, out SceneObjectBase sceneObject)
        {
            sceneObject = null;
            var overlap = OverlapUtility.GetOverlap(componentSettings, searchRadius, float.MaxValue, position, sceneData, new List<ConstructionBase>());
            if (!overlap.exists) return false;
            if (overlap.intersectionObject) sceneObject = overlap.intersectionObject;
            else if (overlap.roadObject) sceneObject = overlap.roadObject;
            else return false;
            return true;
        }

        public override List<ConstructionFail> DetectTrackOverlap(Spline trackSpline, float trackWidth, float trackSpacing, List<SceneObjectBase> ignoreObjects)
        {
            var constructionFails = RoadValidation.DetectTrackOverlap(componentSettings, sceneData, trackSpline, trackWidth, trackSpacing,
                ignoreObjects, out var splinePositions, out var splineTangents);

            return constructionFails;
        }

        /********************************************************************************************************************************/
        // Public
        /********************************************************************************************************************************/

        /// <summary>
        ///     Initializes this <see cref="RoadConstructor" /> component for constructing.
        ///     In play-mode, this is done automatically in Awake.
        /// </summary>
        public void Initialize()
        {
            if (!VerifyInitialization()) return;
            InitializeScene();
            InitializeRoads();
            if (Application.isPlaying) initialized = true;
        }

        /// <summary>
        ///     Deinitializes this <see cref="RoadConstructor" /> component,
        ///     which includes clearing the history and removing helper objects.
        /// </summary>
        public void Uninitialize()
        {
            UninitializeInternal();
            if (Application.isPlaying) initialized = false;
        }

        /// <summary>
        ///     Returns true if this component has been initialized.
        /// </summary>
        public bool IsInitialized()
        {
#if UNITY_EDITOR
            if (roadIndexDict == null) return false;
            if (displayedObjects == null) return false;
#endif
            return true;
        }

        /// <summary>
        ///     Registers existing scene objects for construction.
        ///     The component has to be initialized first.
        ///     The active construction set needs to contain the road name for each object to be registered.
        /// </summary>
        public void RegisterSceneObjects()
        {
            if (Application.isPlaying && !initialized) Initialize();
            RegisterSceneObjectsInternal(out var sceneRoadObjects, out var sceneIntersectionObjects);
        }

        /// <summary>
        ///     Registers existing scene objects for construction.
        ///     The component has to be initialized first.
        ///     The active construction set needs to contain the road name for each object to be registered.
        /// </summary>
        public void RegisterSceneObjects(out List<RoadObject> sceneRoadObjects, out List<IntersectionObject> sceneIntersectionObjects)
        {
            RegisterSceneObjectsInternal(out sceneRoadObjects, out sceneIntersectionObjects);
        }

        /// <summary>
        ///     Gets a <see cref="Road" /> by name.
        /// </summary>
        public bool TryGetRoad(string roadName, out Road road)
        {
            road = null;
            if (!IsInitialized()) return false;
            if (!roadIndexDict.TryGetValue(roadName, out var partIndex)) return false;
            road = _RoadSet.roads[partIndex];
            return true;
        }

        /// <summary>
        ///     Gets a <see cref="RoadDescr" /> by road name.
        /// </summary>
        public bool TryGetRoadDescr(string roadName, out RoadDescr roadDescr)
        {
            roadDescr = null;
            if (!roadIndexDict.TryGetValue(roadName, out var partIndex))
            {
                Debug.LogWarning("The road: " + roadName + " does not exist!");
                return false;
            }

            roadDescr = roadDescrs[partIndex];
            return true;
        }

        /// <summary>
        ///     Gets a list of all registered <see cref="RoadObject" />s from the scene.
        /// </summary>
        public List<RoadObject> GetRoads()
        {
            return sceneData.roadObjects;
        }

        /// <summary>
        ///     Gets a list of all registered <see cref="IntersectionObject" />s from the scene.
        /// </summary>
        public List<IntersectionObject> GetIntersections()
        {
            return sceneData.intersectionObjects;
        }

        /// <summary>
        ///     Gets a list of all registered <see cref="RoadObject" />s and <see cref="IntersectionObject" />s from the scene.
        /// </summary>
        public List<SceneObject> GetSceneObjects()
        {
            var sceneObjects = new List<SceneObject>();
            sceneObjects.AddRange(GetRoads());
            sceneObjects.AddRange(GetIntersections());
            return sceneObjects;
        }

        /// <summary>
        ///     Gets the parent object holding all registered <see cref="RoadObject" />s and <see cref="IntersectionObject" />s from the scene.
        /// </summary>
        /// <returns></returns>
        public Transform GetConstructionParent()
        {
            return constructionParent;
        }

        /// <summary>
        ///     Clears all displayed objects and displayed demolish objects in the scene hierarchy.
        /// </summary>
        public void ClearAllDisplayObjects(bool activateSceneObjects = true)
        {
            if (!IsInitialized()) return;

            ClearDisplayedObjects();
            ClearDisplayedDemolishObjects();
            ClearDisplayedMoveIntersectionObjects();

            if (activateSceneObjects)
            {
                for (var i = 0; i < deactivatedObjects.Count; i++) deactivatedObjects[i].SetActive(true);
                deactivatedObjects.Clear();
            }
        }

        /// <summary>
        ///     Clears all displayed objects in the scene hierarchy.
        /// </summary>
        public void ClearDisplayedObjects()
        {
            for (var i = 0; i < displayedObjects.Count; i++)
            {
                if (!displayedObjects[i]) continue;
                ObjectUtility.DestroyObject(displayedObjects[i].gameObject);
            }

            displayedObjects.Clear();
        }

        /// <summary>
        ///     Clears all displayed demolish objects in the scene hierarchy.
        /// </summary>
        public void ClearDisplayedDemolishObjects()
        {
            for (var i = 0; i < displayedDemolishObjects.Count; i++) ObjectUtility.DestroyObject(displayedDemolishObjects[i], false);
            displayedDemolishObjects.Clear();
        }

        /// <summary>
        ///     Clears all displayed move intersection objects in the scene hierarchy.
        /// </summary>
        public void ClearDisplayedMoveIntersectionObjects()
        {
            for (var i = 0; i < displayedMoveIntersectionObjects.Count; i++) ObjectUtility.DestroyObject(displayedMoveIntersectionObjects[i]);
            displayedMoveIntersectionObjects.Clear();
        }

        /// <summary>
        ///     Snaps a position to the nearest road, intersection or grid using the width of the road.
        /// </summary>
        public Vector3 SnapPosition(string roadName, Vector3 position, out Overlap overlap)
        {
            overlap = new Overlap();
            if (!TryGetRoadDescr(roadName, out var roadDescr)) return position;

            return SnapPosition(roadDescr.width, position, out overlap);
        }

        /// <summary>
        ///     Snaps a position to the nearest road, intersection or grid using a custom radius.
        /// </summary>
        public Vector3 SnapPosition(float radius, Vector3 position, out Overlap overlap)
        {
            float3 newPosition = position;

            overlap = OverlapUtility.GetOverlap(componentSettings, radius, float.MaxValue, position, sceneData, integrations);

            if (overlap.exists) newPosition = overlap.position;
            else ApplyGridPositions(ref newPosition, ref newPosition);
            return newPosition;
        }

        /// <summary>
        ///     Undoes the previous construction operation and restores the previous state.
        ///     Requires objects within the undo storage.
        /// </summary>
        public void UndoLastConstruction()
        {
            UndoInternal();
        }

        /// <summary>
        ///     Removes all registered undo objects from the scene.
        /// </summary>
        public void ClearUndoStorage()
        {
            foreach (var undoObject in undoObjects) ObjectUtility.DestroyObject(undoObject.gameObject);
            undoObjects.Clear();
        }

        /// <summary>
        ///     Creates a road object and two intersection objects (start and end) for a segment between two positions.
        ///     These objects are not initialized in the system and should be deleted in the next frame via
        ///     the <see cref="ClearAllDisplayObjects" /> method.
        /// </summary>
        /// <param name="roadName">The name of the road.</param>
        /// <param name="position01">The first road position.</param>
        /// <param name="position02">The second road position.</param>
        /// <returns>The construction result, including new objects and detailed construction failures.</returns>
        public ConstructionResultRoad DisplayRoad(string roadName, float3 position01, float3 position02)
        {
            return DisplayRoadInternal(roadName, position01, position02, new RoadSettings());
        }

        /// <summary>
        ///     Creates a road object and two intersection objects (start and end) for a segment between two positions.
        ///     These objects are not initialized in the system and should be deleted in the next frame via
        ///     the <see cref="ClearAllDisplayObjects" /> method.
        /// </summary>
        /// <param name="roadName">The name of the road.</param>
        /// <param name="spline">Spline used for construction. Note that only the first and last knots are utilized - any others will be disregarded.</param>
        /// <returns>The construction result, including new objects and detailed construction failures.</returns>
        public ConstructionResultRoad DisplayRoad(string roadName, Spline spline)
        {
            var knots = spline.Knots.ToList();
            var knot01 = knots[0];
            var knot02 = knots[^1];
            return DisplayRoadInternal(roadName, knot01.Position, knot02.Position, new RoadSettings
            {
                setTangent01 = true,
                setTangent02 = true,
                tangent01 = knot01.TangentOut,
                tangent02 = knot02.TangentIn
            });
        }

        /// <summary>
        ///     Creates a road object and two intersection objects (start and end) for a segment between two positions.
        ///     These objects are not initialized in the system and should be deleted in the next frame via
        ///     the <see cref="ClearAllDisplayObjects" /> method.
        /// </summary>
        /// <param name="roadName">The name of the road.</param>
        /// <param name="position01">The first road position.</param>
        /// <param name="position02">The second road position.</param>
        /// <param name="roadSettings">Optional settings that can be dynamically applied.</param>
        /// <returns>The construction result, including new objects and detailed construction failures.</returns>
        public ConstructionResultRoad DisplayRoad(string roadName, float3 position01, float3 position02, RoadSettings roadSettings)
        {
            return DisplayRoadInternal(roadName, position01, position02, roadSettings);
        }

        /// <summary>
        ///     Constructs new intersection and road objects for a segment between two positions and registers them into the construction system.
        /// </summary>
        /// <param name="roadName">The name of the road.</param>
        /// <param name="position01">The first road position.</param>
        /// <param name="position02">The second road position.</param>
        /// <returns>The construction result, including new objects and detailed construction failures.</returns>
        public ConstructionResultRoad ConstructRoad(string roadName, float3 position01, float3 position02)
        {
            return ConstructRoadInternal(roadName, position01, position02, new RoadSettings(), true);
        }

        /// <summary>
        ///     Constructs new intersection and road objects for a segment between two positions and registers them into the construction system.
        /// </summary>
        /// <param name="roadName">The name of the road.</param>
        /// <param name="spline">Spline used for construction. Note that only the first and last knots are utilized - any others will be disregarded.</param>
        /// <returns>The construction result, including new objects and detailed construction failures.</returns>
        public ConstructionResultRoad ConstructRoad(string roadName, Spline spline)
        {
            var knots = spline.Knots.ToList();
            var knot01 = knots[0];
            var knot02 = knots[^1];
            var roadSettings = new RoadSettings
            {
                setTangent01 = true,
                setTangent02 = true,
                tangent01 = knot01.TangentOut,
                tangent02 = knot02.TangentIn
            };
            return ConstructRoadInternal(roadName, knot01.Position, knot02.Position, roadSettings, true);
        }

        /// <summary>
        ///     Constructs new intersection and road objects for a segment between two positions and registers them into the construction system.
        /// </summary>
        /// <param name="roadName">The name of the road.</param>
        /// <param name="position01">The first road position.</param>
        /// <param name="position02">The second road position.</param>
        /// <param name="roadSettings">Optional settings that can be dynamically applied.</param>
        /// <returns>The construction result, including new objects and detailed construction failures.</returns>
        public ConstructionResultRoad ConstructRoad(string roadName, float3 position01, float3 position02, RoadSettings roadSettings)
        {
            return ConstructRoadInternal(roadName, position01, position02, roadSettings, true);
        }

        /// <summary>
        ///     Displays a roundabout at the specified position with the given road name and radius.
        /// </summary>
        /// <param name="roadName">The name of the road to use for the roundabout.</param>
        /// <param name="position">The position where the center of the roundabout will be located.</param>
        /// <param name="design">
        ///     "Roundabout design type. The default is a roundabout with an open inner circle, while a cul-de-sac features a continuous
        ///     ground."
        /// </param>
        /// <param name="radius">The radius of the roundabout.</param>
        /// <returns>The construction result of displaying the roundabout.</returns>
        public ConstructionResultRoundabout DisplayRoundabout(string roadName, float3 position, RoundaboutDesign design, float radius)
        {
            return DisplayRoundaboutInternal(roadName, position, design, radius);
        }

        /// <summary>
        ///     Constructs a roundabout at the specified position with the given road name and radius.
        /// </summary>
        /// <param name="roadName">The name of the road for the roundabout.</param>
        /// <param name="position">The position where the roundabout will be constructed.</param>
        /// <param name="design">
        ///     "Roundabout design type. The default is a roundabout with an open inner circle, while a cul-de-sac features a continuous
        ///     ground."
        /// </param>
        /// <param name="radius">The radius of the roundabout.</param>
        /// <returns>A ConstructionResultRoundabout indicating the result of the construction.</returns>
        public ConstructionResultRoundabout ConstructRoundabout(string roadName, float3 position, RoundaboutDesign design, float radius)
        {
            return ConstructRoundaboutInternal(roadName, position, design, radius, true);
        }

        public ConstructionResultRamp DisplayRamp(string roadName, float3 position01, float3 position02, RoadSettings roadSettings)
        {
            return DisplayRampInternal(roadName, position01, position02, roadSettings);
        }

        public ConstructionResultRamp ConstructRamp(string roadName, float3 position01, float3 position02, RoadSettings roadSettings)
        {
            return ConstructRampInternal(roadName, position01, position02, roadSettings, true);
        }

        /// <summary>
        ///     Displays move intersection object.
        /// </summary>
        /// <param name="currentPosition">The current position where the intersection object is displayed.</param>
        /// <param name="searchRadius">The search radius within which the intersection object will be found.</param>
        /// <param name="newPosition">The new position where the intersection object will be displayed.</param>
        /// <param name="deactivateSceneObjects">A flag indicating whether to deactivate scene objects.</param>
        /// <returns>A list of GameObjects representing the displayed move intersection objects.</returns>
        public List<GameObject> DisplayMoveIntersection(Vector3 currentPosition, float searchRadius, Vector3 newPosition,
            bool deactivateSceneObjects = true)
        {
            return DisplayMoveIntersectionInternal(currentPosition, searchRadius, newPosition, deactivateSceneObjects);
        }

        /// <summary>
        ///     Moves an existing <see cref="IntersectionObject" /> to a new position.
        ///     Undo history will be lost after this method is called.
        /// </summary>
        /// <param name="currentPosition">The position where to search for the intersection.</param>
        /// <param name="searchRadius">The radius within which to search for the intersection.</param>
        /// <param name="newPosition">The new position to move the intersection to.</param>
        /// <returns>A <see cref="ConstructionResultMoveIntersection" /> representing the result of the operation.</returns>
        public ConstructionResultMoveIntersection MoveIntersection(Vector3 currentPosition, float searchRadius, Vector3 newPosition)
        {
            return MoveIntersectionInternal(currentPosition, searchRadius, newPosition);
        }

        /// <summary>
        ///     Reverses the direction of a road within the specified search radius from the given position.
        ///     If a valid road is found, it is inverted and replaced with a newly constructed road.
        /// </summary>
        /// <param name="position">The position at which to search for a road.</param>
        /// <param name="searchRadius">The radius within which to search for a road.</param
        /// <param name="replacedRoad">Newly constructed road.</param>
        public bool ReverseRoadDirection(Vector3 position, float searchRadius, out RoadObject replacedRoad)
        {
            replacedRoad = null;
            if (!TryGetSceneObject(position, searchRadius, out var road)) return false;
            if (road is not RoadObject roadObject) return false;
            ReverseRoadDirection(roadObject, out replacedRoad);
            return true;
        }

        /// <summary>
        ///     Reverses the direction of the specified road.
        /// </summary>
        /// <param name="road">The road object whose direction is to be reversed.</param>
        /// <param name="replacedRoad">The new road object created with the reversed direction.</param>
        public void ReverseRoadDirection(RoadObject road, out RoadObject replacedRoad)
        {
            var _spline = new Spline(road.splineContainer.Spline);
            ConstructionSplineUtility.InvertSpline(_spline);
            replacedRoad = RoadCreation.CreateRoad(road.roadDescr, _spline, road.elevated, road.rampRoad, 1f);
            var constructionObjects = new ConstructionObjects();
            constructionObjects.removableRoads.Add(road);
            constructionObjects.newRoads.Add(replacedRoad);
            FinalizeConstruction(constructionObjects, true, true, true);
        }

        /// <summary>
        ///     Creates display objects for demolishing for the specified position.
        ///     These objects are not initialized in the system and should be deleted in the next frame via
        ///     the <see cref="ClearAllDisplayObjects" /> method.
        /// </summary>
        /// <param name="position">The position from which to demolish objects.</param>
        /// <param name="searchRadius">Search radius to find the demolishable objects.</param>
        /// <param name="deactivateSceneObjects">Deactivates the scene objects.</param>
        /// <returns></returns>
        public List<GameObject> DisplayDemolishObjects(Vector3 position, float searchRadius, bool deactivateSceneObjects = true)
        {
            return DisplayDemolishObjectsInternal(position, searchRadius, deactivateSceneObjects);
        }

        /// <summary>
        ///     Demolishes objects within a specified radius from a given position.
        /// </summary>
        /// <param name="position">The position from which to demolish objects.</param>
        /// <param name="searchRadius">Search radius to find the demolishable objects.</param>
        public void Demolish(Vector3 position, float searchRadius)
        {
            DemolishObjectsInternal(position, searchRadius);
        }

        /// <summary>
        ///     Demolishes the specified intersections and roads.
        /// </summary>
        public void Demolish(List<IntersectionObject> intersections, List<RoadObject> roads)
        {
            DemolishObjectsInternal(intersections, roads);
        }

        /// <summary>
        ///     Updates the colliders of the specified scene objects based on the component settings.
        /// </summary>
        /// <param name="sceneObjects">The list of scene objects to update colliders for.</param>
        public void UpdateColliders<T>(List<T> sceneObjects) where T : SceneObject
        {
            for (var i = 0; i < sceneObjects.Count; i++)
                if (sceneObjects[i].meshFilter.gameObject.TryGetComponent<MeshCollider>(out var meshCollider))
                    ObjectUtility.DestroyObject(meshCollider);

            if (componentSettings.addCollider != AddCollider.None)
                for (var i = 0; i < sceneObjects.Count; i++)
                    if (componentSettings.addCollider == AddCollider.Convex)
                        sceneObjects[i].meshFilter.gameObject.AddComponent<MeshCollider>().convex = true;
                    else
                        sceneObjects[i].meshFilter.gameObject.AddComponent<MeshCollider>().convex = false;
        }

        /// <summary>
        ///     Update the layers and tags of scene objects based on the component settings.
        /// </summary>
        /// <param name="sceneObjects">List of scene objects to update layers and tags</param>
        public void UpdateLayersAndTags<T>(List<T> sceneObjects) where T : SceneObject
        {
            for (var i = 0; i < sceneObjects.Count; i++) sceneObjects[i].meshFilter.gameObject.layer = componentSettings.addColliderLayer;
            for (var i = 0; i < sceneObjects.Count; i++) sceneObjects[i].tag = componentSettings.roadTag;
        }


        /// <summary>
        ///     Editor Only. Exports road and intersection meshes into the project folder.
        /// </summary>
        /// <param name="checkExistingMeshes">
        ///     Checks the project folder for each mesh before exporting.
        ///     If set to false, each mesh will be exported regardless of whether one already exists.
        /// </param>
        public void ExportMeshes(bool checkExistingMeshes)
        {
#if UNITY_EDITOR
            Export.ExportMeshes(constructionParent.gameObject, checkExistingMeshes);
#endif
        }

        /// <summary>
        ///     Removes existing <see cref="Traffic" /> components and creates new ones for the full existing road system.
        ///     The traffic component is required for the <see cref="AddWaypoints" /> method.
        /// </summary>
        public void AddTrafficComponents()
        {
            AddTrafficComponents(GetRoads(), GetIntersections());
        }

        /// <summary>
        ///     Removes existing <see cref="Traffic" /> components and creates new ones for the specified roads/intersections.
        ///     The traffic component is required for the <see cref="AddWaypoints" /> method.
        /// </summary>
        public void AddTrafficComponents(List<RoadObject> roads, List<IntersectionObject> intersections)
        {
            for (var i = 0; i < roads.Count; i++) TrafficUtility.AddTrafficComponent(roads[i]);
            for (var i = 0; i < roads.Count; i++) roads[i].traffic.trafficLanes = roads[i].CreateTrafficLanes();
            for (var i = 0; i < intersections.Count; i++) TrafficUtility.AddTrafficComponent(intersections[i]);
            for (var i = 0; i < intersections.Count; i++) intersections[i].traffic.trafficLanes = intersections[i].CreateTrafficLanes();
        }

        /// <summary>
        ///     Creates a list of interconnected waypoints for the specified roads/intersections and adds them to the existing waypoint system.
        ///     Before using this method, make sure all registered roads have a 'Traffic' component assigned.
        ///     Either by enabling 'Add Traffic Comp.' in the settings or by invoking the AddTrafficComponents method.
        /// </summary>
        public void AddWaypoints(List<RoadObject> roads, List<IntersectionObject> intersections)
        {
            AddWaypoints(roads, intersections, componentSettings.waypointDistance);
        }

        /// <summary>
        ///     Creates a list of interconnected waypoints for the specified roads/intersections and adds them to the existing waypoint system.
        ///     Before using this method, make sure all registered roads have a 'Traffic' component assigned.
        ///     Either by enabling 'Add Traffic Comp.' in the settings or by invoking the AddTrafficComponents method.
        /// </summary>
        /// <param name="maxDistance">Maximum space between each waypoint.</param>
        public void AddWaypoints(List<RoadObject> roads, List<IntersectionObject> intersections, float maxDistance)
        {
            AddWaypoints(roads, intersections, new Vector2(maxDistance, maxDistance));
        }

        /// <summary>
        ///     Creates a list of interconnected waypoints for the specified roads/intersections and adds them to the existing waypoint system.
        ///     Before using this method, make sure all registered roads have a 'Traffic' component assigned.
        ///     Either by enabling 'Add Traffic Comp.' in the settings or by invoking the AddTrafficComponents method.
        /// </summary>
        /// <param name="maxDistance">Maximum space between each waypoint, based on curvature.</param>
        public void AddWaypoints(List<RoadObject> roads, List<IntersectionObject> intersections, Vector2 maxDistance)
        {
            WaypointUtility.CreateWaypoints(roads, intersections, TrafficLaneType.Car, maxDistance);
            WaypointUtility.CreateWaypoints(roads, intersections, TrafficLaneType.Pedestrian, maxDistance);
        }

        /// <summary>
        ///     Creates a list of interconnected waypoints for the specified intersections and adds them to the existing waypoint system.
        ///     Before using this method, make sure all registered roads have a 'Traffic' component assigned.
        ///     Either by enabling 'Add Traffic Comp.' in the settings or by invoking the AddTrafficComponents method.
        /// </summary>
        /// <param name="maxDistance">Maximum space between each waypoint, based on curvature.</param>
        public void AddWaypoints(List<IntersectionObject> intersections, Vector2 maxDistance)
        {
            WaypointUtility.CreateWaypoints(new List<RoadObject>(), intersections, TrafficLaneType.Car, maxDistance);
            WaypointUtility.CreateWaypoints(new List<RoadObject>(), intersections, TrafficLaneType.Pedestrian, maxDistance);
        }

        /// <summary>
        ///     Creates a list of interconnected waypoints from all road and intersection splines in the scene, overwriting any existing waypoints.
        ///     Before using this method, make sure all registered roads have a 'Traffic' component assigned.
        ///     Either by enabling 'Add Traffic Comp.' in the settings or by invoking the AddTrafficComponents method.
        /// </summary>
        public void CreateAllWaypoints()
        {
            CreateAllWaypoints(componentSettings.waypointDistance);
        }

        /// <summary>
        ///     Creates a list of interconnected waypoints from all road and intersection splines in the scene, overwriting any existing waypoints.
        ///     Before using this method, make sure all registered roads have a 'Traffic' component assigned.
        ///     Either by enabling 'Add Traffic Comp.' in the settings or by invoking the AddTrafficComponents method.
        /// </summary>
        /// <param name="maxDistance">Maximum space between each waypoint.</param>
        public void CreateAllWaypoints(float maxDistance)
        {
            CreateAllWaypoints(new Vector2(maxDistance, maxDistance));
        }

        /// <summary>
        ///     Creates a list of interconnected waypoints from all road and intersection splines in the scene, overwriting any existing waypoints.
        ///     Before using this method, make sure all registered roads have a 'Traffic' component assigned.
        ///     Either by enabling 'Add Traffic Comp.' in the settings or by invoking the AddTrafficComponents method.
        /// </summary>
        /// <param name="maxDistance">Maximum space between each waypoint, based on curvature.</param>
        public void CreateAllWaypoints(Vector2 maxDistance)
        {
            WaypointUtility.CreateWaypoints(GetRoads(), GetIntersections(), TrafficLaneType.Car, maxDistance);
            WaypointUtility.CreateWaypoints(GetRoads(), GetIntersections(), TrafficLaneType.Pedestrian, maxDistance);
        }

        /// <summary>
        ///     Removes all traffic components and waypoints from the system.
        /// </summary>
        public void RemoveTrafficSystem()
        {
            var sceneObjects = GetSceneObjects();
            for (var i = 0; i < sceneObjects.Count; i++)
            {
                var sceneObject = sceneObjects[i];
                if (sceneObject.traffic != null)
                {
                    ObjectUtility.DestroyObject(sceneObject.traffic.gameObject);
                    sceneObject.traffic = null;
                }
            }
        }

        /// <summary>
        ///     Removes waypoints to these roads in connecting intersections.
        /// </summary>
        public void RemoveConnectingWaypoints(List<RoadObject> roads)
        {
            WaypointUtility.RemoveConnectingWaypoints(roads, TrafficLaneType.Car);
            WaypointUtility.RemoveConnectingWaypoints(roads, TrafficLaneType.Pedestrian);
        }

        /// <summary>
        ///     Returns a list of all registered waypoints of the specified type.
        /// </summary>
        public List<Waypoint> GetWaypoints(TrafficLaneType trafficLaneType)
        {
            var waypoints = new List<Waypoint>();
            var sceneObjects = GetSceneObjects();
            for (var i = 0; i < sceneObjects.Count; i++)
            {
                var trafficLanes = sceneObjects[i].GetTrafficLanes(trafficLaneType);
                for (var j = 0; j < trafficLanes.Count; j++) waypoints.AddRange(trafficLanes[j].GetWaypoints());
            }

            return waypoints;
        }

        /// <summary>
        ///     For each road marked as 'expanded' in the Road Setup tab, a corresponding road is created in the scene.
        ///     These objects are parented to the 'Road Preview' GameObject and not registered in the system.
        /// </summary>
        public void ConstructPreviewRoads(Transform parent, float roadLength, bool spawnObjects, bool elevated)
        {
            var roadSettings = new RoadSettings();
            var expandedRoads = new List<Road>(_RoadSet.roads.Where(road => road._editorVisible));

            if (expandedRoads.Count == 0)
            {
                Debug.LogWarning("No roads are marked as expanded.\n" +
                                 "Please check the Road Setup tab in the inspector to see if all roads are collapsed.");
                return;
            }

            var LOD = componentSettings.lodList.Count > 1;

            var positionX = 0f;
            RoadDescr lastRoadDescr = default;
            for (var i = 0; i < expandedRoads.Count; i++)
            {
                // Road

                var road = expandedRoads[i];
                var roadDescr = new RoadDescr(this, road, componentSettings, _DefaultReferences, _RoadSet.lanePresets);
                SplineEdgeUtility.CreateRoadLanes(componentSettings, roadDescr, _RoadSet.lanePresets);
                roadDescr.trafficLanesEditor = TrafficUtility.GetTrafficLanesEditor(_RoadSet.trafficLanePresets, roadDescr);

                if (i > 0) positionX += lastRoadDescr!.width + roadDescr.width;
                var position01 = new float3(positionX, 0f, 0f);
                var position02 = new float3(positionX, 0f, roadLength);
                lastRoadDescr = roadDescr;

                var roadData = RoadCreationData.GenerateRoadData(roadSettings, sceneData, roadDescr, position01, position02, false,
                    out var roadObjectClass, out var overlap01, out var overlap02);

                var roadObject = RoadCreation.CreateRoad(roadDescr, roadObjectClass.spline, elevated, false, 1f);
                if (LOD) LODCreation.CreateLODs(roadObject, componentSettings.lodList, road.shadowCastingMode);

                roadObject.previewObject = true;

                if (spawnObjects)
                    SpawnObjectUtility.SpawnObjects(_RoadSet.spawnObjectPresets, new List<RoadObject> {roadObject}, new List<IntersectionObject>(),
                        sceneData, new List<int>(), new List<int>());

                roadObject.transform.SetParent(parent);


                // Intersection
                var centerPosition = position02 + new float3(0f, 0f, roadDescr.width);

                var offsetLength = roadDescr.width * 0.5f + componentSettings.intersectionDistance;
                var knotPosition01 = centerPosition + new float3(-offsetLength, 0f, 0f);
                var knotPosition02 = centerPosition + new float3(offsetLength, 0f, 0f);
                var knotPosition03 = centerPosition + new float3(0f, 0f, offsetLength);

                var tan01 = knotPosition01 - centerPosition;
                var tan02 = knotPosition02 - centerPosition;
                var tan03 = knotPosition03 - centerPosition;
                var knot01 = new BezierKnot(knotPosition01, tan01, -tan01, quaternion.identity);
                var knot02 = new BezierKnot(knotPosition02, tan02, -tan02, quaternion.identity);
                var knot03 = new BezierKnot(knotPosition03, tan03, -tan03, quaternion.identity);

                var knotDatas = new List<KnotData>
                {
                    new(roadDescr, knot01, 0),
                    new(roadDescr, knot02, 1),
                    new(roadDescr, knot03, 2)
                };

                var constructionObjects = new ConstructionObjects();

                IntersectionCreation.CreateIntersection(componentSettings, constructionObjects,
                    knotDatas, centerPosition, elevated, false);

                var intersection = constructionObjects.newIntersections[0];
                intersection.previewObject = true;
                intersection.transform.SetParent(parent);
            }
        }

        /// <summary>
        ///     Retrieves a list of serialized representations of all scene objects
        ///     within the road system. Each object is converted into simple, serializable
        ///     fields for saving or transmission purposes.
        /// </summary>
        /// <returns>A list of <see cref="SerializedSceneObject" /> representing the serialized road system (incl. inheritors).</returns>
        public List<SerializedSceneObject> GetSerializableRoadSystem()
        {
            var sceneObjects = GetSceneObjects();
            var serializedSceneObjects = new List<SerializedSceneObject>();
            for (var i = 0; i < sceneObjects.Count; i++) serializedSceneObjects.Add(sceneObjects[i].Serialize());
            return serializedSceneObjects;
        }

        /// <summary>
        ///     Reads serialized scene objects and adds them to the existing road system.
        /// </summary>
        /// <param name="serializedSceneObjects">A list of serialized scene objects containing data needed to restore the road system.</param>
        public void AddSerializableRoadSystem(List<SerializedSceneObject> serializedSceneObjects)
        {
            var constructionObjects = new ConstructionObjects();

            for (var i = 0; i < serializedSceneObjects.Count; i++)
            {
                var serializedBase = serializedSceneObjects[i];
                if (!TryGetRoadDescr(serializedBase!.roadName, out var roadDescr)) continue;

                serializedBase.CreateObjectFromSerializedData(constructionObjects, roadDescr);
            }

            FinalizeConstruction(constructionObjects, true, false, true);
        }

        /// <summary>
        ///     Reads serialized scene objects and reconstructs the road system.
        /// </summary>
        /// <param name="serializedSceneObjects">A list of serialized scene objects containing data needed to restore the road system.</param>
        public void SetSerializableRoadSystem(List<SerializedSceneObject> serializedSceneObjects)
        {
            ClearRoadSystem();
            AddSerializableRoadSystem(serializedSceneObjects);
        }

        /// <summary>
        ///     Removes all registered objects from the road system.
        /// </summary>
        public void ClearRoadSystem()
        {
            var constructionObjects = new ConstructionObjects();
            constructionObjects.removableIntersections.AddRange(GetIntersections());
            constructionObjects.removableRoads.AddRange(GetRoads());
            FinalizeConstruction(constructionObjects, false, false);
        }


        /********************************************************************************************************************************/
        // Private
        /********************************************************************************************************************************/

        private void RegisterSceneObjectsInternal(out List<RoadObject> sceneRoadObjects, out List<IntersectionObject> sceneIntersectionObjects)
        {
            sceneRoadObjects = new List<RoadObject>();
            sceneIntersectionObjects = new List<IntersectionObject>();

            if (!IsInitialized()) return;

            var constructionObjects = new ConstructionObjects();

            sceneRoadObjects = FindObjectsByType<RoadObject>(FindObjectsSortMode.None).ToList();
            for (var i = sceneRoadObjects.Count - 1; i >= 0; i--)
            {
                if (sceneRoadObjects[i].previewObject)
                {
                    sceneRoadObjects.RemoveAt(i);
                    continue;
                }

                if (sceneData.roadObjects.Contains(sceneRoadObjects[i]))
                {
                    sceneRoadObjects.RemoveAt(i);
                    continue;
                }

                if (!TryGetRoadDescr(sceneRoadObjects[i].road.roadName, out var roadDescr))
                {
                    sceneRoadObjects.RemoveAt(i);
                    continue;
                }

                sceneRoadObjects[i].roadDescr = roadDescr;
            }


            sceneIntersectionObjects = FindObjectsByType<IntersectionObject>(FindObjectsSortMode.None).ToList();
            for (var i = sceneIntersectionObjects.Count - 1; i >= 0; i--)
            {
                if (sceneIntersectionObjects[i].previewObject)
                {
                    sceneIntersectionObjects.RemoveAt(i);
                    continue;
                }

                if (sceneData.intersectionObjects.Contains(sceneIntersectionObjects[i]))
                {
                    sceneIntersectionObjects.RemoveAt(i);
                    continue;
                }

                if (!TryGetRoadDescr(sceneIntersectionObjects[i].road.roadName, out var roadDescr))
                {
                    sceneIntersectionObjects.RemoveAt(i);
                    continue;
                }

                sceneIntersectionObjects[i].roadDescr = roadDescr;
            }

            ConnectionUtility.CleanNullConnections(sceneRoadObjects);
            ConnectionUtility.CleanNullConnections(sceneIntersectionObjects);

            constructionObjects.newRoads.AddRange(sceneRoadObjects);
            constructionObjects.newIntersections.AddRange(sceneIntersectionObjects);
            FinalizeConstruction(constructionObjects, false, false);
        }

        /********************************************************************************************************************************/

        private ConstructionResultRoad DisplayRoadInternal(string roadName, float3 position01, float3 position02, RoadSettings roadSettings)
        {
            var result = ConstructRoadInternal(roadName, position01, position02, roadSettings, false);
            for (var i = 0; i < result.newReplacedRoads.Count; i++) ObjectUtility.DestroyObject(result.newReplacedRoads[i].gameObject);
            result.newReplacedRoads.Clear();
            AddDisplayObjects(result.CombinedNewObjects);
            return result;
        }

        private ConstructionResultRoad ConstructRoadInternal(string roadName, float3 position01, float3 position02, RoadSettings roadSettings,
            bool finalize)
        {
            if (!IsInitialized()) return new ConstructionResultRoad(false);
            if (!TryGetRoadDescr(roadName, out var roadDescr)) return new ConstructionResultRoad(false);

            var constructionObjects = new ConstructionObjects();

            ApplyGridPositions(ref position01, ref position02);

            var roadData = RoadCreationData.GenerateRoadData(roadSettings, sceneData, roadDescr, position01, position02, finalize,
                out var roadObjectClass, out var overlap01, out var overlap02);

            var constructionFails = RoadValidation.ValidateRoad(componentSettings, roadData, roadObjectClass.roadDescr,
                roadObjectClass.spline, sceneData, new List<Overlap> {overlap01, overlap02});

            var roadObject = RoadCreation.CreateRoad(roadDescr, roadObjectClass.spline, roadData.elevated, false, 1f);

            constructionObjects.newRoads.Add(roadObject);

            TryAddRoadToOverlap(constructionObjects, constructionFails, roadObject, roadData, overlap01, finalize);
            TryAddRoadToOverlap(constructionObjects, constructionFails, roadObject, roadData, overlap02, finalize);

            var result = new ConstructionResultRoad(roadData, roadObject, overlap01, overlap02, constructionFails);

            /********************************************************************************************************************************/

            if (finalize && constructionFails.Count > 0)
            {
                constructionObjects.DestroyNewObjects();
                return result;
            }

            /********************************************************************************************************************************/

            result.newRoads.AddRange(constructionObjects.newRoads);
            result.newReplacedRoads.AddRange(constructionObjects.newReplacedRoads);
            result.newIntersections.AddRange(constructionObjects.newIntersections);
            result.newReplacedIntersections.AddRange(constructionObjects.newReplacedIntersections);

            if (finalize) FinalizeConstruction(constructionObjects, true, true, true);

            return result;
        }

        private void TryAddRoadToOverlap(ConstructionObjects constructionObjects, List<ConstructionFail> constructionFails,
            RoadObject roadObject, ConstructionData roadData, Overlap _overlap, bool finalize)
        {
            if (roadData.parallelRoad) return;
            if (!_overlap.exists) return;
            var roadDescr = roadObject.roadDescr;
            var roadSpline = roadObject.splineContainer.Spline;

            if (_overlap.overlapType == OverlapType.Shared)
            {
                var sharedObject = _overlap.sharedObject;
                if (sharedObject == null) constructionFails.Add(new ConstructionFail(FailCause.MissingConnection));
                if (constructionFails.Count > 0) return;
                CustomObjectCreation.CreateCustomObject(constructionObjects, roadDescr, sharedObject!.GetConstructionSplines(), sharedObject.GetConnectionPoints(), sharedObject.IsElevated());
            }
            else
            {
                constructionFails.AddRange(_overlap.SceneObject.ValidateNewConnection(roadSpline));
                if (constructionFails.Count > 0) return;
                _overlap.SceneObject.AddRoad(constructionFails, constructionObjects, _overlap, roadObject);
            }
        }

        /********************************************************************************************************************************/

        private ConstructionResultRoundabout DisplayRoundaboutInternal(string roadName, float3 centerPosition, RoundaboutDesign design, float radius)
        {
            var result = ConstructRoundaboutInternal(roadName, centerPosition, design, radius, false);
            AddDisplayObjects(result.newIntersections);
            return result;
        }

        private ConstructionResultRoundabout ConstructRoundaboutInternal(string roadName, float3 centerPosition, RoundaboutDesign design,
            float radius, bool finalize)
        {
            if (!IsInitialized()) return new ConstructionResultRoundabout(false);
            if (!TryGetRoadDescr(roadName, out var roadDescr)) return new ConstructionResultRoundabout(false);

            var constructionFails =
                IntersectionValidation.ValidateNewRoundabout(integrations, componentSettings, sceneData, roadDescr, centerPosition, radius);

            var result = new ConstructionResultRoundabout(true)
            {
                constructionFails = constructionFails
            };

            if (constructionFails.Count > 0) return result;

            var constructionObjects = new ConstructionObjects();

            ApplyGridPosition(ref centerPosition);

            var roundabout = RoundaboutCreation.CreateRoundabout(roadDescr, new List<KnotData>(), centerPosition, design, radius);
            constructionObjects.newIntersections.Add(roundabout);

            result.newIntersections.AddRange(constructionObjects.newIntersections);

            if (finalize) FinalizeConstruction(constructionObjects, true, true, true);

            return result;
        }

        /********************************************************************************************************************************/

        private ConstructionResultRamp DisplayRampInternal(string roadName, float3 position01, float3 position02, RoadSettings roadSettings)
        {
            var result = ConstructRampInternal(roadName, position01, position02, roadSettings, false);
            AddDisplayObjects(result.CombinedNewObjects);
            return result;
        }

        private ConstructionResultRamp ConstructRampInternal(string roadName, float3 position01, float3 position02, RoadSettings roadSettings,
            bool finalize)
        {
            if (!IsInitialized()) return new ConstructionResultRamp(false);
            if (!TryGetRoadDescr(roadName, out var roadDescr)) return new ConstructionResultRamp(false);

            var constructionObjects = new ConstructionObjects();

            ApplyGridPositions(ref position01, ref position02);

            var roadData = RoadCreationData.GenerateRoadData(roadSettings, sceneData, roadDescr, position01, position02, finalize,
                out var roadObjectClass, out var overlap01, out var overlap02);
            
            var constructionFails = RoadValidation.ValidateRoad(componentSettings, roadData, roadObjectClass.roadDescr,
                roadObjectClass.spline, sceneData, new List<Overlap> {overlap01, overlap02});
            
            if ((!overlap01.exists || overlap01.overlapType != OverlapType.Road) &&
                (!overlap02.exists || overlap02.overlapType != OverlapType.Road))
            {
                constructionFails.Add(new ConstructionFail(FailCause.MissingConnection));
            }
            else
            {
                if(overlap01.overlapType == OverlapType.Road)
                    constructionFails.AddRange(IntersectionValidation.ValidateNewRamp(roadDescr, overlap01));
                if(overlap02.overlapType == OverlapType.Road)
                    constructionFails.AddRange(IntersectionValidation.ValidateNewRamp(roadDescr, overlap02));    
            }

            var roadObject = RoadCreation.CreateRoad(roadDescr, roadObjectClass.spline, roadData.elevated, true, 1f);

            constructionObjects.newRoads.Add(roadObject);

            if(overlap01.overlapType != OverlapType.Road)
                TryAddRoadToOverlap(constructionObjects, constructionFails, roadObject, roadData, overlap01, finalize);
            if(overlap02.overlapType != OverlapType.Road)
                TryAddRoadToOverlap(constructionObjects, constructionFails, roadObject, roadData, overlap02, finalize);
            
            var result = new ConstructionResultRamp(roadData, overlap01.overlapType == OverlapType.Road ? overlap01 : overlap02)
            {
                constructionFails = constructionFails
            };

            if (finalize && constructionFails.Count > 0)
            {
                constructionObjects.DestroyNewObjects();
                return result;
            }

            if (constructionFails.Count == 0)
            {
                if(overlap01.overlapType == OverlapType.Road) RampCreation.CreateNewRamp(constructionObjects, overlap01, roadObject);
                if(overlap02.overlapType == OverlapType.Road) RampCreation.CreateNewRamp(constructionObjects, overlap02, roadObject);
            }

            result.newRoads.AddRange(constructionObjects.newRoads);
            result.newReplacedRoads.AddRange(constructionObjects.newReplacedRoads);
            result.newIntersections.AddRange(constructionObjects.newIntersections);
            result.newReplacedIntersections.AddRange(constructionObjects.newReplacedIntersections);

            if (finalize) FinalizeConstruction(constructionObjects, true, true, true);

            return result;
        }

        /********************************************************************************************************************************/

        private void FinalizeConstruction(ConstructionObjects constructionObjects,
            bool spawnObjects, bool registerUndo, bool updateTerrain = false)
        {
            constructionObjects.removableRoads.ToList().ForEach(r => constructionObjects.updatableRoads.Remove(r));

            AddRoadObjects(constructionObjects.CombinedNewRoads);
            AddIntersectionObjects(constructionObjects.CombinedNewIntersections);

            ConnectionUtility.UpdateConnections(sceneData, constructionObjects);

            AddIntersectionObjects(RoadEndCreation.CreateMissingEndObjects(constructionObjects));

            ApplyComponentSettings(constructionObjects.CombinedNewObjects);

            InvokeOnRoadsRemoved(constructionObjects.removableRoads, constructionObjects.removableIntersections);
            InvokeOnRoadsAdded(constructionObjects.CombinedNewRoads, constructionObjects.CombinedNewIntersections);

            RemoveRoadObjects(constructionObjects.removableRoads);
            RemoveIntersectionObjects(constructionObjects.removableIntersections);
            
            if (updateTerrain && componentSettings.terrainSettings)
            {
                var combinedNewObjects = constructionObjects.CombinedNewObjects;
                for (var i = 0; i < combinedNewObjects.Count; i++)
                {
                    if (componentSettings.roadEnd == RoadEnd.None && combinedNewObjects[i].IsEndObject()) continue;

                    combinedNewObjects[i].CreateTerrainUpdateSplines(out var terrainSplines, out var terrainWidths, out var checkHeight);

                    for (var j = 0; j < terrainSplines.Count; j++)
                    for (var k = 0; k < componentSettings.terrains.Count; k++)
                        TerrainUpdate.UpdateTerrainInternal(componentSettings.terrains[k], terrainUpdateSettings, terrainSplines[j], terrainWidths[j],
                            checkHeight);
                }
            }

            if (spawnObjects) SpawnObjects(constructionObjects);

            for (var i = 0; i < constructionObjects.CombinedRemovableObjects.Count; i++)
                constructionObjects.CombinedRemovableObjects[i].RemoveTrafficComponent();

            if (registerUndo)
                UndoObjectUtility.RegisterUndo(componentSettings, undoParent, undoObjects, constructionObjects);
            else constructionObjects.DestroyRemovableObjects();
        }

        private void ApplyComponentSettings<T>(List<T> newSceneObjects) where T : SceneObject
        {
            UpdateColliders(newSceneObjects);
            UpdateLayersAndTags(newSceneObjects);

            if (componentSettings.addTrafficComponent)
            {
                for (var i = 0; i < newSceneObjects.Count; i++) TrafficUtility.AddTrafficComponent(newSceneObjects[i]);
                for (var i = 0; i < newSceneObjects.Count; i++) newSceneObjects[i].traffic.trafficLanes = newSceneObjects[i].CreateTrafficLanes();
                for (var i = 0; i < newSceneObjects.Count; i++)
                for (var j = 0; j < newSceneObjects[i].traffic.trafficLanes.Count; j++)
                    newSceneObjects[i].traffic.splineContainer.AddSpline(newSceneObjects[i].traffic.trafficLanes[j].spline);

                if (componentSettings.updateWaypoints)
                {
                    var roads = newSceneObjects.OfType<RoadObject>().ToList();
                    var intersections = newSceneObjects.OfType<IntersectionObject>().ToList(); // Includes inheritors
                    AddWaypoints(roads, intersections, componentSettings.waypointDistance);
                }
            }

            var LOD = componentSettings.lodList.Count > 1;
            if (LOD)
                for (var i = 0; i < newSceneObjects.Count; i++)
                    LODCreation.CreateLODs(newSceneObjects[i], componentSettings.lodList, newSceneObjects[i].road.shadowCastingMode);
        }

        private void SpawnObjects(ConstructionObjects constructionObjects)
        {
            var combinedObjects = new List<SceneObject>(constructionObjects.CombinedNewObjects);

            var combinedBounds = new Bounds();

            for (var i = 0; i < combinedObjects.Count; i++)
            {
                var splineBounds = combinedObjects[i].splineContainer.Spline.GetBounds();
                splineBounds = WorldUtility.ExtendBounds(splineBounds,
                    combinedObjects[i].roadDescr.width + componentSettings.minOverlapDistance);

                combinedBounds.Encapsulate(splineBounds);
            }

            var ignoreObjects = new List<SceneObjectBase>(combinedObjects);

            OverlapUtility.GetAllOverlapIndexes(combinedBounds, ignoreObjects, sceneData,
                out var overlapIntersectionIndexes, out var overlapRoadIndexes);

            OverlapUtility.CleanRemovedIndexes(sceneData.roadObjects, overlapRoadIndexes, constructionObjects.removableRoads);
            OverlapUtility.CleanRemovedIndexes(sceneData.intersectionObjects, overlapIntersectionIndexes, constructionObjects.removableIntersections);

            var combinedRoads = new List<RoadObject>(constructionObjects.CombinedNewRoads);
            SpawnObjectUtility.SpawnObjects(_RoadSet.spawnObjectPresets, combinedRoads, constructionObjects.CombinedNewIntersections,
                sceneData, overlapIntersectionIndexes, overlapRoadIndexes);
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        private List<GameObject> DisplayMoveIntersectionInternal(Vector3 currentPosition, float searchRadius, Vector3 newPosition,
            bool deactivateSceneObjects = true)
        {
            if (!GetMoveIntersection(searchRadius, currentPosition, out var intersection))
                return displayedMoveIntersectionObjects;

            CreateMoveIntersectionObjects(intersection, currentPosition, newPosition, out var newIntersection, out var newRoads);

            displayedMoveIntersectionObjects.Add(newIntersection.gameObject);
            for (var i = 0; i < newRoads.Count; i++) displayedMoveIntersectionObjects.Add(newRoads[i].gameObject);

            for (var i = 0; i < displayedMoveIntersectionObjects.Count; i++)
            {
                var obj = displayedMoveIntersectionObjects[i];
                obj.transform.SetParent(displayMoveIntersectionParent.transform);
            }

            if (deactivateSceneObjects)
            {
                deactivatedObjects.Add(intersection.gameObject);
                intersection.gameObject.SetActive(false);

                for (var i = 0; i < intersection.RoadConnections.Count; i++)
                {
                    deactivatedObjects.Add(intersection.RoadConnections[i].gameObject);
                    intersection.RoadConnections[i].gameObject.SetActive(false);
                }
            }

            return displayedMoveIntersectionObjects;
        }

        private ConstructionResultMoveIntersection MoveIntersectionInternal(Vector3 currentPosition, float searchRadius, Vector3 newPosition)
        {
            if (!GetMoveIntersection(searchRadius, currentPosition, out var intersection))
                return new ConstructionResultMoveIntersection(false);

            CreateMoveIntersectionObjects(intersection, currentPosition, newPosition, out var newIntersection, out var newRoads);

            var constructionObjects = new ConstructionObjects();
            constructionObjects.newReplacedIntersections.Add(newIntersection);
            constructionObjects.newReplacedRoads.AddRange(newRoads);
            constructionObjects.removableIntersections.Add(intersection);
            constructionObjects.removableRoads.AddRange(intersection.RoadConnections);

            FinalizeConstruction(constructionObjects, true, true, true);

            var moveResult = new ConstructionResultMoveIntersection(true);
            moveResult.newIntersections.Add(intersection);
            moveResult.newReplacedRoads.AddRange(newRoads);

            return moveResult;
        }

        private void CreateMoveIntersectionObjects(IntersectionObject intersection, Vector3 currentPosition, Vector3 newPosition,
            out IntersectionObject newIntersection, out List<RoadObject> newRoads)
        {
            var settings = intersection.roadDescr.settings;
            newRoads = new List<RoadObject>();

            newIntersection = intersection.Clone();

            var deltaPosition = newPosition - currentPosition;

            for (var i = 0; i < intersection.RoadConnections.Count; i++)
            {
                var roadConnection = intersection.RoadConnections[i];
                var spline = new Spline(roadConnection.splineContainer.Spline);
                OffsetRoadSpline(spline, deltaPosition, newPosition);

                var raycastOffset = Constants.RaycastOffset(settings);
                var elevated = WorldUtility.CheckElevation(spline, roadConnection.road.length, raycastOffset, settings.groundLayers,
                    settings.elevationStartHeight);

                var roadConnectionElevation = roadConnection.elevated;
                roadConnection.elevated = elevated;

                var roadCopyObj = RoadCreation.CreateReplaceRoadObject(roadConnection, spline, 1f);
                roadConnection.elevated = roadConnectionElevation;

                var roadCopy = roadCopyObj.GetComponent<RoadObject>();
                newRoads.Add(roadCopy);
            }

            newIntersection.centerPosition += deltaPosition;
            newIntersection.ClearRoadConnections();
            newIntersection.AddRoadConnections(newRoads);
            newIntersection.elevated = newRoads.Any(conn => conn.elevated);

            for (var i = 0; i < newIntersection.splineContainer.Splines.Count; i++)
                ConstructionSplineUtility.TranslateSpline(newIntersection.splineContainer.Splines[i], deltaPosition);

            var intersectionMesh = newIntersection.CreateMeshFromConnections(1f);

            var meshFilters = newIntersection.GetMeshFilters();
            for (var i = 0; i < meshFilters.Count; i++) meshFilters[i].mesh = intersectionMesh;
        }

        private bool GetMoveIntersection(float searchRadius, Vector3 currentPosition, out IntersectionObject intersection)
        {
            intersection = null;
            if (!TryGetSceneObject(currentPosition, searchRadius, out var sceneObject)) return false;
            if (sceneObject is not IntersectionObject intersectionObject) return false;

            intersection = intersectionObject;
            return true;
        }

        private void OffsetRoadSpline(Spline roadSpline, float3 deltaPosition, float3 newPosition)
        {
            var roadKnots = roadSpline.Knots.ToList();
            var nearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(roadSpline, newPosition);
            var roadKnot = roadKnots[nearestKnotIndex];
            roadKnot.Position += deltaPosition;
            roadSpline.SetKnot(nearestKnotIndex, roadKnot);
            TangentCalculation.CalculateTangents(roadSpline, componentSettings.smoothSlope, componentSettings.tangentLength);
        }

        private void AddDisplayObjects<T>(List<T> sceneObjects) where T : SceneObject
        {
            for (var i = 0; i < sceneObjects.Count; i++) sceneObjects[i].transform.SetParent(displayParent.transform);
            displayedObjects.AddRange(sceneObjects);
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        private void ApplyGridPositions(ref float3 position01, ref float3 position02)
        {
            ApplyGridPosition(ref position01);
            ApplyGridPosition(ref position02);
        }

        private void ApplyGridPosition(ref float3 position)
        {
            float3 snapPosition = componentSettings.grid;
            float3 snapOffset = componentSettings.gridOffset;

            if (snapPosition.x > 0f) position.x = Mathf.Round(position.x / snapPosition.x) * snapPosition.x;
            if (snapPosition.y > 0f) position.y = Mathf.Round(position.y / snapPosition.y) * snapPosition.y;
            if (snapPosition.z > 0f) position.z = Mathf.Round(position.z / snapPosition.z) * snapPosition.z;
            position += snapOffset;
        }

        private void AddRoadObjects(List<RoadObject> _roadObjects)
        {
            foreach (var roadObject in _roadObjects)
            {
                if (string.IsNullOrEmpty(roadObject.iD)) roadObject.iD = roadObject.name; // Should be removable in later versions.

                roadObject.gameObject.SetActive(true);
                roadObject.transform.SetParent(constructionParent.transform);
                sceneData.AddRoad(roadObject);
            }
        }

        private void AddIntersectionObjects(List<IntersectionObject> _intersectionObjects)
        {
            foreach (var intersectionObject in _intersectionObjects)
            {
                if (string.IsNullOrEmpty(intersectionObject.iD))
                    intersectionObject.iD = intersectionObject.name; // Should be removable in later versions.

                intersectionObject.gameObject.SetActive(true);
                intersectionObject.transform.SetParent(constructionParent.transform);
                sceneData.AddIntersection(intersectionObject);
            }
        }

        private void RemoveRoadObjects(List<RoadObject> _roadObjects)
        {
            if (componentSettings.addTrafficComponent && componentSettings.updateWaypoints)
                RemoveConnectingWaypoints(_roadObjects);

            var indicesToDelete = new List<int>();
            for (var i = 0; i < sceneData.roadObjects.Count; i++)
                if (_roadObjects.Contains(sceneData.roadObjects[i]))
                    indicesToDelete.Add(i);

            indicesToDelete.Reverse();
            foreach (var index in indicesToDelete) sceneData.RemoveRoad(index);
        }

        private void RemoveIntersectionObjects(List<IntersectionObject> _intersectionObjects)
        {
            var indicesToDelete = new List<int>();
            for (var i = 0; i < sceneData.intersectionObjects.Count; i++)
                if (_intersectionObjects.Contains(sceneData.intersectionObjects[i]))
                    indicesToDelete.Add(i);

            indicesToDelete.Reverse();
            foreach (var index in indicesToDelete) sceneData.RemoveIntersection(index);
        }

        private List<GameObject> DisplayDemolishObjectsInternal(Vector3 position, float searchRadius, bool deactivateSceneObjects)
        {
            PampelGames.RoadConstructor.Demolish.GetDemolishSceneObjects(componentSettings, position, searchRadius, sceneData,
                out var demolishIntersections, out var demolishRoads, out var overlap);

            if (!overlap.exists) return displayedDemolishObjects;

            if (overlap.overlapType == OverlapType.Intersection)
            {
                var intersection = Instantiate(overlap.intersectionObject.gameObject);
                displayedDemolishObjects.Add(intersection);
            }
            else
            {
                var road = Instantiate(overlap.roadObject.gameObject);
                displayedDemolishObjects.Add(road);

                for (var i = 0; i < overlap.roadObject.RoadConnections.Count; i++)
                {
                    var roadConnection = overlap.roadObject.RoadConnections[i];
                    if (!roadConnection.snapPositionSet) continue;
                    var snapRoad = Instantiate(roadConnection.gameObject);
                    displayedDemolishObjects.Add(snapRoad);
                }
            }

            for (var i = 0; i < displayedDemolishObjects.Count; i++)
            {
                var obj = displayedDemolishObjects[i];
                obj.transform.SetParent(displayDemolishParent.transform);
            }

            if (deactivateSceneObjects)
            {
                for (var i = 0; i < demolishIntersections.Count; i++)
                {
                    deactivatedObjects.Add(demolishIntersections[i].gameObject);
                    demolishIntersections[i].gameObject.SetActive(false);
                }

                for (var i = 0; i < demolishRoads.Count; i++)
                {
                    deactivatedObjects.Add(demolishRoads[i].gameObject);
                    demolishRoads[i].gameObject.SetActive(false);
                }
            }

            return displayedDemolishObjects;
        }

        private void DemolishObjectsInternal(Vector3 position, float radius)
        {
            PampelGames.RoadConstructor.Demolish.GetDemolishSceneObjects(componentSettings, position, radius, sceneData,
                out var demolishIntersections, out var demolishRoads, out var overlap);

            DemolishObjectsInternal(demolishIntersections, demolishRoads);
        }

        private void DemolishObjectsInternal(List<IntersectionObject> demolishIntersections, List<RoadObject> demolishRoads)
        {
            var constructionObjects = new ConstructionObjects();

            /********************************************************************************************************************************/

            PampelGames.RoadConstructor.Demolish.UpdateSceneObjects(constructionObjects, demolishIntersections, demolishRoads);

            /********************************************************************************************************************************/
            if (componentSettings.terrainSettings && componentSettings.undoResetsTerrain)
            {
                var combinedDemolishObjects = new List<SceneObject>(demolishRoads);
                combinedDemolishObjects.AddRange(demolishIntersections);

                for (var i = 0; i < combinedDemolishObjects.Count; i++)
                {
                    combinedDemolishObjects[i].CreateTerrainUpdateSplines(out var terrainSplines, out var terrainWidths, out var checkHeight);

                    for (var j = 0; j < terrainSplines.Count; j++)
                    for (var k = 0; k < componentSettings.terrains.Count(); k++)
                        TerrainUpdate.UpdateTerrainInternal(componentSettings.terrains[k], terrainUpdateSettings, terrainSplines[j], terrainWidths[j],
                            checkHeight, true, undoTerrains[k]);
                }
            }

            /********************************************************************************************************************************/

            constructionObjects.removableRoads.AddRange(demolishRoads);
            constructionObjects.removableIntersections.AddRange(demolishIntersections);

            FinalizeConstruction(constructionObjects, true, true, true);
        }

        private void UndoInternal()
        {
            if (undoObjects.Count == 0) return;

            var dequeuedUndo = undoObjects.Last.Value;

            /********************************************************************************************************************************/
            if (componentSettings.undoResetsTerrain)
            {
                var combinedNewObjects = dequeuedUndo.constructionObjects.CombinedNewObjects;
                for (var i = 0; i < combinedNewObjects.Count; i++)
                {
                    combinedNewObjects[i].CreateTerrainUpdateSplines(out var terrainSplines, out var terrainWidths, out var checkHeight);

                    for (var j = 0; j < terrainSplines.Count; j++)
                    for (var k = 0; k < componentSettings.terrains.Count(); k++)
                        TerrainUpdate.UpdateTerrainInternal(componentSettings.terrains[k], terrainUpdateSettings, terrainSplines[j], terrainWidths[j],
                            checkHeight, true, undoTerrains[k]);
                }
            }
            /********************************************************************************************************************************/

            var constructionObjects = new ConstructionObjects();
            constructionObjects.newReplacedRoads.AddRange(dequeuedUndo.constructionObjects.removableRoads);
            constructionObjects.newIntersections.AddRange(dequeuedUndo.constructionObjects.removableIntersections);
            constructionObjects.removableIntersections.AddRange(dequeuedUndo.constructionObjects.CombinedNewIntersections);
            constructionObjects.removableRoads.AddRange(dequeuedUndo.constructionObjects.CombinedNewRoads);

            FinalizeConstruction(constructionObjects, false, false, true);

            undoObjects.RemoveLast();
            ObjectUtility.DestroyObject(dequeuedUndo.gameObject);
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        private void InvokeOnRoadsAdded(List<RoadObject> _roadObjects, List<IntersectionObject> _intersectionObjects)
        {
            if (_roadObjects.Count == 0 && _intersectionObjects.Count == 0) return;
            OnRoadsAdded(_roadObjects, _intersectionObjects);
        }

        private void InvokeOnRoadsRemoved(List<RoadObject> _roadObjects, List<IntersectionObject> _intersectionObjects)
        {
            if (_roadObjects.Count == 0 && _intersectionObjects.Count == 0) return;
            OnRoadsRemoved(_roadObjects, _intersectionObjects);
        }
    }
}