#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace CreatureClimb.Editor
{
    /// <summary>
    /// Float slider that works inside a ReorderableList (Unity's Slider loses MouseDrag to list reorder).
    /// </summary>
    public static class InspectorSliders
    {
        public static void FloatSlider(Rect rect, SerializedProperty property, string label, float min, float max)
        {
            if (property == null) return;

            var value = property.floatValue;
            if (value < min && Mathf.Abs(value) < 0.05f && min <= 1f && max >= 1f)
                value = 1f;
            value = Mathf.Clamp(value, min, max);

            var labelW = Mathf.Min(EditorGUIUtility.labelWidth, rect.width * 0.38f);
            var numW = 52f;
            var gap = 4f;
            var labelRect = new Rect(rect.x, rect.y, labelW, rect.height);
            var sliderRect = new Rect(
                rect.x + labelW + gap,
                rect.y + 3f,
                Mathf.Max(16f, rect.width - labelW - numW - gap * 3f),
                rect.height - 6f);
            var numRect = new Rect(rect.xMax - numW, rect.y, numW, rect.height);

            EditorGUI.LabelField(labelRect, label);

            var id = GUIUtility.GetControlID(FocusType.Passive, sliderRect);
            var e = Event.current;

            float FromMouse()
            {
                var t = Mathf.InverseLerp(sliderRect.xMin, sliderRect.xMax, e.mousePosition.x);
                return Mathf.Lerp(min, max, t);
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (e.button == 0 && sliderRect.Contains(e.mousePosition))
                    {
                        GUIUtility.hotControl = id;
                        property.floatValue = Mathf.Clamp(FromMouse(), min, max);
                        e.Use();
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        property.floatValue = Mathf.Clamp(Mathf.Round(FromMouse() * 100f) / 100f, min, max);
                        GUI.changed = true;
                        e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
                case EventType.Repaint:
                    GUI.HorizontalSlider(sliderRect, property.floatValue < min ? value : property.floatValue, min, max);
                    break;
            }

            EditorGUI.BeginChangeCheck();
            var typed = EditorGUI.FloatField(numRect, property.floatValue < min ? value : Mathf.Clamp(property.floatValue, min, max));
            if (EditorGUI.EndChangeCheck())
                property.floatValue = Mathf.Clamp(typed, min, max);
        }
    }
}
#endif
