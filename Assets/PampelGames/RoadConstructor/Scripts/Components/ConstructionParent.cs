// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace PampelGames.RoadConstructor
{
    public class ConstructionParent : MonoBehaviour
    {
        
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ConstructionParent))]
    public class ConstructionParentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("This GameObject will be removed after pressing the Register Scene Objects button.", MessageType.Info);
        }
    }
#endif

}
