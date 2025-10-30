// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    public abstract class SceneObject : SceneObjectBase
    {
        public Road road;
        public SplineContainer splineContainer;
        public bool elevated;
        public Traffic traffic;
        
        [HideInInspector] public bool previewObject;

        public float Width => roadDescr.width;
        public Bounds Bounds => meshRenderer.bounds;

        internal RoadDescr roadDescr;

        [SerializeField] private List<IntersectionObject> intersectionConnections = new();

        [SerializeField] private List<RoadObject> roadConnections = new();

        /// <summary>
        ///     Incoming / outgoing <see cref="IntersectionObject" />s.
        /// </summary>
        public List<IntersectionObject> IntersectionConnections => intersectionConnections;

        /// <summary>
        ///     Incoming / outgoing <see cref="RoadObject" />s.
        /// </summary>
        public List<RoadObject> RoadConnections => roadConnections;

        public List<SceneObject> Connections => intersectionConnections.Cast<SceneObject>().Concat(roadConnections).ToList();
        
        /********************************************************************************************************************************/
        // Virtual
        /********************************************************************************************************************************/

        /// <summary>
        ///     Prefix for naming scene objects.
        /// </summary>
        public virtual string NamePrefix()
        {
            return Constants.PrefixIntersection;
        }

        /// <summary>
        ///     Creates a new SceneObject with an additional road after successful validation.
        /// </summary>
        /// <param name="constructionFails">Construction can still fail after initial validation. If so, add the cause and return.</param>
        /// <param name="constructionObjects">New or replaced construction objects.</param>
        /// <param name="overlap">If overlap.exists, an overlap.SceneObject is in the way, which can be this intersection as well.</param>
        /// <param name="newRoad"><see cref="RoadObject" /> to add to this intersection.</param>
        public virtual void AddRoad(List<ConstructionFail> constructionFails, ConstructionObjects constructionObjects, Overlap overlap, RoadObject newRoad)
        {
        }

        /// <summary>
        ///     Creates a new SceneObject with a removed road.
        /// </summary>
        /// <param name="constructionObjects">New or replaced construction objects.</param>
        /// <param name="removableRoad"><see cref="RoadObject" /> Road to remove from this intersection.</param>
        public virtual void RemoveRoad(ConstructionObjects constructionObjects, RoadObject removableRoad)
        {
        }

        /// <summary>
        ///     Creates a list of traffic lanes for this scene object.
        /// </summary>
        public virtual List<TrafficLane> CreateTrafficLanes()
        {
            return new List<TrafficLane>();
        }

        /// <summary>
        ///     Determines if intersection objects (traffic lights etc.) can be spawned for the scene object.
        /// </summary>
        public virtual bool CanSpawnIntersectionObjects()
        {
            return true;
        }

        /// <summary>
        ///     Creates splines used to update the terrain.
        ///     IMPORTANT: splines and widths must have the same count!
        /// </summary>
        /// <param name="checkHeight">If true, terrain will only be adjusted if it's within the height range specified in the settings.</param>
        public virtual void CreateTerrainUpdateSplines(out List<Spline> splines, out List<float> widths, out bool checkHeight)
        {
            splines = new List<Spline>();
            widths = new List<float>();
            checkHeight = false;
        }
        
        /// <summary>
        ///     Splines that are used for object spawn railings.
        ///     The perpendicular spline tangent should look towards the inside.
        /// </summary>
        public virtual List<Spline> CreateRailingSplines()
        {
            return new List<Spline>();
        }

        /// <summary>
        ///     Serializes the current scene object into a representation suitable for storage or transfer.
        /// </summary>
        public virtual SerializedSceneObject Serialize()
        {
            return new SerializedSceneObject(road.roadName, elevated);
        }

        /********************************************************************************************************************************/
        /********************************************************************************************************************************/

        public void Initialize(RoadDescr roadDescr, MeshFilter meshFilter, MeshRenderer meshRenderer, SplineContainer splineContainer, bool elevated)
        {
            iD = name;
            road = roadDescr.road;
            this.roadDescr = roadDescr;
            this.meshFilter = meshFilter;
            this.meshRenderer = meshRenderer;
            this.splineContainer = splineContainer;
            this.roadDescr = roadDescr;
            this.elevated = elevated;
        }

        public List<MeshFilter> GetMeshFilters()
        {
            var meshFilters = new List<MeshFilter>();
            meshFilters.AddRange(meshFilterLODs);
            if (meshFilters.Count == 0) meshFilters.Add(meshFilter);
            return meshFilters;
        }

        public List<MeshRenderer> GetMeshRenderers()
        {
            var meshRenderers = new List<MeshRenderer>();
            meshRenderers.AddRange(meshRendererLODs);
            if (meshRenderers.Count == 0) meshRenderers.Add(meshRenderer);
            return meshRenderers;
        }

        public SpawnedObject[] GetSpawnedObjects()
        {
            var spawnedObjects = GetComponentsInChildren<SpawnedObject>();
            return spawnedObjects;
        }

        public void DestroySpawnedObjects()
        {
            var spawnedObjects = GetComponentsInChildren<SpawnedObject>();
            for (var i = 0; i < spawnedObjects.Length; i++) ObjectUtility.DestroyObject(spawnedObjects[i].gameObject);
        }

        public List<TrafficLane> GetTrafficLanes()
        {
            return traffic.trafficLanes;
        }

        public List<TrafficLane> GetTrafficLanes(TrafficLaneType trafficLaneType)
        {
            return traffic.trafficLanes.Where(t => t.trafficLaneType == trafficLaneType).ToList();
        }

        public List<TrafficLane> GetTrafficLanes(TrafficLaneType trafficLaneType, TrafficLaneDirection trafficLaneDirection)
        {
            if (trafficLaneDirection == TrafficLaneDirection.Both) return GetTrafficLanes(trafficLaneType);
            return traffic.trafficLanes.Where(t => t.trafficLaneType == trafficLaneType && t.direction == trafficLaneDirection).ToList();
        }

        public void RemoveTrafficComponent()
        {
            if (traffic == null) return;
            var trafficLanes = GetTrafficLanes();
            for (var j = 0; j < trafficLanes.Count; j++) trafficLanes[j].RemoveWaypoints();
            ObjectUtility.DestroyObject(traffic.gameObject);
        }

        /// <summary>
        ///     Returns the squared distance to the closest point on the road.
        /// </summary>
        public float GetClosestDistanceSq(Vector3 point)
        {
            var splines = splineContainer.Splines.ToList();
            var nearestDistance = float.MaxValue;
            for (var i = 0; i < splines.Count; i++)
            {
                SplineUtility.GetNearestPoint(splines[i], point, out var nearest, out var t);
                var distance = math.distancesq(point, nearest);
                if (distance < nearestDistance) nearestDistance = distance;
            }

            return nearestDistance;
        }

        /********************************************************************************************************************************/

        public bool IsEndObject()
        {
            if (GetType() == typeof(IntersectionObject))
            {
                var intersectionObject = (IntersectionObject) this;
                return intersectionObject.RoadConnections.Count == 1;
            }

            return false;
        }

        /********************************************************************************************************************************/

        public void AddConnection(SceneObject sceneObject)
        {
            if (sceneObject is RoadObject roadObject)
            {
                if (!roadConnections.Contains(roadObject)) roadConnections.Add(roadObject);
            }
            else
            {
                var intersectionObject = sceneObject as IntersectionObject;
                if (!intersectionConnections.Contains(intersectionObject)) intersectionConnections.Add(intersectionObject);
            }
        }

        public void ClearConnections()
        {
            intersectionConnections.Clear();
            roadConnections.Clear();
        }

        /********************************************************************************************************************************/

        public void AddIntersectionConnection(IntersectionObject intersectionConnection)
        {
            intersectionConnections.Add(intersectionConnection);
        }

        /********************************************************************************************************************************/

        public void ClearRoadConnections()
        {
            roadConnections.Clear();
        }

        public void AddRoadConnection(RoadObject roadConnection)
        {
            roadConnections.Add(roadConnection);
        }

        public void AddRoadConnections(List<RoadObject> _roadConnections)
        {
            roadConnections.AddRange(_roadConnections);
        }

        public void RemoveRoadConnection(RoadObject roadConnection)
        {
            roadConnections.Remove(roadConnection);
        }

        public void RemoveIntersectionConnection(IntersectionObject intersectionConnection)
        {
            intersectionConnections.Remove(intersectionConnection);
        }
    }
}