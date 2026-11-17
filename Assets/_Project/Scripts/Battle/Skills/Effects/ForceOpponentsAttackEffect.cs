using System;
using Game.Core;
using Game.Data;

namespace Game.Battle.Effects
{
    // 전장에 있는 상대 전원의 다음 턴을 지금 턴이 끝난 직후로 끌어온다 (기획서 4-3 하이퍼캐리 변신 2스 반격 태세).
    // 끌어온 턴은 상대의 실제 정규 턴이다: AI가 패턴대로 스킬을 고르고(강한 공격 포함), 턴 종료 처리·게이지 리셋도 그대로 한다.
    //   단일·확산 공격은 시전자를 노린다. 1스만 쓰게 하던 예전 방식과 달리 보스의 강한 공격도 받아 내 반격을 키울 수 있다.
    // 이 블록 뒤에 "추가 공격 예약" 블록(반격)을 두면, 끌어온 턴이 모두 끝난 뒤 반격이 나간다 (BattleManager.RunPulledTurns).
    // 클래스 이름은 에셋에 저장된 효과 블록을 지키려고 그대로 둔다 ([SerializeReference]는 클래스 이름으로 저장).
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("턴·행동/상대 턴 끌어오기", "상대 전원의 다음 턴을 지금 턴 직후로 끌어온다 (실제 턴, 단일 공격은 시전자를 노림). 뒤에 추가 공격 예약을 두면 끌어온 턴이 끝난 뒤 반격", 430)]
    [Serializable]
    public class ForceOpponentsAttackEffect : SkillEffect
    {
        // 전장의 상대 유닛마다 "시전자를 노리는 끌어온 턴"을 예약한다.
        public override void Apply(SkillContext context)
        {
            BattleUnit caster = context.Caster;

            foreach (BattleUnit unit in context.Services.Units)
            {
                bool isOpponentOnField = unit.IsAlly != caster.IsAlly && unit.IsOnField;
                if (isOpponentOnField)
                {
                    context.Actions.EnqueuePulledTurn(unit, caster);
                }
            }

            context.Log($"  {caster.Name}: 상대 전원의 턴을 끌어옴");
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return "상대 전원의 다음 턴을 끌어와 바로 행동시킴 (단일 공격은 자신을 노림)";
        }
    }
}
