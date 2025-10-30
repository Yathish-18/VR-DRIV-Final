// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Utility;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.RoadConstructor
{
    [Serializable]
    public class Road : PGIModule
    {
        public bool _editorVisible = true;

        public string category;
        public string roadName;
        public int priority;
        public float length = 6f;
        public bool elevatable = true;
        public bool oneWay;
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;

        public List<SplineEdgeEditor> splineEdgesEditor = new();
        public List<SpawnObject> spawnObjects = new();
        public List<TrafficLaneEditor> trafficLanes = new();


        // Other
        public string ModuleName()
        {
            return "Road";
        }

        public string ModuleInfo()
        {
            return "";
        }
#if UNITY_EDITOR
        public VisualElement CreatePropertyGUI(Road _road, SerializedProperty property)
        {
            return RoadDrawerCreation.CreatePropertyGUI(_road, property);
        }

        public string GetRoadDisplayText(List<SplineEdgeEditor> allLanes)
        {
            var _category = category;
            if (!string.IsNullOrEmpty(_category)) _category += " | ";
            var _roadName = roadName;
            if (string.IsNullOrEmpty(_roadName)) _roadName = "Road";
            var roadText = _category + _roadName;
            return roadText;
        }
#endif

        public List<SplineEdgeEditor> GetAllLanes(List<LanePreset> lanePresets)
        {
            var _allLanes = new List<SplineEdgeEditor>();
            _allLanes.AddRange(splineEdgesEditor);
            for (var i = 0; i < lanePresets.Count; i++)
            {
                var categoryList = lanePresets[i].category.Split(',').ToList();
                var categoryFound = categoryList.Any(t => category == t);
                if(categoryFound) _allLanes.AddRange(lanePresets[i].lanes);
            }

            return _allLanes;
        }
    }
}