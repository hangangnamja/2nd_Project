using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Battle
{
    // 전투 사건(BattleEvents)을 구독하고, 사건에 맞는 캐릭터 패시브를 실행하거나 대기열에 예약한다.
    //  - 즉시형(Immediate): 사건이 일어난 그 자리에서 효과 실행 (공격 판정에 묻는 화상)
    //  - 추가 공격형(FollowUpAttack): 지금 행동이 끝난 뒤 실행되도록 ActionQueue에 등록
    // IDisposable: "다 쓰고 나면 Dispose()로 정리해야 하는 객체"라는 표시. 여기서는 사건 구독 해제가 정리 작업이다.
    public class PassiveRunner : IDisposable
    {
        private readonly BattleServices services;
        private readonly IReadOnlyList<BattleUnit> allUnits;

        private BattleEvents events => services.Events;
        private ActionQueue actions => services.Actions;
        private Action<string> log => services.Log;

        // 이번 턴에 패시브가 몇 번 발동했는지. 턴마다 비우고, 패시브별 최대 횟수와 비교한다.
        // Dictionary<키, 값>: 키로 값을 빠르게 찾는 자료구조.
        // 키 (BattleUnit, PassiveData)는 두 값을 묶은 튜플로, 같은 캐릭터 에셋을 쓰는 유닛이 둘이어도 횟수를 따로 센다.
        private readonly Dictionary<(BattleUnit, PassiveData), int> triggerCountsThisTurn =
            new Dictionary<(BattleUnit, PassiveData), int>();

        // 전투 공용 도구와 전투 유닛 전체를 받아 사건 구독을 시작한다.
        public PassiveRunner(BattleServices services, IReadOnlyList<BattleUnit> allUnits)
        {
            this.services = services;
            this.allUnits = allUnits;

            events.SkillUsed += HandleSkillUsed;
            events.AttackHit += HandleAttackHit;
            events.AttackPerformed += HandleAttackPerformed;
        }

        // 전투가 끝나 더 이상 반응할 필요가 없을 때 구독을 해제한다.
        public void Dispose()
        {
            events.SkillUsed -= HandleSkillUsed;
            events.AttackHit -= HandleAttackHit;
            events.AttackPerformed -= HandleAttackPerformed;
        }

        // 자기 행동(스킬)을 마쳤을 때: 행동마다 스택이 쌓이는 패시브에 스택을 더한다.
        private void HandleSkillUsed(BattleUnit caster)
        {
            foreach (PassiveData passive in caster.Data.Passives)
            {
                if (!passive.UsesStacks || passive.StacksPerOwnAction <= 0)
                {
                    continue;
                }

                int gained = caster.AddPassiveStacks(passive, passive.StacksPerOwnAction);
                if (gained > 0)
                {
                    log($"  [패시브] {caster.Name}: {passive.PassiveName} 스택 +{gained} ({caster.GetPassiveStacks(passive)}/{passive.MaxStacks})");
                }
            }
        }

        // 새 턴이 시작될 때(추가 턴 포함) 패시브 발동 횟수를 초기화한다.
        public void ResetTurnCounters()
        {
            triggerCountsThisTurn.Clear();
        }

        // 공격 판정 1회: 공격자 본인의 "공격 판정 시" 패시브를 맞은 대상에게 실행한다.
        private void HandleAttackHit(BattleUnit attacker, BattleUnit target)
        {
            foreach (PassiveData passive in attacker.Data.Passives)
            {
                if (passive.Trigger == PassiveTrigger.OnAttackHit)
                {
                    Activate(attacker, passive, target);
                }
            }
        }

        // 공격 행동 1회가 끝남: 공격자를 제외한 같은 편 아군의 "아군 공격 시" 패시브를 실행한다.
        // 정규 행동(스킬)에만 반응하고, 추가 공격에는 반응하지 않는다 (기획서 3-4).
        // 추가 공격이 또 다른 추가 공격을 부르는 연쇄를 없애, 반응 횟수가 아군 스킬 횟수에 비례하게 만든다.
        private void HandleAttackPerformed(
            BattleUnit attacker,
            IReadOnlyList<BattleUnit> hitTargets,
            BattleUnit primaryTarget,
            ActionSource source)
        {
            if (source != ActionSource.Skill)
            {
                return;
            }

            // 확산은 사실상 메인 대상 1인 공격이고 양옆은 여파라서, 반응 추가 공격도 메인 대상을 노린다 (기획서 4-3).
            // 메인 대상이 없는 광역 공격이거나 메인 대상의 HP가 0이 됐으면 HP 최고 → 편성 순서로 고른다.
            bool hasLivingPrimary = primaryTarget != null && !primaryTarget.IsDown;
            BattleUnit mainTarget = hasLivingPrimary ? primaryTarget : PickMainTarget(hitTargets);

            foreach (BattleUnit unit in allUnits)
            {
                // 같은 편이면서, 공격자 본인이 아니고, 살아 있는 유닛만 반응한다.
                bool isOtherAlly = unit.IsAlly == attacker.IsAlly && unit != attacker && !unit.IsDead;
                if (!isOtherAlly)
                {
                    continue;
                }

                foreach (PassiveData passive in unit.Data.Passives)
                {
                    if (passive.Trigger == PassiveTrigger.OnAllyAttack)
                    {
                        Activate(unit, passive, mainTarget);
                    }
                }
            }
        }

        // 광역 공격에 반응할 때 노릴 대상: 맞은 대상 중 HP가 남은 적에서 현재 HP 최고 → 같으면 편성 순서 (기획서 4-3).
        // 맞은 순서는 스킬마다 다르므로(확산은 메인 → 왼쪽 → 오른쪽) 동률은 편성 번호로 직접 비교한다.
        // 모두 HP 0이면 첫 대상을 넘기고, 실행 시점에 BattleManager가 살아 있는 적으로 다시 고른다.
        // 보스 우선 규칙은 보스 개념이 들어올 때 추가한다.
        private static BattleUnit PickMainTarget(IReadOnlyList<BattleUnit> hitTargets)
        {
            BattleUnit best = null;

            foreach (BattleUnit target in hitTargets)
            {
                // 이번 공격으로 HP가 0이 된 적은 공격이 끝나면 쓰러지므로 노리지 않는다 (기획서 3-9).
                if (target.IsDown)
                {
                    continue;
                }

                if (best == null || IsBetterTarget(target, best))
                {
                    best = target;
                }
            }

            return best ?? hitTargets[0];
        }

        // candidate가 current보다 우선인지: 현재 HP가 더 높거나, 같으면 편성 번호가 더 앞일 때.
        private static bool IsBetterTarget(BattleUnit candidate, BattleUnit current)
        {
            if (candidate.CurrentHp != current.CurrentHp)
            {
                return candidate.CurrentHp > current.CurrentHp;
            }

            return candidate.FormationIndex < current.FormationIndex;
        }

        // 패시브 1회 발동: 턴당 횟수와 스택을 확인한 뒤, 실행 방식에 따라 즉시 실행하거나 추가 공격으로 예약한다.
        private void Activate(BattleUnit owner, PassiveData passive, BattleUnit target)
        {
            if (!CanTrigger(owner, passive))
            {
                return;
            }

            ConsumeTrigger(owner, passive);

            if (passive.Execution == PassiveExecution.FollowUpAttack)
            {
                actions.EnqueueFollowUp(new FollowUpAttack(owner, passive, target));
                log($"  [패시브] {owner.Name}: {passive.PassiveName} → 추가 공격 예약");
                return;
            }

            log($"  [패시브] {owner.Name}: {passive.PassiveName}");
            SkillExecutor.ExecuteEffects(
                owner,
                passive.Effects,
                new List<BattleUnit> { target },
                primaryTarget: null,
                services,
                ActionSource.PassiveEffect);
        }

        // 지금 발동할 수 있는지: 이번 턴 발동 횟수가 남아 있고, 스택형이면 소모할 스택이 충분해야 한다.
        // 두 조건을 모두 확인한 뒤에만 소모해야, 한쪽만 깎이고 발동은 안 되는 일이 없다.
        private bool CanTrigger(BattleUnit owner, PassiveData passive)
        {
            // TryGetValue: 키가 있으면 값을 count에 담고, 없으면 count = 0이다.
            triggerCountsThisTurn.TryGetValue((owner, passive), out int count);

            bool hasTurnLimit = passive.MaxTriggersPerTurn > 0;
            if (hasTurnLimit && count >= passive.MaxTriggersPerTurn)
            {
                return false;
            }

            return !passive.UsesStacks || owner.GetPassiveStacks(passive) >= passive.StackCostPerTrigger;
        }

        // 발동 1회를 기록한다: 이번 턴 발동 횟수 +1, 스택형이면 스택 소모.
        private void ConsumeTrigger(BattleUnit owner, PassiveData passive)
        {
            var key = (owner, passive);
            triggerCountsThisTurn.TryGetValue(key, out int count);
            triggerCountsThisTurn[key] = count + 1;

            if (passive.UsesStacks)
            {
                owner.TryConsumePassiveStacks(passive, passive.StackCostPerTrigger);
                log($"  [패시브] {owner.Name}: {passive.PassiveName} 스택 -{passive.StackCostPerTrigger} ({owner.GetPassiveStacks(passive)}/{passive.MaxStacks})");
            }
        }
    }
}
