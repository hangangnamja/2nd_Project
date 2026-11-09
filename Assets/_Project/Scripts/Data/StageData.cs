using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 스테이지 한 개의 적 구성 (현재는 1웨이브). 보스 스테이지처럼 편성과 특수 규칙(무적 적)이 정해진 전투를 에셋으로 저장한다.
    // 웨이브·보상은 스테이지 시스템을 만들 때 추가한다.
    [CreateAssetMenu(fileName = "NewStage", menuName = "Game/Stage Data")]
    public class StageData : ScriptableObject
    {
        [SerializeField] private string stageName = "새 스테이지";

        [Tooltip("이 스테이지 적 전원의 레벨")]
        [SerializeField, Range(1, 60)] private int enemyLevel = 60;

        [Tooltip("적 편성 (리스트 순서 = 편성 순서)")]
        [SerializeField] private List<StageEnemy> enemies = new List<StageEnemy>();

        public string StageName => stageName;
        public int EnemyLevel => enemyLevel;
        public IReadOnlyList<StageEnemy> Enemies => enemies;
    }

    // 스테이지에 배치하는 적 하나와 그 자리의 규칙.
    [Serializable]
    public class StageEnemy
    {
        [SerializeField] private CharacterData character;

        // 무적: 피해를 받지 않고, 승리 조건(적 전멸)에서도 빠진다.
        // 예: 축적의 수호자의 버프몹 → 못 죽이고, 보스만 쓰러뜨리면 클리어 (기획서 6장 보스 1)
        [Tooltip("체크하면 피해를 받지 않고, 이 적이 살아 있어도 다른 적을 모두 쓰러뜨리면 승리")]
        [SerializeField] private bool invulnerable;

        public CharacterData Character => character;
        public bool Invulnerable => invulnerable;
    }
}
