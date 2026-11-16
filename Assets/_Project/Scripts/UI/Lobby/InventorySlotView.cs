using Game.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 인벤토리 그리드의 칸 하나 (무기 1개 또는 재료 1종).
    //  - 테두리·아래쪽 빛을 등급 색(3성 파랑 / 4성 보라 / 5성 황금빛, 가챠와 같은 색)으로 칠한다
    //  - 아래 글자: 무기는 레벨, 재료는 개수. 장착 중인 무기는 "E" 표시
    // 칸을 만들고 클릭을 연결하는 것은 InventoryView가 하고, 이 컴포넌트는 내용 표시만 한다.
    public class InventorySlotView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frame;

        [Tooltip("칸 아래에서 위로 옅어지는 등급 색 빛")]
        [SerializeField] private Image glow;

        [SerializeField] private Image icon;
        [SerializeField] private Text starsText;

        [Tooltip("무기 = Lv, 재료 = 개수")]
        [SerializeField] private Text bottomText;

        [Tooltip("장착 중 표시 (무기만)")]
        [SerializeField] private GameObject equippedTag;

        [Tooltip("지금 고른 칸 표시 (밝은 테두리)")]
        [SerializeField] private GameObject selectedMark;

        public Button Button => button;

        // 무기 개체 하나를 보여준다. 등급 = 무기 성급.
        public void ShowWeapon(OwnedWeapon weapon)
        {
            ShowCommon(weapon.Data.Icon, Color.white, (int)weapon.Data.Grade);
            bottomText.text = $"Lv.{weapon.Level}";
            equippedTag.SetActive(weapon.IsEquipped);
        }

        // 재료 한 종류를 보여준다. 개수는 천 단위 쉼표(N0 서식: 1,234).
        public void ShowMaterial(OwnedMaterial material)
        {
            ShowCommon(material.Data.Icon, material.Data.IconColor, material.Data.Grade);
            bottomText.text = $"×{material.Count:N0}";
            equippedTag.SetActive(false);
        }

        public void SetSelected(bool selected)
        {
            selectedMark.SetActive(selected);
        }

        // 무기·재료 공통: 아이콘(iconColor로 물들임)과 등급 색, ★ 개수.
        private void ShowCommon(Sprite sprite, Color iconColor, int grade)
        {
            Color color = RarityColors.Get(grade);
            frame.color = color;

            // 빛은 은은하게: 등급 색의 알파만 낮춰 쓴다.
            glow.color = new Color(color.r, color.g, color.b, 0.45f);

            icon.sprite = sprite;
            icon.color = iconColor;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;

            // new string(문자, 개수): ★을 등급 숫자만큼 이어 붙인다.
            starsText.text = new string('★', grade);
            starsText.color = Color.Lerp(color, Color.white, 0.4f);
        }
    }

    // 등급(3~5성) 색. 가챠 결과 카드(GachaPresenter의 등급 색)와 같은 값을 써서 화면끼리 색 규칙을 맞춘다.
    public static class RarityColors
    {
        public static readonly Color ThreeStar = new Color(0.3f, 0.6f, 1f);
        public static readonly Color FourStar = new Color(0.72f, 0.38f, 1f);
        public static readonly Color FiveStar = new Color(1f, 0.78f, 0.3f);

        // switch 식: 등급 숫자에 맞는 색을 돌려준다. 5 이상은 황금빛, 4는 보라, 나머지는 파랑.
        public static Color Get(int grade)
        {
            return grade switch
            {
                >= 5 => FiveStar,
                4 => FourStar,
                _ => ThreeStar,
            };
        }
    }
}
