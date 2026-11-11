using System.Collections.Generic;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 스테이지 선택 화면 (출격).
    //  - 하단 탭: 스테이지(기본 스테이지 지도) / 보스전(보스 카드 3장)
    //  - 노드·카드를 누르면 우측 상세 패널에 번호·이름·추천 레벨·웨이브 수와 전투 준비 버튼
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
        [SerializeField] private GameObject normalMap;
        [SerializeField] private GameObject bossCards;

        [Tooltip("기본 스테이지 노드 (1-1부터 순서대로)")]
        [SerializeField] private List<Button> normalNodes = new List<Button>();

        [Tooltip("보스 카드 (보스 1부터 순서대로)")]
        [SerializeField] private List<Button> bossNodes = new List<Button>();

        [Header("상세 패널")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Text detailCode;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailInfo;
        [SerializeField] private Button startButton;

        [Header("탭 색")]
        [SerializeField] private Color selectedTabColor = new Color(0.1f, 0.6f, 0.9f, 0.95f);
        [SerializeField] private Color normalTabColor = new Color(0f, 0f, 0f, 0.55f);

        // 지금 상세 패널에 띄운 스테이지. 작전개시를 누르면 이 스테이지로 전투한다.
        private StageData selectedStage;

        // 목록 내용을 노드·카드에 채우고 클릭을 연결한다. 처음에는 스테이지 탭, 상세 패널 닫힘.
        private void Awake()
        {
            BindNodes(normalNodes, catalog.NormalStages, index => $"1-{index + 1}", codeSize: 38);
            BindNodes(bossNodes, catalog.BossStages, index => $"BOSS {index + 1}", codeSize: 46);

            normalTab.onClick.AddListener(() => ShowTab(isBoss: false));
            bossTab.onClick.AddListener(() => ShowTab(isBoss: true));
            startButton.onClick.AddListener(OpenFormation);
            ShowTab(isBoss: false);
        }

        // 메인 화면에서 출격으로 들어올 때: 스테이지 탭부터, 상세 패널은 닫고 시작한다.
        // 편성 화면에서 뒤로 돌아올 때는 부르지 않아, 고르던 탭과 스테이지가 그대로 남는다.
        public void ResetView()
        {
            ShowTab(isBoss: false);
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

        // 탭 전환: 기본 스테이지 지도와 보스 카드 중 하나만 보이고, 고른 탭 버튼을 강조한다. 상세 패널은 닫는다.
        private void ShowTab(bool isBoss)
        {
            normalMap.SetActive(!isBoss);
            bossCards.SetActive(isBoss);
            normalTab.image.color = isBoss ? normalTabColor : selectedTabColor;
            bossTab.image.color = isBoss ? selectedTabColor : normalTabColor;

            selectedStage = null;
            detailPanel.SetActive(false);
        }

        // 고른 스테이지의 상세 정보를 띄운다.
        private void ShowDetail(StageData stage, string code)
        {
            selectedStage = stage;
            detailPanel.SetActive(true);

            detailCode.text = code;
            detailName.text = stage.StageName;

            string description = string.IsNullOrEmpty(stage.Description) ? string.Empty : $"\n\n{stage.Description}";
            detailInfo.text = $"추천 레벨 LV.{stage.EnemyLevel}\n웨이브 {stage.Waves.Count}{description}";
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
