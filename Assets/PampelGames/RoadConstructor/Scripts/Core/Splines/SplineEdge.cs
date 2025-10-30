// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using PampelGames.Shared.Utility;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    
    // Wait with rename until Legacy folder is removed
    [Serializable]
    public class SplineEdgeEditor : PGIDuplicable
    {
        public LaneType laneType = LaneType.Road;
        public Material material;
        public Vector2 positionX;
        public Vector2 uvX = new(0f, 1f);
        public float height;
        public bool closedEnds = true;
        public bool invert;

        public SplineEdgeEditor()
        {
            // Default constructor needed for copy class.
        }
        
        public SplineEdgeEditor(SplineEdgeEditor splineEdgeEditor)
        {
            laneType = splineEdgeEditor.laneType;
            material = splineEdgeEditor.material;
            positionX = splineEdgeEditor.positionX;
            uvX = splineEdgeEditor.uvX;
            height = splineEdgeEditor.height;
            invert = splineEdgeEditor.invert;
        }

        public SplineEdgeEditor(Material material, Vector2 positionX, Vector2 uvX, float height)
        {
            this.material = material;
            this.positionX = positionX;
            this.uvX = uvX;
            this.height = height;
        }

        public void InvertLaneToRight(float roadSize)
        {
            positionX = new Vector2(roadSize - positionX.y, roadSize - positionX.x);
            uvX = new Vector2(uvX.y, uvX.x);
        }
    }

    internal static class SplineEdgeUtility
    {
        public static void CreateRoadLanes(ComponentSettings settings, RoadDescr roadDescr, List<LanePreset> lanePresets)
        {
            var road = roadDescr.road;

            roadDescr.lanes = new List<Lane>();
            roadDescr.lanesLeft = new List<Lane>();
            roadDescr.lanesRight = new List<Lane>();
            roadDescr.lanesMiddle = new List<Lane>();
            roadDescr.lanesElevated = new List<Lane>();
            roadDescr.lanesRoadEnd = new List<Lane>();
            roadDescr.lanesIntersection = new List<Lane>();
            roadDescr.lanesIntersectionElevated = new List<Lane>();
            roadDescr.lanesElevatedOnly = new List<Lane>();
            
            /********************************************************************************************************************************/

            var lanes = road.GetAllLanes(lanePresets);

            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                switch (lane.laneType)
                {
                    case LaneType.Road:
                        AddRoadLanePrivate(lane);
                        continue;
                    case LaneType.LeftSide:
                        AddSideLanePrivate(lane);
                        continue;
                    case LaneType.Intersection:
                        AddIntersectionLanePrivate(lane);
                        continue;
                    case LaneType.ElevatedOnly:
                        AddElevatedLanePrivate(lane);
                        continue;
                    case LaneType.RoadEnd:
                        AddRoadEndLanePrivate(lane);
                        continue;
                }
            }
            
            /********************************************************************************************************************************/

            roadDescr.sideLanesWidth = roadDescr.lanesLeft.Sum(lane => lane.width);

            float maxPosX, minPosX;

            if (roadDescr.lanesLeft.SelectMany(lane => lane.splineEdges).Any())
            {
                maxPosX = roadDescr.lanesLeft.SelectMany(lane => lane.splineEdges).Max(edge => edge.position.x);
                minPosX = roadDescr.lanesLeft.SelectMany(lane => lane.splineEdges).Min(edge => edge.position.x);
            }
            else
            {
                maxPosX = 0;
                minPosX = 0;
            }

            roadDescr.sideLanesCenterDistance = math.abs((minPosX + maxPosX) / 2f);

            roadDescr.lanesLeftOffset = new List<Lane>();
            roadDescr.lanesRightOffset = new List<Lane>();

            for (var i = 0; i < roadDescr.lanesLeft.Count; i++)
            {
                var laneOffset = LaneUtility.CreateLaneOffset(roadDescr, roadDescr.lanesLeft[i], true);
                roadDescr.lanesLeftOffset.Add(laneOffset);
            }

            for (var i = 0; i < roadDescr.lanesRight.Count; i++)
            {
                var laneOffset = LaneUtility.CreateLaneOffset(roadDescr, roadDescr.lanesRight[i], false);
                roadDescr.lanesRightOffset.Add(laneOffset);
            }

            /********************************************************************************************************************************/
            
            if (roadDescr.lanesIntersection.Count == 0) roadDescr.lanesIntersection = roadDescr.lanes;
            roadDescr.lanesIntersectionElevated.AddRange(roadDescr.lanesIntersection);
            roadDescr.lanesIntersectionElevated.AddRange(roadDescr.lanesElevated);
            roadDescr.lanesElevated.AddRange(roadDescr.lanes);
            
            /********************************************************************************************************************************/
            return;

            void AddRoadLanePrivate(SplineEdgeEditor edgeRoad)
            {
                var _splineEdges = CreateSplineEdges(roadDescr, edgeRoad, settings.baseRoadHeight);
                var _laneClass = LaneUtility.CreateLane(_splineEdges, edgeRoad.height, edgeRoad.closedEnds, edgeRoad.material);
                roadDescr.lanes.Add(_laneClass);
                roadDescr.lanesMiddle.Add(_laneClass);
            }

            void AddSideLanePrivate(SplineEdgeEditor edgeSide)
            {
                var splineEdgesSide = CreateSplineEdges(roadDescr, edgeSide, settings.baseRoadHeight);
                var laneClassSide = LaneUtility.CreateLane(splineEdgesSide, edgeSide.height, edgeSide.closedEnds, edgeSide.material);
                roadDescr.lanesLeft.Add(laneClassSide);
                roadDescr.lanes.Add(laneClassSide);

                var splineEdgeOtherSide = new SplineEdgeEditor(edgeSide);
                splineEdgeOtherSide.InvertLaneToRight(roadDescr.width);
                var splineEdgesOtherSide = CreateSplineEdges(roadDescr, splineEdgeOtherSide, settings.baseRoadHeight);
                var laneClassOtherSide = LaneUtility.CreateLane(splineEdgesOtherSide, splineEdgeOtherSide.height, splineEdgeOtherSide.closedEnds, splineEdgeOtherSide.material);
                roadDescr.lanesRight.Add(laneClassOtherSide);
                roadDescr.lanes.Add(laneClassOtherSide);
            }

            void AddIntersectionLanePrivate(SplineEdgeEditor edgeIntersection)
            {
                var splineEdgesEditorIntersection =
                    new SplineEdgeEditor(edgeIntersection.material, edgeIntersection.positionX, edgeIntersection.uvX, edgeIntersection.height);
                var splineEdgesBase = CreateSplineEdges(roadDescr, splineEdgesEditorIntersection, settings.baseRoadHeight);
                var laneClassBase =
                    LaneUtility.CreateLane(splineEdgesBase, splineEdgesEditorIntersection.height, splineEdgesEditorIntersection.closedEnds, splineEdgesEditorIntersection.material);
                roadDescr.lanesIntersection.Add(laneClassBase);
            }
            
            void AddElevatedLanePrivate(SplineEdgeEditor edgeRoad)
            {
                var _splineEdges = CreateSplineEdges(roadDescr, edgeRoad, settings.baseRoadHeight);
                var _laneClass = LaneUtility.CreateLane(_splineEdges, edgeRoad.height, edgeRoad.closedEnds, edgeRoad.material);
                roadDescr.lanesElevated.Add(_laneClass);
                roadDescr.lanesElevatedOnly.Add(_laneClass);
            }
            
            void AddRoadEndLanePrivate(SplineEdgeEditor edgeRoad)
            {
                var _splineEdges = CreateSplineEdges(roadDescr, edgeRoad, settings.baseRoadHeight);
                var _laneClass = LaneUtility.CreateLane(_splineEdges, edgeRoad.height, edgeRoad.closedEnds, edgeRoad.material);
                roadDescr.lanesRoadEnd.Add(_laneClass);
            }
        }

        private static SplineEdge[] CreateSplineEdges(RoadDescr roadDescr, SplineEdgeEditor splineEdgeEditor, float baseRoadHeight)
        {
            var height = splineEdgeEditor.height;
            var halfRoad = roadDescr.width * 0.5f;
            var positionX = splineEdgeEditor.positionX.x - halfRoad;
            var positionY = splineEdgeEditor.positionX.y - halfRoad;

            
            if (splineEdgeEditor.invert)
            {
                (positionX, positionY) = (positionY, positionX);
                height *= -1f;
            }
            
            var correctedHeight = height;
            if (!splineEdgeEditor.invert) correctedHeight += baseRoadHeight;
            
            var normalRotation = splineEdgeEditor.invert ? 180f : 0f;
            var splineEdges = new List<SplineEdge>
            {
                new()
                {
                    position = new float2(positionX, correctedHeight), uvX = splineEdgeEditor.uvX.x, normalRotation = normalRotation
                },
                new()
                {
                    position = new float2(positionY, correctedHeight), uvX = splineEdgeEditor.uvX.y, normalRotation = normalRotation
                }
            };


            if (!Mathf.Approximately(height, 0f))
            {
                var positionDif = math.abs(math.abs(splineEdgeEditor.positionX.y) - math.abs(splineEdgeEditor.positionX.x));
                var uvDif = splineEdgeEditor.uvX.y - splineEdgeEditor.uvX.x;
                var heightPositionRatio = height / math.max(0.0001f, positionDif);
                var uvRatio = heightPositionRatio * uvDif;
                
                splineEdges.Insert(0, new SplineEdge
                {
                    position = new float2(positionX, 0f), uvX = splineEdgeEditor.uvX.x + uvRatio, normalRotation = -90f + normalRotation
                });

                splineEdges.Add(new SplineEdge
                {
                    position = new float2(positionY, 0f), uvX = splineEdgeEditor.uvX.y - uvRatio, normalRotation = 90f + normalRotation
                });
            }


            return splineEdges.ToArray();
        }
    }
}