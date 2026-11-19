using System;
using Game.Data;

namespace Game.Outgame
{
    // 확률 공시에 보여주는 결과 구간 (GachaMachine.Pull의 난수 구간과 같은 순서).
    public enum GachaRateCategory
    {
        SCharacter,
        ACharacter,
        FiveStarWeapon,
        FourStarWeapon,
        ThreeStarWeapon,
    }

    // 확률 공시 수치 한 묶음 (기획서 7장: 기본 확률과 최종 획득 확률을 함께 표기).
    public class GachaRateTable
    {
        // 구간별 확률. 배열 칸 번호 = GachaRateCategory 숫자 값
        private readonly double[] baseRates;
        private readonly double[] finalRates;

        // 픽업 확정이 없을 때 S가 픽업일 확률 (반천 50%)
        public double BasePickupShare { get; }

        // 반천까지 넣었을 때 나온 S 중 픽업의 비율 (50%면 2/3)
        public double FinalPickupShare { get; }

        public GachaRateTable(double[] baseRates, double[] finalRates, double basePickupShare, double finalPickupShare)
        {
            this.baseRates = baseRates;
            this.finalRates = finalRates;
            BasePickupShare = basePickupShare;
            FinalPickupShare = finalPickupShare;
        }

        // 천장·확정이 없는 1회 뽑기의 확률
        public double GetBaseRate(GachaRateCategory category) => baseRates[(int)category];

        // 천장·소프트 천장·A 확정까지 넣은 장기 평균 확률 (많이 뽑았을 때 실제로 나오는 비율)
        public double GetFinalRate(GachaRateCategory category) => finalRates[(int)category];

        // S 1개를 얻는 데 드는 평균 횟수 = 1 / 최종 S 확률 (최종 1.64%면 약 61회)
        public double AveragePullsPerS => 1.0 / finalRates[(int)GachaRateCategory.SCharacter];

        // 픽업 1개를 얻는 데 드는 평균 횟수 = 1 / (최종 S 확률 × 픽업 비율)
        public double AveragePullsPerPickup => AveragePullsPerS / FinalPickupShare;
    }

    // 가챠 규칙(GachaRulesData)으로 확률 공시 수치를 계산한다. 저장·화면을 모르는 순수 C# 클래스.
    // 최종 확률은 시뮬레이션이 아니라 정확한 계산이라 화면을 열 때마다 같은 숫자가 나온다.
    public static class GachaRateCalculator
    {
        // 반복 계산을 멈추는 기준: 한 번 더 돌려도 확률이 이 값보다 적게 바뀌면 충분히 수렴한 것으로 본다.
        private const double Tolerance = 1e-13;
        private const int MaxIterations = 20000;

        // otherSCount: 픽업이 아닌 S 수. 0이면 S는 늘 픽업이다 (GachaMachine과 같은 규칙).
        public static GachaRateTable Calculate(GachaRulesData rules, int otherSCount)
        {
            double[] baseRates = GetSegmentRates(rules, rules.SRate, isAGuaranteed: false);
            double[] finalRates = CalculateFinalRates(rules);

            double basePickupShare = otherSCount > 0 ? rules.PickupChance : 1.0;
            double finalPickupShare = otherSCount > 0 ? GetFinalPickupShare(rules.PickupChance) : 1.0;
            return new GachaRateTable(baseRates, finalRates, basePickupShare, finalPickupShare);
        }

        // 한 번 뽑을 때 구간별 확률. GachaMachine.Pull처럼 0~1 난수를 S → A → 5성 무기 → 4성 무기 → 3성 무기 순 구간에 댄다.
        // 소프트 천장으로 S 확률이 커지면 뒤 구간이 1을 넘어 잘리므로, 경계마다 Math.Min으로 1에서 자른 뒤 차이를 확률로 쓴다.
        // A 확정 차례면 S가 아닌 결과는 모두 A로 올라간다 → A = 1 - S, 무기 0.
        private static double[] GetSegmentRates(GachaRulesData rules, double sRate, bool isAGuaranteed)
        {
            var rates = new double[5];
            rates[(int)GachaRateCategory.SCharacter] = sRate;

            if (isAGuaranteed)
            {
                rates[(int)GachaRateCategory.ACharacter] = 1.0 - sRate;
                return rates;
            }

            double aEnd = Math.Min(1.0, sRate + rules.ARate);
            double fiveEnd = Math.Min(1.0, aEnd + rules.FiveStarWeaponRate);
            double fourEnd = Math.Min(1.0, fiveEnd + rules.FourStarWeaponRate);

            rates[(int)GachaRateCategory.ACharacter] = aEnd - sRate;
            rates[(int)GachaRateCategory.FiveStarWeapon] = fiveEnd - aEnd;
            rates[(int)GachaRateCategory.FourStarWeapon] = fourEnd - fiveEnd;
            rates[(int)GachaRateCategory.ThreeStarWeapon] = 1.0 - fourEnd;
            return rates;
        }

        // 최종 확률: 카운터 상태(S 없이 뽑은 횟수 p, A 이상 없이 뽑은 횟수 a)마다 "지금 이 상태일 확률"을 구하고,
        // 상태별 구간 확률에 그 비중을 곱해 더한다 (마르코프 연쇄의 정상 분포).
        //  - 상태 이동: S → (0, 0), A → (p + 1, 0), 무기 → (p + 1, a + 1). GachaMachine이 세이브 카운터를 바꾸는 규칙과 같다
        //  - 처음엔 (0, 0)에 100%를 두고 "한 번 뽑은 뒤 분포"를 반복 계산하면, 몇 백 번 안에 더 이상 변하지 않는 분포로 수렴한다
        private static double[] CalculateFinalRates(GachaRulesData rules)
        {
            int pityStates = rules.HardPity;             // p = 0 ~ 천장 - 1 (천장 - 1에서는 S 확정이라 더 커지지 않음)
            int aStates = rules.AGuaranteeInterval;       // a = 0 ~ 간격 - 1 (간격 - 1에서는 A 확정)

            // 상태별 구간 확률은 반복마다 같으므로 미리 계산해 둔다.
            var segmentRates = new double[pityStates, aStates][];
            for (int p = 0; p < pityStates; p++)
            {
                double sRate = rules.GetSRate(p + 1);
                for (int a = 0; a < aStates; a++)
                {
                    segmentRates[p, a] = GetSegmentRates(rules, sRate, isAGuaranteed: a + 1 >= aStates);
                }
            }

            var distribution = new double[pityStates, aStates];
            distribution[0, 0] = 1.0;

            for (int iteration = 0; iteration < MaxIterations; iteration++)
            {
                double[,] next = Step(distribution, segmentRates, pityStates, aStates);
                double change = MaxDifference(distribution, next, pityStates, aStates);
                distribution = next;
                if (change < Tolerance)
                {
                    break;
                }
            }

            // 상태 비중 × 그 상태의 구간 확률을 모두 더한 값이 장기적으로 나오는 비율이다.
            var finalRates = new double[5];
            for (int p = 0; p < pityStates; p++)
            {
                for (int a = 0; a < aStates; a++)
                {
                    for (int category = 0; category < finalRates.Length; category++)
                    {
                        finalRates[category] += distribution[p, a] * segmentRates[p, a][category];
                    }
                }
            }

            return finalRates;
        }

        // 지금 분포에서 한 번 뽑은 뒤의 분포. 각 상태의 비중을 결과별 확률대로 다음 상태에 나눠 준다.
        // 천장 직전(p = 천장 - 1)은 S 확률 100%라 p + 1이 범위를 넘는 이동에는 비중이 0이므로 건너뛴다.
        private static double[,] Step(double[,] distribution, double[,][] segmentRates, int pityStates, int aStates)
        {
            var next = new double[pityStates, aStates];
            for (int p = 0; p < pityStates; p++)
            {
                for (int a = 0; a < aStates; a++)
                {
                    double weight = distribution[p, a];
                    if (weight <= 0.0)
                    {
                        continue;
                    }

                    double[] rates = segmentRates[p, a];
                    next[0, 0] += weight * rates[(int)GachaRateCategory.SCharacter];

                    if (p + 1 >= pityStates)
                    {
                        continue;
                    }

                    next[p + 1, 0] += weight * rates[(int)GachaRateCategory.ACharacter];

                    double weaponRate = rates[(int)GachaRateCategory.FiveStarWeapon]
                        + rates[(int)GachaRateCategory.FourStarWeapon]
                        + rates[(int)GachaRateCategory.ThreeStarWeapon];
                    if (a + 1 < aStates)
                    {
                        next[p + 1, a + 1] += weight * weaponRate;
                    }
                }
            }

            return next;
        }

        private static double MaxDifference(double[,] before, double[,] after, int pityStates, int aStates)
        {
            double max = 0.0;
            for (int p = 0; p < pityStates; p++)
            {
                for (int a = 0; a < aStates; a++)
                {
                    max = Math.Max(max, Math.Abs(before[p, a] - after[p, a]));
                }
            }

            return max;
        }

        // 반천을 넣은 픽업 비율. 픽업 확정이 아닌 상태의 비중을 x라 하면:
        //   확정 상태 비중 = x × (1 - c)  (픽업이 아니었던 S 다음)  →  x + x(1 - c) = 1  →  x = 1 / (2 - c)
        //   픽업 비율 = x × c + 확정 상태 비중 × 1 = 1 / (2 - c)  (c = 50%면 2/3)
        private static double GetFinalPickupShare(double pickupChance)
        {
            return 1.0 / (2.0 - pickupChance);
        }
    }
}
