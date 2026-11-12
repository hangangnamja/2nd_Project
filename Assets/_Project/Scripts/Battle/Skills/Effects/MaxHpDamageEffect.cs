using System;
using Game.Core;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상 최대 HP × 비율 피해 (보스 2: 디버프가 없는 턴에 아군 전체 최대 HP 40%).
    // 공격력·치명·방어력과 무관하게 "최대 HP의 몇 %"가 들어간다. 받는 피해 증가·피해 감소와 보호막은 적용된다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("공격/대상 최대 HP 비례 피해", "대상 최대 HP × 비율 피해. 공격력·치명·방어력 무시, 피해 감소·보호막은 적용", 105)]
    [Serializable]
    public class MaxHpDamageEffect : SkillEffect
    {
        [Tooltip("대상 최대 HP 대비 피해 비율 (0.4 = 최대 HP 40%)")]
        [Label("최대 HP 비율", Min = 0f, Max = 1f)]
        [SerializeField] private float maxHpRatio = 0.4f;

        // 대상마다 그 대상의 최대 HP로 피해를 계산해, 일반 피해와 같은 흐름(보호막 → HP → 공격 판정)으로 넣는다.
        // 람다식 target => ...: 대상 하나를 받아 그 대상의 피해 결과를 돌려주는 함수를 넘긴다.
        public override void Apply(SkillContext context)
        {
            DamageEffect.ApplyDamageWith(context, target => DamageCalculator.CalculateTargetMaxHpDamage(target, maxHpRatio));
        }
    }
}
