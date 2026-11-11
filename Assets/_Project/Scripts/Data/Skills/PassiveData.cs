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
        [InspectorName("자신의 공격 적중 시")] OnAttackHit,

        // 다른 아군이 스킬로 적을 공격했을 때 (스킬 1회마다, 광역도 1번, 추가 공격은 제외). 효과 대상 = 그 공격에 맞은 적
        [InspectorName("다른 아군 스킬 공격 시")] OnAllyAttack,

        // 파티 오라: 이 캐릭터가 살아 있는 동안 자신을 포함한 아군 전원에게 "오라 스탯 보정"을 항상 적용한다.
        // 사건에 반응하지 않으므로 효과 목록·실행 방식은 쓰지 않는다. 쓰러지면 즉시 꺼진다.
        [InspectorName("오라 (상시)")] Aura,

        // 이 캐릭터가 새 버프를 얻었을 때 (버프 1개마다, 갱신은 제외). 효과 대상 = 자신
        [InspectorName("새 버프 획득 시")] OnBuffGained,

        // 이 캐릭터가 적을 처치했을 때 (사망 확정 시점에 마지막으로 피해를 준 유닛 기준). 효과 대상 = 자신
        [InspectorName("적 처치 시")] OnKill,

        // 이 캐릭터가 건 버프가 누구에게서든 지워졌을 때 (만료, 소모, 보호막 깨짐, 밀려남. 사망·강탈 제외). 효과 대상 = 자신
        [InspectorName("자신이 건 버프가 지워질 때")] OnOwnBuffRemoved,

        // 이 캐릭터가 치명적인 피해(남은 HP 이상)를 받기 직전. 발동하면 HP 1로 버틴다. 효과 대상 = 자신
        // 피해 결과를 바꾸는 즉시 개입형이므로 실행 방식은 Immediate로 둔다 (하이퍼캐리 버티기).
        [InspectorName("치명적인 피해를 받기 직전")] OnFatalDamage,

        // 아군의 공격(지속피해 제외)으로 적 HP가 기준(hpThreshold) 초과에서 이하로 내려갈 때 (적 1명마다 1회 쌓임).
        // 쌓인 횟수만큼 타수가 늘어나는 광역 추가 공격으로 실행한다 (돌파 반응 딜러, 기획서 4-3). 효과 = 추가 공격 1타의 효과
        [InspectorName("적 HP 기준선 돌파 시")] OnEnemyHpThreshold,

        // 이 캐릭터의 정규 턴이 끝날 때, 그 턴에 공격하지 않았으면 (공격이 아닌 스킬을 썼거나, 공연·기절로 스킵됨).
        // 효과 대상 = 자신 (아군 전체 회복처럼 대상을 넓히려면 효과 블록의 옵션을 쓴다). 아스트라 패시브
        [InspectorName("공격하지 않은 턴 종료 시")] OnTurnEndWithoutAttack,

        // 같은 편 아군(자신 포함)의 공격 판정이 "필요한 상태효과"가 걸린 적에게 들어갈 때마다 (광역이면 대상마다).
        // 추가 공격의 판정도 포함한다. 효과 대상 = 맞은 적. 니콜 패시브 "이자": 방어력 감소가 걸린 적이 맞을 때마다 스택 +1
        [InspectorName("상태효과 걸린 적이 아군에게 맞을 때")] OnAllyHitDebuffedEnemy,
    }

    // 턴당 횟수 제한을 어느 턴 기준으로 세는지.
    // 에셋에는 숫자 순서로 저장되므로 새 항목은 반드시 맨 뒤에 추가한다.
    public enum PassiveTurnLimitScope
    {
        // 누구의 턴이든 턴이 바뀔 때마다 다시 채운다.
        [InspectorName("매 턴")] AnyTurn,

        // 이 캐릭터의 턴이 시작될 때만 다시 채운다 → "자기 턴 사이에 N회".
        // 예: 곰인형 버퍼 패시브는 아군 4명의 턴마다 버프가 만료돼도, 자기 턴 사이에 1번만 쿨타임을 줄인다.
        [InspectorName("자기 턴 시작 시만")] OwnTurn,
    }

    // 패시브가 발동했을 때 효과를 언제 실행하는지.
    public enum PassiveExecution
    {
        // 사건이 일어난 그 자리에서 바로 실행 (공격 판정에 묻는 화상 등). 공격 행동이 아니다.
        [InspectorName("즉시")] Immediate,

        // 지금 행동이 끝난 뒤 추가 공격으로 실행. 공격 행동이므로 다른 캐릭터의 반응도 일으킨다.
        [InspectorName("추가 공격으로")] FollowUpAttack,

        // 지금 턴의 종료 단계(턴 종료 처리·게이지 리셋 이후)에 실행. 공격 행동이 아니다.
        // 예: 강탈 딜러 추격 → 같은 스킬의 "버프 전부 소모"에 휘말리지 않도록 턴이 끝난 뒤 얻는다.
        [InspectorName("턴 종료 단계에")] TurnEnd,
    }

    // 패시브 하나의 설정: "언제(트리거)" + "어떻게(실행 방식)" + "무엇을(효과 목록)" + "몇 번까지(제한)".
    // 스킬과 같은 효과 블록을 그대로 재사용하므로, 새 패시브도 Inspector에서 조립할 수 있다.
    [Serializable]
    public class PassiveData
    {
        [Label("이름")]
        [SerializeField] private string passiveName = "새 패시브";
        [Label("발동 조건")]
        [SerializeField] private PassiveTrigger trigger = PassiveTrigger.OnAttackHit;
        [Label("실행 시점")]
        [SerializeField] private PassiveExecution execution = PassiveExecution.Immediate;

        // 반응형 패시브는 반드시 횟수 제한(턴당 횟수 또는 스택)을 둬야 연쇄가 언제나 끝난다 (기획서 3-4).
        [Tooltip("한 턴(누구의 턴이든)에 발동할 수 있는 최대 횟수. 0이면 제한 없음")]
        [Label("턴당 최대 발동", Min = 0f)]
        [SerializeField] private int maxTriggersPerTurn;

        [Tooltip("턴당 횟수를 다시 채우는 기준. 매 턴 = 누구의 턴이든, 자기 턴 시작 시만 = 이 캐릭터의 턴이 시작될 때만")]
        [Label("횟수 초기화 기준")]
        [SerializeField] private PassiveTurnLimitScope turnLimitScope = PassiveTurnLimitScope.AnyTurn;

        [Tooltip("전투 한 판에 발동할 수 있는 최대 횟수. 0이면 제한 없음 (하이퍼캐리 버티기 = 1)")]
        [Label("전투당 최대 발동", Min = 0f)]
        [SerializeField] private int maxTriggersPerBattle;

        [Tooltip("체크하면 변신 중에는 발동하지 않는다 (하이퍼캐리 버티기: 변신 전에만)")]
        [Label("변신 중 꺼짐")]
        [SerializeField] private bool inactiveWhileTransformed;

        // 스택: 정해진 때에 쌓이고, 발동할 때 소모된다. 스택이 모자라면 발동하지 않는다.
        // 예: 격발 딜러 패시브 2 = 자기 행동마다 1개(최대 2개), 발동 시 1개 소모
        [Header("스택 (0이면 스택 없이 발동)")]
        [Tooltip("최대 스택 수. 0이면 스택을 쓰지 않는다")]
        [Label("최대 스택", Min = 0f)]
        [SerializeField] private int maxStacks;

        [Tooltip("이 캐릭터가 스킬로 행동할 때마다 얻는 스택 수 (추가 턴의 행동 포함)")]
        [Label("행동마다 얻는 스택", Min = 0f)]
        [SerializeField] private int stacksPerOwnAction;

        [Tooltip("발동 1회에 소모하는 스택 수")]
        [Label("발동마다 쓰는 스택", Min = 0f)]
        [SerializeField] private int stackCostPerTrigger = 1;

        // 발동할 때 스택을 쓰는 대신 얻는 패시브용 (니콜 이자: 맞을 때마다 쌓고, 2스가 한꺼번에 소모).
        [Tooltip("발동 1회에 얻는 스택 수 (최대 스택까지). 스택을 쌓기만 하는 패시브는 소모 스택을 0으로 둔다")]
        [Label("발동마다 얻는 스택", Min = 0f)]
        [SerializeField] private int stacksGainedPerTrigger;

        [Header("HP 돌파 (발동 조건이 '적 HP 기준선 돌파 시'일 때만)")]
        [Tooltip("적 HP가 최대 HP의 이 비율 초과에서 이하로 내려갈 때 1회 쌓인다 (0.5 = 50%)")]
        [Label("HP 기준선", Min = 0.01f, Max = 1f)]
        [SerializeField] private float hpThreshold = 0.5f;

        [Header("필요한 상태효과 (발동 조건이 '상태효과 걸린 적이 아군에게 맞을 때'일 때만)")]
        [Tooltip("맞은 적에게 이 상태효과가 걸려 있을 때만 발동한다 (같은 에셋인지로 판단)")]
        [Label("필요한 상태효과")]
        [SerializeField] private StatusEffectData requiredStatus;

        [Header("오라 (발동 조건이 '오라 (상시)'일 때만)")]
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
        public int StacksGainedPerTrigger => stacksGainedPerTrigger;
        public StatusEffectData RequiredStatus => requiredStatus;
        public float HpThreshold => hpThreshold;
        public IReadOnlyList<StatModifier> AuraModifiers => auraModifiers;
        public IReadOnlyList<SkillEffect> Effects => effects;
    }
}
