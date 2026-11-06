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

            events.AttackHit += HandleAttackHit;
            events.AttackPerformed += HandleAttackPerformed;
        }

        // 전투가 끝나 더 이상 반응할 필요가 없을 때 구독을 해제한다.
        public void Dispose()
        {
            events.AttackHit -= HandleAttackHit;
            events.AttackPerformed -= HandleAttackPerformed;
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
        private void HandleAttackPerformed(BattleUnit attacker, IReadOnlyList<BattleUnit> hitTargets, ActionSource source)
        {
            if (source != ActionSource.Skill)
            {
                return;
            }

            BattleUnit mainTarget = hitTargets[0];

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

        // 패시브 1회 발동: 횟수 제한을 확인한 뒤, 실행 방식에 따라 즉시 실행하거나 추가 공격으로 예약한다.
        private void Activate(BattleUnit owner, PassiveData passive, BattleUnit target)
        {
            if (!TryConsumeTrigger(owner, passive))
            {
                return;
            }

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
                services,
                ActionSource.PassiveEffect);
        }

        // 이번 턴 발동 횟수가 최대치보다 적으면 1 늘리고 true, 이미 다 썼으면 false.
        private bool TryConsumeTrigger(BattleUnit owner, PassiveData passive)
        {
            var key = (owner, passive);

            // TryGetValue: 키가 있으면 값을 count에 담고 true, 없으면 count = 0이고 false.
            triggerCountsThisTurn.TryGetValue(key, out int count);

            bool hasLimit = passive.MaxTriggersPerTurn > 0;
            if (hasLimit && count >= passive.MaxTriggersPerTurn)
            {
                return false;
            }

            triggerCountsThisTurn[key] = count + 1;
            return true;
        }
    }
}
