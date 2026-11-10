using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 캐릭터 일러스트를 칸에 "꽉 채워" 보여준다 (넘치는 부분은 잘라냄).
    // 편성 칸·가챠 카드처럼 세로로 긴 칸에 정사각형·세로형 그림을 찌그러뜨리지 않고 채우기 위해 쓴다.
    //  - 이 오브젝트: 칸 크기 그대로 + RectMask2D(칸 밖으로 나간 그림을 잘라냄)
    //  - 자식 Image: AspectRatioFitter(EnvelopeParent)가 그림 비율을 유지한 채 칸을 덮는 크기로 키운다
    public class IllustrationView : MonoBehaviour
    {
        [SerializeField] private Image image;
        [SerializeField] private AspectRatioFitter fitter;

        [Tooltip("세로로 남는 부분을 어디서 자를지 (1 = 그림 위쪽을 맞춤 → 얼굴이 보이게, 0.5 = 가운데)")]
        [SerializeField, Range(0f, 1f)] private float verticalFocus = 0.9f;

        [Tooltip("칸을 채운 뒤 더 확대하는 배율 (1 = 칸에 꼭 맞춤). 작은 칸에서 머리 부분만 보여줄 때 키운다")]
        [SerializeField, Min(1f)] private float zoom = 1f;

        // 그림을 보여준다. 그림이 없으면(null) 숨기고 false를 돌려준다 → 부르는 쪽이 임시 표시로 바꿀 수 있다.
        public bool Show(Sprite sprite)
        {
            bool hasSprite = sprite != null;
            gameObject.SetActive(hasSprite);
            if (!hasSprite)
            {
                return false;
            }

            image.sprite = sprite;

            // aspectRatio = 가로 / 세로. EnvelopeParent는 이 비율을 지키면서 부모 칸을 빈틈없이 덮는 크기로 맞춘다.
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;

            // pivot: 칸보다 커진 그림이 어느 쪽 기준으로 놓일지. y가 1이면 위쪽을 칸 위에 맞추고 아래쪽이 잘린다.
            image.rectTransform.pivot = new Vector2(0.5f, verticalFocus);

            // localScale은 pivot을 중심으로 커진다 → pivot이 위쪽이면 그림 위쪽(머리)이 제자리에 남은 채 확대된다.
            image.rectTransform.localScale = Vector3.one * zoom;
            return true;
        }
    }
}
