namespace Game.Data
{
    // 스킬 대상 규칙. "적"과 "아군"은 시전자 기준이다 (적 유닛이 쓰면 "적" = 플레이어 쪽).
    public enum SkillTargetType
    {
        EnemySingle,
        EnemyAll,
        Self,
        AllySingle,
        AllyAll,
    }
}
