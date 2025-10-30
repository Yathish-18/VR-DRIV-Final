// ----------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ----------------------------------------------------

#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace PampelGames.RoadConstructor.Editor
{
    [CustomEditor(typeof(TreeBending))]  
    public class TreeBendingInspector : UnityEditor.Editor  
    {  
        private TreeBending _treeBending;
	    
        private VisualElement container;
    
        private Slider bendStrength; 
        private Vector2Field bendDirection;
        
        protected void OnEnable()  
        {         
            _treeBending = target as TreeBending;
            container = new VisualElement();
            
            CreateElements();
            FindAndBindProperties();
        
            container.Add(bendStrength);
            container.Add(bendDirection);
        }

        private void CreateElements()
        {
            bendStrength = new Slider();
            bendStrength.label = "Bend Strength";
            bendStrength.highValue = 2f;
            bendStrength.lowValue = 0f;
            bendStrength.showInputField = true;
            bendDirection = new Vector2Field();
            bendDirection.label = "Bend Direction";
        }
        private void FindAndBindProperties()
        {
            var bendStrengthProperty = serializedObject.FindProperty(nameof(TreeBending.bendStrength));  
            bendStrength.BindProperty(bendStrengthProperty);
            var bendDirectionProperty = serializedObject.FindProperty(nameof(TreeBending.bendDirection));  
            bendDirection.BindProperty(bendDirectionProperty);
        }
    
        /********************************************************************************************************************************/
    
        public override VisualElement CreateInspectorGUI()  
        {  
        
            return container;  
        }            
    }
}  

#endif