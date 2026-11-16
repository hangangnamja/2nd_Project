using System;
using Game.Core;
using Game.Data;

namespace Game.Battle.Effects
{
    // 아군 퇴장 (하이퍼캐리 3스): 시전자를 뺀 같은 편 중 전장에 있는 유닛을 모두 퇴장시킨다.
    // 퇴장한 아군은 공격받지 않고 게이지도 멈추며, 시전자의 변신이 끝나거나 시전자가 쓰러지면 퇴장 전 게이지 그대로 복귀한다.
    // 경계(고립)와는 별개의 효과다: 고립은 스탯·면역, 이 블록은 퇴장만 맡는다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("변신·전장/아군 퇴장", "시전자를 뺀 아군을 퇴장시킨다. 시전자의 변신이 끝나거나 쓰러지면 퇴장 전 게이지 그대로 복귀", 510)]
    [Serializable]
    public class ExitAlliesEffect : SkillEffect
    {
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit ally in context.Caster.ExitAllies())
            {
                context.Log($"  {ally.Name} 퇴장");
            }
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return "다른 아군 전원 퇴장 (변신이 끝나면 복귀)";
        }
    }
}
