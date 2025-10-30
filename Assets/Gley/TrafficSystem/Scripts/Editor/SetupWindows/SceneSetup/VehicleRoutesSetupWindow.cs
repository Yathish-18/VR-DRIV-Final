using Gley.UrbanSystem.Editor;
using Gley.UrbanSystem.Internal;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Gley.TrafficSystem.Editor
{
    public class VehicleRoutesSetupWindow : TrafficSetupWindow
    {
        private readonly float _scrollAdjustment = 104;

        private TrafficWaypointEditorData _trafficWaypointData;
        private TrafficWaypointDrawer _waypointDrawer;
        private List<int> _vehicleTypes;



        public override SetupWindowBase Initialize(WindowProperties windowProperties, SettingsWindowBase window)
        {
            base.Initialize(windowProperties, window);
            _trafficWaypointData = new TrafficWaypointEditorData();
            _waypointDrawer = new TrafficWaypointDrawer(_trafficWaypointData);
            _waypointDrawer.onWaypointClicked += WaypointClicked;
            // Get all vehicle types as int list
            _vehicleTypes = new List<int>((int[])System.Enum.GetValues(typeof(VehicleTypes)));

            // Use utility to sync colors and active flags
            RoutesColorUtility.SyncRoutesColors(_vehicleTypes, _editorSave.AgentRoutes);

            return this;
        }


        public override void DrawInScene()
        {
            for (int i = 0; i < _vehicleTypes.Count; i++)
            {
                if (_editorSave.AgentRoutes.Active[i])
                {
                    _waypointDrawer.ShowWaypointsWithVehicle(i, _editorSave.AgentRoutes.RoutesColor[i]);
                }
            }

            base.DrawInScene();
        }


        protected override void ScrollPart(float width, float height)
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, false, GUILayout.Width(width - SCROLL_SPACE), GUILayout.Height(height - _scrollAdjustment));
            EditorGUILayout.LabelField("Vehicle Routes: ");
            for (int i = 0; i < _vehicleTypes.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(((VehicleTypes)i).ToString(), GUILayout.MaxWidth(150));
                _editorSave.AgentRoutes.RoutesColor[i] = EditorGUILayout.ColorField(_editorSave.AgentRoutes.RoutesColor[i]);
                Color oldColor = GUI.backgroundColor;
                if (_editorSave.AgentRoutes.Active[i])
                {
                    GUI.backgroundColor = Color.green;
                }
                if (GUILayout.Button("View", GUILayout.MaxWidth(BUTTON_DIMENSION)))
                {
                    _editorSave.AgentRoutes.Active[i] = !_editorSave.AgentRoutes.Active[i];
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = oldColor;
                EditorGUILayout.EndHorizontal();
            }

            base.ScrollPart(width, height);
            EditorGUILayout.EndScrollView();
        }


        private void WaypointClicked(WaypointSettingsBase clickedWaypoint, bool leftClick)
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