// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using Unity.Mathematics;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    /// <summary>
    ///     Creates splines that resemble circles.
    ///     From here: https://spencermortensen.com/articles/bezier-circle/
    /// </summary>
    public static class SplineCircle
    {
        private const float a = 1.00005519f;
        private const float b = 0.55342686f;
        private const float c = 0.99873585f;

        /// <summary>
        ///     A quarter of a circle.
        /// </summary>
        public static Spline CreateQuarterCircleSpline(float radius, float3 position, quaternion rotation, bool invert)
        {
            var resizeMultiplier = radius / a;

            var center = float3.zero;
            var P0 = RotatePointAroundPivot(new float3(0, 0, a * resizeMultiplier), center) + position;
            var P1 = RotatePointAroundPivot(new float3(b * resizeMultiplier, 0, c * resizeMultiplier), center) + position;
            var P2 = RotatePointAroundPivot(new float3(c * resizeMultiplier, 0, b * resizeMultiplier), center) + position;
            var P3 = RotatePointAroundPivot(new float3(a * resizeMultiplier, 0, 0), center) + position;

            var curveKnot01 = new BezierKnot();
            var curveKnot02 = new BezierKnot();

            curveKnot01.Rotation = quaternion.identity;
            curveKnot02.Rotation = quaternion.identity;

            if (!invert)
            {
                curveKnot01.Position = P0;
                curveKnot02.Position = P3;

                curveKnot01.TangentOut = P1 - P0;
                curveKnot01.TangentIn = P0 - P1;
                curveKnot02.TangentIn = P2 - P3;
                curveKnot02.TangentOut = P3 - P2;
            }
            else
            {
                curveKnot01.Position = P3;
                curveKnot02.Position = P0;

                curveKnot01.TangentOut = P2 - P3;
                curveKnot01.TangentIn = P3 - P2;
                curveKnot02.TangentIn = P1 - P0;
                curveKnot02.TangentOut = P0 - P1;
            }

            var quadrantSpline = new Spline
            {
                {curveKnot01, TangentMode.Broken},
                {curveKnot02, TangentMode.Broken}
            };

            return quadrantSpline;

            float3 RotatePointAroundPivot(float3 point, float3 pivot)
            {
                return math.mul(rotation, point - pivot) + pivot;
            }
        }

        public static Spline CreateCircleSpline(float radius, float3 position, quaternion rotation, bool invert)
        {
            var resizeMultiplier = radius / a;

            var center = float3.zero;
            var P0 = RotatePointAroundPivot(new float3(0, 0, a * resizeMultiplier), center) + position;
            var P1 = RotatePointAroundPivot(new float3(b * resizeMultiplier, 0, c * resizeMultiplier), center) + position;
            var P2 = RotatePointAroundPivot(new float3(c * resizeMultiplier, 0, b * resizeMultiplier), center) + position;
            var P3 = RotatePointAroundPivot(new float3(a * resizeMultiplier, 0, 0), center) + position;
            var P4 = RotatePointAroundPivot(new float3(0, 0, -a * resizeMultiplier), center) + position;
            var P5 = RotatePointAroundPivot(new float3(-b * resizeMultiplier, 0, -c * resizeMultiplier), center) + position;
            var P6 = RotatePointAroundPivot(new float3(-c * resizeMultiplier, 0, -b * resizeMultiplier), center) + position;
            var P7 = RotatePointAroundPivot(new float3(-a * resizeMultiplier, 0, 0), center) + position;
            var P8 = RotatePointAroundPivot(new float3(0, 0, a * resizeMultiplier), center) + position;
            var P9 = RotatePointAroundPivot(new float3(b * resizeMultiplier, 0, c * resizeMultiplier), center) + position;

            var curveKnot01 = new BezierKnot();
            var curveKnot02 = new BezierKnot();
            var curveKnot03 = new BezierKnot();
            var curveKnot04 = new BezierKnot();

            curveKnot01.Rotation = quaternion.identity;
            curveKnot02.Rotation = quaternion.identity;
            curveKnot03.Rotation = quaternion.identity;
            curveKnot04.Rotation = quaternion.identity;

            if (!invert)
            {
                curveKnot01.Position = P0;
                curveKnot02.Position = P3;
                curveKnot03.Position = P4;
                curveKnot04.Position = P7;

                curveKnot01.TangentIn = P0 - P1;
                curveKnot01.TangentOut = P1 - P0;
                curveKnot02.TangentIn = P2 - P3;
                curveKnot02.TangentOut = P3 - P2;
                curveKnot03.TangentIn = P4 - P5;
                curveKnot03.TangentOut = P5 - P4;
                curveKnot04.TangentIn = P6 - P7;
                curveKnot04.TangentOut = P7 - P6;
            }
            else
            {
                curveKnot01.Position = P8;
                curveKnot02.Position = P7;
                curveKnot03.Position = P4;
                curveKnot04.Position = P3;

                curveKnot01.TangentIn = P9 - P8;
                curveKnot01.TangentOut = P8 - P9;
                curveKnot02.TangentOut = P6 - P7;
                curveKnot02.TangentIn = P7 - P6;
                curveKnot03.TangentOut = P4 - P5;
                curveKnot03.TangentIn = P5 - P4;
                curveKnot04.TangentOut = P2 - P3;
                curveKnot04.TangentIn = P3 - P2;
            }

            var quadrantSpline = new Spline
            {
                {curveKnot01, TangentMode.Broken},
                {curveKnot02, TangentMode.Broken},
                {curveKnot03, TangentMode.Broken},
                {curveKnot04, TangentMode.Broken}
            };

            quadrantSpline.Closed = true;
            return quadrantSpline;

            float3 RotatePointAroundPivot(float3 point, float3 pivot)
            {
                return math.mul(rotation, point - pivot) + pivot;
            }
        }
    }
}