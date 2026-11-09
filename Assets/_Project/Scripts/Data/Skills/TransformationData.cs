using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 변신 설정 (기획서 4-3 하이퍼캐리). CharacterData 안에 들어가는 데이터 묶음이다.
    // 변신하면 1~3스가 여기의 변신 스킬로 바뀌고, 정해진 횟수의 자기 턴이 끝나면 종결기를 쓰고 원래대로 돌아온다.
    [Serializable]
    public class TransformationData
    {
        [SerializeField] private string transformName = "변신";

        [Tooltip("변신 중에만 적용되는 스탯 보정 (공격력·방어력·최대 HP 증가 등)")]
        [SerializeField] private List<StatModifier> statModifiers = new List<StatModifier>();

        [Tooltip("변신 중 얻는 엑스트라 턴 수 (속도로 차례를 얻을 때마다 1회, 버프·디버프 남은 턴이 줄지 않음). 다 쓰면 종결기 후 해제")]
        [SerializeField, Min(1)] private int turnLimit = 5;

        [Tooltip("체크하면 변신 중 기절 등 행동 제어 효과에 걸리지 않는다")]
        [SerializeField] private bool crowdControlImmune = true;

        [Tooltip("변신 중 치명적인 피해를 받으면 사망 대신 이 비율(변신 상태 최대 HP 기준)의 HP로 버티고 종결기 후 해제된다")]
        [SerializeField, Range(0f, 1f)] private float forcedReleaseHpRatio = 0.25f;

        [Header("변신 스킬 (변신 중 1~3스를 대체)")]
        [SerializeField] private SkillData skill1 = new SkillData();
        [SerializeField] private SkillData skill2 = new SkillData();
        [SerializeField] private SkillData skill3 = new SkillData();

        [Header("종결기 (변신 종료 시 자동 발동)")]
        [SerializeField] private SkillData finisher = new SkillData();

        public string TransformName => transformName;
        public IReadOnlyList<StatModifier> StatModifiers => statModifiers;
        public int TurnLimit => turnLimit;
        public bool CrowdControlImmune => crowdControlImmune;
        public float ForcedReleaseHpRatio => forcedReleaseHpRatio;
        public SkillData Finisher => finisher;

        // 슬롯 번호(0 = 변신 1스)로 변신 스킬을 꺼낸다.
        public SkillData GetSkill(int slot) => slot switch
        {
            0 => skill1,
            1 => skill2,
            2 => skill3,
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "스킬 슬롯은 0~2입니다."),
        };
    }
}
