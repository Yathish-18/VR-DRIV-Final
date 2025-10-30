// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    internal static class UndoConstruction
    {
        public static void SaveCurrentState(ComponentSettings settings)
        {
            if (Application.isPlaying) return;
#if UNITY_EDITOR
            if (!settings.levelHeight) return;
            
            for (int i = 0; i < settings.terrains.Count; i++)
            {
                var terrain = settings.terrains[i];
                Undo.RegisterCompleteObjectUndo(terrain.terrainData, "TerrainUndo");
            }
#endif

        }
    }
}