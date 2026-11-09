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
        public static StatusAddResult Apply(BattleUnit target, StatusEffectData data, BattleUnit source, BattleServices services)
        {
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
            var broken = new List<StatusInstance>();
            int remaining = target.Statuses.AbsorbWithShields(amount, broken);
            absorbed = amount - remaining;

            foreach (StatusInstance shield in broken)
            {
                services.Log($"  {target.Name}: {shield.Data.DisplayName} 깨짐");
                ReportRemoved(target, shield, services);
            }

            return target.TakeDamage(remaining, attacker);
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
