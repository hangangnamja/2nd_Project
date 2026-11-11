using System;
using UnityEngine;

namespace Game.Data
{
    // 버프·디버프(나중에는 장비)가 바꿀 수 있는 스탯 종류.
    // [InspectorName("...")]: Inspector 드롭다운에 영어 이름 대신 보여줄 한국어 이름. 에셋에는 여전히 숫자 순서로 저장되므로 이름만 바뀐다.
    public enum StatType
    {
        // 기본 스탯: 최종값 = 원본 × (1 + % 합) + 고정 합
        [InspectorName("공격력")] Attack,
        [InspectorName("방어력")] Defense,
        [InspectorName("속도")] Speed,

        // 비율 스탯: % 칸의 값을 그대로 더한다 (0.2 = +20%p). 고정 칸은 쓰지 않는다.
        [InspectorName("치명 확률")] CritRate,                    // 치명확률 (원본 + 합, 0~100%)
        [InspectorName("치명 피해")] CritDamage,                  // 치명피해 (원본 + 합)
        [InspectorName("주는 피해 증가")] DamageBonus,            // 주는 피해 증가 (공격자 쪽)
        [InspectorName("받는 피해 증가")] DamageTakenIncrease,    // 받는 피해 증가 (대상 쪽, 지속피해에도 적용)

        // 피해 감소: 효과끼리 곱연산 → 1 - (1 - a)(1 - b)..., 최대 75% (기획서 3-5)
        [InspectorName("피해 감소")] DamageReduction,

        // 가하는 지속피해 증가 (비율 스탯, 지속피해를 건 쪽, 발동할 때 적용)
        // 에셋에는 숫자 순서로 저장되므로 새 항목은 반드시 맨 뒤에 추가한다.
        [InspectorName("지속피해 증가")] DamageOverTimeBonus,

        // 최대 HP (기본 스탯). 하이퍼캐리 변신의 HP 증가처럼 최대 HP를 늘리는 효과에 쓴다.
        [InspectorName("최대 HP")] MaxHp,
    }

    // 상태효과 하나가 주는 스탯 보정 한 줄. 예: 공격력 +30%, 속도 +10, 치명확률 +20%p
    // 기본 스탯은 "원본 × (1 + % 합) + 고정 합", 비율 스탯은 "원본 + % 합"으로 계산한다.
    [Serializable]
    public struct StatModifier
    {
        [Tooltip("바꿀 스탯.\n기본 스탯(공격력·방어력·속도·최대 HP): 원본 × (1 + % 합) + 고정 합\n비율 스탯(치명 확률·치명 피해·주는/받는 피해 증가·지속피해 증가): 원본 + % 합 (고정 칸은 안 씀)\n피해 감소: 효과끼리 곱연산, 최대 75%")]
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
