namespace Game.Data
{
    // 레벨에 따른 기본 스탯 배율 묶음 (HP, 공격력, 방어력). 캐릭터 데이터의 스탯(Lv1 기준)에 곱해 쓴다.
    // BattleRulesData가 성장 곡선으로 계산하고, BattleUnit이 전투 시작 때 한 번 받아 기본 스탯으로 고정한다.
    public readonly struct StatScaling
    {
        public float Hp { get; }
        public float Attack { get; }
        public float Defense { get; }

        public StatScaling(float hp, float attack, float defense)
        {
            Hp = hp;
            Attack = attack;
            Defense = defense;
        }

        // 배율 1 = 데이터 스탯 그대로 (Lv1).
        public static StatScaling None => new StatScaling(1f, 1f, 1f);
    }
}
