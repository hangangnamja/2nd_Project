using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    // 가챠 연출의 기어: 손가락(마우스)으로 잡고 시계 방향으로 돌리면 진행도가 0 → 1로 찬다.
    // 이 컴포넌트는 돌지 않는 "잡는 영역"에 붙이고, 실제로 도는 기어 그림(gear)은 따로 둔다.
    //  → 잡는 영역까지 돌면 포인터 각도를 재는 기준이 같이 돌아가 각도 계산이 틀어지기 때문.
    // IBeginDragHandler·IDragHandler: EventSystem이 이 오브젝트 위에서 드래그가 시작·진행될 때 아래 함수를 호출한다.
    public class GachaGearDial : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [Tooltip("드래그에 따라 회전하는 물체 (UI 그림 또는 3D 물레 바퀴). 자기 z축을 중심으로 돈다")]
        [SerializeField] private Transform gear;

        [Tooltip("다 돌렸다고 볼 누적 회전 각도 (360 = 한 바퀴)")]
        [SerializeField, Min(1f)] private float requiredDegrees = 540f;

        private RectTransform area;
        private float lastPointerAngle;
        private float accumulatedDegrees;
        private bool isLocked;

        // 0(시작) ~ 1(다 돌림)
        public float Progress => accumulatedDegrees / requiredDegrees;
        public bool IsComplete => accumulatedDegrees >= requiredDegrees;

        // 이번 드래그로 돌린 각도(도)와 새 진행도를 알린다. 연출이 색·불꽃 양을 바꾸는 데 쓴다.
        public event Action<float, float> Rotated;

        private void Awake()
        {
            area = (RectTransform)transform;
        }

        // 새 뽑기 연출을 시작할 때: 진행도와 기어 각도를 처음으로 되돌리고 다시 돌릴 수 있게 한다.
        public void ResetDial()
        {
            accumulatedDegrees = 0f;
            isLocked = false;
            gear.localEulerAngles = Vector3.zero;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            lastPointerAngle = GetPointerAngle(eventData);
        }

        // 포인터가 기어 중심을 기준으로 몇 도 움직였는지 재서, 시계 방향으로 움직인 만큼만 진행도에 더한다.
        public void OnDrag(PointerEventData eventData)
        {
            if (isLocked)
            {
                return;
            }

            float angle = GetPointerAngle(eventData);

            // Mathf.DeltaAngle: 두 각도의 차이를 -180~180으로 돌려준다 (359° → 1°를 -358°가 아니라 +2°로 계산).
            float delta = Mathf.DeltaAngle(lastPointerAngle, angle);
            lastPointerAngle = angle;

            // 각도는 반시계 방향이 +라서, 시계 방향으로 돌리면 delta가 음수다. 반시계로 돌리면 무시한다.
            if (delta >= 0f)
            {
                return;
            }

            float turned = Mathf.Min(-delta, requiredDegrees - accumulatedDegrees);
            accumulatedDegrees += turned;

            // 바퀴를 누적 각도만큼 시계 방향(z 음수)으로 돌린다. 3D 바퀴면 비스듬히 놓인 바퀴 면 안에서 돈다.
            gear.localEulerAngles = new Vector3(0f, 0f, -accumulatedDegrees);
            isLocked = IsComplete;

            // ?.Invoke: 구독한 연출이 있을 때만 돌린 각도와 진행도를 전달한다.
            Rotated?.Invoke(turned, Progress);
        }

        // 기어 중심에서 본 포인터의 각도 (오른쪽 0°, 위 90°).
        // ScreenPointToLocalPointInRectangle: 화면 좌표(픽셀)를 이 영역 안의 좌표(중심이 원점)로 바꾼다.
        // pressEventCamera: 캔버스를 그리는 카메라 (Screen Space - Camera 캔버스라 필요).
        // Mathf.Atan2(y, x): 원점에서 (x, y) 방향의 각도(라디안) → Rad2Deg를 곱해 도 단위로 바꾼다.
        private float GetPointerAngle(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 local);
            return Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
        }
    }
}
