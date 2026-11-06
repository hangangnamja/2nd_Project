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

        // Min 속성: Inspector에서 이 값보다 작게 입력하지 못하도록 막는다 (HP 0인 캐릭터 방지).
        [SerializeField, Min(1)] private int maxHp = 1000;
        [SerializeField, Min(0)] private int attack = 100;
        [SerializeField, Min(0)] private int defense = 50;

        // => 로 작성한 읽기 전용 프로퍼티: 외부 코드는 값을 읽을 수만 있고 에셋 값을 바꿀 수 없다.
        public string DisplayName => displayName;
        public int MaxHp => maxHp;
        public int Attack => attack;
        public int Defense => defense;
    }
}
