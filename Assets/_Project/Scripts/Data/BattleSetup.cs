using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // "이번 전투에 무엇을 넣을지": 아군 편성·레벨과 싸울 스테이지 (Architecture.md 1장).
    // 지금은 Battle 씬을 바로 실행할 때 쓰는 테스트 설정 에셋이고,
    // 로비가 생기면 편성 화면·스테이지 선택 결과로 같은 내용을 만들어 전투에 넘긴다.
    [CreateAssetMenu(fileName = "NewBattleSetup", menuName = "Game/Battle Setup")]
    public class BattleSetup : ScriptableObject
    {
        [Header("아군 (리스트 순서 = 편성 순서)")]
        [SerializeField] private List<CharacterData> allyParty = new List<CharacterData>();

        [Tooltip("아군 전원 레벨 (성장 시스템 전까지 임시). 진급은 그 레벨까지 마친 것으로 본다")]
        [SerializeField, Range(1, 60)] private int allyLevel = 60;

        [Header("스테이지 (적 웨이브·적 레벨)")]
        [SerializeField] private StageData stage;

        public IReadOnlyList<CharacterData> AllyParty => allyParty;
        public int AllyLevel => allyLevel;
        public StageData Stage => stage;

        // 편성 화면에서 고른 편성·레벨·스테이지로 이번 전투 설정을 새로 만든다 (로비 → 전투).
        // CreateInstance: 파일로 저장하지 않는 실행 중 전용 객체를 만든다 → 에셋 파일은 바뀌지 않는다.
        public static BattleSetup Create(IReadOnlyList<CharacterData> party, int level, StageData selectedStage)
        {
            BattleSetup setup = CreateInstance<BattleSetup>();
            setup.allyParty = new List<CharacterData>(party);
            setup.allyLevel = level;
            setup.stage = selectedStage;
            return setup;
        }
    }
}
