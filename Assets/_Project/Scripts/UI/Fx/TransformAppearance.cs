using UnityEngine;

namespace Game.UI
{
    // 변신 모습(헤일로·날개)의 움직임 (변신 데이터의 "변신 모습" 프리팹에 붙인다, 임시).
    //  - 나타날 때: 0에서 살짝 넘치게 커졌다가 제 크기로 (펼쳐지는 느낌)
    //  - 평소: 헤일로는 천천히 돌고, 날개는 위아래로 천천히 펄럭이며, 전체가 숨 쉬듯 살짝 오르내린다
    // 모양(헤일로 고리·광선·날개깃)은 자식 오브젝트·재질이 담당하고, 이 코드는 움직임만 정한다.
    public class TransformAppearance : MonoBehaviour
    {
        [Tooltip("천천히 도는 헤일로 (자기 z축 기준)")]
        [SerializeField] private Transform halo;

        [SerializeField] private float haloSpinDegrees = 20f;

        [Tooltip("헤일로 바깥 불꽃 갈래 (안쪽 왕관과 반대 방향으로 돌아 일렁이는 느낌)")]
        [SerializeField] private Transform haloOuter;

        [SerializeField] private float haloOuterSpinDegrees = -12f;

        [Tooltip("왼쪽·오른쪽 날개 (각 날개의 뿌리가 원점이 되게 둔다)")]
        [SerializeField] private Transform leftWing;
        [SerializeField] private Transform rightWing;

        [Tooltip("날개가 펄럭이는 각도와 빠르기")]
        [SerializeField] private float flapDegrees = 6f;
        [SerializeField] private float flapSpeed = 1.6f;

        [Tooltip("전체가 오르내리는 높이와 빠르기")]
        [SerializeField] private float bobHeight = 0.05f;
        [SerializeField] private float bobSpeed = 1.2f;

        [Header("날개로 감싸기 (반격 태세 연출)")]
        [Tooltip("다 감쌌을 때 날개를 앞으로 접는 각도 (세로축 기준, 왼쪽은 +, 오른쪽은 -로 거울 대칭). 90 근처면 날개가 옆에서 보이는 칼날처럼 얇아지므로 크게 넘겨 몸 앞에서 엇갈리게 한다")]
        [SerializeField] private float wrapYaw = 150f;

        [Tooltip("다 감쌌을 때 날개 끝을 아래로 내리는 각도")]
        [SerializeField] private float wrapDroop = 20f;

        [Tooltip("다 감쌌을 때 날개 길이를 줄이는 비율 (0.5 = 절반 → 몸에 붙어 감싼다)")]
        [SerializeField, Range(0f, 0.9f)] private float wrapShrink = 0.5f;

        [Tooltip("다 감쌌을 때 날개 뿌리를 옮기는 거리 (앞 +z: 몸 앞으로 나와야 몸에 가리지 않는다). 오른쪽 날개는 겹침 깜빡임을 막으려고 조금 더 앞")]
        [SerializeField] private Vector3 wrapOffset = new Vector3(0f, -0.25f, 1f);

        [Tooltip("나타나는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float appearSeconds = 0.45f;

        private Vector3 basePosition;
        private Vector3 baseScale;
        private Quaternion leftBase;
        private Quaternion rightBase;
        private Vector3 leftBasePosition;
        private Vector3 rightBasePosition;
        private float elapsed;

        // 날개로 감싼 정도 (0 = 평소, 1 = 다 감쌈). 반격 태세 연출(WingGuardFx)이 SetWrap으로 바꾼다.
        private float wrap;

        // 날개로 몸을 감싼 정도를 정한다 (0~1). 감쌀수록 펄럭임은 잦아든다.
        public void SetWrap(float amount)
        {
            wrap = Mathf.Clamp01(amount);
        }

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseScale = transform.localScale;
            leftBase = leftWing != null ? leftWing.localRotation : Quaternion.identity;
            rightBase = rightWing != null ? rightWing.localRotation : Quaternion.identity;
            leftBasePosition = leftWing != null ? leftWing.localPosition : Vector3.zero;
            rightBasePosition = rightWing != null ? rightWing.localPosition : Vector3.zero;
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;

            // 나타나기: 진행도 t(0~1)를 1 + 0.25 × sin(πt) 배 → 중간에 25% 넘쳤다가 끝에서 정확히 1.
            float t = Mathf.Clamp01(elapsed / appearSeconds);
            float overshoot = 1f + 0.25f * Mathf.Sin(t * Mathf.PI);
            transform.localScale = baseScale * (t * overshoot);

            // 숨 쉬듯 오르내림: Mathf.Sin(시간 × 빠르기) = -1~1을 높이에 곱한다.
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(elapsed * bobSpeed) * bobHeight);

            if (halo != null)
            {
                // Rotate(0, 0, 각도, Space.Self): 헤일로 자기 앞뒤 축으로 돌린다 (고리 면 안에서 회전).
                halo.Rotate(0f, 0f, haloSpinDegrees * Time.deltaTime, Space.Self);
            }

            if (haloOuter != null)
            {
                haloOuter.Rotate(0f, 0f, haloOuterSpinDegrees * Time.deltaTime, Space.Self);
            }

            // 날개 펄럭임: 양쪽을 거울처럼 반대 방향(±)으로 자기 z축 기준 같은 각도만큼 돌린다. 감쌀수록 (1 - wrap) 줄어든다.
            float flap = Mathf.Sin(elapsed * flapSpeed) * flapDegrees * (1f - wrap);

            // 감싸기: 원래 자세 앞에 곱한 Euler(0, ±각도, ±내림)은 부모(몸) 기준 세로축으로 날개를 앞으로 접는다.
            // 날개 뿌리의 가로 크기를 줄여 날개가 몸 가까이 붙게 한다.
            Quaternion leftFold = Quaternion.Euler(0f, wrapYaw * wrap, wrapDroop * wrap);
            Quaternion rightFold = Quaternion.Euler(0f, -wrapYaw * wrap, -wrapDroop * wrap);
            Vector3 foldScale = new Vector3(1f - wrapShrink * wrap, 1f, 1f);
            if (leftWing != null)
            {
                leftWing.localRotation = leftFold * leftBase * Quaternion.Euler(0f, 0f, flap);
                leftWing.localScale = foldScale;
                leftWing.localPosition = leftBasePosition + wrapOffset * wrap;
            }

            if (rightWing != null)
            {
                rightWing.localRotation = rightFold * rightBase * Quaternion.Euler(0f, 0f, -flap);
                rightWing.localScale = foldScale;
                rightWing.localPosition = rightBasePosition + (wrapOffset + Vector3.forward * 0.05f) * wrap;
            }
        }
    }
}
