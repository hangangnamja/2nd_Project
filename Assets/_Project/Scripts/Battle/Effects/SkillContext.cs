using System;
using System.Collections.Generic;

namespace Game.Battle.Effects
{
    // 효과 블록이 실행될 때 필요한 정보 묶음 (누가, 누구에게, 로그를 어디로, 사건을 어디로 알릴지).
    // 효과 블록은 이 정보만 보고 동작하므로 BattleManager를 직접 알 필요가 없다.
    public class SkillContext
    {
        public BattleUnit Caster { get; }
        public IReadOnlyList<BattleUnit> Targets { get; }

        // Action<string>: 문자열 하나를 받는 함수를 담는 델리게이트(함수 참조).
        // 효과 블록이 Log("...")를 호출하면 BattleManager가 넘겨준 로그 함수가 실행된다.
        public Action<string> Log { get; }

        // 공격 판정 같은 사건을 알릴 통로. 패시브가 여기에 반응한다.
        public BattleEvents Events { get; }

        // 패시브 반응으로 실행된 효과인지. true이면 이 효과는 다시 "공격 판정" 사건을 일으키지 않는다.
        // 임시 연쇄 방지 장치이며, 추가 공격·큐가 들어올 때 "발생 원인" 정보로 교체한다 (Architecture.md 3장).
        public bool IsReaction { get; }

        // 시전자, 최종 대상 목록, 로그 함수, 사건 통로, 반응 여부를 받아 실행 정보를 만든다.
        public SkillContext(BattleUnit caster, IReadOnlyList<BattleUnit> targets, Action<string> log, BattleEvents events, bool isReaction)
        {
            Caster = caster;
            Targets = targets;
            Log = log;
            Events = events;
            IsReaction = isReaction;
        }
    }
}
