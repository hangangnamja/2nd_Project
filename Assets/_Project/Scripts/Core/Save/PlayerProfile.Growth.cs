using System.Collections.Generic;
using Game.Data;

namespace Game.Core
{
    // 캐릭터 성장 (기획서 5장): 경험치 재료로 레벨업, 레벨 상한에서 진급.
    // partial: PlayerProfile의 성장 부분만 이 파일에 적는다. 바꾼 직후 저장하고 OwnedCharactersChanged로 화면에 알린다.
    public static partial class PlayerProfile
    {
        public static GrowthRulesData GrowthRules => Database.GrowthRules;

        // 보유한 캐릭터 한 명의 성장 상태. 보유하지 않았으면 null.
        public static OwnedCharacter GetOwned(CharacterData character)
        {
            EnsureLoaded();
            OwnedCharacterSave saved = FindOwnedSave(character.Id);
            return saved != null ? new OwnedCharacter(character, saved.level, saved.promotion, saved.exp) : null;
        }

        // 육성 창에서 쓸 수 있는 재료 개수. 디버그 "재료 무한"이면 제한 없음(int.MaxValue).
        public static int GetUsableMaterialCount(MaterialData material)
        {
            return GrowthRules.InfiniteMaterials ? int.MaxValue : GetMaterialCount(material);
        }

        // 경험치 재료를 써서 레벨업한다. counts[i] = 성장 규칙의 i번째 재료를 몇 개 쓰는지.
        // 재료가 모자라면 아무것도 바꾸지 않고 false. 레벨 상한을 넘는 경험치는 버린다 (기획: 초과분 무시).
        public static bool LevelUp(CharacterData character, IReadOnlyList<int> counts)
        {
            EnsureLoaded();
            OwnedCharacterSave saved = FindOwnedSave(character.Id);
            GrowthRulesData rules = GrowthRules;
            if (saved == null || !HasMaterials(rules, counts))
            {
                return false;
            }

            int totalExp = 0;
            for (int i = 0; i < counts.Count && i < rules.ExpMaterials.Count; i++)
            {
                totalExp += counts[i] * rules.ExpMaterials[i].Experience;
            }

            if (totalExp <= 0)
            {
                return false;
            }

            ConsumeMaterials(rules, counts);

            ExpResult result = rules.AddExp(saved.level, saved.exp, rules.GetLevelCap(saved.promotion), totalExp);
            saved.level = result.Level;
            saved.exp = result.Exp;

            SaveGrowth(materialsChanged: !rules.InfiniteMaterials);
            return true;
        }

        // 레벨 상한에 도달했으면 진급한다 (지금은 재화 없이 바로). 상한이 10 늘어난다.
        public static bool Promote(CharacterData character)
        {
            EnsureLoaded();
            OwnedCharacterSave saved = FindOwnedSave(character.Id);
            if (saved == null || !GrowthRules.CanPromote(saved.level, saved.promotion))
            {
                return false;
            }

            saved.promotion++;
            SaveGrowth(materialsChanged: false);
            return true;
        }

        // 테스트용: 보유 캐릭터를 전부 Lv1·진급 0회·경험치 0으로 되돌린다 (에디터 메뉴 Tools > Growth).
        // 지금은 캐릭터가 Lv60으로 지급되므로, 레벨업·진급을 확인하려면 먼저 낮춰야 한다.
        public static void ResetAllGrowthForTest()
        {
            EnsureLoaded();
            foreach (OwnedCharacterSave saved in data.characters)
            {
                saved.level = 1;
                saved.promotion = 0;
                saved.exp = 0;
            }

            SaveGrowth(materialsChanged: false);
        }

        // 고른 개수만큼 재료를 갖고 있는지. 재료 무한이면 항상 true.
        private static bool HasMaterials(GrowthRulesData rules, IReadOnlyList<int> counts)
        {
            if (rules.InfiniteMaterials)
            {
                return true;
            }

            for (int i = 0; i < counts.Count && i < rules.ExpMaterials.Count; i++)
            {
                if (counts[i] > GetMaterialCount(rules.ExpMaterials[i].Material))
                {
                    return false;
                }
            }

            return true;
        }

        // 쓴 재료를 줄인다. 재료 무한이면 줄이지 않는다.
        private static void ConsumeMaterials(GrowthRulesData rules, IReadOnlyList<int> counts)
        {
            if (rules.InfiniteMaterials)
            {
                return;
            }

            for (int i = 0; i < counts.Count && i < rules.ExpMaterials.Count; i++)
            {
                // ?.: 저장 기록이 없으면(null) 건너뛴다. HasMaterials를 통과했으므로 실제로는 있다.
                MaterialSave materialSave = FindMaterialSave(rules.ExpMaterials[i].Material?.Id);
                if (materialSave != null)
                {
                    materialSave.count -= counts[i];
                }
            }
        }

        // 저장하고 캐릭터(·재료) 화면에 알린다. ?.Invoke: 구독한 화면이 없으면 아무것도 하지 않는다.
        private static void SaveGrowth(bool materialsChanged)
        {
            Save();
            OwnedCharactersChanged?.Invoke();
            if (materialsChanged)
            {
                InventoryChanged?.Invoke();
            }
        }
    }
}
