using System.Collections.Generic;
using System.Text;
using Game.Battle.Status;
using Game.Data;

namespace Game.UI
{
    // 상태효과 하나를 툴팁용 짧은 설명 글로 만든다 (표시 전용).
    // 수치는 상태효과 데이터(스탯 보정, 지속피해, 지연 버프 등)에서 자동으로 읽어 오므로,
    // 수치를 바꿔도 설명 글을 따로 고칠 필요가 없다. 데이터의 "설명" 칸은 수치로 안 드러나는 의미만 덧붙인다.
    public static class StatusDescriptionBuilder
    {
        // 제목 줄 + 설명 + 효과 줄들을 줄바꿈으로 이어 돌려준다.
        // <b>…</b>: uGUI Text의 리치 텍스트 태그로 굵게 표시한다.
        // 예: "<b>공격력 증가</b> (버프 · 남은 2턴)\n공격력 +30%\n부여: BearBuffer"
        public static string Build(StatusInstance status)
        {
            StatusEffectData data = status.Data;
            var lines = new List<string> { BuildTitle(status) };

            if (!string.IsNullOrWhiteSpace(data.Description))
            {
                lines.Add(data.Description);
            }

            AddEffectLines(status, lines);

            // ?.Name: 부여자가 없으면(null) 이름을 읽지 않고 null → 이 줄을 건너뛴다.
            string sourceName = status.Source?.Name;
            if (sourceName != null)
            {
                lines.Add($"부여: {sourceName}");
            }

            return string.Join("\n", lines);
        }

        // 제목 줄: 이름, 분류(버프/개인 버프/디버프), 남은 턴.
        private static string BuildTitle(StatusInstance status)
        {
            StatusEffectData data = status.Data;
            string category = data.Category == StatusCategory.Debuff ? "디버프"
                : data.IsPersonal ? "개인 버프"
                : "버프";

            string duration = data.IsPermanent ? "영구" : $"남은 {status.RemainingTurns}턴";
            return $"<b>{data.DisplayName}</b> ({category} · {duration})";
        }

        // 실제 효과를 한 줄씩 붙인다: 지연 버프, 스탯 보정, 지속피해, 보호막, 피해 기록.
        private static void AddEffectLines(StatusInstance status, List<string> lines)
        {
            StatusEffectData data = status.Data;

            // 지연 버프는 걸려 있는 동안 효과가 없으므로 "무엇으로 바뀌는지"가 핵심 정보다.
            if (data.ConvertsTo != null)
            {
                lines.Add($"걸려 있는 동안 효과 없음. 지워지면 {data.ConvertsTo.DisplayName}(으)로 바뀜");
            }

            foreach (StatModifier modifier in data.StatModifiers)
            {
                lines.Add(FormatModifier(modifier));
            }

            if (data.DamageAttackRatio > 0f)
            {
                lines.Add($"턴 종료 시 피해 (부여 당시 공격력 {ToPercent(data.DamageAttackRatio)})");
            }

            if (data.DamageTargetMaxHpRatio > 0f)
            {
                string cap = data.MaxHpDamageAttackCap > 0f
                    ? $", 상한 부여자 공격력 {ToPercent(data.MaxHpDamageAttackCap)}"
                    : string.Empty;
                lines.Add($"턴 종료 시 피해 (최대 HP {ToPercent(data.DamageTargetMaxHpRatio)}{cap})");
            }

            if (data.HealsOverTime)
            {
                lines.Add($"턴 종료 시 회복 (부여 당시 시전자 최대 HP {ToPercent(data.HealSourceMaxHpRatio)} = {(int)(status.SnapshotMaxHp * data.HealSourceMaxHpRatio)})");
            }

            if (status.HasShield)
            {
                lines.Add($"보호막 {status.ShieldAmount} 남음");
            }

            if (data.RecordsDamageTaken)
            {
                lines.Add($"받은 피해 기록 중: {status.RecordedDamage}");
            }
        }

        // 스탯 보정 한 줄을 글로 만든다. 예: 공격력 +30%, 속도 +10, 방어력 -20%
        private static string FormatModifier(StatModifier modifier)
        {
            var text = new StringBuilder(GetStatName(modifier.Stat));

            if (modifier.Percent != 0f)
            {
                text.Append(' ').Append(FormatSigned(modifier.Percent * 100f)).Append('%');
            }

            if (modifier.Flat != 0f)
            {
                text.Append(' ').Append(FormatSigned(modifier.Flat));
            }

            return text.ToString();
        }

        // 부호를 붙인 숫자. 소수점이 필요 없으면 정수로 보인다. 예: 30 → "+30", -12.5 → "-12.5"
        // "+0.##;-0.##": 양수 형식;음수 형식. 소수 둘째 자리까지, 필요 없는 0은 생략한다.
        private static string FormatSigned(float value)
        {
            return value.ToString("+0.##;-0.##");
        }

        // 비율(0.6)을 퍼센트 글(60%)로 바꾼다.
        private static string ToPercent(float ratio)
        {
            return $"{ratio * 100f:0.##}%";
        }

        // 스탯 종류의 화면 이름.
        // switch 식: 값마다 돌려줄 결과를 나열하고, _ 는 나머지 모든 경우다.
        private static string GetStatName(StatType stat) => stat switch
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
    }
}
