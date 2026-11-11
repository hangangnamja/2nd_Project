using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 무기 등급. 숫자가 곧 성급이라 비교·표시(★ 개수)에 그대로 쓴다.
    // 에셋에는 숫자 값(3·4·5)으로 저장되므로 값을 바꾸지 않는다.
    public enum WeaponGrade
    {
        [InspectorName("3성")] ThreeStar = 3,
        [InspectorName("4성")] FourStar = 4,
        [InspectorName("5성")] FiveStar = 5,
    }

    // 무기 종류 하나의 고정 데이터 (기획서 5장: 공격력·HP·방어력 기초 스탯만 제공).
    // 플레이어가 가진 무기 "개체"(레벨·장착 캐릭터)는 세이브의 WeaponSave가 들고, 이 에셋은 ID로 연결된다.
    [CreateAssetMenu(fileName = "Weapon_", menuName = "Game/Weapon")]
    public class WeaponData : ScriptableObject
    {
        [Tooltip("세이브에 저장되는 ID. 한 번 정하면 바꾸지 않는다")]
        [Label("ID")]
        [SerializeField] private string id;

        [Label("이름")]
        [SerializeField] private string displayName = "새 무기";

        [Label("등급")]
        [SerializeField] private WeaponGrade grade = WeaponGrade.ThreeStar;

        [Tooltip("인벤토리·가챠 결과에 보이는 그림")]
        [Label("아이콘")]
        [SerializeField] private Sprite icon;

        // TextArea와 Label은 둘 다 칸을 그리는 속성이라 함께 쓰면 한쪽만 적용된다 → 여러 줄 입력이 필요한 설명은 TextArea만 쓴다.
        [Tooltip("설명 (인벤토리 상세 패널에 표시)")]
        [SerializeField, TextArea(2, 4)] private string description;

        [Header("기초 스탯 (Lv1)")]
        [Label("공격력", Min = 0f)]
        [SerializeField] private int attack = 30;

        [Label("HP", Min = 0f)]
        [SerializeField] private int maxHp;

        [Label("방어력", Min = 0f)]
        [SerializeField] private int defense;

        public string Id => id;
        public string DisplayName => displayName;
        public WeaponGrade Grade => grade;
        public Sprite Icon => icon;
        public string Description => description;
        public int Attack => attack;
        public int MaxHp => maxHp;
        public int Defense => defense;
    }
}
