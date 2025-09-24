// // ╔════════════════════════════════════════════════════════════════╗
// // ║    Copyright © 2025 NWH Coding d.o.o.  All rights reserved.    ║
// // ║    Licensed under Unity Asset Store Terms of Service:          ║
// // ║        https://unity.com/legal/as-terms                        ║
// // ║    Use permitted only in compliance with the License.          ║
// // ║    Distributed "AS IS", without warranty of any kind.          ║
// // ╚════════════════════════════════════════════════════════════════╝

#region

using System;
using NWH.Common.Vehicles;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

#endregion

namespace NWH.VehiclePhysics2.Modules.ABS
{
    /// <summary>
    ///     Anti-lock Braking System module.
    ///     Prevents wheels from locking up by reducing brake 
    ///     when slip reaches too high value.
    /// </summary>
    [Serializable]
    public partial class ABSModule : VehicleComponent
    {
        /// <summary>
        ///     Called each frame while ABS is a active.
        /// </summary>
        [Tooltip("    Called each frame while ABS is a active.")]
        public UnityEvent absActivated = new();

        /// <summary>
        ///     Is ABS currently active?
        /// </summary>
        [Tooltip("    Is ABS currently active?")]
        public bool active;

        /// <summary>
        ///     ABS will not work below this speed.
        /// </summary>
        [Tooltip("    ABS will not work below this speed.")]
        public float lowerSpeedThreshold = 1f;

        /// <summary>
        ///     Range in which brake torque will be reduced. Larger value means less sensitive ABS.
        /// </summary>
        [Range(0.001f, 1)]
        [Tooltip("Range in which brake torque will be reduced. Larger value means less sensitive ABS.")]
        public float slipRange = 0.2f;

        /// <summary>
        ///     Longitudinal slip required for ABS to trigger. Larger value means less sensitive ABS.
        /// </summary>
        [Range(0, 1)]
        [Tooltip(
            "Longitudinal slip required for ABS to trigger.")]
        public float slipThreshold = 0.16f;


        public override bool VC_Enable(bool calledByParent)
        {
            if (!base.VC_Enable(calledByParent))
            {
                return false;
            }
            
            slipRange = slipRange < Vehicle.SMALL_NUMBER ? Vehicle.SMALL_NUMBER : slipRange;
            vehicleController.brakes.brakeTorqueModifiers.Add(BrakeTorqueModifier);
            return true;
        }


        public override bool VC_Disable(bool calledByParent)
        {
            if (!base.VC_Disable(calledByParent))
            {
                return false;
            }

            active = false;
            vehicleController.brakes.brakeTorqueModifiers.Remove(BrakeTorqueModifier);
            return true;
        }


        public float BrakeTorqueModifier()
        {
            active = false;
            
            // Module inactive
            if (!IsActive)
            {
                return 1.0f;
            }

            // Too low speed
            if (vehicleController.Speed < lowerSpeedThreshold)
            {
                return 1.0f;
            }

            // Brakes not active
            if (!vehicleController.brakes.IsActive)
            {
                return 1.0f;
            }

            // Rev limiter active, disable ABS
            if (vehicleController.powertrain.engine.revLimiterActive)
            {
                return 1.0f;
            }

            // No ABS when handbrake in use
            if (vehicleController.input.Handbrake > Vehicle.INPUT_DEADZONE)
            {
                return 1.0f;
            }

            for (int index = 0; index < vehicleController.powertrain.wheelCount; index++)
            {
                WheelUAPI wheelController = vehicleController.powertrain.wheels[index].wheelUAPI;
                if (!wheelController.IsGrounded)
                {
                    continue;
                }

                if (wheelController.LongitudinalSlip < slipThreshold)
                {
                    continue;
                }

                active = true;
                absActivated.Invoke();
                return Mathf.Clamp01(wheelController.LongitudinalSlip - slipThreshold) / slipRange;
            }

            return 1f;
        }
    }
}

#if UNITY_EDITOR

namespace NWH.VehiclePhysics2.Modules.ABS
{
    [CustomPropertyDrawer(typeof(ABSModule))]
    public partial class ABSModuleDrawer : ComponentNUIPropertyDrawer
    {
        public override bool OnNUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (!base.OnNUI(position, property, label))
            {
                return false;
            }

            drawer.Field("slipThreshold");
            drawer.Field("slipRange");
            drawer.Field("lowerSpeedThreshold", true, "m/s");
            drawer.Field("active",              false);
            drawer.Field("active");

            drawer.EndProperty();
            return true;
        }
    }
}

#endif