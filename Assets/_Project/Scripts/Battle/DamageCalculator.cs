using System;
using Game.Data;
using UnityEngine;

namespace Game.Battle
{
    // 피해 계산 결과: 최종 피해량과 치명타 여부 (로그·연출용).
    public readonly struct DamageResult
    {
        public int Amount { get; }
        public bool IsCritical { get; }

        public DamageResult(int amount, bool isCritical)
        {
            Amount = amount;
            IsCritical = isCritical;
        }
    }

    // 피해량 계산만 담당하는 정적 클래스. 유닛 값을 바꾸지 않고 계산 결과만 돌려준다.
    // 기획서 3-5 공식:
    //   최종 피해 = 공격력 × 스킬 배율 × (1 + 피해 증가) × 치명 배율 × 방어 배율
    //             × (1 + 받는 피해 증가) × (1 - 피해 감소)
    // 각 칸(괄호) 안은 합연산, 칸끼리는 곱연산이다 (피해 감소만 칸 안에서도 곱연산).
    public static class DamageCalculator
    {
        // 내림 직전에 더하는 아주 작은 보정값 (부동소수점 오차 대응).
        private const double FloorTolerance = 0.0001d;

        // 공격 행동(스킬, 추가 공격)의 피해를 계산한다. 치명타가 적용된다.
        // random: 치명 판정용 난수, rules: 방어 상수 K 계산용 전투 규칙
        public static DamageResult CalculateAttackDamage(
            BattleUnit attacker,
            BattleUnit target,
            float skillMultiplier,
            System.Random random,
            BattleRulesData rules)
        {
            // NextDouble(): 0 이상 1 미만의 무작위 실수. 치명확률보다 작으면 치명 (예: 확률 0.8 → 80%).
            bool isCritical = random.NextDouble() < attacker.CritRate;
            double critMultiplier = isCritical ? 1d + attacker.CritDamage : 1d;

            double damage = attacker.Attack
                * skillMultiplier
                * (1d + attacker.DamageBonus)
                * critMultiplier
                * GetDefenseMultiplier(attacker.Level, target, rules)
                * GetTargetSideMultiplier(target);

            return new DamageResult(FinalizeDamage(damage), isCritical);
        }

        // 지속피해(화상 등) 1회 피해를 계산한다 (기획서 3-8).
        // 공격자 쪽은 부여 당시 공격력(스냅샷)만 반영하고, 피해 증가 버프와 치명타는 적용하지 않는다.
        // 대상 쪽 보정(받는 피해 증가, 피해 감소)은 일반 공격과 똑같이 적용한다.
        public static int CalculateDamageOverTime(
            int snapshotAttack,
            float attackRatio,
            int sourceLevel,
            BattleUnit target,
            BattleRulesData rules)
        {
            double damage = snapshotAttack
                * attackRatio
                * GetDefenseMultiplier(sourceLevel, target, rules)
                * GetTargetSideMultiplier(target);

            return FinalizeDamage(damage);
        }

        // 방어 배율 = K / (K + 방어력). K는 공격자 레벨 기준이라, 레벨이 높을수록 같은 방어력이 덜 막는다.
        // 예: 공격자 Lv1(K ≈ 89), 대상 방어력 50 → 89 / 139 ≈ 0.64 → 피해의 약 64%만 들어간다.
        private static double GetDefenseMultiplier(int attackerLevel, BattleUnit target, BattleRulesData rules)
        {
            double k = rules.GetDefenseConstant(attackerLevel);
            return k / (k + target.Defense);
        }

        // 대상 쪽 보정: 받는 피해 증가는 더해서 올리고, 피해 감소(최대 75%)는 남는 비율만큼 곱한다.
        private static double GetTargetSideMultiplier(BattleUnit target)
        {
            return (1d + target.DamageTakenIncrease) * (1d - target.DamageReduction);
        }

        // 최종 피해를 정수로 만든다: 내림, 최소 1 (기획서 3-5).
        private static int FinalizeDamage(double damage)
        {
            // Math.Floor: 소수점 아래를 버린다. 105.0이어야 할 값이 104.99999처럼 계산되는 오차를 막으려고
            // 아주 작은 값을 더한 뒤 내린다.
            int floored = (int)Math.Floor(damage + FloorTolerance);
            return Mathf.Max(1, floored);
        }
    }
}
