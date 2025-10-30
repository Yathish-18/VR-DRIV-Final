// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    public class RampObject : IntersectionObject
    {
        public Spline SplineRoad => splineContainer.Spline;

        /********************************************************************************************************************************/
        // SceneObjectBase
        /********************************************************************************************************************************/
        
        public override void AlignTrack(float width, ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02, bool directConnection)
        {
            SnapUtility.SnapRoadToRamp(this, ref position01, ref tangent01, ref position02, ref tangent02, directConnection);
        }
        
        public override List<ConstructionFail> ValidateNewConnection(Spline spline)
        {
            var constructionFails = new List<ConstructionFail>();
            constructionFails.Add(new ConstructionFail(FailCause.MissingConnection));
            return constructionFails;
        }
        
        public override Mesh CreateMeshFromConnections(float lodAmount)
        {
            if (RoadConnections.Count == 0) return new Mesh();

            var rampConnection = GetRampConnection();
            var rampNearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(rampConnection.splineContainer.Spline, centerPosition);
            var nearestKnot = rampConnection.splineContainer.Spline[rampNearestKnotIndex];
            var rampKnotData = new KnotData(rampConnection.roadDescr, nearestKnot, rampNearestKnotIndex);

            var newMesh = RampCreation.CreateRampMesh(roadDescr, SplineRoad, rampKnotData, centerPosition, elevated, lodAmount,
                out var rampMaterials);
            
            meshRenderer.sharedMaterials = rampMaterials.ToArray();
            return newMesh;
        }
        
        /********************************************************************************************************************************/
        // SceneObject
        /********************************************************************************************************************************/
        public override string NamePrefix()
        {
            return Constants.PrefixRamp;
        }

        public override void AddRoad(List<ConstructionFail> constructionFails, ConstructionObjects constructionObjects, Overlap overlap, RoadObject newRoad)
        {
        }

        public override void RemoveRoad(ConstructionObjects constructionObjects, RoadObject removableRoad)
        {
            RampCreation.RemoveRoad(this, removableRoad, constructionObjects);
        }

        public override List<TrafficLane> CreateTrafficLanes()
        {
            return TrafficUtility.CreateTrafficLanesRamp(this);
        }

        public override bool CanSpawnIntersectionObjects()
        {
            return false;
        }
        
        public override void CreateTerrainUpdateSplines(out List<Spline> splines, out List<float> widths, out bool checkHeight)
        {
            TerrainUtility.CreateTerrainUpdateSplinesRamp(this, out splines, out widths);
            checkHeight = false;
        }

        public override SerializedSceneObject Serialize()
        {
            var rampConnection = GetRampConnection();
            var rampNearestKnotIndex = ConstructionSplineUtility.GetNearestKnotIndex(rampConnection.splineContainer.Spline, centerPosition);
            var nearestKnot = rampConnection.splineContainer.Spline[rampNearestKnotIndex];
            var rampKnotData = new KnotData(rampConnection.roadDescr, nearestKnot, rampNearestKnotIndex);

            return new SerializedRamp(road.roadName, centerPosition, elevated, SplineRoad, rampKnotData);
        }

        /********************************************************************************************************************************/

        internal RoadObject GetRampConnection()
        {
            var connections = RoadConnections;
            for (var i = 0; i < connections.Count; i++)
            {
                if (!connections[i].rampRoad) continue;
                return connections[i];
            }

            return null;
        }
    }
}