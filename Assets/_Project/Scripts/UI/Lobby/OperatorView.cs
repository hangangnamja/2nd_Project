using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투원 화면 (메인 화면의 OPERATOR 버튼, 기획서 2장 전투원 화면). 에픽세븐 영웅 화면 배치를 참고했다.
    //  - 오른쪽: 보유 캐릭터 세로 목록 → 누르면 그 캐릭터를 보여준다
    //  - 가운데: 일러스트(없으면 직군 색 실루엣), 옆에 무기 칸 1개 → 누르면 무기 장착 창(WeaponSelectPopup)
    //  - 왼쪽: 탭(상세 / 스킬) + 정보 패널 (직군·이름·등급·레벨·진급 + 스탯, 또는 1~3스·패시브 설명)
    //  - 아래: 레벨업 버튼 → 육성 창(GrowthPopup). 레벨 상한(10·20·…·50)이면 같은 버튼이 "진급"이 된다
    // 화면 모양은 씬에서 만들고, 이 컴포넌트는 세이브(PlayerProfile)의 보유 캐릭터로 내용을 채운다.
    public class OperatorView : MonoBehaviour
    {
        // 탭 순서 = tabButtons·pages 배열 순서.
        private enum Tab { Detail, Skills }

        [Tooltip("레벨 성장 곡선 (전투와 같은 배율로 스탯을 보여주려고 쓴다)")]
        [SerializeField] private BattleRulesData battleRules;

        [Header("보유 캐릭터 목록")]
        [SerializeField] private RectTransform listContainer;

        [Tooltip("목록 칸 원본 (씬에서는 꺼 두고 복제해서 쓴다)")]
        [SerializeField] private OperatorListItem itemTemplate;

        [Header("가운데")]
        [SerializeField] private IllustrationView illustration;

        [Tooltip("일러스트가 없을 때 보여줄 실루엣 (직군 색으로 물든다)")]
        [SerializeField] private Image silhouette;

        [SerializeField] private Text weaponText;

        [Tooltip("무기 칸 (누르면 무기 장착 창)")]
        [SerializeField] private Button weaponSlotButton;

        [SerializeField] private Image weaponFrame;
        [SerializeField] private Image weaponIcon;

        [Tooltip("무기를 안 꼈을 때 보이는 + 표시")]
        [SerializeField] private GameObject weaponEmptyMark;

        [SerializeField] private WeaponSelectPopup weaponPopup;

        [Tooltip("무기로 더해진 스탯 색 (스탯 옆 +n)")]
        [SerializeField] private Color weaponBonusColor = new Color(0.45f, 0.95f, 0.55f);

        [Header("탭")]
        [Tooltip("탭 버튼 (상세, 스킬 순서)")]
        [SerializeField] private Button[] tabButtons;

        [Tooltip("탭마다 보여줄 내용 묶음 (탭 버튼과 같은 순서)")]
        [SerializeField] private GameObject[] pages;

        [SerializeField] private Color tabSelectedColor = new Color(0.12f, 0.3f, 0.42f, 0.95f);
        [SerializeField] private Color tabNormalColor = new Color(0.04f, 0.05f, 0.07f, 0.78f);

        [Header("정보 패널")]
        [SerializeField] private Text roleText;
        [SerializeField] private Text nameText;
        [SerializeField] private Text gradeText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text promotionText;
        [SerializeField] private Text statLabelsText;
        [SerializeField] private Text statValuesText;

        [Header("육성")]
        [SerializeField] private GrowthPopup growthPopup;
        [SerializeField] private Button levelButton;
        [SerializeField] private Text levelButtonText;

        [Header("스킬 탭")]
        [SerializeField] private RectTransform skillList;

        [Tooltip("스킬·패시브 카드 원본 (꺼 둔 채로 둔다)")]
        [SerializeField] private DetailEntryView skillEntryTemplate;

        [SerializeField] private Color skillCardColor = new Color(0.08f, 0.11f, 0.17f, 0.95f);
        [SerializeField] private Color passiveCardColor = new Color(0.16f, 0.1f, 0.24f, 0.95f);
        [SerializeField] private Color transformCardColor = new Color(0.26f, 0.13f, 0.08f, 0.95f);

        [Header("직군 색 (일러스트 대신 임시 표시)")]
        [SerializeField] private Color attackColor = new Color(0.85f, 0.35f, 0.3f);
        [SerializeField] private Color supportColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color defenseColor = new Color(0.35f, 0.55f, 0.9f);
        [SerializeField] private Color healerColor = new Color(0.4f, 0.8f, 0.45f);

        // 목록 칸과 그 칸의 보유 캐릭터 (같은 순서).
        private readonly List<(OperatorListItem item, OwnedCharacter owned)> items = new List<(OperatorListItem, OwnedCharacter)>();
        private readonly List<DetailEntryView> skillEntries = new List<DetailEntryView>();

        private CharacterData selected;
        private Tab currentTab = Tab.Detail;
        private bool isInitialized;

        // 화면이 켜질 때마다(메인 화면에서 들어올 때) 보유 목록을 새로 채우고, 고르던 캐릭터를 다시 보여준다.
        // 꺼진 채로 시작하는 화면이라 Awake 대신 OnEnable에서 처음 준비도 한다.
        private void OnEnable()
        {
            InitializeOnce();
            RebuildList();
        }

        // 처음 한 번만: 탭 버튼을 연결하고 원본 칸·카드를 숨긴다.
        private void InitializeOnce()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            itemTemplate.gameObject.SetActive(false);
            skillEntryTemplate.gameObject.SetActive(false);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                // 람다가 반복 변수 i를 직접 잡으면 반복이 끝난 뒤의 값을 보게 되므로, 반복마다 새 변수에 복사해 잡는다.
                Tab tab = (Tab)i;
                tabButtons[i].onClick.AddListener(() => ShowTab(tab));
            }

            ShowTab(currentTab);
            levelButton.onClick.AddListener(() => growthPopup.Open(selected));
            weaponSlotButton.onClick.AddListener(() => weaponPopup.Open(selected));

            // 레벨업·진급·가챠로 보유 캐릭터가 바뀌면 목록과 정보를 다시 채운다 (화면이 켜져 있을 때만).
            PlayerProfile.OwnedCharactersChanged += HandleCharactersChanged;
        }

        // static 알림 구독 해제 (씬을 떠나 화면이 사라질 때). 구독한 적이 없어도 -= 는 아무 일도 하지 않는다.
        private void OnDestroy()
        {
            PlayerProfile.OwnedCharactersChanged -= HandleCharactersChanged;
        }

        private void HandleCharactersChanged()
        {
            if (gameObject.activeInHierarchy)
            {
                RebuildList();
            }
        }

        // ---------- 보유 목록 ----------

        // 보유 캐릭터로 목록 칸을 다시 만든다. 등급(S 먼저) → 레벨 높은 순으로 세운다.
        // 고르던 캐릭터가 목록에 있으면 그대로, 없으면 첫 칸을 고른다.
        private void RebuildList()
        {
            foreach ((OperatorListItem item, OwnedCharacter _) in items)
            {
                Destroy(item.gameObject);
            }

            items.Clear();

            List<OwnedCharacter> owned = PlayerProfile.GetOwnedCharacters();

            // Sort(비교 람다): 음수면 a가 앞. CharacterGrade는 S = 0, A = 1이라 작은 값(S)이 앞에 온다.
            owned.Sort((a, b) => a.Data.Grade != b.Data.Grade
                ? a.Data.Grade.CompareTo(b.Data.Grade)
                : b.Level.CompareTo(a.Level));

            foreach (OwnedCharacter character in owned)
            {
                OperatorListItem item = Instantiate(itemTemplate, listContainer);
                item.gameObject.SetActive(true);
                item.name = $"Item_{character.Data.name}";
                item.Set(character.Data.Illustration, GetRoleColor(character.Data.Role), character.Data.DisplayName,
                    $"Lv.{character.Level}   <color=#{ColorUtility.ToHtmlStringRGB(GetGradeColor(character.Data.Grade))}>{GetStars(character.Data.Grade)}</color>");

                CharacterData picked = character.Data;
                item.Button.onClick.AddListener(() => Select(picked));
                items.Add((item, character));
            }

            // Exists(조건 람다): 고르던 캐릭터가 지금 목록에도 있는지.
            bool stillOwned = selected != null && items.Exists(entry => entry.owned.Data == selected);
            if (stillOwned)
            {
                Select(selected);
            }
            else if (items.Count > 0)
            {
                Select(items[0].owned.Data);
            }
        }

        // 그 캐릭터를 골라 가운데·정보 패널·스킬 탭을 채우고, 목록에서 그 칸을 강조한다.
        private void Select(CharacterData character)
        {
            selected = character;

            OwnedCharacter owned = null;
            foreach ((OperatorListItem item, OwnedCharacter entry) in items)
            {
                bool isSelected = entry.Data == character;
                item.SetSelected(isSelected);
                if (isSelected)
                {
                    owned = entry;
                }
            }

            if (owned == null)
            {
                return;
            }

            ShowIllustration(owned.Data);
            ShowProfile(owned);
            ShowStats(owned);
            FillSkills(owned.Data);
            ShowLevelButton(owned);
            ShowWeaponSlot(owned.Data);
        }

        // 무기 칸: 낀 무기의 아이콘·등급 테두리·이름·레벨. 안 꼈으면 + 와 "미장착".
        private void ShowWeaponSlot(CharacterData character)
        {
            OwnedWeapon weapon = PlayerProfile.GetEquippedWeapon(character);
            bool hasWeapon = weapon != null;
            weaponEmptyMark.SetActive(!hasWeapon);
            weaponIcon.enabled = hasWeapon && weapon.Data.Icon != null;
            weaponIcon.sprite = hasWeapon ? weapon.Data.Icon : null;
            weaponIcon.preserveAspect = true;
            weaponFrame.color = hasWeapon ? RarityColors.Get((int)weapon.Data.Grade) : new Color(0.04f, 0.05f, 0.08f, 0.97f);
            weaponText.text = hasWeapon ? $"Lv.{weapon.Level}  {weapon.Data.DisplayName}" : "미장착";
        }

        // 레벨 버튼 글자: 상한이면 "진급", 최대 레벨이면 "최대 레벨"(누를 수 없음), 그 외에는 "레벨업".
        private void ShowLevelButton(OwnedCharacter owned)
        {
            GrowthRulesData rules = PlayerProfile.GrowthRules;
            bool isMax = owned.Level >= rules.MaxLevel;
            levelButton.interactable = !isMax;
            levelButtonText.text = isMax ? "최대 레벨"
                : rules.CanPromote(owned.Level, owned.Promotion) ? "진급"
                : "레벨업";
        }

        // ---------- 가운데 ----------

        // 일러스트를 보여준다. 없으면 직군 색으로 물든 실루엣.
        private void ShowIllustration(CharacterData character)
        {
            bool hasArt = illustration.Show(character.Illustration);
            silhouette.gameObject.SetActive(!hasArt);
            silhouette.color = GetRoleColor(character.Role) * new Color(1f, 1f, 1f, 0.55f);
        }

        // ---------- 정보 패널 (상세 탭) ----------

        // 직군·이름·등급·레벨·진급 단계.
        private void ShowProfile(OwnedCharacter owned)
        {
            CharacterData data = owned.Data;
            roleText.text = $"{GetRoleName(data.Role)}  <size=14>{GetRoleEnglish(data.Role)}</size>";
            roleText.color = GetRoleColor(data.Role);
            nameText.text = data.DisplayName;
            gradeText.text = GetStars(data.Grade);
            gradeText.color = GetGradeColor(data.Grade);
            // 레벨 / 지금 진급으로 오를 수 있는 상한 (예: 진급 2회 → Lv. 27 / 30)
            GrowthRulesData rules = PlayerProfile.GrowthRules;
            levelText.text = $"Lv. {owned.Level}<size=30> / {rules.GetLevelCap(owned.Promotion)}</size>";

            // 진급 단계를 ◆(달성)·◇(남음)으로 보여준다. new string(문자, 개수): 같은 문자를 개수만큼 잇는다.
            int promotion = Mathf.Clamp(owned.Promotion, 0, rules.MaxPromotion);
            int need = rules.GetExpToNext(owned.Level);
            string exp = need > 0 ? $"   <color=#8FB4D9>EXP {owned.Exp:N0} / {need:N0}</color>" : string.Empty;
            promotionText.text = $"진급  {new string('◆', promotion)}{new string('◇', rules.MaxPromotion - promotion)}{exp}";
        }

        // 스탯 6개. 데이터 스탯(Lv1)에 전투와 같은 아군 레벨·진급 배율을 곱하고 무기 스탯을 더해, 전투에 들어갔을 때의 기본 스탯을 보여준다.
        // 공격력·방어력은 전투처럼 (int)로 버림해 같은 숫자가 나오게 한다. 무기로 더해진 몫은 옆에 초록 +n으로 따로 보여준다.
        private void ShowStats(OwnedCharacter owned)
        {
            CharacterData data = owned.Data;
            StatScaling scaling = battleRules != null ? battleRules.GetAllyScaling(owned.Level, owned.Promotion) : StatScaling.None;
            WeaponStats weapon = PlayerProfile.GetEquippedWeaponStats(data);

            statLabelsText.text = "공격력\n방어력\n생명력\n속도\n치명 확률\n치명 피해";
            statValuesText.text = string.Join("\n",
                FormatWithWeapon((int)(data.Attack * (double)scaling.Attack), weapon.Attack),
                FormatWithWeapon((int)(data.Defense * (double)scaling.Defense), weapon.Defense),
                FormatWithWeapon((int)(data.MaxHp * (double)scaling.Hp), weapon.MaxHp),
                $"{data.BaseSpeed}",
                EffectText.Percent(data.CritRate),
                EffectText.Percent(data.CritDamage));
        }

        // "합계  +무기 몫" (무기 몫이 없으면 합계만). <size>: 무기 몫은 작은 글자.
        private string FormatWithWeapon(int baseValue, int weaponValue)
        {
            if (weaponValue <= 0)
            {
                return $"{baseValue:N0}";
            }

            return $"<size=20><color=#{ColorUtility.ToHtmlStringRGB(weaponBonusColor)}>+{weaponValue:N0}</color></size>  {baseValue + weaponValue:N0}";
        }

        // ---------- 스킬 탭 ----------

        // 1~3스 → 패시브 → 변신 스킬·종결기 카드. 전투 밖이라 남은 쿨타임·스택은 없으므로 -1을 넘겨 그 줄을 생략한다.
        private void FillSkills(CharacterData character)
        {
            var entries = new List<(string text, Color tint)>();

            for (int slot = 0; slot < CharacterData.SkillCount; slot++)
            {
                entries.Add((SkillDescriptionBuilder.BuildSkill($"{slot + 1}스", character.GetSkill(slot), -1), skillCardColor));
            }

            foreach (PassiveData passive in character.Passives)
            {
                entries.Add((SkillDescriptionBuilder.BuildPassive(passive, -1), passiveCardColor));
            }

            TransformationData transformation = character.Transformation;
            if (transformation != null)
            {
                for (int slot = 0; slot < CharacterData.SkillCount; slot++)
                {
                    string label = $"{transformation.TransformName} {slot + 1}스";
                    entries.Add((SkillDescriptionBuilder.BuildSkill(label, transformation.GetSkill(slot), -1), transformCardColor));
                }

                entries.Add((SkillDescriptionBuilder.BuildSkill($"{transformation.TransformName} 종결기", transformation.Finisher, -1), transformCardColor));
            }

            FillSkillCards(entries);
        }

        // 카드를 글 목록만큼 채운다. 이미 만든 카드는 재사용하고, 모자라면 원본을 복사하고, 남으면 끈다.
        private void FillSkillCards(List<(string text, Color tint)> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (i >= skillEntries.Count)
                {
                    skillEntries.Add(Instantiate(skillEntryTemplate, skillList));
                }

                skillEntries[i].gameObject.SetActive(true);
                skillEntries[i].Set(null, entries[i].text, entries[i].tint);
            }

            for (int i = entries.Count; i < skillEntries.Count; i++)
            {
                skillEntries[i].gameObject.SetActive(false);
            }

            // 캐릭터를 바꾸면 스크롤을 맨 위로 되돌린다.
            skillList.anchoredPosition = Vector2.zero;
        }

        // ---------- 탭 ----------

        // 고른 탭의 내용만 켜고 탭 버튼 색을 바꾼다. 캐릭터를 바꿔도 탭은 그대로 둔다.
        private void ShowTab(Tab tab)
        {
            currentTab = tab;
            for (int i = 0; i < pages.Length; i++)
            {
                bool isSelected = i == (int)tab;
                pages[i].SetActive(isSelected);
                tabButtons[i].image.color = isSelected ? tabSelectedColor : tabNormalColor;
            }
        }

        // ---------- 표시용 이름·색 ----------

        // 등급 별: 가챠 표기와 같게 S = 5성(황금빛), A = 4성(보라).
        private static string GetStars(CharacterGrade grade)
        {
            return grade == CharacterGrade.S ? "★★★★★" : "★★★★";
        }

        private static Color GetGradeColor(CharacterGrade grade)
        {
            return RarityColors.Get(grade == CharacterGrade.S ? 5 : 4);
        }

        // switch 식: 직군마다 돌려줄 값을 나열한다. _ 는 나머지 모든 경우.
        private static string GetRoleName(CharacterRole role) => role switch
        {
            CharacterRole.Attack => "공격",
            CharacterRole.Support => "지원",
            CharacterRole.Defense => "방어",
            CharacterRole.Heal => "회복",
            _ => role.ToString(),
        };

        private static string GetRoleEnglish(CharacterRole role) => role switch
        {
            CharacterRole.Attack => "STRIKER",
            CharacterRole.Support => "SUPPORTER",
            CharacterRole.Defense => "DEFENDER",
            CharacterRole.Heal => "MEDIC",
            _ => string.Empty,
        };

        private Color GetRoleColor(CharacterRole role) => role switch
        {
            CharacterRole.Attack => attackColor,
            CharacterRole.Support => supportColor,
            CharacterRole.Defense => defenseColor,
            CharacterRole.Heal => healerColor,
            _ => Color.gray,
        };
    }
}
