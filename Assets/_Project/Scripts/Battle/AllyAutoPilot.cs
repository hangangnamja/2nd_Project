using System.Collections.Generic;
using Game.Data;

namespace Game.Battle
{
    // 자동 전투에서 아군 한 명의 행동(스킬 칸 + 대상)을 정한다 (기획서 3-9, 임시 규칙).
    //  - 스킬: 쓸 수 있는 스킬 중 가장 높은 칸부터 (3스 → 2스 → 1스). 쿨타임·경계 제한은 CanUseSkill이 거른다
    //    · 예외: 부활 스킬(쓰러진 아군도 고르는 스킬)은 쓰러진 아군이 있거나 HP 50% 미만인 아군이 있을 때만 쓴다 (아끼기)
    //  - 대상:
    //    · 적 단일·확산 = 보스 → 무적이 아닌 적 중 지금 HP가 가장 낮은 적 (막타로 수를 줄임)
    //    · 아군 단일 = HP 비율이 가장 낮은 아군
    //    · 부활 = 쓰러진 아군 우선, 없으면 HP 비율이 가장 낮은 아군
    //    · 광역·자신·아군 전체 = 대상 없음(null) → 대상 규칙대로
    // 정하기만 하고 실행은 BattleManager가 플레이어 입력과 같은 경로(ConfirmAction)로 한다.
    public static class AllyAutoPilot
    {
        // 부활 스킬을 아끼다가 쓰는 기준: 이 비율 미만인 아군이 있으면 쓴다.
        private const float ReviveHpThreshold = 0.5f;

        // 행동을 정한다. 쓸 수 있는 스킬이 하나도 없으면 slot = -1.
        public static (int Slot, BattleUnit Target) Decide(BattleUnit actor, IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> enemies)
        {
            for (int slot = CharacterData.SkillCount - 1; slot >= 0; slot--)
            {
                if (!actor.CanUseSkill(slot))
                {
                    continue;
                }

                SkillData skill = actor.GetSkill(slot);

                // out: 함수가 정한 대상을 target 변수에 담아 돌려준다. false면 이 스킬은 지금 쓰지 않는다.
                if (TryPickTarget(skill.TargetType, allies, enemies, out BattleUnit target))
                {
                    return (slot, target);
                }
            }

            return (-1, null);
        }

        // 스킬 대상 규칙에 맞는 대상을 고른다. 쓸 가치가 없으면 false.
        private static bool TryPickTarget(SkillTargetType targetType, IReadOnlyList<BattleUnit> allies, IReadOnlyList<BattleUnit> enemies, out BattleUnit target)
        {
            target = null;
            switch (targetType)
            {
                case SkillTargetType.EnemySingle:
                case SkillTargetType.EnemyBlast:
                    target = PickEnemy(enemies);
                    return true;
                case SkillTargetType.AllySingle:
                    target = PickLowestHpAlly(allies);
                    return target != null;
                case SkillTargetType.AllySingleOrFallen:
                    return TryPickReviveTarget(allies, out target);
                default:
                    return true;
            }
        }

        // 적 하나: 보스가 있으면 보스, 없으면 무적이 아닌 적 중 지금 HP가 가장 낮은 적. 고를 적이 없으면 null(대상 규칙대로).
        private static BattleUnit PickEnemy(IReadOnlyList<BattleUnit> enemies)
        {
            BattleUnit best = null;
            foreach (BattleUnit enemy in enemies)
            {
                if (!enemy.IsOnField || enemy.IsDown || enemy.IsInvulnerable)
                {
                    continue;
                }

                // 패턴 매칭 is: 적 데이터가 EnemyCharacterData이고 보스면 바로 고른다.
                if (enemy.Data is EnemyCharacterData data && data.IsBoss)
                {
                    return enemy;
                }

                if (best == null || enemy.CurrentHp < best.CurrentHp)
                {
                    best = enemy;
                }
            }

            return best;
        }

        // 부활 스킬 대상: 쓰러진(퇴장하지 않은) 아군이 있으면 그 아군, 없으면 HP 비율이 기준 미만인 아군 중 가장 낮은 아군.
        private static bool TryPickReviveTarget(IReadOnlyList<BattleUnit> allies, out BattleUnit target)
        {
            foreach (BattleUnit ally in allies)
            {
                if (ally.IsDead && !ally.IsExited)
                {
                    target = ally;
                    return true;
                }
            }

            target = PickLowestHpAlly(allies);
            return target != null && HpRatio(target) < ReviveHpThreshold;
        }

        // 전장에 있는 아군 중 HP 비율(지금 HP ÷ 최대 HP)이 가장 낮은 아군.
        private static BattleUnit PickLowestHpAlly(IReadOnlyList<BattleUnit> allies)
        {
            BattleUnit lowest = null;
            foreach (BattleUnit ally in allies)
            {
                if (ally.IsOnField && (lowest == null || HpRatio(ally) < HpRatio(lowest)))
                {
                    lowest = ally;
                }
            }

            return lowest;
        }

        private static float HpRatio(BattleUnit unit)
        {
            return unit.MaxHp > 0 ? unit.CurrentHp / (float)unit.MaxHp : 0f;
        }
    }
}
