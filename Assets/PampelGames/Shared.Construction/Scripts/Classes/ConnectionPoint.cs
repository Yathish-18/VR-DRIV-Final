// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace PampelGames.Shared.Construction
{
    [Serializable]
    public class ConnectionPoint
    {
        public int index;
        public bool used;
        public Vector3 position;
        public Vector3 tangent;

        public ConnectionPoint()
        {
            used = true;
        }

        public ConnectionPoint(int index, bool used, Vector3 position, Vector3 tangent)
        {
            this.index = index;
            this.used = used;
            this.position = position;
            this.tangent = tangent;
        }

        public ConnectionPoint(ConnectionPoint other)
        {
            index = other.index;
            used = other.used;
            position = other.position;
            tangent = other.tangent;
        }
    }

    public static class ConnectionPointUtility
    {
        public static bool TryGetFreeConnection(List<ConnectionPoint> connectionPoints, Vector3 position,
            out ConnectionPoint nearestConnectionPoint)
        {
            nearestConnectionPoint = null;
            var freeConnectionPoints = connectionPoints.Where(t => !t.used).ToList();
            if (freeConnectionPoints.Count <= 0) return false;
            var minDistance = float.MaxValue;
            var closestIndex = -1;
            for (var index = 0; index < freeConnectionPoints.Count; index++)
            {
                var point = freeConnectionPoints[index];
                var distance = math.distancesq(position, point.position);
                if (!(distance < minDistance)) continue;
                minDistance = distance;
                closestIndex = index;
            }

            nearestConnectionPoint = freeConnectionPoints[closestIndex];
            return true;
        }

        public static ConnectionPoint GetNearestConnection(List<ConnectionPoint> connectionPoints, Vector3 position)
        {
            var minDistance = float.MaxValue;
            var closestIndex = -1;
            for (var index = 0; index < connectionPoints.Count; index++)
            {
                var point = connectionPoints[index];
                var distance = math.distancesq(position, point.position);
                if (!(distance < minDistance)) continue;
                minDistance = distance;
                closestIndex = index;
            }

            return connectionPoints[closestIndex];
        }
    }
}