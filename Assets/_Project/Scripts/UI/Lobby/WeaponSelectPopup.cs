using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 무기 장착 창 (전투원 화면의 무기 칸을 누르면 열림).
    //  - 왼쪽: 이 캐릭터가 낄 수 있는 보유 무기(직군에 맞는 종류만) 칸 목록. 지금 낀 무기 → 등급 → 레벨 순
    //  - 오른쪽: 고른 무기의 정보(종류·레벨·진급·지금 스탯·장착 캐릭터) + 장착 / 해제 / 강화
    // 장착·강화는 PlayerProfile이 저장하고, 저장 알림(InventoryChanged)으로 이 창과 전투원 화면이 다시 그려진다.
    public class WeaponSelectPopup : MonoBehaviour
    {
        [SerializeField] private Button closeButton;

        [Tooltip("창 바깥을 덮는 판. 누르면 닫힌다")]
        [SerializeField] private Button closeArea;

        [SerializeField] private Text titleText;

        [Header("목록")]
        [SerializeField] private RectTransform grid;

        [Tooltip("무기 칸 원본 (인벤토리 칸과 같은 모양, 꺼 둔 채로 둔다)")]
        [SerializeField] private InventorySlotView slotTemplate;

        [Tooltip("낄 수 있는 무기가 없을 때 보여줄 글")]
        [SerializeField] private GameObject emptyText;

        [Header("상세")]
        [SerializeField] private GameObject detailGroup;
        [SerializeField] private Image detailFrame;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailInfo;

        [Header("버튼")]
        [SerializeField] private Button equipButton;
        [SerializeField] private Text equipText;
        [SerializeField] private Button upgradeButton;

        [Tooltip("강화를 누르면 여는 육성 창 (무기 모드)")]
        [SerializeField] private GrowthPopup growthPopup;

        private readonly List<InventorySlotView> slots = new List<InventorySlotView>();
        private List<OwnedWeapon> weapons = new List<OwnedWeapon>();
        private CharacterData character;
        private int selectedUid = -1;
        private bool isInitialized;

        // 그 캐릭터의 무기 장착 창을 연다. 처음에는 지금 낀 무기를 고른다.
        public void Open(CharacterData target)
        {
            InitializeOnce();
            character = target;
            OwnedWeapon equipped = PlayerProfile.GetEquippedWeapon(character);
            selectedUid = equipped != null ? equipped.Uid : -1;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        private void InitializeOnce()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            slotTemplate.gameObject.SetActive(false);
            closeButton.onClick.AddListener(Close);
            closeArea.onClick.AddListener(Close);
            equipButton.onClick.AddListener(ToggleEquip);
            upgradeButton.onClick.AddListener(() => growthPopup.OpenWeapon(selectedUid));
            PlayerProfile.InventoryChanged += HandleInventoryChanged;
        }

        // static 알림 구독 해제 (씬을 떠나 창이 사라질 때).
        private void OnDestroy()
        {
            PlayerProfile.InventoryChanged -= HandleInventoryChanged;
        }

        private void HandleInventoryChanged()
        {
            if (gameObject.activeSelf && character != null)
            {
                Refresh();
            }
        }

        // 고른 무기를 이 캐릭터에게 낀다. 이미 이 캐릭터가 끼고 있으면 뺀다.
        private void ToggleEquip()
        {
            OwnedWeapon weapon = FindSelected();
            if (weapon == null)
            {
                return;
            }

            if (weapon.EquippedTo == character.Id)
            {
                PlayerProfile.UnequipWeapon(character);
            }
            else
            {
                PlayerProfile.EquipWeapon(character, weapon.Uid);
            }
        }

        // ---------- 그리기 ----------

        // 낄 수 있는 무기 목록을 다시 채우고, 고른 무기의 상세를 보여준다.
        private void Refresh()
        {
            WeaponType type = GetWeaponType(character.Role);
            titleText.text = $"무기 장착  <size=22><color=#8FB4D9>{EffectText.EnumName(type)} 전용</color></size>";

            weapons = PlayerProfile.GetEquippableWeapons(character);

            // Sort(비교 람다): 음수면 a가 앞. 이 캐릭터가 낀 무기 → 높은 등급 → 높은 레벨 순.
            weapons.Sort((a, b) =>
            {
                bool aMine = a.EquippedTo == character.Id;
                bool bMine = b.EquippedTo == character.Id;
                if (aMine != bMine)
                {
                    return aMine ? -1 : 1;
                }

                return a.Data.Grade != b.Data.Grade ? b.Data.Grade.CompareTo(a.Data.Grade) : b.Level.CompareTo(a.Level);
            });

            // 고른 무기가 목록에 없으면(분해 등) 첫 칸을 고른다. Exists(조건 람다): 조건에 맞는 원소가 있는지.
            if (!weapons.Exists(weapon => weapon.Uid == selectedUid))
            {
                selectedUid = weapons.Count > 0 ? weapons[0].Uid : -1;
            }

            FillSlots();
            emptyText.SetActive(weapons.Count == 0);
            ShowDetail(FindSelected());
        }

        // 칸을 무기 수만큼 채운다. 모자라면 원본을 복사하고, 남으면 끈다.
        private void FillSlots()
        {
            for (int i = 0; i < weapons.Count; i++)
            {
                if (i >= slots.Count)
                {
                    InventorySlotView slot = Instantiate(slotTemplate, grid);
                    int index = i;
                    slot.Button.onClick.AddListener(() => Select(index));
                    slots.Add(slot);
                }

                slots[i].gameObject.SetActive(true);
                slots[i].ShowWeapon(weapons[i]);
                slots[i].SetSelected(weapons[i].Uid == selectedUid);
            }

            for (int i = weapons.Count; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(false);
            }
        }

        private void Select(int index)
        {
            if (index < weapons.Count)
            {
                selectedUid = weapons[index].Uid;
                Refresh();
            }
        }

        // 고른 무기의 정보와 버튼 상태. 고른 게 없으면 상세를 숨긴다.
        private void ShowDetail(OwnedWeapon weapon)
        {
            detailGroup.SetActive(weapon != null);
            if (weapon == null)
            {
                return;
            }

            WeaponData data = weapon.Data;
            Color color = RarityColors.Get((int)data.Grade);
            detailFrame.color = color;
            detailIcon.sprite = data.Icon;
            detailIcon.enabled = data.Icon != null;
            detailIcon.preserveAspect = true;
            detailName.text = $"{data.DisplayName}\n<size=22><color=#{ColorUtility.ToHtmlStringRGB(color)}>{new string('★', (int)data.Grade)}</color>  {EffectText.EnumName(data.Type)}</size>";

            WeaponStats stats = PlayerProfile.GetWeaponStats(weapon);
            int cap = PlayerProfile.WeaponGrowthRules.GetLevelCap(weapon.Promotion);

            // 장착 캐릭터 이름: ID로 데이터를 찾는다. ?.: 못 찾으면 null → ?? 로 ID 그대로.
            string owner = weapon.IsEquipped
                ? PlayerProfile.Database.FindCharacter(weapon.EquippedTo)?.DisplayName ?? weapon.EquippedTo
                : "없음";

            detailInfo.text =
                $"Lv. <b>{weapon.Level}</b> / {cap}    진급 {weapon.Promotion}\n\n" +
                $"공격력  <b>+{stats.Attack:N0}</b>\n" +
                $"HP  <b>+{stats.MaxHp:N0}</b>\n" +
                $"방어력  <b>+{stats.Defense:N0}</b>\n\n" +
                $"장착  {owner}";

            bool isMine = weapon.EquippedTo == character.Id;
            equipText.text = isMine ? "해제" : weapon.IsEquipped ? "가져와 장착" : "장착";
        }

        private OwnedWeapon FindSelected()
        {
            // Find(조건 람다): 조건에 맞는 첫 원소, 없으면 null.
            return weapons.Find(weapon => weapon.Uid == selectedUid);
        }

        // 직군이 낄 수 있는 무기 종류 (제목 표시용). WeaponData.EquippableRole의 반대 방향.
        private static WeaponType GetWeaponType(CharacterRole role) => role switch
        {
            CharacterRole.Defense => WeaponType.Shield,
            CharacterRole.Support => WeaponType.Staff,
            CharacterRole.Heal => WeaponType.Rosary,
            _ => WeaponType.Sword,
        };
    }
}
