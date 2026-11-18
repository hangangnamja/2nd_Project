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

        // 메인 대상 (확산 스킬에서 직접 고른 적). 메인 대상이 없는 광역·자신 스킬은 null.
        // 피해 블록은 메인 대상이 있을 때 나머지 대상에게 "주변 대상 배율"을 적용한다.
        public BattleUnit PrimaryTarget { get; }

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

        // 시전자, 대상 목록, 메인 대상, 전투 공용 도구, 발생 원인을 받아 실행 정보를 만든다.
        public SkillContext(
            BattleUnit caster,
            IReadOnlyList<BattleUnit> targets,
            BattleUnit primaryTarget,
            BattleServices services,
            ActionSource source)
        {
            Caster = caster;
            Targets = targets;
            PrimaryTarget = primaryTarget;
            Services = services;
            Source = source;
        }

        // 이번 행동의 피해 블록들이 가한 피해 합계 (HP에 들어간 양 + 보호막이 흡수한 양, 무적으로 막힌 피해는 0).
        // 피흡처럼 "가한 피해 비례" 효과가 읽는다 → 그 효과는 효과 목록에서 피해 블록 뒤에 있어야 한다.
        public int DamageDealt { get; private set; }

        // 피해 블록이 대상 하나에게 피해를 줄 때마다 부른다.
        public void RecordDamage(int amount)
        {
            DamageDealt += amount;
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
