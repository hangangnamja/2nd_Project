using System;
using Game.Core;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 경계 효과 블록의 공통 부분: 경계를 만들어 전장에 펼친다 (기획서 3-12).
    // 공통 규칙(한 번에 하나만)은 BoundaryField와 BattleUnit.CanUseSkill이 지키므로, 하위 블록은 "어떤 경계를 만들지"만 정한다.
    // abstract: 이 클래스는 직접 쓰지 않고, 경계 종류마다 하위 클래스를 만든다 (Inspector 드롭다운에는 하위 클래스만 나온다).
    [Serializable]
    public abstract class BoundaryEffect : SkillEffect
    {
        public override void Apply(SkillContext context)
        {
            context.Services.Boundaries.Deploy(CreateBoundary(context.Caster));
        }

        // 이 블록이 펼칠 경계를 만든다.
        protected abstract Boundary CreateBoundary(BattleUnit caster);
    }

    // 경계: 고립 (하이퍼캐리 3스). 펼친 유닛의 스탯이 크게 오르고 행동 제어(기절 등)에 면역이 된다.
    // 펼친 유닛의 변신이 끝나거나 펼친 유닛이 쓰러지면 끝난다. 아군 퇴장은 이 경계가 아니라 3스의 "아군 퇴장" 블록이 맡는다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("변신·전장/경계", "경계를 펼친다: 스탯 보정 + 행동 제어 면역. 펼친 유닛의 변신이 끝나거나 쓰러지면 끝 (경계는 한 번에 하나)", 520)]
    [Serializable]
    public class IsolationBoundaryEffect : BoundaryEffect
    {
        [Label("경계 이름")]
        [SerializeField] private string boundaryName = "홀로 싸우는 길";

        [Tooltip("펼친 유닛에게 주는 스탯 보정 (예: 공격력 +60%, 방어력 +30%, 최대 HP +200%)")]
        [SerializeField] private List<StatModifier> statModifiers = new List<StatModifier>();

        [Tooltip("체크하면 경계 동안 행동 제어(기절 등)에 면역")]
        [Label("행동 제어 면역")]
        [SerializeField] private bool crowdControlImmune = true;

        [Tooltip("체크하면 펼친 유닛의 변신이 끝날 때 경계도 끝난다 (하이퍼캐리: 종결기·강제 해제 후)")]
        [Label("변신 끝나면 종료")]
        [SerializeField] private bool endsWithOwnerTransformation = true;

        [Tooltip("머리 위에 자기 버프처럼 보여줄 표시용 효과 (아이콘·툴팁)")]
        [Label("표시용 효과")]
        [SerializeField] private StatusEffectData displayStatus;

        protected override Boundary CreateBoundary(BattleUnit caster)
        {
            return new IsolationBoundary(caster, boundaryName, statModifiers, crowdControlImmune, endsWithOwnerTransformation, displayStatus);
        }

        // 상세 창 스킬 탭 설명: 경계 이름 + 스탯 보정 + 면역·끝나는 조건.
        // ConvertAll(EffectText.Modifier): 스탯 보정마다 "공격력 +60%" 같은 글로 바꾼 목록.
        public override string Describe(SkillTargetType skillTarget)
        {
            var parts = statModifiers.ConvertAll(EffectText.Modifier);
            if (crowdControlImmune)
            {
                parts.Add("행동 제어 면역");
            }

            string end = endsWithOwnerTransformation ? " · 변신이 끝나면 종료" : string.Empty;
            return $"경계: {boundaryName} 전개 ({string.Join(", ", parts)}){end}";
        }
    }

    // 고립 경계의 전투 중 상태: 스탯 보정·면역은 Boundary 공통 처리로 주인에게 적용되고, 여기서는 값과 끝날 조건만 정한다.
    public class IsolationBoundary : Boundary
    {
        private readonly IReadOnlyList<StatModifier> statModifiers;
        private readonly bool crowdControlImmune;
        private readonly bool endsWithOwnerTransformation;
        private readonly StatusEffectData displayStatus;

        public IsolationBoundary(
            BattleUnit owner,
            string name,
            IReadOnlyList<StatModifier> statModifiers,
            bool crowdControlImmune,
            bool endsWithOwnerTransformation,
            StatusEffectData displayStatus)
            : base(owner, name)
        {
            this.statModifiers = statModifiers;
            this.crowdControlImmune = crowdControlImmune;
            this.endsWithOwnerTransformation = endsWithOwnerTransformation; 
            this.displayStatus = displayStatus;
        }

        public override IReadOnlyList<StatModifier> StatModifiers => statModifiers;
        public override bool GrantsCrowdControlImmunity => crowdControlImmune;
        public override StatusEffectData DisplayStatus => displayStatus;

        // 펼친 유닛이 쓰러졌거나, (설정 시) 변신이 끝났으면 끝난다.
        public override bool ShouldEnd()
        {
            return Owner.IsDead || (endsWithOwnerTransformation && !Owner.IsTransformed);
        }
    }
}
