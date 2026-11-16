using System.Collections.Generic;
using Game.Core;
using Game.Data;

namespace Game.Outgame
{
    // 모집 화면의 1회·10회 진행을 처리한다: 보석 확인 → 횟수만큼 GachaMachine으로 판정 → 새 캐릭터·무기 지급·보석 차감·저장.
    // 판정(GachaMachine)과 저장(PlayerProfile) 사이를 잇는 역할이라, 화면은 이 함수 하나만 부르면 된다.
    public static class GachaService
    {
        // 앱 실행 중 계속 쓰는 난수. 매번 새로 만들면 짧은 시간 안에 같은 시드가 나와 결과가 겹칠 수 있다.
        private static readonly System.Random Random = new System.Random();

        public static GachaRulesData Rules => PlayerProfile.Database.GachaRules;

        // count회 뽑는 데 필요한 보석 (디버그 무료 뽑기면 0)
        public static int GetCost(int count)
        {
            return Rules.FreePulls ? 0 : Rules.GemsPerPull * count;
        }

        public static bool CanPull(int count)
        {
            return PlayerProfile.GetGems() >= GetCost(count);
        }

        // count회 뽑고 결과를 저장한다. 보석이 모자라면 null.
        // 같은 캐릭터가 여러 번 나오면 처음 1개만 NEW이고, 중복은 지금은 아무 효과가 없다.
        public static List<GachaResult> Pull(GachaBannerData banner, int count)
        {
            if (!CanPull(count))
            {
                return null;
            }

            GameDatabase database = PlayerProfile.Database;
            var machine = new GachaMachine(
                Rules,
                Random,
                database.GetCharactersOfGrade(CharacterGrade.S),
                database.GetCharactersOfGrade(CharacterGrade.A),
                database.GetWeaponsOfGrade);

            // 천장 카운터는 세이브의 값을 machine이 직접 갱신하고, 아래 CommitGachaPull에서 함께 저장한다.
            GachaSave state = PlayerProfile.GachaState;
            var results = new List<GachaResult>();
            var obtained = new List<CharacterData>();
            var weaponResults = new List<GachaResult>();

            for (int i = 0; i < count; i++)
            {
                GachaResult result = machine.Pull(banner, state);
                if (result.Character != null && !PlayerProfile.Owns(result.Character) && !obtained.Contains(result.Character))
                {
                    result.IsNew = true;
                    obtained.Add(result.Character);
                }

                if (result.Weapon != null)
                {
                    weaponResults.Add(result);
                }

                results.Add(result);
            }

            // ConvertAll(람다): 목록의 원소마다 람다 결과를 모은 새 목록 → 무기 결과에서 무기 데이터만 꺼낸다.
            List<WeaponData> weapons = weaponResults.ConvertAll(result => result.Weapon);
            List<bool> dismantled = PlayerProfile.CommitGachaPull(obtained, weapons, GetCost(count), Rules.ObtainedLevel);

            // 저장 결과(무기 칸이 꽉 차 자동 분해됐는지)를 무기 결과에 같은 순서로 표시한다.
            for (int i = 0; i < weaponResults.Count; i++)
            {
                weaponResults[i].IsDismantled = dismantled[i];
            }

            return results;
        }
    }
}
