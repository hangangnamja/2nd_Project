using Game.Data;

namespace Game.Battle.Status
{
    // 지속피해 발동과 턴 종료 시 상태효과 처리를 담당한다.
    //  - 턴 종료: 지속피해 발동 → 남은 턴 1 감소 → 끝난 효과 제거 (기획서 3-8: 대상의 턴 종료 시 발동)
    //  - 격발: 지속피해만 즉시 1회 발동하고, 남은 턴은 줄이지 않는다
    // 예: 3턴짜리 화상은 대상의 턴 종료 3번에 걸쳐 피해를 준 뒤 사라진다.
    public static class StatusTurnProcessor
    {
        // unit의 턴 종료 시 상태효과를 처리한다. 결과는 전투 로그로 내보낸다.
        public static void ProcessTurnEnd(BattleUnit unit, BattleServices services)
        {
            TriggerDamageOverTime(unit, services, logPrefix: string.Empty);
            unit.Statuses.DecreaseTurnsAndRemoveExpired();
        }

        // 걸려 있는 지속피해 효과를 하나씩 1회 발동시키고, 발동한 개수를 돌려준다.
        // 중첩된 화상 3개면 3번 피해를 받는다. 턴 종료 처리와 격발이 함께 쓴다.
        // logPrefix: 로그 앞에 붙일 표시 (격발이면 "[격발] ")
        public static int TriggerDamageOverTime(BattleUnit unit, BattleServices services, string logPrefix)
        {
            int triggeredCount = 0;

            foreach (StatusInstance instance in unit.Statuses.GetAll())
            {
                // 앞선 지속피해로 이미 쓰러졌다면 남은 지속피해는 발동하지 않는다 ("0 피해" 로그 방지).
                if (unit.IsDead)
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
                    services.Rules);

                int dealt = unit.TakeDamage(damage);
                triggeredCount++;
                services.Log($"  {logPrefix}{unit.Name}: {data.DisplayName} {dealt} 피해 (HP {unit.CurrentHp}/{unit.MaxHp})");
            }

            return triggeredCount;
        }
    }
}
