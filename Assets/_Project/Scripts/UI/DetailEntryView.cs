using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 상세 창 목록의 카드 한 장 (상태효과 하나, 스킬 하나, 패시브 하나).
    // 바탕 판 + (있으면) 아이콘 + 설명 글. 카드 높이는 글 길이에 맞춰 늘어난다 (레이아웃 그룹 + ContentSizeFitter).
    // UnitDetailPanel이 씬에 놓인 원본 카드를 복사해 목록마다 채운다.
    public class DetailEntryView : MonoBehaviour
    {
        [SerializeField] private Image background;

        [Tooltip("상태효과 아이콘 자리. 스킬·패시브 카드는 숨긴다")]
        [SerializeField] private RawImage icon;

        [SerializeField] private Text text;

        // 카드 내용을 채운다. iconTexture가 null이면 아이콘 자리를 숨겨 글이 왼쪽부터 시작한다.
        public void Set(Texture iconTexture, string content, Color tint)
        {
            icon.texture = iconTexture;
            icon.gameObject.SetActive(iconTexture != null);
            text.text = content;
            background.color = tint;
        }
    }
}
