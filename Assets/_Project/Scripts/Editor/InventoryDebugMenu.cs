using Game.Core;
using Game.Data;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    // 인벤토리 확인용 에디터 메뉴 (Tools > Inventory). 재료를 얻는 곳이 아직 없어 직접 넣어 본다.
    // 세이브 파일에 바로 저장되므로, 플레이 중에 누르면 열린 인벤토리 화면에도 바로 반영된다 (InventoryChanged).
    public static class InventoryDebugMenu
    {
        // 데이터베이스의 무기를 종류마다 2개씩, 재료를 종류마다 50개씩 넣는다.
        [MenuItem("Tools/Inventory/테스트 아이템 지급")]
        private static void GiveTestItems()
        {
            GameDatabase database = PlayerProfile.Database;
            foreach (WeaponGrade grade in new[] { WeaponGrade.ThreeStar, WeaponGrade.FourStar, WeaponGrade.FiveStar })
            {
                foreach (WeaponData weapon in database.GetWeaponsOfGrade(grade))
                {
                    PlayerProfile.AddWeapon(weapon);
                    PlayerProfile.AddWeapon(weapon);
                }
            }

            foreach (MaterialData material in database.Materials)
            {
                PlayerProfile.AddMaterial(material, 50);
            }

            Debug.Log($"[Inventory] 테스트 아이템 지급: 무기 {PlayerProfile.WeaponCount} / {PlayerProfile.WeaponCapacity}");
        }

        // 무기 칸이 꽉 찰 때까지 3성 무기를 넣는다 (자동 분해 확인용: 이후 가챠에서 나온 3성은 "자동 분해").
        [MenuItem("Tools/Inventory/무기 칸 가득 채우기 (3성)")]
        private static void FillWeapons()
        {
            var threeStars = PlayerProfile.Database.GetWeaponsOfGrade(WeaponGrade.ThreeStar);
            if (threeStars.Count == 0)
            {
                Debug.LogWarning("[Inventory] GameDatabase에 3성 무기가 없습니다.");
                return;
            }

            int index = 0;
            while (PlayerProfile.WeaponCount < PlayerProfile.WeaponCapacity)
            {
                // %: 나머지 → 3성 무기 목록을 처음부터 돌아가며 넣는다.
                PlayerProfile.AddWeapon(threeStars[index % threeStars.Count]);
                index++;
            }

            Debug.Log($"[Inventory] 무기 칸을 채웠습니다: {PlayerProfile.WeaponCount} / {PlayerProfile.WeaponCapacity}");
        }
    }
}
