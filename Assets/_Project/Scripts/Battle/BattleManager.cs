using System;
using System.Collections;
using System.Collections.Generic;
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

        [Header("진행 설정")]
        [Tooltip("적 AI가 행동하기 전 대기 시간(초). 적의 행동을 눈으로 따라가기 위한 값")]
        [SerializeField, Min(0f)] private float enemyActionDelay = 0.6f;

        private readonly List<BattleUnit> allies = new List<BattleUnit>();
        private readonly List<BattleUnit> enemies = new List<BattleUnit>();
        private TurnScheduler scheduler;
        private int actionCount;

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

            CreateUnits();
            CreateScheduler();

            // ?.Invoke: 구독자가 하나도 없으면(null) 호출하지 않고 넘어간다. 화면이 없는 씬에서도 안전하게 동작한다.
            BattleStarted?.Invoke();

            // StartCoroutine: RunBattle을 여러 프레임에 걸쳐 실행한다.
            // 코루틴 안의 yield return 지점에서 잠시 멈췄다가 다음 프레임(또는 조건 충족 시)에 이어서 실행된다.
            StartCoroutine(RunBattle());
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

        // 대상을 직접 하나 골라야 하는 스킬인지 (적 단일·아군 단일).
        public static bool NeedsSingleTarget(SkillData skill)
        {
            return skill != null
                && (skill.TargetType == SkillTargetType.EnemySingle || skill.TargetType == SkillTargetType.AllySingle);
        }

        // ---------- 전투 흐름 ----------

        // 한쪽 편이 전멸할 때까지 "다음 행동자 → 스킬·대상 선택 → 실행 → 턴 종료"를 반복하는 전투 루프.
        private IEnumerator RunBattle()
        {
            Log($"전투 시작: 아군 {allies.Count}명 vs 적 {enemies.Count}명");

            while (!IsAllDead(allies) && !IsAllDead(enemies))
            {
                CurrentActor = scheduler.AdvanceToNextActor();
                hasPendingAction = false;
                StateChanged?.Invoke();

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
                EndTurn(CurrentActor);
            }

            FinishBattle();
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

            Log($"[사이클 {CurrentCycle} | 행동 {actionCount}]");
            SkillExecutor.Execute(caster, slot, targets, Log);
        }

        // 행동을 마친 유닛의 쿨타임을 줄이고, 트랙 출발점으로 되돌린다.
        private void EndTurn(BattleUnit actor)
        {
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
                default:
                    // ?? : 고른 대상이 없으면(적 AI) 상대편 중 무작위 1명을 고른다.
                    return new List<BattleUnit> { chosenTarget ?? PickRandomAlive(opponents) };
            }
        }

        // 살아 있는 유닛 중 무작위 1명을 고른다 (적 AI의 단일 대상).
        private static BattleUnit PickRandomAlive(List<BattleUnit> units)
        {
            List<BattleUnit> alive = GetAliveUnits(units);

            // Random.Range(0, n): 0 이상 n 미만의 정수를 무작위로 뽑는다 (정수 버전은 최댓값을 포함하지 않음).
            return alive[UnityEngine.Random.Range(0, alive.Count)];
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
            if (IsValidParty(allyParty) && IsValidParty(enemyParty))
            {
                return true;
            }

            Debug.LogError("[Battle] 아군 또는 적 편성이 비어 있거나 빈 슬롯이 있습니다.", this);
            return false;
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
                allies.Add(new BattleUnit(allyParty[i], isAlly: true, formationIndex: i));
            }

            for (int i = 0; i < enemyParty.Count; i++)
            {
                enemies.Add(new BattleUnit(enemyParty[i], isAlly: false, formationIndex: i));
            }
        }

        // 아군과 적 전원을 하나의 트랙에 올려 행동 순서를 계산할 스케줄러를 만든다.
        private void CreateScheduler()
        {
            var allUnits = new List<BattleUnit>(allies);
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
