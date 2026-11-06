using System;
using System.Collections.Generic;

namespace Game.Battle.Effects
{
    // 효과 블록이 실행될 때 필요한 정보 묶음 (누가, 누구에게, 로그를 어디로 보낼지).
    // 효과 블록은 이 정보만 보고 동작하므로 BattleManager를 직접 알 필요가 없다.
    public class SkillContext
    {
        public BattleUnit Caster { get; }
        public IReadOnlyList<BattleUnit> Targets { get; }

        // Action<string>: 문자열 하나를 받는 함수를 담는 델리게이트(함수 참조).
        // 효과 블록이 Log("...")를 호출하면 BattleManager가 넘겨준 로그 함수가 실행된다.
        public Action<string> Log { get; }

        // 시전자, 최종 대상 목록, 로그 출력 함수를 받아 실행 정보를 만든다.
        public SkillContext(BattleUnit caster, IReadOnlyList<BattleUnit> targets, Action<string> log)
        {
            Caster = caster;
            Targets = targets;
            Log = log;
        }
    }
}
