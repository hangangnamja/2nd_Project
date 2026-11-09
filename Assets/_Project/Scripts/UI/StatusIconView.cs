using Game.Battle.Status;
using UnityEngine;

namespace Game.UI
{
    // 머리 위 상태효과 블럭 하나에 붙어, 지금 이 블럭이 어떤 상태효과를 보여주는지 기억한다.
    // 클릭 판정(BattleTargetPicker)이 블럭을 맞히면 여기서 상태효과를 꺼내 툴팁에 넘긴다.
    public class StatusIconView : MonoBehaviour
    {
        public StatusInstance Status { get; private set; }

        // 블럭이 표시할 상태효과를 바꾼다. 블럭은 재사용되므로 갱신할 때마다 호출된다.
        public void SetStatus(StatusInstance status)
        {
            Status = status;
        }
    }
}
