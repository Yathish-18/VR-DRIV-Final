// ----------------------------------------------------
// Road Constructor
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

using System.Collections.Generic;
using PampelGames.Shared.Construction;
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public class UndoObject : MonoBehaviour
    {
        public ConstructionObjects constructionObjects;
    }

    internal static class UndoObjectUtility
    {
        public static void RegisterUndo(ComponentSettings settings, Transform undoParent, LinkedList<UndoObject> undoObjects,
            ConstructionObjects constructionObjects)
        {
            if (settings.undoStorageSize <= 0)
            {
                constructionObjects.DestroyRemovableObjects();
                return;
            }

            var undoObj = ObjectUtility.CreateUndoObj();

            var removableObjects = constructionObjects.CombinedRemovableObjects;

            foreach (var removableObject in removableObjects)
            {
                removableObject.transform.SetParent(undoObj.transform);
                removableObject.gameObject.SetActive(false);
            }

            undoObj.transform.SetParent(undoParent);
            var undoObject = undoObj.AddComponent<UndoObject>();

            undoObject.constructionObjects = constructionObjects;

            undoObjects.AddLast(undoObject);
            if (undoObjects.Count > settings.undoStorageSize)
            {
                var dequeuedUndo = undoObjects.First.Value;
                ObjectUtility.DestroyObject(dequeuedUndo.gameObject);
                undoObjects.RemoveFirst();
            }
        }
    }
}