namespace Game.Battle
{
    // 이번 전투의 업적용 기록: 아군이 처치한 적, 아군의 치명타, 아군 스킬 사용 횟수.
    // 전투 사건(BattleEvents)을 구독해 세기만 하고, 전투가 끝나면 BattleManager가 이 값을 세이브에 더한다 (PlayerProfile.RecordBattle).
    // 전투 로직을 고치지 않고 사건만 듣는 구조라, 기록할 항목이 늘어도 효과 블록·패시브 코드는 바뀌지 않는다 (옵저버 패턴).
    public class BattleRecord
    {
        public int Kills { get; private set; }
        public int CriticalHits { get; private set; }
        public int SkillsUsed { get; private set; }

        // events.X += 함수: 그 사건이 일어날 때마다 함수가 불리도록 등록한다.
        // BattleEvents는 전투와 함께 만들어지고 함께 사라지므로 따로 해제하지 않는다.
        public BattleRecord(BattleEvents events)
        {
            events.UnitKilled += OnUnitKilled;
            events.DamageTaken += OnDamageTaken;
            events.SkillUsed += OnSkillUsed;
        }

        // 아군이 쓰러뜨린 적만 센다. killer: 마지막으로 피해를 준 유닛 (지속피해로 쓰러져도 건 아군), 없으면 null.
        // 자폭·함께 퇴장(Vanish)은 처치 사건이 없으므로 세지 않는다.
        private void OnUnitKilled(BattleUnit victim, BattleUnit killer)
        {
            if (!victim.IsAlly && killer != null && killer.IsAlly)
            {
                Kills++;
            }
        }

        // 적이 맞은 치명타만 센다 (적은 아군끼리 때리지 않으므로 = 아군의 치명타). 무적에 막힌 타격도 치명이면 센다.
        private void OnDamageTaken(DamageTakenInfo info)
        {
            if (info.IsCritical && !info.Target.IsAlly)
            {
                CriticalHits++;
            }
        }

        // 아군이 자기 행동으로 쓴 스킬 (추가 턴 포함, 추가 공격·연동 스킬 제외).
        private void OnSkillUsed(BattleUnit caster)
        {
            if (caster.IsAlly)
            {
                SkillsUsed++;
            }
        }
    }
}
