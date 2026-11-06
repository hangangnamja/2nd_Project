using System;
using Game.Data;

namespace Game.Battle
{
    // 전투 한 판 동안 공유하는 도구 묶음 (로그, 사건 통로, 행동 대기열, 난수, 전투 규칙).
    // 효과 블록·패시브·스킬 실행이 모두 이것 하나만 받으면 되어, 함수 매개변수가 길어지지 않는다.
    public class BattleServices
    {
        // Action<string>: 문자열 하나를 받는 함수를 담는 델리게이트. 호출하면 BattleManager의 로그 함수가 실행된다.
        public Action<string> Log { get; }
        public BattleEvents Events { get; }
        public ActionQueue Actions { get; }

        // 치명타·무작위 대상 선택에 쓰는 난수. 시드를 고정하면 같은 전투를 똑같이 재현할 수 있다.
        public Random Random { get; }

        public BattleRulesData Rules { get; }

        // 전투 시작 시 BattleManager가 한 번 만든다.
        public BattleServices(Action<string> log, BattleEvents events, ActionQueue actions, Random random, BattleRulesData rules)
        {
            Log = log;
            Events = events;
            Actions = actions;
            Random = random;
            Rules = rules;
        }
    }
}
