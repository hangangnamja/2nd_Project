using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 캐릭터 한 명의 원본 설정값을 담는 데이터 에셋.
    // 전투 중 바뀌는 값(현재 HP 등)은 BattleUnit이 따로 들고, 이 에셋은 읽기만 한다.
    // CreateAssetMenu: 프로젝트 창 우클릭 > Create > Game > Character Data 메뉴로 에셋을 만들 수 있게 해준다.
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "Game/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [SerializeField] private string displayName = "새 캐릭터";
        [SerializeField] private CharacterRole role = CharacterRole.Attack;

        // Min 속성: Inspector에서 이 값보다 작게 입력하지 못하도록 막는다 (HP 0인 캐릭터 방지).
        [SerializeField, Min(1)] private int maxHp = 1000;
        [SerializeField, Min(0)] private int attack = 100;
        [SerializeField, Min(0)] private int defense = 50;

        // 기초 속도. 아군은 기획서 기준 90~130 범위로 설정한다. 0이면 행동하지 못하므로 최소 1.
        [SerializeField, Min(1)] private int baseSpeed = 100;

        [Header("스킬 (1스 = 평타)")]
        [SerializeField] private SkillData skill1 = new SkillData();
        [SerializeField] private SkillData skill2 = new SkillData();
        [SerializeField] private SkillData skill3 = new SkillData();

        [Header("패시브")]
        [SerializeField] private List<PassiveData> passives = new List<PassiveData>();

        public const int SkillCount = 3;

        public IReadOnlyList<PassiveData> Passives => passives;

        // => 로 작성한 읽기 전용 프로퍼티: 외부 코드는 값을 읽을 수만 있고 에셋 값을 바꿀 수 없다.
        public string DisplayName => displayName;
        public CharacterRole Role => role;
        public int MaxHp => maxHp;
        public int Attack => attack;
        public int Defense => defense;
        public int BaseSpeed => baseSpeed;

        // 슬롯 번호(0 = 1스, 1 = 2스, 2 = 3스)로 스킬을 꺼낸다.
        // switch 식: 값에 따라 다른 결과를 고르는 문법. _ 는 위의 어느 경우에도 해당하지 않을 때(그 외)다.
        public SkillData GetSkill(int slot) => slot switch
        {
            0 => skill1,
            1 => skill2,
            2 => skill3,
            _ => throw new System.ArgumentOutOfRangeException(nameof(slot), slot, "스킬 슬롯은 0~2입니다."),
        };
    }
}
