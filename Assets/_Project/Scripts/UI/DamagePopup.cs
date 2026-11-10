using UnityEngine;

namespace Game.UI
{
    // 피해·회복 숫자 하나: 유닛 머리 위에 떠서 위로 올라가며 사라진다.
    // 빛·불꽃은 파티클(BattleFxView)이 맡고, 이 컴포넌트는 글자 표시만 한다.
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private TextMesh text;

        [Tooltip("떠 있는 시간(초)")]
        [SerializeField, Min(0.1f)] private float lifetime = 0.9f;

        [Tooltip("초당 올라가는 거리")]
        [SerializeField] private float riseSpeed = 1.2f;

        [Tooltip("처음 커졌다가 원래 크기로 돌아오는 시간(초). 치명타처럼 크게 띄울 때 튀는 느낌")]
        [SerializeField, Min(0.01f)] private float popTime = 0.12f;

        private Color baseColor;
        private Vector3 baseScale;
        private float popScale;
        private float elapsed;

        // 글자·색·크기를 정하고 띄운다. scale: 1 = 기본, 치명타는 더 크게.
        public void Show(string message, Color color, float scale)
        {
            text.text = message;
            text.color = color;
            baseColor = color;
            baseScale = transform.localScale * scale;

            // 크게 띄울수록(치명타) 처음에 더 크게 튀었다가 줄어든다.
            popScale = scale > 1f ? 1.5f : 1.2f;
            transform.localScale = baseScale * popScale;

            // 카메라와 같은 방향을 바라보게 해, 어느 위치에 떠도 글자가 정면으로 읽힌다.
            if (Camera.main != null)
            {
                transform.rotation = Camera.main.transform.rotation;
            }
        }

        // 매 프레임: 위로 올라가고, 처음엔 크기가 줄어들며, 후반부에는 투명해진다. 시간이 다 되면 스스로 지운다.
        private void Update()
        {
            elapsed += Time.deltaTime;
            transform.position += Vector3.up * (riseSpeed * Time.deltaTime);

            // Mathf.Lerp(a, b, t): t(0~1)만큼 a에서 b로 섞은 값 → popTime 동안 튄 크기에서 원래 크기로 돌아온다.
            float pop = Mathf.Lerp(popScale, 1f, elapsed / popTime);
            transform.localScale = baseScale * pop;

            // Mathf.InverseLerp(a, b, v): v가 a~b 중 어디인지 0~1로 돌려준다 (범위 밖은 0 또는 1).
            // lifetime → lifetime의 절반 순서로 넣었으므로, 앞쪽 절반은 1(불투명), 뒤쪽 절반 동안 1 → 0으로 옅어진다.
            float fade = Mathf.InverseLerp(lifetime, lifetime * 0.5f, elapsed);
            text.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);

            if (elapsed >= lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}
