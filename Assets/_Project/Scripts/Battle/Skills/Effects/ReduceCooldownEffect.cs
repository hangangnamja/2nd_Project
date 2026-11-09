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

        // 대상마다 지정한 스킬의 남은 쿨타임을 줄이고 결과를 로그로 남긴다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
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
