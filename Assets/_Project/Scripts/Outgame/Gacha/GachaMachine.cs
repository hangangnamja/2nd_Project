using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;

namespace Game.Outgame
{
    // 뽑기 결과의 등급. 화면 표시는 3성(파랑) / 4성(보라) / 5성(황금빛).
    // 숫자가 클수록 높은 등급이라, 결과 중 최고 등급을 비교로 구할 수 있다.
    public enum GachaRarity
    {
        ThreeStar = 3,  // 3성 무기
        FourStar = 4,   // A 캐릭터
        FiveStar = 5,   // S 캐릭터
    }

    // 뽑기 1회의 결과.
    public class GachaResult
    {
        public GachaRarity Rarity { get; }

        // 나온 캐릭터. 3성 무기면 null.
        public CharacterData Character { get; }

        // 배너의 픽업 캐릭터인지 (5성만 해당)
        public bool IsPickup { get; }

        // 이번에 처음 얻은 캐릭터인지 (GachaService가 보유 여부를 보고 채운다)
        public bool IsNew { get; set; }

        // 나온 무기 (3성일 때). 등록된 3성 무기가 없으면 null.
        public WeaponData Weapon { get; }

        // 무기 칸이 꽉 차 있어 받자마자 무기 재료로 자동 분해됐는지 (GachaService가 저장 결과를 보고 채운다)
        public bool IsDismantled { get; set; }

        public GachaResult(GachaRarity rarity, CharacterData character, bool isPickup, WeaponData weapon = null)
        {
            Rarity = rarity;
            Character = character;
            IsPickup = isPickup;
            Weapon = weapon;
        }
    }

    // 확률·천장 규칙대로 1회 뽑기를 판정한다 (기획서 7-1). 저장·화면은 모르고 판정만 하는 순수 C# 클래스.
    // 카운터(천장·반천·A 확정)는 넘겨받은 GachaSave를 직접 갱신한다 → 저장은 호출한 쪽(GachaService)이 한다.
    // 난수(System.Random)를 밖에서 받으므로, 시드를 고정하면 같은 결과를 재현하거나 시뮬레이션할 수 있다.
    public class GachaMachine
    {
        private readonly GachaRulesData rules;
        private readonly Random random;
        private readonly List<CharacterData> sCharacters;
        private readonly List<CharacterData> aCharacters;
        private readonly List<WeaponData> threeStarWeapons;

        // threeStarWeapons: 3성 결과에서 고를 무기 목록 (비어 있으면 무기 없이 3성 결과만 나온다)
        public GachaMachine(GachaRulesData rules, Random random, List<CharacterData> sCharacters, List<CharacterData> aCharacters, List<WeaponData> threeStarWeapons)
        {
            this.rules = rules;
            this.random = random;
            this.sCharacters = sCharacters;
            this.aCharacters = aCharacters;
            this.threeStarWeapons = threeStarWeapons;
        }

        // 1회 뽑기. 난수 하나(0~1)를 확률 구간에 대어 등급을 정한다:
        //   [0, S 확률) → S / [S 확률, S 확률 + A 확률) → A / 나머지 → 3성 무기
        // S가 아니어도 A 확정 차례(A 이상 없이 10번째)면 A로 올린다.
        public GachaResult Pull(GachaBannerData banner, GachaSave state)
        {
            // pityCount = S 없이 이미 뽑은 횟수 → 이번은 그다음(+1)번째 뽑기
            float sRate = rules.GetSRate(state.pityCount + 1);

            // NextDouble: 0 이상 1 미만의 난수
            double roll = random.NextDouble();
            if (roll < sRate)
            {
                return PullFiveStar(banner, state);
            }

            bool isAGuaranteed = state.aGuaranteeCount + 1 >= rules.AGuaranteeInterval;
            if (isAGuaranteed || roll < sRate + rules.ARate)
            {
                return PullFourStar(state);
            }

            return PullThreeStar(state);
        }

        // S: 천장과 A 확정 카운터를 모두 0으로 (S도 "A 이상"이라 A 확정을 채운 것으로 본다).
        // 반천: 픽업 확정이면 픽업, 아니면 확률로 픽업/다른 S. 다른 S가 나오면 다음 S는 픽업 확정.
        private GachaResult PullFiveStar(GachaBannerData banner, GachaSave state)
        {
            state.pityCount = 0;
            state.aGuaranteeCount = 0;

            // FindAll(조건 람다): 목록에서 조건이 참인 것만 모은 새 목록 → 픽업이 아닌 S들.
            List<CharacterData> others = sCharacters.FindAll(character => character != banner.PickupCharacter);

            // 다른 S가 없으면(S가 픽업 하나뿐) 반천과 무관하게 픽업이 나온다.
            bool isPickup = state.pickupGuaranteed || random.NextDouble() < rules.PickupChance || others.Count == 0;
            state.pickupGuaranteed = !isPickup;

            CharacterData character = isPickup ? banner.PickupCharacter : PickRandom(others);
            return new GachaResult(GachaRarity.FiveStar, character, isPickup);
        }

        // A: S 천장은 계속 쌓이고, A 확정 카운터만 0으로. A는 픽업 없이 A 캐릭터 중 균등 랜덤.
        private GachaResult PullFourStar(GachaSave state)
        {
            state.pityCount++;
            state.aGuaranteeCount = 0;
            return new GachaResult(GachaRarity.FourStar, PickRandom(aCharacters), isPickup: false);
        }

        // 3성 무기: 두 카운터 모두 1 증가. 무기는 3성 무기 중 균등 랜덤.
        private GachaResult PullThreeStar(GachaSave state)
        {
            state.pityCount++;
            state.aGuaranteeCount++;

            WeaponData weapon = threeStarWeapons.Count > 0 ? threeStarWeapons[random.Next(threeStarWeapons.Count)] : null;
            return new GachaResult(GachaRarity.ThreeStar, null, isPickup: false, weapon);
        }

        // 목록에서 하나를 같은 확률로 고른다. Next(n): 0 이상 n 미만의 정수.
        private CharacterData PickRandom(List<CharacterData> candidates)
        {
            return candidates[random.Next(candidates.Count)];
        }
    }
}
