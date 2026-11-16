using System.Collections.Generic;
using Game.Core;
using Game.Data;

namespace Game.UI
{
    // 육성 창(GrowthPopup)이 키우는 대상 하나 (캐릭터 또는 무기).
    // 전략 패턴: 창은 대상이 무엇인지 몰라도 이 인터페이스로 상태를 읽고 레벨업·진급을 요청한다
    //  → 캐릭터 육성과 무기 강화가 같은 창·같은 계산(재료 → 경험치 → 상한에서 진급)을 그대로 쓴다.
    public interface IGrowthTarget
    {
        // 창 제목에 붙는 이름 ("전투원" / "무기")
        string Label { get; }

        // 필요 경험치·재료·진급 규칙 (캐릭터용 / 무기용)
        GrowthRulesData Rules { get; }

        int Level { get; }
        int Exp { get; }
        int Promotion { get; }

        // 세이브에서 지금 상태를 다시 읽는다. 대상이 사라졌으면(무기 분해 등) false.
        bool Reload();

        // 그 레벨·진급에서의 HP·공격력·방어력 (미리 보기용). 튜플 (int, int, int): 이름 붙은 값 셋을 한 번에 돌려준다.
        (int Hp, int Attack, int Defense) GetStats(int level, int promotion);

        bool LevelUp(IReadOnlyList<int> counts);
        bool Promote();
    }

    // 캐릭터 육성: 캐릭터 성장 규칙, 스탯 = 데이터 스탯(Lv1) × 아군 레벨·진급 배율 (무기 제외).
    public class CharacterGrowthTarget : IGrowthTarget
    {
        private readonly CharacterData character;
        private OwnedCharacter owned;

        public CharacterGrowthTarget(CharacterData character)
        {
            this.character = character;
        }

        public string Label => "전투원";
        public GrowthRulesData Rules => PlayerProfile.GrowthRules;
        public int Level => owned.Level;
        public int Exp => owned.Exp;
        public int Promotion => owned.Promotion;

        public bool Reload()
        {
            owned = PlayerProfile.GetOwned(character);
            return owned != null;
        }

        // 공격력·방어력은 전투처럼 (int)로 버린다.
        public (int Hp, int Attack, int Defense) GetStats(int level, int promotion)
        {
            StatScaling scaling = PlayerProfile.Database.BattleRules.GetAllyScaling(level, promotion);
            return ((int)(character.MaxHp * (double)scaling.Hp),
                    (int)(character.Attack * (double)scaling.Attack),
                    (int)(character.Defense * (double)scaling.Defense));
        }

        public bool LevelUp(IReadOnlyList<int> counts) => PlayerProfile.LevelUp(character, counts);
        public bool Promote() => PlayerProfile.Promote(character);
    }

    // 무기 강화: 무기 성장 규칙, 스탯 = 무기 최대 스탯 × 무기 성장 비율.
    public class WeaponGrowthTarget : IGrowthTarget
    {
        private readonly int uid;
        private OwnedWeapon weapon;

        public WeaponGrowthTarget(int uid)
        {
            this.uid = uid;
        }

        public string Label => "무기";
        public GrowthRulesData Rules => PlayerProfile.WeaponGrowthRules;
        public int Level => weapon.Level;
        public int Exp => weapon.Exp;
        public int Promotion => weapon.Promotion;

        public bool Reload()
        {
            weapon = PlayerProfile.GetWeapon(uid);
            return weapon != null;
        }

        public (int Hp, int Attack, int Defense) GetStats(int level, int promotion)
        {
            WeaponStats stats = WeaponStats.Calculate(weapon.Data, level, promotion, PlayerProfile.Database.BattleRules);
            return (stats.MaxHp, stats.Attack, stats.Defense);
        }

        public bool LevelUp(IReadOnlyList<int> counts) => PlayerProfile.LevelUpWeapon(uid, counts);
        public bool Promote() => PlayerProfile.PromoteWeapon(uid);
    }
}
