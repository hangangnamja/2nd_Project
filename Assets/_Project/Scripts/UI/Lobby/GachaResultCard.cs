using Game.Outgame;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 가챠 결과 카드 한 장의 표시 (한 장씩 공개하는 큰 카드, 마지막 결과 목록의 작은 카드가 같이 쓴다).
    //  - 등급 색: 테두리와 그라데이션 배경을 3성 파랑 / 4성 보라 / 5성 황금빛으로 칠한다
    //  - 덮개(cover): 4·5성을 공개하기 전에 내용을 가리는 판. 공개 연출이 끝나면 걷어낸다 (작은 카드는 덮개 없음)
    // 빛·불꽃 같은 효과는 이 카드가 아니라 GachaPresenter의 파티클이 맡는다.
    public class GachaResultCard : MonoBehaviour
    {
        [SerializeField] private Image frame;

        [Tooltip("아래에서 위로 옅어지는 그라데이션 배경")]
        [SerializeField] private Image gradient;

        [Tooltip("일러스트가 없을 때의 그림 자리 / 3성이면 칼 그림")]
        [SerializeField] private Image icon;

        [Tooltip("캐릭터 일러스트 (카드를 꽉 채움). 일러스트가 있으면 icon 대신 이걸 보여준다")]
        [SerializeField] private IllustrationView illustration;

        [SerializeField] private Text nameText;
        [SerializeField] private Text starsText;
        [SerializeField] private GameObject newTag;
        [SerializeField] private GameObject pickupTag;

        [Header("덮개 (공개 전, 없으면 비워 둔다)")]
        [SerializeField] private GameObject cover;
        [SerializeField] private Image coverGradient;

        [Header("그림")]
        [Tooltip("무기 아이콘이 없을 때 쓰는 임시 그림 (칼)")]
        [SerializeField] private Sprite weaponSprite;

        // 실제 등급 색 (덮개를 걷을 때 테두리를 이 색으로 되돌린다)
        private Color rarityColor;

        // 결과 내용을 채운다. color = 이 결과의 등급 색.
        public void Show(GachaResult result, Color color)
        {
            bool isWeapon = result.Character == null;

            rarityColor = color;
            frame.color = color;
            gradient.color = color;

            // 캐릭터 일러스트가 있으면 카드를 그림으로 채우고, 작은 그림 자리(icon)는 숨긴다.
            // ?.: 무기(Character = null)면 Illustration을 읽지 않고 null → 일러스트 없음으로 처리된다.
            bool hasIllustration = illustration != null && illustration.Show(result.Character?.Illustration);
            icon.gameObject.SetActive(!hasIllustration);

            // 3성은 무기 아이콘(없으면 임시 칼 그림)을 그대로, 일러스트가 없는 캐릭터는 옅은 빈 칸으로 둔다.
            // ?. + ??: 무기 데이터나 아이콘이 없으면 임시 칼 그림을 쓴다.
            icon.sprite = isWeapon ? result.Weapon?.Icon ?? weaponSprite : null;
            icon.color = isWeapon ? Color.white : new Color(1f, 1f, 1f, 0.15f);
            icon.preserveAspect = true;

            nameText.text = isWeapon ? BuildWeaponName(result) : result.Character.DisplayName;

            // new string(문자, 개수): 같은 문자를 개수만큼 이어 붙인 문자열 → 등급 숫자만큼 ★
            starsText.text = new string('★', (int)result.Rarity);

            // 배경 그라데이션과 같은 색이면 묻히므로 흰색 쪽으로 60% 섞어 밝게 한다.
            starsText.color = Color.Lerp(color, Color.white, 0.6f);

            newTag.SetActive(result.IsNew);
            pickupTag.SetActive(result.IsPickup);
        }

        // 무기 이름. 무기 칸이 꽉 차 자동 분해됐으면 아랫줄에 표시한다 (리치 텍스트 <color>, <size>로 붉은 작은 글자).
        private string BuildWeaponName(GachaResult result)
        {
            string weaponName = result.Weapon != null ? result.Weapon.DisplayName : "3성 무기";
            if (!result.IsDismantled)
            {
                return weaponName;
            }

            nameText.supportRichText = true;
            int smallSize = Mathf.Max(10, Mathf.RoundToInt(nameText.fontSize * 0.7f));
            return $"{weaponName}\n<size={smallSize}><color=#FF8A7A>자동 분해</color></size>";
        }

        // 공개 전 덮개를 씌운다. 덮개 색은 "보여줄 등급" 색이라, 반전 연출이면 실제 등급과 다를 수 있다.
        public void Hide(Color shownColor)
        {
            cover.SetActive(true);
            SetCoverColor(shownColor);
        }

        // 덮개와 테두리를 그 색으로 칠한다. 테두리도 바꾸는 이유: 실제 등급 색이 보이면 반전 연출이 미리 들키기 때문.
        // 반전 연출에서는 보라색이던 것을 황금빛으로 바꾸는 데 쓴다.
        public void SetCoverColor(Color color)
        {
            coverGradient.color = color;
            frame.color = color;
        }

        // 덮개를 걷어 내용을 보여준다.
        public void Reveal()
        {
            if (cover != null)
            {
                cover.SetActive(false);
            }

            frame.color = rarityColor;
        }
    }
}
