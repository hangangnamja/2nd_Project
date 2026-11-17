using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자의 추가 공격(임시 턴) 하나를 대기열 끝에 예약한다. 효과 목록은 이 블록 안에 따로 조립한다.
    // 대기열은 먼저 넣은 것부터 실행되므로, 앞에서 예약된 추가 공격(상대의 강제 공격 등)이 모두 끝난 뒤에 나간다.
    // 예: 하이퍼캐리 반격 태세 = 상대 전원 공격 예약 → 이 블록으로 반격 예약 → 상대 공격이 다 끝나면 반격
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("턴·행동/추가 공격 예약", "블록 안에 담은 효과 목록을 시전자의 추가 공격(임시 턴)으로 대기열 끝에 예약한다", 420)]
    [Serializable]
    public class QueueFollowUpEffect : SkillEffect
    {
        [Label("추가 공격 이름")]
        [SerializeField] private string followUpName = "반격";
        [Label("대상")]
        [SerializeField] private SkillTargetType targetType = SkillTargetType.EnemyAll;

        // 추가 공격이 나갈 때 기본 돌진 대신 재생할 전용 연출 (SkillFx가 붙은 프리팹, 비우면 기본 돌진).
        [Label("전용 연출")]
        [SerializeField] private GameObject presentationPrefab;

        // 블록 안에 효과 목록을 한 번 더 담는다. 스킬 효과 목록과 같은 방식으로 종류를 골라 넣는다.
        [SerializeReference, SubclassSelector]
        private List<SkillEffect> effects = new List<SkillEffect>();

        // 추가 공격을 예약한다. 대상은 실행 시점에 대상 규칙으로 다시 정한다 (그 사이 쓰러진 적은 빠진다).
        public override void Apply(SkillContext context)
        {
            context.Actions.EnqueueFollowUp(new FollowUpAttack(
                context.Caster,
                followUpName,
                effects,
                targetType,
                preferredTarget: null,
                presentationPrefab: presentationPrefab));

            context.Log($"  {context.Caster.Name}: {followUpName} 예약");
        }

        // 상세 창 스킬 탭 설명. 블록 안 효과들의 설명을 이어 붙인다 (안쪽 블록은 추가 공격 자신의 대상 규칙으로 설명).
        // ConvertAll(람다): 안쪽 효과마다 설명 글로 바꾼 목록을 만든다.
        public override string Describe(SkillTargetType skillTarget)
        {
            string inner = string.Join(", ", effects.ConvertAll(effect => effect.DescribeOrName(targetType)));
            return $"추가 공격 「{followUpName}」({EffectText.EnumName(targetType)}): {inner}";
        }
    }
}
