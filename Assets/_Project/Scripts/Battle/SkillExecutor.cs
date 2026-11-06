using System;
using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Data;

namespace Game.Battle
{
    // 스킬 한 번을 실행한다: 효과 블록을 목록 순서대로 실행하고, 끝나면 쿨타임을 건다.
    // 대상 결정은 BattleManager가 하고, 이 클래스는 "정해진 대상에게 스킬을 쓰는 것"만 담당한다.
    public static class SkillExecutor
    {
        // caster가 slot번 스킬을 targets에게 사용한다. 로그는 log 함수로 내보낸다.
        public static void Execute(BattleUnit caster, int slot, IReadOnlyList<BattleUnit> targets, Action<string> log)
        {
            SkillData skill = caster.Data.GetSkill(slot);
            log($"{caster.Name}: {skill.SkillName}");

            var context = new SkillContext(caster, targets, log);

            foreach (SkillEffect effect in skill.Effects)
            {
                // ?. 연산자: effect가 null(드롭다운에서 아직 종류를 안 고른 빈 칸)이면 호출을 건너뛴다.
                effect?.Apply(context);
            }

            caster.StartCooldown(slot);
        }
    }
}
