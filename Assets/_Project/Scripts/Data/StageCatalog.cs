using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 스테이지 선택 화면에 나오는 스테이지 목록 (기획서 6장: 기본 스테이지 7개 + 보스 스테이지 3개).
    // 화면은 이 목록의 순서대로 노드·카드를 채운다. 목록 칸이 비어 있으면 그 자리는 "준비 중"으로 막힌다.
    [CreateAssetMenu(fileName = "StageCatalog", menuName = "Game/Stage Catalog")]
    public class StageCatalog : ScriptableObject
    {
        [Tooltip("기본 스테이지 (1-1부터 순서대로)")]
        [SerializeField] private List<StageData> normalStages = new List<StageData>();

        [Tooltip("보스 스테이지 (보스 1부터 순서대로)")]
        [SerializeField] private List<StageData> bossStages = new List<StageData>();

        public IReadOnlyList<StageData> NormalStages => normalStages;
        public IReadOnlyList<StageData> BossStages => bossStages;
    }
}
