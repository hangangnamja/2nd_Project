using System;
using Game.Core;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상 전원의 HP를 회복한다. 회복량 기준 스탯(공격력 / 최대 HP)은 캐릭터마다 다르므로 선택할 수 있다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("회복·보호/회복", "대상 HP 회복. 기준 스탯(공격력·최대 HP·시전자 최대 HP)과 비율을 고르고, 시전자 자신·아군 전체 옵션이 있다", 200)]
    [Serializable]
    public class HealEffect : SkillEffect
    {
        // 에셋에는 숫자 순서로 저장되므로 새 항목은 맨 뒤에 추가한다.
        public enum ScaleStat
        {
            [InspectorName("시전자 공격력")] CasterAttack,
            [InspectorName("대상 최대 HP")] TargetMaxHp,

            // 시전자의 최대 HP 기준 (아스트라 패시브: 시전자 최대 HP의 15%를 아군 전체에)
            [InspectorName("시전자 최대 HP")] CasterMaxHp,
        }

        [Label("기준 스탯")]

        [SerializeField] private ScaleStat scaleStat = ScaleStat.CasterAttack;

        [Tooltip("기준 스탯 대비 회복 비율 (100% = 1.0)")]
        [Label("비율", Min = 0f)]
        [SerializeField] private float ratio = 1f;

        // 공격 스킬에 붙여 "공격하고 자신도 회복"을 만들 때 쓴다 (하이퍼캐리 변신 스킬의 HP 20% 회복).
        // 이때 대상 최대 HP 기준이면 시전자 자신의 최대 HP가 기준이 된다.
        [Tooltip("체크하면 스킬 대상 대신 시전자 자신을 회복한다")]
        [Label("시전자 회복")]
        [SerializeField] private bool healCaster;

        // 패시브처럼 대상이 자신 하나로 정해지는 곳에서 "아군 전체 회복"을 만들 때 쓴다 (아스트라 패시브).
        [Tooltip("체크하면 스킬 대상 대신 시전자 편 아군 전원(자신 포함, 전장에 있는 유닛)을 회복한다")]
        [Label("아군 전체 회복")]
        [SerializeField] private bool healAllAllies;

        // 대상마다 회복량을 계산해 적용한다. HP가 0인 대상은 회복하지 않는다 (게임에 부활 없음).
        // 사망 확정 전(같은 공격 안에서 HP 0)이라도 회복하면 사실상 부활이 되므로 IsDown으로 막는다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in PickTargets(context))
            {
                if (target.IsDown)
                {
                    continue;
                }

                int healed = target.Heal(CalculateAmount(context.Caster, target));
                context.Log($"  {target.Name} {healed} 회복 (HP {target.CurrentHp}/{target.MaxHp})");
                context.Events?.RaiseHealed(target, healed);
            }
        }

        // 회복할 대상: 아군 전원(healAllAllies) → 시전자(healCaster) → 스킬 대상 순으로 정한다.
        private System.Collections.Generic.IReadOnlyList<BattleUnit> PickTargets(SkillContext context)
        {
            if (healAllAllies)
            {
                var allies = new System.Collections.Generic.List<BattleUnit>();
                foreach (BattleUnit unit in context.Services.Units)
                {
                    if (unit.IsAlly == context.Caster.IsAlly && unit.IsOnField)
                    {
                        allies.Add(unit);
                    }
                }

                return allies;
            }

            // healCaster면 시전자 한 명만 담은 배열, 아니면 스킬 대상 목록을 그대로 회복 대상으로 쓴다.
            return healCaster ? new[] { context.Caster } : context.Targets;
        }

        // 기준 스탯 × 비율로 회복량을 구한다. 시전자 공격력·시전자 최대 HP 기준이면 시전자, 대상 최대 HP 기준이면 대상의 값을 쓴다.
        // switch 식: 기준 스탯마다 쓸 값을 고른다.
        private int CalculateAmount(BattleUnit caster, BattleUnit target)
        {
            // caster.Attack·caster.MaxHp: 버프·경계가 반영된 시전자의 최종 스탯
            float baseValue = scaleStat switch
            {
                ScaleStat.CasterAttack => caster.Attack,
                ScaleStat.CasterMaxHp => caster.MaxHp,
                _ => target.MaxHp,
            };

            // Mathf.FloorToInt: 소수점을 버려 정수 회복량으로 만든다.
            return Mathf.FloorToInt(baseValue * ratio);
        }
    }
}
