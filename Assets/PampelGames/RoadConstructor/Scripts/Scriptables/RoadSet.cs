// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public class RoadSet : ScriptableObject
    {
        public List<Road> roads = new();
        public List<LanePreset> lanePresets = new();
        public List<SpawnObjectPreset> spawnObjectPresets = new();
        public List<TrafficLanePreset> trafficLanePresets = new();
    }
}