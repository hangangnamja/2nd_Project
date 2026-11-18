using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 업적 분류 (업적 화면의 탭·카드). 저장 값은 숫자 순서라 새 분류는 맨 뒤에 추가한다.
    public enum AchievementCategory
    {
        [InspectorName("작전 기록")] OperationRecord,
        [InspectorName("전투 숙련")] CombatMastery,
        [InspectorName("전투원 육성")] OperatorGrowth,
        [InspectorName("모집")] Recruit,
        [InspectorName("숨은 업적")] Hidden,
    }

    // 업적 등급 (트로피 색). 보상 보석 수는 등급마다 다르다 (기획서 업적: 동 20 / 은 40 / 금 80).
    public enum AchievementTier
    {
        [InspectorName("동")] Bronze,
        [InspectorName("은")] Silver,
        [InspectorName("금")] Gold,
    }

    // 업적 달성 조건. "누적 기록"은 세이브의 카운터를 읽고, 나머지는 지금 세이브 상태(클리어 기록·보유 캐릭터)에서 계산한다.
    // 저장 값은 숫자 순서라 새 조건은 맨 뒤에 추가한다.
    public enum AchievementCondition
    {
        [InspectorName("스테이지 첫 클리어")] StageFirstClear,
        [InspectorName("누적: 적 처치")] EnemiesKilled,
        [InspectorName("누적: 치명타")] CriticalHits,
        [InspectorName("누적: 스킬 사용")] SkillsUsed,
        [InspectorName("누적: 스테이지 클리어")] NormalStageClears,
        [InspectorName("누적: 토벌 클리어")] RaidClears,
        [InspectorName("누적: 탐색 클리어")] ExploreClears,
        [InspectorName("보유 전투원 수")] OwnedCharacters,
        [InspectorName("전투원 최고 레벨")] MaxCharacterLevel,
        [InspectorName("누적: 전투원 진급")] CharacterPromotions,
        [InspectorName("누적: 무기 강화")] WeaponEnhances,
        [InspectorName("누적: 모집 횟수")] RecruitPulls,
        [InspectorName("누적: S 전투원 획득")] SCharactersObtained,
        [InspectorName("누적: 전투원 획득")] CharactersObtained,
    }

    // 업적 하나: 분류·등급·제목 + 조건·목표 + 보상 (기획서 업적).
    // 제목은 에셋에 직접 쓰고, 설명(조건 문장)은 조건·목표로 자동으로 만든다 → 목표를 바꿔도 설명을 따로 고치지 않는다.
    [CreateAssetMenu(fileName = "NewAchievement", menuName = "Game/Achievement Data")]
    public class AchievementData : ScriptableObject
    {
        [Tooltip("세이브의 수령 기록에 저장되는 고유 ID. 한 번 정하면 바꾸지 않는다")]
        [SerializeField] private string id;

        [Label("분류")]
        [SerializeField] private AchievementCategory category;

        [Label("등급")]
        [SerializeField] private AchievementTier tier;

        [Label("제목")]
        [SerializeField] private string title = "새 업적";

        [Label("조건")]
        [SerializeField] private AchievementCondition condition;

        [Tooltip("조건이 '스테이지 첫 클리어'일 때 깨야 하는 스테이지")]
        [Label("스테이지")]
        [SerializeField] private StageData stage;

        [Label("목표 수치", Min = 1f)]
        [SerializeField] private int target = 1;

        [Label("보상 재료")]
        [SerializeField] private MaterialData rewardMaterial;

        [Label("보상 개수", Min = 1f)]
        [SerializeField] private int rewardAmount = 20;

        public string Id => id;
        public AchievementCategory Category => category;
        public AchievementTier Tier => tier;
        public string Title => title;
        public AchievementCondition Condition => condition;
        public StageData Stage => stage;
        public MaterialData RewardMaterial => rewardMaterial;
        public int RewardAmount => rewardAmount;

        // 첫 클리어는 "깼다/안 깼다"뿐이라 목표가 항상 1이다.
        public int Target => condition == AchievementCondition.StageFirstClear ? 1 : Mathf.Max(1, target);

        // 숨은 업적은 달성하기 전까지 조건을 가린다 (업적 화면이 "???"로 보여줌).
        public bool IsHidden => category == AchievementCategory.Hidden;

        // 조건 문장을 조건·목표로 만든다. 예: "적 100기 처치하기 (누적)", "스테이지 1-3을 처음 클리어하기".
        // {Target:N0}: 천 단위 쉼표 (2000 → 2,000).
        public string Describe()
        {
            switch (condition)
            {
                case AchievementCondition.StageFirstClear:
                    return $"{DescribeStage()} 처음 클리어하기";
                case AchievementCondition.EnemiesKilled:
                    return $"적 {Target:N0}기 처치하기 (누적)";
                case AchievementCondition.CriticalHits:
                    return $"치명타 {Target:N0}회 적중시키기 (누적)";
                case AchievementCondition.SkillsUsed:
                    return $"아군 스킬 {Target:N0}회 사용하기 (누적)";
                case AchievementCondition.NormalStageClears:
                    return $"스테이지 {Target:N0}회 클리어하기 (누적)";
                case AchievementCondition.RaidClears:
                    return $"토벌 {Target:N0}회 클리어하기 (누적)";
                case AchievementCondition.ExploreClears:
                    return $"탐색 {Target:N0}회 클리어하기 (누적)";
                case AchievementCondition.OwnedCharacters:
                    return $"전투원 {Target:N0}명 보유하기";
                case AchievementCondition.MaxCharacterLevel:
                    return $"전투원 1명을 Lv.{Target}까지 육성하기";
                case AchievementCondition.CharacterPromotions:
                    return $"전투원 진급 {Target:N0}회 하기 (누적)";
                case AchievementCondition.WeaponEnhances:
                    return $"무기 강화 {Target:N0}회 하기 (누적)";
                case AchievementCondition.RecruitPulls:
                    return $"모집 {Target:N0}회 진행하기 (누적)";
                case AchievementCondition.SCharactersObtained:
                    return $"모집으로 S 전투원 {Target:N0}명 획득하기 (중복 포함)";
                case AchievementCondition.CharactersObtained:
                    return $"모집으로 전투원 {Target:N0}명 획득하기 (중복 포함)";
                default:
                    return title;
            }
        }

        // 첫 클리어 문장의 스테이지 표기: 번호가 있으면 "스테이지 1-3", 없으면 "「축적의 던전」" (탐색은 이름에 난이도 I~III가 들어 있음).
        private string DescribeStage()
        {
            if (stage == null)
            {
                return "(스테이지 없음)";
            }

            return string.IsNullOrEmpty(stage.StageCode)
                ? $"「{stage.StageName}」"
                : $"스테이지 {stage.StageCode}";
        }
    }
}
