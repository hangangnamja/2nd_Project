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

        // 전투 유닛 전원 (아군 + 적). 반격 태세처럼 "상대 전원에게 행동을 시키는" 효과가 대상 목록 밖의 유닛을 찾을 때 쓴다.
        public System.Collections.Generic.IReadOnlyList<BattleUnit> Units { get; }

        // 전장의 경계 자리 (기획서 3-12). 경계 블록이 여기에 경계를 펼치고, 한 번에 하나만 유지된다.
        public BoundaryField Boundaries { get; }

        // 대상 규칙으로 실제 대상 목록을 만드는 함수 (BattleManager.ResolveTargets).
        // Func<시전자, 대상 규칙, 고른 대상, 결과 목록>: 세 값을 받아 대상 목록을 돌려주는 함수를 담는 델리게이트.
        // "다른 스킬 사용" 블록처럼 효과 안에서 다른 스킬의 대상을 다시 정해야 할 때 쓴다 (대상 결정 규칙을 한 곳에 두기 위해 함수를 넘겨받음).
        public Func<BattleUnit, SkillTargetType, BattleUnit, System.Collections.Generic.List<BattleUnit>> ResolveTargets { get; }

        // 부하를 소환하는 함수 (BattleManager.SummonUnit). Func<소환자, 부하 데이터, 만든 유닛>.
        // 전투 유닛 목록·화면 갱신은 BattleManager가 맡으므로 소환 블록은 이 함수만 부른다. 생성자 뒤에 BattleManager가 연결한다.
        public Func<BattleUnit, CharacterData, BattleUnit> Summon { get; set; }

        // 전투 시작 시 BattleManager가 한 번 만든다.
        // units는 BattleManager의 유닛 목록을 그대로 참조하므로, 유닛을 나중에 채워 넣어도 최신 목록을 본다.
        public BattleServices(
            Action<string> log,
            BattleEvents events,
            ActionQueue actions,
            Random random,
            BattleRulesData rules,
            System.Collections.Generic.IReadOnlyList<BattleUnit> units,
            Func<BattleUnit, SkillTargetType, BattleUnit, System.Collections.Generic.List<BattleUnit>> resolveTargets)
        {
            Log = log;
            Events = events;
            Actions = actions;
            Random = random;
            Rules = rules;
            Units = units;
            ResolveTargets = resolveTargets;
            Boundaries = new BoundaryField(log);
        }
    }
}
