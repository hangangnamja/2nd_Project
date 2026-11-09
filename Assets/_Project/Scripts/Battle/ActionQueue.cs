using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Battle.Status;
using Game.Data;

namespace Game.Battle
{
    // 턴이 끝난 뒤 실행할 후속 행동(추가 공격 = 임시 턴, 추가 턴)과, 턴 종료 단계에 받을 버프·패시브 효과를 모아 둔다.
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

        // 턴 종료 단계에 받을 버프 (강탈한 버프). 패시브 효과보다 먼저 들어간다.
        private readonly Queue<TurnEndBuff> turnEndBuffs = new Queue<TurnEndBuff>();

        // 턴 종료 단계에 받을 버프 하나를 예약한다.
        public void EnqueueTurnEndBuff(TurnEndBuff buff)
        {
            turnEndBuffs.Enqueue(buff);
        }

        // 예약된 턴 종료 버프를 하나 꺼낸다. 없으면 false.
        public bool TryDequeueTurnEndBuff(out TurnEndBuff buff)
        {
            if (turnEndBuffs.Count > 0)
            {
                buff = turnEndBuffs.Dequeue();
                return true;
            }

            buff = null;
            return false;
        }

        // 턴 종료 단계에 실행할 패시브 효과 (강탈 딜러 추격 등). 발동한 순서대로 실행된다.
        private readonly Queue<TurnEndEffect> turnEndEffects = new Queue<TurnEndEffect>();

        // 턴 종료 단계에 실행할 패시브 효과 하나를 예약한다.
        public void EnqueueTurnEndEffect(TurnEndEffect effect)
        {
            turnEndEffects.Enqueue(effect);
        }

        // 예약된 턴 종료 효과를 하나 꺼낸다. 없으면 false.
        public bool TryDequeueTurnEndEffect(out TurnEndEffect effect)
        {
            if (turnEndEffects.Count > 0)
            {
                effect = turnEndEffects.Dequeue();
                return true;
            }

            effect = null;
            return false;
        }
    }

    // 턴 종료 단계에 받을 버프 하나. 두 종류가 있다.
    //  - 강탈한 버프: 이미 있는 효과 인스턴스를 남은 턴 그대로 받는다 (Instance)
    //  - 지연 버프가 바뀐 효과: 효과 정의로 새로 건다 (Data + Source)
    public class TurnEndBuff
    {
        public BattleUnit Receiver { get; }
        public StatusInstance Instance { get; }
        public StatusEffectData Data { get; }
        public BattleUnit Source { get; }

        // 강탈한 버프를 그대로 받는 경우.
        public TurnEndBuff(BattleUnit receiver, StatusInstance instance)
        {
            Receiver = receiver;
            Instance = instance;
        }

        // 지연 버프가 지워져 새 효과를 받는 경우. source = 지연 버프를 건 유닛.
        public TurnEndBuff(BattleUnit receiver, StatusEffectData data, BattleUnit source)
        {
            Receiver = receiver;
            Data = data;
            Source = source;
        }
    }

    // 턴 종료 단계에 실행할 패시브 효과 하나:누가, 어떤 패시브를 실행하는지. 효과 대상은 패시브 주인 자신이다.
    public class TurnEndEffect
    {
        public BattleUnit Owner { get; }
        public PassiveData Passive { get; }

        public TurnEndEffect(BattleUnit owner, PassiveData passive)
        {
            Owner = owner;
            Passive = passive;
        }
    }

    // 대기열에 들어가는 추가 공격 하나 (= 임시 턴 하나): 누가, 어떤 효과를, 어떤 대상 규칙으로, 누구를 노리는지.
    // 패시브 추가 공격뿐 아니라 반격 태세의 강제 공격·반격, 변신 종결기도 같은 대기열로 순서대로 실행한다.
    public class FollowUpAttack
    {
        public BattleUnit Owner { get; }

        // 로그에 표시할 이름 (패시브 이름, "반격", 종결기 이름 등).
        public string Name { get; }

        public IReadOnlyList<SkillEffect> Effects { get; }

        // 대상 규칙. 단일 대상이면 PreferredTarget을 노리고, 광역이면 실행 시점의 상대 전원을 노린다.
        public SkillTargetType TargetType { get; }

        // 추가 공격을 일으킨 공격의 대상. 실행 시점에 이미 쓰러졌다면 다른 적을 다시 고른다 (기획서 3-9).
        public BattleUnit PreferredTarget { get; }

        // 이 추가 공격이 끝나면 주인의 변신을 해제하는지 (변신 종결기).
        public bool EndsTransformation { get; }

        // 패시브로 나가는 추가 공격 (격발 딜러 패시브 2 등). 패시브의 효과를 단일 대상에게 쓴다.
        public FollowUpAttack(BattleUnit owner, PassiveData passive, BattleUnit preferredTarget)
            : this(owner, passive.PassiveName, passive.Effects, SkillTargetType.EnemySingle, preferredTarget)
        {
        }

        // 효과 목록을 직접 지정하는 추가 공격 (반격 태세, 변신 종결기 등).
        public FollowUpAttack(
            BattleUnit owner,
            string name,
            IReadOnlyList<SkillEffect> effects,
            SkillTargetType targetType,
            BattleUnit preferredTarget,
            bool endsTransformation = false)
        {
            Owner = owner;
            Name = name;
            Effects = effects;
            TargetType = targetType;
            PreferredTarget = preferredTarget;
            EndsTransformation = endsTransformation;
        }
    }
}
