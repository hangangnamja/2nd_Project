using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 유닛 하나를 화면에 표시한다 (임시 캡슐 + 머리 위 글자).
    // 값을 바꾸지 않고 BattleUnit의 현재 상태를 보여주기만 한다. 3D 모델로 교체할 때 이 클래스만 바꾸면 된다.
    public class UnitView : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private TextMesh label;

        [Header("상태별 색상")]
        [SerializeField] private Color allyColor = new Color(0.35f, 0.6f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.4f, 0.35f);
        [SerializeField] private Color actingColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color deadColor = new Color(0.3f, 0.3f, 0.3f);

        public BattleUnit Unit { get; private set; }

        // 이 뷰가 표시할 전투 유닛을 연결한다. 생성 직후 BattleStageView가 한 번 호출한다.
        public void Bind(BattleUnit unit)
        {
            Unit = unit;
            Refresh(isActing: false, isTargetable: false);
        }

        // 현재 HP와 상태(행동 중, 대상 후보, 사망)에 맞춰 글자와 색을 갱신한다.
        public void Refresh(bool isActing, bool isTargetable)
        {
            if (Unit == null)
            {
                return;
            }

            // 지금 선택된 스킬로 고를 수 있는 대상은 이름 앞에 ▼ 표시를 붙여 구분한다.
            string marker = isTargetable ? "▼ " : string.Empty;
            label.text = $"{marker}{Unit.Name}\nHP {Unit.CurrentHp}/{Unit.MaxHp}";

            // renderer.material: 이 오브젝트 전용 머티리얼 복사본을 돌려주므로 다른 캡슐 색에는 영향이 없다.
            // URP Lit 셰이더는 기본 색(_BaseColor)을 material.color로 바꿀 수 있다.
            bodyRenderer.material.color = PickColor(isActing);
        }

        // 상태 우선순위: 사망 → 행동 중 → 소속(아군/적) 순으로 색을 고른다.
        private Color PickColor(bool isActing)
        {
            if (Unit.IsDead)
            {
                return deadColor;
            }

            if (isActing)
            {
                return actingColor;
            }

            return Unit.IsAlly ? allyColor : enemyColor;
        }
    }
}
