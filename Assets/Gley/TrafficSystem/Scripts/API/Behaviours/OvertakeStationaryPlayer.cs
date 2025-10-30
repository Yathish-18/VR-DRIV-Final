using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Gley.TrafficSystem
{
    // Handles vehicle behavior to overtake a stationary player vehicle
    public class OvertakeStationaryPlayer : VehicleBehaviour
    {
#if GLEY_TRAFFIC_SYSTEM
        // Distance thresholds for movement decisions
        float _forwardDistance = 1.5f;
        float _backwardDistance = 2.5f;

        // Desired speed during maneuver
        float _movementSpeed = 4;

        // State tracking and path setup
        List<int> _originalWaypointIndexes;
        TrafficWaypoint _newWaypoint;
        State _currentState;
        float _oldDistance;
        float _maxSteerReached;
        int _direction;
        int _overtakeDirection;
        bool _waypointSet;

        // FSM states for overtaking maneuver
        enum State
        {
            Stopped,                 // Initial stop
            TurningTowardOvertake,  // Steering toward the other lane
            MovingForward,          // Moving past the obstacle
            TurningAway,            // Steering in reverse direction
            Reversing,              // Backing up
            WaitingForClear,        // Check if the other lane is clear before proceeding
            Exiting                 // Rejoining original behavior
        }

        // Called when the behavior becomes active
        protected override void OnBecomeActive()
        {
            base.OnBecomeActive();
            _oldDistance = Mathf.Infinity;
            _waypointSet = false;
            _maxSteerReached = VehicleComponent.MaxSteer - VehicleComponent.SteerStep;
            // Optional: play horn sound here
        }

        // Main update loop for the behavior
        public override BehaviourResult Execute(MovementInfo knownWaypointsList, float requiredBrakePower, bool stopTargetReached, float3 stopPosition, int currentGear)
        {
            // Setup waypoint for overtake if not already done
            if (!_waypointSet)
            {
                SetNewWaypoint(knownWaypointsList);
            }

            BehaviourResult result = new BehaviourResult();

            float distance = Vector3.Distance(knownWaypointsList.ClosestObstaclePoint, VehicleComponent.FrontPosition.position);
            float steerAngle = VehicleComponent.SteerAngle;

            switch (_currentState)
            {
                case State.Stopped:
                    // Apply full brake
                    requiredBrakePower = 5;
                    result.TargetGear = 0;
                    if (distance != Mathf.Infinity)
                    {
                        ChangeState(State.TurningTowardOvertake);
                    }
                    break;

                case State.TurningTowardOvertake:
                    // Steer in the direction of the overtake
                    requiredBrakePower = 1;
                    Steer(ref result, -_direction);
                    if (-steerAngle * _direction >= _maxSteerReached)
                    {
                        ChangeState(State.MovingForward);
                    }
                    break;

                case State.MovingForward:
                    // Move forward while maintaining overtake steer
                    result.TargetGear = 1;
                    requiredBrakePower = 0;
                    Steer(ref result, -_direction);

                    if (distance > _forwardDistance)
                    {
                        if (distance == Mathf.Infinity)
                        {
                            ChangeState(State.WaitingForClear);
                        }
                        else
                        {
                            if (distance - _oldDistance > 0.01f)
                            {
                                ChangeState(State.TurningAway);
                            }
                        }
                    }
                    else
                    {
                        // Not enough space: start turning away
                        Steer(ref result, _direction);
                        ChangeState(State.TurningAway);
                    }
                    break;

                case State.TurningAway:
                    // Steer in the opposite direction to reverse
                    requiredBrakePower = 1;
                    Steer(ref result, _direction);
                    if (steerAngle * _direction >= _maxSteerReached)
                    {
                        ChangeState(State.Reversing);
                    }
                    break;

                case State.Reversing:
                    // Reverse with opposite steering
                    result.TargetGear = -1;
                    requiredBrakePower = 0;
                    Steer(ref result, _direction);
                    if (distance > _backwardDistance)
                    {
                        ChangeState(State.TurningTowardOvertake);
                    }
                    break;

                case State.WaitingForClear:
                    // Wait and check if the new path is clear
                    Steer(ref result, -_direction);
                    requiredBrakePower = 1;
                    Go();
                    break;

                case State.Exiting:
                    // Let the FSM exit when the waypoint switch is complete
                    Stop();
                    break;
            }

            _oldDistance = distance;

            // Set the target speed based on braking state
            float targetSpeed = requiredBrakePower == 0 ? _movementSpeed : 0;

            // Apply movement logic
            PerformForwardMovement(ref result, targetSpeed, targetSpeed, knownWaypointsList.ClosestObstaclePoint, requiredBrakePower, 0.5f, VehicleComponent.distanceToStop);
            return result;
        }

        // Configuration methods for tuning maneuver behavior
        public void SetForwardDistance(float distance) => _forwardDistance = distance;
        public void SetBackwardDistance(float distance) => _backwardDistance = distance;
        public void SetMovementSpeed(float speed) => _movementSpeed = speed;

        // Determine overtake path based on surrounding waypoints
        void SetNewWaypoint(MovementInfo knownWaypointsList)
        {
            ChangeState(State.Stopped);

            var waypoint = TrafficWaypointsData.GetWaypointFromIndex(knownWaypointsList.GetWaypointIndex(0));

            // No other lanes available
            if (waypoint.OtherLanes == null || waypoint.OtherLanes.Length == 0)
            {
                SwitchToFollowPlayer();
                return;
            }

            int index = GetOvertakeIndex(waypoint.OtherLanes, VehicleComponent.VehicleType);
            if (index == TrafficSystemConstants.INVALID_WAYPOINT_INDEX)
            {
                SwitchToFollowPlayer();
                return;
            }

            // Already processed this path
            if (_originalWaypointIndexes != null && _originalWaypointIndexes.Contains(index) && _newWaypoint != null)
            {
                _waypointSet = true;
                _backwardDistance *= 1.05f;
                ChangeState(State.TurningAway);
                return;
            }
            // Traverse backwards along the overtake lane to compute angle
            _newWaypoint = API.GetWaypointFromIndex(index);
            while (_newWaypoint.HasPrevs)
            {
                var proposedWaypoint = API.GetWaypointFromIndex(_newWaypoint.Prevs[0]);
                float angle = Vector3.SignedAngle(VehicleComponent.GetForwardVector(), VehicleComponent.GetFrontAxlePosition() - proposedWaypoint.Position, Vector3.up);
                if (Mathf.Abs(angle) > 100)
                {
                    _overtakeDirection = (int)Mathf.Sign(angle);
                    _direction = _overtakeDirection;
                    _newWaypoint = proposedWaypoint;
                }
                else
                {
                    break;
                }
            }
            Blink(_direction);
            _originalWaypointIndexes = new List<int>(knownWaypointsList.WaypointIndexes);
            _waypointSet = true;
        }

        // If no overtake path is found, fallback to default behavior
        void SwitchToFollowPlayer()
        {
            API.StartVehicleBehaviour<FollowPlayer>(VehicleIndex);
            Stop();
        }

        // Trigger switch to new waypoint if lane is clear
        void Go()
        {

            if (API.AllPreviousWaypointsAreFree(_newWaypoint.ListIndex, VehicleIndex))
            {
                ChangeState(State.Exiting);
                API.AddWaypointAndClear(_newWaypoint.ListIndex, VehicleIndex);
                Stop();
            }
        }

        // Change state if new state is different from current
        void ChangeState(State newState)
        {
            if (_currentState != newState)
                _currentState = newState;
        }

        // Turn on blinkers based on angle to new waypoint
        void Blink(int direction)
        {
            if (direction ==1)
                VehicleComponent.SetBlinker(UrbanSystem.Internal.BlinkType.Left);
            else 
                VehicleComponent.SetBlinker(UrbanSystem.Internal.BlinkType.Right);
        }

        // Pick best lane with highest speed allowed and allowed for this vehicle type
        int GetOvertakeIndex(int[] otherLanes, VehicleTypes vehicleType)
        {
            float maxSpeed = 0;
            int result = TrafficSystemConstants.INVALID_WAYPOINT_INDEX;

            for (int i = 0; i < otherLanes.Length; i++)
            {
                var waypoint = TrafficWaypointsData.GetWaypointFromIndex(otherLanes[i]);

                bool isAllowed = false;
                foreach (var allowedType in waypoint.AllowedVehicles)
                {
                    if (allowedType == vehicleType)
                    {
                        isAllowed = true;
                        break;
                    }
                }

                if (!isAllowed)
                    continue;

                if (waypoint.MaxSpeed > maxSpeed)
                {
                    maxSpeed = waypoint.MaxSpeed;
                    result = otherLanes[i];
                }
            }
            return result;
        }
#endif
        public override void OnDestroy()
        {
            // No cleanup required
        }

    }
}
