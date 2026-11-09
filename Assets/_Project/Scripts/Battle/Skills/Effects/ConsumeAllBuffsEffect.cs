using System;
using System.Collections.Generic;
using Game.Battle.Status;

namespace Game.Battle.Effects
{
    // 시전자의 버프를 전부 소모한다 (기획서 4-3 강탈 딜러 3스).
    // 피해 블록 뒤에 두면, 버프 효과가 피해에 모두 반영된 다음 사라진다.
    [Serializable]
    public class ConsumeAllBuffsEffect : SkillEffect
    {
        // 시전자의 버프 칸을 비우고 몇 개를 소모했는지 로그로 남긴다.
        public override void Apply(SkillContext context)
        {
            List<StatusInstance> consumed = context.Caster.Statuses.RemoveAllBuffs();
            context.Log($"  {context.Caster.Name}: 버프 {consumed.Count}개 소모");
        }
    }
}
