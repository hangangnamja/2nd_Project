using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 확률 공시 창의 표 한 줄: 등급 색 막대 + 이름(+ 꼬리표) + 기본 확률 + 최종 확률.
    // 등급별 표와 개별 확률 목록이 같은 줄 모양을 쓴다 (씬의 꺼 둔 원본 줄을 복사해 채움).
    public class GachaRateRowView : MonoBehaviour
    {
        [SerializeField] private Image colorBar;
        [SerializeField] private Text label;

        [Tooltip("이름 옆 꼬리표 (PICK UP 등). 비우면 숨긴다")]
        [SerializeField] private Text tagLabel;

        [SerializeField] private Text baseRate;
        [SerializeField] private Text finalRate;

        // 줄 내용을 채운다. 꼬리표 글자가 비어 있으면 꼬리표를 숨긴다.
        public void Set(Color color, string labelText, string tagText, string baseText, string finalText)
        {
            colorBar.color = color;
            label.text = labelText;
            baseRate.text = baseText;
            finalRate.text = finalText;

            if (tagLabel != null)
            {
                bool hasTag = !string.IsNullOrEmpty(tagText);
                tagLabel.gameObject.SetActive(hasTag);
                tagLabel.text = tagText;
            }
        }
    }
}
