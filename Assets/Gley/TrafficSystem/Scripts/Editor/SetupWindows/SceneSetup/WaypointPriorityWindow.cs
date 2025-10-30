using Gley.TrafficSystem.Internal;
using Gley.UrbanSystem.Editor;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class WaypointPriorityWindow : TrafficSetupWindow
    {
        private readonly float _scrollAdjustment = 104;
        private List<int> _priorities;
        private TrafficWaypointEditorData _trafficWaypointData;
        private TrafficWaypointDrawer _waypointDrawer;


        public override SetupWindowBase Initialize(WindowProperties windowProperties, SettingsWindowBase window)
        {
            base.Initialize(windowProperties, window);
            _trafficWaypointData = new TrafficWaypointEditorData();
            _waypointDrawer = new TrafficWaypointDrawer(_trafficWaypointData);

            _priorities = GetDifferentPriorities(_trafficWaypointData.GetAllWaypoints());
            _priorities.Sort();
            RoutesColorUtility.SyncRoutesColors(_priorities, _editorSave.PriorityRoutes);

            _waypointDrawer.onWaypointClicked += WaypointClicked;
            return this;
        }


        public override void DrawInScene()
        {
            for (int i = 0; i < _priorities.Count; i++)
            {
                if (_editorSave.PriorityRoutes.Active[i])
                {
                    _waypointDrawer.ShowWaypointsWithPriority(_priorities[i], _editorSave.PriorityRoutes.RoutesColor[i]);
                }
            }

            base.DrawInScene();
        }


        protected override void ScrollPart(float width, float height)
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, false, GUILayout.Width(width - SCROLL_SPACE), GUILayout.Height(height - _scrollAdjustment));
            EditorGUILayout.LabelField("Waypoint Priorities: ");
            for (int i = 0; i < _priorities.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(_priorities[i].ToString(), GUILayout.MaxWidth(50));
                _editorSave.PriorityRoutes.RoutesColor[i] = EditorGUILayout.ColorField(_editorSave.PriorityRoutes.RoutesColor[i]);
                Color oldColor = GUI.backgroundColor;
                if (_editorSave.PriorityRoutes.Active[i])
                {
                    GUI.backgroundColor = Color.green;
                }
                if (GUILayout.Button("View"))
                {
                    _editorSave.PriorityRoutes.Active[i] = !_editorSave.PriorityRoutes.Active[i];
                    SceneView.RepaintAll();
                }

                GUI.backgroundColor = oldColor;
                EditorGUILayout.EndHorizontal();
            }

            base.ScrollPart(width, height);
            EditorGUILayout.EndScrollView();
        }


        private List<int> GetDifferentPriorities(WaypointSettings[] allWaypoints)
        {
            List<int> result = new List<int>();

            for (int i = 0; i < allWaypoints.Length; i++)
            {
                if (!result.Contains(allWaypoints[i].priority))
                {
                    result.Add(allWaypoints[i].priority);
                }
            }
            return result;
        }


        private void WaypointClicked(WaypointSettings clickedWaypoint, bool leftClick)
        {
            _window.SetActiveWindow(typeof(EditWaypointWindow), true);
        }


        public override void DestroyWindow()
        {
            if (_waypointDrawer != null)
            {
                _waypointDrawer.onWaypointClicked -= WaypointClicked;
                _waypointDrawer.OnDestroy();
            }
            base.DestroyWindow();
        }
    }
}
