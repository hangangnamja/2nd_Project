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
        // 피해를 준 직후 "공격 판정" 사건을 알려, 공격 시 발동 패시브(화상 부여 등)가 같은 판정 안에서 붙게 한다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                int damage = DamageCalculator.CalculateDamage(context.Caster, target, multiplier);
                int dealt = target.TakeDamage(damage);
                context.Log($"  {target.Name}에게 {dealt} 피해 (HP {target.CurrentHp}/{target.MaxHp})");

                // 패시브 반응으로 나간 피해는 다시 사건을 일으키지 않는다 (반응이 반응을 부르는 무한 연쇄 방지).
                if (!context.IsReaction)
                {
                    context.Events?.RaiseAttackHit(context.Caster, target);
                }
            }
        }
    }
}
