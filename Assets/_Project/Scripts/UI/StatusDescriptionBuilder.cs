using System.Collections.Generic;
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

            // 보호막은 여러 명이 양을 보탤 수 있으므로 보탠 유닛을 모두 적는다. 그 외에는 부여자 1명.
            // ?.Name: 부여자가 없으면(null) 이름을 읽지 않고 null → 이 줄을 건너뛴다.
            string sourceName = status.ShieldContributors.Count > 0
                ? JoinNames(status.ShieldContributors)
                : status.Source?.Name;
            if (sourceName != null)
            {
                lines.Add($"부여: {sourceName}");
            }

            return string.Join("\n", lines);
        }

        // 유닛 이름들을 ", "로 잇는다. 예: "도발 탱커, BearBuffer"
        private static string JoinNames(IReadOnlyList<Game.Battle.BattleUnit> units)
        {
            var names = new List<string>();
            foreach (Game.Battle.BattleUnit unit in units)
            {
                names.Add(unit.Name);
            }

            return string.Join(", ", names);
        }

        // 제목 줄: 이름, 분류(버프/개인 버프/디버프), 남은 턴.
        private static string BuildTitle(StatusInstance status)
        {
            StatusEffectData data = status.Data;
            string category = data.Category == StatusCategory.Debuff ? "디버프"
                : data.IsPersonal ? "개인 버프"
                : "버프";

            string duration = data.IsPermanent ? "영구" : $"남은 {status.RemainingTurns}턴";
            string stacks = data.UsesStackCount ? $" ×{status.Stacks}" : string.Empty;
            return $"<b>{data.DisplayName}{stacks}</b> ({category} · {duration})";
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
                lines.Add(EffectText.Modifier(modifier));
            }

            if (data.DamageAttackRatio > 0f)
            {
                lines.Add($"턴 종료 시 피해 (부여 당시 공격력 {EffectText.Percent(data.DamageAttackRatio)}{PerStack(data)})");
            }

            if (data.DamageTargetMaxHpRatio > 0f)
            {
                string cap = data.MaxHpDamageAttackCap > 0f
                    ? $", 상한 부여자 공격력 {EffectText.Percent(data.MaxHpDamageAttackCap)}"
                    : string.Empty;
                lines.Add($"턴 종료 시 피해 (최대 HP {EffectText.Percent(data.DamageTargetMaxHpRatio)}{cap}{PerStack(data)})");
            }

            if (data.HealsOverTime)
            {
                lines.Add($"턴 종료 시 회복 (부여 당시 시전자 최대 HP {EffectText.Percent(data.HealSourceMaxHpRatio)} = {(int)(status.SnapshotMaxHp * data.HealSourceMaxHpRatio)})");
            }

            if (status.HasShield)
            {
                lines.Add($"보호막 {status.ShieldAmount} 남음");
            }

            if (data.BlocksAllDamage)
            {
                lines.Add("모든 피해 무효");
            }

            if (data.GrantsDebuffImmunity)
            {
                lines.Add("새로 걸리는 디버프 면역");
            }

            if (data.RecordsDamageTaken)
            {
                lines.Add($"받은 피해 기록 중: {status.RecordedDamage}");
            }
        }

        // 스택 효과의 피해 줄 끝에 붙는 글자: 위 수치가 1스택 기준이고 스택 수만큼 곱해진다는 표시.
        private static string PerStack(StatusEffectData data)
        {
            return data.UsesStackCount ? $" × 스택, 최대 {data.MaxStacks}스택" : string.Empty;
        }
    }
}
