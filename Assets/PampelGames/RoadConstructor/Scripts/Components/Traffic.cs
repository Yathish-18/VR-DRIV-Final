// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using PampelGames.Shared.Construction;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    public class Traffic : MonoBehaviour
    {
        public SceneObject sceneObject;
        public SplineContainer splineContainer;

        public List<TrafficLane> trafficLanes = new();
    }

    [Serializable]
    public class TrafficLane
    {
        public TrafficLaneType trafficLaneType;
        public TrafficLaneDirection direction;
        public float position;
        public float width;
        public float maxSpeed;
        public Spline spline;
        public Spline heightSpline;
        public bool crossing;

        public List<Waypoint> waypoints = new();

        // The start waypoint will search for waypoints into this lane within this SceneObject.
        [HideInInspector] public bool waypointInnerConnectionIn;
        // The end waypoint will search for waypoints out of this lane within this SceneObject.
        [HideInInspector] public bool waypointInnerConnectionOut;

        public TrafficLane(TrafficLaneEditor trafficLaneEditor, Spline spline, Spline heightSpline, bool crossing)
        {
            trafficLaneType = trafficLaneEditor.trafficLaneType;
            position = trafficLaneEditor.position;
            width = trafficLaneEditor.width;
            direction = trafficLaneEditor.direction == TrafficLaneDirection.Both ? TrafficLaneDirection.Forward : trafficLaneEditor.direction;
            maxSpeed = trafficLaneEditor.maxSpeed;
            this.spline = spline;
            this.heightSpline = heightSpline;
            this.crossing = crossing;
        }

        public void SetWaypoints(List<Waypoint> _waypoints)
        {
            waypoints = _waypoints;
        }

        public List<Waypoint> GetWaypoints()
        {
            return waypoints;
        }

        public void RemoveWaypoints()
        {
            for (var k = 0; k < waypoints.Count; k++)
            {
                for (var l = 0; l < waypoints[k].prev.Count; l++) waypoints[k].prev[l].next.Remove(waypoints[k]);
                for (var l = 0; l < waypoints[k].next.Count; l++) waypoints[k].next[l].prev.Remove(waypoints[k]);
                    
                ObjectUtility.DestroyObject(waypoints[k].gameObject);
            }
            waypoints.Clear();
        }

        public Waypoint GetNearestWaypoint(Vector3 point)
        {
            if (waypoints.Count == 0) return null;

            var nearestIndex = new NativeReference<int>(Allocator.TempJob);
            var waypointPositions = new NativeArray<float3>(waypoints.Count, Allocator.TempJob);
            for (var i = 0; i < waypoints.Count; i++) waypointPositions[i] = waypoints[i].transform.position;

            var job = new NearestWaypointJob
            {
                _waypointPositions = waypointPositions,
                _nearestIndex = nearestIndex,
                _point = point
            };

            var jobHandle = job.Schedule();
            jobHandle.Complete();

            var nearestWaypoint = waypoints[nearestIndex.Value];

            waypointPositions.Dispose();
            nearestIndex.Dispose();

            return nearestWaypoint;
        }

        [BurstCompile]
        private struct NearestWaypointJob : IJob
        {
            [ReadOnly] public NativeArray<float3> _waypointPositions;
            public NativeReference<int> _nearestIndex;
            public float3 _point;

            public void Execute()
            {
                var nearestDistanceSqr = float.MaxValue;
                var nearestIndex = -1;

                for (var i = 0; i < _waypointPositions.Length; i++)
                {
                    var distanceSqr = math.distancesq(_waypointPositions[i], _point);
                    if (!(distanceSqr < nearestDistanceSqr)) continue;
                    nearestDistanceSqr = distanceSqr;
                    nearestIndex = i;
                }

                _nearestIndex.Value = nearestIndex;
            }
        }
    }
}