using System.Collections.Generic;
using System.Text;
using Game.Battle;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 아군·보스 상세 창: 아래 초상화 카드(또는 보스 HP바)를 꾹 누르면 열린다.
    //  - 화면 가운데에 그 캐릭터 일러스트(없으면 직군 색 실루엣)
    //  - 오른쪽 패널: 이름 + 탭 3개
    //    · 스탯: 공격력·방어력·HP·속도·치명 확률·치명 피해·피해 증가 (기본보다 오르면 초록 (+n), 내려가면 빨강)
    //    · 버프·디버프: 두 칸으로 나눠 효과마다 카드 한 장 (아이콘·이름·남은 턴·수치·부여자)
    //    · 스킬: 1~3스(남은 쿨타임 포함)와 패시브, 변신 스킬의 효과 설명
    // 탭은 창을 닫았다 열어도 마지막에 고른 탭을 유지한다. 바깥(배경)을 누르면 닫힌다.
    public class UnitDetailPanel : MonoBehaviour
    {
        // 탭 순서 = tabButtons·pages 배열 순서.
        private enum Tab { Stats, Effects, Skills }

        [SerializeField] private BattleStageView stageView;

        [Tooltip("배경을 덮는 판. 누르면 닫힌다")]
        [SerializeField] private Button closeArea;

        [SerializeField] private IllustrationView illustration;

        [Tooltip("일러스트가 없을 때 보여줄 실루엣")]
        [SerializeField] private Image silhouette;

        [SerializeField] private Text nameText;

        [Header("탭")]
        [Tooltip("탭 버튼 (스탯, 버프·디버프, 스킬 순서)")]
        [SerializeField] private Button[] tabButtons;

        [Tooltip("탭마다 보여줄 내용 묶음 (탭 버튼과 같은 순서)")]
        [SerializeField] private GameObject[] pages;

        [SerializeField] private Color tabSelectedColor = new Color(0.3f, 0.8f, 0.98f);
        [SerializeField] private Color tabNormalColor = new Color(0.1f, 0.13f, 0.2f);
        [SerializeField] private Color tabSelectedTextColor = new Color(0.04f, 0.05f, 0.08f);
        [SerializeField] private Color tabNormalTextColor = new Color(1f, 1f, 1f, 0.7f);

        [Header("스탯 탭")]
        [SerializeField] private Text statsText;

        [Tooltip("올라간 수치 색 (기본보다 높을 때)")]
        [SerializeField] private Color upColor = new Color(0.45f, 0.95f, 0.55f);

        [Tooltip("내려간 수치 색 (디버프로 낮아졌을 때)")]
        [SerializeField] private Color downColor = new Color(1f, 0.45f, 0.4f);

        [Header("버프·디버프 탭")]
        [SerializeField] private Text buffHeaderText;
        [SerializeField] private Text debuffHeaderText;

        [Tooltip("버프 카드가 쌓일 목록 (스크롤 내용)")]
        [SerializeField] private RectTransform buffList;

        [Tooltip("디버프 카드가 쌓일 목록 (스크롤 내용)")]
        [SerializeField] private RectTransform debuffList;

        [Tooltip("목록이 비었을 때 보여줄 글")]
        [SerializeField] private GameObject buffEmpty;
        [SerializeField] private GameObject debuffEmpty;

        [SerializeField] private Color buffCardColor = new Color(0.12f, 0.22f, 0.38f, 0.9f);
        [SerializeField] private Color debuffCardColor = new Color(0.36f, 0.12f, 0.12f, 0.9f);

        [Header("스킬 탭")]
        [Tooltip("스킬·패시브 카드가 쌓일 목록 (스크롤 내용)")]
        [SerializeField] private RectTransform skillList;

        [SerializeField] private Color skillCardColor = new Color(0.1f, 0.15f, 0.24f, 0.95f);
        [SerializeField] private Color passiveCardColor = new Color(0.2f, 0.13f, 0.3f, 0.95f);
        [SerializeField] private Color transformCardColor = new Color(0.32f, 0.16f, 0.1f, 0.95f);

        [Header("카드 원본")]
        [Tooltip("복사해 쓰는 카드 원본 (꺼 둔 채로 둔다)")]
        [SerializeField] private DetailEntryView entryTemplate;

        // 목록마다 만들어 둔 카드. 다시 열 때 지우지 않고 재사용하고, 남는 카드는 끈다.
        private readonly Dictionary<RectTransform, List<DetailEntryView>> entryPools = new Dictionary<RectTransform, List<DetailEntryView>>();

        private Tab currentTab = Tab.Stats;

        private void Awake()
        {
            closeArea.onClick.AddListener(Close);

            for (int i = 0; i < tabButtons.Length; i++)
            {
                // 람다가 반복 변수 i를 직접 잡으면 반복이 끝난 뒤의 값(3)을 보게 되므로, 반복마다 새 변수에 복사해 잡는다.
                Tab tab = (Tab)i;
                tabButtons[i].onClick.AddListener(() => ShowTab(tab));
            }

            entryTemplate.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        // 그 유닛의 상세 정보를 채우고 창을 연다. 세 탭을 모두 채워 두고, 마지막에 고른 탭을 보여준다.
        public void Open(BattleUnit unit)
        {
            gameObject.SetActive(true);

            bool hasIllustration = illustration.Show(unit.Data.Illustration);
            silhouette.gameObject.SetActive(!hasIllustration);
            nameText.text = $"{unit.Name}  <size=26>Lv.{unit.Level}</size>";

            statsText.text = BuildStats(unit);
            FillEffects(unit);
            FillSkills(unit);
            ShowTab(currentTab);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        // 고른 탭의 내용만 켜고, 탭 버튼 색을 고른 것/나머지로 칠한다.
        private void ShowTab(Tab tab)
        {
            currentTab = tab;

            for (int i = 0; i < pages.Length; i++)
            {
                bool selected = i == (int)tab;
                pages[i].SetActive(selected);
                tabButtons[i].image.color = selected ? tabSelectedColor : tabNormalColor;

                Text label = tabButtons[i].GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.color = selected ? tabSelectedTextColor : tabNormalTextColor;
                }
            }
        }

        // ---------- 스탯 탭 ----------

        // 스탯 줄들. 기본 스탯(레벨 반영, 버프 전) 대비 지금 값의 차이를 색으로 붙인다.
        // 공격력·방어력은 지금 값을 (int)로 버림해 쓰므로, 기본값도 똑같이 버림한 값과 비교해야 소수점 차이로 (-1)이 붙지 않는다.
        private string BuildStats(BattleUnit unit)
        {
            var text = new StringBuilder();
            AppendStat(text, "공격력", unit.Attack, (int)unit.BaseAttack, isPercent: false);
            AppendStat(text, "방어력", unit.Defense, (int)unit.BaseDefense, isPercent: false);
            text.AppendLine($"HP  <b>{unit.CurrentHp:N0}</b> / {unit.MaxHp:N0}");
            AppendStat(text, "속도", unit.Speed, unit.Data.BaseSpeed, isPercent: false);
            AppendStat(text, "치명 확률", unit.CritRate, unit.Data.CritRate, isPercent: true);
            AppendStat(text, "치명 피해", unit.CritDamage, unit.Data.CritDamage, isPercent: true);
            AppendStat(text, "피해 증가", unit.DamageBonus, 0d, isPercent: true);
            return text.ToString();
        }

        // "이름  값 (+차이)". isPercent면 0.2 → 20%로 표시. 차이가 거의 없으면(소수점 반올림 오차) 붙이지 않는다.
        private void AppendStat(StringBuilder text, string label, double value, double baseValue, bool isPercent)
        {
            double diff = value - baseValue;
            string Format(double v) => isPercent ? $"{v * 100d:0.#}%" : $"{v:N0}";

            string diffText = string.Empty;
            if (System.Math.Abs(diff) > (isPercent ? 0.0005d : 0.5d))
            {
                // ColorUtility.ToHtmlStringRGB: 색을 "RRGGBB" 글자로 바꿔 리치 텍스트 <color=#...>에 쓴다.
                Color color = diff > 0d ? upColor : downColor;
                string sign = diff > 0d ? "+" : "-";
                diffText = $"  <color=#{ColorUtility.ToHtmlStringRGB(color)}>({sign}{Format(System.Math.Abs(diff))})</color>";
            }

            text.AppendLine($"{label}  <b>{Format(value)}</b>{diffText}");
        }

        // ---------- 버프·디버프 탭 ----------

        // 걸린 효과를 버프·디버프 두 칸으로 나눠, 효과마다 카드(아이콘 + 툴팁과 같은 설명)를 한 장씩 채운다.
        // 목록은 머리 위·카드 아이콘과 같은 것(표시용 효과 포함)을 쓴다.
        private void FillEffects(BattleUnit unit)
        {
            UnitView view = stageView.FindView(unit);
            List<StatusInstance> statuses = view != null ? view.BuildDisplayedStatuses() : unit.Statuses.GetAll();

            // FindAll(조건 람다): 분류가 같은 효과만 모은다.
            List<StatusInstance> buffs = statuses.FindAll(status => status.Data.Category == StatusCategory.Buff);
            List<StatusInstance> debuffs = statuses.FindAll(status => status.Data.Category == StatusCategory.Debuff);

            buffHeaderText.text = $"버프 {buffs.Count}";
            debuffHeaderText.text = $"디버프 {debuffs.Count}";
            FillStatusList(buffList, buffs, buffCardColor, buffEmpty);
            FillStatusList(debuffList, debuffs, debuffCardColor, debuffEmpty);
        }

        // 효과 목록 하나를 카드로 채운다. 비었으면 "없음" 글을 켠다.
        private void FillStatusList(RectTransform list, List<StatusInstance> statuses, Color tint, GameObject emptyLabel)
        {
            // ConvertAll(람다): 효과마다 (아이콘, 설명 글) 묶음으로 바꾼다.
            var entries = statuses.ConvertAll(status => ((Texture)status.Data.Icon, StatusDescriptionBuilder.Build(status), tint));
            FillList(list, entries);
            emptyLabel.SetActive(statuses.Count == 0);
        }

        // ---------- 스킬 탭 ----------

        // 지금 쓰는 1~3스(변신 중이면 변신 스킬) → 패시브 → 변신 스킬 순서로 카드를 채운다.
        private void FillSkills(BattleUnit unit)
        {
            var entries = new List<(Texture, string, Color)>();
            string slotPrefix = unit.IsTransformed ? "변신 " : string.Empty;

            for (int slot = 0; slot < CharacterData.SkillCount; slot++)
            {
                SkillData skill = unit.GetSkill(slot);
                if (skill != null)
                {
                    string text = SkillDescriptionBuilder.BuildSkill($"{slotPrefix}{slot + 1}스", skill, unit.GetRemainingCooldown(slot));
                    entries.Add((null, text, unit.IsTransformed ? transformCardColor : skillCardColor));
                }
            }

            foreach (PassiveData passive in unit.Data.Passives)
            {
                entries.Add((null, SkillDescriptionBuilder.BuildPassive(unit, passive), passiveCardColor));
            }

            AddTransformationEntries(unit, entries);
            FillList(skillList, entries);
        }

        // 변신할 수 있는 캐릭터면 변신 스킬도 보여준다. 변신 전에는 변신 1~3스 + 종결기, 변신 중에는 이미 위에 나왔으므로 종결기만.
        // 아직 쓸 수 없는 스킬이라 남은 쿨타임은 -1로 넘겨 "사용 가능" 줄을 생략한다.
        private void AddTransformationEntries(BattleUnit unit, List<(Texture, string, Color)> entries)
        {
            TransformationData transformation = unit.Data.Transformation;
            if (transformation == null)
            {
                return;
            }

            string name = transformation.TransformName;
            if (!unit.IsTransformed)
            {
                for (int slot = 0; slot < CharacterData.SkillCount; slot++)
                {
                    SkillData skill = transformation.GetSkill(slot);
                    if (skill != null)
                    {
                        entries.Add((null, SkillDescriptionBuilder.BuildSkill($"{name} {slot + 1}스", skill, -1), transformCardColor));
                    }
                }
            }

            if (transformation.Finisher != null)
            {
                string turnLimit = $"<color=#8FB4D9>엑스트라 턴 {transformation.TurnLimit}회를 다 쓰거나 강제 해제될 때 자동 발동</color>";
                string text = SkillDescriptionBuilder.BuildSkill($"{name} 종결기", transformation.Finisher, -1);
                entries.Add((null, $"{text}\n{turnLimit}", transformCardColor));
            }
        }

        // ---------- 카드 목록 공용 ----------

        // 목록을 (아이콘, 글, 바탕색) 묶음으로 채운다. 이미 만든 카드는 재사용하고, 모자라면 원본을 복사하고, 남으면 끈다.
        // 튜플 (Texture, string, Color): 이름 없는 값 세 개를 하나로 묶어 넘긴다 (Item1, Item2, Item3).
        private void FillList(RectTransform list, List<(Texture, string, Color)> entries)
        {
            if (!entryPools.TryGetValue(list, out List<DetailEntryView> pool))
            {
                pool = new List<DetailEntryView>();
                entryPools[list] = pool;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                if (i >= pool.Count)
                {
                    pool.Add(Instantiate(entryTemplate, list));
                }

                pool[i].gameObject.SetActive(true);
                pool[i].Set(entries[i].Item1, entries[i].Item2, entries[i].Item3);
            }

            for (int i = entries.Count; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
            }

            // 목록을 새로 채웠으니 스크롤을 맨 위로 되돌린다 (내용 RectTransform의 위치를 0으로).
            list.anchoredPosition = Vector2.zero;
        }
    }
}
