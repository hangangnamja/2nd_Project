using System;
using System.Collections.Generic;

namespace Game.Battle
{
    // 전투 중 일어난 사건을 알리는 통로 (옵저버 패턴).
    // 효과 블록이 사건을 알리면(Raise), 구독한 쪽(패시브 처리기 등)이 반응한다.
    // 사건을 일으키는 쪽은 누가 반응하는지 몰라도 되므로, 새 패시브를 추가해도 효과 블록 코드는 바뀌지 않는다.
    public class BattleEvents
    {
        // 유닛이 자기 행동으로 스킬을 사용했을 때 (추가 턴 포함, 추가 공격 제외). Action<사용자>
        public event Action<BattleUnit> SkillUsed;

        // 공격 판정 1회가 대상 하나에게 들어갔을 때. Action<공격자, 맞은 대상>
        public event Action<BattleUnit, BattleUnit> AttackHit;

        // 공격 행동 하나(스킬 또는 추가 공격)가 끝났을 때. 광역 공격도 1번만 알린다.
        // Action<공격자, 맞은 대상 목록, 메인 대상(확산만, 없으면 null), 발생 원인>
        public event Action<BattleUnit, IReadOnlyList<BattleUnit>, BattleUnit, ActionSource> AttackPerformed;

        // 유닛이 스킬을 사용했음을 알린다. 행동마다 스택이 쌓이는 패시브가 여기에 반응한다.
        public void RaiseSkillUsed(BattleUnit caster)
        {
            SkillUsed?.Invoke(caster);
        }

        // 공격 판정 1회가 대상에게 들어갔음을 알린다.
        // ?.Invoke: 구독자가 없으면(null) 아무것도 하지 않는다.
        public void RaiseAttackHit(BattleUnit attacker, BattleUnit target)
        {
            AttackHit?.Invoke(attacker, target);
        }

        // 공격 행동 하나가 끝났음을 알린다. 아군 공격에 반응하는 패시브가 여기서 추가 공격을 예약한다.
        public void RaiseAttackPerformed(
            BattleUnit attacker,
            IReadOnlyList<BattleUnit> hitTargets,
            BattleUnit primaryTarget,
            ActionSource source)
        {
            AttackPerformed?.Invoke(attacker, hitTargets, primaryTarget, source);
        }
    }
}
