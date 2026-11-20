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
        // 세이브에는 에셋 대신 이 ID만 저장하고, 불러올 때 GameDatabase가 ID로 이 에셋을 찾는다 (기획서 9장).
        // 에셋 파일 이름과 따로 두어, 파일 이름을 바꿔도 기존 세이브가 깨지지 않게 한다. 적은 저장하지 않으므로 비워 둬도 된다.
        [Tooltip("세이브에 저장되는 고유 ID. 한 번 정하면 바꾸지 않는다 (바꾸면 기존 세이브에서 이 캐릭터를 찾지 못한다)")]
        [SerializeField] private string id;

        [SerializeField] private string displayName = "새 캐릭터";
        [SerializeField] private CharacterRole role = CharacterRole.Attack;

        // 가챠 확률 묶음(S/A)을 정한다. 적은 가챠에 나오지 않으므로 값이 의미 없다.
        [SerializeField] private CharacterGrade grade = CharacterGrade.A;

        // 화면에 보여줄 캐릭터 그림. 가챠 픽업·로비 대표 캐릭터·결과 카드 등 여러 화면이 이 칸 하나를 함께 쓴다.
        // 비워 두면 각 화면이 임시 그림(실루엣 등)을 보여준다.
        [Tooltip("캐릭터 일러스트 (Sprite). 비워 두면 화면마다 임시 그림을 쓴다")]
        [SerializeField] private Sprite illustration;

        // 전투에서 캡슐 대신 보여줄 3D 모델 (UnitModel이 붙은 프리팹: 모델·애니메이션·크기를 프리팹에서 정함).
        // 비워 두면 지금처럼 색 캡슐로 보인다. 화면 표시만 바뀌고 전투 로직은 그대로다.
        [Tooltip("전투 3D 모델 프리팹 (UnitModel 컴포넌트). 비워 두면 캡슐")]
        [SerializeField] private GameObject battleModel;

        // Min 속성: Inspector에서 이 값보다 작게 입력하지 못하도록 막는다 (HP 0인 캐릭터 방지).
        [SerializeField, Min(1)] private int maxHp = 1000;
        [SerializeField, Min(0)] private int attack = 100;
        [SerializeField, Min(0)] private int defense = 50;

        // 기초 속도. 아군은 기획서 기준 90~130 범위로 설정한다. 0이면 행동하지 못하므로 최소 1.
        [SerializeField, Min(1)] private int baseSpeed = 100;

        // 치명 스탯은 캐릭터마다 다르다 (장비가 생기면 장비로도 증감). 값은 비율: 0.05 = 5%
        [Header("치명")]
        [SerializeField, Range(0f, 1f)] private float critRate = 0.05f;
        [SerializeField, Min(0f)] private float critDamage = 0.5f;

        [Header("스킬 (1스 = 평타)")]
        [SerializeField] private SkillData skill1 = new SkillData();
        [SerializeField] private SkillData skill2 = new SkillData();
        [SerializeField] private SkillData skill3 = new SkillData();

        [Header("패시브")]
        [SerializeField] private List<PassiveData> passives = new List<PassiveData>();

        public const int SkillCount = 3;

        public IReadOnlyList<PassiveData> Passives => passives;

        // 변신 설정. 일반 캐릭터는 변신이 없으므로 null이다.
        // virtual: 변신 캐릭터 전용 타입(TransformingCharacterData)이 덮어써서 자기 변신 설정을 돌려준다.
        // 그래서 전투 코드는 캐릭터 타입을 몰라도 "Transformation이 null이 아니면 변신 가능"으로만 판단하면 된다.
        public virtual TransformationData Transformation => null;

        // => 로 작성한 읽기 전용 프로퍼티: 외부 코드는 값을 읽을 수만 있고 에셋 값을 바꿀 수 없다.
        public string Id => id;
        public string DisplayName => displayName;
        public CharacterRole Role => role;
        public CharacterGrade Grade => grade;
        public Sprite Illustration => illustration;
        public GameObject BattleModel => battleModel;

#if UNITY_EDITOR
        // Inspector에서 값을 바꿀 때마다 Unity가 부른다. 전투 3D 모델 칸에 UnitModel이 없는 프리팹(원본 모델 등)을 넣으면
        // 전투에서 캡슐로 남으므로 바로 알려 준다. GetComponent("이름"): 화면 코드(UI)를 참조하지 않고 이름으로만 찾는다.
        private void OnValidate()
        {
            if (battleModel != null && battleModel.GetComponent("UnitModel") == null)
            {
                Debug.LogWarning($"[{name}] 전투 3D 모델 '{battleModel.name}'에 UnitModel이 없습니다. 원본 모델을 품은 전투용 프리팹(Prefabs/Models)을 넣어야 3D로 보입니다.", this);
            }
        }
#endif
        public int MaxHp => maxHp;
        public int Attack => attack;
        public int Defense => defense;
        public int BaseSpeed => baseSpeed;
        public float CritRate => critRate;
        public float CritDamage => critDamage;

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
