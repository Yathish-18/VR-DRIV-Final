// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Construction;

namespace PampelGames.RoadConstructor
{
    public class ConstructionObjects
    {
        public readonly List<IntersectionObject> newIntersections = new();
        public readonly List<IntersectionObject> newReplacedIntersections = new();
        public readonly List<IntersectionObject> removableIntersections = new();
        public readonly List<RoadObject> newRoads = new();
        public readonly List<RoadObject> newReplacedRoads = new();
        public readonly List<RoadObject> removableRoads = new();
        public readonly List<RoadObject> updatableRoads = new(); // Updates connections and creates end objects (if applicable)

        public List<RoadObject> CombinedNewRoads
        {
            get
            {
                var combinedList = new List<RoadObject>(newRoads);
                combinedList.AddRange(newReplacedRoads);
                return combinedList;
            }
        }

        public List<IntersectionObject> CombinedNewIntersections
        {
            get
            {
                var combinedList = new List<IntersectionObject>(newIntersections);
                combinedList.AddRange(newReplacedIntersections);
                return combinedList;
            }
        }
        
        public List<SceneObject> CombinedNewObjects
        {
            get
            {
                var combinedList = new List<SceneObject>(newRoads);
                combinedList.AddRange(newReplacedRoads);
                combinedList.AddRange(newIntersections);
                combinedList.AddRange(newReplacedIntersections);
                return combinedList;
            }
        }
        
        public List<SceneObject> CombinedRemovableObjects
        {
            get
            {
                var combinedList = new List<SceneObject>(removableRoads);
                combinedList.AddRange(removableIntersections);
                return combinedList;
            }
        }

        public void DestroyNewObjects()
        {
            foreach (var newRoad in newRoads) ObjectUtility.DestroyObject(newRoad.gameObject);
            foreach (var newRoad in newReplacedRoads) ObjectUtility.DestroyObject(newRoad.gameObject);
            foreach (var newIntersection in newIntersections) ObjectUtility.DestroyObject(newIntersection.gameObject);
            foreach (var newIntersection in newReplacedIntersections) ObjectUtility.DestroyObject(newIntersection.gameObject);
            newRoads.Clear();
            newReplacedRoads.Clear();
            newIntersections.Clear();
            newReplacedIntersections.Clear();
        }

        public void DestroyRemovableObjects()
        {
            foreach (var removableRoad in removableRoads) ObjectUtility.DestroyObject(removableRoad.gameObject);
            foreach (var removableIntersection in removableIntersections) ObjectUtility.DestroyObject(removableIntersection.gameObject);
            removableRoads.Clear();
            removableIntersections.Clear();
        }

    }
}
