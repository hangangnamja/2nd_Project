using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투원 화면 오른쪽 보유 캐릭터 목록의 칸 하나 (에픽세븐식 세로 목록).
    // 일러스트 머리 부분 + 이름 + Lv·등급. 고른 칸은 왼쪽으로 튀어나오고 강조 막대가 켜진다.
    // OperatorView가 씬에 놓인 원본 칸을 복사해 보유 캐릭터마다 채운다.
    public class OperatorListItem : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("튀어나오는 부분 (칸 내용 전체의 부모). 고르면 왼쪽으로 밀린다")]
        [SerializeField] private RectTransform body;

        [SerializeField] private Image background;
        [SerializeField] private IllustrationView illustration;

        [Tooltip("일러스트가 없을 때 보여줄 직군 색 판")]
        [SerializeField] private Image fallback;

        [SerializeField] private Text nameText;
        [SerializeField] private Text infoText;

        [Tooltip("고른 칸에만 켜지는 강조 막대")]
        [SerializeField] private GameObject selectedAccent;

        [Tooltip("고른 칸이 왼쪽으로 튀어나오는 거리 (픽셀)")]
        [SerializeField] private float selectedOffset = 28f;

        [SerializeField] private Color normalColor = new Color(0.06f, 0.08f, 0.12f, 0.9f);
        [SerializeField] private Color selectedColor = new Color(0.1f, 0.2f, 0.32f, 0.98f);

        public Button Button => button;

        // 칸 내용을 채운다. 일러스트가 없으면 직군 색 판으로 대신한다.
        public void Set(Sprite art, Color roleColor, string displayName, string info)
        {
            bool hasArt = illustration.Show(art);
            fallback.gameObject.SetActive(!hasArt);
            fallback.color = roleColor;
            nameText.text = displayName;
            infoText.text = info;
        }

        // 고른 칸인지 표시한다: 바탕색·강조 막대를 바꾸고 내용 전체를 왼쪽으로 민다.
        public void SetSelected(bool selected)
        {
            background.color = selected ? selectedColor : normalColor;
            selectedAccent.SetActive(selected);
            body.anchoredPosition = new Vector2(selected ? -selectedOffset : 0f, body.anchoredPosition.y);
        }
    }
}
