using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 인벤토리 화면 (메인 화면의 DEPOT). 에픽세븐식으로 무기 인벤과 재료 인벤을 탭으로 나눈다.
    //  - 무기 탭: 개체 1개 = 1칸, "보유 수 / 칸 수", 정렬(등급·레벨·획득순), 일괄 분해
    //  - 재료 탭: 종류마다 1칸 + 개수 (칸 제한 없음)
    //  - 칸을 누르면 오른쪽 상세 패널에 이름·등급·스탯(무기)·설명
    // 화면 모양은 씬에서 만들고, 이 컴포넌트는 세이브(PlayerProfile)의 내용으로 칸을 채우고 버튼을 연결한다.
    public class InventoryView : MonoBehaviour
    {
        private enum Tab { Weapon, Material }

        // 무기 정렬 방식. 정렬 버튼을 누를 때마다 다음 방식으로 넘어간다.
        private enum WeaponSort { Grade, Level, Recent }

        [Header("탭")]
        [SerializeField] private Button weaponTab;
        [SerializeField] private Button materialTab;
        [SerializeField] private Color selectedTabColor = new Color(0.16f, 0.55f, 0.75f, 0.96f);
        [SerializeField] private Color normalTabColor = new Color(0.04f, 0.05f, 0.07f, 0.78f);

        [Header("목록")]
        [Tooltip("칸들이 들어갈 그리드 (GridLayoutGroup)")]
        [SerializeField] private RectTransform grid;

        [Tooltip("복사해서 쓸 칸 원본 (씬에서는 꺼 둔다)")]
        [SerializeField] private InventorySlotView slotTemplate;

        [Tooltip("보유 수 표시 (무기: 12 / 200, 재료: 5종)")]
        [SerializeField] private Text countText;

        [Tooltip("목록이 비었을 때 보여줄 글자")]
        [SerializeField] private GameObject emptyText;

        [SerializeField] private Button sortButton;
        [SerializeField] private Text sortText;
        [SerializeField] private Button dismantleButton;
        [SerializeField] private DismantlePopup dismantlePopup;

        [Tooltip("일괄 분해 결과 문구")]
        [SerializeField] private Text resultText;

        [Header("상세 패널")]
        [SerializeField] private GameObject detailPanel;
        [SerializeField] private Image detailFrame;
        [SerializeField] private Image detailIcon;
        [SerializeField] private Text detailName;
        [SerializeField] private Text detailGrade;
        [SerializeField] private Text detailInfo;
        [SerializeField] private Text detailDescription;

        // 만들어 둔 칸들. 목록이 바뀌면 새로 만들지 않고 있는 칸을 다시 쓰고, 남는 칸은 끈다.
        private readonly List<InventorySlotView> slots = new List<InventorySlotView>();

        private Tab currentTab = Tab.Weapon;
        private WeaponSort weaponSort = WeaponSort.Grade;

        // 상세 패널에 띄운 항목. 목록을 다시 그려도 같은 항목을 계속 골라 두기 위해 기억한다 (무기는 개체 번호, 재료는 데이터).
        private int selectedWeaponUid = -1;
        private MaterialData selectedMaterial;

        // 버튼을 연결하고, 인벤토리가 바뀌면(가챠·분해) 다시 그리도록 구독한다.
        private void Awake()
        {
            slotTemplate.gameObject.SetActive(false);

            weaponTab.onClick.AddListener(() => ShowTab(Tab.Weapon));
            materialTab.onClick.AddListener(() => ShowTab(Tab.Material));
            sortButton.onClick.AddListener(CycleSort);
            dismantleButton.onClick.AddListener(() => dismantlePopup.Open(ShowDismantleResult));

            // 이벤트 구독(+=): 인벤토리가 저장될 때마다 Refresh가 불린다. 화면이 사라질 때 OnDestroy에서 해제한다.
            PlayerProfile.InventoryChanged += Refresh;
        }

        private void OnDestroy()
        {
            PlayerProfile.InventoryChanged -= Refresh;
        }

        // 화면을 열 때마다 무기 탭부터, 분해 창·결과 문구는 닫은 상태로 시작한다.
        private void OnEnable()
        {
            dismantlePopup.Close();
            resultText.text = string.Empty;
            ShowTab(Tab.Weapon);
        }

        private void ShowTab(Tab tab)
        {
            currentTab = tab;
            weaponTab.image.color = tab == Tab.Weapon ? selectedTabColor : normalTabColor;
            materialTab.image.color = tab == Tab.Material ? selectedTabColor : normalTabColor;

            // 정렬·일괄 분해는 무기 탭에만 있다.
            sortButton.gameObject.SetActive(tab == Tab.Weapon);
            dismantleButton.gameObject.SetActive(tab == Tab.Weapon);

            selectedWeaponUid = -1;
            selectedMaterial = null;
            Refresh();
        }

        // 지금 탭의 목록을 다시 그린다.
        private void Refresh()
        {
            if (currentTab == Tab.Weapon)
            {
                RefreshWeapons();
            }
            else
            {
                RefreshMaterials();
            }
        }

        private void RefreshWeapons()
        {
            List<OwnedWeapon> weapons = SortWeapons(PlayerProfile.GetOwnedWeapons());
            countText.text = $"보유  <b>{weapons.Count}</b> / {PlayerProfile.WeaponCapacity}";
            sortText.text = $"정렬  {GetSortName(weaponSort)}";

            PrepareSlots(weapons.Count);
            OwnedWeapon selected = null;
            for (int i = 0; i < weapons.Count; i++)
            {
                OwnedWeapon weapon = weapons[i];
                InventorySlotView slot = slots[i];
                slot.ShowWeapon(weapon);
                slot.SetSelected(weapon.Uid == selectedWeaponUid);

                // 칸마다 클릭을 새로 연결한다: 다시 쓰는 칸이라 이전 항목의 클릭을 먼저 지운다.
                slot.Button.onClick.RemoveAllListeners();
                slot.Button.onClick.AddListener(() => SelectWeapon(weapon));

                if (weapon.Uid == selectedWeaponUid)
                {
                    selected = weapon;
                }
            }

            ShowWeaponDetail(selected);
        }

        private void RefreshMaterials()
        {
            List<OwnedMaterial> materials = PlayerProfile.GetOwnedMaterials();

            // Sort(비교 람다): 분류 → 등급 순으로 정렬한다. CompareTo는 앞이 작으면 음수 → 앞에 온다.
            // 재화(골드·보석)는 분류 순서와 상관없이 맨 앞에 둔다 (CategoryOrder가 -1).
            materials.Sort((a, b) =>
            {
                int byCategory = CategoryOrder(a.Data.Category).CompareTo(CategoryOrder(b.Data.Category));
                return byCategory != 0 ? byCategory : a.Data.Grade.CompareTo(b.Data.Grade);
            });

            countText.text = $"재료  <b>{materials.Count}</b>종";

            PrepareSlots(materials.Count);
            OwnedMaterial selected = null;
            for (int i = 0; i < materials.Count; i++)
            {
                OwnedMaterial material = materials[i];
                slots[i].ShowMaterial(material);
                slots[i].SetSelected(material.Data == selectedMaterial);
                slots[i].Button.onClick.RemoveAllListeners();
                slots[i].Button.onClick.AddListener(() => SelectMaterial(material));

                if (material.Data == selectedMaterial)
                {
                    selected = material;
                }
            }

            ShowMaterialDetail(selected);
        }

        // 필요한 칸 수만큼 켜 둔다. 모자라면 원본을 복사해 만들고, 남는 칸은 끈다.
        private void PrepareSlots(int count)
        {
            while (slots.Count < count)
            {
                // Instantiate(원본, 부모): 원본을 복사해 그리드 아래에 붙인다.
                slots.Add(Instantiate(slotTemplate, grid));
            }

            for (int i = 0; i < slots.Count; i++)
            {
                slots[i].gameObject.SetActive(i < count);
            }

            emptyText.SetActive(count == 0);
        }

        // 정렬: 등급순(높은 등급 → 높은 레벨), 레벨순(높은 레벨 → 높은 등급), 획득순(최근에 얻은 것부터 = 개체 번호가 큰 것부터).
        private List<OwnedWeapon> SortWeapons(List<OwnedWeapon> weapons)
        {
            weapons.Sort((a, b) =>
            {
                // b.CompareTo(a): 큰 값이 앞에 오는 내림차순.
                int byGrade = b.Data.Grade.CompareTo(a.Data.Grade);
                int byLevel = b.Level.CompareTo(a.Level);
                int byRecent = b.Uid.CompareTo(a.Uid);

                switch (weaponSort)
                {
                    case WeaponSort.Level:
                        return byLevel != 0 ? byLevel : byGrade != 0 ? byGrade : byRecent;
                    case WeaponSort.Recent:
                        return byRecent;
                    default:
                        return byGrade != 0 ? byGrade : byLevel != 0 ? byLevel : byRecent;
                }
            });

            return weapons;
        }

        // 정렬 버튼: 등급 → 레벨 → 획득 → 등급 … 순서로 넘긴다. (현재 + 1) % 3 = 마지막 다음은 처음으로.
        private void CycleSort()
        {
            weaponSort = (WeaponSort)(((int)weaponSort + 1) % 3);
            RefreshWeapons();
        }

        private static string GetSortName(WeaponSort sort)
        {
            return sort switch
            {
                WeaponSort.Level => "레벨순",
                WeaponSort.Recent => "획득순",
                _ => "등급순",
            };
        }

        private void SelectWeapon(OwnedWeapon weapon)
        {
            selectedWeaponUid = weapon.Uid;
            RefreshWeapons();
        }

        private void SelectMaterial(OwnedMaterial material)
        {
            selectedMaterial = material.Data;
            RefreshMaterials();
        }

        // 무기 상세: 등급·레벨·기초 스탯·장착 캐릭터. 고른 무기가 없으면 패널을 닫는다.
        private void ShowWeaponDetail(OwnedWeapon weapon)
        {
            detailPanel.SetActive(weapon != null);
            if (weapon == null)
            {
                return;
            }

            WeaponData data = weapon.Data;
            ShowDetailHeader(data.Icon, data.DisplayName, (int)data.Grade, $"{EffectText.EnumName(data.Type)}  ·  Lv.{weapon.Level}");

            // 장착 캐릭터 이름: ID로 데이터를 찾고, 못 찾으면 ID 그대로 보여준다.
            string equipped = weapon.IsEquipped
                ? PlayerProfile.Database.FindCharacter(weapon.EquippedTo)?.DisplayName ?? weapon.EquippedTo
                : "없음";

            // 지금 레벨·진급에서 더해 주는 스탯 (최대 스탯 × 성장 비율).
            WeaponStats stats = PlayerProfile.GetWeaponStats(weapon);
            detailInfo.text =
                $"공격력  <b>+{stats.Attack:N0}</b>\n" +
                $"HP  <b>+{stats.MaxHp:N0}</b>\n" +
                $"방어력  <b>+{stats.Defense:N0}</b>\n\n" +
                $"장착  {equipped}";
            detailDescription.text = data.Description;
        }

        // 재료 상세: 분류·보유 개수·설명.
        private void ShowMaterialDetail(OwnedMaterial material)
        {
            detailPanel.SetActive(material != null);
            if (material == null)
            {
                return;
            }

            MaterialData data = material.Data;
            ShowDetailHeader(data.Icon, data.DisplayName, data.Grade, GetCategoryName(data.Category));
            detailIcon.color = data.IconColor;
            detailInfo.text = $"보유  <b>{material.Count:N0}</b>개";
            detailDescription.text = data.Description;
        }

        // 상세 패널 위쪽 공통: 아이콘·이름·★·분류 줄. 테두리는 등급 색.
        private void ShowDetailHeader(Sprite icon, string itemName, int grade, string subtitle)
        {
            Color color = RarityColors.Get(grade);
            detailFrame.color = color;
            detailIcon.sprite = icon;
            detailIcon.color = Color.white;
            detailIcon.enabled = icon != null;
            detailIcon.preserveAspect = true;
            detailName.text = itemName;

            // <color=#RRGGBB>: ColorUtility.ToHtmlStringRGB로 등급 색을 글자색 코드로 바꿔 ★만 그 색으로 칠한다.
            detailGrade.text = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{new string('★', grade)}</color>   {subtitle}";
        }

        private static string GetCategoryName(MaterialCategory category)
        {
            return category switch
            {
                MaterialCategory.Experience => "경험치 재료",
                MaterialCategory.Promotion => "진급 재료",
                MaterialCategory.Currency => "재화",
                _ => "무기 재료",
            };
        }

        // 재료 탭 정렬 순서: 재화 먼저(-1), 나머지는 분류 enum 순서.
        private static int CategoryOrder(MaterialCategory category)
        {
            return category == MaterialCategory.Currency ? -1 : (int)category;
        }

        // 일괄 분해가 끝나면 결과 문구를 띄운다 (목록은 InventoryChanged로 이미 다시 그려졌다).
        private void ShowDismantleResult(DismantleResult result)
        {
            string materialName = result.Material != null ? result.Material.DisplayName : "재료";
            resultText.text = $"무기 {result.WeaponCount}개를 분해해 {materialName} {result.MaterialAmount:N0}개를 얻었습니다";
        }
    }
}
