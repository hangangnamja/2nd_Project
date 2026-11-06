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

        [Header("테스트 편성 (리스트 순서 = 편성 순서)")]
        [SerializeField] private List<CharacterData> allyParty = new List<CharacterData>();
        [SerializeField] private List<CharacterData> enemyParty = new List<CharacterData>();

        [Tooltip("편성 전원에게 적용할 테스트 레벨 (성장 시스템 전까지 임시)")]
        [SerializeField, Range(1, 60)] private int testLevel = 1;

        [Header("전투 규칙")]
        [SerializeField] private BattleRulesData rules;

        [Tooltip("난수 시드. 0이면 매번 다른 결과, 0이 아니면 같은 시드에서 치명타·무작위 대상이 항상 똑같이 나온다")]
        [SerializeField] private int randomSeed;

        [Header("진행 설정")]
        [Tooltip("적 AI가 행동하기 전 대기 시간(초). 적의 행동을 눈으로 따라가기 위한 값")]
        [SerializeField, Min(0f)] private float enemyActionDelay = 0.6f;

        private readonly List<BattleUnit> allies = new List<BattleUnit>();
        private readonly List<BattleUnit> enemies = new List<BattleUnit>();
        private TurnScheduler scheduler;
        private int actionCount;

        // 아군 + 적 전원 (행동 순서, 패시브 반응 대상 탐색용).
        private readonly List<BattleUnit> allUnits = new List<BattleUnit>();

        // 전투 공용 도구(로그, 사건, 대기열, 난수, 규칙)와 사건에 반응하는 패시브 처리기. 전투 시작 시 만든다.
        private BattleServices services;
        private PassiveRunner passiveRunner;
        private ActionQueue actionQueue => services.Actions;

        // 이번 차례에 실행하기로 확정된 행동. 확정 전에는 hasPendingAction이 false다.
        // pendingTarget은 단일 대상 스킬일 때만 쓰고, 광역·자신 스킬은 대상 규칙으로 다시 계산한다.
        private bool hasPendingAction;
        private int pendingSkillSlot;
        private BattleUnit pendingTarget;

        // event Action: 구독자(화면 스크립트)들에게 "무언가 바뀌었다"를 알리는 통로.
        // 구독자는 += 로 함수를 등록하고, 이 클래스가 Invoke하면 등록된 함수가 모두 실행된다.
        public event Action BattleStarted;
        public event Action StateChanged;
        public event Action<string> LogAdded;

        public IReadOnlyList<BattleUnit> Allies => allies;
        public IReadOnlyList<BattleUnit> Enemies => enemies;
        public BattleUnit CurrentActor { get; private set; }
        public bool IsWaitingForPlayer { get; private set; }
        public bool IsBattleOver { get; private set; }

        // 지금 행동이 추가 턴인지 (HUD 표시용).
        public bool IsExtraTurn { get; private set; }

        // 플레이어가 지금 고른 스킬 슬롯 (0 = 1스). 고른 스킬이 없으면 NoSkillSelected.
        public int SelectedSkillSlot { get; private set; } = NoSkillSelected;

        // scheduler?.CurrentCycle: 스케줄러가 아직 없으면(null) 읽지 않고 null을 돌려준다.
        // ?? 1: 앞의 결과가 null이면 대신 1을 쓴다. 전투 시작 전 HUD가 읽어도 오류 없이 1사이클로 표시된다.
        public int CurrentCycle => scheduler?.CurrentCycle ?? 1;

        // 씬이 시작되면 설정을 확인하고, 유닛과 스케줄러를 만든 뒤 전투를 시작한다.
        private void Start()
        {
            if (!HasValidSetup())
            {
                return;
            }

            CreateServices();
            CreateUnits();
            CreateScheduler();
            passiveRunner = new PassiveRunner(services, allUnits);

            // ?.Invoke: 구독자가 하나도 없으면(null) 호출하지 않고 넘어간다. 화면이 없는 씬에서도 안전하게 동작한다.
            BattleStarted?.Invoke();

            // StartCoroutine: RunBattle을 여러 프레임에 걸쳐 실행한다.
            // 코루틴 안의 yield return 지점에서 잠시 멈췄다가 다음 프레임(또는 조건 충족 시)에 이어서 실행된다.
            StartCoroutine(RunBattle());
        }

        // 오브젝트가 파괴될 때(씬 종료, 플레이 중지) 패시브 처리기의 사건 구독을 해제한다.
        private void OnDestroy()
        {
            passiveRunner?.Dispose();
        }

        // ---------- 화면(HUD, 클릭)에서 호출하는 함수 ----------

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
            if (skill == null || unit == null || unit.IsDead)
            {
                return false;
            }

            bool isOpponent = unit.IsAlly != CurrentActor.IsAlly;

            switch (skill.TargetType)
            {
                case SkillTargetType.EnemySingle:
                case SkillTargetType.EnemyAll:
                case SkillTargetType.EnemyBlast:
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

            return CurrentActor.Data.GetSkill(SelectedSkillSlot);
        }

        // 대상을 직접 하나 골라야 하는 스킬인지 (적 단일·아군 단일·확산의 메인 대상).
        public static bool NeedsSingleTarget(SkillData skill)
        {
            return skill != null
                && (skill.TargetType == SkillTargetType.EnemySingle
                    || skill.TargetType == SkillTargetType.AllySingle
                    || skill.TargetType == SkillTargetType.EnemyBlast);
        }

        // ---------- 전투 흐름 ----------

        // 한쪽 편이 전멸할 때까지 턴을 반복하는 전투 루프. 턴 하나의 처리 순서 (Architecture.md 3장):
        // 행동자 결정(추가 턴이면 같은 유닛) → 스킬·대상 선택 → 스킬 실행 → 대기 중인 추가 공격 실행 → 턴 종료 → 추가 턴 확인
        private IEnumerator RunBattle()
        {
            Log($"전투 시작: 아군 {allies.Count}명 vs 적 {enemies.Count}명");
            BattleUnit extraTurnUnit = null;

            while (!IsBattleDecided())
            {
                BeginTurn(extraTurnUnit);

                // yield return 다른 코루틴: 그 코루틴이 끝날 때까지 여기서 기다린다.
                if (CurrentActor.IsAlly)
                {
                    yield return WaitForPlayerAction();
                }
                else
                {
                    yield return new WaitForSeconds(enemyActionDelay);
                    DecideEnemyAction(CurrentActor);
                }

                UseSkill(CurrentActor, pendingSkillSlot, pendingTarget);
                ProcessFollowUpAttacks();
                EndTurn(CurrentActor);

                // 이번 턴에 추가 턴을 얻었으면, 다음 반복에서 스케줄러를 거치지 않고 같은 유닛이 바로 행동한다.
                extraTurnUnit = actionQueue.ConsumeExtraTurn();
            }

            FinishBattle();
        }

        // 턴을 시작한다. 추가 턴이면 같은 유닛이 다시 행동하고, 아니면 스케줄러가 다음 행동자를 정한다.
        // 새 턴마다 패시브의 "턴당 발동 횟수"를 초기화한다 (추가 턴도 새 턴이다).
        private void BeginTurn(BattleUnit extraTurnUnit)
        {
            bool hasExtraTurn = extraTurnUnit != null && !extraTurnUnit.IsDead;

            IsExtraTurn = hasExtraTurn;
            CurrentActor = hasExtraTurn ? extraTurnUnit : scheduler.AdvanceToNextActor();
            hasPendingAction = false;
            passiveRunner.ResetTurnCounters();

            if (hasExtraTurn)
            {
                Log($"[추가 턴] {CurrentActor.Name}");
            }

            StateChanged?.Invoke();
        }

        // 대기열에 쌓인 추가 공격을 들어온 순서대로 모두 실행한다.
        // 추가 공격이 또 다른 추가 공격을 예약할 수 있지만, 패시브 횟수 제한 덕분에 반드시 끝난다.
        private void ProcessFollowUpAttacks()
        {
            while (!IsBattleDecided() && actionQueue.TryDequeueFollowUp(out FollowUpAttack followUp))
            {
                if (followUp.Owner.IsDead)
                {
                    continue;
                }

                BattleUnit target = PickFollowUpTarget(followUp);
                Log($"[추가 공격] {followUp.Owner.Name}: {followUp.Passive.PassiveName} → {target.Name}");

                SkillExecutor.ExecuteEffects(
                    followUp.Owner,
                    followUp.Passive.Effects,
                    new List<BattleUnit> { target },
                    primaryTarget: null,
                    services,
                    ActionSource.FollowUpAttack);
            }
        }

        // 추가 공격 대상: 원래 대상이 살아 있으면 그대로, 쓰러졌으면 살아 있는 상대 중에서 다시 고른다.
        // 다시 고를 때는 현재 HP가 가장 높은 적 → 같으면 편성 순서 (기획서 4-3 격발 딜러 패시브 2 규칙).
        // 보스 우선 규칙은 보스 개념이 들어올 때 추가한다.
        private BattleUnit PickFollowUpTarget(FollowUpAttack followUp)
        {
            if (followUp.PreferredTarget != null && !followUp.PreferredTarget.IsDead)
            {
                return followUp.PreferredTarget;
            }

            BattleUnit best = null;
            foreach (BattleUnit unit in followUp.Owner.IsAlly ? enemies : allies)
            {
                if (unit.IsDead)
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

        // 어느 한쪽이 전멸해 승패가 정해졌는지.
        private bool IsBattleDecided()
        {
            return IsAllDead(allies) || IsAllDead(enemies);
        }

        // 아군 차례에 플레이어가 스킬과 대상을 확정할 때까지 기다린다. 시작할 때 1스(평타)를 미리 선택해 둔다.
        private IEnumerator WaitForPlayerAction()
        {
            SelectedSkillSlot = FindFirstUsableSkill(CurrentActor);
            IsWaitingForPlayer = true;
            StateChanged?.Invoke();

            // WaitUntil: 괄호 안 조건이 true가 될 때까지 매 프레임 확인하며 기다린다.
            // () => hasPendingAction 은 "행동이 확정되었는가"를 돌려주는 람다식이다.
            yield return new WaitUntil(() => hasPendingAction);

            IsWaitingForPlayer = false;
            SelectedSkillSlot = NoSkillSelected;
        }

        // 플레이어 행동을 확정한다. 대기 중인 코루틴이 다음 프레임에 hasPendingAction을 보고 실행으로 넘어간다.
        private void ConfirmAction(int slot, BattleUnit target)
        {
            pendingSkillSlot = slot;
            pendingTarget = target;
            hasPendingAction = true;
        }

        // 적 AI 행동 결정 (임시): 1스(평타)를 사용하고, 대상은 대상 규칙에 따라 자동으로 정한다 (기획서 3-10).
        private void DecideEnemyAction(BattleUnit enemy)
        {
            ConfirmAction(slot: 0, target: null);
        }

        // 스킬 대상을 정하고 실행한다.
        private void UseSkill(BattleUnit caster, int slot, BattleUnit chosenTarget)
        {
            actionCount++;
            SkillData skill = caster.Data.GetSkill(slot);
            List<BattleUnit> targets = ResolveTargets(caster, skill.TargetType, chosenTarget);

            // 확산 스킬은 대상 목록의 첫 번째가 메인 대상이다. 나머지 스킬은 메인 대상 구분이 없다.
            BattleUnit primaryTarget = skill.TargetType == SkillTargetType.EnemyBlast ? targets[0] : null;

            Log($"[사이클 {CurrentCycle} | 행동 {actionCount}]");
            SkillExecutor.ExecuteSkill(caster, slot, targets, primaryTarget, services);
        }

        // 행동을 마친 유닛의 턴 종료 처리: 상태효과(지속피해 → 남은 턴 감소) → 쿨타임 감소 → 출발점 복귀.
        private void EndTurn(BattleUnit actor)
        {
            StatusTurnProcessor.ProcessTurnEnd(actor, services);
            actor.TickCooldowns();
            scheduler.EndTurn(actor);
            StateChanged?.Invoke();
        }

        // 전투가 끝났을 때 승패를 기록하고 화면에 알린다.
        private void FinishBattle()
        {
            IsBattleOver = true;
            CurrentActor = null;

            string result = IsAllDead(enemies) ? "승리" : "패배";
            Log($"전투 종료: {result} (총 {actionCount}회 행동, {CurrentCycle}사이클)");
            StateChanged?.Invoke();
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
                    return GetAliveUnits(opponents);
                case SkillTargetType.AllyAll:
                    return GetAliveUnits(friends);
                case SkillTargetType.Self:
                    return new List<BattleUnit> { caster };
                case SkillTargetType.AllySingle:
                    // ?? caster: 고른 대상이 없으면(적 AI) 자기 자신에게 쓴다.
                    return new List<BattleUnit> { chosenTarget ?? caster };
                case SkillTargetType.EnemyBlast:
                    return GetBlastTargets(opponents, chosenTarget ?? PickRandomAlive(opponents));
                default:
                    // ?? : 고른 대상이 없으면(적 AI) 상대편 중 무작위 1명을 고른다.
                    return new List<BattleUnit> { chosenTarget ?? PickRandomAlive(opponents) };
            }
        }

        // 확산 대상: 메인 대상 + 살아 있는 적 중 편성 순서상 바로 앞·뒤 (기획서 4-3 독 서브딜러 2스).
        // 쓰러진 적은 건너뛰므로, 가운데 적이 쓰러졌으면 그 너머의 적이 "옆"이 된다.
        // 반환 목록의 첫 번째가 메인 대상이다.
        private static List<BattleUnit> GetBlastTargets(List<BattleUnit> opponents, BattleUnit main)
        {
            List<BattleUnit> alive = GetAliveUnits(opponents);
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
            List<BattleUnit> alive = GetAliveUnits(units);

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

            if (IsValidParty(allyParty) && IsValidParty(enemyParty))
            {
                return true;
            }

            Debug.LogError("[Battle] 아군 또는 적 편성이 비어 있거나 빈 슬롯이 있습니다.", this);
            return false;
        }

        // 전투 공용 도구를 만든다. 시드가 0이면 시간 기반 난수, 아니면 고정 시드 난수를 쓴다.
        private void CreateServices()
        {
            // new System.Random(): 시드 없이 만들면 매번 다른 순서의 난수가 나온다.
            // new System.Random(seed): 같은 시드면 항상 같은 순서의 난수가 나와 전투를 그대로 재현할 수 있다.
            System.Random random = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);
            services = new BattleServices(Log, new BattleEvents(), new ActionQueue(), random, rules);
        }

        // 편성 리스트가 비어 있지 않고, 연결이 빠진(null) 슬롯이 없는지 검사한다.
        private static bool IsValidParty(List<CharacterData> party)
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

        // 원본 데이터(SO)로 전투용 유닛을 만든다. 리스트 위치가 곧 편성 순서(FormationIndex)가 된다.
        private void CreateUnits()
        {
            allies.Clear();
            enemies.Clear();
            actionCount = 0;

            for (int i = 0; i < allyParty.Count; i++)
            {
                allies.Add(new BattleUnit(allyParty[i], isAlly: true, formationIndex: i, level: testLevel));
            }

            for (int i = 0; i < enemyParty.Count; i++)
            {
                enemies.Add(new BattleUnit(enemyParty[i], isAlly: false, formationIndex: i, level: testLevel));
            }

            // 각 유닛에 같은 편 목록을 연결해, 살아 있는 아군의 오라 패시브가 스탯 계산에 반영되게 한다.
            foreach (BattleUnit ally in allies)
            {
                ally.SetTeam(allies);
            }

            foreach (BattleUnit enemy in enemies)
            {
                enemy.SetTeam(enemies);
            }
        }

        // 아군과 적 전원을 하나의 트랙에 올려 행동 순서를 계산할 스케줄러를 만든다.
        private void CreateScheduler()
        {
            allUnits.Clear();
            allUnits.AddRange(allies);
            allUnits.AddRange(enemies);
            scheduler = new TurnScheduler(allUnits);
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
    }
}
