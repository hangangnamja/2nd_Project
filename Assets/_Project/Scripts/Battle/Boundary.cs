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

        // 지금 끝나야 하는지. BattleManager가 변신 해제·사망 확정 같은 시점마다 확인한다.
        public abstract bool ShouldEnd();
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
