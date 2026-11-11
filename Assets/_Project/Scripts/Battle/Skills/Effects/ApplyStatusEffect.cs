using System;
using Game.Core;
using System.Collections.Generic;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상 전원에게 상태효과(화상, 버프 등)를 부여한다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("상태효과/상태효과 부여", "대상에게 상태효과(버프·디버프·지속피해 등)를 개수만큼 건다. 아군 전체에 거는 옵션이 있다", 300)]
    [Serializable]
    public class ApplyStatusEffect : SkillEffect
    {
        [Label("상태효과")]
        [SerializeField] private StatusEffectData status;

        [Tooltip("한 번에 거는 개수. 중첩형 효과는 개수만큼 칸을 차지한다")]
        [Label("개수", Min = 1f)]
        [SerializeField] private int count = 1;

        // 적을 때리는 스킬에 "아군 전체 버프"를 함께 붙일 때 쓴다 (돌파 딜러 3스: 광역 피해 + 천재의 지원).
        [Tooltip("체크하면 스킬 대상 대신 시전자 편 아군 전원(자신 포함, 전장에 있는 유닛)에게 건다")]
        [Label("아군 전체에")]
        [SerializeField] private bool applyToAllAllies;

        // 대상마다 상태효과를 count개 건다. 밀려난 효과 로그와 "새 버프 획득" 사건은 StatusApplier가 처리한다.
        public override void Apply(SkillContext context)
        {
            if (status == null)
            {
                context.Log("  (부여할 상태효과가 연결되지 않음)");
                return;
            }

            foreach (BattleUnit target in PickTargets(context))
            {
                context.Log($"  {target.Name}에게 {status.DisplayName} {count}개 부여");

                for (int i = 0; i < count; i++)
                {
                    StatusApplier.Apply(target, status, context.Caster, context.Services);
                }
            }
        }

        // 효과를 걸 대상: 기본은 스킬 대상, applyToAllAllies면 시전자와 같은 편이면서 전장에 있는 유닛 전원.
        private IReadOnlyList<BattleUnit> PickTargets(SkillContext context)
        {
            if (!applyToAllAllies)
            {
                return context.Targets;
            }

            var allies = new List<BattleUnit>();
            foreach (BattleUnit unit in context.Services.Units)
            {
                if (unit.IsAlly == context.Caster.IsAlly && unit.IsOnField)
                {
                    allies.Add(unit);
                }
            }

            return allies;
        }
    }
}
