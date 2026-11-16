using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data;

namespace Game.Outgame
{
    // 뽑기 결과의 등급. 화면 표시는 3성(파랑) / 4성(보라) / 5성(황금빛).
    // 숫자가 클수록 높은 등급이라, 결과 중 최고 등급을 비교로 구할 수 있다.
    // 캐릭터인지 무기인지는 등급이 아니라 결과의 Character·Weapon으로 구분한다 → 4·5성 무기도 같은 빛 연출을 받는다.
    public enum GachaRarity
    {
        ThreeStar = 3,  // 3성 무기
        FourStar = 4,   // A 캐릭터, 4성 무기
        FiveStar = 5,   // S 캐릭터, 5성 무기
    }

    // 뽑기 1회의 결과.
    public class GachaResult
    {
        public GachaRarity Rarity { get; }

        // 나온 캐릭터. 무기면 null.
        public CharacterData Character { get; }

        // 배너의 픽업 캐릭터인지 (5성만 해당)
        public bool IsPickup { get; }

        // 이번에 처음 얻은 캐릭터인지 (GachaService가 보유 여부를 보고 채운다)
        public bool IsNew { get; set; }

        // 나온 무기 (무기 결과일 때). 그 등급에 등록된 무기가 없으면 null.
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
        private readonly Func<WeaponGrade, List<WeaponData>> weaponsOfGrade;

        // weaponsOfGrade: 등급을 주면 그 등급 무기 목록을 돌려주는 함수 (Func<입력, 결과> = 함수를 값으로 넘기는 델리게이트).
        // 목록이 비어 있으면 무기 없이 등급 결과만 나온다.
        public GachaMachine(GachaRulesData rules, Random random, List<CharacterData> sCharacters, List<CharacterData> aCharacters, Func<WeaponGrade, List<WeaponData>> weaponsOfGrade)
        {
            this.rules = rules;
            this.random = random;
            this.sCharacters = sCharacters;
            this.aCharacters = aCharacters;
            this.weaponsOfGrade = weaponsOfGrade;
        }

        // 1회 뽑기. 난수 하나(0~1)를 확률 구간에 대어 등급을 정한다:
        //   [0, S) → S 캐릭터 / [S, S + A) → A 캐릭터 / 다음 1% → 5성 무기 / 다음 10% → 4성 무기 / 나머지 → 3성 무기
        // 캐릭터 구간이 앞에 있으므로 무기를 넣어도 캐릭터 확률·천장은 바뀌지 않는다 (무기 구간은 3성 무기 몫에서 떼어 냄).
        // S가 아니어도 A 확정 차례(A 이상 없이 10번째)면 A로 올린다. 4·5성 무기는 "A 이상"에 들지 않는다 (캐릭터 확정 유지).
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
            float characterEnd = sRate + rules.ARate;
            if (isAGuaranteed || roll < characterEnd)
            {
                return PullFourStar(state);
            }

            if (roll < characterEnd + rules.FiveStarWeaponRate)
            {
                return PullWeapon(state, WeaponGrade.FiveStar, GachaRarity.FiveStar);
            }

            if (roll < characterEnd + rules.FiveStarWeaponRate + rules.FourStarWeaponRate)
            {
                return PullWeapon(state, WeaponGrade.FourStar, GachaRarity.FourStar);
            }

            return PullWeapon(state, WeaponGrade.ThreeStar, GachaRarity.ThreeStar);
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

        // 무기: 등급과 상관없이 캐릭터 카운터(S 천장·A 확정)는 모두 1 증가. 무기는 그 등급 무기 중 균등 랜덤 (천장 없음).
        private GachaResult PullWeapon(GachaSave state, WeaponGrade grade, GachaRarity rarity)
        {
            state.pityCount++;
            state.aGuaranteeCount++;

            List<WeaponData> candidates = weaponsOfGrade(grade);
            WeaponData weapon = candidates.Count > 0 ? candidates[random.Next(candidates.Count)] : null;
            return new GachaResult(rarity, null, isPickup: false, weapon);
        }

        // 목록에서 하나를 같은 확률로 고른다. Next(n): 0 이상 n 미만의 정수.
        private CharacterData PickRandom(List<CharacterData> candidates)
        {
            return candidates[random.Next(candidates.Count)];
        }
    }
}
