using System.Collections;
using Game.Data;
using UnityEngine;

namespace Game.Battle
{
    // 전투 흐름을 진행하는 관리자 (1단계 임시 버전).
    // 아군 1명과 적 1명이 번갈아 평타를 치고, 한쪽이 쓰러지면 승패를 콘솔에 출력한다.
    // 속도 기반 행동 순서, 스킬, 상태효과는 이후 단계에서 이 흐름에 끼워 넣는다.
    public class BattleManager : MonoBehaviour
    {
        [Header("테스트 편성")]
        [SerializeField] private CharacterData allyData;
        [SerializeField] private CharacterData enemyData;

        [Header("진행 설정")]
        [Tooltip("행동 사이의 대기 시간(초). 로그를 눈으로 따라가기 위한 값")]
        [SerializeField, Min(0f)] private float actionInterval = 0.5f;

        [Tooltip("평타 배율 (100% = 1.0)")]
        [SerializeField, Min(0f)] private float basicAttackMultiplier = 1f;

        private BattleUnit ally;
        private BattleUnit enemy;
        private int actionCount;

        // 씬이 시작되면 설정을 확인하고, 유닛을 만든 뒤 전투를 시작한다.
        private void Start()
        {
            if (!HasValidSetup())
            {
                return;
            }

            CreateUnits();

            // StartCoroutine: RunBattle을 여러 프레임에 걸쳐 실행한다.
            // 코루틴 안의 yield return 지점에서 잠시 멈췄다가 다음 프레임(또는 지정 시간 뒤)에 이어서 실행된다.
            StartCoroutine(RunBattle());
        }

        // Inspector에 캐릭터 데이터가 연결되어 있는지 확인한다. 빠져 있으면 오류를 띄우고 전투를 시작하지 않는다.
        private bool HasValidSetup()
        {
            if (allyData != null && enemyData != null)
            {
                return true;
            }

            Debug.LogError("[Battle] 아군 또는 적 CharacterData가 연결되지 않았습니다.", this);
            return false;
        }

        // 원본 데이터(SO)로 전투용 유닛을 만든다. 이후 전투 중 값은 유닛 쪽에서만 바뀐다.
        private void CreateUnits()
        {
            ally = new BattleUnit(allyData, isAlly: true);
            enemy = new BattleUnit(enemyData, isAlly: false);
            actionCount = 0;

            Debug.Log($"[Battle] 전투 시작: {DescribeHp(ally)} vs {DescribeHp(enemy)}");
        }

        // 둘 중 하나가 쓰러질 때까지 번갈아 평타를 치는 전투 루프.
        // IEnumerator를 돌려주는 함수는 코루틴으로 실행되어, yield return 위치에서 멈췄다 이어갈 수 있다.
        private IEnumerator RunBattle()
        {
            // 1단계에서는 아군이 항상 먼저 행동한다 (속도 규칙은 다음 단계에서 적용).
            BattleUnit attacker = ally;
            BattleUnit target = enemy;

            while (!ally.IsDead && !enemy.IsDead)
            {
                PerformBasicAttack(attacker, target);

                // 튜플 교환: 오른쪽 (target, attacker)를 먼저 만든 뒤 왼쪽 두 변수에 동시에 넣는다.
                // 임시 변수 없이 공격자와 대상을 서로 바꿔, 다음 행동은 반대편이 하도록 만든다.
                (attacker, target) = (target, attacker);

                // actionInterval초 동안 멈췄다가 다음 반복을 실행한다 (로그를 천천히 보기 위함).
                yield return new WaitForSeconds(actionInterval);
            }

            LogResult();
        }

        // 공격자가 대상에게 평타 1회를 가하고, 결과를 로그로 남긴다.
        private void PerformBasicAttack(BattleUnit attacker, BattleUnit target)
        {
            actionCount++;

            int damage = DamageCalculator.CalculateDamage(attacker, target, basicAttackMultiplier);
            int dealt = target.TakeDamage(damage);

            Debug.Log($"[행동 {actionCount}] {attacker.Name} → {target.Name} 평타 {dealt} 피해 ({DescribeHp(target)})");
        }

        // 전투가 끝났을 때 승패를 출력한다. 적이 쓰러졌으면 승리, 아군이 쓰러졌으면 패배.
        private void LogResult()
        {
            string result = enemy.IsDead ? "승리" : "패배";
            Debug.Log($"[Battle] 전투 종료: {result} (총 {actionCount}회 행동)");
        }

        // "이름 HP 현재/최대" 형식의 문자열을 만든다 (로그 표시용).
        private static string DescribeHp(BattleUnit unit)
        {
            return $"{unit.Name} HP {unit.CurrentHp}/{unit.MaxHp}";
        }
    }
}
