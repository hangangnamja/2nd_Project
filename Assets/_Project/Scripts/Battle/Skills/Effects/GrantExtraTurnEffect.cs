using System;

namespace Game.Battle.Effects
{
    // 시전자에게 추가 턴을 준다. 추가 턴은 이번 턴이 완전히 끝난 뒤(추가 공격·턴 종료 처리까지) 바로 이어진다.
    // 같은 턴에 추가 턴을 여러 번 얻어도 1회만 받는다 (기획서 4-3).
    [Serializable]
    public class GrantExtraTurnEffect : SkillEffect
    {
        // 대기열에 추가 턴을 예약한다. 이미 예약되어 있으면 겹치지 않았다고 로그로 알린다.
        public override void Apply(SkillContext context)
        {
            bool granted = context.Actions.GrantExtraTurn(context.Caster);

            context.Log(granted
                ? $"  {context.Caster.Name}: 추가 턴 획득"
                : $"  {context.Caster.Name}: 이미 추가 턴이 예정되어 있어 1회로 처리");
        }
    }
}
