using System;
using System.Collections.Generic;

namespace Game.Battle.Effects
{
    // 효과 블록이 실행될 때 필요한 정보 묶음 (누가, 누구에게, 어떤 행동에서, 전투 공용 도구).
    // 효과 블록은 이 정보만 보고 동작하므로 BattleManager를 직접 알 필요가 없다.
    public class SkillContext
    {
        private readonly List<BattleUnit> hitTargets = new List<BattleUnit>();

        public BattleUnit Caster { get; }
        public IReadOnlyList<BattleUnit> Targets { get; }

        // 전투 공용 도구 (로그, 사건, 대기열, 난수, 규칙).
        public BattleServices Services { get; }

        // 이 효과가 어떤 행동에서 나왔는지 (스킬 / 추가 공격 / 패시브 효과).
        public ActionSource Source { get; }

        // 자주 쓰는 도구는 바로 꺼내 쓸 수 있게 열어 둔다.
        public Action<string> Log => Services.Log;
        public BattleEvents Events => Services.Events;
        public ActionQueue Actions => Services.Actions;

        // 공격 행동으로 취급하는지. 패시브에 붙은 효과(화상 부여 등)는 공격이 아니라서 반응을 일으키지 않는다.
        public bool CountsAsAttack => Source != ActionSource.PassiveEffect;

        // 이번 행동에서 피해를 받은 대상들 (중복 없이). 행동이 끝난 뒤 "공격 행동" 사건에 담아 보낸다.
        public IReadOnlyList<BattleUnit> HitTargets => hitTargets;

        // 시전자, 대상 목록, 전투 공용 도구, 발생 원인을 받아 실행 정보를 만든다.
        public SkillContext(BattleUnit caster, IReadOnlyList<BattleUnit> targets, BattleServices services, ActionSource source)
        {
            Caster = caster;
            Targets = targets;
            Services = services;
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
