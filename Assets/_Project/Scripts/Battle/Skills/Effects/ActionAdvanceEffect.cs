using System;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상의 행동 게이지를 당긴다 (기획서 3-3: 남은 거리 -= 10000 × 비율).
    // 자기 턴 중인 유닛이면 턴 종료 리셋 뒤에 적용된다 (BattleUnit.AddActionAdvance).
    [Serializable]
    public class ActionAdvanceEffect : SkillEffect
    {
        [Tooltip("당기는 비율 (0.3 = 30%)")]
        [SerializeField, Range(0f, 1f)] private float ratio = 0.3f;

        // 대상마다 행동 게이지를 당긴다. 쓰러진 대상은 건너뛴다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                if (target.IsDown)
                {
                    continue;
                }

                target.AddActionAdvance(ratio);

                string timing = target.IsTakingTurn ? " (턴 종료 후 적용)" : string.Empty;
                context.Log($"  {target.Name}: 행동 게이지 {ratio * 100f:F0}% 당김{timing}");
            }
        }
    }
}
