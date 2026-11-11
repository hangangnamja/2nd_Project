using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // [Label("한국어 이름")]이 붙은 필드를 그린다: 이름만 바꾸고, 숫자 칸은 Min·Max 제한(슬라이더)까지 처리한다.
    // 툴팁은 [Tooltip]에서 온 label.tooltip을 그대로 이어받는다.
    [CustomPropertyDrawer(typeof(LabelAttribute))]
    public class LabelDrawer : PropertyDrawer
    {
        // 필드 한 칸을 한국어 이름으로 그린다. 숫자는 제한에 맞춰 슬라이더 또는 입력칸 + 최솟값 맞춤.
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = (LabelAttribute)attribute;
            var content = new GUIContent(attr.Text, label.tooltip);

            EditorGUI.BeginProperty(position, content, property);

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    DrawFloat(position, property, content, attr);
                    break;
                case SerializedPropertyType.Integer:
                    DrawInt(position, property, content, attr);
                    break;
                default:
                    // includeChildren = true: 구조체·클래스 필드면 내부 값까지 펼쳐 그린다.
                    EditorGUI.PropertyField(position, property, content, true);
                    break;
            }

            EditorGUI.EndProperty();
        }

        // 펼친 구조체 등 여러 줄 필드도 올바른 높이를 쓰도록 기본 높이 계산을 그대로 따른다.
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        // 실수 칸: Min·Max가 모두 있으면 슬라이더, Min만 있으면 입력 후 Mathf.Max로 최솟값 아래를 막는다.
        private static void DrawFloat(Rect position, SerializedProperty property, GUIContent content, LabelAttribute attr)
        {
            if (attr.HasRange)
            {
                property.floatValue = EditorGUI.Slider(position, content, property.floatValue, attr.Min, attr.Max);
                return;
            }

            float value = EditorGUI.FloatField(position, content, property.floatValue);
            property.floatValue = attr.HasMin ? Mathf.Max(attr.Min, value) : value;
        }

        // 정수 칸: 실수 칸과 같은 규칙. 제한값은 정수로 바꿔(Mathf.RoundToInt: 가장 가까운 정수) 쓴다.
        private static void DrawInt(Rect position, SerializedProperty property, GUIContent content, LabelAttribute attr)
        {
            if (attr.HasRange)
            {
                property.intValue = EditorGUI.IntSlider(position, content, property.intValue, Mathf.RoundToInt(attr.Min), Mathf.RoundToInt(attr.Max));
                return;
            }

            int value = EditorGUI.IntField(position, content, property.intValue);
            property.intValue = attr.HasMin ? Mathf.Max(Mathf.RoundToInt(attr.Min), value) : value;
        }
    }
}
