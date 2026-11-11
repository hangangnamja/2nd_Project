using System;
using System.Collections.Generic;
using Game.Battle.Status;
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

        // 패시브가 몇 번 발동했는지. 패시브의 횟수 기준(매 턴 / 자기 턴)에 맞춰 비우고, 패시브별 최대 횟수와 비교한다.
        // Dictionary<키, 값>: 키로 값을 빠르게 찾는 자료구조.
        // 키 (BattleUnit, PassiveData)는 두 값을 묶은 튜플로, 같은 캐릭터 에셋을 쓰는 유닛이 둘이어도 횟수를 따로 센다.
        private readonly Dictionary<(BattleUnit, PassiveData), int> triggerCountsThisTurn =
            new Dictionary<(BattleUnit, PassiveData), int>();

        // 전투 한 판 동안의 발동 횟수 (전투당 횟수 제한용). 턴이 바뀌어도 비우지 않는다.
        private readonly Dictionary<(BattleUnit, PassiveData), int> triggerCountsThisBattle =
            new Dictionary<(BattleUnit, PassiveData), int>();

        // 이번 자기 턴에 스킬로 공격한 유닛 (턴이 시작될 때 지우고, 공격 행동 사건 때 넣는다).
        // HashSet: 같은 값이 한 번만 들어가는 집합. "공격했는지"만 빠르게 확인한다.
        private readonly HashSet<BattleUnit> attackedThisTurn = new HashSet<BattleUnit>();

        // 돌파 반응 패시브의 타수 저장소 (유닛·패시브마다 하나, 전투 내내 유지 → 웨이브가 바뀌어도 남은 타수 유지).
        private readonly Dictionary<(BattleUnit, PassiveData), HitCounter> hitCounters =
            new Dictionary<(BattleUnit, PassiveData), HitCounter>();

        // 전투 공용 도구와 전투 유닛 전체를 받아 사건 구독을 시작한다.
        public PassiveRunner(BattleServices services, IReadOnlyList<BattleUnit> allUnits)
        {
            this.services = services;
            this.allUnits = allUnits;

            events.SkillUsed += HandleSkillUsed;
            events.AttackHit += HandleAttackHit;
            events.AttackPerformed += HandleAttackPerformed;
            events.BuffGained += HandleBuffGained;
            events.UnitKilled += HandleUnitKilled;
            events.BuffRemoved += HandleBuffRemoved;
            events.FatalDamageIncoming += HandleFatalDamageIncoming;
            events.HpLowered += HandleHpLowered;
        }

        // 전투가 끝나 더 이상 반응할 필요가 없을 때 구독을 해제한다.
        public void Dispose()
        {
            events.SkillUsed -= HandleSkillUsed;
            events.AttackHit -= HandleAttackHit;
            events.AttackPerformed -= HandleAttackPerformed;
            events.BuffGained -= HandleBuffGained;
            events.UnitKilled -= HandleUnitKilled;
            events.BuffRemoved -= HandleBuffRemoved;
            events.FatalDamageIncoming -= HandleFatalDamageIncoming;
            events.HpLowered -= HandleHpLowered;
        }

        // 적 HP가 줄어듦: 공격한 쪽과 같은 편인 "HP 돌파" 패시브 주인을 찾아, 기준선을 넘어 내려갔으면 타수를 1 쌓는다.
        // 지속피해는 공격이 아니므로 제외한다. 공격자 본인(돌파 딜러의 추가 공격)도 같은 편이라 포함된다 (기획서 3-4 예외).
        private void HandleHpLowered(BattleUnit target, BattleUnit attacker, int hpBefore, bool isDamageOverTime)
        {
            if (attacker == null || isDamageOverTime)
            {
                return;
            }

            foreach (BattleUnit owner in allUnits)
            {
                // 공격자와 같은 편이고, 맞은 유닛은 반대편이며, 주인이 전장에 있을 때만 반응한다.
                bool isRelevantOwner = owner.IsOnField && owner.IsAlly == attacker.IsAlly && target.IsAlly != owner.IsAlly;
                if (!isRelevantOwner)
                {
                    continue;
                }

                foreach (PassiveData passive in owner.Data.Passives)
                {
                    if (passive.Trigger == PassiveTrigger.OnEnemyHpThreshold && HasCrossedThreshold(target, hpBefore, passive.HpThreshold))
                    {
                        AddThresholdHit(owner, passive, target);
                    }
                }
            }
        }

        // 맞기 전 HP는 기준선 위였는데 지금은 기준선 이하인지. 기준선 = 최대 HP × 비율 (예: 10000 × 0.5 = 5000)
        // "이하인 적을 때릴 때마다"가 아니라 "넘어가는 순간"만 세야, 이미 50% 아래인 적을 때리는 추가 공격이 끝없이 늘지 않는다.
        private static bool HasCrossedThreshold(BattleUnit target, int hpBefore, float ratio)
        {
            double threshold = target.MaxHp * (double)ratio;
            return hpBefore > threshold && target.CurrentHp <= threshold;
        }

        // 돌파 1회: 타수를 1 쌓고, 이 타수를 쓸 추가 공격이 아직 없으면 하나 예약한다.
        // 이미 예약됐거나 실행 중이면 새로 예약하지 않는다 → 같은 추가 공격의 타수가 늘어난다.
        private void AddThresholdHit(BattleUnit owner, PassiveData passive, BattleUnit target)
        {
            if (!CanTrigger(owner, passive))
            {
                return;
            }

            ConsumeTrigger(owner, passive);

            var key = (owner, passive);

            // TryGetValue로 없으면 새로 만들어 넣는다 → 유닛·패시브마다 저장소가 하나만 생긴다.
            if (!hitCounters.TryGetValue(key, out HitCounter counter))
            {
                counter = new HitCounter();
                hitCounters[key] = counter;
            }

            counter.Pending++;
            log($"  [패시브] {owner.Name}: {passive.PassiveName} ({target.Name} {passive.HpThreshold:P0} 돌파) → 타수 {counter.Pending}");

            if (!counter.IsQueued)
            {
                counter.IsQueued = true;
                actions.EnqueueFollowUp(new FollowUpAttack(owner, passive, counter));
            }
        }

        // 치명적인 피해 직전: 맞는 유닛 본인의 "치명적인 피해 시" 패시브가 발동하면 사망을 막는다 (하이퍼캐리 버티기).
        // 패시브 효과(보호막 등)는 즉시 실행되고, HP 1로 버티는 처리는 피해를 넣는 StatusApplier가 한다.
        private void HandleFatalDamageIncoming(FatalDamageArgs args)
        {
            BattleUnit target = args.Target;

            foreach (PassiveData passive in target.Data.Passives)
            {
                if (passive.Trigger == PassiveTrigger.OnFatalDamage && Activate(target, passive, target))
                {
                    args.Prevent();
                }
            }
        }

        // 버프가 지워짐: 그 버프를 건 유닛(부여자)의 "자신이 건 버프가 지워질 때" 패시브를 자신에게 실행한다 (곰인형 버퍼).
        // 버프 3개가 한꺼번에 지워지면 사건이 3번 오지만, 횟수 제한(자기 턴 사이 1회)으로 1번만 발동한다.
        private void HandleBuffRemoved(BattleUnit holder, StatusInstance buff)
        {
            BattleUnit caster = buff.Source;
            if (caster == null || caster.IsDead)
            {
                return;
            }

            ActivateOwnPassives(caster, PassiveTrigger.OnOwnBuffRemoved);
        }

        // 새 버프를 얻음: 받은 유닛 본인의 "버프 획득 시" 패시브를 자신에게 실행한다 (강탈 딜러 버프 가속).
        // 버프 1개마다 사건이 1번 오므로, 버프 4개를 강탈하면 4번 발동한다. 갱신은 사건 자체가 오지 않는다.
        private void HandleBuffGained(BattleUnit receiver, string buffName)
        {
            if (receiver.IsDead)
            {
                return;
            }

            ActivateOwnPassives(receiver, PassiveTrigger.OnBuffGained);
        }

        // 사망 확정: 처치한 유닛의 "처치 시" 패시브를 자신에게 실행한다 (강탈 딜러 추격).
        // killer가 null이면(피해 없이 쓰러짐) 반응할 유닛이 없다. 처치자가 이미 쓰러졌어도 반응하지 않는다.
        private void HandleUnitKilled(BattleUnit victim, BattleUnit killer)
        {
            if (killer == null || killer.IsDead)
            {
                return;
            }

            ActivateOwnPassives(killer, PassiveTrigger.OnKill);
        }

        // owner가 가진 패시브 중 trigger와 맞는 것을 자기 자신을 대상으로 발동한다.
        private void ActivateOwnPassives(BattleUnit owner, PassiveTrigger trigger)
        {
            foreach (PassiveData passive in owner.Data.Passives)
            {
                if (passive.Trigger == trigger)
                {
                    Activate(owner, passive, owner);
                }
            }
        }

        // 정규 턴이 끝날 때 BattleManager가 부른다: 이번 턴에 공격하지 않았으면 "공격하지 않은 턴" 패시브를 발동한다 (아스트라).
        // 공연·기절로 스킵된 턴도 공격하지 않은 턴이다.
        public void HandleOwnTurnEnd(BattleUnit actor)
        {
            if (actor.IsDead || attackedThisTurn.Contains(actor))
            {
                return;
            }

            ActivateOwnPassives(actor, PassiveTrigger.OnTurnEndWithoutAttack);
        }

        // 턴 종료 단계로 예약된 패시브 효과를 실행한다 (BattleManager가 턴 종료 처리 뒤에 호출).
        // 대상은 패시브 주인 자신이고, 공격 행동이 아니므로 다른 패시브의 공격 반응을 깨우지 않는다.
        public void ExecuteTurnEndEffect(TurnEndEffect effect)
        {
            log($"  [패시브] {effect.Owner.Name}: {effect.Passive.PassiveName} (턴 종료)");
            SkillExecutor.ExecuteEffects(
                effect.Owner,
                effect.Passive.Effects,
                new List<BattleUnit> { effect.Owner },
                primaryTarget: null,
                services,
                ActionSource.PassiveEffect);
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

        // 새 턴이 시작될 때(추가 턴 포함) 패시브 발동 횟수를 초기화한다. actor = 이번 턴에 행동하는 유닛.
        //  - 매 턴 기준(AnyTurn) 패시브: 누구의 턴이든 비운다
        //  - 자기 턴 기준(OwnTurn) 패시브: 행동하는 유닛 본인의 것만 비운다
        // 추가 공격(임시 턴)은 이 함수를 부르지 않으므로 횟수가 다시 채워지지 않는다.
        public void ResetTurnCounters(BattleUnit actor)
        {
            // 새 턴(추가 턴 포함)마다 "이번 턴에 공격했는지"를 처음부터 다시 센다.
            attackedThisTurn.Remove(actor);

            var keysToClear = new List<(BattleUnit, PassiveData)>();

            // 튜플 분해 (unit, passive): 키에 묶인 두 값을 각각 이름 붙여 꺼낸다.
            foreach ((BattleUnit unit, PassiveData passive) in triggerCountsThisTurn.Keys)
            {
                bool isOwnTurnScope = passive.TurnLimitScope == PassiveTurnLimitScope.OwnTurn;
                if (!isOwnTurnScope || unit == actor)
                {
                    keysToClear.Add((unit, passive));
                }
            }

            // 순회가 끝난 뒤에 지운다 (foreach 도중 Dictionary를 바꾸면 오류가 난다).
            foreach (var key in keysToClear)
            {
                triggerCountsThisTurn.Remove(key);
            }
        }

        // 공격 판정 1회: 공격자 본인의 "공격 판정 시" 패시브를 맞은 대상에게 실행하고,
        // 같은 편의 "디버프 걸린 적 피격 시" 패시브도 확인한다.
        private void HandleAttackHit(BattleUnit attacker, BattleUnit target)
        {
            foreach (PassiveData passive in attacker.Data.Passives)
            {
                if (passive.Trigger == PassiveTrigger.OnAttackHit)
                {
                    Activate(attacker, passive, target);
                }
            }

            HandleAllyHitDebuffedEnemy(attacker, target);
        }

        // 같은 편 아군(공격자 자신 포함)의 공격이 "필요한 상태효과"가 걸린 적에게 들어갔을 때 (니콜 이자).
        // 퇴장한 아군은 반응 패시브가 멈추므로 전장에 있는 유닛만 확인한다.
        private void HandleAllyHitDebuffedEnemy(BattleUnit attacker, BattleUnit target)
        {
            foreach (BattleUnit owner in services.Units)
            {
                if (owner.IsAlly != attacker.IsAlly || !owner.IsOnField)
                {
                    continue;
                }

                foreach (PassiveData passive in owner.Data.Passives)
                {
                    // Statuses.Find(에셋): 맞은 적에게 그 상태효과가 걸려 있으면 인스턴스, 없으면 null.
                    bool targetHasStatus = passive.RequiredStatus != null && target.Statuses.Find(passive.RequiredStatus) != null;
                    if (passive.Trigger == PassiveTrigger.OnAllyHitDebuffedEnemy && targetHasStatus)
                    {
                        Activate(owner, passive, target);
                    }
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

            // 자기 스킬로 공격했음을 기록한다 → 턴이 끝날 때 "공격하지 않은 턴" 패시브가 발동하지 않는다.
            attackedThisTurn.Add(attacker);

            // 확산은 사실상 메인 대상 1인 공격이고 양옆은 여파라서, 반응 추가 공격도 메인 대상을 노린다 (기획서 4-3).
            // 메인 대상이 없는 광역 공격이거나 메인 대상의 HP가 0이 됐으면 HP 최고 → 편성 순서로 고른다.
            bool hasLivingPrimary = primaryTarget != null && !primaryTarget.IsDown;
            BattleUnit mainTarget = hasLivingPrimary ? primaryTarget : PickMainTarget(hitTargets);

            foreach (BattleUnit unit in allUnits)
            {
                // 같은 편이면서, 공격자 본인이 아니고, 전장에 있는(살아 있고 퇴장하지 않은) 유닛만 반응한다.
                bool isOtherAlly = unit.IsAlly == attacker.IsAlly && unit != attacker && unit.IsOnField;
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

        // 패시브 1회 발동: 횟수·스택·조건을 확인한 뒤, 실행 방식에 따라 즉시 실행하거나 예약한다.
        // 실제로 발동했으면 true (치명적인 피해 반응처럼 발동 여부가 결과를 바꾸는 경우에 쓴다).
        private bool Activate(BattleUnit owner, PassiveData passive, BattleUnit target)
        {
            if (!CanTrigger(owner, passive))
            {
                return false;
            }

            ConsumeTrigger(owner, passive);

            if (passive.Execution == PassiveExecution.FollowUpAttack)
            {
                actions.EnqueueFollowUp(new FollowUpAttack(owner, passive, target));
                log($"  [패시브] {owner.Name}: {passive.PassiveName} → 추가 공격 예약");
                return true;
            }

            // 턴 종료형: 지금 실행하지 않고 이번 턴의 종료 단계로 미룬다.
            // 횟수는 발동 시점에 이미 소모했으므로, 같은 턴에 여러 번 처치해도 턴당 1회 제한이 지켜진다.
            if (passive.Execution == PassiveExecution.TurnEnd)
            {
                actions.EnqueueTurnEndEffect(new TurnEndEffect(owner, passive));
                log($"  [패시브] {owner.Name}: {passive.PassiveName} → 턴 종료 시 적용 예약");
                return true;
            }

            log($"  [패시브] {owner.Name}: {passive.PassiveName}");
            SkillExecutor.ExecuteEffects(
                owner,
                passive.Effects,
                new List<BattleUnit> { target },
                primaryTarget: null,
                services,
                ActionSource.PassiveEffect);
            return true;
        }

        // 지금 발동할 수 있는지: 변신 조건, 턴당·전투당 횟수, 스택을 모두 확인한다.
        // 모든 조건을 확인한 뒤에만 소모해야, 한쪽만 깎이고 발동은 안 되는 일이 없다.
        private bool CanTrigger(BattleUnit owner, PassiveData passive)
        {
            if (passive.InactiveWhileTransformed && owner.IsTransformed)
            {
                return false;
            }

            // TryGetValue: 키가 있으면 값을 count에 담고, 없으면 count = 0이다.
            triggerCountsThisTurn.TryGetValue((owner, passive), out int count);

            bool hasTurnLimit = passive.MaxTriggersPerTurn > 0;
            if (hasTurnLimit && count >= passive.MaxTriggersPerTurn)
            {
                return false;
            }

            triggerCountsThisBattle.TryGetValue((owner, passive), out int battleCount);

            bool hasBattleLimit = passive.MaxTriggersPerBattle > 0;
            if (hasBattleLimit && battleCount >= passive.MaxTriggersPerBattle)
            {
                return false;
            }

            return !passive.UsesStacks || owner.GetPassiveStacks(passive) >= passive.StackCostPerTrigger;
        }

        // 발동 1회를 기록한다: 이번 턴·이번 전투 발동 횟수 +1, 스택형이면 스택 소모.
        private void ConsumeTrigger(BattleUnit owner, PassiveData passive)
        {
            var key = (owner, passive);
            triggerCountsThisTurn.TryGetValue(key, out int count);
            triggerCountsThisTurn[key] = count + 1;

            triggerCountsThisBattle.TryGetValue(key, out int battleCount);
            triggerCountsThisBattle[key] = battleCount + 1;

            if (passive.UsesStacks && passive.StackCostPerTrigger > 0)
            {
                owner.TryConsumePassiveStacks(passive, passive.StackCostPerTrigger);
                log($"  [패시브] {owner.Name}: {passive.PassiveName} 스택 -{passive.StackCostPerTrigger} ({owner.GetPassiveStacks(passive)}/{passive.MaxStacks})");
            }

            // 발동할 때 스택을 얻는 패시브 (니콜 이자). 최대 스택을 넘는 양은 버려진다.
            if (passive.UsesStacks && passive.StacksGainedPerTrigger > 0)
            {
                int gained = owner.AddPassiveStacks(passive, passive.StacksGainedPerTrigger);
                if (gained > 0)
                {
                    log($"  [패시브] {owner.Name}: {passive.PassiveName} 스택 +{gained} ({owner.GetPassiveStacks(passive)}/{passive.MaxStacks})");
                }
            }
        }
    }
}
