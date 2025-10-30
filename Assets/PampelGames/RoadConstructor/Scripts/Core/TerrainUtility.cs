// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using PampelGames.Shared.Construction;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    internal static class TerrainUtility 
    {
        
        public static void CreateTerrainUpdateSplinesRoad(RoadObject road,
            out List<Spline> splines, out List<float> widths)
        {
            splines = new List<Spline>();
            widths = new List<float>();
            AddSpline(splines, widths, road.splineContainer.Spline, road.roadDescr.width);
        }
        
        public static void CreateTerrainUpdateSplinesIntersection(IntersectionObject intersection,
            out List<Spline> splines, out List<float> widths)
        {
            splines = new List<Spline>();
            widths = new List<float>();

            TryAddEndObjectSpline(splines, widths, intersection);

            void TryAddEndObjectSpline(List<Spline> intersectionSplines, List<float> intersectionWidths, IntersectionObject intersectionObject)
            {
                if (!intersectionObject.IsEndObject()) return;
                var settings = intersectionObject.roadDescr.settings;
                if (settings.terrains.Count == 0) return;
                var pixelSize = TerrainUpdate.GetMinHeightmapPixelSize(settings.terrains[0].terrainData);
                var increasedSpline = ConstructionSplineUtility.CreateIncreasedSpline(intersectionObject.splineContainer.Spline, false, pixelSize * 3f);
                AddSpline(intersectionSplines, intersectionWidths, increasedSpline, intersectionObject.roadDescr.width);
            }
            
            var _intersectionSplines = intersection.splineContainer.Splines.ToList();
            for (var j = 0; j < _intersectionSplines.Count; j++)
            {
                var _spline = new Spline(_intersectionSplines[j]);
                var width = intersection.roadDescr.width * 1.1f; // Adding some extra space
                AddSpline(splines, widths, _spline, width);
            }
        }
        
        public static void CreateTerrainUpdateSplinesRamp(RampObject ramp,
            out List<Spline> splines, out List<float> widths)
        {
            CreateTerrainUpdateSplinesIntersection(ramp, out splines, out widths);
            
            // Set lowest knot to all
            var minHeight = float.MaxValue; 
            for (int i = 0; i < splines.Count; i++)
            for (int j = 0; j < splines[i].Count; j++)
                minHeight = math.min(minHeight, splines[i][j].Position.y);

            for (int i = 1; i < splines.Count; i++) // Not first (main road) spline
            {
                var knots = splines[i].Knots.ToList();
                for (int j = 0; j < knots.Count(); j++)
                {
                    var knot = knots[j];
                    knot.Position.y = minHeight;
                    splines[i].SetKnot(j, knot);
                }
            }
        }
        
        
        /********************************************************************************************************************************/
        
        private static void AddSpline(List<Spline> splines, List<float> widths, Spline newSpline, float newWidth)
        {
            splines.Add(newSpline);
            widths.Add(newWidth);
        }
        
    }
}