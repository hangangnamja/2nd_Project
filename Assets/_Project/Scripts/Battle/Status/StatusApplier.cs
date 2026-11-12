using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Battle.Status
{
    // 상태효과를 유닛에게 넣고 빼는 공용 처리. 결과를 로그와 사건으로 알린다.
    // 스킬로 거는 것, 강탈로 받는 것, 패시브로 얻는 것, 만료·소모·깨짐으로 지워지는 것이 모두 여기를 거쳐야
    // "새 버프 획득" / "버프 지워짐" 사건과 지연 버프 변환이 빠짐없이 한 곳에서 처리된다.
    public static class StatusApplier
    {
        // target에게 상태효과를 새로 건다. 새 버프면 "버프 획득" 사건을 알린다 (갱신은 알리지 않음).
        // 행동 제어 면역(변신 중 하이퍼캐리)인 대상에게 행동 제어 효과(기절 등)는 걸리지 않는다.
        // 디버프 면역인 대상에게 디버프도 걸리지 않는다.
        // 이때 결과의 Instance가 null이므로, 결과를 쓰는 쪽은 null을 확인해야 한다 (행동 제어 효과·디버프만 해당).
        public static StatusAddResult Apply(BattleUnit target, StatusEffectData data, BattleUnit source, BattleServices services)
        {
            if (data.IsCrowdControl && target.IsCrowdControlImmune)
            {
                services.Log($"  {target.Name}: {data.DisplayName} 면역 (변신 중 행동 제어 면역)");

                // default: 모든 칸이 비어 있는 결과 (Instance = null, 새로 걸리지 않음, 밀려난 효과 없음).
                return default;
            }

            // 디버프 면역(도발 탱커 3스) 중에는 새 디버프가 걸리지 않는다. 이미 걸린 디버프는 지우지 않는다.
            // 결과는 위와 같이 빈 결과(Instance = null)다.
            if (data.Category == StatusCategory.Debuff && target.IsDebuffImmune)
            {
                services.Log($"  {target.Name}: {data.DisplayName} 면역 (디버프 면역)");
                return default;
            }

            StatusAddResult result = target.Statuses.Add(data, source);
            ReportResult(target, result, services);
            return result;
        }

        // target에게 보호막을 준다 (기획서 3-7: 보호막은 유닛당 하나, 아이콘도 하나).
        //  - 보호막이 없으면: shieldData로 새 보호막 버프를 걸고 amount만큼 채운다 (새 버프 → "버프 획득" 사건)
        //  - 이미 있으면: 어떤 보호막이든 그 하나에 양을 더하고 남은 턴을 긴 쪽으로 맞춘다 (갱신 → "버프 획득" 사건 없음)
        // 돌려주는 값은 양이 쌓인 보호막 인스턴스다.
        public static StatusInstance GrantShield(BattleUnit target, StatusEffectData shieldData, int amount, BattleUnit source, BattleServices services)
        {
            StatusInstance existing = target.Statuses.FindShield();
            if (existing == null)
            {
                StatusAddResult result = target.Statuses.Add(shieldData, source);
                result.Instance.AddShield(amount, source);
                ReportResult(target, result, services);
                return result.Instance;
            }

            existing.AddShield(amount, source);
            existing.ExtendRemaining(shieldData.Duration, StatusContainer.TakeAppliedOrder());

            // 갱신이지만 받은 연출(버프 이펙트)은 보여준다. 새 버프가 아니므로 BuffGained(강탈 딜러 가속)는 알리지 않는다.
            services.Events.RaiseBuffApplied(target, existing);
            return existing;
        }

        // 빼앗아 온 효과를 남은 턴 그대로 target에게 넣는다 (버프 강탈).
        // 보호막을 빼앗았고 이미 보호막이 있으면 따로 칸을 차지하지 않고 기존 보호막에 양·남은 턴을 합친다 (유닛당 하나).
        public static StatusAddResult Receive(BattleUnit target, StatusInstance incoming, BattleServices services)
        {
            StatusInstance existingShield = incoming.HasShield ? target.Statuses.FindShield() : null;
            if (existingShield != null)
            {
                existingShield.AddShield(incoming.ShieldAmount, incoming.Source);
                existingShield.ExtendRemaining(incoming.RemainingTurns, StatusContainer.TakeAppliedOrder());
                services.Events.RaiseBuffApplied(target, existingShield);
                return new StatusAddResult(existingShield, isNew: false, pushedOut: null);
            }

            StatusAddResult result = target.Statuses.Receive(incoming);
            ReportResult(target, result, services);
            return result;
        }

        // 효과 여러 개가 지워졌음을 한꺼번에 알린다 (만료, 버프 소모 등).
        public static void ReportRemoved(BattleUnit holder, IReadOnlyList<StatusInstance> removed, BattleServices services)
        {
            foreach (StatusInstance instance in removed)
            {
                ReportRemoved(holder, instance, services);
            }
        }

        // 효과 하나가 지워졌을 때의 공용 처리 (사망·강탈은 여기를 거치지 않는다):
        //  1) 지연 버프면 바뀔 효과를 이번 턴의 종료 단계에 받도록 예약한다 (기획서 3-7)
        //  2) 버프면 "버프 지워짐" 사건 → 부여자의 "자신이 건 버프가 지워질 때" 패시브가 반응한다
        public static void ReportRemoved(BattleUnit holder, StatusInstance instance, BattleServices services)
        {
            StatusEffectData convertsTo = instance.Data.ConvertsTo;
            if (convertsTo != null && !holder.IsDead)
            {
                services.Log($"  {holder.Name}: {instance.Data.DisplayName} 사라짐 → 턴 종료 시 {convertsTo.DisplayName} 획득");

                // 바뀐 효과의 부여자는 지연 버프를 건 유닛 그대로다 → 이 효과가 지워질 때도 부여자의 패시브가 반응한다.
                services.Actions.EnqueueTurnEndBuff(new TurnEndBuff(holder, convertsTo, instance.Source));
            }

            if (instance.Data.Category == StatusCategory.Buff)
            {
                services.Events.RaiseBuffRemoved(holder, instance);
            }
        }

        // 피해를 보호막으로 먼저 흡수한 뒤 HP에 적용하고, 들어간 피해(보호막이 흡수하고 남은 피해)를 돌려준다.
        // 돌려주는 값·피해 숫자는 남은 HP와 상관없는 원래 피해다: HP 9000에 15000을 맞으면 피해는 15000, HP는 0에서 멈춘다.
        // 스킬 피해와 지속피해가 모두 이 함수를 거친다. 깨진 보호막은 "지워짐"으로 알린다.
        // absorbed: 보호막이 흡수한 양 (로그용)
        // damageOverTimeSource: 지속피해면 그 상태효과 (피해 숫자 색을 고르는 데만 씀). 일반 피해는 null.
        // isCritical·isDamageOverTime: 계산에는 쓰지 않고, 화면의 피해 숫자 모양을 고르도록 연출 사건에 그대로 넘긴다.
        // 기본값(= false)이 있는 매개변수라, 넘기지 않으면 일반 피해로 본다.
        public static int DealDamage(
            BattleUnit target,
            int amount,
            BattleUnit attacker,
            BattleServices services,
            out int absorbed,
            bool isCritical = false,
            bool isDamageOverTime = false,
            StatusEffectData damageOverTimeSource = null)
        {
            // 무적(보스 스테이지 버프몹, 힐러 3스의 무적 버프)은 어떤 피해도 받지 않는다. 보호막·피해 기록도 건드리지 않는다.
            if (target.IsDamageImmune)
            {
                absorbed = 0;
                services.Events.RaiseDamageTaken(new DamageTakenInfo(target, 0, 0, isCritical, isDamageOverTime, isBlocked: true, damageOverTimeSource: damageOverTimeSource));
                return 0;
            }

            // 피격 무효(보스 2): 남은 횟수가 있으면 이 피해 1번을 0으로 만들고 1회 줄인다.
            // 지속피해 1개 발동, 격발로 터지는 지속피해 1개, 광역기의 대상 1명분이 각각 1회 → 타수가 많은 지속피해 파티가 빨리 벗긴다.
            // 피해만 막는다: 이 함수는 피해만 다루므로, 같은 스킬의 화상 부여·강탈 같은 효과 블록은 그대로 들어간다.
            // 보호막·피해 기록·HP 돌파 사건도 건드리지 않는다 (맞은 피해가 없으므로).
            if (target.TryConsumeHitBlock())
            {
                absorbed = 0;
                services.Log($"  {target.Name}: 피격 무효 (남은 {target.HitBlockCharges}회)");
                services.Events.RaiseDamageTaken(new DamageTakenInfo(target, 0, 0, isCritical, isDamageOverTime, isBlocked: true, blockedLabel: "무효", damageOverTimeSource: damageOverTimeSource));
                return 0;
            }

            RecordDamageTaken(target, amount);

            var broken = new List<StatusInstance>();
            int remaining = target.Statuses.AbsorbWithShields(amount, broken);
            absorbed = amount - remaining;

            foreach (StatusInstance shield in broken)
            {
                services.Log($"  {target.Name}: {shield.Data.DisplayName} 깨짐");
                ReportRemoved(target, shield, services);
            }

            // 화면·로그에 보여줄 피해 = 보호막에 막히고 남은 원래 피해. 아래 버티기·강제 해제나 HP 부족으로
            // 실제로 깎이는 HP가 줄어들어도, 숫자는 계산된 피해 그대로 보여준다.
            int shownDamage = remaining;

            // 남은 HP 이상의 피해 = 치명적인 피해. 사망을 막는 효과가 있으면 HP에 넣기 전에 피해량을 바꾼다.
            // !target.IsDown: 이미 HP 0인 대상(같은 공격 안에서 쓰러진 대상)은 다시 막지 않는다.
            if (remaining >= target.CurrentHp && !target.IsDown)
            {
                remaining = ApplyFatalDamageRules(target, remaining, services);
            }

            // TakeDamage는 HP를 0 밑으로 내리지 않는다 → 실제로 깎인 HP(hpLost)는 남은 HP를 넘지 않는다.
            int hpBefore = target.CurrentHp;
            int hpLost = target.TakeDamage(remaining, attacker);
            if (hpLost > 0)
            {
                services.Events.RaiseHpLowered(target, attacker, hpBefore, isDamageOverTime);
            }

            // hpBefore > 0 && 지금 HP 0: 이번 피해로 쓰러진 것 → 처치 연출. 버티기(HP 1)로 막혔으면 HP가 남아 false.
            bool isLethal = hpBefore > 0 && target.CurrentHp == 0;
            services.Events.RaiseDamageTaken(new DamageTakenInfo(target, shownDamage, absorbed, isCritical, isDamageOverTime, isBlocked: false, isLethal: isLethal, hpBefore: hpBefore, damageOverTimeSource: damageOverTimeSource));
            return shownDamage;
        }

        // 받은 피해를 "피해 기록" 효과(반격 태세)에 누적한다. 보호막에 흡수된 피해도 받은 피해로 센다.
        // 기록은 피해 감소가 적용되기 전 값으로 한다: 태세의 75% 감소로 줄어든 피해를 세면 반격이 너무 약해지기 때문이다.
        // 피해 공식의 마지막 칸이 × (1 - 피해 감소율)이므로, 들어온 피해를 (1 - 피해 감소율)로 나누면 감소 전 피해가 된다.
        // 예: 감소 전 80 → 75% 감소로 20이 들어옴 → 20 / 0.25 = 80 기록
        private static void RecordDamageTaken(BattleUnit target, int amount)
        {
            int beforeReduction = (int)Math.Round(amount / (1d - target.DamageReduction));

            foreach (StatusInstance buff in target.Statuses.Buffs)
            {
                if (buff.Data.RecordsDamageTaken)
                {
                    buff.RecordDamage(beforeReduction);
                }
            }
        }

        // 치명적인 피해 처리 (기획서 4-3 하이퍼캐리). 실제로 HP에 넣을 피해량을 돌려준다.
        //  - 변신 중: 사망 대신 변신 상태 최대 HP의 일정 비율로 HP를 맞추고, 종결기 → 변신 해제를 예약한다 (강제 해제)
        //    종결기가 나가기 전에 또 치명적인 피해를 받으면 HP 1로 버틴다 (해제 전 사망 방지)
        //  - 변신 전: "치명적인 피해" 사건을 알려, 버티기 패시브가 막으면 HP 1로 버틴다
        private static int ApplyFatalDamageRules(BattleUnit target, int damage, BattleServices services)
        {
            int survivingDamage = target.CurrentHp - 1;

            if (target.IsTransformed)
            {
                TransformationState transformation = target.Transformation;
                if (transformation.TryMarkFinisherQueued())
                {
                    // (int): 소수점을 버린다. 예: 변신 최대 HP 1300 × 0.25 = 325
                    target.SetHp((int)(target.MaxHp * transformation.Data.ForcedReleaseHpRatio));
                    services.Log($"  {target.Name}: 치명상 → 변신 강제 해제 (HP {target.CurrentHp}로 버팀)");
                    QueueFinisher(target, services);
                    return 0;
                }

                return survivingDamage;
            }

            if (services.Events.RaiseFatalDamageIncoming(target))
            {
                services.Log($"  {target.Name}: 치명상을 HP 1로 버팀");
                return survivingDamage;
            }

            return damage;
        }

        // 변신 종결기를 추가 공격(임시 턴)으로 예약한다. 종결기가 끝나면 변신이 해제되고 아군이 복귀한다.
        // 턴 제한 도달(BattleManager)과 강제 해제(위)가 함께 쓴다. 중복 예약 확인은 부르는 쪽이 한다.
        public static void QueueFinisher(BattleUnit unit, BattleServices services)
        {
            SkillData finisher = unit.Transformation.Data.Finisher;
            services.Actions.EnqueueFollowUp(new FollowUpAttack(
                unit,
                finisher.SkillName,
                finisher.Effects,
                finisher.TargetType,
                preferredTarget: null,
                endsTransformation: true));
        }

        // 밀려난 효과를 로그로 남기고 "지워짐"으로 처리하며, 새로 들어간 버프면 사건을 알린다.
        private static void ReportResult(BattleUnit target, StatusAddResult result, BattleServices services)
        {
            if (result.PushedOut != null)
            {
                services.Log($"  {target.Name}: 칸이 가득 차 {result.PushedOut.Data.DisplayName}(남은 {result.PushedOut.RemainingTurns}턴) 밀려남");
                ReportRemoved(target, result.PushedOut, services);
            }

            // result.Instance?.: 행동 제어 면역 등으로 걸리지 않았으면 Instance가 null이라 연출을 건너뛴다.
            bool isBuff = result.Instance?.Data.Category == StatusCategory.Buff;
            if (isBuff)
            {
                services.Events.RaiseBuffApplied(target, result.Instance);
            }

            bool isNewBuff = result.IsNew && isBuff;
            if (isNewBuff)
            {
                services.Events.RaiseBuffGained(target, result.Instance.Data.DisplayName);
            }
        }
    }
}
