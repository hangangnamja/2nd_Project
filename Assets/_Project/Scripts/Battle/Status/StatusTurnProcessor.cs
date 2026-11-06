using System;
using Game.Data;

namespace Game.Battle.Status
{
    // 유닛의 턴이 끝날 때 상태효과를 처리한다 (기획서 3-8: 지속피해는 대상의 턴 종료 시 발동).
    // 순서: 지속피해 발동 → 남은 턴 1 감소 → 끝난 효과 제거
    // 예: 3턴짜리 화상은 대상의 턴 종료 3번에 걸쳐 피해를 준 뒤 사라진다.
    public static class StatusTurnProcessor
    {
        // unit의 턴 종료 시 상태효과를 처리한다. 결과는 log 함수로 내보낸다.
        public static void ProcessTurnEnd(BattleUnit unit, Action<string> log)
        {
            ApplyDamageOverTime(unit, log);
            unit.Statuses.DecreaseTurnsAndRemoveExpired();
        }

        // 걸려 있는 지속피해 효과를 하나씩 발동시킨다. 중첩된 화상 3개면 3번 피해를 받는다.
        private static void ApplyDamageOverTime(BattleUnit unit, Action<string> log)
        {
            foreach (StatusInstance instance in unit.Statuses.GetAll())
            {
                StatusEffectData data = instance.Data;
                if (!data.DealsDamageOverTime)
                {
                    continue;
                }

                int damage = DamageCalculator.CalculateDamageOverTime(instance.SnapshotAttack, data.DamageAttackRatio, unit);
                int dealt = unit.TakeDamage(damage);
                log($"  {unit.Name}: {data.DisplayName} {dealt} 피해 (HP {unit.CurrentHp}/{unit.MaxHp})");
            }
        }
    }
}
