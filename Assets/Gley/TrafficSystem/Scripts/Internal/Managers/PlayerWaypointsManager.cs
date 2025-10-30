using Gley.UrbanSystem.Internal;
using System.Collections.Generic;

namespace Gley.TrafficSystem.Internal
{
    public class PlayerWaypointsManager : IDestroyable
    {
        private readonly Dictionary<int, List<int>> _playerTarget; // PlayerID -> WaypointIndex
        private readonly HashSet<int> _targetWaypoints; // Stores waypoint indices that are targets

        public PlayerWaypointsManager()
        {
            Assign();
            _playerTarget = new Dictionary<int, List<int>>();
            _targetWaypoints = new HashSet<int>();
        }


        public void Assign()
        {
            DestroyableManager.Instance.Register(this);
        }


        public void RegisterPlayer(int id, int waypointIndex)
        {
            if (!_playerTarget.ContainsKey(id))
            {
                _playerTarget[id] = new List<int> { waypointIndex };
                _targetWaypoints.Add(waypointIndex);
            }
        }


        public void UpdatePlayerWaypoint(int id, int newWaypointIndex)
        {
            UpdatePlayerWaypoint(id, new List<int> { newWaypointIndex });
        }

        public void UpdatePlayerWaypoint(int id, List<int> newWaypointIndex)
        {
            if (_playerTarget.TryGetValue(id, out List<int> oldWaypointIndex))
            {
                foreach (var oldWaypoint in oldWaypointIndex)
                {
                    _targetWaypoints.Remove(oldWaypoint); // Remove old waypoints
                }
            }

            _playerTarget[id] = new List<int>(newWaypointIndex);
            foreach (var newWaypoint in newWaypointIndex)
            {
                _targetWaypoints.Add(newWaypoint); // Add new waypoints
            }
        }

        public bool IsThisWaypointIndexATarget(int waypointIndex)
        {
            return _targetWaypoints.Contains(waypointIndex); // O(1) lookup time
        }


        public List<int> GetPlayerTarget(int id)
        {
            if (_playerTarget.TryGetValue(id, out List<int> waypoints))
            {
                return waypoints;
            }
            return new List<int>(); // Return an empty list if no target is found
        }

        public void OnDestroy()
        {
            _playerTarget.Clear();
            _targetWaypoints.Clear();
        }
    }
}