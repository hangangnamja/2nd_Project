using UnityEngine;

namespace Game.Data
{
    // 가챠 배너 하나 (기획서 7장: 픽업 배너). 모집 화면은 이 내용으로 배너 제목·픽업 캐릭터·종료 일시를 채운다.
    // 픽업은 S 1명뿐이다 (A 픽업 없음). 뽑기 확률·천장 규칙은 배너마다 다르지 않으므로 GachaRulesData가 가진다.
    [CreateAssetMenu(fileName = "NewBanner", menuName = "Game/Gacha Banner")]
    public class GachaBannerData : ScriptableObject
    {
        [Tooltip("세이브·기록에 쓰는 고유 ID. 한 번 정하면 바꾸지 않는다")]
        [SerializeField] private string id;

        [SerializeField] private string bannerName = "새 배너";

        [Tooltip("배너 아래 강조 문구")]
        [SerializeField] private string catchphrase = "상기 캐릭터 출현 확률 UP!!!";

        [Tooltip("종료 일시 표기 (기간 시스템 전까지 글자로만 표시)")]
        [SerializeField] private string endDateText = "상시";

        [Tooltip("픽업 S 캐릭터. S가 나오면 반천 규칙에 따라 이 캐릭터 또는 다른 S가 나온다")]
        [SerializeField] private CharacterData pickupCharacter;

        [Tooltip("배너 배경 색 (일러스트 전까지 임시)")]
        [SerializeField] private Color themeColor = new Color(0.85f, 0.35f, 0.5f);

        public string Id => id;
        public string BannerName => bannerName;
        public string Catchphrase => catchphrase;
        public string EndDateText => endDateText;
        public CharacterData PickupCharacter => pickupCharacter;
        public Color ThemeColor => themeColor;
    }
}
