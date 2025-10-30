// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
#endif

namespace PampelGames.RoadConstructor
{
    public class RoadDescr
    {
        public RoadConstructor roadConstructor;
        public readonly Road road;
        public readonly ComponentSettings settings;
        public readonly SO_DefaultReferences _DefaultReferences;
        public readonly Material intersectionMaterial;

        public readonly float width;
        public readonly int resolution;
        public readonly int detailResolution;
        
        public List<Lane> lanes;
        public List<Lane> lanesElevated;
        public List<Lane> lanesLeft;
        public List<Lane> lanesRight;
        public List<Lane> lanesMiddle;
        public List<Lane> lanesRoadEnd;
        public List<Lane> lanesIntersection;
        public List<Lane> lanesIntersectionElevated;
        public List<Lane> lanesElevatedOnly;

        public float sideLanesWidth;
        public float sideLanesCenterDistance;
        public List<Lane> lanesLeftOffset;
        public List<Lane> lanesRightOffset;

        public List<TrafficLaneEditor> trafficLanesEditor;
        
        public List<TrafficLaneEditor> GetSortedTrafficLanesEditor(TrafficLaneType trafficLaneType, TrafficLaneDirection trafficLaneDirection)
        {
            var trafficLanesEditorAll = trafficLanesEditor
                .Where(t => t.trafficLaneType == trafficLaneType && t.direction == trafficLaneDirection).ToList();

            trafficLanesEditorAll.AddRange(trafficLanesEditor
                .Where(t => t.trafficLaneType == trafficLaneType && t.direction == TrafficLaneDirection.Both));
            
            trafficLanesEditorAll = trafficLanesEditorAll.OrderBy(t => t.position).ToList();
            
            
            return trafficLanesEditorAll;
        }



        public RoadDescr(RoadConstructor roadConstructor, Road road, ComponentSettings settings, SO_DefaultReferences _DefaultReferences, List<LanePreset> lanePresets)
        {
            this.roadConstructor = roadConstructor;
            this.road = road;
            this.settings = settings;
            this._DefaultReferences = _DefaultReferences;

            var _allLanes = road.GetAllLanes(lanePresets);

            for (var i = 0; i < _allLanes.Count; i++)
                if (_allLanes[i].laneType == LaneType.Intersection)
                {
                    intersectionMaterial = _allLanes[i].material;
                    break;
                }

            if (intersectionMaterial == null && road.splineEdgesEditor.Count > 0) intersectionMaterial = road.splineEdgesEditor[0].material;

            resolution = (int) math.round(settings.resolution * (road.length / 10f));
            detailResolution = (int) math.round(settings.detailResolution * (road.length / 10f));

            if (resolution < 1) resolution = 1;
            if (detailResolution < 1) detailResolution = 1;

            var xMin = float.MaxValue;
            var xMax = float.MinValue;
            for (int i = 0; i < _allLanes.Count; i++)
            {
                var lane = _allLanes[i];
                xMin = math.min(xMin, lane.positionX.x);
                xMax = math.max(xMax, lane.positionX.y);
            }
            
            width = xMax - xMin;
        }
    }
}