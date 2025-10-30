// ----------------------------------------------------
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
    ///     Represents the scene object created on an <see cref="ISharedObject" />.
    /// </summary>
    public class CustomObject : IntersectionObject
    {
        public List<SplineMeshParameter> constructionSplines;
        public List<ConnectionPoint> connectionPoints;

        /********************************************************************************************************************************/
        // SceneObjectBase
        /********************************************************************************************************************************/

        public override Vector3 SnapPosition(Vector3 position)
        {
            if (ConnectionPointUtility.TryGetFreeConnection(connectionPoints, position, out var nearestConnectionPoint))
                return nearestConnectionPoint.position;

            return meshRenderer.bounds.center;
        }

        public override void AlignTrack(float width, ref float3 position01, ref float3 tangent01, ref float3 position02, ref float3 tangent02,
            bool directConnection)
        {
            if (ConnectionPointUtility.TryGetFreeConnection(connectionPoints, position01, out var nearestConnectionPoint))
            {
                position01 = nearestConnectionPoint.position;
                tangent01 = nearestConnectionPoint.tangent;
            }
        }

        public override List<ConstructionFail> ValidateNewConnection(Spline spline)
        {
            var constructionFails = new List<ConstructionFail>();

            if (!ConnectionPointUtility.TryGetFreeConnection(connectionPoints, spline.GetBounds().center, out var nearestConnectionPoint))
                constructionFails.Add(new ConstructionFail(FailCause.MissingConnection));

            return constructionFails;
        }

        public override Mesh CreateMeshFromConnections(float lodAmount)
        {
            var newMesh = CustomObjectCreation.CreateCustomMesh(roadDescr, constructionSplines, elevated, lodAmount,
                out var rampMaterials);

            meshRenderer.sharedMaterials = rampMaterials.ToArray();
            return newMesh;
        }

        /********************************************************************************************************************************/
        // SceneObject
        /********************************************************************************************************************************/

        public override void AddRoad(List<ConstructionFail> constructionFails, ConstructionObjects constructionObjects, Overlap overlap, RoadObject newRoad)
        {
            CustomObjectCreation.CreateCustomObject(constructionObjects, roadDescr, constructionSplines, connectionPoints, elevated);
            constructionObjects.removableIntersections.Add(this);
        }

        public override void RemoveRoad(ConstructionObjects constructionObjects, RoadObject removableRoad)
        {
            var nearestKnot = ConstructionSplineUtility.GetNearestKnot(removableRoad.splineContainer.Spline, centerPosition);
            var nearestConnection = ConnectionPointUtility.GetNearestConnection(connectionPoints, nearestKnot.Position);
            nearestConnection.used = false;

            CustomObjectCreation.CreateCustomObject(constructionObjects, roadDescr, constructionSplines, connectionPoints, elevated);
        }

        public override List<Spline> CreateRailingSplines()
        {
            return new List<Spline>();
        }
    }
}