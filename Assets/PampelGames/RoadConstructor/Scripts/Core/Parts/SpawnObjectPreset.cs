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
    public class SpawnObjectPreset : PGIModule
    {
        public bool _editorVisible = true;

        public string category;
        public string spawnObjectName;
        public List<SpawnObject> spawnObjects = new();

        public string ModuleName()
        {
            return string.Empty;
        }

        public string ModuleInfo()
        {
            return string.Empty;
        }
        
#if UNITY_EDITOR
        public VisualElement CreatePropertyGUI(SpawnObjectPreset spawnObjectPreset, SerializedProperty property)
        {
            return SpawnObjectPresetDrawerCreation.CreatePropertyGUI(spawnObjectPreset, property);
        }
#endif
    }

    [Serializable]
    public class SpawnObject
    {
        public SpawnObjectType objectType = SpawnObjectType.Road;
        public GameObject obj;
        
        // Railing
        public float railingSpacing = 3f;
        public Vector2 railingOffset = Vector2.zero;
        public bool railingAutoSize = true;
        public ObjectTypeSelection railingObjectType = ObjectTypeSelection.Any;
        public Elevation railingElevation = Elevation.Any;
        
        // Custom
        public SpacingType spacingType = SpacingType.WorldUnits;
        public float spacing = 10;
        public SpawnObjectPosition position = SpawnObjectPosition.Middle;
        public float positionOffsetForward;
        public float positionOffsetRight;
        public float heightOffset;
        public SpawnObjectRotation rotation = SpawnObjectRotation.Inside;
        public bool alignToNormal;
        public Vector2 scale = Vector2.one;
        public bool requiresDirection;
        public bool requiresDirectionForward;
        public bool requiresDirectionLeft;
        public bool requiresDirectionRight;
        public Elevation elevation = Elevation.Any;
        public Vector2 heightRange = new(0f, 10f);
        public bool removeOverlap;
        public float chance = 1f;
    }
}