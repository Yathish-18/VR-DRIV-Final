// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public class ConstructionData
    {
        /// <summary>
        ///     World position of the first road position.
        /// </summary>
        public Vector3 position01;

        /// <summary>
        ///     Direction of the first road position, looking towards position02.
        /// </summary>
        public Vector3 tangent01;

        /// <summary>
        ///     Angle in degrees to a connected road/intersection.
        ///     Set to -1 if position01 is not connected.
        /// </summary>
        public float connectionAngle01;

        /// <summary>
        ///     Height difference to the ground of the first road position.
        ///     For this value to be valid, make sure you have <see cref="ComponentSettings.groundLayers" /> set.
        /// </summary>
        public float height01;

        /// <summary>
        ///     World position of the second road position.
        /// </summary>
        public Vector3 position02;

        /// <summary>
        ///     Direction of the second road position, looking towards position01.
        /// </summary>
        public Vector3 tangent02;

        /// <summary>
        ///     Angle in degrees to a connected road/intersection.
        ///     Set to 0 if position02 is not connected.
        /// </summary>
        public float connectionAngle02;

        /// <summary>
        ///     Height difference to the ground of the second road position.
        ///     For this value to be valid, make sure you have <see cref="ComponentSettings.groundLayers" /> set.
        /// </summary>
        public float height02;

        /// <summary>
        ///     Length of the road;
        /// </summary>
        public float length;

        /// <summary>
        ///     Angle of the road;
        /// </summary>
        public float angle;

        /// <summary>
        ///     The angle (in degrees) representing the deviation from position01/tangent01 to position02/tangent02.
        ///     A value of 0 indicates a straight path, while higher values up to 180 indicate increasing curvature.
        /// </summary>
        public float curvature;

        /// <summary>
        ///     The slope of the road in degrees.
        ///     Positive values indicate an upward slope, negative values indicate a downward slope.
        /// </summary>
        public float slope;

        /// <summary>
        ///     Whether this road is above the specified elevation height.
        /// </summary>
        public bool elevated;

        /// <summary>
        ///     Whether this road has been generated as a parallel road.
        /// </summary>
        public bool parallelRoad;

        public ConstructionData(Vector3 position01, Vector3 tangent01, float connectionAngle01, float height01,
            Vector3 position02, Vector3 tangent02, float connectionAngle02, float height02,
            float length, float angle, float curvature, float slope, bool elevated, bool parallelRoad)
        {
            this.position01 = position01;
            this.position02 = position02;
            this.connectionAngle01 = connectionAngle01;
            this.height01 = height01;
            this.tangent01 = tangent01;
            this.tangent02 = tangent02;
            this.connectionAngle02 = connectionAngle02;
            this.height02 = height02;
            this.length = length;
            this.angle = angle;
            this.curvature = curvature;
            this.slope = slope;
            this.elevated = elevated;
            this.parallelRoad = parallelRoad;
        }
        
        public ConstructionData() 
        {
 
        }
    }
    
}