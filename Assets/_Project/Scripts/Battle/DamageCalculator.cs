using UnityEngine;

namespace Game.Battle
{
    // 피해량 계산만 담당하는 정적 클래스. 값을 바꾸지 않고 계산 결과만 돌려준다.
    // 지금은 "공격력 × 스킬 배율 × 방어력 배율"만 구현하고,
    // 피해 증가·치명타·피해 감소는 GameDesign.md 3-5 공식에 맞춰 이후 단계에서 추가한다.
    public static class DamageCalculator
    {
        // 방어 상수 K (임시값). 기획서의 레벨 기반 K 공식은 레벨 시스템을 넣을 때 교체한다.
        private const float DefenseConstant = 1000f;

        // 내림 직전에 더하는 아주 작은 보정값 (부동소수점 오차 대응).
        private const float FloorTolerance = 0.0001f;

        // 공격자가 대상에게 주는 피해량을 계산한다. skillMultiplier는 스킬 배율 (평타 100% = 1.0).
        public static int CalculateDamage(BattleUnit attacker, BattleUnit target, float skillMultiplier)
        {
            // attacker.Attack: 버프·디버프가 반영된 최종 공격력
            return ApplyDefense(attacker.Attack * skillMultiplier, target);
        }

        // 지속피해(화상 등) 1회 피해량을 계산한다.
        // 부여 당시 시전자 공격력(스냅샷) × 배율에 방어력을 적용하고, 치명타는 없다 (기획서 3-8).
        public static int CalculateDamageOverTime(int snapshotAttack, float attackRatio, BattleUnit target)
        {
            return ApplyDefense(snapshotAttack * attackRatio, target);
        }

        // 방어력 배율을 적용해 최종 피해를 만든다 (일반 공격과 지속피해 공용).
        private static int ApplyDefense(float rawDamage, BattleUnit target)
        {
            // 방어력 배율 = K / (K + 방어력). 방어력이 높을수록 1보다 작아져 피해가 줄어든다.
            // 예: K 1000, 방어력 500 → 1000 / 1500 ≈ 0.667 → 피해의 약 67%만 들어간다.
            // target.Defense: 버프·디버프가 반영된 대상의 최종 방어력 (지속피해에도 똑같이 적용)
            float defenseMultiplier = DefenseConstant / (DefenseConstant + target.Defense);

            // Mathf.FloorToInt: 소수점 아래를 버려 정수로 만든다 (기획서: 최종 피해는 내림).
            // float 계산은 105.0이어야 할 값이 104.99999처럼 아주 조금 작게 나올 수 있어, 내림 전에 아주 작은 값을 더해 보정한다.
            // 예: 156 × 0.7 ÷ 1.04 = 105 → float로는 104.99999 → 보정 없이 내리면 104가 되는 문제 방지
            int damage = Mathf.FloorToInt(rawDamage * defenseMultiplier + FloorTolerance);

            // 아무리 방어력이 높아도 최소 1은 들어가도록 보장한다.
            return Mathf.Max(1, damage);
        }
    }
}
