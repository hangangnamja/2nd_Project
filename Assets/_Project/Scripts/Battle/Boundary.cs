using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Battle
{
    // 경계 하나의 전투 중 상태 (기획서 3-12). "무엇을 주고, 언제 끝나는지"는 경계마다 하위 클래스가 정한다.
    // 경계끼리의 공통 규칙(전장에 하나만 존재)은 BoundaryField가 맡으므로, 하위 클래스는 자기 기능만 구현한다.
    public abstract class Boundary
    {
        // 경계를 펼친 유닛 (경계의 효과를 받는 유닛)
        public BattleUnit Owner { get; }

        // 화면·로그에 쓰는 이름 (예: "고립")
        public string Name { get; }

        protected Boundary(BattleUnit owner, string name)
        {
            Owner = owner;
            Name = name;
        }

        // 경계를 펼친 유닛에게 주는 스탯 보정. 버프 칸이 아니라 경계 자체에 있어 강탈·소모·만료되지 않는다.
        public virtual IReadOnlyList<StatModifier> StatModifiers => Array.Empty<StatModifier>();

        // 경계를 펼친 유닛이 행동 제어(기절 등)에 면역인지
        public virtual bool GrantsCrowdControlImmunity => false;

        // 머리 위에 자기 버프처럼 보여줄 표시용 효과 (아이콘·툴팁). 없으면 null.
        public virtual StatusEffectData DisplayStatus => null;

        // 경계가 펼쳐질 때: 주인에게 경계를 달아 스탯·면역이 적용되게 한다.
        public void Begin(Action<string> log)
        {
            Owner.SetActiveBoundary(this);
            OnBegin(log);
        }

        // 경계가 끝날 때: 주인에게서 경계를 떼어 스탯·면역을 원래대로 돌린다.
        public void End(Action<string> log)
        {
            Owner.SetActiveBoundary(null);
            OnEnd(log);
        }

        // 경계마다 시작·끝에 추가로 할 일 (없으면 비워 둔다).
        protected virtual void OnBegin(Action<string> log)
        {
        }

        protected virtual void OnEnd(Action<string> log)
        {
        }

        // 지금 끝나야 하는지. BattleManager가 변신 해제·사망 확정·주인 턴 종료 같은 시점마다 확인한다.
        public abstract bool ShouldEnd();

        // 경계 동안 주인의 slot번 스킬 대신 쓸 스킬. 없으면 null (원래 스킬 그대로).
        // 영역: 3스 칸이 "업화"로 바뀐다 (BattleUnit.GetSkill이 이 값을 먼저 본다).
        public virtual SkillData GetSkillOverride(int slot) => null;

        // 바꿔 준 스킬을 주인이 썼을 때. 1회용이면 여기서 원래 스킬로 되돌린다.
        public virtual void OnSkillOverrideUsed(int slot)
        {
        }

        // 주인의 턴(정규 턴·추가 턴)이 끝날 때 BattleManager가 부른다. 턴마다 무언가 하는 경계가 쓴다 (영역: 적 전체 참 + 남은 턴 감소).
        public virtual void OnOwnerTurnEnd(BattleServices services)
        {
        }

        // 머리 위 아이콘·툴팁에 덧붙일 남은 턴 (턴 제한이 없는 경계는 -1).
        public virtual int RemainingTurns => -1;
    }

    // 전장의 경계 자리 (전투 한 판에 하나). 경계 공통 규칙: **한 번에 하나의 경계만 존재한다.**
    // 아군은 경계가 펼쳐진 동안 경계 스킬을 쓸 수 없으므로(BattleUnit.CanUseSkill) 보통 교체는 일어나지 않는다.
    // 그래도 새 경계가 들어오면 기존 경계를 끝내고 바꿔, 경계 효과가 겹치는 일은 없게 한다.
    public class BoundaryField
    {
        private readonly Action<string> log;

        public BoundaryField(Action<string> log)
        {
            this.log = log;
        }

        // 지금 펼쳐진 경계. 없으면 null.
        public Boundary Current { get; private set; }

        // 경계를 펼친다. 이미 다른 경계가 있으면 먼저 끝낸다 → 경계가 겹쳐 효과가 중첩되는 일이 없다.
        public void Deploy(Boundary boundary)
        {
            if (Current != null)
            {
                log($"  [경계] {Current.Name} → {boundary.Name}로 교체");
                EndCurrent();
            }

            Current = boundary;
            log($"  [경계] {boundary.Owner.Name}: {boundary.Name} 전개");
            boundary.Begin(log);
        }

        // 주인의 턴이 끝났음을 경계에 알리고, 끝날 조건(남은 턴 0 등)이 되면 끝낸다.
        // ?.: 펼쳐진 경계가 없거나 다른 유닛의 경계면 아무것도 하지 않는다.
        public void HandleTurnEnd(BattleUnit actor, BattleServices services)
        {
            if (Current == null || Current.Owner != actor)
            {
                return;
            }

            Current.OnOwnerTurnEnd(services);
            CheckEnd();
        }

        // 펼쳐진 경계가 끝날 조건이면 끝낸다. 변신 해제·사망 확정처럼 상태가 바뀌는 시점마다 부른다.
        public void CheckEnd()
        {
            if (Current != null && Current.ShouldEnd())
            {
                EndCurrent();
            }
        }

        private void EndCurrent()
        {
            Boundary ending = Current;
            Current = null;
            log($"  [경계] {ending.Name} 종료");
            ending.End(log);
        }
    }
}
