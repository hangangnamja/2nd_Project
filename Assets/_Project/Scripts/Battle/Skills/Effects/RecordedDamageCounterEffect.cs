using System;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자에게 걸린 "피해 기록" 효과(반격 태세)에 쌓인 피해에 비례해 대상 전원에게 피해를 주고, 그 효과를 지운다.
    // 기본 피해 = 기록된 피해 × 배율. 이후 피해 증가·치명·방어력·대상 쪽 보정은 일반 공격과 같다.
    // 예: 태세 중 받은 피해 300, 배율 4.0 → 기본 피해 1200
    [Serializable]
    public class RecordedDamageCounterEffect : SkillEffect
    {
        [Tooltip("받은 피해를 기록하는 상태효과 (반격 태세)")]
        [SerializeField] private StatusEffectData recordStatus;

        [Tooltip("기록된 피해 대비 기본 피해 배율 (4.0 = 받은 피해의 400%)")]
        [SerializeField, Min(0f)] private float ratio = 4f;

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

            double baseDamage = record.RecordedDamage * (double)ratio;
            context.Log($"  {caster.Name}: 받은 피해 {record.RecordedDamage} × {ratio:0.##} 반격");

            if (baseDamage > 0)
            {
                DamageEffect.ApplyBaseDamage(context, baseDamage);
            }

            // 반격을 마치면 태세가 끝난다. 지워짐으로 알려 다른 패시브(곰인형 버퍼 등)도 반응하게 한다.
            caster.Statuses.RemoveInstance(record);
            StatusApplier.ReportRemoved(caster, record, context.Services);
        }
    }
}
