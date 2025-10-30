// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.RoadConstructor
{
    /// <summary>
    ///     A Road Builder which can be executed in editor mode.
    ///     Note that the execution logic is within the inspector script.
    /// </summary>
    [AddComponentMenu("Pampel Games/Road Builder")]
    public class RoadBuilder : RoadBuilderBase
    {
        public bool _editorSettingsVisible = true;

        public bool checkExistingMeshes = true;
    }
}