// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using PampelGames.Shared.Utility;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public class SpawnedObject : MonoBehaviour
    {
        public SpawnObject spawnObject;
        public bool otherSide;

        public void Initialize(SpawnObject spawnObject)
        {
            this.spawnObject = PGClassUtility.CopyClass(spawnObject) as SpawnObject;
        }
    }
}