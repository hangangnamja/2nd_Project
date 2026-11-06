using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Data;

namespace Game.Battle
{
    // 행동 하나(스킬 또는 추가 공격)를 실행한다: 효과 블록을 목록 순서대로 실행하고,
    // 공격 행동이었다면 끝난 뒤 "공격 행동" 사건을 1번 알린다.
    // 대상 결정은 BattleManager가 하고, 이 클래스는 "정해진 대상에게 효과를 실행하는 것"만 담당한다.
    public static class SkillExecutor
    {
        // caster가 slot번 스킬을 targets에게 사용하고 쿨타임을 건다.
        // primaryTarget: 확산 스킬의 메인 대상 (없으면 null)
        public static void ExecuteSkill(
            BattleUnit caster,
            int slot,
            IReadOnlyList<BattleUnit> targets,
            BattleUnit primaryTarget,
            BattleServices services)
        {
            SkillData skill = caster.Data.GetSkill(slot);
            services.Log($"{caster.Name}: {skill.SkillName}");

            ExecuteEffects(caster, skill.Effects, targets, primaryTarget, services, ActionSource.Skill);
            caster.StartCooldown(slot);

            // 스킬이 끝난 뒤 "자기 행동" 사건을 알린다. 이번 행동으로 쌓인 스택은 다음 아군 행동부터 쓸 수 있다.
            services.Events.RaiseSkillUsed(caster);
        }

        // 효과 목록을 순서대로 실행한다. 스킬, 추가 공격, 패시브 효과가 모두 이 함수를 쓴다.
        // 공격 행동(스킬·추가 공격)이 대상에게 피해를 줬다면, 모든 효과가 끝난 뒤 공격 행동 사건을 알린다.
        public static void ExecuteEffects(
            BattleUnit caster,
            IReadOnlyList<SkillEffect> effects,
            IReadOnlyList<BattleUnit> targets,
            BattleUnit primaryTarget,
            BattleServices services,
            ActionSource source)
        {
            var context = new SkillContext(caster, targets, primaryTarget, services, source);

            foreach (SkillEffect effect in effects)
            {
                // ?. 연산자: effect가 null(드롭다운에서 아직 종류를 안 고른 빈 칸)이면 호출을 건너뛴다.
                effect?.Apply(context);
            }

            if (context.CountsAsAttack && context.HitTargets.Count > 0)
            {
                services.Events.RaiseAttackPerformed(caster, context.HitTargets, primaryTarget, source);
            }
        }
    }
}
