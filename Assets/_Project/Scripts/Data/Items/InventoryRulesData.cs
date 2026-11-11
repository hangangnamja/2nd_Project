using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 인벤토리 공통 규칙: 무기 칸 수, 재료 최대 개수, 분해 보상.
    //  - 무기 인벤과 재료 인벤은 따로다: 무기는 개체 1개 = 1칸(한도 있음), 재료는 종류별 개수(칸 제한 없음, 종류당 최대 개수만)
    //  - 무기 칸이 꽉 찬 상태에서 무기가 들어오면 그 무기를 바로 분해해 무기 재료로 바꾼다 (자동 분해)
    [CreateAssetMenu(fileName = "InventoryRules", menuName = "Game/Inventory Rules")]
    public class InventoryRulesData : ScriptableObject
    {
        [Tooltip("무기 인벤토리 칸 수. 꽉 차면 새로 얻는 무기는 자동 분해된다")]
        [Label("무기 칸 수", Min = 1f)]
        [SerializeField] private int weaponCapacity = 200;

        [Tooltip("재료 한 종류를 가질 수 있는 최대 개수. 넘는 양은 버려진다")]
        [Label("재료 최대 개수", Min = 1f)]
        [SerializeField] private int materialMaxCount = 9999;

        [Header("분해")]
        [Tooltip("무기를 분해하면 받는 재료")]
        [Label("분해 재료")]
        [SerializeField] private MaterialData dismantleMaterial;

        [Label("3성 분해 보상", Min = 0f)]
        [SerializeField] private int threeStarReward = 5;

        [Label("4성 분해 보상", Min = 0f)]
        [SerializeField] private int fourStarReward = 20;

        [Label("5성 분해 보상", Min = 0f)]
        [SerializeField] private int fiveStarReward = 60;

        public int WeaponCapacity => weaponCapacity;
        public int MaterialMaxCount => materialMaxCount;
        public MaterialData DismantleMaterial => dismantleMaterial;

        // 그 등급 무기 1개를 분해하면 받는 재료 개수. switch 식: 등급 값에 맞는 칸의 값을 돌려준다.
        public int GetDismantleReward(WeaponGrade grade)
        {
            return grade switch
            {
                WeaponGrade.FiveStar => fiveStarReward,
                WeaponGrade.FourStar => fourStarReward,
                _ => threeStarReward,
            };
        }
    }
}
