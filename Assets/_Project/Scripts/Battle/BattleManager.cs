using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.Battle
{
    // 전투 흐름을 진행하는 관리자 (3단계: 스킬 사용 + 수동 조작).
    // 흐름: 다음 행동자 결정 → (아군이면 플레이어 입력 대기 / 적이면 AI) → 스킬 실행 → 턴 종료 → 승패 판정
    // 화면(유닛 표시, HUD)은 이 클래스를 직접 건드리지 않고, 아래 이벤트를 구독해서 스스로 갱신한다 (옵저버 패턴).
    //
    // 플레이어 조작은 "스킬 먼저 → 대상 선택" 순서다.
    //  - 아군 차례가 시작되면 1스(평타)가 자동 선택된다.
    //  - 단일 대상 스킬: 강조된 대상 중 하나를 클릭하면 즉시 실행
    //  - 광역·자신 스킬: 같은 스킬 버튼을 한 번 더 누르거나, 강조된 대상을 클릭하면 실행
    public class BattleManager : MonoBehaviour
    {
        // 선택된 스킬이 없음을 나타내는 슬롯 번호.
        public const int NoSkillSelected = -1;

        // 이번 전투에 무엇을 넣을지 (아군 편성·레벨, 스테이지). 지금은 씬에서 바로 실행할 때 쓰는 테스트 설정 에셋이다.
        [Header("전투 설정 (아군 편성·레벨, 스테이지)")]
        [SerializeField] private BattleSetup setup;

        // 로비에서 넘겨받은 전투인지. 이길 때만 스테이지 클리어를 세이브에 기록한다 (씬을 바로 실행한 테스트 전투는 기록하지 않음).
        private bool isLaunchedFromLobby;

        [Header("전투 규칙")]
        [SerializeField] private BattleRulesData rules;

        [Tooltip("난수 시드. 0이면 매번 다른 결과, 0이 아니면 같은 시드에서 치명타·무작위 대상이 항상 똑같이 나온다")]
        [SerializeField] private int randomSeed;

        [Header("진행 설정")]
        [Tooltip("적 AI가 행동하기 전 대기 시간(초). 적의 행동을 눈으로 따라가기 위한 값")]
        [SerializeField, Min(0f)] private float enemyActionDelay = 0.6f;

        [Tooltip("자동 전투 때 아군 차례가 오고 스킬을 쓰기까지 기다리는 시간(초). 고른 스킬 구도를 잠깐 보여준다")]
        [SerializeField, Min(0f)] private float autoActionDelay = 0.4f;

        private readonly List<BattleUnit> allies = new List<BattleUnit>();
        private readonly List<BattleUnit> enemies = new List<BattleUnit>();
        private TurnScheduler scheduler;
        private int actionCount;

        // 아군 + 적 전원 (행동 순서, 패시브 반응 대상 탐색용).
        // 스케줄러·패시브 처리기·공용 도구가 이 리스트를 그대로 참조하므로, 웨이브가 바뀌면 이 리스트의 내용만 교체한다.
        private readonly List<BattleUnit> allUnits = new List<BattleUnit>();

        // 웨이브 순서 관리 (지금 몇 번째 웨이브인지, 다음 웨이브가 있는지).
        private StageRunner stageRunner;

        // 전투 공용 도구(로그, 사건, 대기열, 난수, 규칙)와 사건에 반응하는 패시브 처리기. 전투 시작 시 만든다.
        private BattleServices services;
        private PassiveRunner passiveRunner;
        private ActionQueue actionQueue => services.Actions;

        // 이번 차례에 실행하기로 확정된 행동. 확정 전에는 hasPendingAction이 false다.
        // pendingTarget은 단일 대상 스킬일 때만 쓰고, 광역·자신 스킬은 대상 규칙으로 다시 계산한다.
        // 연출 재생기 (없으면 기다리지 않는다 → 화면 없이도 전투가 돈다).
        private IBattlePresentation presentation;

        private bool hasPendingAction;
        private int pendingSkillSlot;
        private BattleUnit pendingTarget;

        // event Action: 구독자(화면 스크립트)들에게 "무언가 바뀌었다"를 알리는 통로.
        // 구독자는 += 로 함수를 등록하고, 이 클래스가 Invoke하면 등록된 함수가 모두 실행된다.
        public event Action BattleStarted;
        public event Action StateChanged;
        public event Action<string> LogAdded;

        // 다음 웨이브의 적이 들어왔을 때 (첫 웨이브는 BattleStarted로 알린다). 화면은 적 캡슐을 새 적으로 교체한다.
        public event Action WaveStarted;

        // 전투 중 유닛이 새로 들어왔을 때 (부하 소환). 화면은 새 유닛의 캡슐을 만들고 지금 장면을 다시 세운다.
        public event Action UnitsSpawned;

        // 웨이브 표시용 (1부터). 전투 시작 전에는 1/1로 보인다.
        // ?. / ??: stageRunner가 아직 없으면(null) 읽지 않고 기본값을 쓴다.
        public int CurrentWaveNumber => (stageRunner?.WaveIndex ?? 0) + 1;
        public int WaveCount => stageRunner?.WaveCount ?? 1;

        // 전투 중 사건 통로. 연출 화면이 BattleStarted 때 구독한다 (전투 시작 전에는 null).
        public BattleEvents Events => services?.Events;

        public IReadOnlyList<BattleUnit> Allies => allies;
        public IReadOnlyList<BattleUnit> Enemies => enemies;
        public BattleUnit CurrentActor { get; private set; }
        public bool IsWaitingForPlayer { get; private set; }
        public bool IsBattleOver { get; private set; }

        // 자동 전투가 켜져 있는지. 켜져 있으면 아군 차례에 AllyAutoPilot이 스킬·대상을 정한다.
        public bool IsAutoBattle { get; private set; }

        // 전투가 끝났을 때 승리했는지 (결과 화면용). 전투 중에는 false.
        public bool IsVictory { get; private set; }

        // 이번 전투의 스테이지 (결과 화면용). 전투 시작 전에는 null.
        public StageData Stage => stageRunner?.Stage;

        // 이번 전투에서 행동한 횟수 (결과 화면용).
        public int ActionCount => actionCount;

        // 이번 전투로 받은 보상 (결과 화면용). 지거나 테스트 전투(씬 직접 실행)면 빈 목록.
        public IReadOnlyList<(MaterialData Material, int Amount)> Rewards { get; private set; } = Array.Empty<(MaterialData, int)>();

        // 업적·별 미션용 전투 기록 (처치·치명타·스킬 사용·쓰러진 아군). 전투가 끝나면 세이브의 누적 기록에 더한다.
        private BattleRecord battleRecord;

        // 전투에 걸린 시간(초, 별 미션 "시간 안에 클리어" 판정용). Update가 전투 중에만 더한다.
        public float BattleSeconds { get; private set; }

        // 이번 전투에서 별 미션을 달성했는지 (스테이지의 별 미션 순서대로, 결과 화면용). 지면 모두 false.
        public IReadOnlyList<bool> StarsAchieved { get; private set; } = Array.Empty<bool>();

        // 이번 전투로 처음 달성한 별인지 (결과 화면 NEW 표시용). 테스트 전투(씬 직접 실행)는 기록하지 않으므로 모두 false.
        public IReadOnlyList<bool> StarsNewlyAchieved { get; private set; } = Array.Empty<bool>();

        // 지금 행동이 추가 턴인지 (HUD 표시용).
        public bool IsExtraTurn { get; private set; }

        // 지금 행동이 하이퍼캐리 변신 중의 엑스트라 턴인지. 이 턴이 끝날 때는 버프·디버프 남은 턴이 줄지 않는다.
        public bool IsTransformTurn { get; private set; }

        // 플레이어가 지금 고른 스킬 슬롯 (0 = 1스). 고른 스킬이 없으면 NoSkillSelected.
        public int SelectedSkillSlot { get; private set; } = NoSkillSelected;

        // scheduler?.CurrentCycle: 스케줄러가 아직 없으면(null) 읽지 않고 null을 돌려준다.
        // ?? 1: 앞의 결과가 null이면 대신 1을 쓴다. 전투 시작 전 HUD가 읽어도 오류 없이 1사이클로 표시된다.
        public int CurrentCycle => scheduler?.CurrentCycle ?? 1;

        // 앞으로의 행동 순서 예측 (행동 순서 UI용). 맨 앞이 지금 행동 중인 유닛이다. 전투 전·후에는 빈 목록.
        public IReadOnlyList<BattleUnit> PredictActionOrder(int count)
        {
            if (scheduler == null || IsBattleOver)
            {
                return Array.Empty<BattleUnit>();
            }

            return scheduler.PredictActionOrder(CurrentActor, count);
        }

        // 씬이 시작되면 설정을 확인하고, 유닛과 스케줄러를 만든 뒤 전투를 시작한다.
        // 로비에서 넘겨받은 설정이 있으면 그걸 쓰고, 없으면(씬을 바로 실행) Inspector의 테스트 설정을 쓴다.
        private void Start()
        {
            if (Game.Core.BattleLaunch.TryTake(out BattleSetup launched))
            {
                setup = launched;
                isLaunchedFromLobby = true;
            }

            if (!HasValidSetup())
            {
                return;
            }

            CreateServices();
            battleRecord = new BattleRecord(services.Events);
            stageRunner = new StageRunner(setup.Stage);
            CreateAllies();
            SpawnCurrentWave();
            scheduler = new TurnScheduler(allUnits);
            passiveRunner = new PassiveRunner(services, allUnits);

            // 등장 시 패시브(시작 부하 소환 등)를 화면이 캡슐을 만들기 전에 실행한다 → 소환된 부하도 처음부터 자리에 선다.
            // new List<>(allUnits): 소환으로 allUnits가 늘어나도 순회가 깨지지 않도록 복사본을 넘긴다.
            passiveRunner.HandleUnitsEntered(new List<BattleUnit>(allUnits));

            // ?.Invoke: 구독자가 하나도 없으면(null) 호출하지 않고 넘어간다. 화면이 없는 씬에서도 안전하게 동작한다.
            BattleStarted?.Invoke();

            // StartCoroutine: RunBattle을 여러 프레임에 걸쳐 실행한다.
            // 코루틴 안의 yield return 지점에서 잠시 멈췄다가 다음 프레임(또는 조건 충족 시)에 이어서 실행된다.
            StartCoroutine(RunBattle());
        }

        // 전투 시간 재기. Time.deltaTime은 배속(timeScale)이 곱해진 시간이라, 3배속이면 실제 1초에 3초가 쌓인다
        // → "1배속으로 봤을 때의 전투 시간"이 되어 배속을 올려도 별 미션이 쉬워지지 않는다. 일시정지(timeScale 0)는 0초.
        // 단, 플레이어가 스킬을 고르는 동안은 실제 시간(unscaledDeltaTime)으로 잰다 → 3배속에서 생각하는 시간이 3배로 불어나지 않게.
        private void Update()
        {
            if (scheduler == null || IsBattleOver || Time.timeScale <= 0f)
            {
                return;
            }

            BattleSeconds += IsWaitingForPlayer ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        // 오브젝트가 파괴될 때(씬 종료, 플레이 중지) 패시브 처리기의 사건 구독을 해제한다.
        private void OnDestroy()
        {
            passiveRunner?.Dispose();
        }

        // ---------- 화면(HUD, 클릭)에서 호출하는 함수 ----------

        // 자동 전투를 켜고 끈다. 아군 차례를 기다리는 중에 켜면 그 차례부터 자동으로 행동한다.
        public void SetAutoBattle(bool enabled)
        {
            IsAutoBattle = enabled;
            StateChanged?.Invoke();
        }

        // 스킬 버튼을 눌렀을 때 호출한다.
        // 다른 스킬이면 선택을 바꾸고, 이미 선택된 광역·자신 스킬을 다시 누르면 실행을 확정한다.
        public void SelectSkill(int slot)
        {
            if (!IsWaitingForPlayer || !CurrentActor.CanUseSkill(slot))
            {
                return;
            }

            bool isSecondPress = slot == SelectedSkillSlot;
            if (isSecondPress && !NeedsSingleTarget(GetSelectedSkill()))
            {
                ConfirmAction(slot, target: null);
                return;
            }

            SelectedSkillSlot = slot;
            StateChanged?.Invoke();
        }

        // 유닛을 클릭했을 때 호출한다. 선택된 스킬의 대상이 될 수 있는 유닛이면 실행을 확정한다.
        public void SelectTarget(BattleUnit unit)
        {
            if (!IsWaitingForPlayer || !IsValidTarget(unit))
            {
                return;
            }

            ConfirmAction(SelectedSkillSlot, unit);
        }

        // unit이 지금 선택된 스킬의 대상이 될 수 있는지 확인한다 (화면 강조 표시에도 사용).
        // "적/아군"은 현재 행동자 기준이다.
        public bool IsValidTarget(BattleUnit unit)
        {
            SkillData skill = GetSelectedSkill();

            if (skill == null || unit == null)
            {
                return false;
            }

            bool isOpponent = unit.IsAlly != CurrentActor.IsAlly;

            // 쓰러진 아군도 고를 수 있는 스킬(부활): 퇴장하지 않은 아군이면 살아 있든 쓰러졌든 고를 수 있다.
            if (skill.TargetType == SkillTargetType.AllySingleOrFallen)
            {
                return !isOpponent && !unit.IsExited;
            }

            // 그 외 스킬은 쓰러졌거나 퇴장한 유닛을 고를 수 없다.
            if (!unit.IsOnField)
            {
                return false;
            }

            switch (skill.TargetType)
            {
                case SkillTargetType.EnemySingle:
                case SkillTargetType.EnemyBlast:
                    // 상대편에 시선집중 중인 유닛이 있으면 그 유닛만 고를 수 있다 (적이 시선집중을 쓰는 경우 대비).
                    BattleUnit taunter = FindTaunter(CurrentActor.IsAlly ? enemies : allies);
                    return isOpponent && (taunter == null || unit == taunter);
                case SkillTargetType.EnemyAll:
                    return isOpponent;
                case SkillTargetType.AllySingle:
                case SkillTargetType.AllyAll:
                    return !isOpponent;
                case SkillTargetType.Self:
                    return unit == CurrentActor;
                default:
                    return false;
            }
        }

        // 지금 선택된 스킬 데이터. 선택이 없거나 아군 차례가 아니면 null.
        public SkillData GetSelectedSkill()
        {
            if (CurrentActor == null || SelectedSkillSlot == NoSkillSelected)
            {
                return null;
            }

            // 변신 중이면 변신 스킬이 나오도록 유닛을 거쳐 꺼낸다.
            return CurrentActor.GetSkill(SelectedSkillSlot);
        }

        // 대상을 직접 하나 골라야 하는 스킬인지 (적 단일·아군 단일·확산의 메인 대상).
        public static bool NeedsSingleTarget(SkillData skill)
        {
            return skill != null
                && (skill.TargetType == SkillTargetType.EnemySingle
                    || skill.TargetType == SkillTargetType.AllySingle
                    || skill.TargetType == SkillTargetType.AllySingleOrFallen
                    || skill.TargetType == SkillTargetType.EnemyBlast);
        }

        // ---------- 전투 흐름 ----------

        // 승패가 정해질 때까지 턴을 반복하는 전투 루프. 턴 하나의 처리 순서 (Architecture.md 3장):
        // 행동자 결정(추가 턴이면 같은 유닛) → 스킬·대상 선택 → 스킬 실행 → 턴 종료 → 끌어온 턴(반격 태세) → 추가 공격(임시 턴) 실행
        // → 추가 턴 확인 → 웨이브를 정리했고 다음 웨이브가 있으면 다음 웨이브 시작
        private IEnumerator RunBattle()
        {
            Log($"전투 시작: {stageRunner.Stage.StageName} (적 Lv{stageRunner.Stage.EnemyLevel})");
            Log($"--- 웨이브 {CurrentWaveNumber}/{WaveCount} ---");
            LogUnitStats();
            BattleUnit extraTurnUnit = null;

            while (!IsBattleDecided())
            {
                BeginTurn(extraTurnUnit);

                // yield return 다른 코루틴: 그 코루틴이 끝날 때까지 여기서 기다린다.
                yield return PerformCurrentAction(preferredOpponent: null);
                EndTurn(CurrentActor);

                // 이번 턴에 상대의 턴을 끌어왔으면(반격 태세) 그 턴들을 먼저 진행한다.
                // 그동안 이번 턴에 예약된 추가 공격(반격·종결기)은 맡아 두었다가 끌어온 턴이 모두 끝난 뒤 실행된다.
                yield return RunPulledTurns();

                // 추가 공격은 행동자의 턴이 완전히 끝난 뒤, 각자의 임시 턴으로 실행한다 (기획서 3-4).
                ProcessFollowUpAttacks();

                // 이번 턴에 추가 턴을 얻었으면, 다음 반복에서 스케줄러를 거치지 않고 같은 유닛이 바로 행동한다.
                extraTurnUnit = actionQueue.ConsumeExtraTurn();

                // 이번 턴에 쌓인 연출(타격·회복·버프)을 다 재생한 뒤에 넘어간다.
                // 웨이브 전환보다 먼저 기다려야, 쓰러진 적의 이펙트가 적 교체 전에 보인다.
                yield return WaitForPresentation();

                // 웨이브 전환은 추가 공격까지 모두 끝난 뒤(턴 사이)에만 한다 → 반격·종결기가 남은 채 적이 바뀌지 않는다.
                if (!IsAllDead(allies) && StageRunner.IsWaveCleared(enemies) && stageRunner.HasNextWave)
                {
                    StartNextWave();
                }
            }

            FinishBattle();
        }

        // 연출 재생기를 연결한다. 연출 화면이 전투 시작(BattleStarted) 때 자신을 넘긴다.
        public void SetPresentation(IBattlePresentation battlePresentation)
        {
            presentation = battlePresentation;
        }

        // 연출 재생이 끝날 때까지 기다린다. 재생기가 없으면 바로 넘어간다.
        // WaitUntil(조건): 매 프레임 조건(람다식)을 확인해 true가 될 때까지 코루틴을 멈춘다.
        private IEnumerator WaitForPresentation()
        {
            if (presentation == null)
            {
                yield break;
            }

            yield return new WaitUntil(() => !presentation.IsPlaying);
        }

        // 지금 행동자의 행동을 진행한다: 기절·공연이면 건너뛰고, 아군이면 플레이어 입력, 적이면 AI가 스킬을 골라 실행한다.
        // preferredOpponent: 적 AI가 단일·확산 공격을 고르고 대상을 정하지 않았을 때 노릴 유닛 (끌어온 턴의 시전자). null이면 무작위.
        private IEnumerator PerformCurrentAction(BattleUnit preferredOpponent)
        {
            // 기절·공연 중이면 행동(스킬 선택·사용)을 건너뛰고 바로 턴을 마친다.
            // 턴 종료 처리는 그대로라 기절·공연의 남은 턴이 줄고, "공격하지 않은 턴" 패시브도 발동한다.
            if (CurrentActor.IsStunned || CurrentActor.IsPerforming)
            {
                string reason = CurrentActor.IsStunned ? "기절로 행동 불가" : $"{CurrentActor.ActiveAura.Name} 중 (행동 스킵)";
                Log($"  {CurrentActor.Name}: {reason}");
                yield return new WaitForSeconds(enemyActionDelay);
            }
            else if (CurrentActor.IsAlly)
            {
                yield return WaitForPlayerAction();
                UseSkill(CurrentActor, pendingSkillSlot, pendingTarget);
            }
            else
            {
                yield return new WaitForSeconds(enemyActionDelay);
                DecideEnemyAction(CurrentActor);
                AimAtPreferredOpponent(preferredOpponent);
                UseSkill(CurrentActor, pendingSkillSlot, pendingTarget);
            }
        }

        // 적 AI가 고른 스킬이 상대 단일·확산 공격이고 대상을 정하지 않았으면, preferredOpponent를 노리게 한다.
        // 광역은 대상 지정이 필요 없고, 아군·자신 스킬에 상대를 넣으면 대상이 잘못되므로 건드리지 않는다.
        // 시선집중 중인 유닛이 있으면 그쪽이 먼저다 (ResolveTargets → PickSingleOpponent).
        private void AimAtPreferredOpponent(BattleUnit preferredOpponent)
        {
            if (preferredOpponent == null || !preferredOpponent.IsOnField || pendingTarget != null)
            {
                return;
            }

            SkillTargetType targetType = CurrentActor.GetSkill(pendingSkillSlot).TargetType;
            if (targetType == SkillTargetType.EnemySingle || targetType == SkillTargetType.EnemyBlast)
            {
                pendingTarget = preferredOpponent;
            }
        }

        // 끌어온 턴(반격 태세, 기획서 4-3)을 예약된 순서대로 진행한다.
        // 끌어온 턴은 그 유닛의 실제 정규 턴이다: 스케줄러로 시간을 흘리지 않고 지금 바로 행동하며, 턴 종료 처리(게이지 리셋 포함)를 한다.
        // 끌어오기 전에 예약된 추가 공격(반격·종결기)은 맡아 두었다가 모든 끌어온 턴이 끝난 뒤 대기열에 되돌린다
        //   → "상대의 공격이 모두 끝난 뒤 반격 → 종결기" 순서가 지켜진다.
        // 끌어온 턴 안에서 생긴 추가 공격은 그 턴이 끝날 때 바로 실행한다 (일반 턴과 같음).
        private IEnumerator RunPulledTurns()
        {
            if (!actionQueue.HasPulledTurns)
            {
                yield break;
            }

            List<FollowUpAttack> heldFollowUps = actionQueue.TakeAllFollowUps();

            while (!IsBattleDecided() && actionQueue.TryDequeuePulledTurn(out PulledTurn pulled))
            {
                // 끌어온 유닛이 그사이 쓰러졌거나 퇴장했으면 건너뛴다.
                if (!pulled.Unit.IsOnField)
                {
                    continue;
                }

                // 앞 턴의 연출을 다 보여준 뒤 다음 끌어온 턴을 시작한다.
                yield return WaitForPresentation();

                StartTurnOf(pulled.Unit, hasExtraTurn: false);
                Log($"[끌어온 턴] {pulled.Unit.Name}");
                yield return PerformCurrentAction(preferredOpponent: pulled.Target);
                EndTurn(CurrentActor);
                ProcessFollowUpAttacks();
            }

            // 승패가 정해져 남은 끌어온 턴이 있으면 버린다 (다음 전투·웨이브로 넘어가지 않게).
            while (actionQueue.TryDequeuePulledTurn(out _))
            {
            }

            actionQueue.RestoreFollowUps(heldFollowUps);
        }

        // 턴을 시작한다. 추가 턴이면 같은 유닛이 다시 행동하고, 아니면 스케줄러가 다음 행동자를 정한다.
        private void BeginTurn(BattleUnit extraTurnUnit)
        {
            bool hasExtraTurn = extraTurnUnit != null && !extraTurnUnit.IsDead;
            StartTurnOf(hasExtraTurn ? extraTurnUnit : scheduler.AdvanceToNextActor(), hasExtraTurn);
        }

        // actor의 턴을 시작한다 (정규 턴·추가 턴·끌어온 턴 공통).
        // 새 턴마다 패시브의 "턴당 발동 횟수"를 초기화한다 (추가 턴·끌어온 턴도 새 턴이다).
        private void StartTurnOf(BattleUnit actor, bool hasExtraTurn)
        {
            IsExtraTurn = hasExtraTurn;
            CurrentActor = actor;
            hasPendingAction = false;
            passiveRunner.ResetTurnCounters(CurrentActor);

            // 자기 턴 표시: 이 턴 중에 얻은 행동 당기기는 턴 종료 리셋 뒤로 미뤄진다 (기획서 4-3 강탈 딜러 패시브 1).
            CurrentActor.BeginOwnTurn();

            // "자기 턴 시작 시" 패시브 (보스 2 피격 무효 충전). 턴당 횟수 초기화 뒤라 매 턴 발동할 수 있다.
            passiveRunner.HandleOwnTurnStart(CurrentActor);

            // 역류 재생이 이번 턴 시작에 "지난 자기 턴 이후 받은 피해"를 읽었으므로, 여기서부터 다시 센다.
            CurrentActor.ResetDamageTakenSinceOwnTurn();

            // 변신 중인 유닛이 속도로 차례를 얻으면, 그 턴은 정규 턴이 아니라 엑스트라 턴이다 (기획서 4-3).
            // 엑스트라 턴을 1 세고, 턴 종료 때 버프·디버프 남은 턴을 줄이지 않는다.
            // 변신 3스를 쓴 턴은 변신 전에 시작했으므로 세지 않는다 → 변신 후 엑스트라 턴을 제한 횟수만큼 온전히 쓴다.
            IsTransformTurn = !hasExtraTurn && CurrentActor.IsTransformed;
            if (IsTransformTurn)
            {
                CurrentActor.Transformation.CountTurn();
                Log($"[엑스트라 턴 {CurrentActor.Transformation.TurnsTaken}/{CurrentActor.Transformation.Data.TurnLimit}] {CurrentActor.Name}");
            }

            if (hasExtraTurn)
            {
                Log($"[추가 턴] {CurrentActor.Name}");
            }

            StateChanged?.Invoke();
        }

        // 대기열에 쌓인 추가 공격을 들어온 순서대로 모두 실행한다. 추가 공격 하나 = 조작할 수 없는 임시 턴 하나 (기획서 3-4).
        // 임시 턴: 공격 → 사망 확정 → 턴 종료 단계 효과. 정규 턴 종료 처리(지속피해, 남은 턴·쿨타임 감소, 게이지)는 없다.
        // 패시브 턴당 횟수도 초기화하지 않는다 → 횟수는 추가 공격을 일으킨 정규 턴에 묶여, 연쇄가 반드시 끝난다.
        private void ProcessFollowUpAttacks()
        {
            while (!IsBattleDecided() && actionQueue.TryDequeueFollowUp(out FollowUpAttack followUp))
            {
                // 쓰러졌거나 퇴장한 유닛의 추가 공격은 나가지 않는다.
                // 다타 추가 공격이면 예약 표시만 풀어 둔다 → 쌓인 타수는 남아 다음 발동 때 쓰인다.
                if (!followUp.Owner.IsOnField)
                {
                    if (followUp.HitCounter != null)
                    {
                        followUp.HitCounter.IsQueued = false;
                    }

                    // ?.Invoke: 끝났을 때 부를 함수가 있을 때만 부른다 (건너뛴 경우도 "끝남"으로 알림).
                    followUp.Completed?.Invoke();
                    continue;
                }

                // 돌파 반응처럼 타수가 쌓이는 추가 공격은 따로 처리한다 (여러 번 때리고, 도중에 타수가 늘 수 있음).
                if (followUp.HitCounter != null)
                {
                    ExecuteMultiHitFollowUp(followUp);
                    ConfirmDeaths();
                    ProcessTurnEndEffects();
                    continue;
                }

                List<BattleUnit> targets = ResolveFollowUpTargets(followUp);
                if (targets.Count == 0)
                {
                    // 칠 대상이 없어도(웨이브의 적을 이미 다 잡음) 변신 종결기였다면 변신은 해제해야 한다.
                    // 안 그러면 다음 웨이브까지 변신이 풀리지 않고 아군이 퇴장한 채로 남는다.
                    if (followUp.EndsTransformation)
                    {
                        EndTransformation(followUp.Owner);
                    }

                    followUp.Completed?.Invoke();
                    continue;
                }

                // string.Join + ConvertAll: 대상 목록을 "이름, 이름" 형태의 한 줄로 만든다 (로그용).
                string targetNames = string.Join(", ", targets.ConvertAll(unit => unit.Name));
                Log($"[추가 공격] {followUp.Owner.Name}: {followUp.Name} → {targetNames}");

                SkillExecutor.ExecuteEffects(
                    followUp.Owner,
                    followUp.Effects,
                    targets,
                    primaryTarget: null,
                    services,
                    ActionSource.FollowUpAttack,
                    skillName: followUp.Name,
                    skill: followUp.Skill,
                    presentationPrefab: followUp.PresentationPrefab);

                // 추가 공격도 하나의 공격이므로, 끝날 때 HP 0인 유닛의 사망을 확정한다.
                ConfirmDeaths();

                // 이 추가 공격이 끝났음을 알린다 (영역의 마지막 공격: 공격·사망 확정 뒤에 영역이 끝남).
                followUp.Completed?.Invoke();

                // 변신 종결기였으면 변신을 해제하고 퇴장한 아군을 복귀시킨다.
                if (followUp.EndsTransformation)
                {
                    EndTransformation(followUp.Owner);
                }

                // 임시 턴의 종료 단계: 이 추가 공격의 처치로 예약된 효과를 남의 턴으로 미루지 않고 여기서 적용한다.
                ProcessTurnEndEffects();
            }

            // 추가 공격은 정규 턴 종료 알림 이후에 실행되므로, 바뀐 HP 등을 화면에 다시 알린다.
            StateChanged?.Invoke();
        }

        // 다타 추가 공격 (돌파 반응, 기획서 4-3): 쌓인 타수만큼 적 전체에게 같은 효과를 반복한다.
        //  - 한 타 칠 때마다 남은 타수에서 1을 빼고, 그 타격으로 새로 돌파한 적이 있으면 패시브가 타수를 더한다 → 이어서 친다
        //  - 적(무적 제외)이 모두 HP 0이 되면 남은 타수와 관계없이 끝나고, 남은 타수는 사라진다
        //  - 처음부터 칠 적이 없으면 한 타도 치지 않고 타수를 그대로 남긴다 → 다음 발동 때 더해진다
        // 사망 확정은 추가 공격 하나가 끝난 뒤 한 번만 한다 (기획서 3-9: 공격 도중 HP 0이어도 공격이 끝나야 사망).
        private void ExecuteMultiHitFollowUp(FollowUpAttack followUp)
        {
            HitCounter counter = followUp.HitCounter;
            int hitNumber = 0;

            while (counter.Pending > 0 && HasStandingEnemy(followUp.Owner))
            {
                counter.Pending--;
                hitNumber++;

                // FindAll(조건): 조건이 true인 원소만 새 목록으로 모은다 → 이번 타에서 HP 0이 된 적은 다음 타에서 빼고 친다.
                List<BattleUnit> targets = ResolveFollowUpTargets(followUp).FindAll(unit => !unit.IsDown);
                string targetNames = string.Join(", ", targets.ConvertAll(unit => unit.Name));
                Log($"[추가 공격] {followUp.Owner.Name}: {followUp.Name} {hitNumber}타 (남은 {counter.Pending}) → {targetNames}");

                SkillExecutor.ExecuteEffects(
                    followUp.Owner,
                    followUp.Effects,
                    targets,
                    primaryTarget: null,
                    services,
                    ActionSource.FollowUpAttack);
            }

            // 한 타 이상 쳤는데 적이 다 쓰러져 끝났다면, 남은 타수는 저장하지 않고 버린다.
            if (hitNumber > 0 && !HasStandingEnemy(followUp.Owner) && counter.Pending > 0)
            {
                Log($"  {followUp.Name}: 적이 모두 쓰러져 남은 {counter.Pending}타 소멸");
                counter.Pending = 0;
            }

            // 실행이 끝났으므로, 이후 새로 쌓이는 타수는 새 추가 공격으로 예약된다.
            counter.IsQueued = false;
        }

        // owner의 상대편에 아직 HP가 남은(무적 제외) 유닛이 있는지. 무적 버프몹만 남으면 더 칠 의미가 없으므로 없다고 본다.
        private bool HasStandingEnemy(BattleUnit owner)
        {
            // Exists(조건): 조건(람다식)이 true인 원소가 하나라도 있으면 true.
            return allUnits.Exists(unit =>
                unit.IsAlly != owner.IsAlly && unit.IsOnField && !unit.IsDown && !unit.IsInvulnerable);
        }

        // 공격이나 턴 종료 처리가 끝난 시점에 HP가 0인 유닛의 사망을 확정한다 (기획서 3-9).
        // 공격 도중에는 HP가 0이어도 남은 효과를 끝까지 받고, 여기서 확정된 뒤부터 행동·대상 선택에서 빠진다.
        private void ConfirmDeaths()
        {
            foreach (BattleUnit unit in allUnits)
            {
                if (unit.ConfirmDeathIfDown())
                {
                    Log($"  {unit.Name} 쓰러짐");

                    // 처치 사건은 사망이 확정된 뒤에 알린다 → 처치 시 패시브가 확정된 결과를 기준으로 반응한다.
                    // LastAttacker: 마지막으로 피해를 준 유닛(지속피해면 그 효과의 부여자)을 처치자로 본다.
                    services.Events.RaiseUnitKilled(unit, unit.LastAttacker);

                    // 아군을 퇴장시킨 유닛이 쓰러졌으면 퇴장한 아군을 바로 복귀시킨다 (남겨진 채 전멸 판정이 나지 않도록).
                    ReturnExitedAllies(unit);
                }
            }

            // 소환한 유닛이 쓰러졌으면 그 부하들도 함께 퇴장한다 (처치가 아니므로 처치 반응 없음).
            // 순회가 끝난 뒤에 따로 확인한다 → 같은 공격에서 보스와 부하가 함께 쓰러져도 부하의 처치 사건은 위에서 정상으로 나간다.
            foreach (BattleUnit unit in allUnits)
            {
                if (unit.IsSummon && !unit.IsDead && unit.Summoner.IsDead)
                {
                    unit.Vanish();
                    Log($"  {unit.Name} 퇴장 ({unit.Summoner.Name} 쓰러짐)");
                }
            }

            // 경계를 펼친 유닛이 쓰러졌으면 경계도 끝난다.
            services.Boundaries.CheckEnd();
        }

        // 변신을 해제한다: 스킬 세트가 원래대로 돌아가고, 3스로 퇴장했던 아군이 퇴장 전 게이지 그대로 복귀한다.
        // 변신과 함께 끝나는 경계(고립)도 여기서 끝나 스탯 증가·행동 제어 면역이 사라진다.
        private void EndTransformation(BattleUnit unit)
        {
            if (!unit.IsTransformed)
            {
                return;
            }

            unit.EndTransformation();
            Log($"  {unit.Name}: 변신 해제");
            ReturnExitedAllies(unit);
            services.Boundaries.CheckEnd();
            Log($"  {unit.Name}: HP {unit.CurrentHp}/{unit.MaxHp}");
        }

        // unit이 퇴장시켰던 아군을 복귀시키고 기록한다. 변신 해제와 퇴장시킨 유닛의 사망 때 부른다.
        private void ReturnExitedAllies(BattleUnit unit)
        {
            foreach (BattleUnit ally in unit.ReturnExitedAllies())
            {
                Log($"  {ally.Name} 복귀");
            }
        }

        // 추가 공격의 대상 목록을 정한다.
        // 단일·확산은 노리던 대상(쓰러졌으면 다시 고른 대상)을 기준으로, 광역·자신·아군은 대상 규칙 그대로 정한다.
        private List<BattleUnit> ResolveFollowUpTargets(FollowUpAttack followUp)
        {
            bool aimsSingleEnemy = followUp.TargetType == SkillTargetType.EnemySingle
                || followUp.TargetType == SkillTargetType.EnemyBlast;

            if (!aimsSingleEnemy)
            {
                return ResolveTargets(followUp.Owner, followUp.TargetType, followUp.PreferredTarget);
            }

            BattleUnit target = PickFollowUpTarget(followUp);
            return target == null
                ? new List<BattleUnit>()
                : ResolveTargets(followUp.Owner, followUp.TargetType, target);
        }

        // 추가 공격 대상: 원래 대상이 살아 있으면 그대로, 쓰러졌으면 살아 있는 상대 중에서 다시 고른다.
        // 다시 고를 때는 현재 HP가 가장 높은 적 → 같으면 편성 순서 (기획서 4-3 격발 딜러 패시브 2 규칙).
        // 보스 우선 규칙은 보스 개념이 들어올 때 추가한다.
        private BattleUnit PickFollowUpTarget(FollowUpAttack followUp)
        {
            if (followUp.PreferredTarget != null && followUp.PreferredTarget.IsOnField)
            {
                return followUp.PreferredTarget;
            }

            BattleUnit best = null;
            foreach (BattleUnit unit in followUp.Owner.IsAlly ? enemies : allies)
            {
                if (!unit.IsOnField)
                {
                    continue;
                }

                // 목록이 편성 순서대로 되어 있으므로, HP가 "더 높을 때만" 바꾸면 같은 HP에서는 앞 번호가 남는다.
                if (best == null || unit.CurrentHp > best.CurrentHp)
                {
                    best = unit;
                }
            }

            return best;
        }

        // 승패가 정해졌는지: 아군 전멸(패배) 또는 승리.
        private bool IsBattleDecided()
        {
            return IsAllDead(allies) || IsStageCleared();
        }

        // 승리: 마지막 웨이브에서 쓰러뜨려야 하는 적(무적 제외)을 모두 잡았을 때 (기획서 3-9).
        // 마지막이 아닌 웨이브를 정리한 것은 승리가 아니라 웨이브 전환이다 (RunBattle에서 처리).
        private bool IsStageCleared()
        {
            return !stageRunner.HasNextWave && StageRunner.IsWaveCleared(enemies);
        }

        // 아군 차례에 플레이어가 스킬과 대상을 확정할 때까지 기다린다. 시작할 때 1스(평타)를 미리 선택해 둔다.
        private IEnumerator WaitForPlayerAction()
        {
            SelectedSkillSlot = FindFirstUsableSkill(CurrentActor);
            IsWaitingForPlayer = true;
            StateChanged?.Invoke();

            // 행동이 확정될 때까지 매 프레임 기다린다. 그사이 자동 전투가 켜져 있으면 자동으로 행동을 정한다.
            while (!hasPendingAction)
            {
                if (IsAutoBattle)
                {
                    yield return PerformAutoAction();
                }
                else
                {
                    yield return null;
                }
            }

            IsWaitingForPlayer = false;
            SelectedSkillSlot = NoSkillSelected;
        }

        // 자동 전투 한 차례: AllyAutoPilot이 정한 스킬을 먼저 고른 상태로 보여 주고(카메라가 그 스킬 구도로),
        // 잠깐 뒤 그 대상으로 확정한다. 기다리는 사이 자동이 꺼졌거나 플레이어가 먼저 확정했으면 아무것도 하지 않는다.
        private IEnumerator PerformAutoAction()
        {
            // 튜플 분해: Decide가 돌려준 (칸, 대상)을 두 변수에 나눠 담는다.
            (int slot, BattleUnit target) = AllyAutoPilot.Decide(CurrentActor, allies, enemies);
            if (slot == NoSkillSelected)
            {
                slot = FindFirstUsableSkill(CurrentActor);
            }

            SelectedSkillSlot = slot;
            StateChanged?.Invoke();
            yield return new WaitForSeconds(autoActionDelay);

            if (IsAutoBattle && !hasPendingAction && slot != NoSkillSelected)
            {
                ConfirmAction(slot, target);
            }
        }

        // 플레이어 행동을 확정한다. 대기 중인 코루틴이 다음 프레임에 hasPendingAction을 보고 실행으로 넘어간다.
        private void ConfirmAction(int slot, BattleUnit target)
        {
            pendingSkillSlot = slot;
            pendingTarget = target;
            hasPendingAction = true;
        }

        // 적 AI 행동 결정 (기획서 3-10).
        //  - 패턴이 있는 적(EnemyCharacterData): 패턴 목록을 위에서부터 확인해 조건이 맞고 쓸 수 있는 첫 패턴을 실행
        //  - 그 외 / 맞는 패턴이 없음: 1스(평타), 대상은 대상 규칙대로 자동
        private void DecideEnemyAction(BattleUnit enemy)
        {
            // 패턴 매칭 is: Data가 EnemyCharacterData 타입이면 그 타입으로 꺼내 enemyData에 담고 true.
            if (enemy.Data is EnemyCharacterData enemyData)
            {
                foreach (EnemyPattern pattern in enemyData.Patterns)
                {
                    if (IsPatternReady(enemy, pattern))
                    {
                        Log($"  [패턴] {enemy.Name}: {pattern.PatternName}");
                        ConfirmAction(pattern.SkillSlot, PickPatternTarget(enemy, pattern.Target));
                        return;
                    }
                }
            }

            ConfirmAction(slot: 0, target: null);
        }

        // 패턴을 지금 쓸 수 있는지: 그 스킬의 쿨타임이 끝났고, 조건이 맞아야 한다.
        private static bool IsPatternReady(BattleUnit enemy, EnemyPattern pattern)
        {
            if (!enemy.CanUseSkill(pattern.SkillSlot))
            {
                return false;
            }

            switch (pattern.Condition)
            {
                case PatternCondition.SelfBuffCountAtLeast:
                    // Statuses.Buffs.Count: 자신에게 걸린 버프 개수. 같은 버프는 중첩되지 않으므로 곧 "버프 종류 수"다.
                    return enemy.Statuses.Buffs.Count >= pattern.Threshold;
                case PatternCondition.OwnTurnAtLeast:
                    // OwnTurnsTaken: 지금 턴까지 시작한 자기 턴 수. 기준 2면 두 번째 자기 턴부터 참 (부하 자폭).
                    return enemy.OwnTurnsTaken >= pattern.Threshold;
                case PatternCondition.SummonsBelow:
                    // 소환한 부하가 기준 수 미만일 때만 소환한다 → 가득 차 있으면 다음 패턴(공격)으로 넘어간다.
                    return enemy.CountLivingSummons() < pattern.Threshold;
                case PatternCondition.SelfDebuffCountBelow:
                    // Statuses.Debuffs.Count: 자신에게 걸린 디버프 칸 수. 기준 1이면 "디버프가 하나도 없을 때"만 참.
                    // 행동을 고르는 시점(턴 시작)에 확인하므로, 지속피해는 아직 발동 전이라 걸려 있는 그대로 센다.
                    return enemy.Statuses.Debuffs.Count < pattern.Threshold;
                case PatternCondition.SelfBuffStacksAtLeast:
                    // CountBuffStacks: 버프 칸의 스택 합. 공세처럼 1칸에 쌓이는 버프는 칸 수가 아니라 스택 수로 센다.
                    return enemy.Statuses.CountBuffStacks() >= pattern.Threshold;
                default:
                    return true;
            }
        }

        // 패턴의 대상 고르기 방식에 따라 대상을 정한다. null이면 스킬의 대상 규칙대로 자동 선택된다.
        private BattleUnit PickPatternTarget(BattleUnit enemy, PatternTarget target)
        {
            if (target != PatternTarget.StrongestAlly)
            {
                return null;
            }

            // 같은 편 중 최대 HP가 가장 높은 유닛 = 보스. 버프몹이 보스에게만 버프를 걸게 한다.
            BattleUnit strongest = null;
            foreach (BattleUnit friend in GetOnFieldUnits(enemy.IsAlly ? allies : enemies))
            {
                if (strongest == null || friend.MaxHp > strongest.MaxHp)
                {
                    strongest = friend;
                }
            }

            return strongest;
        }

        // 스킬 대상을 정하고 실행한다.
        private void UseSkill(BattleUnit caster, int slot, BattleUnit chosenTarget)
        {
            actionCount++;
            SkillData skill = caster.GetSkill(slot);
            List<BattleUnit> targets = ResolveTargets(caster, skill.TargetType, chosenTarget);

            // 확산 스킬은 대상 목록의 첫 번째가 메인 대상이다. 나머지 스킬은 메인 대상 구분이 없다.
            BattleUnit primaryTarget = skill.TargetType == SkillTargetType.EnemyBlast ? targets[0] : null;

            Log($"[사이클 {CurrentCycle} | 행동 {actionCount}]");
            SkillExecutor.ExecuteSkill(caster, slot, targets, primaryTarget, services);

            // 스킬의 모든 효과가 끝난 뒤에 사망을 확정한다. 이후의 추가 공격은 확정된 사망을 기준으로 대상을 고른다.
            ConfirmDeaths();
        }

        // 행동을 마친 유닛의 턴 종료 처리:
        // 상태효과(지속피해 → 남은 턴 감소) → 쿨타임 감소 → 출발점 복귀 → 미뤄둔 행동 당기기 → 턴 종료 단계 패시브.
        private void EndTurn(BattleUnit actor)
        {
            // 변신 상태로 끝나는 턴은 지속피해만 발동하고 버프·디버프 남은 턴은 그대로 둔다.
            // 엑스트라 턴뿐 아니라 변신 3스를 쓴 턴도 포함한다: 변신하는 즉시 엑스트라 턴 상태가 되기 때문이다.
            // (변신 해제는 종결기가 끝난 뒤라, 마지막 엑스트라 턴도 여기서는 아직 변신 상태다.)
            // 쿨타임 감소와 게이지 리셋은 그대로 진행된다 → 변신 스킬 쿨타임이 돌고, 다음 차례는 속도로 다시 얻는다.
            // 자기 행동으로 사라진 유닛(부하 자폭)은 지속피해·남은 턴 처리를 건너뛴다 (이미 전장에 없으므로).
            if (!actor.IsDead)
            {
                StatusTurnProcessor.ProcessTurnEnd(actor, services, decreaseDurations: !actor.IsTransformed);
            }

            // 지속피해로 HP가 0이 됐으면 턴 종료 처리가 끝난 뒤 사망을 확정한다.
            ConfirmDeaths();

            // 이번 턴에 공격하지 않았으면 그 패시브(아스트라 회복)를 발동한다. 지속피해·지속 회복·사망 확정 다음이라
            // 이번 턴 지속피해로 쓰러진 아군은 회복되지 않는다 (부활 없음).
            passiveRunner.HandleOwnTurnEnd(actor);

            // 기간 오라(공연)의 남은 턴을 줄인다. 생성한 턴은 세지 않는다.
            TimedAura endedAura = actor.TickAura();
            if (endedAura != null)
            {
                Log($"  {actor.Name}: {endedAura.Name} 종료");
            }

            // 주인의 턴마다 일하는 경계(영역: 적 전체 참 예약 + 남은 턴 감소). 남은 턴이 0이 되면 여기서 끝난다.
            // 예약한 참은 추가 공격이라, 이 턴의 정규 종료 처리가 다 끝난 뒤 ProcessFollowUpAttacks에서 나간다 (기획서 3-4).
            if (!actor.IsDead)
            {
                services.Boundaries.HandleTurnEnd(actor, services);
            }

            actor.TickCooldowns();
            scheduler.EndTurn(actor);

            // 게이지가 출발점(10000)으로 돌아간 뒤에 이번 턴 중 얻은 당기기를 적용해야 효과가 사라지지 않는다.
            actor.EndOwnTurn();
            ProcessTurnEndEffects();
            QueueFinisherIfTurnLimitReached(actor);
            StateChanged?.Invoke();
        }

        // 변신 턴 제한에 도달했으면 종결기를 추가 공격으로 예약한다 (기획서 4-3 변신 종료).
        // 이번 턴에 예약된 추가 공격(반격 태세의 강제 공격·반격)보다 뒤에 들어가므로, 그것들이 모두 끝난 뒤 종결기가 나간다.
        // 강제 해제로 이미 종결기가 예약됐으면 TryMarkFinisherQueued가 false라 다시 넣지 않는다.
        private void QueueFinisherIfTurnLimitReached(BattleUnit actor)
        {
            if (actor.IsDead || !actor.IsTransformed || !actor.Transformation.HasReachedTurnLimit)
            {
                return;
            }

            if (actor.Transformation.TryMarkFinisherQueued())
            {
                Log($"  {actor.Name}: 변신 턴 종료 → 종결기 예약");
                StatusApplier.QueueFinisher(actor, services);
            }
        }

        // 턴 종료 단계(정규 턴·임시 턴 공통): 예약된 버프를 받고 → 예약된 패시브 효과를 실행한다 (기획서 3-9).
        // 버프 소모 같은 스킬 처리와 턴 종료 상태효과 감소가 모두 끝난 뒤라, 새로 얻은 버프가 바로 깎이거나 소모되지 않는다.
        // 여기서 얻은 버프의 행동 당기기는 게이지 리셋 이후이므로 즉시 적용된다.
        // 받는 쪽이 이미 쓰러졌으면 그 버프·효과는 사라진다.
        private void ProcessTurnEndEffects()
        {
            while (actionQueue.TryDequeueTurnEndBuff(out TurnEndBuff buff))
            {
                if (buff.Receiver.IsDead)
                {
                    continue;
                }

                // 지연 버프가 바뀐 효과: 정의로 새로 건다 (부여자 = 지연 버프를 건 유닛).
                if (buff.Instance == null)
                {
                    Log($"  {buff.Receiver.Name}: {buff.Data.DisplayName} 획득");
                    StatusApplier.Apply(buff.Receiver, buff.Data, buff.Source, services);
                    continue;
                }

                // 강탈한 버프 Receive: 남은 턴 그대로 넣는다. 새 버프면 "버프 획득" 사건 → 강탈 딜러 가속 패시브가 반응한다.
                Log($"  {buff.Receiver.Name}: 강탈한 {buff.Instance.Data.DisplayName} 획득 (남은 {buff.Instance.RemainingTurns}턴)");
                StatusApplier.Receive(buff.Receiver, buff.Instance, services);
            }

            while (actionQueue.TryDequeueTurnEndEffect(out TurnEndEffect effect))
            {
                if (effect.Owner.IsDead)
                {
                    continue;
                }

                passiveRunner.ExecuteTurnEndEffect(effect);
            }
        }

        // 전투가 끝났을 때 승패를 기록하고 화면에 알린다.
        private void FinishBattle()
        {
            IsBattleOver = true;
            IsVictory = IsStageCleared();
            CurrentActor = null;

            string result = IsVictory ? "승리" : "패배";
            Log($"전투 종료: {result} (총 {actionCount}회 행동, {CurrentCycle}사이클)");

            JudgeStarMissions(out StarMissionResult starResult);

            // 로비에서 시작한 전투를 이겼으면 보상(별 보상 포함)을 주고 클리어·별을 저장한다 (씬을 바로 실행한 테스트 전투는 기록하지 않음).
            if (IsVictory && isLaunchedFromLobby)
            {
                Rewards = Game.Core.PlayerProfile.CompleteStage(stageRunner.Stage, starResult);
            }

            // 업적 누적 기록: 로비에서 시작한 전투면 이겼든 졌든 이번 전투의 처치·치명타·스킬 사용 수를 더한다.
            if (isLaunchedFromLobby)
            {
                Game.Core.PlayerProfile.RecordBattle(battleRecord.Kills, battleRecord.CriticalHits, battleRecord.SkillsUsed);
            }
            StateChanged?.Invoke();
        }

        // 별 미션 판정: 이번 전투에서 달성한 별과, 그중 처음 달성한 별을 구한다 (결과 화면용).
        // 처음인지는 저장하기 전(CompleteStage 전)에 세이브 기록을 보고 판단한다. out: 판정에 쓴 결과 값을 저장에도 쓰도록 돌려준다.
        private void JudgeStarMissions(out StarMissionResult result)
        {
            result = new StarMissionResult(IsVictory, BattleSeconds, battleRecord.AlliesFallen);
            IReadOnlyList<StarMission> missions = stageRunner.Stage.StarMissions;
            var achieved = new bool[missions.Count];
            var newly = new bool[missions.Count];

            for (int i = 0; i < missions.Count; i++)
            {
                achieved[i] = missions[i].IsAchieved(result);
                newly[i] = achieved[i] && isLaunchedFromLobby && !Game.Core.PlayerProfile.IsStarAchieved(stageRunner.Stage, i);
            }

            StarsAchieved = achieved;
            StarsNewlyAchieved = newly;
            Log($"별 미션: 전투 시간 {BattleSeconds:F1}초, 쓰러진 아군 {battleRecord.AlliesFallen}명");
        }

        // ---------- 대상 결정 ----------

        // 스킬 대상 규칙에 따라 실제 대상 목록을 만든다. "적/아군"은 시전자 기준이다.
        // chosenTarget은 플레이어가 클릭한 대상이며, 단일 대상 스킬에서만 사용한다.
        private List<BattleUnit> ResolveTargets(BattleUnit caster, SkillTargetType targetType, BattleUnit chosenTarget)
        {
            List<BattleUnit> opponents = caster.IsAlly ? enemies : allies;
            List<BattleUnit> friends = caster.IsAlly ? allies : enemies;

            switch (targetType)
            {
                case SkillTargetType.EnemyAll:
                    return GetOnFieldUnits(opponents);
                case SkillTargetType.AllyAll:
                    return GetOnFieldUnits(friends);
                case SkillTargetType.Self:
                    return new List<BattleUnit> { caster };
                case SkillTargetType.AllySingle:
                case SkillTargetType.AllySingleOrFallen:
                    // ?? caster: 고른 대상이 없으면(적 AI) 자기 자신에게 쓴다.
                    return new List<BattleUnit> { chosenTarget ?? caster };
                case SkillTargetType.EnemyBlast:
                    return GetBlastTargets(opponents, PickSingleOpponent(opponents, chosenTarget));
                default:
                    return new List<BattleUnit> { PickSingleOpponent(opponents, chosenTarget) };
            }
        }

        // 단일·확산 공격의 (메인) 대상을 정한다.
        //  1) 상대편에 시선집중 중인 유닛이 있으면 무조건 그 유닛 (도발 탱커, 기획서 4-3). 고른 대상·추가 공격의 노리던 대상보다 우선
        //  2) 고른 대상이 있으면 그 대상
        //  3) 없으면(적 AI) 상대편 중 무작위 1명
        // ??: 왼쪽이 null이면 오른쪽 값을 쓴다 → 위 순서대로 처음으로 null이 아닌 값이 대상이 된다.
        private BattleUnit PickSingleOpponent(List<BattleUnit> opponents, BattleUnit chosenTarget)
        {
            return FindTaunter(opponents) ?? chosenTarget ?? PickRandomAlive(opponents);
        }

        // 시선집중 중인 유닛을 편성 순서대로 찾는다 (여럿이면 앞 번호). 없으면 null.
        private static BattleUnit FindTaunter(List<BattleUnit> units)
        {
            foreach (BattleUnit unit in units)
            {
                if (unit.IsTaunting)
                {
                    return unit;
                }
            }

            return null;
        }

        // 확산 대상: 메인 대상 + 살아 있는 적 중 편성 순서상 바로 앞·뒤 (기획서 4-3 독 서브딜러 2스).
        // 쓰러진 적은 건너뛰므로, 가운데 적이 쓰러졌으면 그 너머의 적이 "옆"이 된다.
        // 반환 목록의 첫 번째가 메인 대상이다.
        private static List<BattleUnit> GetBlastTargets(List<BattleUnit> opponents, BattleUnit main)
        {
            List<BattleUnit> alive = GetOnFieldUnits(opponents);
            int mainIndex = alive.IndexOf(main);
            var targets = new List<BattleUnit> { main };

            if (mainIndex > 0)
            {
                targets.Add(alive[mainIndex - 1]);
            }

            if (mainIndex >= 0 && mainIndex < alive.Count - 1)
            {
                targets.Add(alive[mainIndex + 1]);
            }

            return targets;
        }

        // 살아 있는 유닛 중 무작위 1명을 고른다 (적 AI의 단일 대상).
        private BattleUnit PickRandomAlive(List<BattleUnit> units)
        {
            List<BattleUnit> alive = GetOnFieldUnits(units);

            // Next(n): 0 이상 n 미만의 정수를 무작위로 뽑는다. 전투 난수를 써서 시드 고정 시 같은 대상이 나온다.
            return alive[services.Random.Next(alive.Count)];
        }

        // 지금 사용할 수 있는 스킬 중 가장 앞 슬롯을 찾는다 (보통 1스). 전부 쿨타임이면 선택 없음.
        private static int FindFirstUsableSkill(BattleUnit unit)
        {
            for (int slot = 0; slot < CharacterData.SkillCount; slot++)
            {
                if (unit.CanUseSkill(slot))
                {
                    return slot;
                }
            }

            return NoSkillSelected;
        }

        // ---------- 준비 ----------

        // 양쪽 편성에 캐릭터가 한 명 이상 있고 빈 슬롯이 없는지 확인한다. 문제가 있으면 전투를 시작하지 않는다.
        private bool HasValidSetup()
        {
            if (rules == null)
            {
                Debug.LogError("[Battle] 전투 규칙(BattleRulesData)이 연결되지 않았습니다.", this);
                return false;
            }

            if (setup == null)
            {
                Debug.LogError("[Battle] 전투 설정(BattleSetup)이 연결되지 않았습니다.", this);
                return false;
            }

            if (IsValidParty(setup.AllyParty) && IsValidStage(setup.Stage))
            {
                return true;
            }

            Debug.LogError("[Battle] 아군 편성 또는 스테이지 웨이브가 비어 있거나 빈 칸이 있습니다.", this);
            return false;
        }

        // 전투 공용 도구를 만든다. 시드가 0이면 시간 기반 난수, 아니면 고정 시드 난수를 쓴다.
        private void CreateServices()
        {
            // new System.Random(): 시드 없이 만들면 매번 다른 순서의 난수가 나온다.
            // new System.Random(seed): 같은 시드면 항상 같은 순서의 난수가 나와 전투를 그대로 재현할 수 있다.
            System.Random random = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            // ResolveTargets: 메서드 이름만 넘기면 델리게이트로 바뀐다 → 효과 블록이 이 대상 결정 규칙을 그대로 빌려 쓴다.
            services = new BattleServices(Log, new BattleEvents(), new ActionQueue(), random, rules, allUnits, ResolveTargets);

            // 소환 블록이 전투 유닛을 새로 만들 수 있도록 이 클래스의 소환 함수를 빌려준다 (대상 결정과 같은 방식).
            services.Summon = SummonUnit;
        }

        // ---------- 소환 (보스 3 군단 지휘관) ----------

        // summoner와 같은 편에 data로 부하를 하나 만들어 전투에 넣고, 만든 유닛을 돌려준다.
        // 처리 순서: 유닛 생성(소환자 레벨) → 부하 표시 → 편 목록에 끼워 넣기 → 전투 유닛 전체 목록에 추가 → 화면에 알림
        // allUnits는 스케줄러·패시브 처리기·공용 도구가 같은 리스트를 참조하므로, 여기에 넣는 것만으로 행동 순서에 들어간다.
        // 새 유닛의 남은 거리는 10000(처음부터)이다.
        private BattleUnit SummonUnit(BattleUnit summoner, CharacterData data)
        {
            List<BattleUnit> side = summoner.IsAlly ? allies : enemies;
            StatScaling scaling = summoner.IsAlly ? rules.GetAllyScaling(summoner.Level) : rules.GetEnemyScaling(summoner.Level);

            var unit = new BattleUnit(data, summoner.IsAlly, formationIndex: side.Count, level: summoner.Level, scaling);
            unit.MarkAsSummon(summoner);
            unit.SetTeam(side);

            side.Insert(PickSummonIndex(side, summoner), unit);
            allUnits.Add(unit);

            Log($"  {summoner.Name}: {unit.Name} 소환");

            // ?.Invoke: 구독자(화면)가 있을 때만 알린다. 화면은 새 유닛의 캡슐을 만들고 지금 장면을 다시 세운다.
            UnitsSpawned?.Invoke();
            return unit;
        }

        // 부하를 끼워 넣을 자리: 소환자의 왼쪽과 오른쪽 중 살아 있는 유닛이 적은 쪽 끝.
        // 편 목록의 순서가 곧 화면의 줄 순서·확산 공격의 양옆이므로, 번갈아 양쪽에 붙여 소환자가 가운데에 남게 한다.
        private static int PickSummonIndex(List<BattleUnit> side, BattleUnit summoner)
        {
            int summonerIndex = side.IndexOf(summoner);
            int leftCount = 0;
            int rightCount = 0;

            for (int i = 0; i < side.Count; i++)
            {
                if (side[i].IsDead || i == summonerIndex)
                {
                    continue;
                }

                // 소환자보다 앞이면 왼쪽, 뒤면 오른쪽에 센다.
                if (i < summonerIndex)
                {
                    leftCount++;
                }
                else
                {
                    rightCount++;
                }
            }

            // 왼쪽이 적거나 같으면 맨 앞(0번), 아니면 맨 뒤에 넣는다.
            return leftCount <= rightCount ? 0 : side.Count;
        }

        // 편성 리스트가 비어 있지 않고, 연결이 빠진(null) 슬롯이 없는지 검사한다.
        private static bool IsValidParty(IReadOnlyList<CharacterData> party)
        {
            if (party == null || party.Count == 0)
            {
                return false;
            }

            foreach (CharacterData data in party)
            {
                if (data == null)
                {
                    return false;
                }
            }

            return true;
        }

        // 스테이지에 웨이브가 하나 이상 있고, 모든 웨이브에 적이 있으며 캐릭터가 빠진 자리가 없는지 검사한다.
        private static bool IsValidStage(StageData stageData)
        {
            if (stageData == null || stageData.Waves.Count == 0)
            {
                return false;
            }

            foreach (StageWave wave in stageData.Waves)
            {
                if (wave == null || wave.Enemies.Count == 0)
                {
                    return false;
                }

                foreach (StageEnemy enemy in wave.Enemies)
                {
                    if (enemy == null || enemy.Character == null)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // 레벨이 반영된 유닛별 스탯을 로그로 남긴다 (밸런스 테스트용, 콘솔에서 확인).
        private void LogUnitStats()
        {
            foreach (BattleUnit unit in allUnits)
            {
                Debug.Log($"[Battle] {unit.Name} Lv{unit.Level}: HP {unit.MaxHp} / 공격력 {unit.Attack} / 방어력 {unit.Defense} / 속도 {unit.Speed:0}");
            }
        }

        // 전투 설정의 아군 편성으로 아군 유닛을 만든다. 리스트 위치가 곧 편성 순서(FormationIndex)가 된다.
        // 아군은 전투 내내 그대로이고, 웨이브가 바뀌어도 다시 만들지 않는다.
        private void CreateAllies()
        {
            allies.Clear();
            actionCount = 0;

            // 아군과 적은 레벨 성장 곡선이 다르다: 적 HP는 아군 공격력 성장을, 적 공격력은 아군 HP 성장을 따라간다 (BattleRulesData).
            // 캐릭터마다 보유 레벨이 다를 수 있으므로, 레벨 배율도 아군마다 따로 구한다.
            for (int i = 0; i < setup.AllyParty.Count; i++)
            {
                // 진급 횟수를 알면(로비에서 시작) 실제 진급 기준, 모르면(테스트 설정) 그 레벨까지 진급을 마친 것으로 본다.
                int level = setup.GetAllyLevel(i);
                int promotion = setup.GetAllyPromotion(i);
                StatScaling allyScaling = promotion >= 0 ? rules.GetAllyScaling(level, promotion) : rules.GetAllyScaling(level);
                allies.Add(new BattleUnit(setup.AllyParty[i], isAlly: true, formationIndex: i, level: level, allyScaling, setup.GetAllyWeapon(i)));
            }

            // 각 유닛에 같은 편 목록을 연결해, 살아 있는 아군의 오라 패시브가 스탯 계산에 반영되게 한다.
            foreach (BattleUnit ally in allies)
            {
                ally.SetTeam(allies);
            }
        }

        // 현재 웨이브의 적을 만들어 적 목록과 전투 유닛 전체 목록을 교체한다.
        // 적 레벨은 스테이지 값을 쓰고, 무적 자리의 적은 무적으로 만든다.
        // allUnits는 스케줄러·패시브 처리기가 참조하는 같은 리스트라, 내용만 바꾸면 모두에게 새 적이 반영된다.
        private void SpawnCurrentWave()
        {
            StageData stage = stageRunner.Stage;
            StatScaling enemyScaling = rules.GetEnemyScaling(stage.EnemyLevel);
            IReadOnlyList<StageEnemy> slots = stageRunner.CurrentWave.Enemies;

            enemies.Clear();
            for (int i = 0; i < slots.Count; i++)
            {
                var unit = new BattleUnit(slots[i].Character, isAlly: false, formationIndex: i, level: stage.EnemyLevel, enemyScaling);

                if (slots[i].Invulnerable)
                {
                    unit.MakeInvulnerable();
                }

                enemies.Add(unit);
            }

            foreach (BattleUnit enemy in enemies)
            {
                enemy.SetTeam(enemies);
            }

            allUnits.Clear();
            allUnits.AddRange(allies);
            allUnits.AddRange(enemies);
        }

        // 다음 웨이브로 넘어간다 (기획서 3-9 웨이브 전환).
        // 아군의 버프·디버프·쿨타임·변신 상태는 그대로 두고, 행동 게이지만 처음(10000)으로 되돌린다.
        // 새 적은 만들어질 때 이미 게이지가 10000이다.
        private void StartNextWave()
        {
            stageRunner.MoveToNextWave();
            SpawnCurrentWave();

            // 새 웨이브 적의 등장 시 패시브 (시작 부하 소환). 복사본을 넘겨 소환으로 목록이 늘어나도 안전하게 순회한다.
            passiveRunner.HandleUnitsEntered(new List<BattleUnit>(enemies));

            foreach (BattleUnit ally in allies)
            {
                ally.ResetDistance();
            }

            Log($"--- 웨이브 {CurrentWaveNumber}/{WaveCount} ---");
            LogUnitStats();

            // ?.Invoke: 구독자(화면)가 있을 때만 알린다. 화면은 적 캡슐을 새 적으로 교체한다.
            WaveStarted?.Invoke();
            StateChanged?.Invoke();
        }

        // ---------- 공용 도우미 ----------

        // 콘솔과 화면 로그에 같은 내용을 남긴다.
        private void Log(string message)
        {
            Debug.Log($"[Battle] {message}");
            LogAdded?.Invoke(message);
        }

        // 목록의 유닛이 모두 쓰러졌는지 확인한다.
        private static bool IsAllDead(List<BattleUnit> units)
        {
            return GetAliveUnits(units).Count == 0;
        }

        // 살아 있는 유닛만 모은 새 리스트를 돌려준다.
        private static List<BattleUnit> GetAliveUnits(List<BattleUnit> units)
        {
            var alive = new List<BattleUnit>();

            foreach (BattleUnit unit in units)
            {
                if (!unit.IsDead)
                {
                    alive.Add(unit);
                }
            }

            return alive;
        }

        // 전장에 있는(살아 있고 퇴장하지 않은) 유닛만 모은 새 리스트를 돌려준다. 대상 선택에 쓴다.
        // 승패 판정(GetAliveUnits)과 나눈 이유: 퇴장한 아군은 공격받지 않지만 쓰러진 것은 아니기 때문이다.
        private static List<BattleUnit> GetOnFieldUnits(List<BattleUnit> units)
        {
            var onField = new List<BattleUnit>();

            foreach (BattleUnit unit in units)
            {
                if (unit.IsOnField)
                {
                    onField.Add(unit);
                }
            }

            return onField;
        }
    }
}
