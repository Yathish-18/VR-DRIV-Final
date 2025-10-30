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
    public class RoundaboutObject : IntersectionObject
    {
        public RoundaboutDesign roundaboutDesign = RoundaboutDesign.Default;
        public float radius;

        
        /********************************************************************************************************************************/
        // IntersectionObject
        /********************************************************************************************************************************/
        public override IntersectionObject Clone()
        {
            var roundabout = IntersectionCreation.CreateReplaceIntersectionObject(this) as RoundaboutObject;
            roundabout!.radius = radius;
            roundabout.roundaboutDesign = roundaboutDesign;
            return roundabout;
        }
        
        /********************************************************************************************************************************/
        // SceneObjectBase
        /********************************************************************************************************************************/
        
        public override void AlignTrack(float width, ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02, bool directConnection)
        {
            SnapUtility.AlignRoadToRoundabout(this, ref position01, ref tangent01, ref position02, ref tangent02, directConnection);
        }
        
        public override List<ConstructionFail> ValidateNewConnection(Spline spline)
        {
            return IntersectionValidation.ValidateRoundaboutAddRoad(this);
        }
        
        public override Mesh CreateMeshFromConnections(float lodAmount)
        {
            var meshDatas = IntersectionCreation.CreateKnotDatas(centerPosition, RoadConnections);
            var newMesh = RoundaboutCreation.CreateRoundaboutMesh(roadDescr, centerPosition, roundaboutDesign, radius, lodAmount,
                meshDatas, out var materials, out var splines, out var splineMiddle);
            
            meshRenderer.sharedMaterials = materials.ToArray();
            return newMesh;
        }
        
        /********************************************************************************************************************************/
        // SceneObject
        /********************************************************************************************************************************/
        
        public override string NamePrefix()
        {
            return Constants.PrefixRoundabout;
        }

        public override void AddRoad(List<ConstructionFail> constructionFails, ConstructionObjects constructionObjects, Overlap overlap, RoadObject newRoad)
        {
            RoundaboutCreation.AddRoad(constructionObjects, overlap, newRoad, roadDescr, centerPosition, roundaboutDesign, radius);
        }

        public override void RemoveRoad(ConstructionObjects constructionObjects, RoadObject removableRoad)
        {
            RoundaboutCreation.RemoveRoad(this, removableRoad, constructionObjects);
        }

        public override List<TrafficLane> CreateTrafficLanes()
        {
            return TrafficUtility.CreateTrafficLanesRoundabout(this);
        }
        
        public override List<Spline> CreateRailingSplines()
        {
            return new List<Spline>();
        }

        public override SerializedSceneObject Serialize()
        {
            var meshDatas = IntersectionCreation.CreateKnotDatas(centerPosition, RoadConnections);
            return new SerializedRoundabout(road.roadName, elevated, centerPosition, roundaboutDesign, radius, meshDatas);
        }
    }
}