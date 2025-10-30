// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Linq;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public class Lane
    {
        public Material material;
        public SplineEdge[] splineEdges;

        public float height;
        public float width;
        public bool closedEnds;

        public float centerDistance;
        public bool centerIsLeftSide;

        public Lane()
        {
        }

        public Lane(Lane lane)
        {
            material = lane.material;
            height = lane.height;
            width = lane.width;
            closedEnds = lane.closedEnds;
            centerDistance = lane.centerDistance;
            centerIsLeftSide = lane.centerIsLeftSide;
        }
    }
    

    internal static class LaneUtility
    {
        public static Lane CreateLane(SplineEdge[] splineEdges, float height, bool closedEnds, Material material)
        {
            var centerDistance = (splineEdges[0].position.x + splineEdges[^1].position.x) * 0.5f;
            
            var minX = splineEdges.Min(edge => edge.position.x);
            var maxX = splineEdges.Max(edge => edge.position.x);
            var width = math.abs(maxX - minX);


            var lane = new Lane
            {
                material = material,
                splineEdges = splineEdges,

                width = width,
                height = height,
                closedEnds = closedEnds,

                centerDistance = math.abs(centerDistance),
                centerIsLeftSide = centerDistance < 0,
            };

            return lane;
        }
        
        public static Lane CreateLaneOffset(RoadDescr roadDescr, Lane lane, bool left)
        {
            var detailCenterDistance = (lane.splineEdges[0].position.x + lane.splineEdges[^1].position.x) * 0.5f;

            var sideSplineEdges = new SplineEdge[lane.splineEdges.Length];
            for (var j = 0; j < lane.splineEdges.Length; j++)
                sideSplineEdges[j] = new SplineEdge
                {
                    position = new float2(lane.splineEdges[j].position.x - detailCenterDistance, lane.splineEdges[j].position.y),
                    uvX = lane.splineEdges[j].uvX,
                    normalRotation = lane.splineEdges[j].normalRotation
                };

            
            var centerDistanceDif = lane.centerDistance - roadDescr.sideLanesCenterDistance;
            
            var splineEdgesOffset = new SplineEdge[lane.splineEdges.Length];

            for (int j = 0; j < splineEdgesOffset.Length; j++)
            {
                var sideLane = sideSplineEdges[j];
                var posOffsetX = left ? new float2(sideLane.position.x - centerDistanceDif, sideLane.position.y) :
                    new float2(sideLane.position.x + centerDistanceDif, sideLane.position.y);
                splineEdgesOffset[j] = new SplineEdge
                {
                    position = posOffsetX,
                    normalRotation = sideLane.normalRotation,
                    uvX = sideLane.uvX
                };
            }

            var laneOffset = new Lane(lane);
            laneOffset.splineEdges = splineEdgesOffset;
            return laneOffset;
        }
    }
}