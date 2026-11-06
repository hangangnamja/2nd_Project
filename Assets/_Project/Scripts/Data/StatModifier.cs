using System;
using UnityEngine;

namespace Game.Data
{
    // 버프·디버프가 바꿀 수 있는 스탯 종류.
    public enum StatType
    {
        Attack,
        Defense,
        Speed,
    }

    // 상태효과 하나가 주는 스탯 보정 한 줄. 예: 공격력 +30%, 속도 +10
    // 최종 스탯 = 기본값 × (1 + 같은 스탯 % 보정의 합) + 같은 스탯 고정 보정의 합 (기획서 3-3, 3-5)
    [Serializable]
    public struct StatModifier
    {
        [SerializeField] private StatType stat;

        [Tooltip("% 보정. 0.3 = +30%, -0.2 = -20% (디버프)")]
        [SerializeField] private float percent;

        [Tooltip("고정 보정. 10 = +10")]
        [SerializeField] private float flat;

        public StatType Stat => stat;
        public float Percent => percent;
        public float Flat => flat;
    }
}
