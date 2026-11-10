using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    // 편성 화면 (스테이지 선택의 "전투 준비" 다음, 기획서 2장 편성: 4인 파티).
    //  - 가운데 4칸: 칸마다 캐릭터 모델 자리(지금은 직군 색 박스) + 이름·레벨. 빈 칸은 "+"
    //  - 칸을 누르면 오른쪽에 보유 캐릭터 목록 → 고르면 그 칸에 넣는다 (이미 편성된 캐릭터면 두 칸을 맞바꾼다)
    //  - 좌하단 전투 시작: 지금 편성과 고른 스테이지로 Battle 씬을 연다
    // 칸·목록의 모양은 씬에서 만들고, 이 컴포넌트는 편성 상태를 들고 내용을 채운다.
    public class FormationView : MonoBehaviour
    {
        public const int PartySize = 4;

        [Header("데이터")]
        [Tooltip("보유 캐릭터 목록 (세이브 시스템 전까지 임시 에셋)")]
        [SerializeField] private PlayerRoster roster;

        [SerializeField] private string battleSceneName = "Battle";

        [Header("화면")]
        [SerializeField] private LobbyNavigator navigator;
        [SerializeField] private Text stageText;

        [Tooltip("편성 칸 4개 (왼쪽부터). 칸 안의 Model 이미지와 Label 글자를 채운다")]
        [SerializeField] private List<Button> slots = new List<Button>();

        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        [Header("보유 캐릭터 목록")]
        [SerializeField] private GameObject listPanel;
        [SerializeField] private RectTransform listContainer;

        [Tooltip("목록 카드 템플릿 (씬에서는 꺼 두고 복제해서 쓴다)")]
        [SerializeField] private Button cardTemplate;

        [SerializeField] private Button clearSlotButton;
        [SerializeField] private Button closeListButton;

        [Header("직군 색 (모델 대신 임시 표시)")]
        [SerializeField] private Color attackColor = new Color(0.85f, 0.35f, 0.3f);
        [SerializeField] private Color supportColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color defenseColor = new Color(0.35f, 0.55f, 0.9f);
        [SerializeField] private Color healerColor = new Color(0.4f, 0.8f, 0.45f);
        [SerializeField] private Color emptyColor = new Color(1f, 1f, 1f, 0.1f);

        // 지금 편성 (칸 번호 → 캐릭터, 빈 칸은 null). 화면을 닫았다 열어도 유지된다.
        private readonly CharacterData[] party = new CharacterData[PartySize];

        // 보유 목록에서 만든 카드들 (캐릭터와 짝).
        private readonly List<(Button card, CharacterData character)> cards = new List<(Button, CharacterData)>();

        private StageData stage;
        private int editingSlot = -1;
        private bool isInitialized;

        // 스테이지 선택에서 "전투 준비"를 누르면 호출된다: 고른 스테이지를 기억하고 편성 화면을 연다.
        public void Open(StageData selectedStage)
        {
            InitializeOnce();
            stage = selectedStage;

            string code = string.IsNullOrEmpty(stage.StageCode) ? string.Empty : $"{stage.StageCode}  ";
            stageText.text = $"출격  {code}{stage.StageName}  (추천 LV.{stage.EnemyLevel})";

            CloseList();
            navigator.Show(gameObject);
            RefreshSlots();
        }

        // 처음 열 때 한 번만: 기본 편성을 채우고, 버튼과 보유 목록 카드를 만든다.
        // (화면이 꺼진 상태로 시작하므로 Awake 대신 첫 Open에서 준비한다.)
        private void InitializeOnce()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;

            for (int i = 0; i < PartySize && i < roster.DefaultParty.Count; i++)
            {
                party[i] = roster.DefaultParty[i];
            }

            for (int i = 0; i < slots.Count; i++)
            {
                // 람다식이 기억할 칸 번호를 반복마다 새 지역 변수로 복사한다 (안 그러면 모든 칸이 마지막 i를 쓴다).
                int slotIndex = i;
                slots[i].onClick.AddListener(() => OpenList(slotIndex));
            }

            startButton.onClick.AddListener(StartBattle);
            backButton.onClick.AddListener(() => navigator.ShowStageSelect());
            clearSlotButton.onClick.AddListener(ClearEditingSlot);
            closeListButton.onClick.AddListener(CloseList);

            CreateCards();
        }

        // 보유 캐릭터마다 카드를 하나씩 만든다. 누르면 지금 고르는 칸에 그 캐릭터를 넣는다.
        private void CreateCards()
        {
            foreach (CharacterData character in roster.OwnedCharacters)
            {
                Button card = Instantiate(cardTemplate, listContainer);
                card.gameObject.SetActive(true);
                card.name = $"Card_{character.name}";
                card.image.color = GetRoleColor(character);

                CharacterData picked = character;
                card.onClick.AddListener(() => PickCharacter(picked));
                cards.Add((card, character));
            }
        }

        // 칸 i를 바꾸려고 보유 목록을 연다.
        private void OpenList(int slotIndex)
        {
            editingSlot = slotIndex;
            listPanel.SetActive(true);
            RefreshCards();
        }

        private void CloseList()
        {
            editingSlot = -1;
            listPanel.SetActive(false);
        }

        // 고른 캐릭터를 지금 칸에 넣는다. 이미 다른 칸에 있으면 두 칸의 캐릭터를 맞바꾼다 (같은 캐릭터 중복 편성 방지).
        private void PickCharacter(CharacterData character)
        {
            if (editingSlot < 0)
            {
                return;
            }

            // System.Array.IndexOf: 편성에서 그 캐릭터가 있는 칸 번호, 없으면 -1.
            int currentSlot = System.Array.IndexOf(party, character);
            if (currentSlot >= 0)
            {
                party[currentSlot] = party[editingSlot];
            }

            party[editingSlot] = character;
            CloseList();
            RefreshSlots();
        }

        // 지금 칸을 비운다.
        private void ClearEditingSlot()
        {
            if (editingSlot >= 0)
            {
                party[editingSlot] = null;
            }

            CloseList();
            RefreshSlots();
        }

        // 칸마다 모델 자리 색과 이름·레벨을 채운다. 편성이 비어 있으면 전투 시작을 막는다.
        private void RefreshSlots()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterData character = party[i];
                Image model = slots[i].transform.Find("Model").GetComponent<Image>();
                Text label = slots[i].transform.Find("Label").GetComponent<Text>();

                model.color = character != null ? GetRoleColor(character) : emptyColor;
                label.text = character != null ? $"LV.{roster.Level}  {character.DisplayName}" : "+";
            }

            startButton.interactable = GetSelectedParty().Count > 0;
        }

        // 목록 카드에 이름·레벨과 "편성 중" 표시를 채운다.
        private void RefreshCards()
        {
            foreach ((Button card, CharacterData character) in cards)
            {
                bool inParty = System.Array.IndexOf(party, character) >= 0;
                string mark = inParty ? "\n<b>편성 중</b>" : string.Empty;
                card.GetComponentInChildren<Text>(true).text = $"{character.DisplayName}\nLV.{roster.Level}{mark}";
            }
        }

        // 빈 칸을 뺀 편성 (왼쪽 칸부터 = 편성 순서).
        private List<CharacterData> GetSelectedParty()
        {
            var selected = new List<CharacterData>();
            foreach (CharacterData character in party)
            {
                if (character != null)
                {
                    selected.Add(character);
                }
            }

            return selected;
        }

        // 지금 편성·레벨·스테이지로 전투 설정을 만들어 맡기고 Battle 씬을 연다.
        private void StartBattle()
        {
            List<CharacterData> selected = GetSelectedParty();
            if (selected.Count == 0 || stage == null)
            {
                return;
            }

            BattleLaunch.Request(BattleSetup.Create(selected, roster.Level, stage));

            // LoadScene: 지금 씬을 닫고 이름이 같은 씬을 연다. 씬이 Build Settings에 등록돼 있어야 한다.
            SceneManager.LoadScene(battleSceneName);
        }

        // 직군에 맞는 임시 모델 색. switch 식: 직군마다 색을 고르고, _는 그 외 전부.
        private Color GetRoleColor(CharacterData character) => character.Role switch
        {
            CharacterRole.Attack => attackColor,
            CharacterRole.Support => supportColor,
            CharacterRole.Defense => defenseColor,
            _ => healerColor,
        };
    }
}
