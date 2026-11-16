using System;
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

        [Tooltip("아군 전원 레벨 (테스트 설정용). 진급은 그 레벨까지 마친 것으로 본다")]
        [SerializeField, Range(1, 60)] private int allyLevel = 60;

        // 로비에서 만든 설정의 캐릭터별 레벨 (allyParty와 같은 순서, 세이브의 보유 캐릭터 레벨).
        // 테스트 에셋에는 없으므로 null이고, 그때는 allyLevel을 모두에게 쓴다.
        // NonSerialized: Inspector·에셋 파일에 저장하지 않는 실행 중 전용 값.
        [NonSerialized] private List<int> allyLevels;

        // 캐릭터별 진급 횟수 (allyLevels와 함께 로비에서만 채운다). 진급 점프 스탯을 실제 진급 기준으로 넣기 위해 쓴다.
        [NonSerialized] private List<int> allyPromotions;

        [Header("스테이지 (적 웨이브·적 레벨)")]
        [SerializeField] private StageData stage;

        public IReadOnlyList<CharacterData> AllyParty => allyParty;
        public StageData Stage => stage;

        // 편성 순서 index번 아군의 레벨. 캐릭터별 레벨이 있으면 그 값, 없으면(테스트 설정) 전원 공통 레벨.
        public int GetAllyLevel(int index)
        {
            return allyLevels != null && index < allyLevels.Count ? allyLevels[index] : allyLevel;
        }

        // 편성 순서 index번 아군의 진급 횟수. 모르면(테스트 설정) -1 → 그 레벨까지 진급을 마친 것으로 계산한다.
        public int GetAllyPromotion(int index)
        {
            return allyPromotions != null && index < allyPromotions.Count ? allyPromotions[index] : -1;
        }

        // 편성 화면에서 고른 편성·캐릭터별 레벨·진급·스테이지로 이번 전투 설정을 새로 만든다 (로비 → 전투).
        // levels·promotions는 party와 같은 순서다. CreateInstance: 파일로 저장하지 않는 실행 중 전용 객체를 만든다 → 에셋 파일은 바뀌지 않는다.
        public static BattleSetup Create(IReadOnlyList<CharacterData> party, IReadOnlyList<int> levels, IReadOnlyList<int> promotions, StageData selectedStage)
        {
            BattleSetup setup = CreateInstance<BattleSetup>();
            setup.allyParty = new List<CharacterData>(party);
            setup.allyLevels = new List<int>(levels);
            setup.allyPromotions = new List<int>(promotions);
            setup.stage = selectedStage;
            return setup;
        }
    }
}
