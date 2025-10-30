// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.Shared.Construction
{
    public static class ConstructionOverlapUtility 
    {
        /// <summary>
        /// Detects overlaps between a given track spline and other overlapping constraints such as track intersections or other tracks,
        /// based on specified parameters like track width, spacing, and overlap thresholds.
        /// </summary>
        /// <param name="trackSpline">The main spline representing the track to check for overlaps.</param>
        /// <param name="trackWidth">The width of the track used for calculating overlapping boundaries.</param>
        /// <param name="trackSpacing">The spacing between track points for spline evaluation.</param>
        /// <param name="minOverlapHeight">The minimum height to consider when detecting overlaps.</param>
        /// <param name="minOverlapDistance">The minimum distance to consider for overlaps between the tracks.</param>
        /// <param name="overlapIntersectionBounds">An array of bounds representing intersections for overlap detection.</param>
        /// <param name="overlapTrackSplines">List of track splines to check for overlaps against the main track spline.</param>
        /// <param name="overlapTrackWidths">List of widths corresponding to the overlap track splines.</param>
        /// <param name="splinePositions">The evaluated positions along the spline (output parameter).</param>
        /// <param name="splineTangents">The tangents of the evaluated spline positions (output parameter).</param>
        /// <returns>A list of construction failures.</returns>
        public static List<ConstructionFail> DetectTrackOverlap(Spline trackSpline, float trackWidth, float trackSpacing,
            float minOverlapHeight, float minOverlapDistance,
            Bounds[] overlapIntersectionBounds, List<Spline> overlapTrackSplines, List<float> overlapTrackWidths,
            out float3[] splinePositions, out float3[] splineTangents)
        {
            var constructionFails = new List<ConstructionFail>();

            SplineWorldUtility.SplineEvaluationLeftRight(trackSpline, trackSpacing, trackWidth * 0.5f,
                out var splinePositionsLeft, out var splinePositionsRight, out splineTangents);

            splinePositions = splinePositionsLeft.Concat(splinePositionsRight).ToArray();

            /********************************************************************************************************************************/
            // Intersection

            var insidePositionsIntersection = SplineWorldUtility.CheckPositionsInsideBounds(splinePositions, overlapIntersectionBounds);
            if (insidePositionsIntersection.Length > 0) constructionFails.Add(new ConstructionFail(FailCause.OverlapIntersection));

            /********************************************************************************************************************************/
            // Track

            for (var i = 0; i < overlapTrackSplines.Count; i++)
            {
                var overlapTrackSpline = overlapTrackSplines[i];
                var overlapTrackWidth = overlapTrackWidths[i];

                // Left + right splitting necessary to skip indexes correctly
                var insidePositionsTrackLeft = SplineWorldUtility.CheckPositionsInsideSpline(splinePositionsLeft, overlapTrackSpline,
                    overlapTrackWidth * 0.5f, minOverlapDistance, minOverlapHeight);

                if (insidePositionsTrackLeft.Length > 0)
                {
                    constructionFails.Add(new ConstructionFail(FailCause.OverlapTrack));
                }
                else
                {
                    var insidePositionsTrackRight = SplineWorldUtility.CheckPositionsInsideSpline(splinePositionsRight, overlapTrackSpline,
                        overlapTrackWidth * 0.5f, minOverlapDistance, minOverlapHeight);
                    if (insidePositionsTrackRight.Length > 0) constructionFails.Add(new ConstructionFail(FailCause.OverlapTrack));
                }
            }
            
            return constructionFails;
        }    
    }
}