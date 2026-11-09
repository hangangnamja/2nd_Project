using Game.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Game.UI
{
    // 화면에서 캡슐을 클릭하면 그 유닛을 BattleManager에 전달한다.
    // 선택된 스킬의 대상이 될 수 있는 유닛이면 BattleManager가 바로 스킬을 실행한다.
    public class BattleTargetPicker : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private Camera targetCamera;

        [Tooltip("클릭 판정 최대 거리")]
        [SerializeField] private float maxRayDistance = 100f;

        [Tooltip("상태효과 블럭을 클릭했을 때 설명을 띄울 툴팁 (없으면 툴팁 없이 대상 선택만 한다)")]
        [SerializeField] private StatusTooltip statusTooltip;

        // 매 프레임 마우스 왼쪽 클릭이 있었는지 확인하고, 있으면 클릭 위치의 유닛을 찾는다.
        private void Update()
        {
            // Mouse.current: 새 Input System의 현재 마우스. 마우스가 없는 기기에서는 null이라 먼저 확인한다.
            // wasPressedThisFrame: 이번 프레임에 막 눌렸을 때만 true (누르고 있는 동안 계속 true가 아님).
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            {
                return;
            }

            // 스킬 버튼 같은 UI 위를 클릭했다면 대상 선택으로 처리하지 않는다.
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            HandleClickAt(mouse.position.ReadValue());
        }

        // 화면 좌표에서 카메라 방향으로 광선을 쏴 처음 맞은 것을 처리한다.
        //  - 상태효과 블럭: 설명 툴팁을 띄운다 (대상 선택은 하지 않음)
        //  - 유닛: 툴팁을 닫고 대상으로 선택한다
        //  - 빈 곳: 툴팁만 닫는다
        private void HandleClickAt(Vector2 screenPosition)
        {
            // ScreenPointToRay: 화면의 픽셀 좌표를 카메라에서 뻗어 나가는 3D 광선으로 변환한다.
            Ray ray = targetCamera.ScreenPointToRay(screenPosition);

            // Physics.Raycast: 광선이 콜라이더에 닿으면 true를 돌려주고, 맞은 정보를 hit에 담는다 (out 매개변수).
            if (!Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
            {
                HideTooltip();
                return;
            }

            // 상태효과 블럭은 유닛의 자식이라 UnitView보다 먼저 확인해야 한다 (안 그러면 유닛 선택으로 처리됨).
            StatusIconView icon = hit.collider.GetComponent<StatusIconView>();
            if (icon != null && icon.Status != null && statusTooltip != null)
            {
                statusTooltip.Show(icon.Status, screenPosition);
                return;
            }

            HideTooltip();

            // GetComponentInParent: 맞은 콜라이더 오브젝트나 그 부모에서 UnitView를 찾는다.
            UnitView view = hit.collider.GetComponentInParent<UnitView>();
            if (view != null)
            {
                battleManager.SelectTarget(view.Unit);
            }
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
