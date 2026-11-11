using System;
using Game.Core;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자에게 걸린 버프 개수에 비례해 강해지는 피해 (기획서 4-3 강탈 딜러 3스).
    // 배율 = 기본 배율 + 버프 1개당 배율 × 버프 개수. 버프 칸 10개가 자연 상한이다.
    // 예: 기본 150%, 버프당 50%, 버프 4개 → 150% + 200% = 350%
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("공격/버프 수 비례 피해", "시전자 버프 개수에 비례한 피해: 기본 배율 + 버프 1개당 배율 × 개수 (강탈 딜러 3스)", 110)]
    [Serializable]
    public class BuffScalingDamageEffect : SkillEffect
    {
        [Tooltip("버프가 없어도 들어가는 기본 배율")]
        [SerializeField, Min(0f)] private float baseMultiplier = 1.5f;

        [Tooltip("버프 1개당 더해지는 배율")]
        [SerializeField, Min(0f)] private float multiplierPerBuff = 0.5f;

        // 버프 개수로 배율을 정한 뒤, 일반 피해 블록과 같은 방식으로 피해를 준다.
        public override void Apply(SkillContext context)
        {
            int buffCount = context.Caster.Statuses.Buffs.Count;
            float multiplier = baseMultiplier + multiplierPerBuff * buffCount;

            context.Log($"  버프 {buffCount}개 → 배율 {multiplier * 100f:F0}%");

            // 람다식 _ => multiplier: 대상이 누구든(_ = 쓰지 않는 매개변수) 같은 배율을 돌려준다.
            DamageEffect.ApplyDamage(context, _ => multiplier);
        }
    }
}
