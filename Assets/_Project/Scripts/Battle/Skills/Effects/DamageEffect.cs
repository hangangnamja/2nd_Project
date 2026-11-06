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

                // 공격 행동(스킬, 추가 공격)에서 나간 피해만 공격 판정 사건을 알린다.
                // 패시브에 붙은 피해 효과는 공격이 아니므로 다른 패시브를 깨우지 않는다.
                if (context.CountsAsAttack)
                {
                    context.RecordHit(target);
                    context.Events?.RaiseAttackHit(context.Caster, target);
                }
            }
        }
    }
}
