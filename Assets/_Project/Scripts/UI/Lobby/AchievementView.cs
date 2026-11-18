using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 업적 화면 (메인 화면의 MISSION): 개요 → 분류 목록 두 단계 (붕괴 스타레일 업적 화면 배치 참고).
    //  - 개요: 전체 달성 수, 등급별(금·은·동) 달성 수, 분류 카드 5장, 받을 수 있는 보상 전부 받기
    //  - 목록: 왼쪽 분류 탭 + 업적 줄 (제목·설명·진행 막대·보상·상태)
    // 뒤로: 목록이면 개요로, 개요면 메인 화면으로.
    public class AchievementView : MonoBehaviour
    {
        // 목록 화면 왼쪽의 분류 탭 하나. [Serializable]: Inspector에서 칸을 채울 수 있게 한다.
        [Serializable]
        private class CategoryTab
        {
            public AchievementCategory category;
            public Button button;
            public GameObject badge;
        }

        [SerializeField] private LobbyNavigator navigator;
        [SerializeField] private Button backButton;

        [Header("개요")]
        [SerializeField] private GameObject overviewPanel;
        [SerializeField] private Text totalText;
        [SerializeField] private RectTransform totalFill;
        [SerializeField] private Text goldCountText;
        [SerializeField] private Text silverCountText;
        [SerializeField] private Text bronzeCountText;
        [SerializeField] private Text claimableText;
        [SerializeField] private Button claimAllButton;
        [SerializeField] private List<AchievementCategoryCard> cards = new List<AchievementCategoryCard>();

        [Header("목록")]
        [SerializeField] private GameObject listPanel;
        [SerializeField] private List<CategoryTab> tabs = new List<CategoryTab>();
        [SerializeField] private Text listTitle;
        [SerializeField] private Text listCountText;
        [SerializeField] private Button claimCategoryButton;
        [SerializeField] private ScrollRect listScroll;
        [Tooltip("업적 줄 원본 (꺼 둔 첫 줄, 같은 부모 아래에 복사해 쓴다)")]
        [SerializeField] private AchievementRowView rowTemplate;
        [SerializeField] private Text emptyText;

        [Header("탭 색")]
        [SerializeField] private Color selectedTabColor = new Color(0.16f, 0.55f, 0.75f, 0.96f);
        [SerializeField] private Color normalTabColor = new Color(0.04f, 0.05f, 0.07f, 0.78f);

        // 만들어 둔 업적 줄 (다시 그릴 때 재사용, 모자라면 원본을 복사해 늘린다).
        private readonly List<AchievementRowView> rows = new List<AchievementRowView>();

        // 지금 목록에 띄운 분류 (개요 화면이면 null). AchievementCategory?: 값이 없을 수도 있는 enum (nullable).
        private AchievementCategory? openCategory;

        private AchievementCatalog Catalog => PlayerProfile.Database.AchievementCatalog;

        // 버튼 연결은 한 번만. 람다 () => ...: 버튼을 누르면 실행할 동작.
        private void Awake()
        {
            backButton.onClick.AddListener(GoBack);
            claimAllButton.onClick.AddListener(() => PlayerProfile.ClaimAchievements(Catalog.Achievements));
            claimCategoryButton.onClick.AddListener(ClaimOpenCategory);

            foreach (AchievementCategoryCard card in cards)
            {
                card.Init(OpenCategory);
            }

            foreach (CategoryTab tab in tabs)
            {
                // 람다가 반복 변수 tab을 그대로 쓰면 안전하도록 지역 변수에 담아 둔다 (탭마다 자기 분류를 기억).
                AchievementCategory category = tab.category;
                tab.button.onClick.AddListener(() => OpenCategory(category));
            }
        }

        // 화면이 켜질 때마다 개요부터 보여준다. 보상을 받으면(AchievementsChanged) 지금 보이는 쪽을 다시 그린다.
        private void OnEnable()
        {
            PlayerProfile.AchievementsChanged += Redraw;
            ShowOverview();
        }

        private void OnDisable()
        {
            PlayerProfile.AchievementsChanged -= Redraw;
        }

        private void GoBack()
        {
            if (openCategory.HasValue)
            {
                ShowOverview();
            }
            else
            {
                navigator.ShowMain();
            }
        }

        private void Redraw()
        {
            if (openCategory.HasValue)
            {
                DrawList(openCategory.Value, keepScroll: true);
            }
            else
            {
                DrawOverview();
            }
        }

        // ---------- 개요 ----------

        private void ShowOverview()
        {
            openCategory = null;
            overviewPanel.SetActive(true);
            listPanel.SetActive(false);
            DrawOverview();
        }

        // 전체·등급별 달성 수와 분류 카드를 채운다. 숨은 업적도 달성하면 수에 들어간다.
        private void DrawOverview()
        {
            int completed = 0;
            int claimable = 0;
            var tierCounts = new int[3];
            foreach (AchievementData achievement in Catalog.Achievements)
            {
                if (achievement == null)
                {
                    continue;
                }

                if (PlayerProfile.IsAchievementCompleted(achievement))
                {
                    completed++;
                    // (int)tier: 동 0 / 은 1 / 금 2 → 등급별 칸에 하나씩 센다.
                    tierCounts[(int)achievement.Tier]++;
                }

                if (PlayerProfile.CanClaimAchievement(achievement))
                {
                    claimable++;
                }
            }

            int total = Catalog.Achievements.Count;
            totalText.text = $"<size=72><b>{completed}</b></size>  / {total}";
            totalFill.anchorMax = new Vector2(total > 0 ? (float)completed / total : 0f, totalFill.anchorMax.y);
            bronzeCountText.text = tierCounts[(int)AchievementTier.Bronze].ToString();
            silverCountText.text = tierCounts[(int)AchievementTier.Silver].ToString();
            goldCountText.text = tierCounts[(int)AchievementTier.Gold].ToString();

            claimableText.text = claimable > 0 ? $"받을 수 있는 보상 <b>{claimable}</b>개" : "받을 수 있는 보상이 없습니다";
            claimAllButton.interactable = claimable > 0;

            foreach (AchievementCategoryCard card in cards)
            {
                (int done, int count, bool hasClaimable) = CountCategory(card.Category);
                card.Refresh(done, count, hasClaimable);
            }
        }

        // 한 분류의 (달성 수, 전체 수, 받을 보상 있는지). 튜플 (a, b, c): 값 여러 개를 한 번에 돌려준다.
        private (int Completed, int Total, bool HasClaimable) CountCategory(AchievementCategory category)
        {
            List<AchievementData> list = Catalog.GetByCategory(category);
            int completed = 0;
            bool hasClaimable = false;
            foreach (AchievementData achievement in list)
            {
                if (PlayerProfile.IsAchievementCompleted(achievement))
                {
                    completed++;
                }

                hasClaimable |= PlayerProfile.CanClaimAchievement(achievement);
            }

            return (completed, list.Count, hasClaimable);
        }

        // ---------- 목록 ----------

        private void OpenCategory(AchievementCategory category)
        {
            openCategory = category;
            overviewPanel.SetActive(false);
            listPanel.SetActive(true);
            DrawList(category, keepScroll: false);
        }

        // 분류 탭 색·알림 점, 머리글, 업적 줄을 채운다.
        // keepScroll: 보상을 받아 다시 그릴 때는 보던 위치를 유지하고, 분류를 바꿀 때는 맨 위로.
        private void DrawList(AchievementCategory category, bool keepScroll)
        {
            foreach (CategoryTab tab in tabs)
            {
                tab.button.image.color = tab.category == category ? selectedTabColor : normalTabColor;
                tab.badge.SetActive(CountCategory(tab.category).HasClaimable);
            }

            List<AchievementData> list = SortForList(Catalog.GetByCategory(category));
            (int completed, int total, bool hasClaimable) = CountCategory(category);
            listTitle.text = GetCategoryName(category);
            listCountText.text = $"달성  <b>{completed}</b> / {total}";
            claimCategoryButton.interactable = hasClaimable;

            for (int i = 0; i < list.Count; i++)
            {
                GetRow(i).Bind(list[i], ClaimOne);
            }

            for (int i = list.Count; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(false);
            }

            emptyText.gameObject.SetActive(list.Count == 0);
            if (!keepScroll)
            {
                // verticalNormalizedPosition 1 = 맨 위.
                listScroll.verticalNormalizedPosition = 1f;
            }
        }

        // 목록 순서: 받을 수 있는 업적 → 아직 못 한 업적 → 받은 업적. 같은 묶음 안은 카탈로그 순서 그대로.
        // OrderBy 대신 묶음 번호로 직접 나눠 담아 같은 묶음 안의 원래 순서를 지킨다.
        private static List<AchievementData> SortForList(List<AchievementData> source)
        {
            var claimable = new List<AchievementData>();
            var inProgress = new List<AchievementData>();
            var claimed = new List<AchievementData>();
            foreach (AchievementData achievement in source)
            {
                if (PlayerProfile.CanClaimAchievement(achievement))
                {
                    claimable.Add(achievement);
                }
                else if (PlayerProfile.IsAchievementClaimed(achievement))
                {
                    claimed.Add(achievement);
                }
                else
                {
                    inProgress.Add(achievement);
                }
            }

            claimable.AddRange(inProgress);
            claimable.AddRange(claimed);
            return claimable;
        }

        // index번째 줄을 꺼낸다. 모자라면 원본을 복사해 같은 부모(목록 내용) 아래에 하나 더 만든다.
        private AchievementRowView GetRow(int index)
        {
            while (rows.Count <= index)
            {
                rows.Add(Instantiate(rowTemplate, rowTemplate.transform.parent));
            }

            AchievementRowView row = rows[index];
            row.gameObject.SetActive(true);
            return row;
        }

        // 줄의 수령 버튼 → 보상 받기. 받으면 AchievementsChanged로 목록이 다시 그려진다.
        private void ClaimOne(AchievementData achievement)
        {
            PlayerProfile.ClaimAchievement(achievement);
        }

        private void ClaimOpenCategory()
        {
            if (openCategory.HasValue)
            {
                PlayerProfile.ClaimAchievements(Catalog.GetByCategory(openCategory.Value));
            }
        }

        // 분류의 한국어 이름 (enum의 [InspectorName]과 같은 글).
        private static string GetCategoryName(AchievementCategory category)
        {
            switch (category)
            {
                case AchievementCategory.OperationRecord:
                    return "작전 기록";
                case AchievementCategory.CombatMastery:
                    return "전투 숙련";
                case AchievementCategory.OperatorGrowth:
                    return "전투원 육성";
                case AchievementCategory.Recruit:
                    return "모집";
                default:
                    return "숨은 업적";
            }
        }
    }
}
