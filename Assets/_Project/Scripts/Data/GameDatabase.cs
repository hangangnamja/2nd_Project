using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 세이브의 ID를 실제 데이터 에셋(SO)으로 바꿔 주는 목록 (기획서 9장: 세이브에는 ID만 저장하고, 불러올 때 ID로 SO를 찾아 연결).
    // 새 게임을 시작할 때 지급할 캐릭터도 여기서 정한다.
    // Resources 폴더에 두어, 씬에 연결하지 않아도 PlayerProfile이 이름으로 불러올 수 있게 한다.
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "Game/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        [Tooltip("플레이어가 보유할 수 있는 캐릭터 전체 (적 제외). 여기 없는 캐릭터는 세이브에 있어도 불러오지 않는다")]
        [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

        [Tooltip("스테이지 목록 (기본·보스 스테이지 모두 이 목록에서 ID로 찾는다)")]
        [SerializeField] private StageCatalog stageCatalog;

        [Tooltip("가챠 확률·천장·비용 규칙")]
        [SerializeField] private GachaRulesData gachaRules;

        [Header("인벤토리")]
        [Tooltip("무기 전체. 가챠의 3성 무기는 이 중 3성에서 고르고, 세이브의 무기 ID도 여기서 찾는다")]
        [SerializeField] private List<WeaponData> weapons = new List<WeaponData>();

        [Tooltip("재료 전체. 세이브의 재료 ID를 여기서 찾는다")]
        [SerializeField] private List<MaterialData> materials = new List<MaterialData>();

        [Tooltip("무기 칸 수·재료 최대 개수·분해 보상")]
        [SerializeField] private InventoryRulesData inventoryRules;

        [Tooltip("레벨별 필요 경험치·진급 구간·경험치 재료")]
        [SerializeField] private GrowthRulesData growthRules;

        [Header("새 게임")]
        [Tooltip("처음 시작할 때 보유하는 캐릭터. 앞에서부터 최대 4명이 첫 편성이 된다")]
        [SerializeField] private List<StartingCharacter> startingCharacters = new List<StartingCharacter>();

        public IReadOnlyList<StartingCharacter> StartingCharacters => startingCharacters;
        public GrowthRulesData GrowthRules => growthRules;
        public GachaRulesData GachaRules => gachaRules;
        public InventoryRulesData InventoryRules => inventoryRules;
        public IReadOnlyList<MaterialData> Materials => materials;

        // 그 등급의 무기 목록 (가챠에서 3성 무기가 나올 때 이 중에서 고른다).
        public List<WeaponData> GetWeaponsOfGrade(WeaponGrade grade)
        {
            // FindAll(조건 람다): 조건이 참인 원소만 모은 새 목록. 빈 칸(null)은 제외한다.
            return weapons.FindAll(weapon => weapon != null && weapon.Grade == grade);
        }

        // ID가 같은 무기를 찾는다. 없으면 null. Find(조건 람다): 조건이 참인 첫 원소.
        public WeaponData FindWeapon(string id)
        {
            return weapons.Find(weapon => weapon != null && weapon.Id == id);
        }

        // ID가 같은 재료를 찾는다. 없으면 null.
        public MaterialData FindMaterial(string id)
        {
            return materials.Find(material => material != null && material.Id == id);
        }

        // 보유 가능한 캐릭터 중 그 등급인 캐릭터 목록 (가챠에서 S·A가 나올 때 이 중에서 고른다).
        public List<CharacterData> GetCharactersOfGrade(CharacterGrade grade)
        {
            var result = new List<CharacterData>();
            foreach (CharacterData character in characters)
            {
                if (character != null && character.Grade == grade)
                {
                    result.Add(character);
                }
            }

            return result;
        }

        // ID가 같은 보유 가능 캐릭터를 찾는다. 없으면 null (삭제된 캐릭터이거나 ID가 바뀐 경우).
        public CharacterData FindCharacter(string id)
        {
            foreach (CharacterData character in characters)
            {
                if (character != null && character.Id == id)
                {
                    return character;
                }
            }

            return null;
        }

        // ID가 같은 스테이지를 기본 → 보스 스테이지 순서로 찾는다. 없으면 null.
        public StageData FindStage(string id)
        {
            // ??: 왼쪽(기본 스테이지에서 찾은 결과)이 null일 때만 오른쪽(보스 스테이지에서 찾기)을 실행해 그 결과를 쓴다.
            return FindStageIn(stageCatalog.NormalStages, id) ?? FindStageIn(stageCatalog.BossStages, id);
        }

        private static StageData FindStageIn(IReadOnlyList<StageData> stages, string id)
        {
            foreach (StageData stage in stages)
            {
                if (stage != null && stage.Id == id)
                {
                    return stage;
                }
            }

            return null;
        }
    }

    // 새 게임에서 지급하는 캐릭터 한 명과 그 레벨.
    [Serializable]
    public class StartingCharacter
    {
        [SerializeField] private CharacterData character;
        [SerializeField, Range(1, 60)] private int level = 1;

        public CharacterData Character => character;
        public int Level => level;
    }
}
