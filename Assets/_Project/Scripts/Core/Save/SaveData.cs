using System;
using System.Collections.Generic;

namespace Game.Core
{
    // 세이브 파일(JSON)에 그대로 저장되는 내용 (기획서 9장).
    // 데이터 에셋(SO)은 저장하지 않고 ID와 플레이 중 바뀌는 값만 담는다. 불러올 때 GameDatabase가 ID로 에셋을 찾는다.
    // JSON 변환(Newtonsoft)은 public 필드를 이름 그대로 저장·복원하므로, 필드 이름을 바꾸면 기존 세이브의 값을 못 읽는다.
    //  → 이름을 바꾸거나 구조를 고칠 때는 CurrentVersion을 올리고 SaveStorage.Upgrade에 변환을 추가한다.
    [Serializable]
    public class SaveData
    {
        // 2: 무기 인벤(weapons, nextWeaponUid)·재료 인벤(materials) 추가
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public CurrencySave currency = new CurrencySave();
        public List<OwnedCharacterSave> characters = new List<OwnedCharacterSave>();
        public GachaSave gacha = new GachaSave();
        public List<StageRecordSave> stages = new List<StageRecordSave>();

        // 편성 칸 순서대로 캐릭터 ID. 빈 칸은 null로 저장해 칸 위치를 유지한다.
        public List<string> party = new List<string>();

        // 무기 인벤토리: 개체 하나가 한 칸 (같은 무기라도 개체마다 레벨이 다르다, 기획서 9장)
        public List<WeaponSave> weapons = new List<WeaponSave>();

        // 다음에 얻는 무기 개체에 붙일 고유 번호. 분해로 지운 번호를 다시 쓰지 않도록 계속 늘어나기만 한다.
        public int nextWeaponUid = 1;

        // 재료 인벤토리: 종류마다 개수 하나 (칸 제한 없음)
        public List<MaterialSave> materials = new List<MaterialSave>();
    }

    // 보유 무기 개체 하나. uid로 개체를 구분하고, id로 무기 종류(WeaponData)를 찾는다.
    [Serializable]
    public class WeaponSave
    {
        public int uid;
        public string id;
        public int level = 1;
        public int promotion;

        // 장착한 캐릭터 ID. 장착하지 않았으면 null. 장착 중인 무기는 일괄 분해에서 빠진다.
        public string equippedTo;
    }

    // 보유 재료 한 종류의 개수.
    [Serializable]
    public class MaterialSave
    {
        public string id;
        public int count;
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

        // A 이상이 나오지 않고 연속으로 뽑은 횟수 (0~9, 10회째 A 확정). S가 나와도 A 확정을 채운 것으로 보고 0으로 되돌린다
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
