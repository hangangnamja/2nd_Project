using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 스테이지 상세 패널의 보상 칸 하나: 등급 색 테두리 + 재료 아이콘 + 개수, 첫 클리어 보상이면 위에 작은 꼬리표.
    // 이미 받은 첫 클리어 보상은 어둡게 덮고 꼬리표를 "획득 완료"로 바꾼다.
    public class StageRewardSlot : MonoBehaviour
    {
        [SerializeField] private Image frame;
        [SerializeField] private Image icon;
        [SerializeField] private Text countText;

        [Tooltip("첫 클리어 꼬리표 (배경 + 글자). 매번 받는 보상이면 숨긴다")]
        [SerializeField] private GameObject tag;
        [SerializeField] private Image tagBackground;
        [SerializeField] private Text tagText;

        [Tooltip("이미 받은 보상을 어둡게 덮는 판")]
        [SerializeField] private GameObject claimedCover;

        [Header("꼬리표 색")]
        [SerializeField] private Color firstClearColor = new Color(1f, 0.78f, 0.3f);
        [SerializeField] private Color claimedColor = new Color(0.35f, 0.38f, 0.45f);

        // 재료·개수를 채운다. isFirstClear: 첫 클리어 보상인지, claimed: 그 첫 클리어 보상을 이미 받았는지.
        // 아이콘은 흰 그림을 재료의 아이콘 색으로 물들인다 (골드 황금색, 보석 하늘색 — 인벤토리와 같음). {N0}: 천 단위 쉼표.
        public void Bind(MaterialData material, int amount, bool isFirstClear, bool claimed)
        {
            frame.color = RarityColors.Get(material.Grade);
            icon.sprite = material.Icon;
            icon.color = material.IconColor;
            icon.enabled = material.Icon != null;
            countText.text = $"×{amount:N0}";

            tag.SetActive(isFirstClear);
            if (isFirstClear)
            {
                tagText.text = claimed ? "획득 완료" : "첫 클리어";
                tagBackground.color = claimed ? claimedColor : firstClearColor;
            }

            claimedCover.SetActive(isFirstClear && claimed);
        }
    }
}
