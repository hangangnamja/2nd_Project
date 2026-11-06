using System.Collections.Generic;
using Game.Data;

namespace Game.Battle
{
    // 현재 행동이 끝난 뒤 실행할 후속 행동(추가 공격)과, 이번 턴이 끝난 뒤 받을 추가 턴을 모아 둔다.
    // 이 클래스는 "다음에 무엇을 실행할지"만 관리하고, 실제 실행은 BattleManager가 한다 (Architecture.md 3장).
    public class ActionQueue
    {
        // Queue: 먼저 넣은 것이 먼저 나오는 자료구조. 추가 공격은 발동한 순서대로 실행된다.
        private readonly Queue<FollowUpAttack> followUps = new Queue<FollowUpAttack>();

        // 이번 턴이 끝난 뒤 추가 턴을 받을 유닛. 한 턴에 여러 번 얻어도 1회만 받으므로 하나만 기억한다.
        private BattleUnit extraTurnUnit;

        // 추가 공격 하나를 대기열 끝에 넣는다.
        public void EnqueueFollowUp(FollowUpAttack followUp)
        {
            followUps.Enqueue(followUp);
        }

        // 대기 중인 추가 공격을 하나 꺼낸다. 없으면 false.
        // out: 함수가 값을 하나 더 돌려주기 위한 매개변수. 꺼낸 추가 공격이 followUp에 담긴다.
        public bool TryDequeueFollowUp(out FollowUpAttack followUp)
        {
            if (followUps.Count > 0)
            {
                followUp = followUps.Dequeue();
                return true;
            }

            followUp = null;
            return false;
        }

        // unit에게 이번 턴이 끝난 뒤 추가 턴을 준다.
        // 같은 턴에 이미 추가 턴이 예정되어 있으면 겹치지 않고 1회로 처리하며 false를 돌려준다 (기획서 4-3 강탈 딜러 규칙).
        public bool GrantExtraTurn(BattleUnit unit)
        {
            if (extraTurnUnit == unit)
            {
                return false;
            }

            extraTurnUnit = unit;
            return true;
        }

        // 예정된 추가 턴을 꺼내고 비운다. 예정이 없으면 null.
        public BattleUnit ConsumeExtraTurn()
        {
            BattleUnit unit = extraTurnUnit;
            extraTurnUnit = null;
            return unit;
        }
    }

    // 대기열에 들어가는 추가 공격 하나: 누가, 어떤 패시브로, 누구를 노리는지.
    public class FollowUpAttack
    {
        public BattleUnit Owner { get; }
        public PassiveData Passive { get; }

        // 추가 공격을 일으킨 공격의 대상. 실행 시점에 이미 쓰러졌다면 다른 적을 다시 고른다 (기획서 3-9).
        public BattleUnit PreferredTarget { get; }

        public FollowUpAttack(BattleUnit owner, PassiveData passive, BattleUnit preferredTarget)
        {
            Owner = owner;
            Passive = passive;
            PreferredTarget = preferredTarget;
        }
    }
}
