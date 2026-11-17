using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // 상태효과 에셋 Inspector: 공통 칸 + "효과 종류"에서 고른 효과의 칸만 보여준다.
    // 예: 공격력 증가 → 스탯 보정 칸만, 소생 → 지속 회복 칸만, 기절 → 설명 한 줄만.
    // 값 저장은 Unity 기본 방식(SerializedProperty) 그대로라, 이 화면을 지워도 에셋 데이터는 바뀌지 않는다.
    // [CustomEditor(typeof(...))]: 이 타입의 에셋을 선택하면 기본 Inspector 대신 아래 OnInspectorGUI로 그린다.
    [CustomEditor(typeof(StatusEffectData))]
    public class StatusEffectDataEditor : Editor
    {
        // Inspector를 그린다: 기본 정보 → 효과 종류 → 고른 효과의 칸 → 표시 설정 순서.
        public override void OnInspectorGUI()
        {
            // serializedObject.Update: 에셋의 최신 값을 읽어 온다. 끝의 ApplyModifiedProperties가 바꾼 값을 저장한다.
            serializedObject.Update();

            DrawBasics();
            StatusFeatures features = DrawFeatureSelector();
            DrawFeatureSections(features);
            DrawDisplay();

            serializedObject.ApplyModifiedProperties();
        }

        // 이름, 설명, 분류, 중첩 방식(디버프만), 지속 턴.
        private void DrawBasics()
        {
            Draw("displayName");
            Draw("description");
            Draw("category");

            // 중첩 방식은 버프·디버프 모두 보여준다 (버프는 '스택'만 따르고 나머지는 갱신형으로 처리).
            Draw("stackMode");

            // 스택 방식(enumValueIndex 2 = StackCount)일 때만 최대 스택 칸을 보여준다.
            if (serializedObject.FindProperty("stackMode").enumValueIndex == (int)StatusStackMode.StackCount)
            {
                Draw("maxStacks");
            }

            Draw("duration");
        }

        // 효과 종류를 여러 개 고르는 드롭다운을 그리고, 고른 값을 돌려준다.
        // EnumFlagsField: [Flags] 열거형을 체크 목록 드롭다운으로 그린다 (여러 항목 동시 선택 가능).
        private StatusFeatures DrawFeatureSelector()
        {
            EditorGUILayout.Space();
            SerializedProperty property = serializedObject.FindProperty("features");

            // intValue: 열거형이 저장된 정수 값. (StatusFeatures)로 바꿔 드롭다운에 넘기고, 고른 결과를 다시 정수로 저장한다.
            var current = (StatusFeatures)property.intValue;
            var selected = (StatusFeatures)EditorGUILayout.EnumFlagsField(
                new GUIContent("효과 종류", "이 상태효과가 가진 효과. 고른 종류의 설정만 아래에 나온다"),
                current);
            property.intValue = (int)selected;
            return selected;
        }

        // 고른 효과 종류마다 그 설정 칸만 그린다. 고르지 않은 종류는 화면에 나오지 않는다.
        private void DrawFeatureSections(StatusFeatures features)
        {
            if (Has(features, StatusFeatures.StatModifiers))
            {
                Section("스탯 보정");
                Draw("statModifiers");
            }

            if (Has(features, StatusFeatures.DamageOverTime))
            {
                Section("지속피해");
                Draw("damageAttackRatio", "공격력 비례");
                Draw("damageTargetMaxHpRatio", "대상 최대 HP 비례");
                Draw("maxHpDamageAttackCap", "HP 비례 상한 (공격력 배율)");
                Draw("damagePopupColor", "피해 숫자 색");
            }

            if (Has(features, StatusFeatures.HealOverTime))
            {
                Section("지속 회복");
                Draw("healSourceMaxHpRatio", "시전자 최대 HP 비례");
            }

            if (Has(features, StatusFeatures.SkipTurn))
            {
                Section("행동 불가");
                EditorGUILayout.HelpBox("걸려 있는 동안 자기 턴에 행동하지 못한다 (행동 제어 효과).", MessageType.None);
            }

            if (Has(features, StatusFeatures.ConvertOnRemove))
            {
                Section("지연 버프");
                Draw("convertsTo", "지워지면 바뀔 효과");
            }

            if (Has(features, StatusFeatures.RecordDamage))
            {
                Section("피해 기록");
                EditorGUILayout.HelpBox("걸려 있는 동안 받은 피해를 누적한다 (반격 태세).", MessageType.None);
            }
        }

        // 아이콘과 표시 옵션. 개인 버프는 버프에서만 의미가 있으므로 버프일 때만 보여준다.
        private void DrawDisplay()
        {
            Section("표시");
            Draw("icon");

            if (!IsDebuff())
            {
                Draw("isPersonal");
            }

            Draw("isPermanent");
        }

        private bool IsDebuff()
        {
            return serializedObject.FindProperty("category").enumValueIndex == (int)StatusCategory.Debuff;
        }

        private static bool Has(StatusFeatures features, StatusFeatures feature)
        {
            return (features & feature) != 0;
        }

        // 굵은 소제목 한 줄.
        private static void Section(string title)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        // 필드 하나를 기본 모양으로 그린다. label을 주면 그 이름으로 표시한다.
        // includeChildren = true: 리스트(스탯 보정)처럼 안에 항목이 있는 필드도 펼쳐서 그린다.
        private void Draw(string propertyName, string label = null)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            GUIContent content = label == null ? null : new GUIContent(label, property.tooltip);
            EditorGUILayout.PropertyField(property, content, includeChildren: true);
        }
    }
}
