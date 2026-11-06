using System;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상 전원의 HP를 회복한다. 회복량 기준 스탯(공격력 / 최대 HP)은 캐릭터마다 다르므로 선택할 수 있다.
    [Serializable]
    public class HealEffect : SkillEffect
    {
        public enum ScaleStat
        {
            CasterAttack,
            TargetMaxHp,
        }

        [SerializeField] private ScaleStat scaleStat = ScaleStat.CasterAttack;

        [Tooltip("기준 스탯 대비 회복 비율 (100% = 1.0)")]
        [SerializeField, Min(0f)] private float ratio = 1f;

        // 대상마다 회복량을 계산해 적용한다. 쓰러진 대상은 부활시키지 않는다 (게임에 부활 없음).
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                if (target.IsDead)
                {
                    continue;
                }

                int healed = target.Heal(CalculateAmount(context.Caster, target));
                context.Log($"  {target.Name} {healed} 회복 (HP {target.CurrentHp}/{target.MaxHp})");
            }
        }

        // 기준 스탯 × 비율로 회복량을 구한다. 시전자 공격력 기준이면 시전자, 최대 HP 기준이면 대상의 값을 쓴다.
        private int CalculateAmount(BattleUnit caster, BattleUnit target)
        {
            float baseValue = scaleStat == ScaleStat.CasterAttack ? caster.Data.Attack : target.MaxHp;

            // Mathf.FloorToInt: 소수점을 버려 정수 회복량으로 만든다.
            return Mathf.FloorToInt(baseValue * ratio);
        }
    }
}
