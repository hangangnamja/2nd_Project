using System.Collections.Generic;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // 패시브(PassiveData) 한 개를 Inspector에 그린다. 발동 조건에 따라 쓰는 칸만 보여 준다.
    //  - 항상: 이름, 발동 조건
    //  - 상시형(오라, 시선집중)이 아닐 때: 실행 시점, 횟수 제한, 스택, 효과 목록
    //  - 발동 조건 전용 칸: HP 기준선 / 필요한 상태효과 / 시선집중 표시 효과 / 오라 스탯 보정
    //  - 스택: "스택 사용"을 켰을 때만 세부 칸을 보여 준다 (끄면 스택 값을 0으로 되돌림)
    // 칸을 숨겨도 저장된 값은 지우지 않는다(스택 끄기 제외). 전투 코드는 발동 조건에 맞는 값만 읽으므로 남아 있어도 영향이 없다.
    [CustomPropertyDrawer(typeof(PassiveData))]
    public class PassiveDataDrawer : PropertyDrawer
    {
        private const float Gap = 2f;

        // 그릴 줄 하나: 필드(property) 또는 소제목(header) 또는 "스택 사용" 토글.
        // label이 있으면 필드 이름 대신 이 글자로 그린다 (리스트 필드의 한국어 이름).
        private struct Row
        {
            public SerializedProperty Property;
            public GUIContent Label;
            public string Header;
            public bool IsStackToggle;
        }

        // 접힌 제목 줄 + 펼쳤으면 보이는 줄들을 위에서부터 차례로 그린다.
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            // 제목에는 리스트 번호 대신 패시브 이름을 보여 준다 (비어 있으면 원래 이름).
            string passiveName = property.FindPropertyRelative("passiveName").stringValue;
            string title = string.IsNullOrEmpty(passiveName) ? label.text : passiveName;
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, title, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                float y = line.yMax + Gap;

                foreach (Row row in BuildRows(property))
                {
                    float height = GetRowHeight(row);
                    DrawRow(new Rect(position.x, y, position.width, height), property, row);
                    y += height + Gap;
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        // 그리는 줄과 같은 목록으로 높이를 더한다 → OnGUI와 높이가 어긋나지 않는다.
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
            {
                return height;
            }

            foreach (Row row in BuildRows(property))
            {
                height += GetRowHeight(row) + Gap;
            }

            return height;
        }

        // 발동 조건을 보고 보여 줄 줄 목록을 만든다.
        private static List<Row> BuildRows(SerializedProperty property)
        {
            var rows = new List<Row>();
            SerializedProperty trigger = property.FindPropertyRelative("trigger");

            // intValue를 PassiveTrigger로 바꿔 비교한다 (enum은 정수로 저장된다).
            var triggerValue = (PassiveTrigger)trigger.intValue;

            // 상시형: 사건에 반응하지 않아 실행 시점·횟수·효과 목록을 쓰지 않는다.
            bool isAlwaysOn = PassiveData.IsAlwaysOnTrigger(triggerValue);

            AddField(rows, property, "passiveName");
            AddField(rows, property, "trigger");

            if (!isAlwaysOn)
            {
                AddField(rows, property, "execution");

                rows.Add(new Row { Header = "횟수 제한 (0 = 제한 없음)" });
                AddField(rows, property, "maxTriggersPerTurn");

                // 턴당 제한이 있을 때만 "언제 다시 채우는지"가 의미가 있다.
                if (property.FindPropertyRelative("maxTriggersPerTurn").intValue > 0)
                {
                    AddField(rows, property, "turnLimitScope");
                }

                AddField(rows, property, "maxTriggersPerBattle");
                AddField(rows, property, "inactiveWhileTransformed");

                rows.Add(new Row { IsStackToggle = true });
                if (property.FindPropertyRelative("maxStacks").intValue > 0)
                {
                    AddField(rows, property, "maxStacks");
                    AddField(rows, property, "stacksPerOwnAction");
                    AddField(rows, property, "stackCostPerTrigger");
                    AddField(rows, property, "stacksGainedPerTrigger");
                }
            }

            // switch: 발동 조건마다 전용 칸이 하나씩 있다.
            switch (triggerValue)
            {
                case PassiveTrigger.OnEnemyHpThreshold:
                    rows.Add(new Row { Header = "HP 돌파" });
                    AddField(rows, property, "hpThreshold");
                    break;
                case PassiveTrigger.OnAllyHitDebuffedEnemy:
                    rows.Add(new Row { Header = "필요한 상태효과" });
                    AddField(rows, property, "requiredStatus");
                    break;
                case PassiveTrigger.TauntWhileShielded:
                    rows.Add(new Row { Header = "시선집중" });
                    AddField(rows, property, "tauntDisplay");
                    break;
                case PassiveTrigger.Aura:
                    rows.Add(new Row { Header = "오라" });
                    AddField(rows, property, "auraModifiers", "오라 스탯 보정");
                    break;
                case PassiveTrigger.WhileSummonsAlive:
                    // 오라와 같은 보정 칸을 쓰지만 같은 편 전원이 아니라 자신에게만 건다.
                    rows.Add(new Row { Header = "부하가 있는 동안 (자신)" });
                    AddField(rows, property, "auraModifiers", "자신 스탯 보정");
                    AddField(rows, property, "tauntDisplay");
                    break;
                case PassiveTrigger.WhileOwnStatusCount:
                    // 조건을 만족하는 동안 보정을 그대로 건다 (개수와 무관).
                    rows.Add(new Row { Header = "상태효과 수 조건 (자신)" });
                    AddField(rows, property, "countedCategory");
                    AddField(rows, property, "countThreshold");
                    AddField(rows, property, "countComparison");
                    AddField(rows, property, "auraModifiers", "자신 스탯 보정");
                    AddField(rows, property, "tauntDisplay");
                    break;
                case PassiveTrigger.ScalesWithOwnStatusCount:
                    // 보정 칸은 1개당 값이고, 개수만큼 곱해서 건다.
                    rows.Add(new Row { Header = "상태효과 수 비례 (자신)" });
                    AddField(rows, property, "countedCategory");
                    AddField(rows, property, "countCap");
                    AddField(rows, property, "auraModifiers", "1개당 자신 스탯 보정");
                    AddField(rows, property, "tauntDisplay");
                    break;
            }

            if (!isAlwaysOn)
            {
                AddField(rows, property, "effects", "효과 목록");
                AddField(rows, property, "presentationPrefab");
            }

            return rows;
        }

        // 필드 한 줄을 목록에 넣는다. label을 주면 그 이름으로 그린다.
        private static void AddField(List<Row> rows, SerializedProperty parent, string name, string label = null)
        {
            rows.Add(new Row
            {
                Property = parent.FindPropertyRelative(name),
                Label = label != null ? new GUIContent(label) : null,
            });
        }

        // 한 줄의 높이. 소제목·토글은 한 줄, 필드는 펼친 리스트까지 포함한 실제 높이.
        private static float GetRowHeight(Row row)
        {
            if (row.Property == null)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            // label이 null이면 필드에 붙은 [Label]·이름을 그대로 쓴다.
            return row.Label != null
                ? EditorGUI.GetPropertyHeight(row.Property, row.Label, true)
                : EditorGUI.GetPropertyHeight(row.Property, true);
        }

        // 한 줄을 종류에 맞게 그린다.
        private static void DrawRow(Rect rect, SerializedProperty parent, Row row)
        {
            if (row.Header != null)
            {
                EditorGUI.LabelField(rect, row.Header, EditorStyles.boldLabel);
                return;
            }

            if (row.IsStackToggle)
            {
                DrawStackToggle(rect, parent);
                return;
            }

            if (row.Label != null)
            {
                EditorGUI.PropertyField(rect, row.Property, row.Label, true);
            }
            else
            {
                EditorGUI.PropertyField(rect, row.Property, true);
            }
        }

        // "스택 사용" 토글: 스택은 최대 스택이 0보다 크면 켜진 것으로 본다 (PassiveData.UsesStacks와 같은 기준).
        // 켜면 최대 스택 1로 시작하고, 끄면 스택 관련 값을 0으로 되돌려 남은 값이 동작을 바꾸지 않게 한다.
        private static void DrawStackToggle(Rect rect, SerializedProperty parent)
        {
            SerializedProperty maxStacks = parent.FindPropertyRelative("maxStacks");
            bool usesStacks = maxStacks.intValue > 0;
            bool toggled = EditorGUI.Toggle(rect, new GUIContent("스택 사용", "체크하면 스택 칸이 나온다. 끄면 스택 값이 0으로 돌아간다"), usesStacks);

            if (toggled == usesStacks)
            {
                return;
            }

            if (toggled)
            {
                maxStacks.intValue = 1;
                return;
            }

            maxStacks.intValue = 0;
            parent.FindPropertyRelative("stacksPerOwnAction").intValue = 0;
            parent.FindPropertyRelative("stacksGainedPerTrigger").intValue = 0;
        }
    }
}
