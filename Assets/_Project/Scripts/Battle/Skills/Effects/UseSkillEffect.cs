using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자의 다른 스킬(1~3스)을 이 자리에서 함께 사용한다 (니콜 3스 → 2스 연동).
    // 연동한 스킬은 자기 대상 규칙으로 대상을 다시 정하고 자기 효과 목록을 실행한다 → 2스를 고치면 3스에도 그대로 반영된다.
    // 연동한 스킬의 쿨타임은 돌리지 않는다 (지금 쓰는 스킬의 쿨타임만 걸림).
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("턴·행동/다른 스킬 사용", "시전자의 다른 스킬을 이 자리에서 함께 쓴다. 그 스킬의 대상 규칙·효과를 그대로 쓰고 쿨타임은 걸지 않는다", 450)]
    [Serializable]
    public class UseSkillEffect : SkillEffect
    {
        [Tooltip("함께 쓸 스킬 번호 (1 = 1스, 2 = 2스, 3 = 3스). 변신 중이면 변신 스킬 기준")]
        [Label("스킬 번호", Min = 1f, Max = 3f)]
        [SerializeField] private int skillNumber = 2;

        // 연동 스킬을 실행하는 중인지. 연동한 스킬 안에 또 연동 블록이 있으면(서로 부르는 경우 포함) 무한 반복이 되므로 막는다.
        // static: 블록 인스턴스와 상관없이 하나만 있는 값 → 어느 스킬의 연동이든 실행 중이면 다음 연동은 건너뛴다.
        private static bool isUsingLinkedSkill;

        // 연동 스킬의 대상을 정하고 효과 목록을 실행한다.
        public override void Apply(SkillContext context)
        {
            if (isUsingLinkedSkill)
            {
                context.Log($"  (연동 스킬 안의 연동은 건너뜀)");
                return;
            }

            // 스킬 번호(1~3)를 배열 칸 번호(0~2)로 바꾼다. GetSkill은 변신 중이면 변신 스킬을 돌려준다.
            SkillData skill = context.Caster.GetSkill(skillNumber - 1);

            // 이번 공격으로 상대가 모두 쓰러졌으면 상대를 노리는 연동 스킬은 칠 대상이 없으므로 건너뛴다
            // (대상 규칙의 무작위 선택은 살아 있는 상대가 있다고 가정한다).
            if (TargetsOpponents(skill.TargetType) && !HasOpponentStanding(context))
            {
                return;
            }

            List<BattleUnit> targets = context.Services.ResolveTargets(context.Caster, skill.TargetType, PickChosenTarget(context, skill.TargetType));
            if (targets.Count == 0)
            {
                return;
            }

            // 확산 스킬은 대상 목록의 첫 번째가 메인 대상이다 (BattleManager.UseSkill과 같은 규칙).
            BattleUnit primaryTarget = skill.TargetType == SkillTargetType.EnemyBlast ? targets[0] : null;
            context.Log($"  연동: {skill.SkillName}");

            // try/finally: 연동 스킬 실행 중 예외가 나더라도 finally에서 표시를 반드시 되돌려, 다음 연동이 막히지 않게 한다.
            isUsingLinkedSkill = true;
            try
            {
                // 지금 행동과 같은 출처(스킬·추가 공격)로 실행한다. 공격이 있었다면 연동 스킬의 "공격 행동" 사건이 따로 1번 나간다.
                SkillExecutor.ExecuteEffects(context.Caster, skill.Effects, targets, primaryTarget, context.Services, context.Source);
            }
            finally
            {
                isUsingLinkedSkill = false;
            }
        }

        // 상대편을 노리는 대상 규칙인지 (적 단일·광역·확산).
        private static bool TargetsOpponents(SkillTargetType type)
        {
            return type == SkillTargetType.EnemySingle || type == SkillTargetType.EnemyAll || type == SkillTargetType.EnemyBlast;
        }

        // 전장에 HP가 남은 상대가 한 명이라도 있는지.
        private static bool HasOpponentStanding(SkillContext context)
        {
            foreach (BattleUnit unit in context.Services.Units)
            {
                if (unit.IsAlly != context.Caster.IsAlly && unit.IsOnField && !unit.IsDown)
                {
                    return true;
                }
            }

            return false;
        }

        // 연동 스킬이 직접 대상을 고르는 스킬(단일·확산)일 때 넘길 대상.
        // 지금 스킬이 같은 편(적 또는 아군)을 노리고 있으면 그 대상을 이어받고, 아니면 null → 대상 규칙이 무작위로 고른다.
        // 예: 니콜 3스(아군 전체) → 2스(적 전체)는 고를 필요가 없고, 적 단일 스킬끼리 연동하면 같은 적을 이어서 노린다.
        private static BattleUnit PickChosenTarget(SkillContext context, SkillTargetType linkedType)
        {
            bool wantsOpponent = linkedType == SkillTargetType.EnemySingle || linkedType == SkillTargetType.EnemyBlast;
            bool wantsFriend = linkedType == SkillTargetType.AllySingle;

            if (context.PrimaryTarget != null && wantsOpponent && !context.PrimaryTarget.IsDown)
            {
                return context.PrimaryTarget;
            }

            foreach (BattleUnit target in context.Targets)
            {
                // IsAlly가 시전자와 다르면 상대편, 같으면 같은 편이다.
                bool isOpponent = target.IsAlly != context.Caster.IsAlly;
                if (!target.IsDown && ((wantsOpponent && isOpponent) || (wantsFriend && !isOpponent)))
                {
                    return target;
                }
            }

            return null;
        }
    }
}
