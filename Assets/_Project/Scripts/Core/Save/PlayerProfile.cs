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

        public OwnedCharacter(CharacterData data, int level, int promotion)
        {
            Data = data;
            Level = level;
            Promotion = promotion;
        }
    }

    // 플레이어의 저장 상태(세이브)를 들고 각 화면에 제공하는 곳. static이라 씬이 바뀌어도 그대로 유지된다.
    //  - 처음 쓰일 때 세이브를 불러오고, 없으면 새 게임을 만든다 → 어느 씬을 먼저 실행해도 동작한다
    //  - 값을 바꾸는 함수는 바꾼 직후 바로 저장한다 (기획서 9장: 변경 직후 즉시 저장)
    //  - 화면은 세이브 구조(SaveData)를 직접 만지지 않고, 이 함수들을 거쳐 데이터 에셋으로 받는다
    public static class PlayerProfile
    {
        // 편성 칸 수 (기획서 2장 4인 파티)
        public const int FormationSize = 4;

        // Resources 폴더 안의 GameDatabase 에셋 이름
        private const string DatabaseResourceName = "GameDatabase";

        private static SaveData data;
        private static GameDatabase database;

        // 이 프로젝트는 플레이 시작 때 static 값을 지우는 과정(도메인 리로드)을 꺼 두었으므로,
        // 플레이할 때마다 직접 비워 세이브를 다시 읽게 한다. (에디터에서 세이브를 지운 뒤 다시 플레이해도 반영됨)
        // RuntimeInitializeOnLoadMethod(SubsystemRegistration): 플레이 시작 시 씬을 불러오기 전에 Unity가 이 함수를 자동 호출한다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            data = null;
            database = null;
        }

        // ---------- 보유 캐릭터 ----------

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
                    owned.Add(new OwnedCharacter(character, saved.level, saved.promotion));
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
        }

        // 새 게임: 데이터베이스의 시작 캐릭터를 지급하고, 앞에서부터 편성 칸에 넣는다.
        private static SaveData CreateNewGame()
        {
            var newData = new SaveData();
            foreach (StartingCharacter starting in database.StartingCharacters)
            {
                // 객체 초기화자 { id = ..., level = ... }: 객체를 만들면서 필드 값을 바로 채운다.
                newData.characters.Add(new OwnedCharacterSave { id = starting.Character.Id, level = starting.Level });

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
