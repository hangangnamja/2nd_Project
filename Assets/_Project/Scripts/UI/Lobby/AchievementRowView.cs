using System;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 업적 목록의 줄 하나: 등급 트로피 + 제목·설명 + 진행 막대(현재 / 목표) + 보상 칸 + 상태(미완료 / 수령 버튼 / 완료).
    // 원본 줄을 복사해 재사용하고, 보상 받기는 화면(AchievementView)에 알려 처리하게 한다.
    public class AchievementRowView : MonoBehaviour
    {
        [SerializeField] private Image trophy;
        [SerializeField] private Text title;
        [SerializeField] private Text description;

        [Tooltip("진행 막대의 채움 (오른쪽 앵커를 진행 비율로 바꿔 늘인다)")]
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private Text progressText;

        [SerializeField] private Image rewardFrame;
        [SerializeField] private Image rewardIcon;
        [SerializeField] private Text rewardCount;

        [SerializeField] private Button claimButton;
        [Tooltip("보상까지 받은 업적에 보이는 \"완료\" 표시")]
        [SerializeField] private GameObject doneMark;
        [Tooltip("아직 달성하지 못한 업적에 보이는 \"미완료\" 글자")]
        [SerializeField] private GameObject incompleteMark;

        [Tooltip("보상까지 받은 줄을 살짝 어둡게 하는 판")]
        [SerializeField] private GameObject claimedCover;

        private AchievementData achievement;

        // 수령 버튼을 눌렀을 때 화면에 알리는 콜백 (Action<업적>: 업적 하나를 받는 함수를 담는 델리게이트).
        private Action<AchievementData> onClaim;

        // 버튼은 한 번만 연결하고, 다시 쓸 때는 Bind가 담아 둔 업적만 바꾼다.
        private void Awake()
        {
            // ?.Invoke: 콜백이 연결돼 있을 때만 부른다.
            claimButton.onClick.AddListener(() => onClaim?.Invoke(achievement));
        }

        // 업적 하나의 내용·진행 상태로 줄을 채운다.
        public void Bind(AchievementData data, Action<AchievementData> claimCallback)
        {
            achievement = data;
            onClaim = claimCallback;

            int target = data.Target;
            int progress = PlayerProfile.GetAchievementProgress(data);
            bool completed = progress >= target;
            bool claimed = PlayerProfile.IsAchievementClaimed(data);

            trophy.color = AchievementTierColors.Get(data.Tier);
            BindTexts(data, completed);
            BindProgress(Mathf.Min(progress, target), target);
            BindReward(data);

            // 상태는 셋 중 하나만: 미달성 → "미완료", 달성·미수령 → 수령 버튼, 수령함 → "완료".
            incompleteMark.SetActive(!completed);
            claimButton.gameObject.SetActive(completed && !claimed);
            doneMark.SetActive(claimed);
            claimedCover.SetActive(claimed);
        }

        // 숨은 업적은 달성하기 전까지 제목·조건을 가린다.
        private void BindTexts(AchievementData data, bool completed)
        {
            bool masked = data.IsHidden && !completed;
            title.text = masked ? "???" : data.Title;
            description.text = masked ? "숨겨진 업적입니다" : data.Describe();
        }

        // 진행 막대: anchorMax.x = 진행 비율(0~1)이라 채움이 막대 왼쪽부터 그 비율만큼만 늘어난다.
        // {N0}: 천 단위 쉼표 (1500 → 1,500).
        private void BindProgress(int shown, int target)
        {
            float ratio = (float)shown / target;
            progressFill.anchorMax = new Vector2(ratio, progressFill.anchorMax.y);
            progressText.text = $"{shown:N0} / {target:N0}";
        }

        // 보상 칸: 등급 색 테두리 + 재료 아이콘(재료 색으로 물들임) + 개수. 스테이지 보상 칸과 같은 모양.
        private void BindReward(AchievementData data)
        {
            MaterialData material = data.RewardMaterial;
            rewardFrame.gameObject.SetActive(material != null);
            if (material == null)
            {
                return;
            }

            rewardFrame.color = RarityColors.Get(material.Grade);
            rewardIcon.sprite = material.Icon;
            rewardIcon.color = material.IconColor;
            rewardCount.text = $"×{data.RewardAmount:N0}";
        }
    }

    // 업적 등급(동·은·금) 색. 목록 줄의 트로피와 개요의 트로피 수 표시가 같은 색을 쓴다.
    public static class AchievementTierColors
    {
        public static readonly Color Bronze = new Color(0.82f, 0.53f, 0.33f);
        public static readonly Color Silver = new Color(0.78f, 0.83f, 0.9f);
        public static readonly Color Gold = new Color(1f, 0.8f, 0.32f);

        public static Color Get(AchievementTier tier)
        {
            switch (tier)
            {
                case AchievementTier.Gold:
                    return Gold;
                case AchievementTier.Silver:
                    return Silver;
                default:
                    return Bronze;
            }
        }
    }
}
