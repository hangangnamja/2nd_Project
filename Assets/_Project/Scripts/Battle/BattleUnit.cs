using System;
using Game.Battle.Status;
using Game.Data;

namespace Game.Battle
{
    // 전투 중인 유닛 하나. CharacterData(원본)를 참고해 만들어지고, 전투 중 바뀌는 값을 들고 있다.
    // MonoBehaviour가 아닌 순수 C# 클래스라서 화면(3D 모델, UI)과 분리되어 있다.
    public class BattleUnit
    {
        public CharacterData Data { get; }
        public bool IsAlly { get; }

        // 편성 순서 (0부터). 행동 순서가 동시에 도착했을 때 마지막 기준으로 쓴다.
        public int FormationIndex { get; }

        // 레벨. 방어 상수 K(공격자 레벨 기준) 계산에 쓴다. 성장 시스템 전까지는 편성의 테스트 레벨을 받는다.
        public int Level { get; }

        public int CurrentHp { get; private set; }

        // 이 유닛에게 걸린 버프·디버프 칸.
        public StatusContainer Statuses { get; } = new StatusContainer();

        // 행동 게이지 트랙에서 도착점까지 남은 거리. 행동하면 트랙 길이(10000)로 되돌아간다.
        // 행동수치가 아니라 남은 거리를 저장해야, 속도가 중간에 바뀌어도 도착 시간만 다시 계산하면 된다.
        public double RemainingDistance { get; private set; }

        public string Name => Data.DisplayName;

        // 레벨이 반영된 기본 스탯 = 캐릭터 데이터(Lv1 기준) × 레벨 배율. 전투 시작 때 정해지고 전투 중에는 바뀌지 않는다.
        // 버프·변신 보정은 이 값을 기준으로 붙는다.
        public double BaseMaxHp { get; }
        public double BaseAttack { get; }
        public double BaseDefense { get; }

        // 최대 HP도 최종 스탯이다: 변신 HP 증가 같은 보정이 붙으면 늘어나고, 보정이 사라지면 원래대로 돌아온다.
        public int MaxHp => (int)CalculateFinalStat(StatType.MaxHp, BaseMaxHp);
        public CharacterRole Role => Data.Role;

        // 최종 스탯: 기본 스탯에 걸려 있는 버프·디버프 보정을 더한 값. 전투 계산은 항상 이 값을 쓴다.
        public int Attack => (int)CalculateFinalStat(StatType.Attack, BaseAttack);
        public int Defense => (int)CalculateFinalStat(StatType.Defense, BaseDefense);

        // 최종 속도 = 기초 속도 × (1 + 속도 %) + 고정 속도 (기획서 3-3).
        // 속도가 바뀌어도 남은 거리는 그대로이고, 행동수치(도착 시간)만 이 값으로 다시 계산된다.
        public double Speed => CalculateFinalStat(StatType.Speed, Data.BaseSpeed);

        // 비율 스탯: 원본 값 + 상태효과 % 칸의 합 (0.2 = +20%p).
        // Math.Clamp(값, 0, 1): 치명확률을 0~100% 사이로 제한한다 (기획서 3-6 상한 100%).
        public double CritRate => Math.Clamp(Data.CritRate + SumModifierPercent(StatType.CritRate), 0d, 1d);
        public double CritDamage => Math.Max(0d, Data.CritDamage + SumModifierPercent(StatType.CritDamage));
        public double DamageBonus => SumModifierPercent(StatType.DamageBonus);
        public double DamageTakenIncrease => SumModifierPercent(StatType.DamageTakenIncrease);
        public double DamageOverTimeBonus => SumModifierPercent(StatType.DamageOverTimeBonus);

        // 같은 편 유닛 목록 (자신 포함). 아군의 오라 패시브를 찾는 데 쓴다. 전투 시작 시 BattleManager가 연결한다.
        private System.Collections.Generic.IReadOnlyList<BattleUnit> team = Array.Empty<BattleUnit>();

        // 같은 편 유닛 목록 (자신 포함). 하이퍼캐리 변신이 퇴장시킬 아군을 찾는 데 쓴다.
        public System.Collections.Generic.IReadOnlyList<BattleUnit> Team => team;

        // 같은 편 유닛 목록을 연결한다. 이후 스탯 계산에 살아 있는 아군의 오라가 반영된다.
        public void SetTeam(System.Collections.Generic.IReadOnlyList<BattleUnit> teamUnits)
        {
            team = teamUnits;
        }

        // 피해 감소율: 효과끼리 곱연산 후 최대 75%로 제한한다 (기획서 3-5).
        // 남는 피해 비율을 곱해 나가는 방식이라 많이 쌓을수록 효율이 줄어든다.
        // 예: 40% + 30% → 남는 비율 0.6 × 0.7 = 0.42 → 감소율 58% (합연산이면 70%)
        public double DamageReduction
        {
            get
            {
                double remainingRatio = 1d;

                foreach (StatModifier modifier in GetModifiers(StatType.DamageReduction))
                {
                    remainingRatio *= 1d - modifier.Percent;
                }

                return Math.Clamp(1d - remainingRatio, 0d, MaxDamageReduction);
            }
        }

        // 피해 감소율 최대치 (기획서 3-5). 버프를 쌓아 피해가 0이 되는 것을 막는다.
        public const double MaxDamageReduction = 0.75d;

        // 행동수치 = 남은 거리 / 속도. 도착까지 걸리는 시간이며, 가장 작은 유닛이 다음에 행동한다.
        // 예: 남은 거리 10000, 속도 120 → 약 83.3
        public double ActionValue => RemainingDistance / Speed;

        // 사망은 두 단계로 나눈다 (기획서 3-9).
        //  - IsDown: HP가 0이 된 상태. 지금 진행 중인 공격의 남은 효과는 끝까지 받는다
        //  - IsDead: 공격이 끝난 뒤 사망이 확정된 상태. 행동 순서·대상 선택·승패 판정은 이 값을 쓴다
        public bool IsDown => CurrentHp <= 0;
        public bool IsDead { get; private set; }

        // 퇴장 상태 (하이퍼캐리 변신 중의 아군). 공격받지 않고, 행동 게이지도 전진하지 않으며, 패시브·오라도 멈춘다.
        // 버프·디버프·쿨타임은 자기 턴이 없으므로 그대로 유지된다 (기획서 4-3).
        public bool IsExited { get; private set; }

        // 전장에 있는지 = 살아 있고 퇴장하지 않음. 행동 순서·대상 선택·반응 패시브는 이 값으로 판단한다.
        // 승패 판정은 IsDead로 한다 → 퇴장한 아군은 쓰러진 것이 아니다.
        public bool IsOnField => !IsDead && !IsExited;

        // 기절 등 행동 불가 효과가 걸려 있는지. 자기 차례가 와도 스킬을 쓰지 못하고 턴을 넘긴다.
        public bool IsStunned
        {
            get
            {
                foreach (StatusInstance status in Statuses.GetAll())
                {
                    if (status.Data.SkipsTurn)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // 무적: 피해를 받지 않는다 (보스 스테이지의 버프몹). 스테이지 설정으로 정해지고 전투 중 바뀌지 않는다.
        // 무적인 적은 승리 조건(적 전멸)에서도 빠진다 → 보스만 쓰러뜨리면 클리어.
        public bool IsInvulnerable { get; private set; }

        // 무적으로 만든다. 전투 시작 때 스테이지 설정을 보고 BattleManager가 호출한다.
        public void MakeInvulnerable()
        {
            IsInvulnerable = true;
        }

        // 퇴장시키거나(true) 복귀시킨다(false). 하이퍼캐리 변신·해제 때 호출된다.
        public void SetExited(bool exited)
        {
            IsExited = exited;
        }

        // HP가 0이면 사망을 확정한다. 공격(스킬·추가 공격)이나 턴 종료 처리가 끝날 때 BattleManager가 호출한다.
        // 이번에 새로 확정됐으면 true (로그·처치 반응용), 이미 확정됐거나 HP가 남았으면 false.
        public bool ConfirmDeathIfDown()
        {
            if (IsDead || !IsDown)
            {
                return false;
            }

            IsDead = true;
            return true;
        }

        // 스킬 슬롯별 남은 쿨타임 (0 = 사용 가능). 인덱스 0 = 1스, 1 = 2스, 2 = 3스.
        private readonly int[] remainingCooldowns = new int[CharacterData.SkillCount];

        // 패시브별 현재 스택 수. 원본 데이터(PassiveData)는 바꾸지 않고 전투 중 값만 여기에 둔다.
        private readonly System.Collections.Generic.Dictionary<PassiveData, int> passiveStacks =
            new System.Collections.Generic.Dictionary<PassiveData, int>();

        // 원본 데이터, 소속(아군/적), 편성 순서, 레벨, 레벨 배율을 받아 전투용 유닛을 만든다.
        // scaling: 레벨에 따른 HP·공격력·방어력 배율 (BattleRulesData가 아군/적 성장 곡선으로 계산해 넘겨준다).
        // 시작 HP는 최대 HP, 시작 거리는 트랙 길이 (기획서: 전투 시작 시 전원 10000).
        // 쿨타임은 스킬마다 설정한 시작 쿨타임으로 채운다 (기본 0 = 첫 턴부터 사용 가능).
        public BattleUnit(CharacterData data, bool isAlly, int formationIndex, int level, StatScaling scaling)
        {
            Data = data;
            IsAlly = isAlly;
            FormationIndex = formationIndex;
            Level = level;

            // 데이터 스탯을 Lv1 값으로 보고 레벨 배율을 곱한다. 예: 공격력 120, Lv60 아군 배율 6.15 → 738
            BaseMaxHp = data.MaxHp * (double)scaling.Hp;
            BaseAttack = data.Attack * (double)scaling.Attack;
            BaseDefense = data.Defense * (double)scaling.Defense;

            CurrentHp = MaxHp;
            RemainingDistance = TurnScheduler.TrackLength;

            for (int slot = 0; slot < CharacterData.SkillCount; slot++)
            {
                remainingCooldowns[slot] = data.GetSkill(slot).StartCooldown;
            }
        }

        // 패시브의 현재 스택 수. 한 번도 쌓이지 않았으면 0.
        public int GetPassiveStacks(PassiveData passive)
        {
            // TryGetValue: 키가 있으면 값을 stacks에 담고, 없으면 stacks = 0이다.
            passiveStacks.TryGetValue(passive, out int stacks);
            return stacks;
        }

        // 패시브 스택을 amount만큼 늘린다. 최대 스택을 넘지 않게 자르고, 실제로 늘어난 양을 돌려준다.
        public int AddPassiveStacks(PassiveData passive, int amount)
        {
            int before = GetPassiveStacks(passive);
            int after = Math.Min(passive.MaxStacks, before + amount);
            passiveStacks[passive] = after;
            return after - before;
        }

        // 패시브 스택을 amount만큼 쓴다. 모자라면 쓰지 않고 false를 돌려준다.
        public bool TryConsumePassiveStacks(PassiveData passive, int amount)
        {
            int current = GetPassiveStacks(passive);
            if (current < amount)
            {
                return false;
            }

            passiveStacks[passive] = current - amount;
            return true;
        }

        // ---------- 스킬과 쿨타임 ----------
        // 변신 중이면 1~3스가 변신 스킬로 바뀌고, 쿨타임도 변신 스킬의 것을 쓴다.
        // 그래서 스킬·쿨타임을 읽는 쪽은 Data.GetSkill이 아니라 항상 이 함수들을 거친다.

        // 지금 쓸 수 있는 slot번 스킬 (변신 중이면 변신 스킬).
        public SkillData GetSkill(int slot)
        {
            return IsTransformed ? Transformation.Data.GetSkill(slot) : Data.GetSkill(slot);
        }

        // 해당 슬롯의 남은 쿨타임 (UI 표시용). 변신 중이면 변신 스킬의 쿨타임.
        public int GetRemainingCooldown(int slot)
        {
            return IsTransformed ? Transformation.GetCooldown(slot) : remainingCooldowns[slot];
        }

        // 쿨타임이 끝나 지금 사용할 수 있는 스킬인지 확인한다.
        public bool CanUseSkill(int slot) => GetRemainingCooldown(slot) <= 0;

        // 스킬을 사용했을 때 해당 슬롯에 쿨타임을 건다.
        // usedTransformedSkill: 사용한 스킬이 변신 스킬이었는지 (스킬을 쓰기 전 상태 기준).
        // 변신 3스처럼 "쓰는 도중 변신"한 경우, 쿨타임은 변신 스킬이 아니라 원래 3스에 걸려야 하기 때문이다.
        public void StartCooldown(int slot, bool usedTransformedSkill)
        {
            if (usedTransformedSkill && IsTransformed)
            {
                Transformation.SetCooldown(slot, Transformation.Data.GetSkill(slot).Cooldown);
                return;
            }

            remainingCooldowns[slot] = Data.GetSkill(slot).Cooldown;
        }

        // 자기 턴이 끝날 때 모든 스킬의 쿨타임을 1씩 줄인다 (추가 턴도 턴이므로 똑같이 호출된다).
        // 이번 턴에 사용한 스킬도 함께 줄어들어, 쿨타임 N인 스킬은 N턴마다 한 번 쓸 수 있다.
        // 예: 쿨타임 3 → 사용(3) → 턴 종료 2 → 다음 턴 종료 1 → 다음 턴 종료 0 → 그다음 턴에 사용 가능
        // 변신 중에는 변신 스킬 쿨타임만 줄고, 원래 스킬 쿨타임은 멈춘다.
        // 그래야 변신 3스(쿨타임 5)가 변신하는 동안 돌아 버리지 않고, 해제된 뒤부터 다시 세어진다.
        public void TickCooldowns()
        {
            for (int slot = 0; slot < remainingCooldowns.Length; slot++)
            {
                if (IsTransformed)
                {
                    // Math.Max(0, ...): 이미 0인 쿨타임이 음수로 내려가지 않게 한다.
                    Transformation.SetCooldown(slot, Math.Max(0, Transformation.GetCooldown(slot) - 1));
                    continue;
                }

                remainingCooldowns[slot] = Math.Max(0, remainingCooldowns[slot] - 1);
            }
        }

        // 해당 슬롯의 남은 쿨타임을 turns만큼 줄인다 (쿨타임 감소 효과). 0 아래로는 내려가지 않는다.
        // 지금 쓰고 있는 스킬 세트(변신 중이면 변신 스킬)에 적용한다.
        public void ReduceCooldown(int slot, int turns)
        {
            if (slot < 0 || slot >= remainingCooldowns.Length)
            {
                return;
            }

            if (IsTransformed)
            {
                Transformation.SetCooldown(slot, Math.Max(0, Transformation.GetCooldown(slot) - turns));
                return;
            }

            remainingCooldowns[slot] = Math.Max(0, remainingCooldowns[slot] - turns);
        }

        // ---------- 변신 (기획서 4-3 하이퍼캐리) ----------

        // 변신 상태. 변신 중이 아니면 null.
        public TransformationState Transformation { get; private set; }
        public bool IsTransformed => Transformation != null;

        // 변신한다: 변신 상태를 만들고, exitAllies면 같은 편의 전장에 있는 다른 유닛을 퇴장시킨다.
        // 최대 HP가 늘어난 만큼 현재 HP도 올려, 변신 직후 HP 비율이 떨어지지 않게 한다.
        // 예: 최대 HP 1000 → 1300 (+30%), 현재 HP 800 → 1100
        public void Transform(TransformationData data, bool exitAllies)
        {
            var exited = new System.Collections.Generic.List<BattleUnit>();
            if (exitAllies)
            {
                foreach (BattleUnit ally in team)
                {
                    if (ally != this && ally.IsOnField)
                    {
                        ally.SetExited(true);
                        exited.Add(ally);
                    }
                }
            }

            int maxHpBefore = MaxHp;
            Transformation = new TransformationState(data, exited);
            CurrentHp += MaxHp - maxHpBefore;
        }

        // 변신을 해제하고 퇴장시켰던 아군을 복귀시킨다. 복귀한 아군 목록을 돌려준다 (로그용).
        // 최대 HP가 원래대로 줄어들므로, 현재 HP가 넘치면 최대 HP로 맞춘다 (기획서 4-3 강제 해제 규칙).
        public System.Collections.Generic.IReadOnlyList<BattleUnit> EndTransformation()
        {
            System.Collections.Generic.IReadOnlyList<BattleUnit> returned = Transformation.ExitedAllies;
            Transformation = null;

            foreach (BattleUnit ally in returned)
            {
                ally.SetExited(false);
            }

            CurrentHp = Math.Min(CurrentHp, MaxHp);
            return returned;
        }

        // HP를 지정한 값으로 맞춘다 (1 ~ 최대 HP). 변신 강제 해제처럼 "사망 대신 정해진 HP로 버티는" 처리에 쓴다.
        // Math.Clamp(값, 최소, 최대): 값을 1 ~ 최대 HP 범위로 자른다.
        public void SetHp(int hp)
        {
            CurrentHp = Math.Clamp(hp, 1, MaxHp);
        }

        // 원본 값에 상태효과·오라·변신의 스탯 보정을 적용한다.
        // 같은 스탯의 % 보정은 모두 더한 뒤 한 번에 곱한다 (같은 종류는 합연산, 기획서 3-5).
        // 예: 공격력 120, 공증 30% + 공증 10% → 120 × (1 + 0.4) = 168
        // 단, 공격력·방어력 % 증가 버프끼리는 중첩 감소를 적용한다 (아래 CombineBuffPercents).
        private double CalculateFinalStat(StatType stat, double baseValue)
        {
            double percentSum = 0d;
            double flatSum = 0d;
            var diminishingBuffs = new System.Collections.Generic.List<double>();

            foreach (StatModifier modifier in GetStatusModifiers(stat))
            {
                flatSum += modifier.Flat;

                // 중첩 감소 대상(공격력·방어력의 % 증가)은 따로 모았다가 한꺼번에 합친다. 디버프(음수)는 그대로 더한다.
                if (UsesDiminishingBuffs(stat) && modifier.Percent > 0f)
                {
                    diminishingBuffs.Add(modifier.Percent);
                    continue;
                }

                percentSum += modifier.Percent;
            }

            // 오라·변신 보정은 버프 칸의 효과가 아니므로 중첩 감소 없이 그대로 더한다.
            foreach (StatModifier modifier in GetNonStatusModifiers(stat))
            {
                percentSum += modifier.Percent;
                flatSum += modifier.Flat;
            }

            percentSum += CombineBuffPercents(diminishingBuffs);
            double finalValue = baseValue * (1d + percentSum) + flatSum;

            // 디버프가 아무리 겹쳐도 스탯이 0 아래로 내려가지 않게 한다.
            // 속도는 0이면 행동수치 계산(남은 거리 ÷ 속도)이 불가능하므로 최소 1로 둔다.
            double minimum = stat == StatType.Speed ? 1d : 0d;
            return Math.Max(minimum, finalValue);
        }

        // 중첩 감소를 적용하는 스탯인지. 공격력·방어력 증가는 아군 버퍼와 적에게서 뺏은 버프(광폭·경화)가 겹치기 쉬워
        // 단순 합산이면 지나치게 커지므로, 겹칠수록 효율이 떨어지게 한다 (기획서 3-7).
        private static bool UsesDiminishingBuffs(StatType stat)
        {
            return stat == StatType.Attack || stat == StatType.Defense;
        }

        // 같은 스탯 % 증가 버프 여러 개를 중첩 감소로 합친다: 1 - (1 - a)(1 - b)… (피해 감소와 같은 곱연산 방식)
        // 버프 하나만 있으면 그 값 그대로이고, 겹칠수록 단순 합보다 작아진다.
        // 예: 공뻥 30% + 광폭 30% → 1 - 0.7 × 0.7 = 51% (단순 합이면 60%)
        // 100% 이상인 버프는 남는 비율이 0 이하가 되므로 Math.Max로 0에서 멈춘다 → 합쳐도 최대 +100%.
        private static double CombineBuffPercents(System.Collections.Generic.List<double> percents)
        {
            double remainingRatio = 1d;

            foreach (double percent in percents)
            {
                remainingRatio *= Math.Max(0d, 1d - percent);
            }

            return 1d - remainingRatio;
        }

        // 걸려 있는 모든 상태효과에서 특정 스탯의 % 보정만 더한다 (비율 스탯용).
        private double SumModifierPercent(StatType stat)
        {
            double sum = 0d;

            foreach (StatModifier modifier in GetModifiers(stat))
            {
                sum += modifier.Percent;
            }

            return sum;
        }

        // 이 유닛에 적용되는 특정 스탯의 보정을 모두 골라 돌려준다:
        // 자신에게 걸린 상태효과 + 살아 있는 같은 편(자신 포함)의 오라 패시브.
        // yield return: 조건에 맞는 보정을 찾을 때마다 하나씩 넘겨주고, 호출한 foreach가 다음 값을 요청하면 이어서 찾는다.
        // 결과를 담을 리스트를 따로 만들지 않고, 필요한 보정만 차례로 꺼내 쓸 수 있다.
        private System.Collections.Generic.IEnumerable<StatModifier> GetModifiers(StatType stat)
        {
            foreach (StatModifier modifier in GetStatusModifiers(stat))
            {
                yield return modifier;
            }

            foreach (StatModifier modifier in GetNonStatusModifiers(stat))
            {
                yield return modifier;
            }
        }

        // 자신에게 걸린 상태효과(버프·디버프 칸)의 보정만 꺼낸다.
        private System.Collections.Generic.IEnumerable<StatModifier> GetStatusModifiers(StatType stat)
        {
            foreach (StatusInstance status in Statuses.GetAll())
            {
                foreach (StatModifier modifier in status.Data.StatModifiers)
                {
                    if (modifier.Stat == stat)
                    {
                        yield return modifier;
                    }
                }
            }
        }

        // 상태효과 칸 밖의 보정을 꺼낸다: 살아 있는 같은 편의 오라 패시브 + 변신 스탯 보정.
        private System.Collections.Generic.IEnumerable<StatModifier> GetNonStatusModifiers(StatType stat)
        {
            foreach (StatModifier modifier in GetAuraModifiers(stat))
            {
                yield return modifier;
            }

            // 변신 중이면 변신 스탯 보정(공격력·방어력·최대 HP 증가)도 함께 적용한다.
            // 상태효과 칸이 아니라 변신 설정에 들어 있으므로 강탈·소모·만료되지 않는다.
            if (IsTransformed)
            {
                foreach (StatModifier modifier in Transformation.Data.StatModifiers)
                {
                    if (modifier.Stat == stat)
                    {
                        yield return modifier;
                    }
                }
            }
        }

        // 살아 있는 같은 편 유닛들의 오라 패시브에서 특정 스탯 보정을 꺼낸다 (기획서 4-3 독 서브딜러 패시브).
        // 오라 주인이 쓰러지면 그 오라는 더 이상 반영되지 않는다.
        private System.Collections.Generic.IEnumerable<StatModifier> GetAuraModifiers(StatType stat)
        {
            foreach (BattleUnit ally in team)
            {
                // 쓰러졌거나 퇴장한 유닛의 오라는 꺼진다.
                if (!ally.IsOnField)
                {
                    continue;
                }

                foreach (PassiveData passive in ally.Data.Passives)
                {
                    if (passive.Trigger != PassiveTrigger.Aura)
                    {
                        continue;
                    }

                    foreach (StatModifier modifier in passive.AuraModifiers)
                    {
                        if (modifier.Stat == stat)
                        {
                            yield return modifier;
                        }
                    }
                }
            }
        }

        // HP를 회복하고, 실제로 회복된 양을 돌려준다. 최대 HP를 넘지 않는다.
        public int Heal(int amount)
        {
            int hpBefore = CurrentHp;

            // Math.Min: 회복 후 HP가 최대 HP를 넘으면 최대 HP로 맞춘다.
            CurrentHp = Math.Min(MaxHp, CurrentHp + amount);
            return CurrentHp - hpBefore;
        }

        // 마지막으로 이 유닛에게 피해를 준 유닛. 사망이 확정될 때 "누가 처치했는지" 판단에 쓴다.
        public BattleUnit LastAttacker { get; private set; }

        // 피해를 받아 HP를 줄이고, 실제로 줄어든 양을 돌려준다 (로그·연출용).
        // attacker: 피해를 준 유닛 (지속피해는 그 효과를 건 유닛). 처치자 판정에 기록한다.
        public int TakeDamage(int amount, BattleUnit attacker)
        {
            int hpBefore = CurrentHp;

            // Math.Max(0, ...): 남은 HP가 음수가 되지 않도록 0 아래로 내려가면 0으로 고정한다.
            CurrentHp = Math.Max(0, CurrentHp - amount);

            int dealt = hpBefore - CurrentHp;
            if (dealt > 0)
            {
                LastAttacker = attacker;
            }

            // 남은 HP보다 큰 피해가 들어와도 실제로 깎인 만큼만 돌려준다 (예: HP 30에 100 피해 → 30).
            return dealt;
        }

        // ---------- 행동 게이지 ----------

        // 지금 자기 턴을 진행 중인지. 자기 턴 중에 얻은 행동 당기기는 턴 종료 리셋 뒤에 적용해야 한다.
        public bool IsTakingTurn { get; private set; }

        // 자기 턴 중에 쌓인 행동 당기기 비율 (1.0 = 100%). 턴이 끝나 게이지가 리셋된 뒤 한꺼번에 적용한다.
        private double pendingAdvanceRatio;

        // elapsedTime만큼 시간이 흐를 때 그동안 이동한 거리(속도 × 시간)만큼 도착점에 가까워진다.
        // 부동소수점 오차로 음수가 되지 않도록 0 아래는 0으로 고정한다.
        public void Advance(double elapsedTime)
        {
            RemainingDistance = Math.Max(0d, RemainingDistance - Speed * elapsedTime);
        }

        // 행동을 마친 유닛을 트랙 출발점으로 되돌린다.
        public void ResetDistance()
        {
            RemainingDistance = TurnScheduler.TrackLength;
        }

        // 자기 턴 시작을 표시한다. 이때부터 얻는 행동 당기기는 턴이 끝날 때까지 보류된다.
        public void BeginOwnTurn()
        {
            IsTakingTurn = true;
        }

        // 자기 턴 종료(게이지 리셋 이후)를 표시하고, 보류해 둔 행동 당기기를 적용한다.
        public void EndOwnTurn()
        {
            IsTakingTurn = false;

            if (pendingAdvanceRatio > 0d)
            {
                ApplyActionAdvance(pendingAdvanceRatio);
                pendingAdvanceRatio = 0d;
            }
        }

        // 행동 게이지를 ratio만큼 당긴다 (기획서 3-3: 남은 거리 -= 10000 × 비율).
        // 자기 턴 중이면 턴 종료 리셋에 지워지지 않도록 보류해 두었다가 턴이 끝난 뒤 적용한다.
        public void AddActionAdvance(double ratio)
        {
            if (IsTakingTurn)
            {
                pendingAdvanceRatio += ratio;
                return;
            }

            ApplyActionAdvance(ratio);
        }

        // 남은 거리를 실제로 줄인다. 0(바로 다음 행동) 아래로는 내려가지 않는다.
        private void ApplyActionAdvance(double ratio)
        {
            RemainingDistance = Math.Max(0d, RemainingDistance - TurnScheduler.TrackLength * ratio);
        }
    }
}
