using System;
using UnityEngine;

namespace Game.Data
{
    // 적 패턴이 발동하는 조건. 에셋에는 숫자 순서로 저장되므로 새 항목은 반드시 맨 뒤에 추가한다.
    public enum PatternCondition
    {
        // 항상 (목록 맨 끝의 기본 행동으로 쓴다)
        Always,

        // 자신에게 걸린 버프가 기준 개수 이상일 때. 예: 축적의 수호자 = 버프 4개 이상이면 필살기
        SelfBuffCountAtLeast,
    }

    // 패턴 스킬의 대상을 고르는 방법. 새 항목은 맨 뒤에 추가한다.
    public enum PatternTarget
    {
        // 스킬의 대상 규칙대로 (적 단일이면 무작위)
        Default,

        // 같은 편 중 최대 HP가 가장 높은 유닛 = 보스. 버프몹이 보스에게만 버프를 걸 때 쓴다.
        StrongestAlly,
    }

    // 적 패턴 하나: "조건이 맞으면 몇 번 스킬을 누구에게 쓴다".
    // 패턴 목록을 위에서부터 확인해, 조건이 맞고 쿨타임이 끝난 첫 패턴을 실행한다 (우선순위 = 목록 순서).
    [Serializable]
    public class EnemyPattern
    {
        [SerializeField] private string patternName = "새 패턴";
        [SerializeField] private PatternCondition condition = PatternCondition.Always;

        [Tooltip("조건의 기준 값 (버프 개수 등). Always에서는 쓰지 않는다")]
        [SerializeField, Min(0)] private int threshold;

        [Tooltip("사용할 스킬 번호 (1 = 1스, 2 = 2스, 3 = 3스)")]
        [SerializeField, Range(1, 3)] private int skillNumber = 1;

        [SerializeField] private PatternTarget target = PatternTarget.Default;

        public string PatternName => patternName;
        public PatternCondition Condition => condition;
        public int Threshold => threshold;

        // 스킬 슬롯 번호 (0 = 1스). 스킬 번호에서 1을 뺀 값이다.
        public int SkillSlot => skillNumber - 1;
        public PatternTarget Target => target;
    }
}
