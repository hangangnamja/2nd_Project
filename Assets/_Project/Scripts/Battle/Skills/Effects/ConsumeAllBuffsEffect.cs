using System;
using Game.Core;
using System.Collections.Generic;
using Game.Battle.Status;
using Game.Data;

namespace Game.Battle.Effects
{
    // 시전자의 버프를 전부 소모한다 (기획서 4-3 강탈 딜러 3스).
    // 피해 블록 뒤에 두면, 버프 효과가 피해에 모두 반영된 다음 사라진다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("상태효과/버프 전부 소모", "시전자 버프를 전부 지운다. 피해 블록 뒤에 두면 버프가 피해에 반영된 다음 사라진다 (강탈 딜러 3스)", 320)]
    [Serializable]
    public class ConsumeAllBuffsEffect : SkillEffect
    {
        // 시전자의 버프 칸을 비우고 몇 개를 소모했는지 로그로 남긴다.
        public override void Apply(SkillContext context)
        {
            List<StatusInstance> consumed = context.Caster.Statuses.RemoveAllBuffs();
            context.Log($"  {context.Caster.Name}: 버프 {consumed.Count}개 소모");

            // 소모도 "지워짐"이다 → 지연 버프는 턴 종료 단계에 바뀐 효과로 돌아오고, 부여자의 패시브가 반응한다.
            StatusApplier.ReportRemoved(context.Caster, consumed, context.Services);
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return "자신의 버프 전부 소모";
        }
    }
}
