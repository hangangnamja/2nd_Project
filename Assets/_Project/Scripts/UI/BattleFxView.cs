using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using Game.Battle.Status;
using UnityEngine;

namespace Game.UI
{
    // 전투 임시 연출: 맞은 유닛에 타격 이펙트 + 피해 숫자, 회복받은 유닛에 회복 이펙트 + 회복량, 버프받은 유닛에 버프 이펙트 + 버프 이름.
    //
    // 실행 흐름
    //  1) 전투 로직은 행동 하나(스킬 → 턴 종료 → 추가 공격)를 한 번에 끝까지 계산하면서 연출용 사건을 알린다
    //  2) 이 컴포넌트는 사건을 받은 순서대로 "박자(beat)"에 담아 두고, 박자마다 간격을 두고 재생한다
    //     → 같은 프레임에 계산된 일이라도 "타격 → 지속피해 → 추가 공격 → 버프" 같은 발동 순서가 눈에 보인다
    //  3) BattleManager는 IsPlaying이 false가 될 때까지 다음 턴을 기다린다
    // 이펙트 모양은 파티클 프리팹(머티리얼 HDR 색 + Bloom)에 있고, 이 코드는 언제·어디서 재생할지만 정한다.
    public class BattleFxView : MonoBehaviour, IBattlePresentation
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private BattleStageView stageView;

        [Header("이펙트 (파티클 프리팹, 재생이 끝나면 스스로 지워지게 설정)")]
        [SerializeField] private ParticleSystem hitFx;
        [SerializeField] private ParticleSystem criticalHitFx;
        [SerializeField] private ParticleSystem damageOverTimeFx;
        [SerializeField] private ParticleSystem healFx;
        [SerializeField] private ParticleSystem buffFx;

        [Header("숫자")]
        [SerializeField] private DamagePopup popupPrefab;
        [SerializeField] private Color damageColor = Color.white;
        [SerializeField] private Color criticalColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color damageOverTimeColor = new Color(1f, 0.55f, 0.3f);
        [SerializeField] private Color healColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private Color buffColor = new Color(0.5f, 0.85f, 1f);
        [SerializeField] private Color blockedColor = new Color(0.7f, 0.7f, 0.7f);

        [Header("위치 (캡슐 중심 기준)")]
        [SerializeField] private float effectHeight;
        [SerializeField] private float popupHeight = 0.8f;

        [Tooltip("이펙트·숫자를 카메라 쪽으로 당기는 거리. 캡슐 몸에 가려지지 않게 한다")]
        [SerializeField] private float towardCamera = 1.2f;

        [Header("타이밍")]
        [Tooltip("박자 하나를 재생한 뒤 다음 박자까지의 간격(초)")]
        [SerializeField, Min(0f)] private float beatInterval = 0.3f;

        [Tooltip("마지막 박자 뒤에 숫자를 읽을 시간(초)")]
        [SerializeField, Min(0f)] private float endHold = 0.3f;

        [Header("타격감")]
        [Tooltip("화면 흔들림을 주는 카메라 연출")]
        [SerializeField] private BattleCameraDirector cameraDirector;

        [Tooltip("히트스톱·슬로모션")]
        [SerializeField] private BattleTimeFx timeFx;

        [Tooltip("오른쪽 위 이번 턴 피해 합계")]
        [SerializeField] private DamageTotalView damageTotal;

        [Tooltip("3스 컷인")]
        [SerializeField] private SkillCutInView cutIn;

        [Tooltip("맞는 순간 멈추는 시간(초): 일반 / 치명")]
        [SerializeField, Min(0f)] private float hitStopSeconds = 0.04f;
        [SerializeField, Min(0f)] private float criticalHitStopSeconds = 0.08f;

        [Tooltip("처치 순간: 멈춤 뒤 슬로모션 시간(실제 초)")]
        [SerializeField, Min(0f)] private float killHitStopSeconds = 0.1f;
        [SerializeField, Min(0f)] private float killSlowMotionSeconds = 0.35f;

        [Tooltip("화면 흔들림 세기: 일반 / 치명 / 처치. 3스는 여기에 배율을 곱한다")]
        [SerializeField, Min(0f)] private float hitShake = 0.04f;
        [SerializeField, Min(0f)] private float criticalShake = 0.14f;
        [SerializeField, Min(0f)] private float killShake = 0.2f;
        [SerializeField, Min(1f)] private float ultimateShakeMultiplier = 1.6f;
        [SerializeField, Min(0.01f)] private float shakeSeconds = 0.18f;

        [Header("지속피해 숫자 (한 번에 파파팍)")]
        [Tooltip("이어서 발동한 지속피해 숫자 사이의 간격(초). 작을수록 한꺼번에 터지는 느낌")]
        [SerializeField, Min(0f)] private float dotPopupStagger = 0.05f;

        [Tooltip("지속피해 숫자를 흩뿌리는 범위 (가로, 세로)")]
        [SerializeField] private Vector2 dotPopupScatter = new Vector2(0.9f, 0.7f);

        [Tooltip("맞은 유닛이 밀리는 거리: 일반 / 치명")]
        [SerializeField, Min(0f)] private float hitPush = 0.25f;
        [SerializeField, Min(0f)] private float criticalPush = 0.45f;

        private enum FxKind
        {
            // 행동 시작: 3스 컷인 → 돌진 (항상 박자 하나를 따로 쓴다)
            Action,
            Hit,
            Heal,
            Buff,
        }

        // 재생할 연출 하나 (어떤 종류를, 누구에게, 무슨 값으로)
        private readonly struct FxEntry
        {
            public readonly FxKind Kind;
            public readonly BattleUnit Unit;
            public readonly DamageTakenInfo Damage;
            public readonly int HealAmount;
            public readonly string BuffName;
            public readonly ActionStartInfo Action;

            // 이 사건 직후의 HP·보호막. 박자를 재생할 때 화면 HP를 이 값으로 바꾼다 (ShownHp).
            // 사건이 일어난 그 순간(로직 계산 중)에 만들어지므로, 그때의 실제 값 = 이 사건 후 값이다.
            public readonly int HpAfter;
            public readonly int ShieldAfter;

            // 기본값이 있는 매개변수: 종류마다 필요한 값만 넘기고 나머지는 비워 둔다 (예: 회복이면 healAmount만).
            public FxEntry(FxKind kind, BattleUnit unit, DamageTakenInfo damage = default, int healAmount = 0, string buffName = null, ActionStartInfo action = default)
            {
                Kind = kind;
                Unit = unit;
                Damage = damage;
                HealAmount = healAmount;
                BuffName = buffName;
                Action = action;
                HpAfter = unit.CurrentHp;
                ShieldAfter = unit.ShieldAmount;
            }
        }

        // 박자 목록. 박자 하나 = 동시에 재생할 연출 묶음 (예: 광역기에 맞은 적 전원).
        private readonly List<List<FxEntry>> beats = new List<List<FxEntry>>();
        private BattleEvents events;
        private bool isRunning;

        // 지금 재생 중인 행동의 공격자 자리와 3스 여부. 맞은 유닛이 공격 반대쪽으로 밀리고, 3스면 더 세게 흔들린다.
        private Vector3? attackerPosition;
        private bool isUltimateAction;

        // 재생 중이거나 재생할 박자가 남아 있으면 true → BattleManager가 다음 턴을 미룬다.
        public bool IsPlaying => isRunning || beats.Count > 0;

        // BattleStarted는 BattleManager.Start에서 알린다. OnEnable은 씬의 모든 Start보다 먼저 불리므로 놓치지 않는다.
        private void OnEnable()
        {
            battleManager.BattleStarted += HandleBattleStarted;
        }

        private void OnDisable()
        {
            battleManager.BattleStarted -= HandleBattleStarted;
            Unsubscribe();

            // 연출 도중 꺼져도 화면 HP가 이전 값에 묶여 남지 않게 놓는다.
            ShownHp.ReleaseAll();
        }

        // 전투가 시작되면 사건 통로가 생기므로, 그때 연출 사건을 구독하고 자신을 연출 재생기로 등록한다.
        private void HandleBattleStarted()
        {
            events = battleManager.Events;
            events.ActionStarted += HandleActionStarted;
            events.DamageTaken += HandleDamageTaken;
            events.Healed += HandleHealed;
            events.BuffApplied += HandleBuffApplied;
            battleManager.SetPresentation(this);
        }

        private void Unsubscribe()
        {
            if (events == null)
            {
                return;
            }

            events.ActionStarted -= HandleActionStarted;
            events.DamageTaken -= HandleDamageTaken;
            events.Healed -= HandleHealed;
            events.BuffApplied -= HandleBuffApplied;
            events = null;
        }

        private void HandleActionStarted(ActionStartInfo info)
        {
            Enqueue(new FxEntry(FxKind.Action, info.Caster, action: info));
        }

        // 피해·회복 사건이 오면 그 유닛의 화면 HP를 "사건 전" 값으로 붙잡는다 → 타격 박자가 재생될 때까지 HP바가 줄지 않는다.
        private void HandleDamageTaken(DamageTakenInfo info)
        {
            ShownHp.Hold(info.Target, info.HpBefore, info.ShieldBefore);
            Enqueue(new FxEntry(FxKind.Hit, info.Target, damage: info));
        }

        // 회복 사건은 회복한 직후에 오므로 회복 전 HP = 지금 HP - 회복량.
        private void HandleHealed(BattleUnit target, int amount)
        {
            ShownHp.Hold(target, target.CurrentHp - amount, target.ShieldAmount);
            Enqueue(new FxEntry(FxKind.Heal, target, healAmount: amount));
        }

        private void HandleBuffApplied(BattleUnit target, StatusInstance buff)
        {
            Enqueue(new FxEntry(FxKind.Buff, target, buffName: buff.Data.DisplayName));
        }

        // 연출을 박자에 담는다. 마지막 박자와 종류가 같고 그 박자에 같은 유닛이 없으면 같은 박자에 넣는다.
        //  - 광역기로 적 3명이 맞음 → 한 박자에 동시 재생
        //  - 같은 적이 화상 3개로 3번 맞음 → 박자 3개로 하나씩 재생 (발동 횟수가 보이도록)
        //  - 타격 뒤 버프 → 종류가 달라 다음 박자 (발동 순서가 보이도록)
        //  - 한 스킬이 같은 유닛에게 버프 여러 개 → 한 박자, 버프 이름을 한 글자 묶음으로 합침 (니콜 3스 치명 확률·가속, 수나 3스 4개)
        private void Enqueue(FxEntry entry)
        {
            List<FxEntry> last = beats.Count > 0 ? beats[beats.Count - 1] : null;

            // 버프는 효과마다 하나씩 따로 보여주면 같은 스킬의 버프가 박자를 나눠 따로따로 떠서 느리다 → 같은 유닛의 버프 글자에 이어 붙인다.
            // FindIndex(조건): 조건이 맞는 첫 원소의 위치, 없으면 -1.
            if (last != null && last[0].Kind == FxKind.Buff && entry.Kind == FxKind.Buff)
            {
                int sameUnit = last.FindIndex(other => other.Unit == entry.Unit);
                if (sameUnit >= 0)
                {
                    FxEntry merged = last[sameUnit];
                    last[sameUnit] = new FxEntry(FxKind.Buff, merged.Unit, buffName: $"{merged.BuffName}\n{entry.BuffName}");
                    return;
                }
            }

            // Exists(조건): 조건(람다식)이 true인 원소가 하나라도 있으면 true → 이미 그 유닛의 연출이 있는 박자인지 확인
            // 행동 시작은 언제나 박자 하나를 따로 쓴다 (컷인·돌진이 끝난 뒤 타격이 나오도록).
            // 일반 타격과 지속피해는 같은 박자에 섞지 않는다 (스킬 타격 → 지속피해 묶음 순서가 보이도록).
            // 지속피해끼리는 같은 유닛이어도 한 박자에 모은다 → 턴 종료·격발로 터진 지속피해 숫자가 한꺼번에 파파팍 뜬다.
            bool isDotBurst = last != null && IsDamageOverTime(entry) && last.TrueForAll(IsDamageOverTime);

            bool canJoin = last != null
                && entry.Kind != FxKind.Action
                && last[0].Kind == entry.Kind
                && IsDamageOverTime(last[0]) == IsDamageOverTime(entry)
                && (isDotBurst || !last.Exists(other => other.Unit == entry.Unit));

            if (canJoin)
            {
                last.Add(entry);
            }
            else
            {
                beats.Add(new List<FxEntry> { entry });
            }

            if (!isRunning)
            {
                StartCoroutine(PlayBeats());
            }
        }

        // 쌓인 박자를 앞에서부터 하나씩 재생한다. 재생 중에 새 박자가 들어와도 이어서 재생한다.
        private IEnumerator PlayBeats()
        {
            isRunning = true;
            attackerPosition = null;
            isUltimateAction = false;

            // 한 번의 재생 = 한 턴(행동 + 턴 종료 지속피해 + 추가 공격)이므로, 시작할 때 피해 합계를 새로 센다.
            // Unity 오브젝트는 ?. 대신 != null로 확인한다 (비어 있는 칸도 Unity식 null로 정확히 판단).
            if (damageTotal != null)
            {
                damageTotal.ResetTotal();
            }

            // 사건은 로직 계산 도중에 들어오므로, 한 프레임 기다려 이번 행동의 사건이 모두 모인 뒤 시작한다.
            // yield return null: 다음 프레임까지 멈춘다.
            yield return null;

            while (beats.Count > 0)
            {
                List<FxEntry> beat = beats[0];
                beats.RemoveAt(0);

                // 행동 시작 박자: 컷인과 돌진을 끝까지 기다린 뒤 다음 박자(타격)로 넘어간다.
                // yield return 코루틴: 그 코루틴이 끝날 때까지 이어서 기다린다.
                if (beat[0].Kind == FxKind.Action)
                {
                    yield return PlayAction(beat[0].Action);
                    continue;
                }

                // 박자 재생에 걸리는 시간(지속피해를 차례로 터뜨리는 시간)과 기본 간격 중 긴 쪽만큼 기다린다.
                float duration = PlayBeat(beat);
                yield return new WaitForSeconds(Mathf.Max(beatInterval, duration));
            }

            yield return new WaitForSeconds(endHold);
            isRunning = false;

            // 연출이 끝났으므로 붙잡아 둔 화면 HP를 놓아 실제 값을 보여준다.
            ShownHp.ReleaseAll();
        }

        // 행동 시작 연출: 아군 3스면 컷인 → 상대를 노리는 행동이면 공격자가 대상 쪽으로 돌진.
        // 다음 박자(타격)는 돌진이 대상에 닿는 순간(나가는 시간) 뒤에 나온다.
        private IEnumerator PlayAction(ActionStartInfo action)
        {
            BattleUnit caster = action.Caster;
            UnitView casterView = stageView.FindView(caster);
            bool isCasterVisible = casterView != null && casterView.gameObject.activeInHierarchy;

            // 3스(칸 번호 2)를 스킬로 직접 쓴 아군만 컷인 (추가 공격·연동 스킬·적은 제외).
            isUltimateAction = action.SkillSlot == 2 && action.Source == ActionSource.Skill;
            if (isUltimateAction && caster.IsAlly && cutIn != null)
            {
                cutIn.Play(caster.Data.Illustration, caster.Name, action.SkillName);
                yield return new WaitForSecondsRealtime(cutIn.Duration);
            }

            attackerPosition = isCasterVisible ? casterView.transform.position : (Vector3?)null;

            // 노리는 상대의 화면 위치 (보이는 상대만). 없으면 돌진하지 않는다 (버프·회복 스킬 등).
            Vector3? targetPosition = FindOpponentTargetPosition(action);
            if (!isCasterVisible || !targetPosition.HasValue)
            {
                yield break;
            }

            casterView.PlayLunge(targetPosition.Value);
            yield return new WaitForSeconds(casterView.LungeOutSeconds);
        }

        // 행동이 노리는 상대 쪽 위치: 확산의 메인 대상이 보이면 그 자리, 아니면 보이는 상대 대상들의 가운데.
        // 같은 편 대상(버프·회복)만 있으면 null.
        private Vector3? FindOpponentTargetPosition(ActionStartInfo action)
        {
            UnitView primary = action.PrimaryTarget != null ? stageView.FindView(action.PrimaryTarget) : null;
            if (primary != null && primary.gameObject.activeInHierarchy && action.PrimaryTarget.IsAlly != action.Caster.IsAlly)
            {
                return primary.transform.position;
            }

            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (BattleUnit target in action.Targets)
            {
                if (target.IsAlly == action.Caster.IsAlly)
                {
                    continue;
                }

                UnitView view = stageView.FindView(target);
                if (view != null && view.gameObject.activeInHierarchy)
                {
                    sum += view.transform.position;
                    count++;
                }
            }

            return count > 0 ? sum / count : (Vector3?)null;
        }

        // 박자 하나를 재생한다. 타격 박자면 박자 전체에서 가장 강한 타격 하나만큼 화면을 흔들고 멈춘다
        // (광역기로 여럿이 맞아도 흔들림·멈춤은 한 번).
        // 돌려주는 값 = 이 박자를 다 보여주는 데 걸리는 시간 (지속피해 묶음이면 마지막 숫자가 뜰 때까지).
        private float PlayBeat(List<FxEntry> beat)
        {
            // 지속피해 묶음: 숫자를 아주 짧은 간격으로 차례로 흩뿌린다 (손맛 연출 없음, 합계·HP만 반영).
            // TrueForAll(조건): 모든 원소가 조건을 만족하면 true.
            if (beat.TrueForAll(IsDamageOverTime))
            {
                StartCoroutine(PlayDotBurst(beat));
                return dotPopupStagger * beat.Count;
            }

            float stop = 0f;
            float shake = 0f;
            bool hasKill = false;

            foreach (FxEntry entry in beat)
            {
                // 화면 HP를 이 박자 순간의 값으로 맞춘다 (숨겨진 유닛도 카드 HP는 보이므로 연출 재생과 상관없이).
                if (entry.Kind != FxKind.Action)
                {
                    ShownHp.Set(entry.Unit, entry.HpAfter, entry.ShieldAfter);
                }

                Play(entry);

                if (entry.Kind != FxKind.Hit || !IsVisible(entry.Unit))
                {
                    continue;
                }

                DamageTakenInfo damage = entry.Damage;
                AddToTotal(damage);

                // 지속피해·막힌 피해는 손맛 연출(멈춤·흔들림) 없이 숫자만.
                if (damage.IsBlocked || damage.IsDamageOverTime)
                {
                    continue;
                }

                // Mathf.Max: 박자 안에서 가장 센 값만 남긴다.
                stop = Mathf.Max(stop, damage.IsCritical ? criticalHitStopSeconds : hitStopSeconds);
                shake = Mathf.Max(shake, damage.IsCritical ? criticalShake : hitShake);

                if (damage.IsLethal)
                {
                    hasKill = true;
                    stop = Mathf.Max(stop, killHitStopSeconds);
                    shake = Mathf.Max(shake, killShake);
                }
            }

            // 카메라 연출·시간 조절이 연결되지 않은 씬에서도 오류 없이 건너뛴다.
            if (shake > 0f && cameraDirector != null)
            {
                cameraDirector.Shake(isUltimateAction ? shake * ultimateShakeMultiplier : shake, shakeSeconds);
            }

            if (timeFx != null)
            {
                if (stop > 0f)
                {
                    timeFx.HitStop(stop);
                }

                // 처치: 멈춤이 풀린 뒤 이어서 느리게 흐른다 (멈춤이 슬로모션보다 우선).
                if (hasKill)
                {
                    timeFx.SlowMotion(killHitStopSeconds + killSlowMotionSeconds);
                }
            }

            return 0f;
        }

        // 지속피해 피해 연출인지 (화상·독 발동, 격발로 터진 지속피해).
        private static bool IsDamageOverTime(FxEntry entry)
        {
            return entry.Kind == FxKind.Hit && entry.Damage.IsDamageOverTime;
        }

        // 지속피해 숫자를 dotPopupStagger 간격으로 하나씩 띄운다. 화면 HP·피해 합계도 숫자가 뜰 때 함께 반영된다.
        private IEnumerator PlayDotBurst(List<FxEntry> beat)
        {
            foreach (FxEntry entry in beat)
            {
                ShownHp.Set(entry.Unit, entry.HpAfter, entry.ShieldAfter);
                Play(entry);

                if (IsVisible(entry.Unit))
                {
                    AddToTotal(entry.Damage);
                }

                yield return new WaitForSeconds(dotPopupStagger);
            }
        }

        // 피해 합계에 더한다. 막힌 피해(무적·무효)는 0이라 빠진다. 적이 맞았으면 아군이 준 피해.
        private void AddToTotal(DamageTakenInfo damage)
        {
            if (damageTotal == null || damage.IsBlocked)
            {
                return;
            }

            damageTotal.Add(damage.Amount, alliesDealt: !damage.Target.IsAlly, isBig: damage.IsCritical || damage.IsLethal);
        }

        private bool IsVisible(BattleUnit unit)
        {
            UnitView view = stageView.FindView(unit);
            return view != null && view.gameObject.activeInHierarchy;
        }

        // 연출 하나를 재생한다: 유닛 위치에 파티클 + 머리 위에 숫자·글자.
        // 퇴장해서 숨겨진 유닛(하이퍼캐리 변신 중 아군)은 건너뛴다.
        private void Play(FxEntry entry)
        {
            UnitView view = stageView.FindView(entry.Unit);
            if (view == null || !view.gameObject.activeInHierarchy)
            {
                return;
            }

            Vector3 basePosition = view.transform.position;
            switch (entry.Kind)
            {
                case FxKind.Hit:
                    PlayHit(entry.Damage, basePosition);
                    PlayHitMotion(view, entry.Damage);
                    break;
                case FxKind.Heal:
                    SpawnEffect(healFx, basePosition);
                    SpawnPopup(basePosition, $"+{entry.HealAmount}", healColor, 1f);
                    break;
                case FxKind.Buff:
                    SpawnEffect(buffFx, basePosition);
                    SpawnPopup(basePosition, entry.BuffName, buffColor, 0.7f);
                    break;
            }
        }

        // 맞은 캡슐의 반응: 처치면 날아가며 넘어지고, 아니면 번쩍 + 밀림. 지속피해는 작게 번쩍·흔들림만.
        private void PlayHitMotion(UnitView view, DamageTakenInfo damage)
        {
            if (damage.IsBlocked)
            {
                view.PlayHitReaction(attackerPosition, 0.05f);
                return;
            }

            if (damage.IsLethal)
            {
                view.PlayKnockdown(damage.IsDamageOverTime ? null : attackerPosition);
                return;
            }

            float push = damage.IsDamageOverTime ? 0.08f : damage.IsCritical ? criticalPush : hitPush;
            view.PlayHitReaction(damage.IsDamageOverTime ? null : attackerPosition, push);
        }

        // 피해 종류에 맞는 이펙트와 숫자를 고른다.
        //  - 무적: 회색 "무적" / 지속피해: 주황 작은 숫자 / 치명: 노란 큰 숫자 + "!" + 큰 이펙트 / 그 외: 흰 숫자
        //  - 보호막이 흡수한 양이 있으면 숫자 아래 줄에 함께 적는다
        private void PlayHit(DamageTakenInfo damage, Vector3 basePosition)
        {
            if (damage.IsBlocked)
            {
                SpawnEffect(hitFx, basePosition);
                SpawnPopup(basePosition, damage.BlockedLabel, blockedColor, 0.8f);
                return;
            }

            string shieldText = damage.Absorbed > 0 ? $"\n(보호막 {damage.Absorbed})" : string.Empty;

            if (damage.IsDamageOverTime)
            {
                // 화상 빨강·독 초록처럼 지속피해 종류마다 상태효과 데이터에 정한 색. 모르면 기본 지속피해 색.
                // 넓게 흩뿌려 여러 개가 한꺼번에 떠도 겹치지 않고 "파파팍" 터지는 느낌을 준다.
                Color color = damage.DamageOverTimeSource != null ? damage.DamageOverTimeSource.DamagePopupColor : damageOverTimeColor;
                SpawnEffect(damageOverTimeFx, basePosition);
                SpawnPopup(basePosition, $"{damage.Amount}{shieldText}", color, 0.9f, dotPopupScatter);
            }
            else if (damage.IsCritical)
            {
                SpawnEffect(criticalHitFx, basePosition);
                SpawnPopup(basePosition, $"{damage.Amount}!{shieldText}", criticalColor, 1.4f);
            }
            else
            {
                SpawnEffect(hitFx, basePosition);
                SpawnPopup(basePosition, $"{damage.Amount}{shieldText}", damageColor, 1f);
            }
        }

        // 파티클 프리팹을 유닛 몸 높이에 하나 만든다. 재생이 끝나면 프리팹 설정(Stop Action = Destroy)으로 스스로 지워진다.
        private void SpawnEffect(ParticleSystem prefab, Vector3 basePosition)
        {
            if (prefab == null)
            {
                return;
            }

            Instantiate(prefab, basePosition + Vector3.up * effectHeight + GetTowardCamera(), Quaternion.identity, transform);
        }

        // 머리 위에 숫자·글자를 띄운다.
        // scatter: 흩뿌릴 범위 (가로, 세로). 넘기지 않으면(null) 좌우로만 조금.
        // Vector2?: 값이 없을 수도 있는 2D 값. ?? : 왼쪽이 null이면 오른쪽 기본값을 쓴다.
        private void SpawnPopup(Vector3 basePosition, string message, Color color, float scale, Vector2? scatter = null)
        {
            if (popupPrefab == null)
            {
                return;
            }

            // 연달아 뜨는 숫자가 정확히 겹치지 않도록 흩뜨린다. Random.Range(a, b): a~b 사이 무작위 실수.
            // 가로는 카메라 오른쪽 방향, 세로는 위 방향으로 흩뿌려 화면에서 고르게 퍼져 보인다.
            Vector2 range = scatter ?? new Vector2(0.3f, 0f);
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            Vector3 jitter = right * Random.Range(-range.x, range.x) + Vector3.up * Random.Range(-range.y * 0.3f, range.y);
            Vector3 position = basePosition + Vector3.up * popupHeight + jitter + GetTowardCamera();
            DamagePopup popup = Instantiate(popupPrefab, position, Quaternion.identity, transform);
            popup.Show(message, color, scale);
        }

        // 카메라 쪽으로 towardCamera만큼 당기는 이동량. -forward: 카메라가 바라보는 방향의 반대 = 카메라 쪽.
        private Vector3 GetTowardCamera()
        {
            Camera mainCamera = Camera.main;
            return mainCamera != null ? -mainCamera.transform.forward * towardCamera : Vector3.zero;
        }
    }
}
