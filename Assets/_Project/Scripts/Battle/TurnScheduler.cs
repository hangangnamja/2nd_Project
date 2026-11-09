using System;
using System.Collections.Generic;

namespace Game.Battle
{
    // 속도 기반 행동 순서를 계산한다 (붕괴 스타레일식 행동수치).
    // 각 유닛은 길이 10000짜리 트랙을 자기 속도로 달리고, 도착점에 가장 먼저 닿는 유닛이 행동한다.
    // 전투 흐름(누가 무엇을 하는지)은 BattleManager가 맡고, 이 클래스는 "다음이 누구인지"만 계산한다.
    public class TurnScheduler
    {
        public const double TrackLength = 10000d;

        // 사이클(절대 턴) 길이: 첫 사이클은 행동수치 150, 이후 100마다 1사이클 (기획서 3-3).
        private const double FirstCycleLength = 150d;
        private const double CycleLength = 100d;

        // 행동수치를 비교할 때 허용하는 오차. 부동소수점 계산으로 생기는 아주 작은 차이를
        // "동시 도착"으로 보기 위해 사용한다 (예: 100.0000000001과 100은 같은 시간으로 취급).
        private const double Epsilon = 1e-6;

        private readonly List<BattleUnit> units;

        // 전투 시작부터 흐른 총 시간(행동수치 누적). 사이클 계산에만 사용한다.
        public double ElapsedTime { get; private set; }

        public int CurrentCycle => CalculateCycle(ElapsedTime);

        // 순서를 계산할 유닛 목록을 받는다. 바깥 리스트가 바뀌어도 영향받지 않도록 복사해 둔다.
        public TurnScheduler(IEnumerable<BattleUnit> battleUnits)
        {
            units = new List<BattleUnit>(battleUnits);
        }

        // 다음에 행동할 유닛을 찾고, 그 유닛이 도착할 때까지 살아 있는 모든 유닛을 전진시킨다.
        // 행동할 유닛이 없으면(전원 사망) null을 돌려준다.
        public BattleUnit AdvanceToNextActor()
        {
            BattleUnit nextActor = FindNextActor();
            if (nextActor == null)
            {
                return null;
            }

            // 다음 행동자의 행동수치 = 지금부터 그 유닛이 도착할 때까지 걸리는 시간.
            // 이 시간만큼 모두를 전진시키면 다음 행동자는 남은 거리 0(도착)이 된다.
            double elapsed = nextActor.ActionValue;
            AdvanceAliveUnits(elapsed);
            ElapsedTime += elapsed;

            return nextActor;
        }

        // 행동을 마친 유닛을 출발점으로 되돌린다. 행동이 모두 끝난 뒤 BattleManager가 호출한다.
        public void EndTurn(BattleUnit actor)
        {
            actor.ResetDistance();
        }

        // 전장에 있는 유닛 중 가장 먼저 도착하는 유닛을 찾는다. 쓰러졌거나 퇴장한 유닛은 행동하지 않는다.
        private BattleUnit FindNextActor()
        {
            BattleUnit best = null;

            foreach (BattleUnit unit in units)
            {
                if (!unit.IsOnField)
                {
                    continue;
                }

                if (best == null || ActsBefore(unit, best))
                {
                    best = unit;
                }
            }

            return best;
        }

        // a가 b보다 먼저 행동하는지 판단한다. 행동수치가 작은 쪽이 먼저이고, 같으면 동시 도착 규칙을 따른다.
        private static bool ActsBefore(BattleUnit a, BattleUnit b)
        {
            double difference = a.ActionValue - b.ActionValue;

            // Math.Abs: 절댓값. 두 행동수치의 차이가 오차보다 크면 실제로 다른 시간이므로 작은 쪽이 먼저다.
            if (Math.Abs(difference) > Epsilon)
            {
                return difference < 0;
            }

            return CompareTiePriority(a, b) < 0;
        }

        // 동시 도착 시 우선순위 (기획서 3-3): 아군 → 직군(지원 → 공격 → 방어 → 회복) → 편성 순서.
        // 적군끼리는 직군을 보지 않고 편성 순서만 비교한다.
        // 반환값이 음수면 a가 먼저, 양수면 b가 먼저다 (CompareTo와 같은 규칙).
        private static int CompareTiePriority(BattleUnit a, BattleUnit b)
        {
            if (a.IsAlly != b.IsAlly)
            {
                return a.IsAlly ? -1 : 1;
            }

            // 직군 enum은 우선순위 순서대로 0, 1, 2, 3 값을 가지므로 숫자 비교가 곧 우선순위 비교다.
            if (a.IsAlly && a.Role != b.Role)
            {
                return ((int)a.Role).CompareTo((int)b.Role);
            }

            return a.FormationIndex.CompareTo(b.FormationIndex);
        }

        // 흐른 시간만큼 살아 있는 유닛을 모두 전진시킨다.
        private void AdvanceAliveUnits(double elapsed)
        {
            foreach (BattleUnit unit in units)
            {
                // 퇴장한 아군은 전진하지 않는다 → 복귀하면 퇴장 전 게이지 그대로 이어서 달린다 (기획서 4-3).
                if (unit.IsOnField)
                {
                    unit.Advance(elapsed);
                }
            }
        }

        // 누적 시간으로 현재 사이클 번호를 구한다. 0~150은 1사이클, 이후 100마다 1씩 증가한다.
        // 예: 150 → 1, 150.1 → 2, 250 → 2, 250.1 → 3
        private static int CalculateCycle(double elapsedTime)
        {
            if (elapsedTime <= FirstCycleLength + Epsilon)
            {
                return 1;
            }

            // Math.Ceiling: 소수점을 올림한다. 첫 사이클 이후 지난 시간을 100으로 나눠 올림하면 추가 사이클 수가 된다.
            double overFirstCycle = elapsedTime - FirstCycleLength - Epsilon;
            return 1 + (int)Math.Ceiling(overFirstCycle / CycleLength);
        }
    }
}
