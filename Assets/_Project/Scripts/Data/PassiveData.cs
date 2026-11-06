using System;
using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 패시브가 반응하는 전투 사건 (트리거). 필요해질 때마다 항목을 추가한다.
    public enum PassiveTrigger
    {
        // 이 캐릭터의 공격이 대상에게 적중했을 때 (공격 판정 1회마다). 효과 대상 = 맞은 대상
        OnAttackHit,
    }

    // 패시브 하나의 설정: "언제(트리거)" + "무엇을(효과 목록)".
    // 스킬과 같은 효과 블록을 그대로 재사용하므로, 새 패시브도 Inspector에서 조립할 수 있다.
    [Serializable]
    public class PassiveData
    {
        [SerializeField] private string passiveName = "새 패시브";
        [SerializeField] private PassiveTrigger trigger = PassiveTrigger.OnAttackHit;

        // [SerializeReference] + [SubclassSelector]: 스킬 효과 목록과 같은 방식으로 효과 종류를 골라 넣는다.
        [SerializeReference, SubclassSelector]
        private List<SkillEffect> effects = new List<SkillEffect>();

        public string PassiveName => passiveName;
        public PassiveTrigger Trigger => trigger;
        public IReadOnlyList<SkillEffect> Effects => effects;
    }
}
