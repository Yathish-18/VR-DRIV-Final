#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

// Custom property drawer for GraphConnection
[CustomPropertyDrawer(typeof(GraphConnection))]
public class GraphConnectionDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        position = EditorGUI.PrefixLabel(position,
            GUIUtility.GetControlID(FocusType.Passive), label);

        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;

        // Calculate rects
        var fromRect = new Rect(position.x, position.y, 30, position.height);
        var arrowRect = new Rect(position.x + 35, position.y, 20, position.height);
        var toRect = new Rect(position.x + 60, position.y, 30, position.height);
        var weightRect = new Rect(position.x + 95, position.y, 40, position.height);
        var bidirRect = new Rect(position.x + 140, position.y, 15, position.height);

        // Draw fields
        EditorGUI.PropertyField(fromRect,
            property.FindPropertyRelative("fromNodeID"), GUIContent.none);

        SerializedProperty bidirectionalProp =
            property.FindPropertyRelative("bidirectional");

        string arrow = bidirectionalProp.boolValue ? "↔" : "→";
        EditorGUI.LabelField(arrowRect, arrow);

        EditorGUI.PropertyField(toRect,
            property.FindPropertyRelative("toNodeID"), GUIContent.none);

        EditorGUI.PropertyField(weightRect,
            property.FindPropertyRelative("weight"), GUIContent.none);

        EditorGUI.PropertyField(bidirRect, bidirectionalProp, GUIContent.none);

        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }
}
#endif
