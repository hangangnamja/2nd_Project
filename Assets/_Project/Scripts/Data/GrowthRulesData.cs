using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 경험치 재료 한 종류와 1개당 경험치 (육성 창의 재료 칸 하나).
    [Serializable]
    public class ExpMaterialEntry
    {
        [Label("재료")]
        [SerializeField] private MaterialData material;

        [Label("1개당 경험치", Min = 1f)]
        [SerializeField] private int experience = 300;

        public MaterialData Material => material;
        public int Experience => experience;
    }

    // 경험치를 더한 결과 (육성 창 미리 보기·실제 레벨업이 같은 계산을 쓴다).
    // readonly struct: 값 묶음이라 만든 뒤 바뀌지 않는다.
    public readonly struct ExpResult
    {
        public int Level { get; }

        // 지금 레벨에서 다음 레벨까지 쌓인 경험치
        public int Exp { get; }

        // 레벨 상한에 막혀 버려진 경험치 (기획: 초과분은 무시)
        public int Wasted { get; }

        public ExpResult(int level, int exp, int wasted)
        {
            Level = level;
            Exp = exp;
            Wasted = wasted;
        }
    }

    // 캐릭터 성장 규칙 (기획서 5장): 레벨별 필요 경험치, 진급 구간, 경험치 재료.
    // 수치는 StatTable.xlsx의 "경험치" 시트가 기준이다 (Lv1 → 60 = 900,000 = 고급 재료 300개).
    [CreateAssetMenu(fileName = "GrowthRules", menuName = "Game/Growth Rules")]
    public class GrowthRulesData : ScriptableObject
    {
        [Tooltip("레벨마다 다음 레벨까지 필요한 경험치. 0번 = Lv1→2, 마지막 = Lv59→60. 개수 + 1이 최대 레벨")]
        [SerializeField] private List<int> expToNextLevel = new List<int>();

        [Tooltip("진급 1회마다 늘어나는 레벨 상한 (10이면 10·20·30·40·50에서 진급)")]
        [Label("진급 간격 (레벨)", Min = 1f)]
        [SerializeField] private int levelsPerPromotion = 10;

        [Label("최대 진급 횟수", Min = 0f)]
        [SerializeField] private int maxPromotion = 5;

        [Tooltip("육성 창 재료 칸 (왼쪽부터 = 작은 재료부터)")]
        [SerializeField] private List<ExpMaterialEntry> expMaterials = new List<ExpMaterialEntry>();

        [Tooltip("체크하면 재료를 갖고 있지 않아도 쓸 수 있고 줄지도 않는다 (재화 던전이 생기기 전 테스트용)")]
        [Label("디버그: 재료 무한")]
        [SerializeField] private bool infiniteMaterials = true;

        public int MaxLevel => expToNextLevel.Count + 1;
        public int MaxPromotion => maxPromotion;
        public IReadOnlyList<ExpMaterialEntry> ExpMaterials => expMaterials;
        public bool InfiniteMaterials => infiniteMaterials;

        // 그 레벨에서 다음 레벨까지 필요한 경험치. 최대 레벨이면 0.
        public int GetExpToNext(int level)
        {
            int index = level - 1;
            return index >= 0 && index < expToNextLevel.Count ? expToNextLevel[index] : 0;
        }

        // 진급 횟수에 따른 레벨 상한. 예: 진급 0회 = 10, 2회 = 30, 5회 = 60.
        // Mathf.Min: 계산한 상한이 최대 레벨을 넘지 않게 한다.
        public int GetLevelCap(int promotion)
        {
            return Mathf.Min(MaxLevel, levelsPerPromotion * (promotion + 1));
        }

        // 그 레벨이 되려면 최소 몇 번 진급했어야 하는지. 예: Lv10 = 0회, Lv11 = 1회, Lv60 = 5회.
        // 진급 기능 전에 Lv60으로 지급된 캐릭터를 맞춰 줄 때 쓴다. (level - 1) / 간격: 정수 나눗셈이라 소수점은 버린다.
        public int GetMinPromotion(int level)
        {
            return Mathf.Clamp((level - 1) / levelsPerPromotion, 0, maxPromotion);
        }

        // 레벨 상한에 도달했고 아직 진급할 수 있으면 true → 레벨업 대신 진급을 띄운다.
        public bool CanPromote(int level, int promotion)
        {
            return promotion < maxPromotion && level >= GetLevelCap(promotion) && level < MaxLevel;
        }

        // 지금 상태(level, exp)에 경험치 amount를 더한 결과. 레벨 상한(levelCap)에서 멈추고, 넘친 양은 Wasted로 버린다.
        public ExpResult AddExp(int level, int exp, int levelCap, int amount)
        {
            exp += amount;
            while (level < levelCap)
            {
                int need = GetExpToNext(level);
                if (need <= 0 || exp < need)
                {
                    break;
                }

                exp -= need;
                level++;
            }

            // 상한에 닿았으면 남은 경험치는 다음 진급 후로 넘기지 않고 버린다.
            int wasted = level >= levelCap ? exp : 0;
            return new ExpResult(level, level >= levelCap ? 0 : exp, wasted);
        }

        // 지금 상태에서 targetLevel까지 남은 경험치 (일괄 추가가 재료를 얼마나 채울지 계산할 때 쓴다).
        public int GetExpToReach(int level, int exp, int targetLevel)
        {
            int total = -exp;
            for (int lv = level; lv < targetLevel; lv++)
            {
                total += GetExpToNext(lv);
            }

            return Mathf.Max(0, total);
        }
    }
}
