using System;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상 전원에게 상태효과(화상, 버프 등)를 부여한다.
    [Serializable]
    public class ApplyStatusEffect : SkillEffect
    {
        [SerializeField] private StatusEffectData status;

        [Tooltip("한 번에 거는 개수. 중첩형 효과는 개수만큼 칸을 차지한다")]
        [SerializeField, Min(1)] private int count = 1;

        // 대상마다 상태효과를 count개 건다. 밀려난 효과 로그와 "새 버프 획득" 사건은 StatusApplier가 처리한다.
        public override void Apply(SkillContext context)
        {
            if (status == null)
            {
                context.Log("  (부여할 상태효과가 연결되지 않음)");
                return;
            }

            foreach (BattleUnit target in context.Targets)
            {
                context.Log($"  {target.Name}에게 {status.DisplayName} {count}개 부여");

                for (int i = 0; i < count; i++)
                {
                    StatusApplier.Apply(target, status, context.Caster, context.Services);
                }
            }
        }
    }
}
