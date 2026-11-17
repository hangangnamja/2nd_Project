using System;
using Game.Core;
using UnityEngine;
using Game.Data;

namespace Game.Battle.Effects
{
    // 공격력 배율 피해 + 대상 최대 HP × 비율 피해 (보스 2: 디버프가 없는 턴에 아군 전체 공격력 100% + 최대 HP 40%).
    // 공격력 몫은 일반 공격처럼 피해 증가·치명·방어력이 적용되고, 최대 HP 몫은 공격력·치명·방어력과 무관하다.
    // 공격력 몫을 두는 이유: 최대 HP 몫만 있으면 레벨이 낮은 아군도 "최대 HP의 40%"만 잃어 한 대에 쓰러지지 않는다.
    //   공격력 몫이 레벨 차이만큼 커지므로, 권장 레벨보다 약한 아군은 이 공격 한 번에 쓰러질 수 있다.
    // 받는 피해 증가·피해 감소와 보호막은 두 몫 모두에 적용된다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("공격/대상 최대 HP 비례 피해", "공격력 × 배율 피해 + 대상 최대 HP × 비율 피해. 최대 HP 몫은 공격력·치명·방어력 무시, 피해 감소·보호막은 둘 다 적용", 105)]
    [Serializable]
    public class MaxHpDamageEffect : SkillEffect
    {
        [Tooltip("공격력 대비 피해 배율 (1.0 = 공격력 100%). 0이면 최대 HP 몫만 들어간다")]
        [Label("공격력 배율", Min = 0f)]
        [SerializeField] private float attackMultiplier = 1f;

        [Tooltip("대상 최대 HP 대비 피해 비율 (0.4 = 최대 HP 40%)")]
        [Label("최대 HP 비율", Min = 0f, Max = 1f)]
        [SerializeField] private float maxHpRatio = 0.4f;

        // 대상마다 공격력 몫과 최대 HP 몫을 더한 피해를 구해, 일반 피해와 같은 흐름(보호막 → HP → 공격 판정)으로 넣는다.
        // 람다식 target => ...: 대상 하나를 받아 그 대상의 피해 결과를 돌려주는 함수를 넘긴다.
        public override void Apply(SkillContext context)
        {
            DamageEffect.ApplyDamageWith(context, target => CalculateCombinedDamage(context, target));
        }

        // 공격력 몫(치명 판정 포함) + 최대 HP 몫을 한 번의 피해로 합친다. 치명 여부는 공격력 몫의 판정을 따른다.
        // 공격력 배율이 0이면 치명 판정(난수)을 하지 않아, 예전처럼 최대 HP 몫만 쓰는 블록과 결과가 같다.
        private DamageResult CalculateCombinedDamage(SkillContext context, BattleUnit target)
        {
            DamageResult hpPart = DamageCalculator.CalculateTargetMaxHpDamage(target, maxHpRatio);
            if (attackMultiplier <= 0f)
            {
                return hpPart;
            }

            DamageResult attackPart = DamageCalculator.CalculateAttackDamage(
                context.Caster,
                target,
                attackMultiplier,
                context.Services.Random,
                context.Services.Rules);

            return new DamageResult(attackPart.Amount + hpPart.Amount, attackPart.IsCritical);
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            string hpText = $"대상 최대 HP {EffectText.Percent(maxHpRatio)}";
            return attackMultiplier > 0f
                ? $"공격력 {EffectText.Percent(attackMultiplier)} + {hpText} 피해 (최대 HP 몫은 치명·방어력 무시)"
                : $"{hpText} 피해 (공격력·치명·방어력 무시)";
        }
    }
}
