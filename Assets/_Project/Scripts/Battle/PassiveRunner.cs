using System;
using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Data;

namespace Game.Battle
{
    // 전투 사건(BattleEvents)을 구독하고, 사건에 맞는 캐릭터 패시브를 실행한다.
    // 지금은 "공격 판정 시" 패시브를 즉시 실행한다 (즉시 개입형, Architecture.md 3장).
    // 추가 공격·추가 턴 같은 후속 행동형 패시브는 이후 행동 큐를 도입할 때 큐에 등록하는 방식으로 확장한다.
    // IDisposable: "다 쓰고 나면 Dispose()로 정리해야 하는 객체"라는 표시. 여기서는 사건 구독 해제가 정리 작업이다.
    public class PassiveRunner : IDisposable
    {
        private readonly BattleEvents events;
        private readonly Action<string> log;

        // 사건 통로와 로그 함수를 받아 사건 구독을 시작한다.
        public PassiveRunner(BattleEvents events, Action<string> log)
        {
            this.events = events;
            this.log = log;
            events.AttackHit += HandleAttackHit;
        }

        // 전투가 끝나 더 이상 반응할 필요가 없을 때 구독을 해제한다.
        public void Dispose()
        {
            events.AttackHit -= HandleAttackHit;
        }

        // 공격자가 대상을 맞혔을 때, 공격자의 "공격 판정 시" 패시브를 맞은 대상에게 실행한다.
        private void HandleAttackHit(BattleUnit attacker, BattleUnit target)
        {
            foreach (PassiveData passive in attacker.Data.Passives)
            {
                if (passive.Trigger != PassiveTrigger.OnAttackHit)
                {
                    continue;
                }

                log($"  [패시브] {attacker.Name}: {passive.PassiveName}");
                RunEffects(passive, attacker, new List<BattleUnit> { target });
            }
        }

        // 패시브의 효과 블록을 순서대로 실행한다. 반응으로 실행되므로 isReaction = true.
        private void RunEffects(PassiveData passive, BattleUnit owner, List<BattleUnit> targets)
        {
            var context = new SkillContext(owner, targets, log, events, isReaction: true);

            foreach (SkillEffect effect in passive.Effects)
            {
                // ?. 연산자: 종류를 아직 고르지 않은 빈 칸(null)은 건너뛴다.
                effect?.Apply(context);
            }
        }
    }
}
