using System;
using Game.Battle.Status;

namespace Game.Battle.Effects
{
    // 격발: 대상에게 걸린 모든 지속피해(화상, 독 등)를 즉시 1회씩 발동시킨다 (기획서 3-8).
    // 지속피해는 사라지거나 남은 턴이 줄지 않고 그대로 유지된다.
    // 효과 목록에서 피해 블록 뒤에 두면, 그 공격으로 막 붙은 화상까지 함께 터진다 (기획서 4-3 격발 딜러 3스).
    [Serializable]
    public class DetonateEffect : SkillEffect
    {
        // 대상마다 지속피해를 1회씩 발동시킨다. 지속피해 피해는 치명타·공격자 피해 증가가 적용되지 않는다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                // 이번 공격으로 HP가 이미 0이면 터뜨려도 피해가 들어가지 않으므로 건너뛴다.
                if (target.IsDown)
                {
                    continue;
                }

                int count = StatusTurnProcessor.TriggerDamageOverTime(target, context.Services, logPrefix: "[격발] ");

                if (count == 0)
                {
                    context.Log($"  {target.Name}: 격발할 지속피해 없음");
                }
            }
        }
    }
}
