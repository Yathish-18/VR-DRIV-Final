// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    internal static class Demolish
    {
        public static void GetDemolishSceneObjects(ComponentSettings settings, float3 position, float radius,
            RoadConstructor.SceneData sceneData,
            out List<IntersectionObject> demolishIntersections, out List<RoadObject> demolishRoads, out Overlap overlap)
        {
            demolishIntersections = new List<IntersectionObject>();
            demolishRoads = new List<RoadObject>();

            overlap = OverlapUtility.GetOverlap(settings, radius, settings.snapHeight * 2f, position, sceneData, new List<ConstructionBase>());

            if (!overlap.exists) return;

            if (overlap.overlapType == OverlapType.Intersection)
                demolishIntersections.Add(overlap.intersectionObject);
            else
                demolishRoads.Add(overlap.roadObject);
        }

        public static void UpdateSceneObjects(ConstructionObjects constructionObjects,
            List<IntersectionObject> demolishIntersections, List<RoadObject> demolishRoads)
        {
            for (var i = demolishRoads.Count - 1; i >= 0; i--)
            {
                var roadConnections = demolishRoads[i].RoadConnections;
                for (var j = 0; j < roadConnections.Count; j++)
                {
                    var roadConnection = roadConnections[j];
                    if (demolishRoads.Contains(roadConnection)) continue;
                    var replacedRoad = RoadCreation.CreateReplaceRoadObject(roadConnection, roadConnection.splineContainer.Spline, 1f);
                    for (var k = 0; k < roadConnection.IntersectionConnections.Count; k++)
                        roadConnection.IntersectionConnections[k].AddRoadConnection(replacedRoad); // Set manually for RemoveRoad to work properly

                    constructionObjects.newReplacedRoads.Add(replacedRoad);
                    demolishRoads.Add(roadConnection);
                }

                var intersectionConnections = demolishRoads[i].IntersectionConnections;
                for (var j = 0; j < intersectionConnections.Count; j++)
                {
                    var intersectionConnection = intersectionConnections[j];
                    if (demolishIntersections.Contains(intersectionConnection)) continue;
                    intersectionConnection.RemoveRoad(constructionObjects, demolishRoads[i]);
                    demolishIntersections.Add(intersectionConnection);
                    
                    if (intersectionConnection.RoadConnections.Count == 2) // Making sure EndObjects are created
                    {
                        var intersectionRoadConnections = intersectionConnection.RoadConnections;
                        for (int k = 0; k < intersectionRoadConnections.Count; k++)
                        {
                            if(!constructionObjects.removableRoads.Contains(intersectionRoadConnections[k]) &&
                               !demolishRoads.Contains(intersectionRoadConnections[k]))
                                constructionObjects.updatableRoads.Add(intersectionRoadConnections[k]);
                        }
                    }
                }
            }

            for (var i = demolishIntersections.Count - 1; i >= 0; i--)
            {
                if (demolishRoads.Count > 0) break; // Either road or intersection

                var roadConnections = demolishIntersections[i].RoadConnections;
                for (var j = 0; j < roadConnections.Count; j++)
                {
                    var roadConnection = roadConnections[j];
                    if (demolishRoads.Contains(roadConnection)) continue;

                    var intersectionConnections = roadConnection.IntersectionConnections;

                    for (var k = 0; k < intersectionConnections.Count; k++)
                    {
                        var intersectionConnection = intersectionConnections[k];
                        if (intersectionConnection.iD == demolishIntersections[i].iD) continue;

                        intersectionConnection.RemoveRoad(constructionObjects, roadConnection);
                        if (!demolishIntersections.Contains(intersectionConnection)) demolishIntersections.Add(intersectionConnection);
                        
                        if (intersectionConnection.RoadConnections.Count == 2) // Making sure EndObjects are created
                        {
                            var intersectionRoadConnections = intersectionConnection.RoadConnections;
                            for (int l = 0; l < intersectionRoadConnections.Count; l++)
                            {
                                if(!constructionObjects.removableRoads.Contains(intersectionRoadConnections[l]) &&
                                   !demolishRoads.Contains(intersectionRoadConnections[l]))
                                    constructionObjects.updatableRoads.Add(intersectionRoadConnections[l]);
                            }
                        }
                    }

                    demolishRoads.Add(roadConnection);

                    var roadConnectionConnections = roadConnection.RoadConnections;
                    for (var l = 0; l < roadConnectionConnections.Count; l++)
                    {
                        var roadConnectionConnection = roadConnectionConnections[l];
                        if (demolishRoads.Contains(roadConnectionConnection)) continue;
                        var replacedRoad =
                            RoadCreation.CreateReplaceRoadObject(roadConnectionConnection, roadConnectionConnection.splineContainer.Spline, 1f);
                        constructionObjects.newReplacedRoads.Add(replacedRoad);
                        demolishRoads.Add(roadConnectionConnection);
                    }
                }
            }
        }
    }
}