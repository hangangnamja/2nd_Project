using Game.Data;

namespace Game.Battle.Status
{
    // 실제로 유닛에게 걸린 상태효과 하나. 같은 화상이라도 인스턴스마다 남은 턴과 부여자가 다르다.
    public class StatusInstance
    {
        public StatusEffectData Data { get; }
        public BattleUnit Source { get; }
        public int RemainingTurns { get; private set; }

        // 부여 당시 시전자의 최종 공격력 (스냅샷, 버프 포함). 이후 시전자의 공격력이 바뀌어도 이 효과의 피해는 그대로다.
        // 그래서 "공격력 버프를 받고 화상을 거는" 순서가 의미를 가진다 (기획서 3-7).
        public int SnapshotAttack { get; }

        // 부여 당시 시전자의 최대 HP (스냅샷). 지속 회복(소생)의 회복량 기준이다.
        // 강탈로 다른 유닛에게 넘어가도 처음 건 시전자의 값을 그대로 쓴다.
        public int SnapshotMaxHp { get; }

        // 몇 번째로 걸린 효과인지. 칸이 가득 차 밀어낼 때 "남은 턴이 같으면 먼저 걸린 것부터" 비교에 쓴다.
        public long AppliedOrder { get; private set; }

        // 상태효과 정의, 부여자, 부여 순서를 받아 인스턴스를 만든다. 남은 턴은 정의의 지속 턴으로 시작한다.
        public StatusInstance(StatusEffectData data, BattleUnit source, long appliedOrder)
        {
            Data = data;
            Source = source;
            SnapshotAttack = source.Attack;
            SnapshotMaxHp = source.MaxHp;
            RemainingTurns = data.Duration;
            AppliedOrder = appliedOrder;
        }

        public bool IsExpired => RemainingTurns <= 0;

        // 남은 보호막 양. 보호막 효과 블록이 건 버프에만 0보다 크다. 피해를 이만큼 먼저 흡수하고, 0이 되면 버프가 깨진다.
        public int ShieldAmount { get; private set; }
        public bool HasShield => ShieldAmount > 0;

        // 보호막 양을 정한다. 같은 보호막을 다시 걸면(갱신) 남은 양과 새 양 중 큰 쪽을 남긴다 → 약한 보호막으로 덮어써 손해 보지 않는다.
        public void SetShieldAtLeast(int amount)
        {
            ShieldAmount = System.Math.Max(ShieldAmount, amount);
        }

        // 피해를 보호막으로 흡수하고, 흡수하지 못한 나머지 피해를 돌려준다.
        // Math.Min: 피해와 남은 보호막 중 작은 값만큼 흡수한다. 예: 보호막 200, 피해 350 → 200 흡수, 나머지 150
        public int Absorb(int damage)
        {
            int absorbed = System.Math.Min(ShieldAmount, damage);
            ShieldAmount -= absorbed;
            return damage - absorbed;
        }

        // 이 효과가 걸려 있는 동안 보유자가 받은 피해 합계. 정의의 "피해 기록"이 켜진 효과만 쌓인다 (반격 태세).
        public int RecordedDamage { get; private set; }

        // 받은 피해를 누적한다. StatusApplier.DealDamage가 피해를 적용할 때마다 호출한다.
        public void RecordDamage(int amount)
        {
            RecordedDamage += amount;
        }

        // 효과를 가진 유닛의 턴이 끝날 때 남은 턴을 1 줄인다.
        public void DecreaseTurn()
        {
            RemainingTurns--;
        }

        // 강탈로 같은 버프를 또 받았을 때: 남은 턴이 더 긴 쪽으로 맞춘다 (기획서 4-3).
        // Math.Max: 둘 중 큰 값. 예: 내 버프 1턴, 뺏은 버프 2턴 → 2턴
        public void KeepLongerRemaining(int otherRemainingTurns, long appliedOrder)
        {
            RemainingTurns = System.Math.Max(RemainingTurns, otherRemainingTurns);
            AppliedOrder = appliedOrder;
        }

        // 다른 유닛에게서 넘어왔을 때 "새 칸에 들어온 효과"로 순번만 새로 매긴다. 남은 턴은 그대로 계승한다.
        public void ResetAppliedOrder(long appliedOrder)
        {
            AppliedOrder = appliedOrder;
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
