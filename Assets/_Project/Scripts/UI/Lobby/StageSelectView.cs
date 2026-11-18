using System.Collections.Generic;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 스테이지 선택 화면 (출격).
    //  - 하단 탭: 스테이지(기본 스테이지 지도) / 토벌(보스 카드 3장) / 탐색(재화 던전 장소 지도)
    //  - 노드·카드를 누르면 우측 상세 패널에 번호·이름·추천 레벨·웨이브 수·보상과 전투 준비 버튼
    //  - 탐색 장소는 상세 패널의 난이도 버튼으로 난이도(스테이지)를 고른다
    //  - 전투 준비: 고른 스테이지로 편성 화면을 연다 (전투 시작은 편성 화면에서)
    // 노드·카드의 모양과 위치는 씬에서 만들고, 이 컴포넌트는 스테이지 목록(StageCatalog)의 내용을 채우고 클릭을 연결한다.
    public class StageSelectView : MonoBehaviour
    {
        [Header("데이터")]
        [SerializeField] private StageCatalog catalog;

        [Tooltip("전투 준비를 누르면 열 편성 화면")]
        [SerializeField] private FormationView formation;

        [Header("탭")]
        [SerializeField] private Button normalTab;
        [SerializeField] private Button bossTab;
        [SerializeField] private Button exploreTab;
        [SerializeField] private GameObject normalMap;
        [SerializeField] private GameObject bossCards;
        [SerializeField] private GameObject exploreMap;

        [Tooltip("기본 스테이지 노드 (1-1부터 순서대로)")]
        [SerializeField] private List<Button> normalNodes = new List<Button>();

        [Tooltip("보스 카드 (보스 1부터 순서대로)")]
        [SerializeField] private List<Button> bossNodes = new List<Button>();

        [Tooltip("탐색 장소 노드 (StageCatalog의 탐색 장소 순서대로)")]
        [SerializeField] private List<Button> siteNodes = new List<Button>();

        [Header("상세 패널")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Text detailCode;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailInfo;
        [SerializeField] private Button startButton;

        [Tooltip("스테이지 설명 글 (보상 줄 아래)")]
        [SerializeField] private Text detailDescription;

        [Tooltip("\"획득 보상\" 제목 (보상이 없으면 숨김)")]
        [SerializeField] private GameObject rewardHeader;

        [Tooltip("보상 칸 원본 (꺼 둔 첫 칸, 같은 부모 아래에 복사해 줄로 늘어놓는다)")]
        [SerializeField] private StageRewardSlot rewardSlotTemplate;

        [Tooltip("탐색 장소일 때만 보이는 난이도 버튼 줄")]
        [SerializeField] private GameObject difficultyRow;

        [Tooltip("난이도 버튼 (쉬운 것부터). 장소의 난이도 수보다 많은 버튼은 숨긴다")]
        [SerializeField] private List<Button> difficultyButtons = new List<Button>();

        [Header("탭 색")]
        [SerializeField] private Color selectedTabColor = new Color(0.1f, 0.6f, 0.9f, 0.95f);
        [SerializeField] private Color normalTabColor = new Color(0f, 0f, 0f, 0.55f);

        // 하단 탭 종류.
        private enum Tab
        {
            Normal,
            Boss,
            Explore,
        }

        // 지금 상세 패널에 띄운 스테이지. 작전개시를 누르면 이 스테이지로 전투한다.
        private StageData selectedStage;

        // 지금 상세 패널에 띄운 탐색 장소 (탐색 탭이 아니면 null).
        private ExplorationSite selectedSite;

        // 만들어 둔 보상 칸 (다시 열 때 재사용, 모자라면 원본을 복사해 늘린다).
        private readonly List<StageRewardSlot> rewardSlots = new List<StageRewardSlot>();

        // 목록 내용을 노드·카드에 채우고 클릭을 연결한다. 처음에는 스테이지 탭, 상세 패널 닫힘.
        private void Awake()
        {
            // 보상 칸 원본은 복사용이라 숨겨 둔다.
            rewardSlotTemplate.gameObject.SetActive(false);

            BindNodes(normalNodes, catalog.NormalStages, index => $"1-{index + 1}", codeSize: 38);
            BindNodes(bossNodes, catalog.BossStages, index => $"BOSS {index + 1}", codeSize: 46);
            BindSites();
            BindDifficultyButtons();

            normalTab.onClick.AddListener(() => ShowTab(Tab.Normal));
            bossTab.onClick.AddListener(() => ShowTab(Tab.Boss));
            exploreTab.onClick.AddListener(() => ShowTab(Tab.Explore));
            startButton.onClick.AddListener(OpenFormation);
            ShowTab(Tab.Normal);
        }

        // 메인 화면에서 출격으로 들어올 때: 스테이지 탭부터, 상세 패널은 닫고 시작한다.
        // 편성 화면에서 뒤로 돌아올 때는 부르지 않아, 고르던 탭과 스테이지가 그대로 남는다.
        public void ResetView()
        {
            ShowTab(Tab.Normal);
        }

        // 노드 i에 목록의 i번째 스테이지를 연결한다. 스테이지가 없으면 "준비 중"으로 표시하고 누를 수 없게 한다.
        // defaultCode: 스테이지 에셋에 번호가 비어 있을 때 쓸 기본 표기 (1-3, BOSS 2 등).
        // System.Func<int, string>: 정수(순서)를 받아 문자열(표기)을 돌려주는 함수를 담는 델리게이트.
        // codeSize: 번호 글자 크기. 번호는 크고 굵게, 이름은 노드 기본 글자 크기로 아래 줄에 쓴다 (리치 텍스트 <size>, <b>).
        private void BindNodes(List<Button> nodes, IReadOnlyList<StageData> stages, System.Func<int, string> defaultCode, int codeSize)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                StageData stage = i < stages.Count ? stages[i] : null;
                string code = stage != null && !string.IsNullOrEmpty(stage.StageCode) ? stage.StageCode : defaultCode(i);

                Text label = nodes[i].GetComponentInChildren<Text>(true);
                string stageName = stage != null ? stage.StageName : "준비 중";
                label.text = $"<size={codeSize}><b>{code}</b></size>\n{stageName}";

                nodes[i].interactable = stage != null;

                // 람다식 안에서 쓸 값은 반복마다 새 지역 변수(stage, code)라, 버튼마다 자기 스테이지를 기억한다.
                nodes[i].onClick.AddListener(() => ShowDetail(stage, code));
            }
        }

        // 탐색 장소 노드 i에 i번째 장소를 연결한다: 장소 이름(크게) + 주는 재화(작게). 장소가 없으면 "준비 중"으로 막는다.
        // 노드 안의 첫 Text가 이름 칸이다 (아이콘은 씬에서 정한 그림).
        private void BindSites()
        {
            IReadOnlyList<ExplorationSite> sites = catalog.ExplorationSites;
            for (int i = 0; i < siteNodes.Count; i++)
            {
                ExplorationSite site = i < sites.Count ? sites[i] : null;
                Text label = siteNodes[i].GetComponentInChildren<Text>(true);
                label.text = site != null
                    ? $"<size=34><b>{site.SiteName}</b></size>\n{site.RewardLabel}"
                    : "<size=34><b>준비 중</b></size>";

                siteNodes[i].interactable = site != null && site.Difficulties.Count > 0;
                siteNodes[i].onClick.AddListener(() => ShowSite(site));
            }
        }

        // 난이도 버튼 i를 누르면 지금 장소의 i번째 난이도를 고른다.
        // 람다 안에서 쓰는 index는 반복마다 새 변수라 버튼마다 자기 번호를 기억한다 (for의 i를 그대로 쓰면 마지막 값이 잡힘).
        private void BindDifficultyButtons()
        {
            for (int i = 0; i < difficultyButtons.Count; i++)
            {
                int index = i;
                difficultyButtons[i].onClick.AddListener(() => SelectDifficulty(index));
            }
        }

        // 탭 전환: 지도·카드 중 고른 탭 것만 보이고, 고른 탭 버튼을 강조한다. 상세 패널은 닫는다.
        private void ShowTab(Tab tab)
        {
            normalMap.SetActive(tab == Tab.Normal);
            bossCards.SetActive(tab == Tab.Boss);
            exploreMap.SetActive(tab == Tab.Explore);
            normalTab.image.color = tab == Tab.Normal ? selectedTabColor : normalTabColor;
            bossTab.image.color = tab == Tab.Boss ? selectedTabColor : normalTabColor;
            exploreTab.image.color = tab == Tab.Explore ? selectedTabColor : normalTabColor;

            selectedStage = null;
            selectedSite = null;
            detailPanel.SetActive(false);
        }

        // 고른 스테이지의 상세 정보를 띄운다 (기본 스테이지·토벌). 난이도 버튼 줄은 숨긴다.
        private void ShowDetail(StageData stage, string code)
        {
            selectedSite = null;
            difficultyRow.SetActive(false);
            FillDetail(stage, code, stage.StageName);
        }

        // 탐색 장소를 고르면 상세 패널에 난이도 버튼을 장소의 난이도 수만큼 켜고, 첫 난이도를 고른 상태로 연다.
        private void ShowSite(ExplorationSite site)
        {
            selectedSite = site;
            difficultyRow.SetActive(true);
            for (int i = 0; i < difficultyButtons.Count; i++)
            {
                bool exists = i < site.Difficulties.Count;
                difficultyButtons[i].gameObject.SetActive(exists);
                if (exists)
                {
                    difficultyButtons[i].GetComponentInChildren<Text>(true).text = $"LV.{site.Difficulties[i].EnemyLevel}";
                }
            }

            SelectDifficulty(0);
        }

        // 지금 장소의 index번째 난이도를 고른다: 그 스테이지로 상세 내용을 바꾸고, 고른 버튼을 강조한다.
        private void SelectDifficulty(int index)
        {
            if (selectedSite == null || index >= selectedSite.Difficulties.Count)
            {
                return;
            }

            for (int i = 0; i < difficultyButtons.Count; i++)
            {
                difficultyButtons[i].image.color = i == index ? selectedTabColor : normalTabColor;
            }

            StageData stage = selectedSite.Difficulties[index];
            FillDetail(stage, selectedSite.SiteName, stage.StageName);
        }

        // 상세 패널 내용: 번호(장소 이름)·이름·추천 레벨·웨이브 수 + 보상 칸 + 설명.
        private void FillDetail(StageData stage, string code, string stageName)
        {
            selectedStage = stage;
            detailPanel.SetActive(true);

            detailCode.text = code;
            detailName.text = stageName;
            detailInfo.text = $"추천 레벨 LV.{stage.EnemyLevel}\n웨이브 {stage.Waves.Count}";
            detailDescription.text = stage.Description ?? string.Empty;
            ShowRewards(stage);
        }

        // 보상 칸: 매번 받는 보상 → 첫 클리어 보상 순으로 아이콘 칸을 늘어놓는다.
        // 첫 클리어 보상은 "첫 클리어" 꼬리표, 이미 깬 스테이지면 어둡게 덮고 "획득 완료". 보상이 하나도 없으면 제목까지 숨긴다.
        private void ShowRewards(StageData stage)
        {
            bool cleared = Game.Core.PlayerProfile.IsStageCleared(stage);
            int used = 0;
            foreach (StageReward reward in stage.ClearRewards)
            {
                BindSlot(ref used, reward, isFirstClear: false, claimed: false);
            }

            foreach (StageReward reward in stage.FirstClearRewards)
            {
                BindSlot(ref used, reward, isFirstClear: true, claimed: cleared);
            }

            // 이번에 쓰지 않은 칸은 숨긴다.
            for (int i = used; i < rewardSlots.Count; i++)
            {
                rewardSlots[i].gameObject.SetActive(false);
            }

            rewardHeader.SetActive(used > 0);
        }

        // 보상 하나를 used번째 칸에 채운다. 칸이 모자라면 원본을 복사해 하나 더 만든다.
        // ref used: 함수 안에서 늘린 칸 번호가 부른 쪽 변수에도 그대로 반영된다.
        private void BindSlot(ref int used, StageReward reward, bool isFirstClear, bool claimed)
        {
            if (reward.Material == null)
            {
                return;
            }

            if (used >= rewardSlots.Count)
            {
                rewardSlots.Add(Instantiate(rewardSlotTemplate, rewardSlotTemplate.transform.parent));
            }

            StageRewardSlot slot = rewardSlots[used];
            slot.gameObject.SetActive(true);
            slot.Bind(reward.Material, reward.Amount, isFirstClear, claimed);
            used++;
        }

        // 고른 스테이지로 편성 화면을 연다.
        private void OpenFormation()
        {
            if (selectedStage != null)
            {
                formation.Open(selectedStage);
            }
        }
    }
}
