using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Core
{
    // 보유 무기 개체 하나: 무기 데이터 + 세이브에 저장된 개체 값.
    public class OwnedWeapon
    {
        public int Uid { get; }
        public WeaponData Data { get; }
        public int Level { get; }
        public int Promotion { get; }

        // 지금 레벨에서 다음 레벨까지 쌓인 경험치
        public int Exp { get; }

        // 장착한 캐릭터 ID (없으면 null)
        public string EquippedTo { get; }
        public bool IsEquipped => !string.IsNullOrEmpty(EquippedTo);

        public OwnedWeapon(int uid, WeaponData data, int level, int promotion, int exp, string equippedTo)
        {
            Uid = uid;
            Data = data;
            Level = level;
            Promotion = promotion;
            Exp = exp;
            EquippedTo = equippedTo;
        }
    }

    // 보유 재료 한 종류: 재료 데이터 + 개수.
    public class OwnedMaterial
    {
        public MaterialData Data { get; }
        public int Count { get; }

        public OwnedMaterial(MaterialData data, int count)
        {
            Data = data;
            Count = count;
        }
    }

    // 일괄 분해 결과 (또는 분해하면 어떻게 되는지 미리 보기): 분해한 무기 수와 받은 재료.
    public readonly struct DismantleResult
    {
        public int WeaponCount { get; }
        public MaterialData Material { get; }
        public int MaterialAmount { get; }

        public DismantleResult(int weaponCount, MaterialData material, int materialAmount)
        {
            WeaponCount = weaponCount;
            Material = material;
            MaterialAmount = materialAmount;
        }
    }

    // PlayerProfile의 인벤토리 부분 (무기 인벤·재료 인벤).
    // partial: 한 클래스를 여러 파일에 나눠 적는 문법. 세이브(data)는 같이 쓰고, 인벤토리 코드만 이 파일에 모은다.
    //  - 무기: 개체 1개 = 1칸, 칸 수 한도가 있다. 꽉 찬 상태에서 3성 무기가 들어오면 바로 분해해 무기 재료로 바꾼다 (자동 분해)
    //    4·5성은 귀한 무기라 자동 분해하지 않고 칸을 넘어서라도 보관한다
    //  - 재료: 종류별 개수, 칸 제한 없음. 종류당 최대 개수를 넘는 양은 버린다
    public static partial class PlayerProfile
    {
        // 무기·재료 인벤토리가 바뀌어 저장된 직후 알린다 (인벤토리 화면이 목록을 다시 그린다).
        public static event Action InventoryChanged;

        private static InventoryRulesData InventoryRules => database.InventoryRules;

        // ---------- 무기 ----------

        public static int WeaponCount
        {
            get
            {
                EnsureLoaded();
                return data.weapons.Count;
            }
        }

        public static int WeaponCapacity
        {
            get
            {
                EnsureLoaded();
                return InventoryRules.WeaponCapacity;
            }
        }

        // 보유 무기 목록 (세이브 순서 = 획득 순서). 데이터베이스에서 찾지 못한 무기 ID는 건너뛴다.
        public static List<OwnedWeapon> GetOwnedWeapons()
        {
            EnsureLoaded();
            var owned = new List<OwnedWeapon>();
            foreach (WeaponSave saved in data.weapons)
            {
                WeaponData weapon = database.FindWeapon(saved.id);
                if (weapon != null)
                {
                    owned.Add(ToOwnedWeapon(saved, weapon));
                }
            }

            return owned;
        }

        // 무기 하나를 얻는다 (저장까지). 자동 분해됐으면 false.
        public static bool AddWeapon(WeaponData weapon)
        {
            EnsureLoaded();
            bool stored = StoreWeaponOrDismantle(weapon);
            SaveInventory();
            return stored;
        }

        // 고른 등급의 무기를 분해하면 어떻게 되는지 미리 계산한다 (일괄 분해 확인 창 표시용). 세이브는 바꾸지 않는다.
        public static DismantleResult PreviewDismantle(ICollection<WeaponGrade> grades)
        {
            EnsureLoaded();
            List<WeaponSave> targets = FindDismantleTargets(grades);
            return new DismantleResult(targets.Count, InventoryRules.DismantleMaterial, SumDismantleReward(targets));
        }

        // 일괄 분해: 고른 등급(예: 3성·4성)의 무기를 장착 중인 것만 빼고 전부 분해해 무기 재료로 바꾸고 저장한다.
        public static DismantleResult DismantleWeapons(ICollection<WeaponGrade> grades)
        {
            EnsureLoaded();
            List<WeaponSave> targets = FindDismantleTargets(grades);
            int reward = SumDismantleReward(targets);
            if (targets.Count == 0)
            {
                return new DismantleResult(0, InventoryRules.DismantleMaterial, 0);
            }

            // RemoveAll(조건 람다): 조건이 참인 원소를 한 번에 지운다 → 분해 대상 목록에 든 개체만 지운다.
            data.weapons.RemoveAll(saved => targets.Contains(saved));
            AddMaterialAmount(InventoryRules.DismantleMaterial, reward);
            SaveInventory();
            return new DismantleResult(targets.Count, InventoryRules.DismantleMaterial, reward);
        }

        // ---------- 재료 ----------

        // 재화 재료의 ID (MaterialData의 ID 칸과 같아야 한다). 개수는 재료 목록이 아니라 세이브의 재화 값에서 읽는다.
        public const string GoldId = "gold";
        public const string GemsId = "gems";

        // 보유 재료 목록 (개수가 1 이상인 것만). 데이터베이스에 등록된 순서로 돌려준다.
        // 재화(골드·보석)는 0개여도 늘 보여준다 (인벤토리에서 재화를 확인하는 자리이므로).
        public static List<OwnedMaterial> GetOwnedMaterials()
        {
            EnsureLoaded();
            var owned = new List<OwnedMaterial>();
            foreach (MaterialData material in database.Materials)
            {
                if (material == null)
                {
                    continue;
                }

                int count = GetMaterialCount(material);
                if (count > 0 || material.Category == MaterialCategory.Currency)
                {
                    owned.Add(new OwnedMaterial(material, count));
                }
            }

            return owned;
        }

        public static int GetMaterialCount(MaterialData material)
        {
            EnsureLoaded();
            if (material != null && material.Category == MaterialCategory.Currency)
            {
                return GetCurrencyAmount(material.Id);
            }

            // ?.: 저장 기록이 없으면(null) count를 읽지 않고 null → ?? 0으로 0개.
            return FindMaterialSave(material?.Id)?.count ?? 0;
        }

        // 재화 재료의 개수 = 세이브의 재화 값 (골드·보석). 모르는 ID면 0.
        private static int GetCurrencyAmount(string id)
        {
            return id switch
            {
                GoldId => data.currency.gold,
                GemsId => data.currency.gems,
                _ => 0,
            };
        }

        // 재료를 amount개 얻는다 (저장까지). 최대 개수를 넘는 양은 버린다.
        public static void AddMaterial(MaterialData material, int amount)
        {
            EnsureLoaded();
            AddMaterialAmount(material, amount);
            SaveInventory();
        }

        // ---------- 내부 처리 ----------

        // 무기 칸에 넣는다. 칸이 꽉 찼고 3성이면 넣지 않고 분해 보상만 준다 → false.
        // 저장은 하지 않는다: 가챠 10회처럼 여러 개를 넣고 한 번에 저장하기 위해.
        private static bool StoreWeaponOrDismantle(WeaponData weapon)
        {
            bool isFull = data.weapons.Count >= InventoryRules.WeaponCapacity;
            if (isFull && weapon.Grade == WeaponGrade.ThreeStar)
            {
                AddMaterialAmount(InventoryRules.DismantleMaterial, InventoryRules.GetDismantleReward(weapon.Grade));
                return false;
            }

            // nextWeaponUid++: 지금 번호를 쓰고 나서 1 늘린다 → 개체마다 다른 번호.
            data.weapons.Add(new WeaponSave { uid = data.nextWeaponUid++, id = weapon.Id });
            return true;
        }

        // 분해 대상: 고른 등급이고 장착하지 않은 무기. 무기 ID를 찾지 못한 개체(삭제된 무기)는 건드리지 않는다.
        private static List<WeaponSave> FindDismantleTargets(ICollection<WeaponGrade> grades)
        {
            return data.weapons.FindAll(saved =>
            {
                WeaponData weapon = database.FindWeapon(saved.id);
                return weapon != null && grades.Contains(weapon.Grade) && string.IsNullOrEmpty(saved.equippedTo);
            });
        }

        private static int SumDismantleReward(List<WeaponSave> targets)
        {
            int total = 0;
            foreach (WeaponSave saved in targets)
            {
                total += InventoryRules.GetDismantleReward(database.FindWeapon(saved.id).Grade);
            }

            return total;
        }

        // 재료 개수를 늘린다. Math.Min: 최대 개수를 넘지 않게 자른다. 저장은 호출한 쪽이 한다.
        private static void AddMaterialAmount(MaterialData material, int amount)
        {
            if (material == null || amount <= 0)
            {
                return;
            }

            // 재화는 재료 목록이 아니라 재화 값에 더한다 (재료 최대 개수 제한도 받지 않음).
            if (material.Category == MaterialCategory.Currency)
            {
                if (material.Id == GoldId)
                {
                    data.currency.gold += amount;
                }
                else if (material.Id == GemsId)
                {
                    data.currency.gems += amount;
                }

                return;
            }

            MaterialSave saved = FindMaterialSave(material.Id);
            if (saved == null)
            {
                saved = new MaterialSave { id = material.Id };
                data.materials.Add(saved);
            }

            saved.count = Math.Min(InventoryRules.MaterialMaxCount, saved.count + amount);
        }

        private static MaterialSave FindMaterialSave(string id)
        {
            return id == null ? null : data.materials.Find(saved => saved.id == id);
        }

        // 저장하고 인벤토리 화면에 알린다. ?.Invoke: 구독한 화면이 없으면 아무것도 하지 않는다.
        private static void SaveInventory()
        {
            Save();
            InventoryChanged?.Invoke();
        }
    }
}
