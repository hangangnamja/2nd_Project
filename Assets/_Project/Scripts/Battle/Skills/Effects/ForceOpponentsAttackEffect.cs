using System;
using Game.Core;
using Game.Data;

namespace Game.Battle.Effects
{
    // 전장에 있는 상대 전원이 시전자를 1스로 1회씩 공격하게 한다 (기획서 4-3 하이퍼캐리 변신 2스 반격 태세).
    // 공격은 지금 턴이 끝난 뒤 상대 각자의 추가 공격(임시 턴)으로 실행된다 → 상대의 행동 게이지·쿨타임은 변하지 않는다.
    // 이 블록 뒤에 "추가 공격 예약" 블록(반격)을 두면, 대기열 순서상 상대의 공격이 모두 끝난 뒤 반격이 나간다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("턴·행동/상대 강제 공격", "상대 전원이 시전자를 1스로 1회씩 추가 공격하게 한다 (반격 태세). 뒤에 추가 공격 예약을 두면 그다음에 반격", 430)]
    [Serializable]
    public class ForceOpponentsAttackEffect : SkillEffect
    {
        // 상대 편 유닛마다 "시전자를 노리는 1스 추가 공격"을 대기열에 넣는다.
        public override void Apply(SkillContext context)
        {
            BattleUnit caster = context.Caster;

            foreach (BattleUnit unit in context.Services.Units)
            {
                bool isOpponentOnField = unit.IsAlly != caster.IsAlly && unit.IsOnField;
                if (!isOpponentOnField)
                {
                    continue;
                }

                // GetSkill(0): 그 유닛의 1스(평타). 대상 규칙은 1스 그대로 쓰고, 단일 대상이면 시전자를 노린다.
                // 1스가 공격이 아닌 유닛(아군 버프 등)은 끌어들일 공격이 없으므로 건너뛴다.
                SkillData basicAttack = unit.GetSkill(0);
                if (!TargetsOpponents(basicAttack.TargetType))
                {
                    continue;
                }

                context.Actions.EnqueueFollowUp(new FollowUpAttack(
                    unit,
                    basicAttack.SkillName,
                    basicAttack.Effects,
                    basicAttack.TargetType,
                    preferredTarget: caster));
            }

            context.Log($"  {caster.Name}: 상대 전원의 공격을 끌어들임");
        }

        // 상대 편을 공격하는 대상 규칙인지 (적 단일·광역·확산).
        private static bool TargetsOpponents(SkillTargetType targetType)
        {
            return targetType == SkillTargetType.EnemySingle
                || targetType == SkillTargetType.EnemyAll
                || targetType == SkillTargetType.EnemyBlast;
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return "상대 전원이 자신을 1스로 1회씩 공격";
        }
    }
}
