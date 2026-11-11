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

        [Header("대상 표시")]
        [Tooltip("지금 고른 스킬로 고를 수 있는 대상에 보여주는 고리 (붕괴 스타레일식 조준 표시)")]
        [SerializeField] private SpriteRenderer targetRing;

        [Tooltip("아군을 고를 때(버프·회복) 고리 색")]
        [SerializeField] private Color allyTargetColor = new Color(0.3f, 0.85f, 1f, 0.9f);

        [Tooltip("적을 고를 때(공격) 고리 색")]
        [SerializeField] private Color enemyTargetColor = new Color(1f, 0.4f, 0.35f, 0.9f);

        [Tooltip("고리가 도는 속도 (초당 각도)")]
        [SerializeField] private float ringSpinSpeed = 45f;

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

            // 아군 이름·HP·스택은 화면 아래 초상화 카드(PartyCardView)에서 보여준다.
            // 아군은 카메라 바로 앞에 서는 장면이 많아 머리 위 글자가 화면을 가리므로 숨긴다. 적은 그대로 머리 위에 표시.
            label.gameObject.SetActive(!Unit.IsAlly);

            // renderer.material: 이 오브젝트 전용 머티리얼 복사본을 돌려주므로 다른 캡슐 색에는 영향이 없다.
            // URP Lit 셰이더는 기본 색(_BaseColor)을 material.color로 바꿀 수 있다.
            bodyRenderer.material.color = PickColor(isActing);

            statusBar.Show(BuildDisplayedStatuses());
            ShowTargetRing(isTargetable);
        }

        // 고를 수 있는 대상이면 고리를 켠다. 아군이면 하늘색, 적이면 붉은색.
        private void ShowTargetRing(bool isTargetable)
        {
            if (targetRing == null)
            {
                return;
            }

            targetRing.gameObject.SetActive(isTargetable && !Unit.IsDead);
            targetRing.color = Unit.IsAlly ? allyTargetColor : enemyTargetColor;
        }

        // 고리를 천천히 돌린다. 고리는 Billboard로 카메라를 보므로, 그 위에 화면 기준 z축 회전을 더한다.
        // LateUpdate: Billboard가 정면을 맞춘 다음 프레임 순서와 상관없이 매번 정면 + 회전각을 다시 계산한다.
        private void LateUpdate()
        {
            if (targetRing == null || !targetRing.gameObject.activeSelf || Camera.main == null)
            {
                return;
            }

            // Quaternion.Euler(0, 0, 각도): 화면을 바라보는 축(z)으로 각도만큼 돌린 회전. Time.time × 속도 = 계속 늘어나는 각도.
            targetRing.transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 0f, Time.time * ringSpinSpeed);

            // 고리를 몸 가운데에서 카메라 쪽으로 조금 당겨 캡슐에 가려지지 않게 한다.
            // (카메라 위치 - 몸 위치).normalized: 몸에서 카메라를 향하는 길이 1짜리 방향.
            Vector3 toCamera = (Camera.main.transform.position - transform.position).normalized;
            targetRing.transform.position = transform.position + Vector3.up * 0.1f + toCamera * 0.7f;
        }

        // 머리 위에 보여줄 효과 목록: 걸려 있는 상태효과 + 고정 상태(무적·경계·오라) 표시. 쓰러진 유닛은 아무것도 표시하지 않는다.
        // 아래 초상화 카드·상세 창(PartyCardView, UnitDetailPanel)도 같은 목록을 쓰도록 public으로 연다.
        public List<StatusInstance> BuildDisplayedStatuses()
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
