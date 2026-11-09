using UnityEngine;

namespace Game.Data
{
    // 변신 기능이 있는 캐릭터의 데이터 (하이퍼캐리 등). 일반 캐릭터 데이터에 변신 설정만 더한다.
    // 변신 항목을 일반 CharacterData에 두면 모든 캐릭터 Inspector에 빈 변신 칸이 보이므로, 타입을 나눠 변신 캐릭터에만 둔다.
    // 상속: 기본 정보·스탯·스킬·패시브는 CharacterData의 것을 그대로 쓰고, 여기서는 변신 설정만 추가한다.
    [CreateAssetMenu(fileName = "NewTransformingCharacter", menuName = "Game/Transforming Character Data")]
    public class TransformingCharacterData : CharacterData
    {
        [Header("변신")]
        [SerializeField] private TransformationData transformation = new TransformationData();

        // override: 부모의 "변신 없음(null)" 대신 이 캐릭터의 변신 설정을 돌려준다.
        public override TransformationData Transformation => transformation;
    }
}
