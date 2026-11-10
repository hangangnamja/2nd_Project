using System;
using System.Collections.Generic;

namespace Game.Core
{
    // 세이브 파일(JSON)에 그대로 저장되는 내용 (기획서 9장).
    // 데이터 에셋(SO)은 저장하지 않고 ID와 플레이 중 바뀌는 값만 담는다. 불러올 때 GameDatabase가 ID로 에셋을 찾는다.
    // JSON 변환(Newtonsoft)은 public 필드를 이름 그대로 저장·복원하므로, 필드 이름을 바꾸면 기존 세이브의 값을 못 읽는다.
    //  → 이름을 바꾸거나 구조를 고칠 때는 CurrentVersion을 올리고 SaveStorage.Upgrade에 변환을 추가한다.
    // 무기(개체 ID·레벨·장착 캐릭터)는 무기 시스템을 만들 때 추가한다.
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public CurrencySave currency = new CurrencySave();
        public List<OwnedCharacterSave> characters = new List<OwnedCharacterSave>();
        public GachaSave gacha = new GachaSave();
        public List<StageRecordSave> stages = new List<StageRecordSave>();

        // 편성 칸 순서대로 캐릭터 ID. 빈 칸은 null로 저장해 칸 위치를 유지한다.
        public List<string> party = new List<string>();
    }

    [Serializable]
    public class CurrencySave
    {
        public int gems;
        public int gold;
    }

    // 보유 캐릭터 한 명: 어떤 캐릭터인지(ID)와 성장 상태.
    [Serializable]
    public class OwnedCharacterSave
    {
        public string id;
        public int level = 1;
        public int promotion;
    }

    // 가챠 카운터 (기획서 7-1). 다음 픽업 배너로 이월되므로 배너가 아니라 플레이어 단위로 저장한다.
    [Serializable]
    public class GachaSave
    {
        // S가 나오지 않고 연속으로 뽑은 횟수 (0~89, 90회째 S 확정)
        public int pityCount;

        // 지난 S가 픽업이 아니었으면 true → 다음 S는 픽업 확정
        public bool pickupGuaranteed;

        // A 확정까지 센 횟수 (0~9, 10회째 A 확정). S가 나왔을 때 초기화할지는 가챠 구현 때 정한다 (기획서 10장 미정)
        public int aGuaranteeCount;
    }

    // 스테이지 하나의 기록. 별 미션은 조건이 정해지면 칸을 추가한다.
    [Serializable]
    public class StageRecordSave
    {
        public string id;
        public bool cleared;
    }
}
