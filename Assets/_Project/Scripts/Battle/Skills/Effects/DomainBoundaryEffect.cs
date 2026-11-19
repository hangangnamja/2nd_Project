using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 경계: 영역 (흉신 마라 3스 "영역: 혈해신전", 기획서 4-3).
    //  - 펼치는 즉시 블록 안에 정한 "영역 공격"(광역 300% + 출혈 2개)을 적 전체에게 쓴다
    //  - 정해진 턴 동안, 시전자의 턴이 끝날 때마다 영역 공격을 적 전체에게 추가 공격으로 쓴다 (펼친 턴도 1턴으로 셈)
    //  - 영역 동안 1회, 지정 칸(3스)이 따로 정한 스킬(업화)로 바뀐다. 쓰면 원래 스킬로 돌아오고, 영역은 남은 턴만큼 계속된다
    // 경계 공통 규칙(한 번에 하나, 같은 편 경계가 있으면 경계 스킬 사용 불가)은 BoundaryEffect·BoundaryField가 지킨다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("변신·전장/경계: 영역", "영역을 펼친다: 펼칠 때·자기 턴이 끝날 때마다 영역 공격(블록 안 효과 목록)을 적 전체에 사용, 영역 동안 1회 지정 칸이 다른 스킬로 바뀜. 정한 턴이 지나거나 쓰러지면 끝", 525)]
    [Serializable]
    public class DomainBoundaryEffect : BoundaryEffect
    {
        [Label("경계 이름")]
        [SerializeField] private string boundaryName = "혈해신전";

        [Tooltip("영역이 이어지는 시전자의 턴 수. 펼친 턴의 끝도 1턴으로 센다 (3이면 턴 종료 공격 3번)")]
        [Label("지속 턴", Min = 1f)]
        [SerializeField] private int durationTurns = 3;

        [Tooltip("펼칠 때와 턴이 끝날 때마다 적 전체에게 쓰는 영역 공격의 이름 (로그·연출 표시)")]
        [Label("영역 공격 이름")]
        [SerializeField] private string strikeName = "참";

        [Tooltip("체크하면 펼치는 즉시 영역 공격을 적 전체에게 한 번 쓴다")]
        [Label("펼칠 때 공격")]
        [SerializeField] private bool strikeOnDeploy = true;

        // 영역 공격의 효과 목록 (적 전체 대상). 스킬 효과 목록과 같은 방식으로 종류를 골라 넣는다. 예: 피해 300% + 출혈 2개
        [SerializeReference, SubclassSelector]
        private List<SkillEffect> strikeEffects = new List<SkillEffect>();

        [Tooltip("영역 공격의 전용 연출 (SkillFx 프리팹, 비우면 기본 돌진). 펼칠 때·턴 종료 때 모두 이 연출")]
        [Label("영역 공격 연출")]
        [SerializeField] private GameObject strikePresentationPrefab;

        [Tooltip("영역이 펼쳐진 동안 전장에 띄울 배경 (DomainScenery 프리팹: 사찰·물바닥·하늘). 비우면 배경 없음")]
        [Label("영역 배경")]
        [SerializeField] private GameObject sceneryPrefab;

        [Tooltip("영역이 끝날 때 재생할 연출 (SkillFx 프리팹: 사찰이 흐려지며 사라짐). 마지막 영역 공격 다음에 나온다. 비우면 배경이 바로 가라앉음")]
        [Label("영역 해제 연출")]
        [SerializeField] private GameObject closePresentationPrefab;

        [Tooltip("영역 동안 1회 다른 스킬로 바뀌는 칸 (3 = 3스)")]
        [Label("바뀌는 스킬 번호", Min = 1f, Max = 3f)]
        [SerializeField] private int overrideSkillNumber = 3;

        [Tooltip("영역 동안 1회 쓸 수 있는 스킬 (쿨타임 없음, 쓰면 원래 스킬로 돌아옴)")]
        [SerializeField] private SkillData overrideSkill = new SkillData();

        [Tooltip("머리 위에 자기 버프처럼 보여줄 표시용 효과 (아이콘·툴팁, 숫자는 남은 턴)")]
        [Label("표시용 효과")]
        [SerializeField] private StatusEffectData displayStatus;

        // 영역을 펼친 뒤(base.Apply), 설정했으면 그 자리에서 영역 공격 스킬을 적 전체에게 쓴다.
        public override void Apply(SkillContext context)
        {
            base.Apply(context);
            if (strikeOnDeploy)
            {
                StrikeAllNow(context);
            }
        }

        protected override Boundary CreateBoundary(BattleUnit caster)
        {
            return new DomainBoundary(caster, boundaryName, durationTurns, strikeName, strikeEffects, overrideSkillNumber - 1, overrideSkill, displayStatus,
                strikePresentationPrefab, sceneryPrefab, closePresentationPrefab);
        }

        // 영역 공격을 지금 행동 안에서 적 전체에게 실행한다 (3스 안에서 바로 터지는 참).
        // ResolveTargets(적 전체): 지금 전장에 서 있는 상대 전원. 칠 상대가 없으면 건너뛴다.
        // ExecuteEffects를 따로 부르므로 연출에서는 3스 시전 뒤 참이 별도 행동으로 이어서 보인다.
        private void StrikeAllNow(SkillContext context)
        {
            List<BattleUnit> targets = context.Services.ResolveTargets(context.Caster, SkillTargetType.EnemyAll, null);
            if (targets.Count == 0)
            {
                return;
            }

            context.Log($"  {boundaryName}: {strikeName} (적 전체)");
            SkillExecutor.ExecuteEffects(context.Caster, strikeEffects, targets, null, context.Services, context.Source,
                skillName: strikeName, presentationPrefab: strikePresentationPrefab);
        }

        // 상세 창 스킬 탭 설명: 영역 이름·턴 + 영역 공격(효과 설명) + 바뀌는 스킬과 그 효과.
        // ConvertAll(람다): 효과마다 설명 글로 바꾼 목록 → ", "로 잇는다.
        public override string Describe(SkillTargetType skillTarget)
        {
            string strikeText = string.Join(", ", strikeEffects.ConvertAll(effect => effect.DescribeOrName(SkillTargetType.EnemyAll)));
            string strike = $"{(strikeOnDeploy ? "펼칠 때와 " : string.Empty)}자기 턴이 끝날 때마다 「{strikeName}」({strikeText})";
            var overrideEffects = new List<SkillEffect>(overrideSkill.Effects);
            string overrideText = string.Join(", ", overrideEffects.ConvertAll(effect => effect.DescribeOrName(overrideSkill.TargetType)));
            return $"경계: {boundaryName} 전개 ({durationTurns}턴) · {strike} · 영역 동안 1회 {overrideSkillNumber}스가 「{overrideSkill.SkillName}」({overrideText})로 바뀜";
        }
    }

    // 영역의 전투 중 상태: 남은 턴, 바뀐 스킬을 아직 쓸 수 있는지.
    public class DomainBoundary : Boundary
    {
        private readonly string strikeName;
        private readonly IReadOnlyList<SkillEffect> strikeEffects;
        private readonly int overrideSlot;
        private readonly SkillData overrideSkill;
        private readonly StatusEffectData displayStatus;

        private int remainingTurns;
        private bool overrideAvailable = true;

        // 마지막 영역 공격(남은 턴이 0이 된 턴의 공격)이 끝났는지. 끝나야 영역이 닫힌다 → 공격 → 피해 → 영역 해제 순서.
        private bool finalStrikeDone;

        // 영역 공격을 예약할 때 받아 둔 전투 공용 도구 (영역이 끝날 때 해제 연출 사건을 알리는 데 씀). 턴 종료 전이면 null.
        private BattleServices services;

        // 영역 공격의 전용 연출 프리팹 (없으면 null) · 영역이 펼쳐진 동안 띄울 배경 프리팹 (DomainSceneryView가 읽음, 없으면 null).
        public GameObject StrikePresentationPrefab { get; }
        public GameObject SceneryPrefab { get; }

        // 영역이 끝날 때 재생할 연출 프리팹 (없으면 null → DomainSceneryView가 배경을 바로 가라앉힘).
        public GameObject ClosePresentationPrefab { get; }

        public DomainBoundary(BattleUnit owner, string name, int durationTurns, string strikeName, IReadOnlyList<SkillEffect> strikeEffects,
            int overrideSlot, SkillData overrideSkill, StatusEffectData displayStatus, GameObject strikePresentationPrefab, GameObject sceneryPrefab,
            GameObject closePresentationPrefab)
            : base(owner, name)
        {
            ClosePresentationPrefab = closePresentationPrefab;
            StrikePresentationPrefab = strikePresentationPrefab;
            SceneryPrefab = sceneryPrefab;
            remainingTurns = durationTurns;
            this.strikeName = strikeName;
            this.strikeEffects = strikeEffects;
            this.overrideSlot = overrideSlot;
            this.overrideSkill = overrideSkill;
            this.displayStatus = displayStatus;
        }

        public override StatusEffectData DisplayStatus => displayStatus;
        public override int RemainingTurns => remainingTurns;

        // 아직 쓰지 않았으면 지정 칸을 바꾼 스킬로 돌려준다.
        public override SkillData GetSkillOverride(int slot)
        {
            return overrideAvailable && slot == overrideSlot ? overrideSkill : null;
        }

        // 바꾼 스킬은 영역 동안 1번만: 쓰면 원래 스킬(영역)로 돌아온다. 영역은 남은 턴만큼 계속된다.
        public override void OnSkillOverrideUsed(int slot)
        {
            if (slot == overrideSlot)
            {
                overrideAvailable = false;
            }
        }

        // 주인의 턴이 끝날 때: 영역 공격을 적 전체에게 추가 공격으로 예약하고 남은 턴을 1 줄인다.
        // 추가 공격(임시 턴)이라 이 턴의 정규 종료 처리가 끝난 뒤 나가고, 실행할 때 서 있는 적 전원을 다시 고른다.
        // 남은 턴이 0이 되는 턴의 공격은 "마지막 공격"이다: 그 공격이 끝난 뒤(Completed)에 영역을 닫는다
        // → 턴 종료 시점에 바로 닫지 않아, 화면에서도 공격 → 피해 → 영역 해제 순서로 보인다.
        // 람다 () => {...}: 추가 공격이 끝날 때 BattleManager가 부르는 함수. 표시해 두고 끝날 조건을 다시 확인한다.
        public override void OnOwnerTurnEnd(BattleServices services)
        {
            this.services = services;
            var strike = new FollowUpAttack(
                Owner,
                $"{Name}: {strikeName}",
                strikeEffects,
                SkillTargetType.EnemyAll,
                preferredTarget: null,
                presentationPrefab: StrikePresentationPrefab);

            remainingTurns--;
            if (remainingTurns <= 0)
            {
                strike.Completed = () =>
                {
                    finalStrikeDone = true;
                    services.Boundaries.CheckEnd();
                };
            }

            services.Actions.EnqueueFollowUp(strike);
            services.Log($"  [경계] {Name}: {strikeName} 예약 (남은 {remainingTurns}턴)");
        }

        // 주인이 쓰러지면 바로, 아니면 남은 턴을 다 쓰고 마지막 공격까지 끝나면 끝난다.
        public override bool ShouldEnd()
        {
            return Owner.IsDead || (remainingTurns <= 0 && finalStrikeDone);
        }

        // 영역이 끝날 때: 주인이 살아 있으면 해제 연출을 하나의 행동처럼 알린다 → 연출 재생 순서상 마지막 공격의 피해 다음에 나온다.
        // 대상은 없고(빈 목록), 연출만 재생한다. 주인이 쓰러져 끝난 경우는 배경이 바로 가라앉는다 (DomainSceneryView).
        protected override void OnEnd(Action<string> log)
        {
            if (services == null || Owner.IsDead || ClosePresentationPrefab == null)
            {
                return;
            }

            services.Events.RaiseActionStarted(new ActionStartInfo(
                Owner, Array.Empty<BattleUnit>(), null, ActionSource.PassiveEffect, -1, $"{Name} 해제",
                presentationPrefab: ClosePresentationPrefab));
        }
    }
}
