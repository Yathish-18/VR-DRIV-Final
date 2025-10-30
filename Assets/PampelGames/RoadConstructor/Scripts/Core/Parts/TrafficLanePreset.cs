// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Utility;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PampelGames.RoadConstructor
{
    [Serializable]
    public class TrafficLanePreset : PGIModule
    {
        public bool _editorVisible = true;

        public string category;
        public string trafficLanePresetName;
        public List<TrafficLaneEditor> trafficLanes = new();

        public string ModuleName()
        {
            return string.Empty;
        }

        public string ModuleInfo()
        {
            return string.Empty;
        }

#if UNITY_EDITOR
        public VisualElement CreatePropertyGUI(TrafficLanePreset trafficLanePreset, SerializedProperty property)
        {
            return TrafficLanePresetDrawerCreation.CreatePropertyGUI(trafficLanePreset, property);
        }
#endif
    }

    [Serializable]
    public class TrafficLaneEditor
    {
        public TrafficLaneType trafficLaneType = TrafficLaneType.Car;
        public float position;
        public float width = 3f;
        public TrafficLaneDirection direction = TrafficLaneDirection.Forward;
        public float maxSpeed = 50f;
        public bool mirror = true;
    }
}