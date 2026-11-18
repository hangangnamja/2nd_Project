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

        // 레벨 성장 곡선: StatTable.xlsx 파라미터 시트의 "S급 균형형" 값 (무기 제외). 방어력은 위 K 계산과 같은 값을 쓴다.
        [Header("레벨 성장 (S급 균형형, 무기 제외)")]
        [SerializeField, Min(1f)] private float balancedHpAtLv1 = 600f;
        [SerializeField, Min(1f)] private float balancedHpAtLv60 = 7500f;
        [SerializeField, Min(1f)] private float balancedAttackAtLv1 = 130f;
        [SerializeField, Min(1f)] private float balancedAttackAtLv60 = 800f;

        [Header("무기 성장")]
        [Tooltip("무기 Lv1 스탯 = 최대 스탯(Lv60·진급 5회) × 이 비율. 나머지는 캐릭터와 같은 모양(레벨당 + 진급 점프)으로 오른다 (StatTable 상위 무기 Lv1 80 / Lv60 500 = 0.16)")]
        [SerializeField, Range(0f, 1f)] private float weaponLv1Ratio = 0.16f;

        [Header("피해 난수")]
        [Tooltip("체크하면 공격 행동(스킬·추가 공격)의 피해에 아래 범위의 무작위 배율을 곱한다. 끄면 지금까지처럼 같은 조건이면 같은 피해 (치명 판정만 난수)")]
        [SerializeField] private bool damageVarianceEnabled = true;

        [Tooltip("무작위 배율의 최소 · 최대 (0.9 ~ 1.1 = 피해가 ±10% 사이에서 흔들림). 지속피해·회복에는 적용하지 않는다")]
        [SerializeField, Range(0.5f, 1f)] private float damageVarianceMin = 0.9f;
        [SerializeField, Range(1f, 1.5f)] private float damageVarianceMax = 1.1f;

        private const int MaxLevel = 60;

        // 공격 피해에 곱할 무작위 배율을 하나 뽑는다. 꺼져 있으면 항상 1 (피해 그대로).
        // random.NextDouble(): 0 이상 1 미만의 무작위 실수 → 최소 ~ 최대 사이로 늘려 쓴다 (전투 공용 난수라 시드를 고정하면 결과도 재현됨).
        public double RollDamageVariance(System.Random random)
        {
            if (!damageVarianceEnabled)
            {
                return 1d;
            }

            return damageVarianceMin + (damageVarianceMax - damageVarianceMin) * random.NextDouble();
        }
        private const int MaxAscension = 5;
        private const int AscensionInterval = 10;

        // 공격자 레벨에 맞는 방어 상수 K를 구한다 (기획서 3-5).
        // K = 그 레벨 균형형 방어력 × (1 - 목표 감소율) / 목표 감소율
        // → 같은 레벨 균형형 적에게 방어 배율 K / (K + 방어력)이 정확히 (1 - 목표 감소율)이 된다.
        // 예: Lv1 방어력 48, 목표 35% → K = 48 × 0.65 / 0.35 ≈ 89
        public float GetDefenseConstant(int attackerLevel)
        {
            float defense = GetBalancedStat(balancedDefenseAtLv1, balancedDefenseAtLv60, attackerLevel, GetCompletedAscension(attackerLevel));
            return defense * (1f - targetDefenseReduction) / targetDefenseReduction;
        }

        // 아군의 레벨 배율: 캐릭터 데이터의 스탯을 Lv1 값으로 보고, 균형형 성장 곡선이 커진 비율만큼 키운다.
        // 예: Lv60·진급 5회 → HP 7500 / 600 = 12.5배, 공격력 800 / 130 ≈ 6.15배, 방어력 600 / 48 = 12.5배
        // 그래서 캐릭터마다 다른 스탯(직군·등급 차이)은 레벨이 올라도 같은 비율로 유지된다.
        // promotion: 실제로 마친 진급 횟수 → 진급 버튼을 누른 순간 점프만큼 스탯이 오른다 (Lv10·진급 0회 < Lv10·진급 1회).
        public StatScaling GetAllyScaling(int level, int promotion)
        {
            int ascension = Mathf.Clamp(promotion, 0, MaxAscension);
            return new StatScaling(
                GetGrowthRatio(balancedHpAtLv1, balancedHpAtLv60, level, ascension),
                GetGrowthRatio(balancedAttackAtLv1, balancedAttackAtLv60, level, ascension),
                GetGrowthRatio(balancedDefenseAtLv1, balancedDefenseAtLv60, level, ascension));
        }

        // 진급 횟수를 모를 때(전투 씬 바로 실행한 테스트 설정, 아군 소환)의 아군 배율: 그 레벨까지 진급을 마친 것으로 본다.
        public StatScaling GetAllyScaling(int level)
        {
            return GetAllyScaling(level, GetCompletedAscension(level));
        }

        // 적의 레벨 배율: StatTable 적 스탯 시트의 역산 기준을 따른다 (기획서 6장).
        //  - 적 HP는 "아군 공격 몇 방에 죽는지"로 정하므로 아군 공격력 성장을 따라간다
        //  - 적 공격력은 "아군 HP의 몇 %를 깎는지"로 정하므로 아군 HP 성장을 따라간다
        //  - 적 방어력은 K 계산과 같은 균형형 방어력 성장을 따라간다
        // 아군과 같은 곡선을 쓰면 고레벨에서 적 HP가 공격력보다 2배 빨리 커져 전투가 지나치게 길어진다.
        // 무기 스탯 비율 (최대 스탯 대비): Lv1 = weaponLv1Ratio, Lv60·진급 5회 = 1.
        // 그 사이는 캐릭터 성장과 같은 식(레벨당 증가 + 진급 점프)으로 채운다 → 진급하면 무기도 점프한다.
        // 예: 공격력 최대 500 → Lv1 80, Lv60·진급 5회 500
        public float GetWeaponStatRatio(int level, int promotion)
        {
            // 균형형 식에 Lv1 = 0, Lv60 = 1을 넣으면 "Lv1→최대까지 몇 % 왔는지"(0~1)가 나온다.
            int ascension = Mathf.Clamp(promotion, 0, MaxAscension);
            float progress = GetBalancedStat(0f, 1f, level, ascension);
            return weaponLv1Ratio + (1f - weaponLv1Ratio) * progress;
        }

        // 적은 진급이 없으므로 그 레벨까지 진급을 마친 아군과 같은 곡선 위치를 쓴다 (예: Lv10 적 = 진급 1회 기준).
        public StatScaling GetEnemyScaling(int level)
        {
            int ascension = GetCompletedAscension(level);
            return new StatScaling(
                GetGrowthRatio(balancedAttackAtLv1, balancedAttackAtLv60, level, ascension),
                GetGrowthRatio(balancedHpAtLv1, balancedHpAtLv60, level, ascension),
                GetGrowthRatio(balancedDefenseAtLv1, balancedDefenseAtLv60, level, ascension));
        }

        // 그 레벨에서 진급을 마쳤다고 볼 때의 진급 횟수 (Lv10 → 1회, Lv60 → 5회). 정수 나눗셈이라 소수점은 버린다.
        private static int GetCompletedAscension(int level)
        {
            return Mathf.Min(MaxAscension, Mathf.Clamp(level, 1, MaxLevel) / AscensionInterval);
        }

        // Lv1 대비 그 레벨·진급 횟수의 균형형 스탯 비율. 예: 공격력 Lv1 130, Lv10·진급 1회 241.7 → 약 1.86배
        private float GetGrowthRatio(float atLv1, float atLv60, int level, int ascension)
        {
            return GetBalancedStat(atLv1, atLv60, level, ascension) / atLv1;
        }

        // 균형형 스탯 = Lv1 값 + 레벨당 증가 × (레벨 - 1) + 진급 점프 × 진급 횟수 (StatTable 성장표와 같은 식).
        private float GetBalancedStat(float atLv1, float atLv60, int level, int ascension)
        {
            // Mathf.Clamp: 레벨을 1~60 범위로 제한한다.
            int clampedLevel = Mathf.Clamp(level, 1, MaxLevel);

            // Lv60 값 = Lv1 값 + 레벨당 증가 × (59 + 진급 5회 × 점프 레벨 수) 에서 레벨당 증가를 역산한다.
            float perLevel = (atLv60 - atLv1) / (MaxLevel - 1 + MaxAscension * ascensionJumpLevels);
            float jump = perLevel * ascensionJumpLevels;

            return atLv1 + perLevel * (clampedLevel - 1) + jump * ascension;
        }
    }
}
