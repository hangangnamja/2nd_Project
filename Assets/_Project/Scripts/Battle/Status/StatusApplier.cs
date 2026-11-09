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
        // 이때 결과의 Instance가 null이므로, 결과를 쓰는 쪽은 null을 확인해야 한다 (행동 제어 효과만 해당).
        public static StatusAddResult Apply(BattleUnit target, StatusEffectData data, BattleUnit source, BattleServices services)
        {
            if (data.IsCrowdControl && target.IsCrowdControlImmune)
            {
                services.Log($"  {target.Name}: {data.DisplayName} 면역 (변신 중 행동 제어 면역)");

                // default: 모든 칸이 비어 있는 결과 (Instance = null, 새로 걸리지 않음, 밀려난 효과 없음).
                return default;
            }

            StatusAddResult result = target.Statuses.Add(data, source);
            ReportResult(target, result, services);
            return result;
        }

        // 빼앗아 온 효과를 남은 턴 그대로 target에게 넣는다 (버프 강탈).
        public static StatusAddResult Receive(BattleUnit target, StatusInstance incoming, BattleServices services)
        {
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

        // 피해를 보호막으로 먼저 흡수한 뒤 HP에 적용하고, HP가 실제로 줄어든 양을 돌려준다.
        // 스킬 피해와 지속피해가 모두 이 함수를 거친다. 깨진 보호막은 "지워짐"으로 알린다.
        // absorbed: 보호막이 흡수한 양 (로그용)
        public static int DealDamage(BattleUnit target, int amount, BattleUnit attacker, BattleServices services, out int absorbed)
        {
            // 무적(보스 스테이지 버프몹)은 어떤 피해도 받지 않는다. 보호막·피해 기록도 건드리지 않는다.
            if (target.IsInvulnerable)
            {
                absorbed = 0;
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

            // 남은 HP 이상의 피해 = 치명적인 피해. 사망을 막는 효과가 있으면 HP에 넣기 전에 피해량을 바꾼다.
            // !target.IsDown: 이미 HP 0인 대상(같은 공격 안에서 쓰러진 대상)은 다시 막지 않는다.
            if (remaining >= target.CurrentHp && !target.IsDown)
            {
                remaining = ApplyFatalDamageRules(target, remaining, services);
            }

            return target.TakeDamage(remaining, attacker);
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

            bool isNewBuff = result.IsNew && result.Instance.Data.Category == StatusCategory.Buff;
            if (isNewBuff)
            {
                services.Events.RaiseBuffGained(target, result.Instance.Data.DisplayName);
            }
        }
    }
}
