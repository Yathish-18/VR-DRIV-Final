// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public class Waypoint : MonoBehaviour
    {
        public string roadID;
        public TrafficLaneType laneType;
        public List<Waypoint> next = new();
        public List<Waypoint> prev = new();
        public float laneWidth;
        public bool startPoint;
        public bool endPoint;
        public TrafficLaneDirection direction;


        /// <summary>
        ///     Retrieves the next directional waypoints based on the current direction of the waypoint.
        /// </summary>
        public List<Waypoint> GetNextDirectionalWaypoints()
        {
            var nextWaypoints = new List<Waypoint>();
            for (var i = 0; i < next.Count; i++)
            {
                var nextWaypoint = next[i];
                if (nextWaypoint.direction != direction) continue;
                nextWaypoints.Add(nextWaypoint);
            }

            if (nextWaypoints.Count == 0) nextWaypoints.AddRange(next);

            return nextWaypoints;
        }
    }
}