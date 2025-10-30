// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    /// <summary>
    ///     Roads can be connected to intersections or other roads.
    /// </summary>
    public class RoadObject : SceneObject
    {
        public float length;
        public float curvature;

        [HideInInspector] public bool snapPositionSet;
        [HideInInspector] public Vector3 snapPosition;
        [HideInInspector] public bool rampRoad;

        [HideInInspector] public string splitOriginalID;
        [HideInInspector] public Spline splitOriginalSpline;

        public void InitializeRoad(RoadDescr _roadDescr, MeshFilter _meshFilter, MeshRenderer _meshRenderer, SplineContainer _splineContainer,
            bool _elevated, bool _rampRoad)
        {
            Initialize(_roadDescr, _meshFilter, _meshRenderer, _splineContainer, _elevated);
            
            rampRoad = _rampRoad;

            length = _splineContainer.Spline.GetLength();
            var maxSnapLength = _roadDescr.width * Constants.RoadMaxSnapLength; // Approximation

            var spline = _splineContainer.Spline;
            curvature = ConstructionSplineUtility.GetCurvature(spline[0], spline[^1]);

            if (maxSnapLength > length)
            {
                var knot01 = spline[0];
                var knot02 = spline[^1];
                var intersectionPoint = PGTrigonometryUtility.IntersectionPointXZ(knot01.Position, knot01.TangentOut, knot02.Position, knot02.TangentIn);
                
                snapPositionSet = true;
                snapPosition = intersectionPoint;
            }
        }
        
        /********************************************************************************************************************************/
        // Virtual
        /********************************************************************************************************************************/

        public virtual Vector3 SnapRoadPosition(Vector3 position, out float t)
        {
            SplineUtility.GetNearestPoint(splineContainer.Spline, position, out var nearest, out t);
            return nearest;
        }
        
        /********************************************************************************************************************************/
        // SceneObjectBase
        /********************************************************************************************************************************/

        public override Vector3 SnapPosition(Vector3 position)
        {
            SplineUtility.GetNearestPoint(splineContainer.Spline, position, out var nearest, out var t);
            return nearest;
        }

        public override void AlignTrack(float width, ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02, bool directConnection)
        {
            SnapUtility.AlignRoadToRoad(this, width, ref position01, ref tangent01, ref position02, ref tangent02, directConnection);
        }

        public override List<ConstructionFail> ValidateNewConnection(Spline spline)
        {
            return new List<ConstructionFail>();
        }

        public override Mesh CreateMeshFromConnections(float lodAmount)
        {
            return RoadCreation.CreateRoadMesh(roadDescr, splineContainer.Spline, lodAmount, elevated, out var materials);
        }
        
        /********************************************************************************************************************************/
        // SceneObject
        /********************************************************************************************************************************/
        
        public override string NamePrefix()
        {
            return Constants.PrefixRoad;
        }

        public override void AddRoad(List<ConstructionFail> constructionFails, ConstructionObjects constructionObjects, Overlap overlap, RoadObject newRoad)
        {
            IntersectionCreation.AddRoad(constructionFails, constructionObjects, overlap, newRoad);
        }

        public override List<TrafficLane> CreateTrafficLanes()
        {
            return TrafficUtility.CreateTrafficLanesRoad(splineContainer.Spline, roadDescr);
        }
        
        public override void CreateTerrainUpdateSplines(out List<Spline> splines, out List<float> widths, out bool checkHeight)
        {
            TerrainUtility.CreateTerrainUpdateSplinesRoad(this, out splines, out widths);
            checkHeight = true;
        }
        
        public override List<Spline> CreateRailingSplines()
        {
            return RoadCreation.CreateRailingSplines(this);
        }

        public override SerializedSceneObject Serialize()
        {
            return new SerializedRoad(road.roadName, elevated, rampRoad, splineContainer.Spline, splitOriginalID, splitOriginalSpline);
        }
    }
}