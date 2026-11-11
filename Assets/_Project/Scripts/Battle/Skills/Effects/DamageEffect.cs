using System;
using Game.Battle.Status;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상 전원에게 "공격력 × 배율" 기반 피해를 준다.
    [Serializable]
    public class DamageEffect : SkillEffect
    {
        [Tooltip("공격력 대비 배율 (100% = 1.0). 확산 스킬에서는 메인 대상이 받는 배율")]
        [SerializeField, Min(0f)] private float multiplier = 1f;

        [Tooltip("확산 스킬에서 양옆(메인 대상이 아닌) 대상이 받는 공격력 대비 배율. 메인 배율과 따로 정한다. 메인 대상이 없는 스킬(단일·광역)에서는 쓰지 않는다")]
        [SerializeField, Min(0f)] private float secondaryMultiplier = 1f;

        // 대상마다 배율을 정해 피해를 준다 (확산이면 메인 대상과 양옆이 서로 다른 배율을 쓴다).
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
            ApplyDamageWith(context, target => DamageCalculator.CalculateAttackDamage(
                context.Caster,
                target,
                multiplierFor(target),
                context.Services.Random,
                context.Services.Rules));
        }

        // 기본 피해를 직접 정해 대상 전원에게 피해를 준다 (공격력 배율 대신 받은 피해 비례 등).
        public static void ApplyBaseDamage(SkillContext context, double baseDamage)
        {
            ApplyDamageWith(context, target => DamageCalculator.CalculateAttackDamageFromBase(
                context.Caster,
                target,
                baseDamage,
                context.Services.Random,
                context.Services.Rules));
        }

        // 피해 적용 공통 흐름: 대상마다 calculate로 피해를 구하고 → 보호막·HP에 적용 → 로그 → 공격 판정 사건.
        // calculate: 대상 하나를 받아 피해 결과를 돌려주는 함수 (배율 방식 / 기본 피해 방식에 따라 다르게 넘긴다).
        private static void ApplyDamageWith(SkillContext context, Func<BattleUnit, DamageResult> calculate)
        {
            foreach (BattleUnit target in context.Targets)
            {
                DamageResult result = calculate(target);

                // 보호막이 있으면 먼저 흡수하고, 남은 피해만 HP에 들어간다.
                // out absorbed: 함수가 피해와 별도로 "보호막이 흡수한 양"을 하나 더 돌려준다 (로그용).
                int dealt = StatusApplier.DealDamage(target, result.Amount, context.Caster, context.Services, out int absorbed, result.IsCritical);
                string critText = result.IsCritical ? " (치명!)" : string.Empty;
                string shieldText = absorbed > 0 ? $" (보호막 {absorbed} 흡수)"
                    : target.IsInvulnerable ? " (무적)"
                    : string.Empty;
                context.Log($"  {target.Name}에게 {dealt} 피해{critText}{shieldText} (HP {target.CurrentHp}/{target.MaxHp})");

                // 공격 행동(스킬, 추가 공격)에서 나간 피해만 공격 판정 사건을 알린다.
                // 패시브에 붙은 피해 효과는 공격이 아니므로 다른 패시브를 깨우지 않는다.
                if (context.CountsAsAttack)
                {
                    context.RecordHit(target);
                    context.Events?.RaiseAttackHit(context.Caster, target);
                }
            }
        }

        // 이 대상에게 쓸 배율: 메인 대상이 있는 스킬(확산)에서 양옆 대상이면 양옆 배율, 그 외(메인 대상·단일·광역)는 기본 배율.
        // context.PrimaryTarget: 확산에서 직접 고른 적. 단일·광역 스킬은 null이라 항상 기본 배율을 쓴다.
        // 예: 기본 배율 1.5, 양옆 배율 0.75 → 메인 150%, 양옆 75%
        private float GetMultiplierFor(BattleUnit target, SkillContext context)
        {
            bool isSecondary = context.PrimaryTarget != null && target != context.PrimaryTarget;
            return isSecondary ? secondaryMultiplier : multiplier;
        }
    }
}
