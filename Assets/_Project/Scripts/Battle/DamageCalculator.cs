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

        // 공격자가 대상에게 주는 피해량을 계산한다. skillMultiplier는 스킬 배율 (평타 100% = 1.0).
        public static int CalculateDamage(BattleUnit attacker, BattleUnit target, float skillMultiplier)
        {
            float rawDamage = attacker.Data.Attack * skillMultiplier;

            // 방어력 배율 = K / (K + 방어력). 방어력이 높을수록 1보다 작아져 피해가 줄어든다.
            // 예: K 1000, 방어력 500 → 1000 / 1500 ≈ 0.667 → 피해의 약 67%만 들어간다.
            float defenseMultiplier = DefenseConstant / (DefenseConstant + target.Data.Defense);

            // Mathf.FloorToInt: 소수점 아래를 버려 정수로 만든다 (기획서: 최종 피해는 내림).
            int damage = Mathf.FloorToInt(rawDamage * defenseMultiplier);

            // 아무리 방어력이 높아도 최소 1은 들어가도록 보장한다.
            return Mathf.Max(1, damage);
        }
    }
}
