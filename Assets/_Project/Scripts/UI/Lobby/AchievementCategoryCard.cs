using System;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 업적 개요 화면의 분류 카드 하나: 분류 이름(씬에 적어 둠) + 달성 수 / 전체 + 진행 막대 + 받을 보상 알림 점.
    // 누르면 그 분류의 업적 목록을 연다 (AchievementView가 콜백으로 처리).
    public class AchievementCategoryCard : MonoBehaviour
    {
        [Tooltip("이 카드가 보여줄 분류")]
        [SerializeField] private AchievementCategory category;

        [SerializeField] private Button button;
        [SerializeField] private Text countText;
        [SerializeField] private RectTransform progressFill;

        [Tooltip("받을 수 있는 보상이 있으면 켜지는 빨간 점")]
        [SerializeField] private GameObject badge;

        public AchievementCategory Category => category;

        // 눌렀을 때 화면이 할 일을 연결한다 (Action<분류>: 분류 하나를 받는 함수).
        public void Init(Action<AchievementCategory> onClick)
        {
            button.onClick.AddListener(() => onClick(category));
        }

        // 달성 수·전체 수·받을 보상 유무로 카드를 갱신한다. 업적이 하나도 없는 분류(숨은 업적)는 "- / -".
        // anchorMax.x = 달성 비율이라 채움이 왼쪽부터 그 비율만큼 늘어난다.
        public void Refresh(int completed, int total, bool hasClaimable)
        {
            countText.text = total > 0 ? $"{completed} / {total}" : "- / -";
            float ratio = total > 0 ? (float)completed / total : 0f;
            progressFill.anchorMax = new Vector2(ratio, progressFill.anchorMax.y);
            badge.SetActive(hasClaimable);
        }
    }
}
