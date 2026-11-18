using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 업적 전체 목록. 목록 순서 = 업적 화면에 보이는 순서 (분류마다 이 순서를 그대로 따른다).
    [CreateAssetMenu(fileName = "AchievementCatalog", menuName = "Game/Achievement Catalog")]
    public class AchievementCatalog : ScriptableObject
    {
        [SerializeField] private List<AchievementData> achievements = new List<AchievementData>();

        public IReadOnlyList<AchievementData> Achievements => achievements;

        // 한 분류의 업적만 목록 순서대로 모은다 (분류 탭·카드용).
        public List<AchievementData> GetByCategory(AchievementCategory category)
        {
            // FindAll(람다): 조건이 참인 원소만 모은 새 목록. 빈 칸(null)은 빼고 분류가 같은 업적만.
            return achievements.FindAll(achievement => achievement != null && achievement.Category == category);
        }
    }
}
