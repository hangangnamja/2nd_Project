using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 스테이지 선택 화면에 나오는 스테이지 목록 (기획서 6장: 기본 스테이지 7개 + 토벌(보스) 3개 + 탐색 던전).
    // 화면은 이 목록의 순서대로 노드·카드를 채운다. 목록 칸이 비어 있으면 그 자리는 "준비 중"으로 막힌다.
    [CreateAssetMenu(fileName = "StageCatalog", menuName = "Game/Stage Catalog")]
    public class StageCatalog : ScriptableObject
    {
        [Tooltip("기본 스테이지 (1-1부터 순서대로)")]
        [SerializeField] private List<StageData> normalStages = new List<StageData>();

        [Tooltip("토벌(보스 스테이지) (보스 1부터 순서대로)")]
        [SerializeField] private List<StageData> bossStages = new List<StageData>();

        [Tooltip("탐색 장소 (지도 노드 순서대로). 장소마다 난이도별 스테이지를 둔다")]
        [SerializeField] private List<ExplorationSite> explorationSites = new List<ExplorationSite>();

        public IReadOnlyList<StageData> NormalStages => normalStages;
        public IReadOnlyList<StageData> BossStages => bossStages;
        public IReadOnlyList<ExplorationSite> ExplorationSites => explorationSites;
    }

    // 탐색 장소 하나 (재화 던전, 기획서 6장): 지도에 보이는 장소 이름·주는 재화 표기 + 난이도별 스테이지.
    [System.Serializable]
    public class ExplorationSite
    {
        [Tooltip("지도 노드에 크게 보이는 장소 이름 (예: 동굴)")]
        [SerializeField] private string siteName = "새 장소";

        [Tooltip("장소 이름 아래 작은 글자 (주는 재화, 예: 골드)")]
        [SerializeField] private string rewardLabel;

        [Tooltip("난이도 단계별 스테이지 (쉬운 것부터)")]
        [SerializeField] private List<StageData> difficulties = new List<StageData>();

        public string SiteName => siteName;
        public string RewardLabel => rewardLabel;
        public IReadOnlyList<StageData> Difficulties => difficulties;
    }
}
