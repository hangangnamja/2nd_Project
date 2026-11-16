namespace Game.Data
{
    // 무기 하나가 지금 레벨·진급에서 더해 주는 스탯 (전투원 화면·전투가 같은 값을 쓴다).
    // 무기 데이터의 최대 스탯(Lv60·진급 5회)에 성장 비율(BattleRulesData.GetWeaponStatRatio)을 곱한 값이다.
    // readonly struct: 만든 뒤 바뀌지 않는 값 묶음.
    public readonly struct WeaponStats
    {
        public int MaxHp { get; }
        public int Attack { get; }
        public int Defense { get; }

        public WeaponStats(int maxHp, int attack, int defense)
        {
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
        }

        // 무기를 끼지 않았을 때.
        public static WeaponStats None => new WeaponStats(0, 0, 0);

        // weapon의 level·promotion 스탯. (int): 소수점은 버린다. weapon이 없으면 None.
        public static WeaponStats Calculate(WeaponData weapon, int level, int promotion, BattleRulesData rules)
        {
            if (weapon == null || rules == null)
            {
                return None;
            }

            float ratio = rules.GetWeaponStatRatio(level, promotion);
            return new WeaponStats((int)(weapon.MaxHp * ratio), (int)(weapon.Attack * ratio), (int)(weapon.Defense * ratio));
        }
    }
}
