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

        // 대상마다 상태효과를 count개 건다. 칸이 가득 차 밀려난 효과가 있으면 로그로 알린다.
        public override void Apply(SkillContext context)
        {
            if (status == null)
            {
                context.Log("  (부여할 상태효과가 연결되지 않음)");
                return;
            }

            foreach (BattleUnit target in context.Targets)
            {
                for (int i = 0; i < count; i++)
                {
                    StatusInstance pushedOut = target.Statuses.Add(status, context.Caster);

                    if (pushedOut != null)
                    {
                        context.Log($"  {target.Name}: 칸이 가득 차 {pushedOut.Data.DisplayName}(남은 {pushedOut.RemainingTurns}턴) 밀려남");
                    }
                }

                context.Log($"  {target.Name}에게 {status.DisplayName} {count}개 부여");
            }
        }
    }
}
