using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using Game.Data;

namespace Game.Battle.Effects
{
    // 대상의 행동 게이지를 당긴다 (기획서 3-3: 남은 거리 -= 10000 × 비율).
    // 자기 턴 중인 유닛이면 턴 종료 리셋 뒤에 적용된다 (BattleUnit.AddActionAdvance).
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("턴·행동/행동 당기기", "대상(또는 시전자 자신) 행동 게이지를 비율만큼 당긴다 (남은 거리 -= 10000 × 비율)", 400)]
    [Serializable]
    public class ActionAdvanceEffect : SkillEffect
    {
        [Tooltip("당기는 비율 (0.3 = 30%)")]
        [Label("당기는 비율", Min = 0f, Max = 1f)]
        [SerializeField] private float ratio = 0.3f;

        // 패시브 "공격 적중 시"처럼 효과 대상이 적으로 정해지는 곳에서 자기 게이지를 당기려면 이 값을 켠다.
        // 예: 니콜 패시브 = 때리면 니콜 자신의 행동 게이지를 당김 (꺼져 있으면 맞은 적의 게이지가 당겨진다)
        [Tooltip("체크하면 스킬·패시브 대상 대신 시전자 자신의 행동 게이지를 당긴다")]
        [Label("시전자 자신")]
        [SerializeField] private bool targetCaster;

        // 대상마다 행동 게이지를 당긴다. 쓰러진 대상은 건너뛴다.
        public override void Apply(SkillContext context)
        {
            // targetCaster면 시전자 한 명만 담은 배열, 아니면 스킬·패시브 대상 목록을 그대로 쓴다.
            IReadOnlyList<BattleUnit> targets = targetCaster
                ? new[] { context.Caster }
                : context.Targets;

            foreach (BattleUnit target in targets)
            {
                if (target.IsDown)
                {
                    continue;
                }

                target.AddActionAdvance(ratio);

                string timing = target.IsTakingTurn ? " (턴 종료 후 적용)" : string.Empty;
                context.Log($"  {target.Name}: 행동 게이지 {ratio * 100f:F0}% 당김{timing}");
            }
        }

        // 상세 창 스킬 탭 설명. 행동 게이지를 당기는 비율과 대상(자신/스킬 대상).
        public override string Describe(SkillTargetType skillTarget)
        {
            return $"{(targetCaster ? "자신의 " : string.Empty)}행동 게이지 {EffectText.Percent(ratio)} 당기기";
        }
    }
}
