// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using UnityEditor;
#endif
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace PampelGames.RoadConstructor
{
    internal static class WaypointGizmos
    {

#if UNITY_EDITOR
        [DrawGizmo(GizmoType.Selected)]
        private static void DrawGizmosSelected(RoadConstructor roadConstructor, GizmoType gizmoType)
        {
            if (roadConstructor.componentSettings.waypointGizmos != PampelGames.RoadConstructor.DrawGizmos.None)
                DrawGizmos(roadConstructor);
        }

        [DrawGizmo(GizmoType.NonSelected)]
        private static void DrawGizmos(RoadConstructor roadConstructor, GizmoType gizmoType)
        {
            if (roadConstructor.componentSettings.waypointGizmos == PampelGames.RoadConstructor.DrawGizmos.Always)
                DrawGizmos(roadConstructor);
        }

        private static void DrawGizmos(RoadConstructor roadConstructor)
        {
            var settings = roadConstructor.componentSettings;
            var sceneObjects = roadConstructor.GetSceneObjects();
            
            for (int i = 0; i < sceneObjects.Count; i++)
            {
                var traffic = sceneObjects[i].traffic;
                if(traffic == null) continue;
                
                for (int j = 0; j < traffic.trafficLanes.Count; j++)
                {
                    var trafficLane = traffic.trafficLanes[j];

                    if(settings.waypointGizmosColor == DrawGizmosColor.Object)
                    {
                        if (sceneObjects[i].GetType() == typeof(RoadObject))
                            Handles.color = trafficLane.trafficLaneType == TrafficLaneType.Car
                                ? Constants.GizmoColorCar
                                : Constants.GizmoColorPedestrian;
                        else
                            Handles.color = trafficLane.trafficLaneType == TrafficLaneType.Car
                                ? Constants.GizmoColorCarIntersection
                                : Constants.GizmoColorPedestrianIntersection;
                    }
                    else
                    {
                        Handles.color = GetColor(j);
                    }
                    
                    var waypoints = trafficLane.GetWaypoints();

                    for (int k = 0; k < waypoints.Count; k++)
                    {
                        var waypoint = waypoints[k];
                        if(waypoint == null) continue;
                        

                        if (settings.waypointConnectionsOnly && !waypoint.startPoint && !waypoint.endPoint) continue;

                        if (waypoint.next.Count == 0)
                        {
                            Handles.DrawWireDisc(waypoint.transform.position, Vector3.up, settings.waypointGizmoSize);
                        }
                        else
                        {
                            var pos01 = waypoint.transform.position;
                            for (int l = 0; l < waypoint.next.Count; l++)
                            {
                                if(waypoint.next[l] == null || !waypoint.next[l].gameObject.activeInHierarchy)
                                {
                                    Gizmos.color = Color.red;
                                    Gizmos.DrawSphere(waypoint.transform.position, settings.waypointGizmoSize);
                                    continue;
                                }
                                
                                var pos02 = waypoint.next[l].transform.position;
                                
                                var rotation = quaternion.LookRotationSafe(pos02 - pos01, math.up());
                                Handles.ArrowHandleCap(0, waypoint.transform.position, rotation, settings.waypointGizmoSize, EventType.Repaint);                               
                            }
                        }
                    }
                }
            }
        }

        private static Color GetColor(int index)
        {
            return Colors[index % Colors.Length];
        }
        
        private static readonly Color[] Colors =
        {
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.magenta,
            Color.cyan,
            Color.white,
            Color.grey,
        };
#endif

    }
}