using Game.Core;
using UnityEngine;

namespace Game.UI
{
    // 메인 화면 업적 버튼의 빨간 알림 점: 받을 수 있는 업적 보상이 있으면 켠다.
    // 메인 화면은 다른 화면에서 돌아올 때마다 다시 켜지므로 켜질 때(OnEnable) 확인하고, 켜져 있는 동안 보상을 받으면 다시 확인한다.
    public class AchievementBadge : MonoBehaviour
    {
        [SerializeField] private GameObject dot;

        private void OnEnable()
        {
            PlayerProfile.AchievementsChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerProfile.AchievementsChanged -= Refresh;
        }

        private void Refresh()
        {
            dot.SetActive(PlayerProfile.CountClaimableAchievements() > 0);
        }
    }
}
