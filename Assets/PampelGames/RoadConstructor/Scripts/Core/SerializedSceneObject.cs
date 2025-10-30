// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace PampelGames.RoadConstructor
{
    [Serializable]
    public class SerializedSceneObject
    {
        public string roadName;
        public bool elevated;

        public SerializedSceneObject(string roadName, bool elevated)
        {
            this.roadName = roadName;
            this.elevated = elevated;
        }

        public virtual void CreateObjectFromSerializedData(ConstructionObjects constructionObjects, RoadDescr roadDescr)
        {
            
        }
    }

    [Serializable]
    public class SerializedRoad : SerializedSceneObject
    {
        public Spline spline;
        public bool rampRoad;
        
        public string splitOriginalID;
        public Spline splitOriginalSpline;

        public SerializedRoad(string roadName, bool elevated, bool rampRoad, Spline spline, string splitOriginalID, Spline splitOriginalSpline) : base(roadName, elevated)
        {
            this.spline = spline;
            this.rampRoad = rampRoad;
            this.splitOriginalID = splitOriginalID;
            this.splitOriginalSpline = splitOriginalSpline;
        }

        public override void CreateObjectFromSerializedData(ConstructionObjects constructionObjects, RoadDescr roadDescr)
        {
            var road = RoadCreation.CreateRoad(roadDescr, spline, elevated, rampRoad, 1f);
            road.splitOriginalID = splitOriginalID;
            road.splitOriginalSpline = splitOriginalSpline;
            constructionObjects.newRoads.Add(road);
        }
    }

    [Serializable]
    public class SerializedIntersection : SerializedSceneObject
    {
        public Vector3 centerPosition;

        public List<string> connectionRoadNames;
        public List<BezierKnot> connectionKnots;
        public List<int> connectionNearestIndexes;

        public SerializedIntersection(string roadName, bool elevated, Vector3 centerPosition, 
            List<KnotData> createIntersectionMeshDatas) : base(roadName, elevated)
        {
            this.centerPosition = centerPosition;
            connectionRoadNames = new List<string>();
            connectionKnots = new List<BezierKnot>();
            connectionNearestIndexes = new List<int>();
            for (int i = 0; i < createIntersectionMeshDatas.Count; i++)
            {
                connectionRoadNames.Add(createIntersectionMeshDatas[i].roadDescr.road.roadName);
                connectionKnots.Add(createIntersectionMeshDatas[i].nearestKnot);
                connectionNearestIndexes.Add(createIntersectionMeshDatas[i].nearestKnotIndex);
            }
        }

        public override void CreateObjectFromSerializedData(ConstructionObjects constructionObjects, RoadDescr roadDescr)
        {
            var knotDatas = CreateKnotDatas(roadDescr);

            IntersectionCreation.CreateIntersection(roadDescr.settings, constructionObjects, knotDatas,
                centerPosition, elevated, false);
        }
        
        protected List<KnotData> CreateKnotDatas(RoadDescr roadDescr)
        {
            var knotDatas = new List<KnotData>();

            for (int j = 0; j < connectionRoadNames.Count; j++)
            {
                if (!roadDescr.roadConstructor.TryGetRoadDescr(connectionRoadNames[j], out var connectionRoadDescr)) continue;
                knotDatas.Add(new KnotData(connectionRoadDescr, connectionKnots[j], connectionNearestIndexes[j]));
            }

            return knotDatas;
        }
    }
    
    [Serializable]
    public class SerializedRoundabout : SerializedIntersection
    {
        public RoundaboutDesign design;
        public float radius;

        public SerializedRoundabout(string roadName, bool elevated, Vector3 centerPosition, RoundaboutDesign design, float radius,
            List<KnotData> createIntersectionMeshDatas) : base(roadName, elevated, centerPosition, createIntersectionMeshDatas)
        {
            this.design = design;
            this.radius = radius;
        }

        public override void CreateObjectFromSerializedData(ConstructionObjects constructionObjects, RoadDescr roadDescr)
        {
            var knotDatas = CreateKnotDatas(roadDescr);

            var roundabout = RoundaboutCreation.CreateRoundabout(roadDescr, knotDatas, centerPosition, design, radius);
            constructionObjects.newIntersections.Add(roundabout);
        }
    }
    
    [Serializable]
    public class SerializedRamp : SerializedSceneObject
    {
        public Vector3 centerPosition;
        public Spline splineRoad;
        
        public string rampRoadName;
        public BezierKnot rampKnot;
        public int rampNearestIndex;

        public SerializedRamp(string roadName, Vector3 centerPosition, bool elevated, Spline splineRoad, KnotData rampKnotData)
            : base(roadName, elevated)
        {
            this.centerPosition = centerPosition;
            this.splineRoad = splineRoad;
            rampRoadName = rampKnotData.roadDescr.road.roadName;
            rampKnot = rampKnotData.nearestKnot;
            rampNearestIndex = rampKnotData.nearestKnotIndex;
        }

        public override void CreateObjectFromSerializedData(ConstructionObjects constructionObjects, RoadDescr roadDescr)
        {
            if(!roadDescr.roadConstructor.TryGetRoadDescr(rampRoadName, out var rampRoadDescr)) return;
            var rampKnotData = new KnotData(rampRoadDescr, rampKnot, rampNearestIndex);
            RampCreation.CreateRamp(constructionObjects, roadDescr, splineRoad, rampKnotData, centerPosition, elevated);
        }
    }
    
    
}