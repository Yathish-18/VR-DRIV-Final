// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Utility;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor
{
    [Serializable]
    public class LanePreset : PGIModule
    {
        public bool _editorVisible = true;

        public string category;
        public string templateName;
        public List<SplineEdgeEditor> lanes = new();

        public string ModuleName()
        {
            return string.Empty;
        }

        public string ModuleInfo()
        {
            return string.Empty;
        }
        
#if UNITY_EDITOR
        public VisualElement CreatePropertyGUI(LanePreset lanePreset, SerializedProperty property)
        {
            return LanePresetDrawerCreation.CreatePropertyGUI(lanePreset, property);
        }
#endif
    }
}