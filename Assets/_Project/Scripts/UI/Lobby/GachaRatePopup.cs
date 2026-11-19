using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Outgame;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 확률 공시 창 (기획서 2장·7장): 모집 화면의 "확률 공시" 버튼으로 연다.
    //  - 왼쪽: 등급별 기본 확률 / 최종 확률 표 + 천장·확정 규칙 + 평균 횟수
    //  - 오른쪽: 캐릭터·무기 하나하나의 기본 / 최종 확률 목록 (지금 배너의 픽업 표시)
    // 수치는 전부 GachaRulesData에서 계산해, 규칙 에셋 수치를 바꾸면 공시도 함께 바뀐다.
    public class GachaRatePopup : MonoBehaviour
    {
        [SerializeField] private Text bannerText;

        [Header("등급별 확률")]
        [Tooltip("등급별 표의 원본 줄 (꺼 둔 채로 두고 복사해 씀)")]
        [SerializeField] private GachaRateRowView gradeRowTemplate;

        [SerializeField] private Text rulesText;
        [SerializeField] private Text averageText;

        [Header("개별 확률")]
        [Tooltip("개별 확률 목록의 원본 줄 (꺼 둔 채로 두고 복사해 씀)")]
        [SerializeField] private GachaRateRowView itemRowTemplate;

        [SerializeField] private ScrollRect itemScroll;

        [Header("닫기")]
        [SerializeField] private Button closeButton;

        [Tooltip("창 바깥 어두운 바탕 (누르면 닫힘)")]
        [SerializeField] private Button backdropButton;

        private readonly List<GachaRateRowView> gradeRows = new List<GachaRateRowView>();
        private readonly List<GachaRateRowView> itemRows = new List<GachaRateRowView>();

        private void Awake()
        {
            closeButton.onClick.AddListener(Close);
            backdropButton.onClick.AddListener(Close);
            gradeRowTemplate.gameObject.SetActive(false);
            itemRowTemplate.gameObject.SetActive(false);
        }

        // 지금 배너 기준으로 표를 채우고 창을 연다. 픽업 캐릭터가 배너마다 달라 열 때마다 다시 계산한다.
        public void Open(GachaBannerData banner)
        {
            GachaRulesData rules = GachaService.Rules;
            GameDatabase database = PlayerProfile.Database;

            List<CharacterData> sCharacters = database.GetCharactersOfGrade(CharacterGrade.S);
            CharacterData pickup = banner != null ? banner.PickupCharacter : null;

            // FindAll(람다): 조건이 참인 것만 모은 새 목록 → 픽업이 아닌 S들 (GachaMachine과 같은 구분)
            List<CharacterData> otherS = sCharacters.FindAll(character => character != pickup);
            GachaRateTable table = GachaRateCalculator.Calculate(rules, otherS.Count);

            gameObject.SetActive(true);
            bannerText.text = pickup != null
                ? $"{banner.BannerName}  ·  픽업 <color=#FFC84D>{pickup.DisplayName}</color>"
                : banner?.BannerName ?? string.Empty;

            ShowGradeRows(table);
            ShowRules(rules, table);
            ShowItemRows(table, pickup, otherS, database);

            // 목록을 맨 위부터 보이게 한다 (verticalNormalizedPosition 1 = 맨 위, 0 = 맨 아래).
            itemScroll.verticalNormalizedPosition = 1f;
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        // 등급별 표: S 캐릭터 → A 캐릭터 → 5·4·3성 무기 (뽑기 판정 구간 순서와 같음)
        private void ShowGradeRows(GachaRateTable table)
        {
            var rows = new List<(Color color, string name, GachaRateCategory category)>
            {
                (RarityColors.FiveStar, "S 캐릭터", GachaRateCategory.SCharacter),
                (RarityColors.FourStar, "A 캐릭터", GachaRateCategory.ACharacter),
                (RarityColors.FiveStar, "5성 무기", GachaRateCategory.FiveStarWeapon),
                (RarityColors.FourStar, "4성 무기", GachaRateCategory.FourStarWeapon),
                (RarityColors.ThreeStar, "3성 무기", GachaRateCategory.ThreeStarWeapon),
            };

            for (int i = 0; i < rows.Count; i++)
            {
                GachaRateRowView row = GetRow(gradeRows, gradeRowTemplate, i);
                row.Set(rows[i].color, rows[i].name, null,
                    FormatPercent(table.GetBaseRate(rows[i].category)),
                    FormatPercent(table.GetFinalRate(rows[i].category)));
            }

            HideExtraRows(gradeRows, rows.Count);
        }

        // 천장·확정 규칙 문장과 평균 횟수. 수치는 규칙 에셋에서 읽어 문장을 따로 고치지 않게 한다.
        private void ShowRules(GachaRulesData rules, GachaRateTable table)
        {
            int softStep = Mathf.RoundToInt(rules.SoftPityStep * 100f);
            int pickupChance = Mathf.RoundToInt(rules.PickupChance * 100f);

            rulesText.text =
                $"· S 천장: S 없이 <b>{rules.HardPity}회째</b> S 확정. {rules.SoftPityStart}회째부터 S 확률이 매 회 +{softStep}%p\n" +
                $"· A 확정: A 이상 캐릭터 없이 <b>{rules.AGuaranteeInterval}회째</b> A 캐릭터 확정 (무기는 포함하지 않음)\n" +
                $"· 픽업: S가 나오면 {pickupChance}% 확률로 픽업. 픽업이 아니었다면 다음 S는 픽업 확정\n" +
                "· 천장·확정 횟수와 픽업 확정은 다음 배너로 이어짐. 무기에는 천장 없음\n" +
                "· 최종 확률 = 천장·확정까지 포함해 많이 뽑았을 때 실제로 나오는 비율";

            // F0: 소수점 없이 반올림 (61.4 → 61)
            averageText.text =
                $"S 1개 평균 <b>약 {table.AveragePullsPerS:F0}회</b>   ·   픽업 1개 평균 <b>약 {table.AveragePullsPerPickup:F0}회</b>";
        }

        // 개별 확률: 픽업 S → 다른 S → A 캐릭터 → 5·4·3성 무기. 같은 구간 안에서는 균등 랜덤이라 구간 확률 ÷ 개수.
        // S는 반천 때문에 픽업과 나머지의 몫이 다르다: 픽업 = S 확률 × 픽업 비율, 나머지 S 하나 = S 확률 × (1 - 비율) ÷ 나머지 수.
        private void ShowItemRows(GachaRateTable table, CharacterData pickup, List<CharacterData> otherS, GameDatabase database)
        {
            int index = 0;
            double sBase = table.GetBaseRate(GachaRateCategory.SCharacter);
            double sFinal = table.GetFinalRate(GachaRateCategory.SCharacter);

            if (pickup != null)
            {
                SetItemRow(index++, RarityColors.FiveStar, pickup.DisplayName, "PICK UP",
                    sBase * table.BasePickupShare, sFinal * table.FinalPickupShare);
            }

            foreach (CharacterData character in otherS)
            {
                SetItemRow(index++, RarityColors.FiveStar, character.DisplayName, null,
                    sBase * (1.0 - table.BasePickupShare) / otherS.Count,
                    sFinal * (1.0 - table.FinalPickupShare) / otherS.Count);
            }

            List<CharacterData> aCharacters = database.GetCharactersOfGrade(CharacterGrade.A);
            foreach (CharacterData character in aCharacters)
            {
                SetItemRow(index++, RarityColors.FourStar, character.DisplayName, null,
                    table.GetBaseRate(GachaRateCategory.ACharacter) / aCharacters.Count,
                    table.GetFinalRate(GachaRateCategory.ACharacter) / aCharacters.Count);
            }

            index = AddWeaponRows(index, table, database, WeaponGrade.FiveStar, GachaRateCategory.FiveStarWeapon);
            index = AddWeaponRows(index, table, database, WeaponGrade.FourStar, GachaRateCategory.FourStarWeapon);
            index = AddWeaponRows(index, table, database, WeaponGrade.ThreeStar, GachaRateCategory.ThreeStarWeapon);

            HideExtraRows(itemRows, index);
        }

        // 한 등급의 무기 줄들을 채우고 다음 줄 번호를 돌려준다.
        private int AddWeaponRows(int index, GachaRateTable table, GameDatabase database, WeaponGrade grade, GachaRateCategory category)
        {
            List<WeaponData> weapons = database.GetWeaponsOfGrade(grade);
            foreach (WeaponData weapon in weapons)
            {
                SetItemRow(index++, RarityColors.Get((int)grade), weapon.DisplayName, null,
                    table.GetBaseRate(category) / weapons.Count,
                    table.GetFinalRate(category) / weapons.Count);
            }

            return index;
        }

        private void SetItemRow(int index, Color color, string name, string tagText, double baseRate, double finalRate)
        {
            GetRow(itemRows, itemRowTemplate, index).Set(color, name, tagText, FormatPercent(baseRate), FormatPercent(finalRate));
        }

        // index번 줄을 돌려준다. 아직 없으면 원본을 복사해 같은 부모 아래에 만들어 두고, 다음에 열 때 재사용한다.
        private static GachaRateRowView GetRow(List<GachaRateRowView> rows, GachaRateRowView template, int index)
        {
            while (rows.Count <= index)
            {
                GachaRateRowView row = Instantiate(template, template.transform.parent);
                rows.Add(row);
            }

            rows[index].gameObject.SetActive(true);
            return rows[index];
        }

        // 이번에 쓰지 않은 줄은 숨긴다 (배너를 바꿔도 줄 수는 같지만, 데이터가 줄었을 때를 대비).
        private static void HideExtraRows(List<GachaRateRowView> rows, int usedCount)
        {
            for (int i = usedCount; i < rows.Count; i++)
            {
                rows[i].gameObject.SetActive(false);
            }
        }

        // 비율(0.0164)을 백분율 글자(1.64%)로. "0.###": 소수 셋째 자리까지, 끝의 0은 지움 (0.6% / 79.4% / 1.635%).
        private static string FormatPercent(double rate)
        {
            return (rate * 100.0).ToString("0.###") + "%";
        }
    }
}
