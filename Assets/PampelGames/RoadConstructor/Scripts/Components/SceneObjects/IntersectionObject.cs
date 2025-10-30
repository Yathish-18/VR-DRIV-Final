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
    /// <summary>
    ///     Intersections are connected to roads.
    /// </summary>
    public class IntersectionObject : SceneObject
    {
        public Vector3 centerPosition;

        /********************************************************************************************************************************/
        // Virtual
        /********************************************************************************************************************************/
        
        /// <summary>
        ///     Creates a clone of the current intersection object.
        ///     Simply call the CreateReplaceIntersectionObject method and assign additional fields of the inheritor manually.
        /// </summary>
        /// <returns>A new instance of <see cref="IntersectionObject" /> that is a clone of the current object.</returns>
        public virtual IntersectionObject Clone()
        {
            return IntersectionCreation.CreateReplaceIntersectionObject(this);
        }
        
        /********************************************************************************************************************************/
        // SceneObjectBase
        /********************************************************************************************************************************/
        
        public override Vector3 SnapPosition(Vector3 position)
        {
            return centerPosition;
        }

        public override void AlignTrack(float width, ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02, bool directConnection)
        {
            SnapUtility.AlignRoadToIntersection(this, centerPosition, width, ref position01, ref tangent01, ref position02, ref tangent02, directConnection);
        }

        public override List<ConstructionFail> ValidateNewConnection(Spline spline)
        {
            return IntersectionValidation.ValidateIntersectionAddRoad(this);
        }

        public override Mesh CreateMeshFromConnections(float lodAmount)
        {
            var createIntersectionMeshDatas = IntersectionCreation.CreateKnotDatas(centerPosition, RoadConnections);
            
            var newMesh = IntersectionCreation.CreateIntersectionMesh(createIntersectionMeshDatas, centerPosition, elevated, lodAmount,
                out var newMaterials, out var newSplines, out var newPositions);
            
            meshRenderer.sharedMaterials = newMaterials.ToArray();
            return newMesh;
        }

        /********************************************************************************************************************************/
        // SceneObject
        /********************************************************************************************************************************/ 

        public override void AddRoad(List<ConstructionFail> constructionFails, ConstructionObjects constructionObjects, Overlap overlap, RoadObject newRoad)
        {
            IntersectionCreation.AddRoad(constructionFails, constructionObjects, overlap, newRoad);
        }

        public override void RemoveRoad(ConstructionObjects constructionObjects, RoadObject removableRoad)
        {
            IntersectionCreation.RemoveRoad(this, removableRoad, constructionObjects);
        }

        public override List<TrafficLane> CreateTrafficLanes()
        {
            return TrafficUtility.CreateTrafficLanesIntersection(this, traffic);
        }

        public override void CreateTerrainUpdateSplines(out List<Spline> splines, out List<float> widths, out bool checkHeight)
        {
            TerrainUtility.CreateTerrainUpdateSplinesIntersection(this, out splines, out widths);
            checkHeight = false;
        }
        
        public override List<Spline> CreateRailingSplines()
        {
            return IntersectionCreation.CreateRailingSplines(this);
        }

        public override SerializedSceneObject Serialize()
        {
            var meshDatas = IntersectionCreation.CreateKnotDatas(centerPosition, RoadConnections);
            return new SerializedIntersection(road.roadName, elevated, centerPosition, meshDatas);
        }
    }
}