using UnityEngine;

namespace Game.Data
{
    // 전투 공식에 쓰는 공통 수치 (방어 상수 K 계산 등). 캐릭터가 아닌 "게임 규칙" 데이터라 별도 에셋으로 둔다.
    // 수치는 Docs/StatTable.xlsx 파라미터 시트와 같은 값을 쓴다.
    [CreateAssetMenu(fileName = "BattleRules", menuName = "Game/Battle Rules Data")]
    public class BattleRulesData : ScriptableObject
    {
        [Header("방어 상수 K")]
        [Tooltip("같은 레벨 적에게 방어력이 피해를 깎는 목표 비율 (0.35 = 35%)")]
        [SerializeField, Range(0.01f, 0.99f)] private float targetDefenseReduction = 0.35f;

        [Tooltip("S급 균형형 Lv1 방어력 (무기 제외)")]
        [SerializeField, Min(0f)] private float balancedDefenseAtLv1 = 48f;

        [Tooltip("S급 균형형 Lv60 방어력 (무기 제외)")]
        [SerializeField, Min(0f)] private float balancedDefenseAtLv60 = 600f;

        [Tooltip("진급 1회 점프가 레벨업 몇 회분인지")]
        [SerializeField, Min(0f)] private float ascensionJumpLevels = 5f;

        private const int MaxLevel = 60;
        private const int MaxAscension = 5;
        private const int AscensionInterval = 10;

        // 공격자 레벨에 맞는 방어 상수 K를 구한다 (기획서 3-5).
        // K = 그 레벨 균형형 방어력 × (1 - 목표 감소율) / 목표 감소율
        // → 같은 레벨 균형형 적에게 방어 배율 K / (K + 방어력)이 정확히 (1 - 목표 감소율)이 된다.
        // 예: Lv1 방어력 48, 목표 35% → K = 48 × 0.65 / 0.35 ≈ 89
        public float GetDefenseConstant(int attackerLevel)
        {
            float defense = GetBalancedDefense(attackerLevel);
            return defense * (1f - targetDefenseReduction) / targetDefenseReduction;
        }

        // 레벨별 균형형 방어력 = Lv1 값 + 레벨당 증가 × (레벨 - 1) + 진급 점프 × 진급 횟수.
        // 진급 횟수는 그 레벨에서 진급을 마친 상태로 본다 (Lv10 → 1회, Lv60 → 5회).
        private float GetBalancedDefense(int level)
        {
            // Mathf.Clamp: 레벨을 1~60 범위로 제한한다.
            int clampedLevel = Mathf.Clamp(level, 1, MaxLevel);
            int ascension = Mathf.Min(MaxAscension, clampedLevel / AscensionInterval);

            // Lv60 값 = Lv1 값 + 레벨당 증가 × (59 + 진급 5회 × 점프 레벨 수) 에서 레벨당 증가를 역산한다.
            float perLevel = (balancedDefenseAtLv60 - balancedDefenseAtLv1)
                / (MaxLevel - 1 + MaxAscension * ascensionJumpLevels);
            float jump = perLevel * ascensionJumpLevels;

            return balancedDefenseAtLv1 + perLevel * (clampedLevel - 1) + jump * ascension;
        }
    }
}
