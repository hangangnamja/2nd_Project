using System;

namespace Game.Battle
{
    // 전투 중 일어난 사건을 알리는 통로 (옵저버 패턴).
    // 효과 블록이 사건을 알리면(Raise), 구독한 쪽(패시브 처리기 등)이 반응한다.
    // 사건을 일으키는 쪽은 누가 반응하는지 몰라도 되므로, 새 패시브를 추가해도 효과 블록 코드는 바뀌지 않는다.
    public class BattleEvents
    {
        // Action<BattleUnit, BattleUnit>: (공격자, 맞은 대상) 두 값을 받는 함수를 담는 델리게이트.
        public event Action<BattleUnit, BattleUnit> AttackHit;

        // 공격 판정 1회가 대상에게 들어갔음을 알린다.
        // ?.Invoke: 구독자가 없으면(null) 아무것도 하지 않는다.
        public void RaiseAttackHit(BattleUnit attacker, BattleUnit target)
        {
            AttackHit?.Invoke(attacker, target);
        }
    }
}
