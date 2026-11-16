using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    // 편성 화면 (기획서 2장 편성: 4인 파티, 팀 4개 저장).
    //  - 두 가지로 열린다: 메인 화면의 편성 버튼(편성 관리, 전투 시작 없음) / 스테이지 선택의 "전투 준비"(출격)
    //  - 위쪽 팀 탭 1~4: 고르면 그 팀의 편성을 불러온다. 옆 입력 칸에서 팀 이름을 바꾼다
    //  - 가운데 4칸: 칸마다 캐릭터 일러스트(없으면 직군 색 박스) + 이름·레벨. 빈 칸은 "+"
    //  - 칸을 누르면 오른쪽에 보유 캐릭터 목록 → 고르면 그 칸에 넣는다 (이미 편성된 캐릭터면 두 칸을 맞바꾼다)
    //  - 좌하단 전투 시작(출격일 때만): 지금 팀과 고른 스테이지로 Battle 씬을 연다
    // 칸·목록의 모양은 씬에서 만들고, 이 컴포넌트는 편성 상태를 들고 내용을 채운다.
    // 보유 캐릭터·레벨·편성은 세이브(PlayerProfile)에서 읽고, 편성을 바꾸면 바로 세이브에 저장한다.
    public class FormationView : MonoBehaviour
    {
        public const int PartySize = PlayerProfile.FormationSize;

        [SerializeField] private string battleSceneName = "Battle";

        [Header("화면")]
        [SerializeField] private LobbyNavigator navigator;
        [SerializeField] private Text stageText;

        [Tooltip("편성 칸 4개 (왼쪽부터). 칸 안의 Model 이미지와 Label 글자를 채운다")]
        [SerializeField] private List<Button> slots = new List<Button>();

        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        [Header("팀")]
        [Tooltip("팀 탭 4개 (1팀부터). 누르면 그 팀의 편성을 불러온다")]
        [SerializeField] private List<Button> teamTabs = new List<Button>();

        [Tooltip("지금 팀의 이름 입력 칸. 입력을 마치면(엔터·다른 곳 누름) 저장한다")]
        [SerializeField] private InputField teamNameInput;

        [SerializeField] private Color teamTabSelectedColor = new Color(0.45f, 0.8f, 1f);
        [SerializeField] private Color teamTabNormalColor = new Color(0.2f, 0.24f, 0.32f);

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

        // 지금 편성 (칸 번호 → 캐릭터, 빈 칸은 null). 세이브에서 읽어 오고, 바뀔 때마다 세이브에 저장한다.
        private readonly CharacterData[] party = new CharacterData[PartySize];

        // 보유 목록에서 만든 카드들 (캐릭터와 짝).
        private readonly List<(Button card, CharacterData character)> cards = new List<(Button, CharacterData)>();

        // 출격할 스테이지. 메인 화면에서 편성 관리로 열었으면 null (전투 시작 버튼을 숨긴다).
        private StageData stage;
        private int editingSlot = -1;
        private bool isInitialized;

        // 스테이지 선택에서 "전투 준비"를 누르면 호출된다: 고른 스테이지를 기억하고 편성 화면을 연다.
        public void Open(StageData selectedStage)
        {
            stage = selectedStage;
            string code = string.IsNullOrEmpty(stage.StageCode) ? string.Empty : $"{stage.StageCode}  ";
            ShowScreen($"출격  {code}{stage.StageName}  (추천 LV.{stage.EnemyLevel})");
        }

        // 메인 화면의 편성 버튼: 출격 없이 팀만 편성·저장한다 (전투 시작 버튼 숨김, 뒤로 가면 메인 화면).
        public void OpenForManage()
        {
            stage = null;
            ShowScreen("편성 관리  팀을 골라 편성하고 이름을 붙일 수 있습니다");
        }

        // 두 경로 공통: 안내 글을 쓰고, 지금 고른 팀을 불러와 화면을 켠다.
        private void ShowScreen(string info)
        {
            InitializeOnce();
            stageText.text = info;
            startButton.gameObject.SetActive(stage != null);
            LoadCurrentTeam();
            CloseList();
            navigator.Show(gameObject);
        }

        // 처음 열 때 한 번만: 저장된 편성과 보유 목록을 채우고, 버튼과 보유 목록 변경 알림을 연결한다.
        // (화면이 꺼진 상태로 시작하므로 Awake 대신 첫 Open에서 준비한다.)
        private void InitializeOnce()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;

            for (int i = 0; i < teamTabs.Count; i++)
            {
                int teamIndex = i;
                teamTabs[i].onClick.AddListener(() => SelectTeam(teamIndex));
            }

            // onEndEdit: 입력을 마쳤을 때(엔터, 칸 밖을 누름) 입력한 글자를 넘겨 부른다 → 그때 한 번만 저장.
            teamNameInput.onEndEdit.AddListener(RenameCurrentTeam);

            for (int i = 0; i < slots.Count; i++)
            {
                // 람다식이 기억할 칸 번호를 반복마다 새 지역 변수로 복사한다 (안 그러면 모든 칸이 마지막 i를 쓴다).
                int slotIndex = i;
                slots[i].onClick.AddListener(() => OpenList(slotIndex));
            }

            startButton.onClick.AddListener(StartBattle);

            // 뒤로: 출격 중이면 스테이지 선택으로, 편성 관리면 메인 화면으로.
            backButton.onClick.AddListener(() =>
            {
                if (stage != null)
                {
                    navigator.ShowStageSelect();
                }
                else
                {
                    navigator.ShowMain();
                }
            });
            clearSlotButton.onClick.AddListener(ClearEditingSlot);
            closeListButton.onClick.AddListener(CloseList);

            RebuildCards();

            // 이후 가챠·육성으로 보유 목록이 바뀌면 그때만 다시 만든다 (화면이 꺼져 있어도 받는다).
            PlayerProfile.OwnedCharactersChanged += HandleOwnedCharactersChanged;
        }

        // 씬을 떠나 화면이 사라질 때 static 알림 구독을 해제한다 (사라진 화면을 호출하지 않도록).
        // 구독한 적이 없어도 -= 는 아무 일도 하지 않으므로 그대로 호출해도 된다.
        private void OnDestroy()
        {
            PlayerProfile.OwnedCharactersChanged -= HandleOwnedCharactersChanged;
        }

        // 보유 캐릭터가 늘거나 레벨이 바뀌면 목록 카드와 칸의 레벨 표시를 새 값으로 다시 채운다.
        private void HandleOwnedCharactersChanged()
        {
            RebuildCards();
            RefreshSlots();
        }

        // 지금 보유한 캐릭터로 목록 카드를 새로 만든다 (이전 카드는 지운다). 누르면 지금 고르는 칸에 그 캐릭터를 넣는다.
        private void RebuildCards()
        {
            foreach ((Button card, CharacterData _) in cards)
            {
                Destroy(card.gameObject);
            }

            cards.Clear();
            foreach (OwnedCharacter owned in PlayerProfile.GetOwnedCharacters())
            {
                CharacterData character = owned.Data;
                Button card = Instantiate(cardTemplate, listContainer);
                card.gameObject.SetActive(true);
                card.name = $"Card_{character.name}";
                card.image.color = GetRoleColor(character);

                // 카드 위쪽에 일러스트의 머리 부분을 보여준다 (확대·자르기는 카드 템플릿의 IllustrationView 설정).
                // GetComponentInChildren(true): 꺼져 있는 자식까지 찾는다 → 일러스트 칸이 꺼진 상태로 복제돼도 찾을 수 있다.
                IllustrationView illust = card.GetComponentInChildren<IllustrationView>(true);
                if (illust != null)
                {
                    illust.Show(character.Illustration);
                }

                CharacterData picked = character;
                card.onClick.AddListener(() => PickCharacter(picked));
                cards.Add((card, character));
            }
        }

        // 팀 탭을 눌렀을 때: 그 팀을 고른 팀으로 저장하고 편성을 불러온다.
        private void SelectTeam(int teamIndex)
        {
            PlayerProfile.SelectTeam(teamIndex);
            LoadCurrentTeam();
            CloseList();
        }

        // 지금 고른 팀의 편성을 칸에 채우고, 탭 강조와 이름 칸을 맞춘다.
        private void LoadCurrentTeam()
        {
            List<CharacterData> saved = PlayerProfile.GetParty();
            for (int i = 0; i < PartySize; i++)
            {
                party[i] = i < saved.Count ? saved[i] : null;
            }

            RefreshTeamTabs();
            RefreshSlots();
        }

        // 고른 팀 탭만 밝은 색, 나머지는 어두운 색. 탭 글자는 팀 번호 + 이름.
        private void RefreshTeamTabs()
        {
            int current = PlayerProfile.CurrentTeam;
            for (int i = 0; i < teamTabs.Count; i++)
            {
                teamTabs[i].image.color = i == current ? teamTabSelectedColor : teamTabNormalColor;
                teamTabs[i].GetComponentInChildren<Text>(true).text = $"{i + 1}  {PlayerProfile.GetTeamName(i)}";
            }

            // SetTextWithoutNotify: 글자만 바꾸고 onEndEdit 같은 알림은 보내지 않는다 (탭 전환이 이름 저장으로 이어지지 않게).
            teamNameInput.SetTextWithoutNotify(PlayerProfile.GetTeamName(current));
        }

        // 이름 입력을 마치면 지금 팀 이름으로 저장하고 탭 글자를 고친다. 비우면 기본 이름("팀 n")으로 돌아간다.
        private void RenameCurrentTeam(string newName)
        {
            PlayerProfile.SetTeamName(PlayerProfile.CurrentTeam, newName);
            RefreshTeamTabs();
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
            PlayerProfile.SetParty(party);
            CloseList();
            RefreshSlots();
        }

        // 지금 칸을 비운다.
        private void ClearEditingSlot()
        {
            if (editingSlot >= 0)
            {
                party[editingSlot] = null;
                PlayerProfile.SetParty(party);
            }

            CloseList();
            RefreshSlots();
        }

        // 칸마다 일러스트(없으면 직군 색 박스)와 이름·레벨을 채운다. 편성이 비어 있으면 전투 시작을 막는다.
        private void RefreshSlots()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                CharacterData character = party[i];
                Image model = slots[i].transform.Find("Model").GetComponent<Image>();
                Text label = slots[i].transform.Find("Label").GetComponent<Text>();

                // 일러스트가 있으면 칸을 그림으로 채우고 색 박스는 숨긴다. 빈 칸·일러스트 없는 캐릭터는 색 박스를 그대로 쓴다.
                // ?.: 빈 칸(character = null)이면 Illustration을 읽지 않고 null → 일러스트 없음.
                IllustrationView illust = slots[i].GetComponentInChildren<IllustrationView>(true);
                bool hasIllustration = illust != null && illust.Show(character?.Illustration);
                model.gameObject.SetActive(!hasIllustration);

                model.color = character != null ? GetRoleColor(character) : emptyColor;
                label.text = character != null ? $"LV.{PlayerProfile.GetLevel(character)}  {character.DisplayName}" : "+";
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
                card.GetComponentInChildren<Text>(true).text = $"{character.DisplayName}\nLV.{PlayerProfile.GetLevel(character)}{mark}";
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

        // 지금 편성·캐릭터별 보유 레벨·스테이지로 전투 설정을 만들어 맡기고 Battle 씬을 연다.
        private void StartBattle()
        {
            List<CharacterData> selected = GetSelectedParty();
            if (selected.Count == 0 || stage == null)
            {
                return;
            }

            var levels = new List<int>();
            var promotions = new List<int>();
            var weapons = new List<WeaponStats>();
            foreach (CharacterData character in selected)
            {
                OwnedCharacter owned = PlayerProfile.GetOwned(character);
                levels.Add(owned.Level);
                promotions.Add(owned.Promotion);
                weapons.Add(PlayerProfile.GetEquippedWeaponStats(character));
            }

            BattleLaunch.Request(BattleSetup.Create(selected, levels, promotions, weapons, stage));

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
