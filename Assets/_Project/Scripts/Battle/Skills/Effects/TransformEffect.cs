using System;
using Game.Core;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자를 변신시킨다 (기획서 4-3 하이퍼캐리 3스). 변신 설정은 시전자 CharacterData의 "변신" 항목을 쓴다.
    // 변신하면 1~3스가 변신 스킬로 바뀌고, 정해진 자기 턴 수가 끝나면 종결기 → 해제가 자동으로 진행된다.
    // 아군 퇴장은 이 블록이 아니라 경계 블록(IsolationBoundaryEffect, 경계: 고립)이 맡는다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("변신·전장/변신", "시전자를 변신시킨다 (1~3스가 변신 스킬로 교체, 턴 제한·종결기·해제는 변신 설정을 따름)", 500)]
    [Serializable]
    public class TransformEffect : SkillEffect
    {
        // 변신 설정이 있는지 확인하고 변신한다. 이미 변신 중이면 다시 변신하지 않는다.
        public override void Apply(SkillContext context)
        {
            BattleUnit caster = context.Caster;

            if (caster.Data.Transformation == null)
            {
                context.Log($"  [오류] {caster.Name}에게 변신 설정이 없습니다.");
                return;
            }

            if (caster.IsTransformed)
            {
                return;
            }

            caster.Transform(caster.Data.Transformation);
            context.Log($"  {caster.Name}: {caster.Transformation.Data.TransformName} (HP {caster.CurrentHp}/{caster.MaxHp})");
        }
    }
}
