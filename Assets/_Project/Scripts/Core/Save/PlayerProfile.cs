using System;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Core
{
    // 보유 캐릭터 한 명: 데이터 에셋 + 세이브에 저장된 성장 값.
    public class OwnedCharacter
    {
        public CharacterData Data { get; }
        public int Level { get; }
        public int Promotion { get; }

        // 지금 레벨에서 다음 레벨까지 쌓인 경험치
        public int Exp { get; }

        public OwnedCharacter(CharacterData data, int level, int promotion, int exp)
        {
            Data = data;
            Level = level;
            Promotion = promotion;
            Exp = exp;
        }
    }

    // 플레이어의 저장 상태(세이브)를 들고 각 화면에 제공하는 곳. static이라 씬이 바뀌어도 그대로 유지된다.
    //  - 처음 쓰일 때 세이브를 불러오고, 없으면 새 게임을 만든다 → 어느 씬을 먼저 실행해도 동작한다
    //  - 값을 바꾸는 함수는 바꾼 직후 바로 저장한다 (기획서 9장: 변경 직후 즉시 저장)
    //  - 화면은 세이브 구조(SaveData)를 직접 만지지 않고, 이 함수들을 거쳐 데이터 에셋으로 받는다
    // partial: 인벤토리 부분은 PlayerProfile.Inventory.cs에 따로 적는다.
    public static partial class PlayerProfile
    {
        // 편성 칸 수 (기획서 2장 4인 파티)
        public const int FormationSize = 4;

        // Resources 폴더 안의 GameDatabase 에셋 이름
        private const string DatabaseResourceName = "GameDatabase";

        private static SaveData data;
        private static GameDatabase database;

        // 보유 캐릭터 목록이나 성장 값(레벨 등)이 바뀌어 저장된 직후 알린다.
        // event Action: 구독한 화면(편성, 이후 육성 화면 등)의 함수를 모아 두었다가 Invoke 때 한꺼번에 호출한다.
        // static이라 구독한 화면은 꺼질 때(OnDestroy) 반드시 해제해야, 사라진 화면이 호출되지 않는다.
        public static event Action OwnedCharactersChanged;

        // 이 프로젝트는 플레이 시작 때 static 값을 지우는 과정(도메인 리로드)을 꺼 두었으므로,
        // 플레이할 때마다 직접 비워 세이브를 다시 읽게 한다. (에디터에서 세이브를 지운 뒤 다시 플레이해도 반영됨)
        // RuntimeInitializeOnLoadMethod(SubsystemRegistration): 플레이 시작 시 씬을 불러오기 전에 Unity가 이 함수를 자동 호출한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            data = null;
            database = null;

            // 지난 플레이에서 해제되지 못한 구독이 남아 있지 않도록 비운다.
            OwnedCharactersChanged = null;
            InventoryChanged = null;
        }

        // ID → 데이터 에셋 목록 (가챠가 S·A 캐릭터 목록과 규칙을 꺼낼 때 사용)
        public static GameDatabase Database
        {
            get
            {
                EnsureLoaded();
                return database;
            }
        }

        // ---------- 재화 ----------

        // 보유 보석 (가챠 화면 표시용)
        public static int GetGems()
        {
            EnsureLoaded();
            return data.currency.gems;
        }

        // ---------- 가챠 ----------

        // 세이브의 가챠 카운터. 뽑기 판정(GachaMachine)이 직접 갱신하고, CommitGachaPull이 저장한다.
        public static GachaSave GachaState
        {
            get
            {
                EnsureLoaded();
                return data.gacha;
            }
        }

        // 뽑기 결과 반영: 새로 얻은 캐릭터를 보유 목록에 추가하고, 나온 무기를 무기 인벤에 넣고, 보석을 쓴 뒤 바로 저장한다
        // (기획서 9장: 가챠 직후 저장). 이미 보유한 캐릭터는 newCharacters에 넣지 않는다 (중복은 지금은 효과 없음).
        // 반환: weapons와 같은 순서로 "자동 분해됐는지" (무기 칸이 꽉 차 있었으면 true) → 결과 화면에 "분해됨" 표시.
        public static List<bool> CommitGachaPull(IReadOnlyList<CharacterData> newCharacters, IReadOnlyList<WeaponData> weapons, int gemCost, int level)
        {
            EnsureLoaded();
            foreach (CharacterData character in newCharacters)
            {
                data.characters.Add(CreateCharacterSave(character, level));
            }

            var dismantled = new List<bool>();
            foreach (WeaponData weapon in weapons)
            {
                dismantled.Add(!StoreWeaponOrDismantle(weapon));
            }

            data.currency.gems -= gemCost;
            Save();

            // 새 캐릭터가 없으면(전부 무기·중복) 목록이 그대로이므로 알리지 않는다.
            // ?.Invoke: 구독한 화면이 하나도 없으면 아무것도 하지 않는다.
            if (newCharacters.Count > 0)
            {
                OwnedCharactersChanged?.Invoke();
            }

            if (weapons.Count > 0)
            {
                InventoryChanged?.Invoke();
            }

            return dismantled;
        }

        // ---------- 보유 캐릭터 ----------

        public static bool Owns(CharacterData character)
        {
            EnsureLoaded();
            return FindOwnedSave(character.Id) != null;
        }

        // 보유 캐릭터 목록 (세이브 순서 = 획득 순서). 데이터베이스에서 찾지 못한 ID는 건너뛴다.
        public static List<OwnedCharacter> GetOwnedCharacters()
        {
            EnsureLoaded();
            var owned = new List<OwnedCharacter>();
            foreach (OwnedCharacterSave saved in data.characters)
            {
                CharacterData character = database.FindCharacter(saved.id);
                if (character != null)
                {
                    owned.Add(new OwnedCharacter(character, saved.level, saved.promotion, saved.exp));
                }
            }

            return owned;
        }

        // 보유한 캐릭터의 레벨. 보유하지 않았으면 0.
        public static int GetLevel(CharacterData character)
        {
            EnsureLoaded();
            OwnedCharacterSave saved = FindOwnedSave(character.Id);
            return saved != null ? saved.level : 0;
        }

        // ---------- 편성 ----------

        // 저장된 편성을 칸 순서대로 돌려준다. 빈 칸·보유하지 않은 캐릭터 칸은 null.
        public static List<CharacterData> GetParty()
        {
            EnsureLoaded();
            var party = new List<CharacterData>();
            foreach (string id in data.party)
            {
                bool isOwned = id != null && FindOwnedSave(id) != null;
                party.Add(isOwned ? database.FindCharacter(id) : null);
            }

            return party;
        }

        // 편성을 칸 순서대로 저장한다 (빈 칸은 null).
        public static void SetParty(IReadOnlyList<CharacterData> party)
        {
            EnsureLoaded();
            data.party.Clear();
            foreach (CharacterData character in party)
            {
                // ?.: character가 null(빈 칸)이면 Id를 읽지 않고 null을 넣는다.
                data.party.Add(character?.Id);
            }

            Save();
        }

        // ---------- 스테이지 ----------

        public static bool IsStageCleared(StageData stage)
        {
            EnsureLoaded();
            StageRecordSave record = FindStageRecord(stage.Id);
            return record != null && record.cleared;
        }

        // 스테이지 클리어를 기록하고 저장한다. 이미 클리어한 스테이지면 저장하지 않는다.
        public static void RecordStageClear(StageData stage)
        {
            EnsureLoaded();
            StageRecordSave record = FindStageRecord(stage.Id);
            if (record != null && record.cleared)
            {
                return;
            }

            if (record == null)
            {
                record = new StageRecordSave { id = stage.Id };
                data.stages.Add(record);
            }

            record.cleared = true;
            Save();
        }

        // ---------- 불러오기·저장 ----------

        // 아직 불러오지 않았으면 데이터베이스와 세이브를 불러온다. 세이브가 없으면 새 게임을 만들어 바로 저장한다.
        private static void EnsureLoaded()
        {
            if (data != null)
            {
                return;
            }

            // Resources.Load: Resources 폴더 안의 에셋을 이름으로 불러온다 (씬에 연결하지 않아도 됨).
            database = Resources.Load<GameDatabase>(DatabaseResourceName);
            if (database == null)
            {
                Debug.LogError($"[Save] Resources 폴더에 {DatabaseResourceName} 에셋이 없습니다.");
            }

            if (!SaveStorage.TryLoad(out data))
            {
                data = CreateNewGame();
                Save();
            }

            FixPromotionsForLevel();
        }

        // 진급 기능보다 먼저 Lv60으로 지급된 캐릭터는 진급 0회로 저장돼 있다 → 레벨에 필요한 진급 횟수로 올려 준다.
        // 이렇게 하지 않으면 Lv60인데 상한이 10으로 보인다. 고친 캐릭터가 있을 때만 저장한다.
        private static void FixPromotionsForLevel()
        {
            GrowthRulesData rules = database != null ? database.GrowthRules : null;
            if (rules == null)
            {
                return;
            }

            bool changed = false;
            foreach (OwnedCharacterSave saved in data.characters)
            {
                int minPromotion = rules.GetMinPromotion(saved.level);
                if (saved.promotion < minPromotion)
                {
                    saved.promotion = minPromotion;
                    changed = true;
                }
            }

            if (changed)
            {
                Save();
            }
        }

        // 새로 얻은 캐릭터의 세이브 항목. 지급 레벨에 맞는 진급 횟수도 함께 채운다 (예: Lv60 지급 → 진급 5회).
        private static OwnedCharacterSave CreateCharacterSave(CharacterData character, int level)
        {
            GrowthRulesData rules = database != null ? database.GrowthRules : null;
            int promotion = rules != null ? rules.GetMinPromotion(level) : 0;
            // 객체 초기화자 { id = ..., level = ... }: 객체를 만들면서 필드 값을 바로 채운다.
            return new OwnedCharacterSave { id = character.Id, level = level, promotion = promotion };
        }

        // 새 게임: 데이터베이스의 시작 캐릭터를 지급하고, 앞에서부터 편성 칸에 넣는다.
        private static SaveData CreateNewGame()
        {
            var newData = new SaveData();
            foreach (StartingCharacter starting in database.StartingCharacters)
            {
                newData.characters.Add(CreateCharacterSave(starting.Character, starting.Level));

                if (newData.party.Count < FormationSize)
                {
                    newData.party.Add(starting.Character.Id);
                }
            }

            Debug.Log("[Save] 세이브가 없어 새 게임을 시작합니다.");
            return newData;
        }

        private static void Save()
        {
            SaveStorage.Save(data);
        }

        private static OwnedCharacterSave FindOwnedSave(string id)
        {
            foreach (OwnedCharacterSave saved in data.characters)
            {
                if (saved.id == id)
                {
                    return saved;
                }
            }

            return null;
        }

        private static StageRecordSave FindStageRecord(string id)
        {
            foreach (StageRecordSave record in data.stages)
            {
                if (record.id == id)
                {
                    return record;
                }
            }

            return null;
        }
    }
}
