// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    internal static class CustomObjectCreation
    {
        public static void CreateCustomObject(ConstructionObjects constructionObjects, RoadDescr roadDescr,
            List<SplineMeshParameter> constructionSplines, List<ConnectionPoint> connectionPoints, bool elevated)
        {
            var splines = constructionSplines.Select(t => new Spline(t.spline)).ToList();

            var customMesh = CreateCustomMesh(roadDescr, constructionSplines, elevated, 1f,
                out var _materials);

            var customObj = ObjectUtility.CreateObj(Constants.PrefixCustom, ShadowCastingMode.On, out var meshFilter, out var meshRenderer);
            meshFilter.sharedMesh = customMesh;
            meshRenderer.sharedMaterials = _materials.ToArray();

            var customObject = customObj.AddComponent<CustomObject>();
            var splineContainer = customObj.AddComponent<SplineContainer>();
            splineContainer.RemoveSpline(splineContainer.Spline);
            for (var i = 0; i < splines.Count; i++) splineContainer.AddSpline(splines[i]);

            customObject.Initialize(roadDescr, meshFilter, meshRenderer, splineContainer, elevated);
            customObject.centerPosition = meshRenderer.bounds.center;
            customObject.constructionSplines = constructionSplines;
            customObject.connectionPoints = new List<ConnectionPoint>();
            for (var i = 0; i < connectionPoints.Count; i++) customObject.connectionPoints.Add(new ConnectionPoint(connectionPoints[i]));

            constructionObjects.newIntersections.Add(customObject);
        }

        public static Mesh CreateCustomMesh(RoadDescr roadDescr, List<SplineMeshParameter> constructionSplines,
            bool elevated, float lodAmount, out List<Material> newMaterials)
        {
            var road = roadDescr.road;
            newMaterials = new List<Material>();
            var combineMeshes = new List<Mesh>();
            
            for (var i = 0; i < constructionSplines.Count; i++)
            {
                var parameter = constructionSplines[i];
                var lanes = roadDescr.lanes;
                var spline = parameter.spline;
                
                var resolution = ConstructionSplineUtility.CalculateResolution(roadDescr.resolution, roadDescr.settings.smartReduce, roadDescr.settings.smoothSlope, spline.Knots.First(),
                    spline.Knots.Last(), lodAmount);
                
                parameter.SetISharedObjectValues(roadDescr.width, road.length, resolution);
                
                RoadSplineMesh.CreateMultipleSplineMeshes(lanes, parameter, out var roadMeshes, out var roadMaterials);
                
                newMaterials.AddRange(roadMaterials);
                combineMeshes.AddRange(roadMeshes);
            }

            /********************************************************************************************************************************/
            PGMeshUtility.CombineAndPackMeshes(newMaterials, combineMeshes, out var intersectionMaterials, out var intersectionMesh);
            newMaterials = new List<Material>(intersectionMaterials);
            /********************************************************************************************************************************/

            return intersectionMesh;
        }
    }
}