using System.Collections.Generic;
using Game.Battle;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.UI
{
    // 유닛 하나를 화면에 표시한다 (임시 캡슐 + 머리 위 글자).
    // 값을 바꾸지 않고 BattleUnit의 현재 상태를 보여주기만 한다. 3D 모델로 교체할 때 이 클래스만 바꾸면 된다.
    public class UnitView : MonoBehaviour
    {
        [SerializeField] private Renderer bodyRenderer;
        [SerializeField] private TextMesh label;
        [SerializeField] private UnitStatusBar statusBar;

        [Header("상태별 색상")]
        [SerializeField] private Color allyColor = new Color(0.35f, 0.6f, 1f);
        [SerializeField] private Color enemyColor = new Color(1f, 0.4f, 0.35f);
        [SerializeField] private Color actingColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color deadColor = new Color(0.3f, 0.3f, 0.3f);

        [Tooltip("퇴장한 아군(하이퍼캐리 변신 중) 색. 쓰러진 것과 구분되도록 밝은 회색")]
        [SerializeField] private Color exitedColor = new Color(0.65f, 0.65f, 0.7f);

        [Header("고정 상태 아이콘")]
        [Tooltip("무적 유닛 머리 위에 함께 보여줄 표시 전용 효과 (영구 표시)")]
        [SerializeField] private StatusEffectData invulnerableDisplay;

        public BattleUnit Unit { get; private set; }

        // 무적 표시용 인스턴스. 상태효과 칸에 넣지 않고 화면 표시에만 쓰므로 강탈·소모되지 않는다. 처음 필요할 때 한 번 만든다.
        private StatusInstance invulnerableIcon;

        // 경계 표시용 인스턴스. 경계는 상태효과 칸이 아니라서, 머리 위 표시에만 쓰는 인스턴스를 따로 만든다.
        private StatusInstance boundaryIcon;

        // 기간 오라(공연) 표시용 인스턴스. 오라도 버프 칸이 아니라 머리 위 표시에만 쓴다.
        private StatusInstance auraIcon;

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
            label.text = $"{marker}{Unit.Name}{BuildStackText()}{BuildFieldStateText()}\nHP {Unit.CurrentHp}/{Unit.MaxHp}";

            // renderer.material: 이 오브젝트 전용 머티리얼 복사본을 돌려주므로 다른 캡슐 색에는 영향이 없다.
            // URP Lit 셰이더는 기본 색(_BaseColor)을 material.color로 바꿀 수 있다.
            bodyRenderer.material.color = PickColor(isActing);

            statusBar.Show(BuildDisplayedStatuses());
        }

        // 머리 위에 보여줄 효과 목록: 걸려 있는 상태효과 + 고정 상태(무적) 표시. 쓰러진 유닛은 아무것도 표시하지 않는다.
        private List<StatusInstance> BuildDisplayedStatuses()
        {
            if (Unit.IsDead)
            {
                return new List<StatusInstance>();
            }

            List<StatusInstance> statuses = Unit.Statuses.GetAll();

            if (Unit.IsInvulnerable && invulnerableDisplay != null)
            {
                // ??=: 왼쪽이 null일 때만 오른쪽을 만들어 넣는다 → 표시용 인스턴스를 한 번만 만든다.
                invulnerableIcon ??= new StatusInstance(invulnerableDisplay, Unit, appliedOrder: 0);

                // 맨 앞에 넣어, 버프·디버프가 늘어나도 무적 아이콘이 항상 첫 칸에 보이게 한다.
                statuses.Insert(0, invulnerableIcon);
            }

            // 경계를 펼친 동안: 경계의 표시용 효과를 자기 버프처럼 맨 앞에 보여준다 (기획서 3-12).
            // ?.: 경계가 없으면(null) DisplayStatus를 읽지 않고 null을 돌려준다.
            StatusEffectData boundaryDisplay = Unit.ActiveBoundary?.DisplayStatus;
            if (boundaryDisplay != null)
            {
                // 같은 경계를 보여주는 동안에는 인스턴스를 다시 만들지 않는다 (툴팁이 매 갱신마다 바뀌지 않도록).
                if (boundaryIcon == null || boundaryIcon.Data != boundaryDisplay)
                {
                    boundaryIcon = new StatusInstance(boundaryDisplay, Unit, appliedOrder: 0);
                }

                statuses.Insert(0, boundaryIcon);
            }

            // 기간 오라(공연)를 생성하는 동안: 오라의 표시용 효과를 맨 앞에 보여준다. 오라는 버프 칸이 아니라 표시만 따로 한다.
            StatusEffectData auraDisplay = Unit.ActiveAura?.DisplayStatus;
            if (auraDisplay != null)
            {
                if (auraIcon == null || auraIcon.Data != auraDisplay)
                {
                    auraIcon = new StatusInstance(auraDisplay, Unit, appliedOrder: 0);
                }

                statuses.Insert(0, auraIcon);
            }

            return statuses;
        }

        // 스택형 패시브가 있으면 이름 뒤에 현재 스택을 붙인다 (테스트용). 예: "DotMainAttack ★1/2"
        private string BuildStackText()
        {
            string text = string.Empty;

            foreach (PassiveData passive in Unit.Data.Passives)
            {
                if (passive.UsesStacks)
                {
                    text += $" ★{Unit.GetPassiveStacks(passive)}/{passive.MaxStacks}";
                }
            }

            return text;
        }

        // 퇴장·변신 상태 표시. 변신 중이면 진행한 변신 턴 / 제한을 붙인다. 예: " (퇴장)", " [변신 1/3]"
        // 무적은 글자 대신 머리 위 아이콘으로 보여준다 (BuildDisplayedStatuses).
        private string BuildFieldStateText()
        {
            if (Unit.IsExited)
            {
                return " (퇴장)";
            }

            TransformationState transformation = Unit.Transformation;
            return Unit.IsTransformed
                ? $" [변신 {transformation.TurnsTaken}/{transformation.Data.TurnLimit}]"
                : string.Empty;
        }

        // 상태 우선순위: 사망 → 퇴장 → 행동 중 → 소속(아군/적) 순으로 색을 고른다.
        private Color PickColor(bool isActing)
        {
            if (Unit.IsDead)
            {
                return deadColor;
            }

            if (Unit.IsExited)
            {
                return exitedColor;
            }

            if (isActing)
            {
                return actingColor;
            }

            return Unit.IsAlly ? allyColor : enemyColor;
        }
    }
}
