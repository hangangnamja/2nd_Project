using System.Collections.Generic;
using Game.Data;

namespace Game.Battle.Status
{
    // 지속피해·지속 회복 발동과 턴 종료 시 상태효과 처리를 담당한다.
    //  - 턴 종료: 지속피해 발동 → 지속 회복 발동 → 남은 턴 1 감소 → 끝난 효과 제거 (기획서 3-8: 대상의 턴 종료 시 발동)
    //  - 격발: 지속피해만 즉시 1회 발동하고, 남은 턴은 줄이지 않는다
    // 예: 3턴짜리 화상은 대상의 턴 종료 3번에 걸쳐 피해를 준 뒤 사라진다.
    public static class StatusTurnProcessor
    {
        // unit의 턴 종료 시 상태효과를 처리한다. 결과는 전투 로그로 내보낸다.
        // decreaseDurations: false면 지속피해만 발동하고 남은 턴은 줄이지 않는다 (하이퍼캐리 변신 중 엑스트라 턴).
        public static void ProcessTurnEnd(BattleUnit unit, BattleServices services, bool decreaseDurations = true)
        {
            TriggerDamageOverTime(unit, services, logPrefix: string.Empty);

            // 지속피해 다음에 회복한다 → 같은 턴에 지속피해로 HP가 0이 됐으면 회복으로 되살아나지 않는다 (부활 없음).
            TriggerHealOverTime(unit, services);

            if (!decreaseDurations)
            {
                return;
            }

            // 만료된 효과도 "지워짐"으로 알린다 → 지연 버프는 바뀐 효과를 이번 턴 종료 단계에 받는다.
            List<StatusInstance> expired = unit.Statuses.DecreaseTurnsAndRemoveExpired();
            StatusApplier.ReportRemoved(unit, expired, services);
        }

        // 걸려 있는 지속 회복 효과(소생)를 하나씩 1회 발동시킨다. 회복량 = 부여 당시 시전자 최대 HP × 비율.
        // HP가 0인 유닛은 회복하지 않는다 (게임에 부활 없음).
        private static void TriggerHealOverTime(BattleUnit unit, BattleServices services)
        {
            foreach (StatusInstance instance in unit.Statuses.GetAll())
            {
                if (unit.IsDown)
                {
                    return;
                }

                StatusEffectData data = instance.Data;
                if (!data.HealsOverTime)
                {
                    continue;
                }

                // (int): 소수점을 버린다. 예: 시전자 최대 HP 1,530 × 0.05 = 76
                int healed = unit.Heal((int)(instance.SnapshotMaxHp * data.HealSourceMaxHpRatio));
                services.Log($"  {unit.Name}: {data.DisplayName} {healed} 회복 (HP {unit.CurrentHp}/{unit.MaxHp})");
                services.Events.RaiseHealed(unit, healed);
            }
        }

        // 걸려 있는 지속피해 효과를 하나씩 1회 발동시키고, 발동한 개수를 돌려준다.
        // 칸 하나 = 1번 발동: 스택 방식 화상 ×5는 1번에 5스택분 피해, 칸마다 걸린 효과(중첩 방식)는 칸 수만큼. 턴 종료 처리와 격발이 함께 쓴다.
        // logPrefix: 로그 앞에 붙일 표시 (격발이면 "[격발] ")
        public static int TriggerDamageOverTime(BattleUnit unit, BattleServices services, string logPrefix)
        {
            int triggeredCount = 0;

            foreach (StatusInstance instance in unit.Statuses.GetAll())
            {
                // 앞선 지속피해로 HP가 0이 됐다면 남은 지속피해는 발동하지 않는다 ("0 피해" 로그 방지).
                if (unit.IsDown)
                {
                    break;
                }

                StatusEffectData data = instance.Data;
                if (!data.DealsDamageOverTime)
                {
                    continue;
                }

                // 부여 당시 공격력·부여자 레벨로 계산한다 (부여자가 이미 쓰러졌어도 피해는 그대로 남는다).
                int damage = DamageCalculator.CalculateDamageOverTime(
                    data,
                    instance.SnapshotAttack,
                    instance.Source,
                    unit,
                    services.Rules,
                    instance.Stacks);

                // 지속피해의 처치자는 그 효과를 건 유닛으로 본다. 보호막이 있으면 먼저 흡수한다.
                int dealt = StatusApplier.DealDamage(unit, damage, instance.Source, services, out int absorbed, isDamageOverTime: true);
                triggeredCount++;
                string shieldText = absorbed > 0 ? $" (보호막 {absorbed} 흡수)" : string.Empty;
                string stackText = data.UsesStackCount ? $" ×{instance.Stacks}" : string.Empty;
                services.Log($"  {logPrefix}{unit.Name}: {data.DisplayName}{stackText} {dealt} 피해{shieldText} (HP {unit.CurrentHp}/{unit.MaxHp})");
            }

            return triggeredCount;
        }
    }
}
