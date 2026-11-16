using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Data;

namespace Game.UI
{
    // 상세 창 스킬 탭의 카드 글을 만든다 (표시 전용).
    // 효과 설명은 효과 블록마다 Describe가 자기 설정 수치로 만들므로, 수치를 바꿔도 설명 글을 따로 고칠 필요가 없다.
    public static class SkillDescriptionBuilder
    {
        // 제목 아래 보조 정보(대상·쿨타임·발동 조건) 색과 쿨타임 상태 색. 리치 텍스트 <color=#RRGGBB>에 쓴다.
        private const string InfoColor = "#8FB4D9";
        private const string ReadyColor = "#73F28C";
        private const string WaitColor = "#FF7366";

        // 스킬 카드: "1스 · 이름" + 대상·쿨타임 + 지금 쓸 수 있는지 + 효과 줄들.
        // remainingCooldown: 남은 쿨타임. 음수면(변신 전이라 아직 쓸 수 없는 변신 스킬 등) 상태 줄을 생략한다.
        public static string BuildSkill(string slotLabel, SkillData skill, int remainingCooldown)
        {
            string cooldown = skill.Cooldown > 0 ? $"쿨타임 {skill.Cooldown}" : "쿨타임 없음";
            var lines = new List<string>
            {
                $"<b>{slotLabel} · {skill.SkillName}</b>",
                $"<color={InfoColor}>{EffectText.EnumName(skill.TargetType)} · {cooldown}</color>",
            };

            if (remainingCooldown == 0)
            {
                lines.Add($"<color={ReadyColor}>사용 가능</color>");
            }
            else if (remainingCooldown > 0)
            {
                lines.Add($"<color={WaitColor}>남은 쿨타임 {remainingCooldown}</color>");
            }

            AddEffectLines(lines, skill.Effects, skill.TargetType);
            return string.Join("\n", lines);
        }

        // 패시브 카드: "패시브 · 이름" + 발동 조건·실행 시점·횟수 제한 + 스택 + 오라 보정 + 효과 줄들.
        // currentStacks: 지금 쌓인 스택 (전투 중 상세 창). 전투 밖(전투원 화면)처럼 셀 스택이 없으면 -1 → "스택 n / 최대" 줄을 생략한다.
        public static string BuildPassive(PassiveData passive, int currentStacks)
        {
            var lines = new List<string>
            {
                $"<b>패시브 · {passive.PassiveName}</b>",
                $"<color={InfoColor}>{BuildPassiveCondition(passive)}</color>",
            };

            if (passive.UsesStacks)
            {
                if (currentStacks >= 0)
                {
                    lines.Add($"스택 {currentStacks} / {passive.MaxStacks}");
                }

                AddStackRules(lines, passive);
            }

            // 오라·상시 패시브는 효과 블록 대신 "오라 스탯 보정" 칸의 보정을 건다.
            foreach (StatModifier modifier in passive.AuraModifiers)
            {
                string scope = passive.Trigger == PassiveTrigger.Aura ? "아군 전체" : "자신";
                lines.Add($"• {scope} {EffectText.Modifier(modifier)}");
            }

            // 패시브 효과의 대상은 발동 조건이 정하므로, 대상 규칙은 기본(적 단일)으로 넘긴다 (확산 배율 표시만 쓰는 값).
            AddEffectLines(lines, passive.Effects, SkillTargetType.EnemySingle);
            return string.Join("\n", lines);
        }

        // 발동 조건 한 줄. 상시(오라·시선집중·부하가 있는 동안)는 사건에 반응하지 않으므로 실행 시점을 붙이지 않는다.
        // 예: "적 처치 시 · 턴 종료 단계에 · 턴당 1회"
        private static string BuildPassiveCondition(PassiveData passive)
        {
            var parts = new List<string> { EffectText.EnumName(passive.Trigger) };

            // 발동 조건에 딸린 값: 필요한 상태효과(니콜 이자), HP 기준선(돌파 딜러).
            if (passive.Trigger == PassiveTrigger.OnAllyHitDebuffedEnemy && passive.RequiredStatus != null)
            {
                parts.Add($"필요: {passive.RequiredStatus.DisplayName}");
            }
            else if (passive.Trigger == PassiveTrigger.OnEnemyHpThreshold)
            {
                parts.Add($"기준 HP {EffectText.Percent(passive.HpThreshold)}");
            }

            bool isConstant = passive.Trigger == PassiveTrigger.Aura
                || passive.Trigger == PassiveTrigger.TauntWhileShielded
                || passive.Trigger == PassiveTrigger.WhileSummonsAlive;
            if (!isConstant)
            {
                parts.Add(EffectText.EnumName(passive.Execution));
            }

            if (passive.MaxTriggersPerTurn > 0)
            {
                string scope = passive.TurnLimitScope == PassiveTurnLimitScope.OwnTurn ? "자기 턴 사이" : "턴당";
                parts.Add($"{scope} {passive.MaxTriggersPerTurn}회");
            }

            if (passive.MaxTriggersPerBattle > 0)
            {
                parts.Add($"전투당 {passive.MaxTriggersPerBattle}회");
            }

            if (passive.InactiveWhileTransformed)
            {
                parts.Add("변신 중 꺼짐");
            }

            return string.Join(" · ", parts);
        }

        // 스택이 언제 쌓이고 언제 쓰이는지. 효과 블록이 없는 패시브(니콜 이자: 쌓기만 함)도 무엇을 하는지 보이게 한다.
        private static void AddStackRules(List<string> lines, PassiveData passive)
        {
            if (passive.StacksPerOwnAction > 0)
            {
                lines.Add($"• 자기 행동마다 스택 +{passive.StacksPerOwnAction}");
            }

            if (passive.StacksGainedPerTrigger > 0)
            {
                lines.Add($"• 발동마다 스택 +{passive.StacksGainedPerTrigger}");
            }

            if (passive.StackCostPerTrigger > 0 && passive.Effects.Count > 0)
            {
                lines.Add($"• 발동할 때 스택 {passive.StackCostPerTrigger} 소모");
            }
        }

        // 효과 블록마다 "• 설명" 한 줄씩 붙인다 (블록 순서 = 실행 순서).
        private static void AddEffectLines(List<string> lines, IReadOnlyList<SkillEffect> effects, SkillTargetType target)
        {
            foreach (SkillEffect effect in effects)
            {
                if (effect != null)
                {
                    lines.Add($"• {effect.DescribeOrName(target)}");
                }
            }
        }
    }
}
