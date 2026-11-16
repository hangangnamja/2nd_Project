using System;
using Game.Core;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상에게 보호막 버프를 건다 (곰인형 버퍼 2스 등, 여러 캐릭터가 공용으로 쓰는 블록).
    // 보호막 양 = 기준 스탯 × 비율. 피해는 보호막이 먼저 흡수하고, 다 깎이거나 남은 턴이 끝나면 버프가 사라진다.
    // 보호막은 유닛당 하나: 이미 보호막이 있으면 양이 더해지고 남은 턴은 긴 쪽으로 맞춰진다 (아이콘도 하나).
    // 보호막은 버프 칸을 쓰는 일반 버프라서 강탈 딜러 3스의 소모 대상이기도 하다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("회복·보호/보호막", "보호막 부여. 양 = 기준 스탯 × 비율. 이미 보호막이 있으면 양이 더해진다. 피해를 먼저 흡수하고 다 깎이거나 만료되면 사라진다", 210)]
    [Serializable]
    public class ShieldEffect : SkillEffect
    {
        [Tooltip("보호막으로 쓸 버프 (지속 턴·아이콘은 이 상태효과 데이터를 따른다)")]
        [Label("보호막 효과")]
        [SerializeField] private StatusEffectData shieldStatus;

        // 보호막 양의 기준 스탯. 곰인형 버퍼는 최대 HP, 하이퍼캐리 버티기는 공격력 비례다.
        public enum ScaleStat
        {
            [InspectorName("시전자 최대 HP")] CasterMaxHp,
            [InspectorName("시전자 공격력")] CasterAttack,
        }

        [Tooltip("보호막 양의 기준 스탯")]
        [Label("기준 스탯")]
        [SerializeField] private ScaleStat scaleStat = ScaleStat.CasterMaxHp;

        // 기존 에셋에 저장된 이름(maxHpRatio)을 유지한다. 기준 스탯을 고를 수 있게 되어 이름만 의미상 "비율"이다.
        [Tooltip("기준 스탯 대비 보호막 양 (0.25 = 25%)")]
        [Label("비율", Min = 0f)]
        [SerializeField] private float maxHpRatio = 0.25f;

        // 대상마다 보호막 버프를 걸고 양을 정한다.
        public override void Apply(SkillContext context)
        {
            if (shieldStatus == null)
            {
                context.Log("  [오류] 보호막 상태효과가 지정되지 않았습니다.");
                return;
            }

            // (int): 소수점 아래를 버려 정수 보호막 양으로 만든다. 예: 최대 HP 1000 × 0.25 = 250
            int baseValue = scaleStat == ScaleStat.CasterAttack ? context.Caster.Attack : context.Caster.MaxHp;
            int amount = (int)(baseValue * maxHpRatio);

            foreach (BattleUnit target in context.Targets)
            {
                // 보호막은 유닛당 하나: 이미 보호막이 있으면(다른 스킬·다른 캐릭터가 건 것이라도) 그 양에 더해진다.
                StatusInstance shield = StatusApplier.GrantShield(target, shieldStatus, amount, context.Caster, context.Services);
                context.Log($"  {target.Name}에게 {shieldStatus.DisplayName} +{amount} (합계 {shield.ShieldAmount})");
            }
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return $"보호막 ({EffectText.EnumName(scaleStat)} {EffectText.Percent(maxHpRatio)}{(shieldStatus != null && !shieldStatus.IsPermanent ? $", {shieldStatus.Duration}턴" : string.Empty)})";
        }
    }
}
