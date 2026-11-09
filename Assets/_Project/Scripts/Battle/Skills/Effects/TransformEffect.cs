using System;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자를 변신시킨다 (기획서 4-3 하이퍼캐리 3스). 변신 설정은 시전자 CharacterData의 "변신" 항목을 쓴다.
    // 변신하면 1~3스가 변신 스킬로 바뀌고, 정해진 자기 턴 수가 끝나면 종결기 → 해제가 자동으로 진행된다.
    [Serializable]
    public class TransformEffect : SkillEffect
    {
        [Tooltip("체크하면 변신하면서 같은 편의 다른 유닛을 전원 퇴장시킨다 (해제 시 복귀)")]
        [SerializeField] private bool exitAllies = true;

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

            caster.Transform(caster.Data.Transformation, exitAllies);
            context.Log($"  {caster.Name}: {caster.Transformation.Data.TransformName} (HP {caster.CurrentHp}/{caster.MaxHp})");

            foreach (BattleUnit ally in caster.Transformation.ExitedAllies)
            {
                context.Log($"  {ally.Name} 퇴장");
            }
        }
    }
}
