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

        // 다른 아군이 스킬로 적을 공격했을 때 (스킬 1회마다, 광역도 1번, 추가 공격은 제외). 효과 대상 = 그 공격에 맞은 적
        OnAllyAttack,

        // 파티 오라: 이 캐릭터가 살아 있는 동안 자신을 포함한 아군 전원에게 "오라 스탯 보정"을 항상 적용한다.
        // 사건에 반응하지 않으므로 효과 목록·실행 방식은 쓰지 않는다. 쓰러지면 즉시 꺼진다.
        Aura,

        // 이 캐릭터가 새 버프를 얻었을 때 (버프 1개마다, 갱신은 제외). 효과 대상 = 자신
        OnBuffGained,

        // 이 캐릭터가 적을 처치했을 때 (사망 확정 시점에 마지막으로 피해를 준 유닛 기준). 효과 대상 = 자신
        OnKill,

        // 이 캐릭터가 건 버프가 누구에게서든 지워졌을 때 (만료, 소모, 보호막 깨짐, 밀려남. 사망·강탈 제외). 효과 대상 = 자신
        OnOwnBuffRemoved,

        // 이 캐릭터가 치명적인 피해(남은 HP 이상)를 받기 직전. 발동하면 HP 1로 버틴다. 효과 대상 = 자신
        // 피해 결과를 바꾸는 즉시 개입형이므로 실행 방식은 Immediate로 둔다 (하이퍼캐리 버티기).
        OnFatalDamage,
    }

    // 턴당 횟수 제한을 어느 턴 기준으로 세는지.
    // 에셋에는 숫자 순서로 저장되므로 새 항목은 반드시 맨 뒤에 추가한다.
    public enum PassiveTurnLimitScope
    {
        // 누구의 턴이든 턴이 바뀔 때마다 다시 채운다.
        AnyTurn,

        // 이 캐릭터의 턴이 시작될 때만 다시 채운다 → "자기 턴 사이에 N회".
        // 예: 곰인형 버퍼 패시브는 아군 4명의 턴마다 버프가 만료돼도, 자기 턴 사이에 1번만 쿨타임을 줄인다.
        OwnTurn,
    }

    // 패시브가 발동했을 때 효과를 언제 실행하는지.
    public enum PassiveExecution
    {
        // 사건이 일어난 그 자리에서 바로 실행 (공격 판정에 묻는 화상 등). 공격 행동이 아니다.
        Immediate,

        // 지금 행동이 끝난 뒤 추가 공격으로 실행. 공격 행동이므로 다른 캐릭터의 반응도 일으킨다.
        FollowUpAttack,

        // 지금 턴의 종료 단계(턴 종료 처리·게이지 리셋 이후)에 실행. 공격 행동이 아니다.
        // 예: 강탈 딜러 추격 → 같은 스킬의 "버프 전부 소모"에 휘말리지 않도록 턴이 끝난 뒤 얻는다.
        TurnEnd,
    }

    // 패시브 하나의 설정: "언제(트리거)" + "어떻게(실행 방식)" + "무엇을(효과 목록)" + "몇 번까지(제한)".
    // 스킬과 같은 효과 블록을 그대로 재사용하므로, 새 패시브도 Inspector에서 조립할 수 있다.
    [Serializable]
    public class PassiveData
    {
        [SerializeField] private string passiveName = "새 패시브";
        [SerializeField] private PassiveTrigger trigger = PassiveTrigger.OnAttackHit;
        [SerializeField] private PassiveExecution execution = PassiveExecution.Immediate;

        // 반응형 패시브는 반드시 횟수 제한(턴당 횟수 또는 스택)을 둬야 연쇄가 언제나 끝난다 (기획서 3-4).
        [Tooltip("한 턴(누구의 턴이든)에 발동할 수 있는 최대 횟수. 0이면 제한 없음")]
        [SerializeField, Min(0)] private int maxTriggersPerTurn;

        [Tooltip("턴당 횟수를 다시 채우는 기준. AnyTurn = 매 턴, OwnTurn = 이 캐릭터의 턴이 시작될 때만")]
        [SerializeField] private PassiveTurnLimitScope turnLimitScope = PassiveTurnLimitScope.AnyTurn;

        [Tooltip("전투 한 판에 발동할 수 있는 최대 횟수. 0이면 제한 없음 (하이퍼캐리 버티기 = 1)")]
        [SerializeField, Min(0)] private int maxTriggersPerBattle;

        [Tooltip("체크하면 변신 중에는 발동하지 않는다 (하이퍼캐리 버티기: 변신 전에만)")]
        [SerializeField] private bool inactiveWhileTransformed;

        // 스택: 정해진 때에 쌓이고, 발동할 때 소모된다. 스택이 모자라면 발동하지 않는다.
        // 예: 격발 딜러 패시브 2 = 자기 행동마다 1개(최대 2개), 발동 시 1개 소모
        [Header("스택 (0이면 스택 없이 발동)")]
        [Tooltip("최대 스택 수. 0이면 스택을 쓰지 않는다")]
        [SerializeField, Min(0)] private int maxStacks;

        [Tooltip("이 캐릭터가 스킬로 행동할 때마다 얻는 스택 수 (추가 턴의 행동 포함)")]
        [SerializeField, Min(0)] private int stacksPerOwnAction;

        [Tooltip("발동 1회에 소모하는 스택 수")]
        [SerializeField, Min(0)] private int stackCostPerTrigger = 1;

        [Header("오라 (트리거가 Aura일 때만)")]
        [Tooltip("아군 전원(자신 포함)에게 적용할 스탯 보정. 예: 지속피해 증가 +20%")]
        [SerializeField] private List<StatModifier> auraModifiers = new List<StatModifier>();

        // [SerializeReference] + [SubclassSelector]: 스킬 효과 목록과 같은 방식으로 효과 종류를 골라 넣는다.
        [SerializeReference, SubclassSelector]
        private List<SkillEffect> effects = new List<SkillEffect>();

        public string PassiveName => passiveName;
        public PassiveTrigger Trigger => trigger;
        public PassiveExecution Execution => execution;
        public int MaxTriggersPerTurn => maxTriggersPerTurn;
        public PassiveTurnLimitScope TurnLimitScope => turnLimitScope;
        public int MaxTriggersPerBattle => maxTriggersPerBattle;
        public bool InactiveWhileTransformed => inactiveWhileTransformed;
        public bool UsesStacks => maxStacks > 0;
        public int MaxStacks => maxStacks;
        public int StacksPerOwnAction => stacksPerOwnAction;
        public int StackCostPerTrigger => stackCostPerTrigger;
        public IReadOnlyList<StatModifier> AuraModifiers => auraModifiers;
        public IReadOnlyList<SkillEffect> Effects => effects;
    }
}
