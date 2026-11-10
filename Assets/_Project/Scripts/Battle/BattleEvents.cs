using System;
using System.Collections.Generic;
using Game.Battle.Status;

namespace Game.Battle
{
    // 전투 중 일어난 사건을 알리는 통로 (옵저버 패턴).
    // 효과 블록이 사건을 알리면(Raise), 구독한 쪽(패시브 처리기 등)이 반응한다.
    // 사건을 일으키는 쪽은 누가 반응하는지 몰라도 되므로, 새 패시브를 추가해도 효과 블록 코드는 바뀌지 않는다.
    public class BattleEvents
    {
        // 유닛이 자기 행동으로 스킬을 사용했을 때 (추가 턴 포함, 추가 공격 제외). Action<사용자>
        public event Action<BattleUnit> SkillUsed;

        // 공격 판정 1회가 대상 하나에게 들어갔을 때. Action<공격자, 맞은 대상>
        public event Action<BattleUnit, BattleUnit> AttackHit;

        // 공격 행동 하나(스킬 또는 추가 공격)가 끝났을 때. 광역 공격도 1번만 알린다.
        // Action<공격자, 맞은 대상 목록, 메인 대상(확산만, 없으면 null), 발생 원인>
        public event Action<BattleUnit, IReadOnlyList<BattleUnit>, BattleUnit, ActionSource> AttackPerformed;

        // 유닛이 새 버프를 얻었을 때 (갱신은 제외). Action<받은 유닛, 받은 버프 이름>
        public event Action<BattleUnit, string> BuffGained;

        // 유닛의 사망이 확정됐을 때. Action<쓰러진 유닛, 처치한 유닛(마지막으로 피해를 준 유닛, 없으면 null)>
        public event Action<BattleUnit, BattleUnit> UnitKilled;

        // 유닛에게 걸린 버프가 지워졌을 때 (만료, 소모, 보호막 깨짐, 밀려남 등. 사망·강탈 제외).
        // Action<버프를 가졌던 유닛, 지워진 버프 인스턴스(부여자 = Source)>
        public event Action<BattleUnit, StatusInstance> BuffRemoved;

        // 피해로 유닛의 HP가 줄었을 때 (보호막에 다 막혔거나 무적이면 알리지 않음).
        // Action<맞은 유닛, 공격한 유닛, 맞기 전 HP, 지속피해인지>. HP 돌파 반응 패시브가 "몇 %에서 몇 %로 내려갔는지"를 판단한다.
        public event Action<BattleUnit, BattleUnit, int, bool> HpLowered;

        public void RaiseHpLowered(BattleUnit target, BattleUnit attacker, int hpBefore, bool isDamageOverTime)
        {
            HpLowered?.Invoke(target, attacker, hpBefore, isDamageOverTime);
        }

        // ---------- 연출용 사건: 화면(BattleFxView)만 구독하고, 패시브 등 전투 로직은 반응하지 않는다 ----------

        // 피해가 들어갔을 때 (스킬·추가 공격·지속피해 모두, 무적이라 막힌 경우 포함). 타격 이펙트와 피해 숫자를 띄운다.
        public event Action<DamageTakenInfo> DamageTaken;

        // 회복을 받았을 때 (회복 스킬, 지속 회복). Action<회복한 유닛, 실제로 오른 HP>
        public event Action<BattleUnit, int> Healed;

        // 버프가 걸렸을 때 (새로 얻은 것 + 같은 버프의 남은 턴 갱신). Action<받은 유닛, 걸린 버프>
        // BuffGained와 달리 갱신도 알린다: 패시브에게는 "새 버프"만 의미가 있지만, 화면에는 "버프를 받았다"가 보여야 하기 때문.
        public event Action<BattleUnit, StatusInstance> BuffApplied;

        public void RaiseDamageTaken(DamageTakenInfo info)
        {
            DamageTaken?.Invoke(info);
        }

        public void RaiseHealed(BattleUnit target, int amount)
        {
            Healed?.Invoke(target, amount);
        }

        public void RaiseBuffApplied(BattleUnit target, StatusInstance buff)
        {
            BuffApplied?.Invoke(target, buff);
        }

        // 유닛이 치명적인 피해(남은 HP 이상의 피해)를 받기 직전. 구독자가 사망을 막을지 정한다.
        // 다른 사건과 달리 "결과를 돌려받는" 사건이라, 값을 바꿀 수 있는 FatalDamageArgs를 넘긴다.
        public event Action<FatalDamageArgs> FatalDamageIncoming;

        // 치명적인 피해를 알리고, 구독자 중 누군가 사망을 막았으면 true를 돌려준다 (하이퍼캐리 버티기 패시브).
        public bool RaiseFatalDamageIncoming(BattleUnit target)
        {
            var args = new FatalDamageArgs(target);
            FatalDamageIncoming?.Invoke(args);
            return args.IsPrevented;
        }

        // 버프가 지워졌음을 알린다. "자신이 건 버프가 지워질 때" 패시브(곰인형 버퍼)가 여기에 반응한다.
        public void RaiseBuffRemoved(BattleUnit holder, StatusInstance buff)
        {
            BuffRemoved?.Invoke(holder, buff);
        }

        // 새 버프를 얻었음을 알린다. 버프 획득 시 발동 패시브(강탈 딜러 가속)가 여기에 반응한다.
        public void RaiseBuffGained(BattleUnit receiver, string buffName)
        {
            BuffGained?.Invoke(receiver, buffName);
        }

        // 사망 확정을 알린다. 처치 시 발동 패시브(강탈 딜러 추격 등)가 여기에 반응한다.
        public void RaiseUnitKilled(BattleUnit victim, BattleUnit killer)
        {
            UnitKilled?.Invoke(victim, killer);
        }

        // 유닛이 스킬을 사용했음을 알린다. 행동마다 스택이 쌓이는 패시브가 여기에 반응한다.
        public void RaiseSkillUsed(BattleUnit caster)
        {
            SkillUsed?.Invoke(caster);
        }

        // 공격 판정 1회가 대상에게 들어갔음을 알린다.
        // ?.Invoke: 구독자가 없으면(null) 아무것도 하지 않는다.
        public void RaiseAttackHit(BattleUnit attacker, BattleUnit target)
        {
            AttackHit?.Invoke(attacker, target);
        }

        // 공격 행동 하나가 끝났음을 알린다. 아군 공격에 반응하는 패시브가 여기서 추가 공격을 예약한다.
        public void RaiseAttackPerformed(
            BattleUnit attacker,
            IReadOnlyList<BattleUnit> hitTargets,
            BattleUnit primaryTarget,
            ActionSource source)
        {
            AttackPerformed?.Invoke(attacker, hitTargets, primaryTarget, source);
        }
    }

    // 피해 한 번의 연출용 정보 (피해 숫자 색·크기를 고르는 데 쓴다).
    // readonly struct: 만든 뒤 값이 바뀌지 않는 작은 값 묶음. 사건으로 넘겨도 받는 쪽이 원본을 바꿀 수 없다.
    public readonly struct DamageTakenInfo
    {
        public BattleUnit Target { get; }

        // HP가 실제로 줄어든 양
        public int Amount { get; }

        // 보호막이 흡수한 양
        public int Absorbed { get; }

        public bool IsCritical { get; }
        public bool IsDamageOverTime { get; }

        // 무적이라 피해를 받지 않았는지 (보스 스테이지 버프몹)
        public bool IsBlocked { get; }

        public DamageTakenInfo(BattleUnit target, int amount, int absorbed, bool isCritical, bool isDamageOverTime, bool isBlocked)
        {
            Target = target;
            Amount = amount;
            Absorbed = absorbed;
            IsCritical = isCritical;
            IsDamageOverTime = isDamageOverTime;
            IsBlocked = isBlocked;
        }
    }

    // 치명적인 피해 사건의 정보. 구독자가 Prevent()를 부르면 피해를 넣는 쪽이 HP 1로 버티게 처리한다.
    // 사건 알림은 반환값을 받을 수 없으므로, 구독자가 이 객체의 값을 바꾸는 방식으로 결과를 돌려준다.
    public class FatalDamageArgs
    {
        public BattleUnit Target { get; }
        public bool IsPrevented { get; private set; }

        public FatalDamageArgs(BattleUnit target)
        {
            Target = target;
        }

        // 이번 피해로 쓰러지지 않게 한다 (HP 1로 버팀).
        public void Prevent()
        {
            IsPrevented = true;
        }
    }
}
