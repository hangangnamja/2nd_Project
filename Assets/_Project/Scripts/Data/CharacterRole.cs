namespace Game.Data
{
    // 캐릭터 직군. 숫자가 작을수록 행동 순서가 동시에 도착했을 때 먼저 행동한다.
    // 지원이 먼저 버프를 깔고 → 공격 → 방어 → 회복이 그 턴의 피해를 보고 회복하도록 순서를 정했다.
    public enum CharacterRole
    {
        Support = 0,
        Attack = 1,
        Defense = 2,
        Heal = 3,
    }
}
