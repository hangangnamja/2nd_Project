using System;
using System.Collections;
using Game.Core;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // [SubclassSelector]가 붙은 [SerializeReference] 필드를 그리는 Inspector 확장.
    // 필드 오른쪽에 드롭다운 버튼을 그리고, 고른 하위 클래스의 새 인스턴스를 필드에 넣어준다.
    // 이 드로어 덕분에 효과 블록 목록에서 "피해 / 회복 ..." 종류를 골라 끼워 넣을 수 있다.
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        private const string NoneLabel = "(없음)";

        // 필드 한 칸을 그린다: 기본 필드(펼치기 + 내부 값) 위에 첫 줄 오른쪽으로 종류 선택 버튼을 겹쳐 그린다.
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // SerializeReference가 아닌 필드에 잘못 붙였으면 평소처럼 그리고 끝낸다.
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            Rect buttonRect = new Rect(
                position.x + EditorGUIUtility.labelWidth,
                position.y,
                position.width - EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight);

            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(GetCurrentTypeName(property)), FocusType.Keyboard))
            {
                ShowTypeMenu(property);
            }

            // includeChildren = true: 고른 효과의 내부 값(배율 등)까지 펼쳐서 그린다.
            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.EndProperty();
        }

        // 내부 값까지 펼쳤을 때의 전체 높이를 Inspector에 알려준다.
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }

        // 필드 타입을 상속한 구체 클래스 목록으로 메뉴를 만들어 띄운다.
        private void ShowTypeMenu(SerializedProperty property)
        {
            Type baseType = GetElementType(fieldInfo.FieldType);
            var menu = new GenericMenu();

            // 메뉴 클릭은 나중(다음 프레임)에 실행되므로, 지금의 property 대신
            // 대상 오브젝트와 경로를 저장해 두었다가 클릭 시점에 다시 찾는다.
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;

            // 람다식 () => ...: 메뉴 항목을 눌렀을 때 실행할 코드를 함수로 넘긴다.
            menu.AddItem(new GUIContent(NoneLabel), false, () => AssignInstance(serializedObject, propertyPath, null));

            // TypeCache.GetTypesDerivedFrom: 프로젝트에서 baseType을 상속한 모든 타입을 빠르게 찾는다.
            foreach (Type type in TypeCache.GetTypesDerivedFrom(baseType))
            {
                if (!IsCreatable(type))
                {
                    continue;
                }

                // 반복 변수를 지역 변수로 복사해야 각 메뉴 항목이 자기 타입을 기억한다.
                Type capturedType = type;
                menu.AddItem(new GUIContent(type.Name), false, () => AssignInstance(serializedObject, propertyPath, capturedType));
            }

            menu.ShowAsContext();
        }

        // 고른 타입의 새 인스턴스를 만들어 필드에 넣고 저장한다. type이 null이면 비운다.
        private static void AssignInstance(SerializedObject serializedObject, string propertyPath, Type type)
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);

            // Activator.CreateInstance: 타입 정보만으로 기본 생성자를 호출해 새 객체를 만든다.
            property.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
            serializedObject.ApplyModifiedProperties();
        }

        // 리스트·배열 필드라면 원소 타입을, 단일 필드라면 필드 타입 그대로를 돌려준다.
        // 예: List<SkillEffect> → SkillEffect
        private static Type GetElementType(Type fieldType)
        {
            if (fieldType.IsArray)
            {
                return fieldType.GetElementType();
            }

            if (fieldType.IsGenericType && typeof(IList).IsAssignableFrom(fieldType))
            {
                return fieldType.GetGenericArguments()[0];
            }

            return fieldType;
        }

        // 드롭다운에 넣을 수 있는 타입인지 검사한다: 추상 클래스·제네릭·Unity 오브젝트는 직접 만들 수 없으므로 제외.
        private static bool IsCreatable(Type type)
        {
            return !type.IsAbstract
                && !type.IsGenericType
                && !typeof(UnityEngine.Object).IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) != null;
        }

        // 버튼에 표시할 현재 타입 이름을 구한다.
        // managedReferenceFullTypename은 "어셈블리명 네임스페이스.클래스명" 형식이라 마지막 클래스명만 잘라 쓴다.
        private static string GetCurrentTypeName(SerializedProperty property)
        {
            string fullName = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(fullName))
            {
                return NoneLabel;
            }

            int lastDot = fullName.LastIndexOf('.');
            int lastSpace = fullName.LastIndexOf(' ');

            // Math.Max: 점과 공백 중 더 뒤에 있는 구분자 다음부터가 클래스 이름이다.
            return fullName.Substring(Math.Max(lastDot, lastSpace) + 1);
        }
    }
}
