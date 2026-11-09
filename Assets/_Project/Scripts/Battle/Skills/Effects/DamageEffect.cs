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

        [Tooltip("확산 스킬에서 메인 대상이 아닌 주변 대상이 받는 비율 (0.5 = 메인 배율의 50%). 메인 대상이 없는 스킬에서는 쓰지 않는다")]
        [SerializeField, Range(0f, 1f)] private float secondaryTargetRatio = 1f;

        // 대상마다 배율을 정해 피해를 준다 (확산이면 주변 대상은 비율만큼 줄어든다).
        public override void Apply(SkillContext context)
        {
            // 람다식 target => ...: 대상 하나를 받아 그 대상에게 쓸 배율을 돌려주는 함수를 넘긴다.
            ApplyDamage(context, target => GetMultiplierFor(target, context));
        }

        // 피해 처리 공용 함수 (일반 피해, 버프 비례 피해 등 피해 블록이 함께 쓴다).
        // multiplierFor: 대상별 배율을 구하는 함수. Func<BattleUnit, float> = "유닛을 받아 실수를 돌려주는 함수"를 담는 델리게이트.
        // 대상마다 피해를 계산해 적용한다. 이미 HP가 0인 대상도 같은 공격 안에서는 효과를 받는다 (기획서 3-9).
        // 피해를 준 직후 "공격 판정" 사건을 알려, 공격 시 발동 패시브(화상 부여 등)가 같은 판정 안에서 붙게 한다.
        public static void ApplyDamage(SkillContext context, Func<BattleUnit, float> multiplierFor)
        {
            foreach (BattleUnit target in context.Targets)
            {
                DamageResult result = DamageCalculator.CalculateAttackDamage(
                    context.Caster,
                    target,
                    multiplierFor(target),
                    context.Services.Random,
                    context.Services.Rules);

                int dealt = target.TakeDamage(result.Amount, context.Caster);
                string critText = result.IsCritical ? " (치명!)" : string.Empty;
                context.Log($"  {target.Name}에게 {dealt} 피해{critText} (HP {target.CurrentHp}/{target.MaxHp})");

                // 공격 행동(스킬, 추가 공격)에서 나간 피해만 공격 판정 사건을 알린다.
                // 패시브에 붙은 피해 효과는 공격이 아니므로 다른 패시브를 깨우지 않는다.
                if (context.CountsAsAttack)
                {
                    context.RecordHit(target);
                    context.Events?.RaiseAttackHit(context.Caster, target);
                }
            }
        }

        // 이 대상에게 쓸 배율: 메인 대상이 있는 스킬(확산)에서 주변 대상이면 배율 × 주변 비율, 그 외는 배율 그대로.
        // 예: 배율 1.5, 주변 비율 0.5 → 메인 150%, 양옆 75%
        private float GetMultiplierFor(BattleUnit target, SkillContext context)
        {
            bool isSecondary = context.PrimaryTarget != null && target != context.PrimaryTarget;
            return isSecondary ? multiplier * secondaryTargetRatio : multiplier;
        }
    }
}
