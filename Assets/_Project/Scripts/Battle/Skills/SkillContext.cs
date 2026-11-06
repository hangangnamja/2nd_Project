using System;
using System.Collections.Generic;

namespace Game.Battle.Effects
{
    // 효과 블록이 실행될 때 필요한 정보 묶음 (누가, 누구에게, 로그·사건·후속 행동을 어디로 보낼지).
    // 효과 블록은 이 정보만 보고 동작하므로 BattleManager를 직접 알 필요가 없다.
    public class SkillContext
    {
        private readonly List<BattleUnit> hitTargets = new List<BattleUnit>();

        public BattleUnit Caster { get; }
        public IReadOnlyList<BattleUnit> Targets { get; }

        // Action<string>: 문자열 하나를 받는 함수를 담는 델리게이트(함수 참조).
        // 효과 블록이 Log("...")를 호출하면 BattleManager가 넘겨준 로그 함수가 실행된다.
        public Action<string> Log { get; }

        // 공격 판정 같은 사건을 알릴 통로. 패시브가 여기에 반응한다.
        public BattleEvents Events { get; }

        // 추가 턴처럼 지금 행동이 끝난 뒤 처리할 일을 맡길 대기열.
        public ActionQueue Actions { get; }

        // 이 효과가 어떤 행동에서 나왔는지 (스킬 / 추가 공격 / 패시브 효과).
        public ActionSource Source { get; }

        // 공격 행동으로 취급하는지. 패시브에 붙은 효과(화상 부여 등)는 공격이 아니라서 반응을 일으키지 않는다.
        public bool CountsAsAttack => Source != ActionSource.PassiveEffect;

        // 이번 행동에서 피해를 받은 대상들 (중복 없이). 행동이 끝난 뒤 "공격 행동" 사건에 담아 보낸다.
        public IReadOnlyList<BattleUnit> HitTargets => hitTargets;

        // 시전자, 대상 목록, 로그 함수, 사건 통로, 대기열, 발생 원인을 받아 실행 정보를 만든다.
        public SkillContext(
            BattleUnit caster,
            IReadOnlyList<BattleUnit> targets,
            Action<string> log,
            BattleEvents events,
            ActionQueue actions,
            ActionSource source)
        {
            Caster = caster;
            Targets = targets;
            Log = log;
            Events = events;
            Actions = actions;
            Source = source;
        }

        // 피해를 받은 대상을 기록한다. 같은 대상을 여러 번 때려도 한 번만 기록한다.
        public void RecordHit(BattleUnit target)
        {
            if (!hitTargets.Contains(target))
            {
                hitTargets.Add(target);
            }
        }
    }
}
