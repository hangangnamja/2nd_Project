using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using Game.Battle.Status;
using Game.Core;
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

        [Header("타격 모션 (임시, 캡슐)")]
        [Tooltip("공격할 때 대상 쪽으로 튀어 나가는 최대 거리")]
        [Label("돌진 거리", Min = 0f)]
        [SerializeField] private float lungeDistance = 2.5f;

        [Tooltip("대상 앞에서 멈출 거리 (대상과 겹치지 않게)")]
        [Label("돌진 멈춤 거리", Min = 0f)]
        [SerializeField] private float lungeStopShort = 1.3f;

        [Label("돌진 나가는 시간", Min = 0.01f)]
        [SerializeField] private float lungeOutSeconds = 0.12f;

        [Label("돌진 머무는 시간", Min = 0f)]
        [SerializeField] private float lungeHoldSeconds = 0.3f;

        [Label("돌진 돌아오는 시간", Min = 0.01f)]
        [SerializeField] private float lungeBackSeconds = 0.2f;

        [Tooltip("버프·회복 행동 때 위로 떠오르는 높이")]
        [Label("시전 뜀 높이", Min = 0f)]
        [SerializeField] private float castHeight = 0.6f;

        [Label("시전 떠오르는 시간", Min = 0.01f)]
        [SerializeField] private float castRiseSeconds = 0.16f;

        [Label("시전 머무는 시간", Min = 0f)]
        [SerializeField] private float castHoldSeconds = 0.2f;

        [Label("시전 내려오는 시간", Min = 0.01f)]
        [SerializeField] private float castFallSeconds = 0.18f;

        [Tooltip("버프·회복 행동 때 몸이 빛나는 색")]
        [SerializeField] private Color castGlowColor = new Color(1f, 0.85f, 0.4f);

        [Tooltip("맞았을 때 하얗게 번쩍이는 시간(초)")]
        [Label("피격 번쩍임 시간", Min = 0.01f)]
        [SerializeField] private float flashSeconds = 0.12f;

        [Label("피격 반응 시간", Min = 0.01f)]
        [SerializeField] private float hitReactSeconds = 0.25f;

        [Label("처치 날아가는 시간", Min = 0.01f)]
        [SerializeField] private float knockdownSeconds = 0.5f;

        public BattleUnit Unit { get; private set; }

        // 돌진 모션의 나가는 시간 (BattleFxView가 돌진이 대상에 닿는 순간에 맞춰 타격 박자를 재생하려고 읽는다).
        public float LungeOutSeconds => lungeOutSeconds;

        // 시전 모션이 가장 높이 떠오르기까지의 시간 (BattleFxView가 이 순간에 버프·회복 박자를 재생한다).
        public float CastRiseSeconds => castRiseSeconds;

        // ---------- 모션 (자리 + 어긋남) ----------
        // 무대(BattleStageView)가 정한 자리 = 기준 자리. 모션은 그 위에 더하는 "어긋남"만 바꾸고, 매 프레임 기준 자리 + 어긋남으로 놓는다.
        // 이렇게 나누면 모션 도중 무대가 다시 세워져도(자리 이동) 모션이 엉뚱한 곳으로 튀지 않는다.
        private Vector3 anchorPosition;
        private Quaternion anchorRotation = Quaternion.identity;
        private Vector3 motionOffset;
        private Quaternion motionRotation = Quaternion.identity;

        // 지금 재생 중인 모션 (돌진·피격·처치). 새 모션이 오면 멈추고 바꾼다.
        private Coroutine motion;

        // 상태 색 (Refresh가 정한 색). 피격 번쩍임은 이 색과 흰색을 섞는다.
        private Color baseColor = Color.white;

        // 마지막 Refresh 때 대상 후보였는지 (화면 HP만 바뀌어 글자를 다시 쓸 때 ▼ 표시를 유지하려고 기억).
        private bool lastTargetable;

        // 마지막 Refresh 때의 "행동 중" 여부. 연출이 끝나 화면 사망이 확정될 때 같은 조건으로 다시 그리기 위해 기억한다.
        private bool lastActing;
        private float flashRemaining;

        // 번쩍임 색: 피격은 흰색, 시전은 castGlowColor.
        private Color flashColor = Color.white;
        private float flashDuration = 0.12f;

        // 무적 표시용 인스턴스. 상태효과 칸에 넣지 않고 화면 표시에만 쓰므로 강탈·소모되지 않는다. 처음 필요할 때 한 번 만든다.
        private StatusInstance invulnerableIcon;

        // 경계 표시용 인스턴스. 경계는 상태효과 칸이 아니라서, 머리 위 표시에만 쓰는 인스턴스를 따로 만든다.
        private StatusInstance boundaryIcon;

        // 기간 오라(공연) 표시용 인스턴스. 오라도 버프 칸이 아니라 머리 위 표시에만 쓴다.
        private StatusInstance auraIcon;

        // 상시 패시브·자폭 카운트다운 표시용 인스턴스 (표시 효과 데이터마다 하나). 버프 칸이 아니라 표시만 따로 한다.
        // 같은 표시를 다시 만들지 않아야 툴팁이 갱신마다 바뀌지 않는다.
        private readonly Dictionary<StatusEffectData, StatusInstance> displayIcons = new Dictionary<StatusEffectData, StatusInstance>();

        // 표시 효과 데이터에 맞는 표시용 인스턴스를 꺼낸다. 처음이면 만들어 둔다.
        private StatusInstance GetDisplayIcon(StatusEffectData display)
        {
            // TryGetValue: 이미 만든 인스턴스가 있으면 icon에 담고 true.
            if (!displayIcons.TryGetValue(display, out StatusInstance icon))
            {
                icon = new StatusInstance(display, Unit, appliedOrder: 0);
                displayIcons[display] = icon;
            }

            return icon;
        }

        // 이 뷰가 표시할 전투 유닛을 연결한다. 생성 직후 BattleStageView가 한 번 호출한다.
        public void Bind(BattleUnit unit)
        {
            Unit = unit;
            SetAnchor(transform.position, transform.rotation);
            Refresh(isActing: false, isTargetable: false);
        }

        // 현재 HP와 상태(행동 중, 대상 후보, 사망)에 맞춰 글자와 색을 갱신한다.
        public void Refresh(bool isActing, bool isTargetable)
        {
            if (Unit == null)
            {
                return;
            }

            lastTargetable = isTargetable;
            lastActing = isActing;
            UpdateLabel();

            // 아군 이름·HP·스택은 화면 아래 초상화 카드(PartyCardView)에서 보여준다.
            // 아군은 카메라 바로 앞에 서는 장면이 많아 머리 위 글자가 화면을 가리므로 숨긴다. 적은 그대로 머리 위에 표시.
            label.gameObject.SetActive(!Unit.IsAlly);

            // renderer.material: 이 오브젝트 전용 머티리얼 복사본을 돌려주므로 다른 캡슐 색에는 영향이 없다.
            // URP Lit 셰이더는 기본 색(_BaseColor)을 material.color로 바꿀 수 있다.
            baseColor = PickColor(isActing);
            bodyRenderer.material.color = baseColor;

            // 아군은 화면 아래 초상화 카드(PartyCardView), 보스는 상단 보스 HP바(BossHpBarView)에서 버프·디버프를 보여주므로
            // 머리 위 줄을 끈다. 나머지 적만 머리 위에 표시.
            // 패턴 매칭 is: 데이터가 EnemyCharacterData이고 보스 표시가 켜져 있으면 보스.
            bool isBoss = Unit.Data is EnemyCharacterData enemyData && enemyData.IsBoss;
            bool showsOverhead = !Unit.IsAlly && !isBoss;
            statusBar.gameObject.SetActive(showsOverhead);
            if (showsOverhead)
            {
                statusBar.Show(BuildDisplayedStatuses());
            }
            ShowTargetRing(isTargetable);
        }

        // 고를 수 있는 대상이면 고리를 켠다. 아군이면 하늘색, 적이면 붉은색.
        private void ShowTargetRing(bool isTargetable)
        {
            if (targetRing == null)
            {
                return;
            }

            // 쓰러진 유닛도 부활 스킬의 대상이면 고리를 보여준다 (isTargetable이 이미 스킬 규칙을 확인함).
            targetRing.gameObject.SetActive(isTargetable);
            targetRing.color = Unit.IsAlly ? allyTargetColor : enemyTargetColor;
        }

        // 고리를 천천히 돌린다. 고리는 Billboard로 카메라를 보므로, 그 위에 화면 기준 z축 회전을 더한다.
        // LateUpdate: Billboard가 정면을 맞춘 다음 프레임 순서와 상관없이 매번 정면 + 회전각을 다시 계산한다.
        private void LateUpdate()
        {
            // 모션을 먼저 적용해, 아래 조준 고리가 이번 프레임의 몸 위치를 기준으로 놓이게 한다.
            ApplyMotion();

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
            // 화면 기준 사망(ShownHp.IsDead): 쓰러뜨린 공격의 연출이 끝나기 전에는 효과 아이콘을 그대로 둔다.
            if (ShownHp.IsDead(Unit))
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

            // 조건이 맞아 켜진 상시 패시브(시선집중, 부하가 있는 동안 피해 감소): 표시용 효과를 맨 앞에 보여준다.
            // 조건이 꺼지면(보호막 깨짐, 부하 전멸) 다음 갱신에서 바로 사라진다.
            foreach (PassiveData passive in Unit.GetActiveConditionalPassives())
            {
                StatusEffectData display = passive.DisplayStatus;
                if (display != null)
                {
                    statuses.Insert(0, GetDisplayIcon(display));
                }
            }

            // 자폭 카운트다운(부하): 남은 턴 = 자폭하는 자기 턴 번호 - 지금까지 시작한 자기 턴 수. 예: 2번째 턴에 자폭, 1턴 지남 → 1
            if (Unit.Data is EnemyCharacterData enemyData && enemyData.CountdownDisplay != null)
            {
                StatusInstance countdown = GetDisplayIcon(enemyData.CountdownDisplay);

                // Math.Max(1, …): 자폭하는 턴에도 0이 아니라 1로 보여 "이번 턴에 터짐"을 나타낸다.
                countdown.SetRemainingTurnsForDisplay(System.Math.Max(1, enemyData.CountdownTurns - Unit.OwnTurnsTaken));
                statuses.Insert(0, countdown);
            }

            // 피격 무효(보스 2): 남은 횟수가 있을 때만, 남은 턴 자리에 남은 횟수를 적어 맨 앞에 보여준다.
            if (Unit.HitBlockCharges > 0 && Unit.HitBlockDisplay != null)
            {
                StatusInstance hitBlock = GetDisplayIcon(Unit.HitBlockDisplay);
                hitBlock.SetRemainingTurnsForDisplay(Unit.HitBlockCharges);
                statuses.Insert(0, hitBlock);
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
        // 머리 위 글자(이름·스택·상태·HP). HP는 연출에 맞춘 화면 HP(ShownHp) → 타격 박자에 맞춰 줄어든다.
        // 지금 선택된 스킬로 고를 수 있는 대상은 이름 앞에 ▼ 표시를 붙여 구분한다.
        private void UpdateLabel()
        {
            string marker = lastTargetable ? "▼ " : string.Empty;
            label.text = $"{marker}{Unit.Name}{BuildStackText()}{BuildFieldStateText()}\nHP {ShownHp.Hp(Unit)}/{Unit.MaxHp}";
        }

        private void OnEnable()
        {
            ShownHp.Changed += HandleShownHpChanged;
        }

        private void OnDisable()
        {
            ShownHp.Changed -= HandleShownHpChanged;
        }

        // 화면 HP가 바뀌면 글자를 다시 쓴다. 쓰러진 유닛은 연출이 끝나 화면에서도 쓰러진 순간(ShownHp.IsDead)이 오므로
        // 그때 색·상태효과까지 전부 다시 그린다 (로직 사망 시점이 아니라 그 공격의 연출이 끝난 뒤 회색이 되도록).
        private void HandleShownHpChanged()
        {
            if (Unit == null)
            {
                return;
            }

            if (Unit.IsDead)
            {
                Refresh(lastActing, lastTargetable);
            }
            else
            {
                UpdateLabel();
            }
        }

        // 무대가 정한 자리를 기준으로 삼는다 (BattleStageView.Place가 부름). 새 장면이므로 남아 있던 모션은 지운다.
        public void SetAnchor(Vector3 position, Quaternion rotation)
        {
            anchorPosition = position;
            anchorRotation = rotation;
            StopMotion();
            ApplyMotion();
        }

        // 기준 자리 + 모션 어긋남으로 놓고, 피격 번쩍임 색을 섞는다. LateUpdate마다 부른다.
        private void ApplyMotion()
        {
            transform.SetPositionAndRotation(anchorPosition + motionOffset, anchorRotation * motionRotation);

            if (flashRemaining > 0f)
            {
                flashRemaining -= Time.deltaTime;

                // Color.Lerp(a, b, t): t만큼 a에서 b로 섞는다 → 남은 시간 비율만큼 번쩍임 색에 가깝다 (시작 순간 가장 밝고 점점 원래 색).
                float t = Mathf.Clamp01(flashRemaining / flashDuration);
                bodyRenderer.material.color = Color.Lerp(baseColor, flashColor, t);
            }
        }

        // 진행 중인 모션을 멈추고 어긋남을 0으로 되돌린다.
        private void StopMotion()
        {
            if (motion != null)
            {
                StopCoroutine(motion);
                motion = null;
            }

            motionOffset = Vector3.zero;
            motionRotation = Quaternion.identity;
        }

        // 모션을 새로 시작한다. 꺼진 오브젝트는 코루틴을 시작할 수 없으므로 보일 때만 한다.
        private void StartMotion(IEnumerator routine)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            StopMotion();
            motion = StartCoroutine(routine);
        }

        // 공격: 대상 쪽으로 튀어 나갔다가 잠시 머문 뒤 돌아온다.
        // 이동 거리 = 돌진 거리와 (대상까지 거리 - 멈춤 거리) 중 작은 값 → 가까운 대상과 겹치지 않는다.
        public void PlayLunge(Vector3 targetPosition)
        {
            Vector3 toTarget = targetPosition - anchorPosition;
            toTarget.y = 0f;
            float distance = Mathf.Min(lungeDistance, Mathf.Max(0f, toTarget.magnitude - lungeStopShort));

            // normalized: 길이 1짜리 방향. 대상까지의 방향 × 이동 거리 = 튀어 나갈 지점(기준 자리 기준).
            StartMotion(LungeRoutine(toTarget.normalized * distance));
        }

        // 돌진 순서: 빠르게 나감(감속) → 머묾 → 부드럽게 돌아옴.
        private IEnumerator LungeRoutine(Vector3 peak)
        {
            // 람다 t => 1 - (1 - t)²: 처음에 빠르고 끝에서 느려지는 감속 곡선 → 툭 치고 멈추는 느낌.
            yield return MoveOffset(Vector3.zero, peak, lungeOutSeconds, t => 1f - (1f - t) * (1f - t));
            yield return new WaitForSeconds(lungeHoldSeconds);

            // Mathf.SmoothStep: 처음과 끝이 부드러운 곡선 → 자연스럽게 제자리로.
            yield return MoveOffset(peak, Vector3.zero, lungeBackSeconds, t => Mathf.SmoothStep(0f, 1f, t));
            motion = null;
        }

        // 버프·회복 행동(시전): 제자리에서 위로 떠오르며 빛났다가 내려온다. 공격 돌진과 구분되는 "기를 모으는" 느낌.
        public void PlayCast()
        {
            Flash(castGlowColor, castRiseSeconds + castHoldSeconds + castFallSeconds);
            StartMotion(CastRoutine());
        }

        // 시전 순서: 감속하며 떠오름 → 머묾 → 부드럽게 내려옴.
        private IEnumerator CastRoutine()
        {
            Vector3 peak = Vector3.up * castHeight;
            yield return MoveOffset(Vector3.zero, peak, castRiseSeconds, t => 1f - (1f - t) * (1f - t));
            yield return new WaitForSeconds(castHoldSeconds);
            yield return MoveOffset(peak, Vector3.zero, castFallSeconds, t => Mathf.SmoothStep(0f, 1f, t));
            motion = null;
        }

        // 몸을 color로 번쩍이게 하고 seconds 동안 원래 색으로 돌아오게 한다 (ApplyMotion이 매 프레임 섞는다).
        private void Flash(Color color, float seconds)
        {
            flashColor = color;
            flashDuration = Mathf.Max(0.01f, seconds);
            flashRemaining = flashDuration;
        }

        // 맞음: 하얗게 번쩍 + 공격 반대 방향으로 밀렸다가 흔들리며 돌아온다.
        // from: 공격해 온 위치 (모르면 null → 카메라 반대쪽으로 밀림). strength: 밀리는 거리 (치명타는 더 크게).
        // Vector3?: 값이 없을 수도 있는(nullable) 위치. HasValue로 있는지 확인한다.
        // 이미 다른 모션(돌진·날아감) 중이면 그 모션은 그대로 두고 번쩍임만 준다.
        public void PlayHitReaction(Vector3? from, float strength)
        {
            Flash(Color.white, flashSeconds);
            if (motion != null)
            {
                return;
            }

            StartMotion(HitRoutine(GetPushDirection(from) * strength));
        }

        // 밀림은 처음에 가장 크고 점점 0으로, 흔들림은 좌우로 떨리며 줄어든다.
        private IEnumerator HitRoutine(Vector3 push)
        {
            float elapsed = 0f;
            while (elapsed < hitReactSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hitReactSeconds);

                // (1 - t)²: 시간이 갈수록 빠르게 0으로 줄어드는 감쇠 값. Mathf.Sin(t × 40): 빠르게 -1~1을 오가는 진동.
                float decay = (1f - t) * (1f - t);
                Vector3 shake = transform.right * (Mathf.Sin(t * 40f) * 0.06f * decay);
                motionOffset = push * decay + shake;
                yield return null;
            }

            motionOffset = Vector3.zero;
            motion = null;
        }

        // 처치: 공격 반대쪽 위로 날아가며 뒤로 넘어간다. 다음 장면부터 쓰러진 유닛은 빠지므로 날아간 자리에 그대로 둔다.
        public void PlayKnockdown(Vector3? from)
        {
            Flash(Color.white, flashSeconds);
            StartMotion(KnockdownRoutine(GetPushDirection(from)));
        }

        private IEnumerator KnockdownRoutine(Vector3 direction)
        {
            // Vector3.Cross(위, 방향): 날아가는 방향에 수직인 옆 축 → 그 축으로 돌려 뒤로 넘어지게 한다.
            Vector3 tumbleAxis = Vector3.Cross(Vector3.up, direction);
            float elapsed = 0f;
            while (elapsed < knockdownSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / knockdownSeconds);

                // 포물선: 수평으로 2.5만큼 날아가고, 높이는 4t(1 - t) → 절반 지점에서 가장 높이(1.0) 떴다가 내려온다.
                motionOffset = direction * (2.5f * t) + Vector3.up * (4f * t * (1f - t));

                // Quaternion.AngleAxis(각도, 축): 월드 축을 중심으로 각도만큼 돈 회전 → 90도까지 넘어간다.
                // 최종 회전 = 기준 회전 × 모션 회전이므로, 월드 기준 회전을 기준 회전의 좌표로 바꿔(Inverse로 감싸) 넣는다.
                motionRotation = Quaternion.Inverse(anchorRotation) * Quaternion.AngleAxis(90f * t, tumbleAxis) * anchorRotation;
                yield return null;
            }

            motion = null;
        }

        // 밀려날 방향: 공격해 온 쪽의 반대 (수평). 모르면 카메라가 보는 방향(화면 안쪽).
        private Vector3 GetPushDirection(Vector3? from)
        {
            Vector3 direction;
            if (from.HasValue)
            {
                direction = anchorPosition - from.Value;
            }
            else
            {
                direction = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            }

            direction.y = 0f;

            // sqrMagnitude: 길이의 제곱. 0에 가까우면 방향을 정할 수 없으므로 앞쪽으로 둔다.
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        // 어긋남을 from → to로 seconds 동안 옮긴다.
        // ease: 0~1 진행도를 받아 가속·감속을 입힌 0~1 값을 돌려주는 함수 (Func<float, float> = 실수를 받아 실수를 돌려주는 델리게이트).
        private IEnumerator MoveOffset(Vector3 from, Vector3 to, float seconds, System.Func<float, float> ease)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                motionOffset = Vector3.Lerp(from, to, ease(Mathf.Clamp01(elapsed / seconds)));
                yield return null;
            }

            motionOffset = to;
        }

        private Color PickColor(bool isActing)
        {
            if (ShownHp.IsDead(Unit))
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
