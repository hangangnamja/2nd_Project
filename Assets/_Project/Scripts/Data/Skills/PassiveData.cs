using System;
using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 패시브가 반응하는 전투 사건 (트리거). 필요해질 때마다 항목을 추가한다.
    public enum PassiveTrigger
    {
        // 이 캐릭터의 공격이 대상에게 적중했을 때 (공격 판정 1회마다). 효과 대상 = 맞은 대상
        OnAttackHit,

        // 다른 아군이 적을 공격했을 때 (공격 행동 1회마다, 광역도 1번). 효과 대상 = 그 공격에 맞은 적
        OnAllyAttack,
    }

    // 패시브가 발동했을 때 효과를 언제 실행하는지.
    public enum PassiveExecution
    {
        // 사건이 일어난 그 자리에서 바로 실행 (공격 판정에 묻는 화상 등). 공격 행동이 아니다.
        Immediate,

        // 지금 행동이 끝난 뒤 추가 공격으로 실행. 공격 행동이므로 다른 캐릭터의 반응도 일으킨다.
        FollowUpAttack,
    }

    // 패시브 하나의 설정: "언제(트리거)" + "어떻게(실행 방식)" + "무엇을(효과 목록)" + "몇 번까지(제한)".
    // 스킬과 같은 효과 블록을 그대로 재사용하므로, 새 패시브도 Inspector에서 조립할 수 있다.
    [Serializable]
    public class PassiveData
    {
        [SerializeField] private string passiveName = "새 패시브";
        [SerializeField] private PassiveTrigger trigger = PassiveTrigger.OnAttackHit;
        [SerializeField] private PassiveExecution execution = PassiveExecution.Immediate;

        // 반응형 패시브는 반드시 횟수 제한을 둬야 연쇄가 언제나 끝난다 (기획서 3-4).
        [Tooltip("한 턴(누구의 턴이든)에 발동할 수 있는 최대 횟수. 0이면 제한 없음")]
        [SerializeField, Min(0)] private int maxTriggersPerTurn;

        // [SerializeReference] + [SubclassSelector]: 스킬 효과 목록과 같은 방식으로 효과 종류를 골라 넣는다.
        [SerializeReference, SubclassSelector]
        private List<SkillEffect> effects = new List<SkillEffect>();

        public string PassiveName => passiveName;
        public PassiveTrigger Trigger => trigger;
        public PassiveExecution Execution => execution;
        public int MaxTriggersPerTurn => maxTriggersPerTurn;
        public IReadOnlyList<SkillEffect> Effects => effects;
    }
}
