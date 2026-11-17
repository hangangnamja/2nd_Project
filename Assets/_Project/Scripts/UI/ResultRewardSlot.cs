using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 결과 화면의 보상 칸 한 개: 등급 색 테두리·아래 빛 + 재료 아이콘 + 개수 + 이름 (인벤토리 칸과 같은 등급 색).
    public class ResultRewardSlot : MonoBehaviour
    {
        [SerializeField] private Image frame;

        [Tooltip("칸 아래쪽에서 올라오는 등급 색 빛")]
        [SerializeField] private Image glow;

        [SerializeField] private Image icon;
        [SerializeField] private Text countText;
        [SerializeField] private Text nameText;

        // 재료 하나와 받은 개수를 채운다.
        // 아이콘은 흰 그림을 재료의 아이콘 색으로 물들인다 (골드 황금색, 보석 하늘색 등, 인벤토리와 같음).
        // {amount:N0}: 천 단위 쉼표 (예: 20,000).
        public void Bind(MaterialData material, int amount)
        {
            Color gradeColor = RarityColors.Get(material.Grade);
            frame.color = gradeColor;
            glow.color = new Color(gradeColor.r, gradeColor.g, gradeColor.b, glow.color.a);

            icon.sprite = material.Icon;
            icon.color = material.IconColor;
            icon.enabled = material.Icon != null;

            countText.text = $"×{amount:N0}";
            nameText.text = material.DisplayName;
        }
    }
}
