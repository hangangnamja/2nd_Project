using UnityEngine;

namespace Game.UI
{
    // 붙은 오브젝트(이름표, 상태효과 블럭, 대상 표시 고리)가 항상 카메라를 정면으로 보게 한다.
    // 카메라가 캐릭터마다 다른 각도로 움직이므로, 글자·아이콘이 비스듬히 보이거나 뒤집히지 않게 하기 위함.
    // keepScreenSize를 켜면 카메라와의 거리에 비례해 크기를 바꿔, 가까운 아군 이름표가 화면을 덮거나 먼 적 이름표가 너무 작아지지 않게 한다.
    public class Billboard : MonoBehaviour
    {
        [Tooltip("체크하면 카메라 거리와 상관없이 화면에서 비슷한 크기로 보이게 한다")]
        [SerializeField] private bool keepScreenSize = true;

        [Tooltip("원래 크기(1배)로 보일 카메라 거리")]
        [SerializeField, Min(0.1f)] private float referenceDistance = 10f;

        [Tooltip("크기 배율의 최소·최대 (너무 작거나 커지지 않게)")]
        [SerializeField] private Vector2 scaleRange = new Vector2(0.12f, 1.6f);

        // 처음 크기. 거리 배율은 이 크기에 곱한다.
        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        // LateUpdate: 모든 Update와 카메라 이동이 끝난 뒤에 불려, 이번 프레임의 최종 카메라를 기준으로 맞춘다.
        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            // 카메라와 같은 회전을 쓰면 화면과 평행한 판이 되어 언제나 정면으로 보인다.
            transform.rotation = mainCamera.transform.rotation;

            if (keepScreenSize)
            {
                // 원근 카메라에서는 거리가 2배면 2배 작게 보이므로, 거리 / 기준 거리만큼 키우면 화면 크기가 거의 같아진다.
                // Mathf.Clamp: 그 배율을 정한 범위 안으로 자른다.
                float distance = Vector3.Distance(mainCamera.transform.position, transform.position);
                float scale = Mathf.Clamp(distance / referenceDistance, scaleRange.x, scaleRange.y);
                transform.localScale = baseScale * scale;
            }
        }
    }
}
