using System.Collections;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Battle
{
    // 전투 흐름을 진행하는 관리자 (2단계: 속도 기반 행동 순서).
    // TurnScheduler가 정해준 순서대로 유닛이 평타를 치고, 한쪽 편이 전멸하면 승패를 콘솔에 출력한다.
    // 스킬, 상태효과, 행동 결정자(플레이어 입력·AI)는 이후 단계에서 이 흐름에 끼워 넣는다.
    public class BattleManager : MonoBehaviour
    {
        [Header("테스트 편성 (리스트 순서 = 편성 순서)")]
        [SerializeField] private List<CharacterData> allyParty = new List<CharacterData>();
        [SerializeField] private List<CharacterData> enemyParty = new List<CharacterData>();

        [Header("진행 설정")]
        [Tooltip("행동 사이의 대기 시간(초). 로그를 눈으로 따라가기 위한 값")]
        [SerializeField, Min(0f)] private float actionInterval = 0.5f;

        [Tooltip("평타 배율 (100% = 1.0)")]
        [SerializeField, Min(0f)] private float basicAttackMultiplier = 1f;

        private readonly List<BattleUnit> allies = new List<BattleUnit>();
        private readonly List<BattleUnit> enemies = new List<BattleUnit>();
        private TurnScheduler scheduler;
        private int actionCount;

        // 씬이 시작되면 설정을 확인하고, 유닛과 스케줄러를 만든 뒤 전투를 시작한다.
        private void Start()
        {
            if (!HasValidSetup())
            {
                return;
            }

            CreateUnits();
            CreateScheduler();

            // StartCoroutine: RunBattle을 여러 프레임에 걸쳐 실행한다.
            // 코루틴 안의 yield return 지점에서 잠시 멈췄다가 다음 프레임(또는 지정 시간 뒤)에 이어서 실행된다.
            StartCoroutine(RunBattle());
        }

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

            Debug.Log($"[Battle] 전투 시작: 아군 {allies.Count}명 vs 적 {enemies.Count}명");
        }

        // 아군과 적 전원을 하나의 트랙에 올려 행동 순서를 계산할 스케줄러를 만든다.
        private void CreateScheduler()
        {
            var allUnits = new List<BattleUnit>(allies);
            allUnits.AddRange(enemies);
            scheduler = new TurnScheduler(allUnits);
        }

        // 한쪽 편이 전멸할 때까지 "다음 행동자 결정 → 평타 → 턴 종료"를 반복하는 전투 루프.
        // IEnumerator를 돌려주는 함수는 코루틴으로 실행되어, yield return 위치에서 멈췄다 이어갈 수 있다.
        private IEnumerator RunBattle()
        {
            while (!IsAllDead(allies) && !IsAllDead(enemies))
            {
                BattleUnit actor = scheduler.AdvanceToNextActor();
                BattleUnit target = ChooseBasicAttackTarget(actor);

                PerformBasicAttack(actor, target);
                scheduler.EndTurn(actor);

                // actionInterval초 동안 멈췄다가 다음 반복을 실행한다 (로그를 천천히 보기 위함).
                yield return new WaitForSeconds(actionInterval);
            }

            LogResult();
        }

        // 평타 대상을 고른다 (임시 규칙).
        // 아군: 살아 있는 적 중 편성 순서가 가장 앞인 적 (플레이어 입력은 이후 단계에서 추가)
        // 적: 살아 있는 아군 중 무작위 1명 (기획서 3-10 일반 적 AI)
        private BattleUnit ChooseBasicAttackTarget(BattleUnit actor)
        {
            if (actor.IsAlly)
            {
                return FindFirstAlive(enemies);
            }

            List<BattleUnit> aliveAllies = GetAliveUnits(allies);

            // Random.Range(0, n): 0 이상 n 미만의 정수를 무작위로 뽑는다 (정수 버전은 최댓값을 포함하지 않음).
            return aliveAllies[Random.Range(0, aliveAllies.Count)];
        }

        // 공격자가 대상에게 평타 1회를 가하고, 사이클·누적 시간과 함께 결과를 로그로 남긴다.
        private void PerformBasicAttack(BattleUnit attacker, BattleUnit target)
        {
            actionCount++;

            int damage = DamageCalculator.CalculateDamage(attacker, target, basicAttackMultiplier);
            int dealt = target.TakeDamage(damage);

            // F1: 소수점 첫째 자리까지만 표시하는 숫자 서식.
            Debug.Log(
                $"[사이클 {scheduler.CurrentCycle} | 행동 {actionCount} | 시간 {scheduler.ElapsedTime:F1}] " +
                $"{attacker.Name}(속도 {attacker.Speed}) → {target.Name} 평타 {dealt} 피해 ({DescribeHp(target)})");
        }

        // 전투가 끝났을 때 승패를 출력한다. 적이 전멸했으면 승리, 아군이 전멸했으면 패배.
        private void LogResult()
        {
            string result = IsAllDead(enemies) ? "승리" : "패배";
            Debug.Log($"[Battle] 전투 종료: {result} (총 {actionCount}회 행동, {scheduler.CurrentCycle}사이클)");
        }

        // 목록의 유닛이 모두 쓰러졌는지 확인한다.
        private static bool IsAllDead(List<BattleUnit> units)
        {
            return FindFirstAlive(units) == null;
        }

        // 편성 순서대로 살펴보며 처음 만나는 살아 있는 유닛을 돌려준다. 없으면 null.
        private static BattleUnit FindFirstAlive(List<BattleUnit> units)
        {
            foreach (BattleUnit unit in units)
            {
                if (!unit.IsDead)
                {
                    return unit;
                }
            }

            return null;
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

        // "이름 HP 현재/최대" 형식의 문자열을 만든다 (로그 표시용).
        private static string DescribeHp(BattleUnit unit)
        {
            return $"{unit.Name} HP {unit.CurrentHp}/{unit.MaxHp}";
        }
    }
}
