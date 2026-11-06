using System;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상 전원에게 "공격력 × 배율" 기반 피해를 준다.
    [Serializable]
    public class DamageEffect : SkillEffect
    {
        [Tooltip("공격력 대비 배율 (100% = 1.0)")]
        [SerializeField, Min(0f)] private float multiplier = 1f;

        // 대상마다 피해를 계산해 적용한다. 이미 쓰러진 대상도 같은 공격 안에서는 효과를 받는다 (기획서 3-9).
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                int damage = DamageCalculator.CalculateDamage(context.Caster, target, multiplier);
                int dealt = target.TakeDamage(damage);
                context.Log($"  {target.Name}에게 {dealt} 피해 (HP {target.CurrentHp}/{target.MaxHp})");
            }
        }
    }
}
