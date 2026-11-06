using Game.Data;

namespace Game.Battle.Status
{
    // 실제로 유닛에게 걸린 상태효과 하나. 같은 화상이라도 인스턴스마다 남은 턴과 부여자가 다르다.
    public class StatusInstance
    {
        public StatusEffectData Data { get; }
        public BattleUnit Source { get; }
        public int RemainingTurns { get; private set; }

        // 부여 당시 시전자의 공격력 (스냅샷). 이후 시전자의 공격력이 바뀌어도 이 효과의 피해는 그대로다.
        public int SnapshotAttack { get; }

        // 몇 번째로 걸린 효과인지. 칸이 가득 차 밀어낼 때 "남은 턴이 같으면 먼저 걸린 것부터" 비교에 쓴다.
        public long AppliedOrder { get; private set; }

        // 상태효과 정의, 부여자, 부여 순서를 받아 인스턴스를 만든다. 남은 턴은 정의의 지속 턴으로 시작한다.
        public StatusInstance(StatusEffectData data, BattleUnit source, long appliedOrder)
        {
            Data = data;
            Source = source;
            SnapshotAttack = source.Data.Attack;
            RemainingTurns = data.Duration;
            AppliedOrder = appliedOrder;
        }

        public bool IsExpired => RemainingTurns <= 0;

        // 효과를 가진 유닛의 턴이 끝날 때 남은 턴을 1 줄인다.
        public void DecreaseTurn()
        {
            RemainingTurns--;
        }

        // 갱신형 효과를 다시 걸었을 때 남은 턴을 처음 값으로 되돌린다.
        // 부여 순서도 새로 매겨, 밀어내기 판정에서 "방금 걸린 효과"로 취급되게 한다.
        public void Refresh(long appliedOrder)
        {
            RemainingTurns = Data.Duration;
            AppliedOrder = appliedOrder;
        }
    }
}
