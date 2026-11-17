using Game.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.UI
{
    // 화면의 캡슐을 누르면:
    //  - 짧게 눌렀다 떼면 그 유닛을 BattleManager에 대상으로 전달한다 (선택된 스킬의 대상이 될 수 있으면 BattleManager가 처리)
    //  - 꾹 누르고 있으면 그 유닛의 상세 창(스탯·버프·디버프·스킬)을 연다 → 아래 아군 카드 꾹 누르기와 같은 창을 적에게도 쓴다
    // 상태효과 블럭을 누르면 설명 툴팁만 띄운다.
    public class BattleTargetPicker : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private Camera targetCamera;

        [Tooltip("클릭 판정 최대 거리")]
        [SerializeField] private float maxRayDistance = 100f;

        [Tooltip("상태효과 블럭을 클릭했을 때 설명을 띄울 툴팁 (없으면 툴팁 없이 대상 선택만 한다)")]
        [SerializeField] private StatusTooltip statusTooltip;

        [Tooltip("캡슐을 꾹 눌렀을 때 열 상세 창. 비워 두면 씬에서 찾는다")]
        [SerializeField] private UnitDetailPanel detailPanel;

        [Tooltip("이 시간(초) 이상 누르고 있으면 대상 선택 대신 상세 창을 연다 (아군 카드와 같은 값)")]
        [SerializeField, Min(0.1f)] private float longPressSeconds = 0.45f;

        // 지금 누르고 있는 유닛과 누른 시간. 누르고 있지 않으면 pressedView = null.
        private UnitView pressedView;
        private float pressTime;
        private bool longPressFired;

        private void Awake()
        {
            // FindFirstObjectByType(FindObjectsInactive.Include): 꺼져 있는 오브젝트까지 포함해 씬에서 첫 번째 상세 창을 찾는다.
            if (detailPanel == null)
            {
                detailPanel = FindFirstObjectByType<UnitDetailPanel>(FindObjectsInactive.Include);
            }
        }

        // 매 프레임 마우스 상태를 보고 누름 → 누르는 중(꾹 누르기 판정) → 뗌 순서로 처리한다.
        private void Update()
        {
            // Mouse.current: 새 Input System의 현재 마우스. 마우스가 없는 기기에서는 null이라 먼저 확인한다.
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            // wasPressedThisFrame / wasReleasedThisFrame: 이번 프레임에 막 눌렸을 때 / 막 뗐을 때만 true.
            if (mouse.leftButton.wasPressedThisFrame)
            {
                HandlePress(mouse.position.ReadValue());
            }
            else if (pressedView != null && mouse.leftButton.isPressed)
            {
                UpdateLongPress();
            }
            else if (pressedView != null && mouse.leftButton.wasReleasedThisFrame)
            {
                HandleRelease(mouse.position.ReadValue());
            }
        }

        // 누른 순간: 상태효과 블럭이면 툴팁, 유닛이면 누르기 시작 (대상 선택은 뗄 때까지 미룬다).
        private void HandlePress(Vector2 screenPosition)
        {
            pressedView = null;

            // 스킬 버튼 같은 UI 위를 클릭했다면 캡슐 누르기로 처리하지 않는다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (!TryRaycast(screenPosition, out RaycastHit hit))
            {
                HideTooltip();
                return;
            }

            // 상태효과 블럭은 유닛의 자식이라 UnitView보다 먼저 확인해야 한다 (안 그러면 유닛 누르기로 처리됨).
            StatusIconView icon = hit.collider.GetComponent<StatusIconView>();
            if (icon != null && icon.Status != null && statusTooltip != null)
            {
                statusTooltip.Show(icon.Status, screenPosition);
                return;
            }

            HideTooltip();

            // GetComponentInParent: 맞은 콜라이더 오브젝트나 그 부모에서 UnitView를 찾는다.
            pressedView = hit.collider.GetComponentInParent<UnitView>();
            pressTime = 0f;
            longPressFired = false;
        }

        // 누르고 있는 동안 시간을 재다가, 정한 시간이 지나면 한 번만 상세 창을 연다.
        // unscaledDeltaTime: 히트스톱·일시정지(timeScale)와 상관없이 실제로 흐른 시간.
        private void UpdateLongPress()
        {
            if (longPressFired)
            {
                return;
            }

            pressTime += Time.unscaledDeltaTime;
            if (pressTime >= longPressSeconds && detailPanel != null && pressedView.Unit != null)
            {
                longPressFired = true;
                detailPanel.Open(pressedView.Unit);
            }
        }

        // 뗀 순간: 꾹 누르기로 상세 창을 열었으면 아무것도 하지 않고, 짧게 눌렀으면 같은 유닛 위에서 뗐을 때만 대상으로 고른다.
        private void HandleRelease(Vector2 screenPosition)
        {
            UnitView pressed = pressedView;
            pressedView = null;

            if (longPressFired)
            {
                return;
            }

            // 누른 채 다른 곳으로 끌고 가서 떼면 취소한다.
            if (TryRaycast(screenPosition, out RaycastHit hit) && hit.collider.GetComponentInParent<UnitView>() == pressed)
            {
                battleManager.SelectTarget(pressed.Unit);
            }
        }

        // 화면 좌표에서 카메라 방향으로 광선을 쏴 처음 맞은 콜라이더를 찾는다.
        // ScreenPointToRay: 화면의 픽셀 좌표를 카메라에서 뻗어 나가는 3D 광선으로 변환한다.
        // Physics.Raycast: 광선이 콜라이더에 닿으면 true를 돌려주고, 맞은 정보를 hit에 담는다 (out 매개변수).
        private bool TryRaycast(Vector2 screenPosition, out RaycastHit hit)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out hit, maxRayDistance);
        }

        // 툴팁이 연결돼 있으면 닫는다.
        private void HideTooltip()
        {
            // Unity 오브젝트는 ?. 대신 != null로 확인한다 (파괴된 오브젝트를 == null로 알려 주는 건 이 비교뿐이다).
            if (statusTooltip != null)
            {
                statusTooltip.Hide();
            }
        }
    }
}
