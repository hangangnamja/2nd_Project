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

        [Header("보호막·HP바")]
        [Tooltip("보호막이 다 깎이는 순간 터지는 파편 이펙트")]
        [SerializeField] private ParticleSystem shieldBreakFx;
        [SerializeField] private Color shieldBreakColor = new Color(0.5f, 0.85f, 1f);

        [Tooltip("최대 HP의 이 비율 이상 맞으면 그 유닛의 HP바(아군 카드·보스 바)를 흔든다")]
        [SerializeField, Range(0f, 1f)] private float hpShakeThreshold = 0.05f;

        [Tooltip("HP바 흔들림 세기 = 맞은 비율 × 이 값 (픽셀), 최대 hpShakeMax")]
        [SerializeField] private float hpShakePerRatio = 70f;
        [SerializeField] private float hpShakeMax = 16f;

        [Header("연출 다단 히트")]
        [Tooltip("다단 히트 한 타 사이 간격(초)")]
        [SerializeField, Min(0.01f)] private float multiHitInterval = 0.1f;

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

        // 지금 행동의 연출 타수 (스킬 데이터의 "연출 타수", 스킬이 아니면 1).
        private int currentActionHits = 1;

        // 스킬 전용 연출(메테오 등)이 아직 다 닿지 않은 대상 → 이 대상의 타격 박자는 연출이 닿는 만큼씩 나눠 보여준다.
        // HashSet: 같은 값을 한 번만 담는 집합. Contains로 들어 있는지 빠르게 확인한다.
        private readonly HashSet<BattleUnit> awaitingImpact = new HashSet<BattleUnit>();

        // 연출이 보낸 "이 대상에게 피해의 이만큼을 보여줘" 알림 (대상, 비율). 타격 박자가 꺼내 쓴다.
        private readonly List<(BattleUnit Unit, float Share)> impactQueue = new List<(BattleUnit, float)>();

        // 대상마다 지금까지 보여준 피해 비율 (0 ~ 1). 1이 되면 그 대상의 타격이 끝난 것.
        private readonly Dictionary<BattleUnit, float> shownRatio = new Dictionary<BattleUnit, float>();

        [Tooltip("연출의 일부 타격(잔챙이 메테오 등)의 손맛 세기 (1 = 일반 타격)")]
        [SerializeField, Range(0f, 1f)] private float partialImpactScale = 0.35f;

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
                    // 다음 행동(추가 공격 등)을 시작하기 전에, 앞 행동에서 쓰러지는 타격까지 보여준 유닛을 화면에서도 쓰러뜨린다
                    // (회색·카드 흐림·보스 바 숨김). 로직 순서 "공격 → 피해 → 사망 처리 → 추가 공격"을 화면도 같은 순서로 보여준다.
                    ShownHp.ReleaseFallen();
                    yield return PlayAction(beat[0].Action);
                    continue;
                }

                // 전용 연출이 아직 닿지 않은 대상이 든 타격 박자: 연출이 그 대상에 닿는 순서대로 하나씩 재생한다.
                if (beat[0].Kind == FxKind.Hit && beat.Exists(entry => awaitingImpact.Contains(entry.Unit)))
                {
                    yield return PlayHitsOnImpact(beat);
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

        // 행동 시작 연출 (스킬·추가 공격·눈에 보이는 패시브 모두):
        //  1) 행동하는 유닛이 지금 화면에 없으면(다른 아군의 추가 공격 등) 그 유닛이 보이는 장면으로 바꾼다
        //  2) 아군 3스면 컷인
        //  3) 상대를 노리는 행동이면 대상 쪽으로 돌진(공격 모션), 아니면 제자리에서 떠오르며 빛남(버프·회복 모션)
        // 다음 박자(타격·버프)는 돌진이 닿는 순간 / 가장 높이 떠오른 순간에 나온다.
        private IEnumerator PlayAction(ActionStartInfo action)
        {
            BattleUnit caster = action.Caster;
            bool targetsOpponents = HasTarget(action, opponents: true);
            bool targetsTeam = HasTarget(action, opponents: false);

            UnitView casterView = stageView.FindView(caster);
            bool isCasterVisible = casterView != null && casterView.gameObject.activeInHierarchy;
            if (!isCasterVisible && casterView != null && caster.IsOnField && cameraDirector != null)
            {
                yield return cameraDirector.ShowActionBy(caster, targetsOpponents, targetsTeam);
                isCasterVisible = casterView.gameObject.activeInHierarchy;
            }

            // 3스(칸 번호 2)를 스킬로 직접 쓴 아군만 컷인 (추가 공격·연동 스킬·적은 제외).
            isUltimateAction = action.SkillSlot == 2 && action.Source == ActionSource.Skill;

            // ?.: 스킬이 아닌 행동(추가 공격·패시브)이면 Skill이 null → ?? 로 1타.
            currentActionHits = action.Source == ActionSource.Skill ? action.Skill?.PresentationHits ?? 1 : 1;
            if (isUltimateAction && caster.IsAlly && cutIn != null)
            {
                cutIn.Play(caster.Data.Illustration, caster.Name, action.SkillName);
                yield return new WaitForSecondsRealtime(cutIn.Duration);
            }

            attackerPosition = isCasterVisible ? casterView.transform.position : (Vector3?)null;

            if (!isCasterVisible)
            {
                yield break;
            }

            // 스킬 전용 연출이 있으면 기본 돌진보다 먼저 확인해 그 연출을 띄운다 (시전자는 기를 모으듯 떠오른다).
            // 연출은 따로 돌고, 타격 박자는 연출이 대상에 닿을 때 재생된다 (PlayHitsOnImpact) → 여기서는 기다리지 않고 바로 넘어간다.
            SkillFx skillFx = FindSkillFx(action);
            if (skillFx != null)
            {
                casterView.PlayCast();
                StartSkillFx(skillFx, action, casterView.transform.position);
                yield break;
            }

            // 노리는 상대의 화면 위치 (보이는 상대만). 있으면 공격 돌진, 없으면(버프·회복·자기 강화) 시전 모션.
            Vector3? targetPosition = FindOpponentTargetPosition(action);
            if (targetPosition.HasValue)
            {
                casterView.PlayLunge(targetPosition.Value);
                yield return new WaitForSeconds(casterView.LungeOutSeconds);
                yield break;
            }

            casterView.PlayCast();
            yield return new WaitForSeconds(casterView.CastRiseSeconds);
        }

        // 쓴 스킬의 전용 연출 프리팹에서 SkillFx를 찾는다. 스킬 행동이 아니거나 프리팹이 없으면 null.
        // ?.: Skill이 null이면 PresentationPrefab을 읽지 않고 null.
        private static SkillFx FindSkillFx(ActionStartInfo action)
        {
            GameObject prefab = action.Skill?.PresentationPrefab;
            return prefab != null ? prefab.GetComponent<SkillFx>() : null;
        }

        // 전용 연출을 만들어 재생한다. 화면에 보이는 상대 대상을 넘기고, 연출이 닿을 때마다 (대상, 비율)을 impactQueue에 쌓는다.
        private void StartSkillFx(SkillFx prefab, ActionStartInfo action, Vector3 casterPosition)
        {
            var targets = new List<(BattleUnit Unit, Vector3 Position)>();
            awaitingImpact.Clear();
            impactQueue.Clear();
            shownRatio.Clear();
            foreach (BattleUnit target in action.Targets)
            {
                UnitView view = stageView.FindView(target);
                if (target.IsAlly != action.Caster.IsAlly && view != null && view.gameObject.activeInHierarchy)
                {
                    targets.Add((target, view.transform.position));
                    awaitingImpact.Add(target);
                }
            }

            SkillFx fx = Instantiate(prefab, casterPosition, Quaternion.identity);

            // 람다 (unit, share) => ...: 연출이 대상에 닿을 때마다 "그 대상 피해의 share만큼 보여줘"를 쌓는다.
            StartCoroutine(RunSkillFx(fx.Play(casterPosition, targets, (unit, share) => impactQueue.Add((unit, share)), cameraDirector)));
        }

        // 연출 코루틴을 끝까지 돌린 뒤, 아직 다 보여주지 않은 대상은 남은 피해를 모두 보여주게 한다 (타격 박자가 영원히 기다리지 않게).
        private IEnumerator RunSkillFx(IEnumerator routine)
        {
            yield return routine;
            foreach (BattleUnit unit in awaitingImpact)
            {
                impactQueue.Add((unit, 1f));
            }
        }

        // 전용 연출의 타격 박자: 기다릴 필요 없는 대상은 바로 보여주고, 기다리는 대상은 연출이 닿을 때마다 그 비율만큼 나눠 보여준다.
        //  - 일부(잔챙이 메테오 등): 피해의 일부만 숫자·HP 감소로 보여주고 손맛은 약하게
        //  - 마지막(남은 비율을 다 채움): 남은 피해를 원래 세기로 (연출 타수가 2 이상이면 남은 피해를 다시 여러 타로 나눔)
        // 다 보여준 대상은 기다림 목록에서 빼서, 이후의 타격(지속피해 등)은 평소대로 재생된다.
        private IEnumerator PlayHitsOnImpact(List<FxEntry> beat)
        {
            // 기다리는 대상 → 그 대상의 타격 연출. Dictionary: 대상으로 바로 찾기 위해.
            var pending = new Dictionary<BattleUnit, FxEntry>();
            var now = new List<FxEntry>();
            foreach (FxEntry entry in beat)
            {
                if (awaitingImpact.Contains(entry.Unit) && !pending.ContainsKey(entry.Unit))
                {
                    pending[entry.Unit] = entry;
                }
                else
                {
                    now.Add(entry);
                }
            }

            if (now.Count > 0)
            {
                PlayBeat(now);
            }

            while (pending.Count > 0)
            {
                if (impactQueue.Count > 0)
                {
                    // 쌓인 알림을 한꺼번에 꺼낸다 (같은 프레임에 닿은 메테오 여러 개 = 한 번의 손맛).
                    var arrived = new List<(BattleUnit Unit, float Share)>(impactQueue);
                    impactQueue.Clear();
                    yield return PlayArrivedImpacts(arrived, pending);
                }

                yield return null;
            }

            yield return new WaitForSeconds(beatInterval);
        }

        // 도착한 알림들을 처리한다: 남은 비율을 다 채우지 않는 것은 일부 타격, 채우는 것은 마지막 타격.
        private IEnumerator PlayArrivedImpacts(List<(BattleUnit Unit, float Share)> arrived, Dictionary<BattleUnit, FxEntry> pending)
        {
            float shake = 0f;
            float stop = 0f;
            bool hasKill = false;
            var finals = new List<(FxEntry Entry, float From)>();

            foreach ((BattleUnit unit, float share) in arrived)
            {
                // TryGetValue: 그 대상이 아직 기다리는 중이면 entry에 담고 true.
                if (!pending.TryGetValue(unit, out FxEntry entry))
                {
                    continue;
                }

                float from = shownRatio.TryGetValue(unit, out float shown) ? shown : 0f;
                float to = Mathf.Min(1f, from + share);
                if (to >= 0.999f)
                {
                    finals.Add((entry, from));
                    pending.Remove(unit);
                    awaitingImpact.Remove(unit);
                    continue;
                }

                PlayPortion(entry, from, to, partialImpactScale, ref shake, ref stop, ref hasKill);
                shownRatio[unit] = to;
            }

            ApplyImpact(shake, stop, hasKill);

            if (finals.Count > 0)
            {
                yield return PlayMultiHit(finals, currentActionHits);
            }
        }

        // 행동 대상에 상대(opponents = true) 또는 시전자 말고 같은 편(false)이 있는지. 화면에 보이는지와 상관없이 로직의 대상으로 판단한다.
        private static bool HasTarget(ActionStartInfo action, bool opponents)
        {
            foreach (BattleUnit target in action.Targets)
            {
                bool isOpponent = target.IsAlly != action.Caster.IsAlly;
                if (opponents ? isOpponent : !isOpponent && target != action.Caster)
                {
                    return true;
                }
            }

            return false;
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

            // 연출 타수가 2 이상인 행동의 타격 박자: 피해를 나눠 빠르게 연달아 보여준다.
            if (currentActionHits > 1 && beat[0].Kind == FxKind.Hit)
            {
                // ConvertAll(람다): 박자의 타격마다 (연출, 시작 비율 0) 묶음으로 바꾼다.
                StartCoroutine(PlayMultiHit(beat.ConvertAll(entry => (entry, 0f)), currentActionHits));
                return multiHitInterval * (currentActionHits - 1);
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
                PlayShieldBreak(entry.Unit, damage.ShieldBefore, damage.ShieldAfter);
                RequestHpShake(damage, damage.Amount);

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

            ApplyImpact(shake, stop, hasKill);
            return 0f;
        }

        // 타격 박자 한 번의 손맛: 화면 흔들림·히트스톱·처치 슬로모션.
        private void ApplyImpact(float shake, float stop, bool hasKill)
        {
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
        }

        // 연출 다단 히트: 대상마다 "From부터 끝(1)까지" 남은 피해를 hits등분해 multiHitInterval 간격으로 나눠 보여준다.
        // 로직 피해는 이미 한 번에 들어갔고, 화면만 나눈다 (합계·최종 HP는 같음). 중간 타는 약하게, 처치는 마지막 타에만.
        private IEnumerator PlayMultiHit(List<(FxEntry Entry, float From)> items, int hits)
        {
            hits = Mathf.Max(1, hits);
            for (int hit = 1; hit <= hits; hit++)
            {
                bool isFinal = hit == hits;
                float shake = 0f;
                float stop = 0f;
                bool hasKill = false;

                foreach ((FxEntry entry, float from) in items)
                {
                    if (entry.Kind != FxKind.Hit)
                    {
                        continue;
                    }

                    // 이번 타가 맡는 구간: 남은 부분(1 - from)을 hits등분한 (hit - 1)번째 ~ hit번째.
                    float segmentFrom = from + (1f - from) * (hit - 1) / hits;
                    float segmentTo = isFinal ? 1f : from + (1f - from) * hit / hits;
                    PlayPortion(entry, segmentFrom, segmentTo, isFinal ? 1f : 0.45f, ref shake, ref stop, ref hasKill);
                }

                ApplyImpact(shake, stop, hasKill);
                if (!isFinal)
                {
                    yield return new WaitForSeconds(multiHitInterval);
                }
            }
        }

        // 한 대상의 피해 중 from ~ to 비율 구간을 보여준다: 화면 HP·보호막, 숫자, 이펙트, 맞는 모션, 피해 합계.
        // to가 1이면 마지막 구간이라 남은 피해를 정확히 다 보여주고, 처치·보호막 깨짐도 이때만.
        // 손맛(흔들림·멈춤) 세기는 박자 안에서 가장 센 값을 남기도록 ref로 받아 갱신한다 (ref: 부른 쪽 변수를 직접 바꾼다).
        private void PlayPortion(FxEntry entry, float from, float to, float impactScale, ref float shake, ref float stop, ref bool hasKill)
        {
            DamageTakenInfo damage = entry.Damage;
            bool isFinal = to >= 0.999f;

            // 화면 HP·보호막: 전 값에서 줄어든 양의 to 비율만큼 뺀 값 (마지막이면 정확히 사건 후 값).
            int hp = isFinal ? entry.HpAfter : Mathf.RoundToInt(Mathf.Lerp(damage.HpBefore, entry.HpAfter, to));
            int shield = isFinal ? entry.ShieldAfter : Mathf.RoundToInt(Mathf.Lerp(damage.ShieldBefore, entry.ShieldAfter, to));
            ShownHp.Set(entry.Unit, hp, shield);

            UnitView view = stageView.FindView(entry.Unit);
            if (view == null || !view.gameObject.activeInHierarchy)
            {
                return;
            }

            // 이번 구간의 피해·흡수량 = (전체 × to) - (전체 × from), 반올림. 마지막 구간은 전체 × 1이라 합계가 정확히 맞는다.
            int amount = Portion(damage.Amount, from, to);
            int absorbed = Portion(damage.Absorbed, from, to);
            var part = new DamageTakenInfo(entry.Unit, amount, absorbed, damage.IsCritical, damage.IsDamageOverTime, damage.IsBlocked,
                damage.BlockedLabel, isLethal: isFinal && damage.IsLethal, hpBefore: damage.HpBefore, damageOverTimeSource: damage.DamageOverTimeSource);

            PlayHit(part, view.transform.position);
            PlayHitMotion(view, part);
            AddToTotal(part);
            RequestHpShake(damage, amount);
            if (isFinal)
            {
                PlayShieldBreak(entry.Unit, damage.ShieldBefore, damage.ShieldAfter);
            }

            if (damage.IsBlocked || damage.IsDamageOverTime)
            {
                return;
            }

            stop = Mathf.Max(stop, (damage.IsCritical ? criticalHitStopSeconds : hitStopSeconds) * impactScale);
            shake = Mathf.Max(shake, (damage.IsCritical ? criticalShake : hitShake) * impactScale);
            if (part.IsLethal)
            {
                hasKill = true;
                stop = Mathf.Max(stop, killHitStopSeconds);
                shake = Mathf.Max(shake, killShake);
            }
        }

        // 전체 total 중 from ~ to 비율 구간의 몫 (반올림 차이로 합계가 어긋나지 않게 누적값끼리 뺀다).
        private static int Portion(int total, float from, float to)
        {
            return Mathf.RoundToInt(total * to) - Mathf.RoundToInt(total * from);
        }

        // 보호막이 이번 피해로 다 깎였으면(전 > 0, 후 = 0) 파편 이펙트와 "보호막 파괴" 글자.
        private void PlayShieldBreak(BattleUnit unit, int shieldBefore, int shieldAfter)
        {
            if (shieldBefore <= 0 || shieldAfter > 0)
            {
                return;
            }

            UnitView view = stageView.FindView(unit);
            if (view == null || !view.gameObject.activeInHierarchy)
            {
                return;
            }

            SpawnEffect(shieldBreakFx, view.transform.position);
            SpawnPopup(view.transform.position + Vector3.up * 0.4f, "보호막 파괴", shieldBreakColor, 0.75f);
        }

        // 최대 HP 대비 이번에 깎인 양이 기준 이상이면 그 유닛의 HP바를 흔들라고 알린다. 치명은 조금 더 세게.
        private void RequestHpShake(DamageTakenInfo damage, int amount)
        {
            if (damage.IsBlocked || damage.Target.MaxHp <= 0)
            {
                return;
            }

            float ratio = (float)amount / damage.Target.MaxHp;
            if (ratio < hpShakeThreshold)
            {
                return;
            }

            float strength = Mathf.Min(hpShakeMax, ratio * hpShakePerRatio) * (damage.IsCritical ? 1.3f : 1f);
            HpBarFeedback.RequestShake(damage.Target, strength);
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
