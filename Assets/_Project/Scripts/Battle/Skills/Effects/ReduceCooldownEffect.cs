using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상의 지정한 스킬 쿨타임을 줄인다 (곰인형 버퍼 패시브 등).
    [Serializable]
    public class ReduceCooldownEffect : SkillEffect
    {
        [Tooltip("쿨타임을 줄일 스킬 번호 (1 = 1스, 2 = 2스, 3 = 3스)")]
        [SerializeField] private List<int> skillNumbers = new List<int> { 2, 3 };

        [Tooltip("줄일 턴 수")]
        [SerializeField, Min(1)] private int turns = 1;

        // 적을 공격하는 스킬에 붙이면 스킬 대상은 적이므로, 자기 쿨타임을 줄이려면 이 값을 켜야 한다.
        // 예: 하이퍼캐리 2스 = 적 공격 + "자신의" 3스 쿨타임 감소
        [Tooltip("체크하면 스킬 대상 대신 시전자 자신의 쿨타임을 줄인다")]
        [SerializeField] private bool targetCaster;

        // 대상마다 지정한 스킬의 남은 쿨타임을 줄이고 결과를 로그로 남긴다.
        public override void Apply(SkillContext context)
        {
            // targetCaster면 시전자 한 명만 담은 배열, 아니면 스킬 대상 목록을 그대로 쓴다.
            IReadOnlyList<BattleUnit> targets = targetCaster
                ? new[] { context.Caster }
                : context.Targets;

            foreach (BattleUnit target in targets)
            {
                foreach (int skillNumber in skillNumbers)
                {
                    // 스킬 번호(1~3)를 배열 칸 번호(0~2)로 바꾼다.
                    int slot = skillNumber - 1;
                    target.ReduceCooldown(slot, turns);
                }

                context.Log($"  {target.Name}: {string.Join("·", skillNumbers)}스 쿨타임 {turns} 감소");
            }
        }
    }
}
