using System;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상에게 보호막 버프를 건다 (곰인형 버퍼 2스 등, 여러 캐릭터가 공용으로 쓰는 블록).
    // 보호막 양 = 시전자 최대 HP × 비율. 피해는 보호막이 먼저 흡수하고, 다 깎이거나 남은 턴이 끝나면 버프가 사라진다.
    // 보호막은 버프 칸을 쓰는 일반 버프라서 강탈 딜러 3스의 소모 대상이기도 하다.
    [Serializable]
    public class ShieldEffect : SkillEffect
    {
        [Tooltip("보호막으로 쓸 버프 (지속 턴·아이콘은 이 상태효과 데이터를 따른다)")]
        [SerializeField] private StatusEffectData shieldStatus;

        [Tooltip("시전자 최대 HP 대비 보호막 양 (0.25 = 25%)")]
        [SerializeField, Min(0f)] private float maxHpRatio = 0.25f;

        // 대상마다 보호막 버프를 걸고 양을 정한다.
        public override void Apply(SkillContext context)
        {
            if (shieldStatus == null)
            {
                context.Log("  [오류] 보호막 상태효과가 지정되지 않았습니다.");
                return;
            }

            // (int): 소수점 아래를 버려 정수 보호막 양으로 만든다. 예: 최대 HP 1000 × 0.25 = 250
            int amount = (int)(context.Caster.MaxHp * maxHpRatio);

            foreach (BattleUnit target in context.Targets)
            {
                StatusAddResult result = StatusApplier.Apply(target, shieldStatus, context.Caster, context.Services);

                // 이미 같은 보호막이 있으면 갱신된 그 인스턴스에, 남은 양과 새 양 중 큰 값을 남긴다.
                result.Instance.SetShieldAtLeast(amount);
                context.Log($"  {target.Name}에게 {shieldStatus.DisplayName} {result.Instance.ShieldAmount}");
            }
        }
    }
}
