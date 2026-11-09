using System;
using UnityEngine;

namespace Game.Data
{
    // 버프·디버프(나중에는 장비)가 바꿀 수 있는 스탯 종류.
    public enum StatType
    {
        // 기본 스탯: 최종값 = 원본 × (1 + % 합) + 고정 합
        Attack,
        Defense,
        Speed,

        // 비율 스탯: % 칸의 값을 그대로 더한다 (0.2 = +20%p). 고정 칸은 쓰지 않는다.
        CritRate,           // 치명확률 (원본 + 합, 0~100%)
        CritDamage,         // 치명피해 (원본 + 합)
        DamageBonus,        // 주는 피해 증가 (공격자 쪽)
        DamageTakenIncrease,// 받는 피해 증가 (대상 쪽, 지속피해에도 적용)

        // 피해 감소: 효과끼리 곱연산 → 1 - (1 - a)(1 - b)..., 최대 75% (기획서 3-5)
        DamageReduction,

        // 가하는 지속피해 증가 (비율 스탯, 지속피해를 건 쪽, 발동할 때 적용)
        // 에셋에는 숫자 순서로 저장되므로 새 항목은 반드시 맨 뒤에 추가한다.
        DamageOverTimeBonus,

        // 최대 HP (기본 스탯). 하이퍼캐리 변신의 HP 증가처럼 최대 HP를 늘리는 효과에 쓴다.
        MaxHp,
    }

    // 상태효과 하나가 주는 스탯 보정 한 줄. 예: 공격력 +30%, 속도 +10, 치명확률 +20%p
    // 기본 스탯은 "원본 × (1 + % 합) + 고정 합", 비율 스탯은 "원본 + % 합"으로 계산한다.
    [Serializable]
    public struct StatModifier
    {
        [SerializeField] private StatType stat;

        [Tooltip("% 보정. 0.3 = +30% (비율 스탯은 +30%p), -0.2 = -20% (디버프)")]
        [SerializeField] private float percent;

        [Tooltip("고정 보정 (기본 스탯 전용). 10 = +10")]
        [SerializeField] private float flat;

        public StatType Stat => stat;
        public float Percent => percent;
        public float Flat => flat;
    }
}
