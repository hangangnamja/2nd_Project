using System;
using Game.Core;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자에게 걸린 "피해 기록" 효과(반격 태세)에 쌓인 피해에 비례해 대상 전원에게 피해를 주고, 그 효과를 지운다.
    // 받은 피해를 그대로 돌려주지 않고 "시전자 최대 HP 대비 비율"로 바꾼 뒤 공격력 배율로 쓴다:
    //   스킬 배율 = (받은 피해 / 시전자 최대 HP) × 계수 → 피해 = 공격력 × 스킬 배율 (이후 피해 증가·치명·방어력은 일반 공격과 같음)
    // 이유: 적 공격력은 아군 HP 곡선(Lv60 ×12.5), 적 HP는 아군 공격력 곡선(×6.15)을 따라 커져서,
    //       받은 피해를 그대로 쓰면 레벨이 오를수록 반격이 적 HP보다 빨리 커진다. 비율로 바꾸면 레벨과 상관없이 같은 비율이 나온다.
    // 예: 최대 HP 16,250, 받은 피해 5,520 (34%), 계수 4.0 → 공격력 136% 광역
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("공격/받은 피해 비례 반격", "피해 기록 효과에 쌓인 받은 피해(최대 HP 대비 비율) × 계수를 공격력 배율로 반격하고 기록을 지운다 (반격 태세)", 120)]
    [Serializable]
    public class RecordedDamageCounterEffect : SkillEffect
    {
        [Tooltip("받은 피해를 기록하는 상태효과 (반격 태세)")]
        [Label("피해 기록 효과")]
        [SerializeField] private StatusEffectData recordStatus;

        [Tooltip("받은 피해 비율(최대 HP 대비) → 공격력 배율 계수. 4.0이면 최대 HP의 50%를 받았을 때 공격력 200%")]
        [Label("계수", Min = 0f)]
        [SerializeField] private float ratio = 4f;

        // 기록을 읽어 피해를 주고, 태세를 소모한다.
        public override void Apply(SkillContext context)
        {
            BattleUnit caster = context.Caster;

            // 상태효과가 연결되지 않았거나 태세가 이미 사라졌으면 record는 null이다.
            StatusInstance record = recordStatus != null ? caster.Statuses.Find(recordStatus) : null;
            if (record == null)
            {
                context.Log($"  {caster.Name}: 기록된 피해가 없어 반격하지 않음");
                return;
            }

            // 받은 피해를 최대 HP 대비 비율로 바꿔 공격력 배율을 만든다. 예: 5,520 / 16,250 ≈ 0.34 → × 4 = 1.36 (136%)
            double takenRatio = record.RecordedDamage / (double)caster.MaxHp;
            float multiplier = (float)(takenRatio * ratio);
            context.Log($"  {caster.Name}: 받은 피해 {record.RecordedDamage} (최대 HP의 {takenRatio:P0}) → 공격력 {multiplier:P0} 반격");

            if (multiplier > 0f)
            {
                // 람다식 _ => multiplier: 대상이 누구든 같은 배율을 쓴다 (광역 반격).
                DamageEffect.ApplyDamage(context, _ => multiplier);
            }

            // 반격을 마치면 태세가 끝난다. 지워짐으로 알려 다른 패시브(곰인형 버퍼 등)도 반응하게 한다.
            caster.Statuses.RemoveInstance(record);
            StatusApplier.ReportRemoved(caster, record, context.Services);
        }
    }
}
