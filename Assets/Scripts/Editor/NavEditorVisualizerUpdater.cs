//#if UNITY_EDITOR
//using UnityEngine;

//[ExecuteInEditMode]
//[RequireComponent(typeof(CentralizedNavigationSystem))]
//public class NavEditorVisualizerUpdater : MonoBehaviour
//{
//    private CentralizedNavigationSystem nav;

//    private void OnEnable()
//    {
//        nav = GetComponent<CentralizedNavigationSystem>();
//    }

//    private void Update()
//    {
//        if (!Application.isPlaying && nav != null && nav.visualizeAllConnectionsEditor)
//        {
//            nav.DrawAllConnectionsIntoLineRenderer();
//        }
//    }
//}
//#endif
