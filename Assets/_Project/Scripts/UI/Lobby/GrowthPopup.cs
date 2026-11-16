using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투원 육성 창 (전투원 화면의 레벨업 버튼). 젠레스 존 제로 에이전트 육성 창을 참고했다.
    //  - 레벨업: 경험치 재료 칸을 눌러(홀드하면 연속) 개수를 고르면, 오를 레벨·경험치·스탯을 미리 보여준다
    //    "일괄 추가"는 다음 진급 레벨(레벨 상한)까지 필요한 만큼 큰 재료부터 채운다. 상한을 넘는 경험치는 버린다
    //  - 진급: 레벨 상한(10·20·…·50)에 닿았으면 같은 창이 진급 화면으로 바뀐다 (지금은 재화 없이 바로 진급)
    // 실제 변경은 PlayerProfile.LevelUp·Promote가 하고, 저장 알림(OwnedCharactersChanged)으로 이 창과 전투원 화면이 다시 그려진다.
    public class GrowthPopup : MonoBehaviour
    {
        [Tooltip("스탯 미리 보기용 레벨 성장 곡선 (전투와 같은 배율)")]
        [SerializeField] private BattleRulesData battleRules;

        [SerializeField] private Button closeButton;

        [Tooltip("창 바깥을 덮는 판. 누르면 닫힌다")]
        [SerializeField] private Button closeArea;

        [SerializeField] private Text titleText;

        [Header("스탯 (지금 값 + 오를 값)")]
        [SerializeField] private Text hpValueText;
        [SerializeField] private Text attackValueText;
        [SerializeField] private Text defenseValueText;

        [Tooltip("오를 값 색")]
        [SerializeField] private Color gainColor = new Color(0.85f, 1f, 0.2f);

        [Header("레벨업")]
        [Tooltip("레벨업일 때만 켜는 묶음 (경험치 바·재료·전부 제거·일괄 추가)")]
        [SerializeField] private GameObject levelUpGroup;

        [SerializeField] private Text fromLevelText;
        [SerializeField] private Text toLevelText;
        [SerializeField] private Text expText;

        [Tooltip("지금 쌓인 경험치 (바의 어두운 채움)")]
        [SerializeField] private Image currentFill;

        [Tooltip("재료를 넣었을 때 도달할 경험치 (바의 밝은 채움)")]
        [SerializeField] private Image previewFill;

        [Tooltip("재료 칸 (성장 규칙의 경험치 재료와 같은 순서)")]
        [SerializeField] private List<GrowthMaterialSlot> slots = new List<GrowthMaterialSlot>();

        [SerializeField] private Text hintText;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button fillButton;

        [Header("진급")]
        [Tooltip("진급일 때만 켜는 묶음")]
        [SerializeField] private GameObject promoteGroup;

        [SerializeField] private Text promoteText;

        [Header("확인")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private Text confirmText;

        private CharacterData character;
        private int[] selected = new int[0];
        private bool isInitialized;

        // 그 캐릭터의 육성 창을 연다. 레벨 상한이면 진급, 아니면 레벨업 화면.
        public void Open(CharacterData target)
        {
            InitializeOnce();
            character = target;
            selected = new int[PlayerProfile.GrowthRules.ExpMaterials.Count];
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        // 처음 한 번만: 버튼·재료 칸을 연결하고, 저장 알림을 구독한다.
        private void InitializeOnce()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            closeButton.onClick.AddListener(Close);
            closeArea.onClick.AddListener(Close);
            clearButton.onClick.AddListener(ClearSelection);
            fillButton.onClick.AddListener(FillToCap);
            confirmButton.onClick.AddListener(Confirm);

            IReadOnlyList<ExpMaterialEntry> materials = PlayerProfile.GrowthRules.ExpMaterials;
            for (int i = 0; i < slots.Count; i++)
            {
                bool hasMaterial = i < materials.Count;
                slots[i].gameObject.SetActive(hasMaterial);
                if (!hasMaterial)
                {
                    continue;
                }

                MaterialData material = materials[i].Material;
                slots[i].SetMaterial(material != null ? material.Icon : null, RarityColors.Get(material != null ? material.Grade : 3), materials[i].Experience);

                // 람다가 반복 변수 i를 직접 잡으면 반복이 끝난 뒤의 값을 보게 되므로, 반복마다 새 변수에 복사해 잡는다.
                int index = i;
                slots[i].Added += () => ChangeCount(index, 1);
                slots[i].Removed += () => ChangeCount(index, -1);
            }

            PlayerProfile.OwnedCharactersChanged += HandleCharactersChanged;
        }

        // static 알림 구독 해제 (씬을 떠나 창이 사라질 때). 구독한 적이 없어도 -= 는 아무 일도 하지 않는다.
        private void OnDestroy()
        {
            PlayerProfile.OwnedCharactersChanged -= HandleCharactersChanged;
        }

        // 레벨업·진급이 저장되면 창이 열려 있을 때만 새 상태로 다시 그린다.
        private void HandleCharactersChanged()
        {
            if (gameObject.activeSelf && character != null)
            {
                Refresh();
            }
        }

        // ---------- 재료 고르기 ----------

        // 재료 개수를 하나 늘리거나 줄인다. 늘릴 때는 보유 개수를 넘거나, 이미 레벨 상한까지 찼으면 막는다.
        private void ChangeCount(int index, int delta)
        {
            OwnedCharacter owned = PlayerProfile.GetOwned(character);
            if (owned == null)
            {
                return;
            }

            if (delta > 0)
            {
                bool reachedCap = Preview(owned).Level >= GetCap(owned);
                bool outOfStock = selected[index] >= GetUsable(index);
                if (reachedCap || outOfStock)
                {
                    return;
                }
            }

            selected[index] = Mathf.Max(0, selected[index] + delta);
            Refresh();
        }

        private void ClearSelection()
        {
            System.Array.Clear(selected, 0, selected.Length);
            Refresh();
        }

        // 일괄 추가: 다음 진급 레벨(상한)까지 필요한 경험치를 큰 재료부터 채운다.
        //  1) 큰 재료부터 "넘치지 않는 만큼" 넣고  2) 그래도 모자라면 남은 재료 중 가장 작은 것을 하나씩 더해 상한을 넘긴다
        // 넘친 경험치는 버린다 (기획: 초과분 무시).
        private void FillToCap()
        {
            OwnedCharacter owned = PlayerProfile.GetOwned(character);
            if (owned == null)
            {
                return;
            }

            GrowthRulesData rules = PlayerProfile.GrowthRules;
            IReadOnlyList<ExpMaterialEntry> materials = rules.ExpMaterials;
            System.Array.Clear(selected, 0, selected.Length);

            int remaining = rules.GetExpToReach(owned.Level, owned.Exp, GetCap(owned));
            for (int i = materials.Count - 1; i >= 0 && remaining > 0; i--)
            {
                // 남은 경험치 ÷ 재료 1개 경험치(정수 나눗셈 = 넘치지 않는 개수)와 보유 개수 중 작은 값.
                int count = Mathf.Min(GetUsable(i), remaining / materials[i].Experience);
                selected[i] += count;
                remaining -= count * materials[i].Experience;
            }

            while (remaining > 0)
            {
                int smallest = FindSmallestAvailable();
                if (smallest < 0)
                {
                    break;
                }

                selected[smallest]++;
                remaining -= materials[smallest].Experience;
            }

            Refresh();
        }

        // 아직 더 넣을 수 있는 재료 중 가장 작은 것(왼쪽부터)의 번호. 없으면 -1.
        private int FindSmallestAvailable()
        {
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] < GetUsable(i))
                {
                    return i;
                }
            }

            return -1;
        }

        // ---------- 확인 ----------

        // 레벨업 또는 진급을 실행한다. 성공하면 저장 알림으로 창이 다시 그려진다.
        private void Confirm()
        {
            OwnedCharacter owned = PlayerProfile.GetOwned(character);
            if (owned == null)
            {
                return;
            }

            if (IsPromotion(owned))
            {
                PlayerProfile.Promote(character);
                return;
            }

            // 재료를 비우고 나서 저장해야, 알림으로 다시 그릴 때 새 레벨 기준 빈 선택으로 보인다.
            int[] used = (int[])selected.Clone();
            System.Array.Clear(selected, 0, selected.Length);
            if (!PlayerProfile.LevelUp(character, used))
            {
                // 실패(재료 부족 등)하면 고른 개수를 되돌린다.
                selected = used;
                Refresh();
            }
        }

        // ---------- 그리기 ----------

        // 지금 상태와 고른 재료로 창 전체를 다시 그린다.
        private void Refresh()
        {
            OwnedCharacter owned = PlayerProfile.GetOwned(character);
            if (owned == null)
            {
                Close();
                return;
            }

            bool isPromotion = IsPromotion(owned);
            bool isMaxLevel = owned.Level >= PlayerProfile.GrowthRules.MaxLevel;
            levelUpGroup.SetActive(!isPromotion);
            promoteGroup.SetActive(isPromotion);
            titleText.text = isPromotion ? "전투원 진급" : "전투원 육성";

            // 진급 화면은 레벨이 그대로이고 진급 횟수만 +1 → 진급 점프만큼 오를 스탯을 보여준다.
            ExpResult preview = isPromotion ? new ExpResult(owned.Level, 0, 0) : Preview(owned);
            int previewPromotion = isPromotion ? owned.Promotion + 1 : owned.Promotion;
            ShowStats(owned.Level, owned.Promotion, preview.Level, previewPromotion);

            if (isPromotion)
            {
                ShowPromotion(owned);
                confirmText.text = "진급";
                confirmButton.interactable = true;
                return;
            }

            ShowLevelBar(owned, preview);
            ShowSlots();

            bool hasSelection = TotalSelectedExp() > 0;
            confirmText.text = isMaxLevel ? "최대 레벨" : "레벨업";
            confirmButton.interactable = hasSelection && !isMaxLevel;
            hintText.text = preview.Wasted > 0
                ? $"재료를 홀드하면 연속으로 추가할 수 있습니다   <color=#FF9966>초과 경험치 {preview.Wasted:N0}은 버려집니다</color>"
                : "재료를 홀드하면 연속으로 추가할 수 있습니다";
        }

        // HP·공격력·방어력의 지금 값과 오를 값. 전투와 같은 아군 레벨·진급 배율로 계산한다 (공격력·방어력은 전투처럼 버림).
        private void ShowStats(int level, int promotion, int previewLevel, int previewPromotion)
        {
            StatScaling now = battleRules.GetAllyScaling(level, promotion);
            StatScaling next = battleRules.GetAllyScaling(previewLevel, previewPromotion);

            hpValueText.text = FormatStat((int)(character.MaxHp * (double)now.Hp), (int)(character.MaxHp * (double)next.Hp));
            attackValueText.text = FormatStat((int)(character.Attack * (double)now.Attack), (int)(character.Attack * (double)next.Attack));
            defenseValueText.text = FormatStat((int)(character.Defense * (double)now.Defense), (int)(character.Defense * (double)next.Defense));
        }

        // "지금 값 +오를 값". 오르지 않으면 지금 값만.
        private string FormatStat(int now, int next)
        {
            int gain = next - now;
            return gain > 0 ? $"{now:N0} <color=#{ColorUtility.ToHtmlStringRGB(gainColor)}>+{gain:N0}</color>" : $"{now:N0}";
        }

        // 경험치 바: 왼쪽 지금 레벨, 오른쪽 도달할 레벨, 가운데 "경험치 / 필요량".
        // 레벨이 오르지 않으면 어두운 채움(지금)과 밝은 채움(재료 포함)을 함께, 오르면 도달할 레벨의 경험치만 밝게 보여준다.
        private void ShowLevelBar(OwnedCharacter owned, ExpResult preview)
        {
            GrowthRulesData rules = PlayerProfile.GrowthRules;
            int cap = GetCap(owned);
            fromLevelText.text = owned.Level.ToString();
            toLevelText.text = preview.Level.ToString();

            int need = rules.GetExpToNext(preview.Level);
            bool atCap = preview.Level >= cap;
            expText.text = atCap ? $"Lv.{cap} 도달 (상한)" : $"{preview.Exp:N0} / {need:N0}";

            // fillAmount: Image(Filled)가 0~1 비율만큼만 그린다.
            float previewRatio = atCap ? 1f : (float)preview.Exp / need;
            bool sameLevel = preview.Level == owned.Level;
            float currentRatio = sameLevel && need > 0 ? (float)owned.Exp / need : 0f;
            previewFill.fillAmount = previewRatio;
            currentFill.fillAmount = currentRatio;
        }

        private void ShowSlots()
        {
            for (int i = 0; i < selected.Length && i < slots.Count; i++)
            {
                slots[i].SetCount(selected[i], GetUsable(i));
            }
        }

        // 진급 화면: 레벨 상한이 어떻게 바뀌는지.
        private void ShowPromotion(OwnedCharacter owned)
        {
            GrowthRulesData rules = PlayerProfile.GrowthRules;
            int nextCap = rules.GetLevelCap(owned.Promotion + 1);
            promoteText.text = $"진급 {owned.Promotion} → <b>{owned.Promotion + 1}</b>\n최대 레벨  {GetCap(owned)} → <b>{nextCap}</b>\n\n<size=20>지금은 재화 없이 바로 진급됩니다 (진급 재료 미정)</size>";
        }

        // ---------- 계산 ----------

        // 고른 재료를 다 넣었을 때의 결과 (저장하지 않는 미리 보기).
        private ExpResult Preview(OwnedCharacter owned)
        {
            return PlayerProfile.GrowthRules.AddExp(owned.Level, owned.Exp, GetCap(owned), TotalSelectedExp());
        }

        private int TotalSelectedExp()
        {
            IReadOnlyList<ExpMaterialEntry> materials = PlayerProfile.GrowthRules.ExpMaterials;
            int total = 0;
            for (int i = 0; i < selected.Length; i++)
            {
                total += selected[i] * materials[i].Experience;
            }

            return total;
        }

        private static int GetCap(OwnedCharacter owned)
        {
            return PlayerProfile.GrowthRules.GetLevelCap(owned.Promotion);
        }

        private static bool IsPromotion(OwnedCharacter owned)
        {
            return PlayerProfile.GrowthRules.CanPromote(owned.Level, owned.Promotion);
        }

        private static int GetUsable(int index)
        {
            return PlayerProfile.GetUsableMaterialCount(PlayerProfile.GrowthRules.ExpMaterials[index].Material);
        }
    }
}
