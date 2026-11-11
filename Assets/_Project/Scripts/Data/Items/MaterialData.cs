using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 재료 분류 (기획서 8장 성장 재화). 인벤토리 재료 탭의 정렬 순서이기도 하다.
    // 에셋에는 숫자 순서로 저장되므로 새 항목은 맨 뒤에 추가한다.
    public enum MaterialCategory
    {
        [InspectorName("경험치 재료")] Experience,
        [InspectorName("진급 재료")] Promotion,
        [InspectorName("무기 재료")] Weapon,
    }

    // 재료 종류 하나의 고정 데이터. 재료는 개체가 아니라 "종류별 개수"로 보관한다 (세이브의 MaterialSave).
    [CreateAssetMenu(fileName = "Material_", menuName = "Game/Material")]
    public class MaterialData : ScriptableObject
    {
        [Tooltip("세이브에 저장되는 ID. 한 번 정하면 바꾸지 않는다")]
        [Label("ID")]
        [SerializeField] private string id;

        [Label("이름")]
        [SerializeField] private string displayName = "새 재료";

        [Label("분류")]
        [SerializeField] private MaterialCategory category = MaterialCategory.Experience;

        [Tooltip("테두리 색을 정하는 등급 (3~5). 같은 분류 안의 상·중·하 구분에 쓴다")]
        [Label("등급", Min = 3f, Max = 5f)]
        [SerializeField] private int grade = 3;

        [Label("아이콘")]
        [SerializeField] private Sprite icon;

        // TextArea와 Label은 둘 다 칸을 그리는 속성이라 함께 쓰면 한쪽만 적용된다 → 여러 줄 입력이 필요한 설명은 TextArea만 쓴다.
        [Tooltip("설명 (인벤토리 상세 패널에 표시)")]
        [SerializeField, TextArea(2, 4)] private string description;

        public string Id => id;
        public string DisplayName => displayName;
        public MaterialCategory Category => category;
        public int Grade => grade;
        public Sprite Icon => icon;
        public string Description => description;
    }
}
