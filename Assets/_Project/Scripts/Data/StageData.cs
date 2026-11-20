using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 스테이지 한 개의 구성: 적 레벨 + 웨이브 목록 + 클리어 보상 + 별 미션 (기획서 6장).
    // 웨이브는 순서대로 진행되며, 한 웨이브의 쓰러뜨려야 하는 적(무적 제외)을 모두 잡으면 다음 웨이브로 넘어간다.
    // 웨이브 구성은 이 에셋의 Inspector에서 조절한다 (웨이브 추가·삭제, 적 순서, 자리별 무적).
    [CreateAssetMenu(fileName = "NewStage", menuName = "Game/Stage Data")]
    public class StageData : ScriptableObject
    {
        // 세이브의 클리어 기록에 저장되는 고유 ID (CharacterData의 ID와 같은 이유로 에셋 이름과 따로 둔다).
        [Tooltip("세이브에 저장되는 고유 ID. 한 번 정하면 바꾸지 않는다")]
        [SerializeField] private string id;

        [SerializeField] private string stageName = "새 스테이지";

        [Tooltip("스테이지 선택 화면에 크게 보이는 번호 (예: 1-3). 비우면 화면이 정한 기본 표기를 쓴다")]
        [SerializeField] private string stageCode;

        [Tooltip("스테이지 상세 패널에 보이는 짧은 설명")]
        [SerializeField, TextArea(1, 3)] private string description;

        [Tooltip("이 스테이지 적 전원의 레벨 (스테이지 선택 화면에는 추천 레벨로 표시)")]
        [SerializeField, Range(1, 60)] private int enemyLevel = 60;

        [Tooltip("웨이브 목록 (위에서부터 순서대로 진행)")]
        [SerializeField] private List<StageWave> waves = new List<StageWave>();

        [Tooltip("이길 때마다 받는 보상 (탐색 던전의 재료 등)")]
        [SerializeField] private List<StageReward> clearRewards = new List<StageReward>();

        [Tooltip("처음 이겼을 때 한 번만 더 받는 보상")]
        [SerializeField] private List<StageReward> firstClearRewards = new List<StageReward>();

        [Tooltip("별 미션 (위에서부터 ★1 ★2 ★3). 별마다 처음 달성했을 때 보상을 한 번 받는다")]
        [SerializeField] private List<StarMission> starMissions = new List<StarMission>();

        [Tooltip("전투 배경 프리팹 (평원·제단·동굴 등, 맨 위에 BattleBackground). 비우면 기본 배경(평원)")]
        [Label("전투 배경")]
        [SerializeField] private GameObject battleBackground;

        public string Id => id;
        public string StageName => stageName;
        public string StageCode => stageCode;
        public string Description => description;
        public int EnemyLevel => enemyLevel;
        public IReadOnlyList<StageWave> Waves => waves;
        public IReadOnlyList<StageReward> ClearRewards => clearRewards;
        public IReadOnlyList<StageReward> FirstClearRewards => firstClearRewards;
        public IReadOnlyList<StarMission> StarMissions => starMissions;
        public GameObject BattleBackground => battleBackground;
    }

    // 보상 한 줄: 재료(골드·보석도 재료 에셋) + 개수.
    [Serializable]
    public class StageReward
    {
        [SerializeField] private MaterialData material;
        [SerializeField, Min(1)] private int amount = 1;

        public MaterialData Material => material;
        public int Amount => amount;
    }

    // 웨이브 하나의 적 편성 (리스트 순서 = 편성 순서 = 화면 왼쪽부터).
    [Serializable]
    public class StageWave
    {
        [SerializeField] private List<StageEnemy> enemies = new List<StageEnemy>();

        public IReadOnlyList<StageEnemy> Enemies => enemies;
    }

    // 웨이브에 배치하는 적 하나와 그 자리의 규칙.
    [Serializable]
    public class StageEnemy
    {
        [SerializeField] private CharacterData character;

        // 무적: 피해를 받지 않고, 웨이브 클리어 조건(적 전멸)에서도 빠진다.
        // 예: 축적의 수호자의 버프몹 → 못 죽이고, 보스만 쓰러뜨리면 클리어 (기획서 6장 보스 1)
        [Tooltip("체크하면 피해를 받지 않고, 이 적이 살아 있어도 다른 적을 모두 쓰러뜨리면 웨이브 클리어")]
        [SerializeField] private bool invulnerable;

        public CharacterData Character => character;
        public bool Invulnerable => invulnerable;
    }
}
