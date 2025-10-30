#if GLEY_TRAFFIC_SYSTEM
using Unity.Mathematics;
using UnityEngine;
#endif

namespace Gley.TrafficSystem
{
    // Slow down if the next waypoint has a big angle
    public class CurveSlowDown : VehicleBehaviour
    {
#if GLEY_TRAFFIC_SYSTEM
        private float _safeCurveSpeed = 50f; // Speed below which we don't apply corrections
#endif
        protected override void OnBecomeActive()
        {
            base.OnBecomeActive();
            MovementInfo.OnKnownListUpdated += KnownListUpdatedHandler;
        }


        protected override void OnBecameInactive()
        {
            base.OnBecameInactive();
            MovementInfo.OnKnownListUpdated -= KnownListUpdatedHandler;
            VehicleComponent.MovementInfo.SetMaxSpeedCorrectionPercent(1);
        }

#if GLEY_TRAFFIC_SYSTEM
        public override BehaviourResult Execute(MovementInfo knownWaypointsList, float requiredBrakePower, bool stopTargetReached, float3 stopPosition, int currentGear)
        {
            return new BehaviourResult();
        }
#endif

        private void KnownListUpdatedHandler(int vehicleIndex)
        {
#if GLEY_TRAFFIC_SYSTEM
            if (vehicleIndex != VehicleIndex)
                return;

            // If the vehicle is moving very slowly, no need to correct speed
            float currentSpeed = VehicleComponent.GetCurrentSpeedMS().ToKMH();
            if (currentSpeed <= _safeCurveSpeed)
            {
                VehicleComponent.MovementInfo.SetMaxSpeedCorrectionPercent(1f);
                return;
            }

            bool correctSpeed = false;
            // Calculate cumulative angle of the next segments
            float cumulativeAngle = Vector3.Angle(VehicleComponent.GetForwardVector(), VehicleComponent.MovementInfo.GetFirstPosition() - VehicleComponent.FrontPosition.position);
            if (cumulativeAngle < 5)
            {
                cumulativeAngle = 0;
            }

            for (int i = 0; i < VehicleComponent.MovementInfo.GetAngleLength(); i++)
            {
                float angle = math.abs(VehicleComponent.MovementInfo.GetAngle(i));
                if (angle > 1f)
                {
                    cumulativeAngle += angle;
                    correctSpeed = true;
                }
            }

            if (correctSpeed == false)
            {
                VehicleComponent.MovementInfo.SetMaxSpeedCorrectionPercent(1f);
                return;
            }

            cumulativeAngle = Mathf.Clamp(cumulativeAngle, 0, 90); // Cap to prevent overreaction
            float speedRatio = currentSpeed / VehicleComponent.maxPossibleSpeed;

            // Reduce max speed more aggressively at higher speed and higher curvature
            float correction = 1f - (cumulativeAngle / 90f) * Mathf.Clamp01(speedRatio * 1.2f);
            correction = Mathf.Clamp(correction, 0.4f, 1f); // Never drop below 40% to avoid complete stop

            VehicleComponent.MovementInfo.SetMaxSpeedCorrectionPercent(correction);
#endif
        }


        public override void OnDestroy()
        {
            MovementInfo.OnKnownListUpdated -= KnownListUpdatedHandler;
        }
    }
}