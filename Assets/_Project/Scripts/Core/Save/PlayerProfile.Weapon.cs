using System.Collections.Generic;
using Game.Data;

namespace Game.Core
{
    // 무기 장착·성장 (기획서 5장). 무기 종류마다 낄 수 있는 직군이 정해져 있고, 캐릭터 한 명은 무기 하나만 낀다.
    // 성장은 캐릭터와 같은 방식(경험치 재료 → 레벨업, 10레벨마다 진급)이고 규칙만 무기용(WeaponGrowthRules)을 쓴다.
    // partial: PlayerProfile의 무기 부분만 이 파일에 적는다. 바꾼 직후 저장하고 화면에 알린다.
    public static partial class PlayerProfile
    {
        public static GrowthRulesData WeaponGrowthRules => Database.WeaponGrowthRules;

        // 개체 번호로 보유 무기 하나. 없으면 null.
        public static OwnedWeapon GetWeapon(int uid)
        {
            EnsureLoaded();
            WeaponSave saved = FindWeaponSave(uid);
            WeaponData weapon = saved != null ? database.FindWeapon(saved.id) : null;
            return weapon != null ? ToOwnedWeapon(saved, weapon) : null;
        }

        // 그 캐릭터가 낀 무기. 없으면 null.
        public static OwnedWeapon GetEquippedWeapon(CharacterData character)
        {
            EnsureLoaded();
            WeaponSave saved = data.weapons.Find(weapon => weapon.equippedTo == character.Id);
            WeaponData weaponData = saved != null ? database.FindWeapon(saved.id) : null;
            return weaponData != null ? ToOwnedWeapon(saved, weaponData) : null;
        }

        // 그 캐릭터가 낄 수 있는 보유 무기 (직군에 맞는 종류만). FindAll(조건 람다): 조건이 참인 것만 모은다.
        public static List<OwnedWeapon> GetEquippableWeapons(CharacterData character)
        {
            return GetOwnedWeapons().FindAll(weapon => weapon.Data.CanEquip(character));
        }

        // 그 무기가 지금 더해 주는 스탯 (레벨·진급 반영).
        public static WeaponStats GetWeaponStats(OwnedWeapon weapon)
        {
            return weapon != null
                ? WeaponStats.Calculate(weapon.Data, weapon.Level, weapon.Promotion, Database.BattleRules)
                : WeaponStats.None;
        }

        // 그 캐릭터가 낀 무기의 스탯 (안 꼈으면 0).
        public static WeaponStats GetEquippedWeaponStats(CharacterData character)
        {
            return GetWeaponStats(GetEquippedWeapon(character));
        }

        // 무기를 캐릭터에게 낀다. 직군이 맞지 않으면 false.
        // 캐릭터가 이미 낀 무기는 빠지고, 다른 캐릭터가 끼고 있던 무기면 그 캐릭터에게서 옮겨 온다 (한 무기 = 한 명).
        public static bool EquipWeapon(CharacterData character, int uid)
        {
            EnsureLoaded();
            WeaponSave target = FindWeaponSave(uid);
            WeaponData weapon = target != null ? database.FindWeapon(target.id) : null;
            if (weapon == null || !weapon.CanEquip(character))
            {
                return false;
            }

            UnequipSilently(character);
            target.equippedTo = character.Id;
            SaveWeapons();
            return true;
        }

        // 캐릭터가 낀 무기를 뺀다. 낀 무기가 없으면 false.
        public static bool UnequipWeapon(CharacterData character)
        {
            EnsureLoaded();
            if (!UnequipSilently(character))
            {
                return false;
            }

            SaveWeapons();
            return true;
        }

        // 무기 레벨업 (캐릭터 LevelUp과 같은 규칙, 재료는 무기 성장 규칙의 재료).
        public static bool LevelUpWeapon(int uid, IReadOnlyList<int> counts)
        {
            EnsureLoaded();
            WeaponSave saved = FindWeaponSave(uid);
            GrowthRulesData rules = WeaponGrowthRules;
            if (saved == null || !TryApplyExp(rules, counts, saved.level, saved.exp, saved.promotion, out ExpResult result))
            {
                return false;
            }

            saved.level = result.Level;
            saved.exp = result.Exp;

            // 업적: 누적 무기 강화 횟수 (레벨업 버튼 1번 = 1회). 레벨업과 함께 저장된다.
            AddCounter(AchievementCondition.WeaponEnhances, 1);
            SaveWeapons();
            return true;
        }

        // 무기 진급 (레벨 상한에서, 무기 개조 키트를 써서).
        public static bool PromoteWeapon(int uid)
        {
            EnsureLoaded();
            WeaponSave saved = FindWeaponSave(uid);
            GrowthRulesData rules = WeaponGrowthRules;
            if (saved == null || !rules.CanPromote(saved.level, saved.promotion) || !TryPayPromotion(rules, saved.promotion))
            {
                return false;
            }

            saved.promotion++;
            SaveWeapons();
            return true;
        }

        // ---------- 내부 처리 ----------

        private static OwnedWeapon ToOwnedWeapon(WeaponSave saved, WeaponData weapon)
        {
            return new OwnedWeapon(saved.uid, weapon, saved.level, saved.promotion, saved.exp, saved.equippedTo);
        }

        private static WeaponSave FindWeaponSave(int uid)
        {
            return data.weapons.Find(weapon => weapon.uid == uid);
        }

        // 캐릭터가 낀 무기를 빼기만 한다 (저장 안 함). 뺀 게 있으면 true.
        private static bool UnequipSilently(CharacterData character)
        {
            bool removed = false;
            foreach (WeaponSave saved in data.weapons)
            {
                if (saved.equippedTo == character.Id)
                {
                    saved.equippedTo = null;
                    removed = true;
                }
            }

            return removed;
        }

        // 저장하고 무기(인벤토리)·캐릭터(장착 스탯) 화면에 모두 알린다. ?.Invoke: 구독한 화면이 없으면 아무것도 하지 않는다.
        private static void SaveWeapons()
        {
            Save();
            InventoryChanged?.Invoke();
            OwnedCharactersChanged?.Invoke();
        }
    }
}
