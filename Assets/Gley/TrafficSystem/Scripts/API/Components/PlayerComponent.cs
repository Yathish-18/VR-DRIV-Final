using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if GLEY_TRAFFIC_SYSTEM
using TrafficManager = Gley.TrafficSystem.Internal.TrafficManager;
using CellData = Gley.UrbanSystem.Internal.CellData;
using PlayerWaypointsManager = Gley.TrafficSystem.Internal.PlayerWaypointsManager;
using GridData = Gley.UrbanSystem.Internal.GridData;
using TrafficWaypointsData = Gley.TrafficSystem.Internal.TrafficWaypointsData;
#endif


namespace Gley.TrafficSystem
{
    public class PlayerComponent : MonoBehaviour, ITrafficParticipant
    {
        private Rigidbody _rb;
        private Transform _myTransform;
        private bool _initialized;
#if GLEY_TRAFFIC_SYSTEM
        private List<TrafficWaypoint> _allWaypoints;
        private List<Vector2Int> _cellNeighbors;     

        private GridData _gridData;
        private CellData _currentCell;
        private PlayerWaypointsManager _playerWaypointsManager;
        private TrafficWaypointsData _trafficWaypointsData;
        private TrafficWaypoint _proposedTarget;
        private TrafficWaypoint _currentTarget;
        private Vector3 _playerPosition;
        
        private bool _targetChanged;


        private void OnEnable()
        {
            StartCoroutine(Initialize());
        }


        IEnumerator Initialize()
        {
            while (!TrafficManager.Instance.Initialized)
            {
                yield return null;
            }
            _rb = GetComponent<Rigidbody>();
            _myTransform = transform;
            _gridData = TrafficManager.Instance.GridData;
            _trafficWaypointsData = TrafficManager.Instance.TrafficWaypointsData;
            _playerWaypointsManager = TrafficManager.Instance.PlayerWaypointsManager;
            _playerWaypointsManager.RegisterPlayer(GetInstanceID(), -1);
            _allWaypoints = new List<TrafficWaypoint>();
            _initialized = true;
        }


        void Update()
        {
            if (!_initialized)
            {
                return;
            }
            _playerPosition = _myTransform.position;
            CellData cell = _gridData.GetCell(_playerPosition);

            // Update waypoints only if the player changes the grid cell
            if (cell != _currentCell)
            {
                _currentCell = cell;
                _cellNeighbors = _gridData.GetCellNeighbors(cell.CellProperties.Row, cell.CellProperties.Column, 1, false);
                _allWaypoints.Clear();

                foreach (var neighbor in _cellNeighbors)
                {
                    _allWaypoints.AddRange(_gridData.GetAllTrafficWaypointsInCell(neighbor).Select(index => _trafficWaypointsData.AllTrafficWaypoints[index]));
                }
            }

            // Find closest valid waypoint
            float minDistance = Mathf.Infinity;
            TrafficWaypoint bestWaypoint = null;

            foreach (var waypoint in _allWaypoints)
            {
                float newDistance = Vector3.SqrMagnitude(_playerPosition - waypoint.Position);
                if (newDistance < minDistance && CheckOrientation(waypoint, out TrafficWaypoint proposedTarget))
                {
                    minDistance = newDistance;
                    bestWaypoint = waypoint;
                    _proposedTarget = proposedTarget; // Store proposed target when orientation is valid
                }
            }

            if (_currentTarget == _proposedTarget)
            {
                return;
            }

            // Determine if we need to change target
            _targetChanged = false;

            if (_currentTarget != null)
            {
                if (_currentTarget.Neighbors.Contains(_proposedTarget.ListIndex))
                {
                    _targetChanged = true;
                }
                else
                {
                    Vector3 forward = _myTransform.forward;
                    float angle1 = Vector3.SignedAngle(forward, _proposedTarget.Position - _playerPosition, Vector3.up);
                    float angle2 = Vector3.SignedAngle(forward, _currentTarget.Position - _playerPosition, Vector3.up);

                    if (Mathf.Abs(angle1) < Mathf.Abs(angle2))
                    {
                        _targetChanged = true;
                    }
                    else
                    {
                        float dist1 = Vector3.SqrMagnitude(_playerPosition - _proposedTarget.Position);
                        float dist2 = Vector3.SqrMagnitude(_playerPosition - _currentTarget.Position);
                        if (dist1 < dist2) _targetChanged = true;
                    }
                }
            }
            else
            {
                _targetChanged = true;
            }

            if (_targetChanged)
            {
                _currentTarget = _proposedTarget;
                _playerWaypointsManager.UpdatePlayerWaypoint(GetInstanceID(), _proposedTarget.ListIndex);
            }
        }


        /// <summary>
        /// Checks if the waypoint's direction is valid and returns the correct next target.
        /// </summary>
        private bool CheckOrientation(TrafficWaypoint waypoint, out TrafficWaypoint proposedTarget)
        {
            proposedTarget = null;

            if (waypoint.Neighbors.Length < 1)
            {
                return false;
            }

            TrafficWaypoint neighbor = _trafficWaypointsData.AllTrafficWaypoints[waypoint.Neighbors[0]];
            float angle = Vector3.SignedAngle(_myTransform.forward, neighbor.Position - waypoint.Position, Vector3.up);

            if (Mathf.Abs(angle) < 90)
            {
                proposedTarget = neighbor;
                return true;
            }

            return false;
        }


#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                if (_initialized)
                {
                    if (TrafficManager.Instance.DebugManager.IsDebugWaypointsEnabled())
                    {
                        if (_currentTarget != null)
                        {
                            Gizmos.color = Color.green;
                            Vector3 position = _currentTarget.Position;
                            Gizmos.DrawSphere(position, 1);
                        }
                    }
                }
            }
        }
#endif
#endif

        public float GetCurrentSpeedMS()
        {
            if(!_initialized)
            {
                return 0f;
            }
#if UNITY_6000_0_OR_NEWER
            return _rb.linearVelocity.magnitude;
#else
            return _rb.velocity.magnitude;
#endif
        }


        public Vector3 GetHeading()
        {
            return _myTransform.forward;
        }

        public bool AlreadyCollidingWith(Collider[] allColliders)
        {
            return false;
        }
    }
}