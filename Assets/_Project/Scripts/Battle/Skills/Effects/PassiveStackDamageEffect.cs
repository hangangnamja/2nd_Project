using System;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자 패시브에 쌓인 스택에 비례해 강해지는 피해를 주고, 그 스택을 전부 소모한다 (니콜 2스: 이자 정산).
    // 배율 = 기본 배율 + 스택 1개당 배율 × 스택 수. 예: 기본 200%, 스택당 20%, 이자 6스택 → 320%
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("공격/패시브 스택 비례 피해", "시전자 패시브 스택에 비례한 피해: 기본 배율 + 스택 1개당 배율 × 스택 수. 피해 뒤 그 스택을 전부 소모 (니콜 2스 이자)", 115)]
    [Serializable]
    public class PassiveStackDamageEffect : SkillEffect
    {
        [Tooltip("스택이 없을 때의 공격력 대비 배율 (2.0 = 200%)")]
        [Label("기본 배율", Min = 0f)]
        [SerializeField] private float baseMultiplier = 2f;

        [Tooltip("스택 1개당 더해지는 배율 (0.2 = +20%)")]
        [Label("스택당 배율", Min = 0f)]
        [SerializeField] private float multiplierPerStack = 0.2f;

        [Tooltip("스택을 읽을 패시브 번호 (1 = 시전자의 첫 번째 패시브)")]
        [Label("패시브 번호", Min = 1f)]
        [SerializeField] private int passiveNumber = 1;

        // 스택 수로 배율을 정해 대상 전원에게 피해를 준 뒤, 스택을 모두 소모한다.
        // 광역이어도 배율은 한 번만 정한다 → 모든 대상이 같은 스택 수의 혜택을 받고, 소모는 공격이 끝난 뒤 한 번.
        public override void Apply(SkillContext context)
        {
            PassiveData passive = FindPassive(context.Caster);
            int stacks = passive != null ? context.Caster.GetPassiveStacks(passive) : 0;
            float multiplier = baseMultiplier + multiplierPerStack * stacks;

            context.Log($"  {passive?.PassiveName ?? "스택"} {stacks}개 → 배율 {multiplier * 100f:F0}%");

            // 람다식 _ => multiplier: 대상이 누구든(_ = 쓰지 않는 매개변수) 같은 배율을 돌려준다.
            DamageEffect.ApplyDamage(context, _ => multiplier);

            if (stacks > 0)
            {
                context.Caster.TryConsumePassiveStacks(passive, stacks);
            }
        }

        // 패시브 번호(1부터)를 목록 칸 번호(0부터)로 바꿔 찾는다. 번호가 목록 밖이면 null → 스택 0으로 취급.
        private PassiveData FindPassive(BattleUnit caster)
        {
            int index = passiveNumber - 1;
            return index >= 0 && index < caster.Data.Passives.Count ? caster.Data.Passives[index] : null;
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return $"공격력 {EffectText.Percent(baseMultiplier)} + 패시브 스택 1개당 {EffectText.Percent(multiplierPerStack)} 피해, 스택 전부 소모";
        }
    }
}
