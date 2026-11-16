using System;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자에게 피격 무효를 정해진 횟수만큼 채운다 (보스 2, 기획서 6장). "자기 턴 시작 시" 패시브에 넣어 쓴다.
    // 피격 무효 1회 = 피해 1번을 0으로 (지속피해 1개 발동도 1번). 그 타격의 다른 효과(화상 부여, 강탈 등)는 그대로 들어간다.
    // 버프 칸이 아니라 유닛이 직접 들고 있는 횟수라 강탈·소모·디버프 제거로 없앨 수 없다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("회복·보호/피격 무효 충전", "시전자의 피격 무효를 횟수만큼 채운다 (피해만 0, 효과는 들어감). 버프가 아니라 지울 수 없음", 215)]
    [Serializable]
    public class HitBlockEffect : SkillEffect
    {
        [Tooltip("채울 횟수. 이미 더 많이 남아 있으면 그대로 둔다 (쌓이지 않음)")]
        [Label("충전 횟수", Min = 1f)]
        [SerializeField] private int charges = 4;

        [Tooltip("남은 횟수와 함께 머리 위·보스 바에 보여줄 표시용 효과 (아이콘·이름·설명)")]
        [Label("표시 효과")]
        [SerializeField] private StatusEffectData display;

        // 시전자의 피격 무효를 채우고 로그를 남긴다. 쓰러졌으면 아무것도 하지 않는다.
        public override void Apply(SkillContext context)
        {
            BattleUnit caster = context.Caster;
            if (caster.IsDead)
            {
                return;
            }

            caster.ChargeHitBlock(charges, display);
            context.Log($"  {caster.Name}: 피격 무효 {caster.HitBlockCharges}회 충전");
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return $"피격 무효 {charges}회 충전";
        }
    }
}
