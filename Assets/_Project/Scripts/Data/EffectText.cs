using System;
using System.Reflection;
using UnityEngine;

namespace Game.Data
{
    // 효과·스킬 설명 글에 공통으로 쓰는 숫자·이름 글자 도구 (표시 전용).
    // 상태효과 툴팁(StatusDescriptionBuilder)과 스킬 설명(효과 블록의 Describe)이 같은 표기를 쓰도록 한곳에 모았다.
    public static class EffectText
    {
        // 비율(0.6)을 퍼센트 글(60%)로 바꾼다. "0.##": 소수 둘째 자리까지, 필요 없는 0은 생략.
        public static string Percent(float ratio)
        {
            return $"{ratio * 100f:0.##}%";
        }

        // 부호를 붙인 숫자. 예: 30 → "+30", -12.5 → "-12.5"
        // "+0.##;-0.##": 세미콜론 앞은 양수 형식, 뒤는 음수 형식이다.
        public static string Signed(float value)
        {
            return value.ToString("+0.##;-0.##");
        }

        // 스탯 보정 한 줄. 예: 공격력 +30%, 속도 +10, 방어력 -20%
        public static string Modifier(StatModifier modifier)
        {
            string text = StatName(modifier.Stat);

            if (modifier.Percent != 0f)
            {
                text += $" {Signed(modifier.Percent * 100f)}%";
            }

            if (modifier.Flat != 0f)
            {
                text += $" {Signed(modifier.Flat)}";
            }

            return text;
        }

        // 스탯 종류의 화면 이름. switch 식: 값마다 결과를 나열하고, _ 는 나머지 모든 경우다.
        public static string StatName(StatType stat) => stat switch
        {
            StatType.Attack => "공격력",
            StatType.Defense => "방어력",
            StatType.Speed => "속도",
            StatType.CritRate => "치명 확률",
            StatType.CritDamage => "치명 피해",
            StatType.DamageBonus => "주는 피해",
            StatType.DamageTakenIncrease => "받는 피해",
            StatType.DamageReduction => "피해 감소",
            StatType.DamageOverTimeBonus => "지속피해",
            StatType.MaxHp => "최대 HP",
            _ => stat.ToString(),
        };

        // enum 값에 붙인 [InspectorName("한국어 이름")]을 읽어 화면 이름으로 쓴다 (대상 규칙, 패시브 발동 조건 등).
        // 리플렉션: GetField(값 이름)로 그 enum 항목을 찾고, GetCustomAttribute로 붙은 속성을 꺼낸다. 없으면 영어 이름 그대로.
        public static string EnumName(Enum value)
        {
            FieldInfo field = value.GetType().GetField(value.ToString());
            InspectorNameAttribute attribute = field != null ? field.GetCustomAttribute<InspectorNameAttribute>() : null;
            return attribute != null ? attribute.displayName : value.ToString();
        }

        // 상태효과 이름 (에셋이 비어 있으면 "?").
        public static string StatusName(StatusEffectData status)
        {
            return status != null ? status.DisplayName : "?";
        }

        // 상태효과 데이터의 요약: 지속 턴 + 실제 효과. 스킬 탭에서 "무엇을 거는지" 한눈에 보이도록 쓴다.
        // 예: "3턴 · 공격력 +30%", "2턴 · 턴 종료 시 공격력 60% 피해"
        public static string StatusSummary(StatusEffectData status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            var parts = new System.Collections.Generic.List<string>
            {
                status.IsPermanent ? "영구" : $"{status.Duration}턴",
            };

            foreach (StatModifier modifier in status.StatModifiers)
            {
                parts.Add(Modifier(modifier));
            }

            if (status.DamageAttackRatio > 0f)
            {
                parts.Add($"턴 종료 시 공격력 {Percent(status.DamageAttackRatio)} 피해");
            }

            if (status.DamageTargetMaxHpRatio > 0f)
            {
                parts.Add($"턴 종료 시 최대 HP {Percent(status.DamageTargetMaxHpRatio)} 피해");
            }

            if (status.HealsOverTime)
            {
                parts.Add($"턴 종료 시 최대 HP {Percent(status.HealSourceMaxHpRatio)} 회복");
            }

            if (status.SkipsTurn)
            {
                parts.Add("행동 불가");
            }

            if (status.GrantsDebuffImmunity)
            {
                parts.Add("디버프 면역");
            }

            if (status.BlocksAllDamage)
            {
                parts.Add("모든 피해 무효");
            }

            if (status.ConvertsTo != null)
            {
                parts.Add($"지워지면 {status.ConvertsTo.DisplayName}");
            }

            return string.Join(" · ", parts);
        }
    }
}
