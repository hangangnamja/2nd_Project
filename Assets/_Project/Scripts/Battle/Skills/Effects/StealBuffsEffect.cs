using System;
using Game.Core;
using System.Collections.Generic;
using Game.Battle.Status;
using Game.Data;

namespace Game.Battle.Effects
{
    // 대상의 버프를 전부 빼앗는다 (기획서 4-3 강탈 딜러 2스).
    // 처리 순서: 타격 → 강탈(대상에게서 즉시 제거) → 턴 종료 → 시전자가 버프 획득.
    //  - 제거는 즉시: 이후의 추가 공격과 적의 다음 행동은 버프가 빠진 상태를 본다.
    //  - 획득은 턴 종료 단계: 강탈한 턴의 남은 턴 감소를 건너뛰어, 1턴짜리 버프도 다음 자기 턴까지 유지된다.
    // 빼앗은 버프는 남은 턴을 그대로 계승하고, 같은 버프가 이미 있으면 남은 턴이 긴 쪽 하나만 남긴다.
    // HP가 0이 된 대상도 같은 공격 안에서는 강탈된다 (사망 판정 규칙, 기획서 3-9).
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("상태효과/버프 강탈", "대상 버프를 전부 즉시 빼앗고, 턴 종료 단계에 시전자가 남은 턴 그대로 얻는다 (강탈 딜러 2스)", 310)]
    [Serializable]
    public class StealBuffsEffect : SkillEffect
    {
        // 대상마다 버프를 비우고, 비운 버프를 시전자가 턴 종료 단계에 받도록 예약한다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                // 강탈은 효과가 지워지는 게 아니라 그대로 옮겨가는 것이므로 "지워짐"(지연 버프 변환, 부여자 패시브)으로 알리지 않는다.
                List<StatusInstance> stolen = target.Statuses.RemoveAllBuffs();

                if (stolen.Count == 0)
                {
                    context.Log($"  {target.Name}: 강탈할 버프 없음");
                    continue;
                }

                context.Log($"  {target.Name}의 버프 {stolen.Count}개 강탈 (턴 종료 시 획득)");

                foreach (StatusInstance buff in stolen)
                {
                    context.Services.Actions.EnqueueTurnEndBuff(new TurnEndBuff(context.Caster, buff));
                }
            }
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return "대상 버프 전부 강탈 (턴 종료 단계에 획득)";
        }
    }
}
