using System;
using System.Collections.Generic;
using Game.Battle.Effects;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 스킬 하나의 설정. 별도 에셋이 아니라 CharacterData 안에 들어가는 데이터 묶음이다.
    // 효과 목록에 블록을 순서대로 끼워 넣는 방식으로 스킬을 조립한다.
    [Serializable]
    public class SkillData
    {
        [SerializeField] private string skillName = "새 스킬";
        [SerializeField] private SkillTargetType targetType = SkillTargetType.EnemySingle;

        [Tooltip("사용 후 다시 쓸 수 있을 때까지의 자기 턴 수. 0이면 매 턴 사용 가능")]
        [SerializeField, Min(0)] private int cooldown;

        [Tooltip("전투 시작 시 남아 있는 쿨타임. 0이면 첫 턴부터 사용 가능")]
        [SerializeField, Min(0)] private int startCooldown;

        // [SerializeReference]: 리스트 원소를 "실제 하위 클래스 그대로" 저장한다.
        // 일반 [SerializeField]는 선언된 타입(SkillEffect)으로만 저장해 DamageEffect의 배율 같은 값이 사라지지만,
        // SerializeReference는 DamageEffect, HealEffect처럼 서로 다른 타입을 한 리스트에 섞어 담을 수 있다.
        // [SubclassSelector]: Inspector에서 원소마다 효과 종류를 고르는 드롭다운을 표시한다.
        [SerializeReference, SubclassSelector]
        private List<SkillEffect> effects = new List<SkillEffect>();

        public string SkillName => skillName;
        public SkillTargetType TargetType => targetType;
        public int Cooldown => cooldown;
        public int StartCooldown => startCooldown;
        public IReadOnlyList<SkillEffect> Effects => effects;

        // 경계를 펼치는 스킬인지 (효과 목록에 경계 블록이 있는지). 같은 편 경계가 펼쳐진 동안 이런 스킬은 쓸 수 없다 (기획서 3-12).
        // Exists(조건): 조건(람다식)이 true인 원소가 하나라도 있으면 true. is: 그 블록이 경계 블록 종류인지 확인한다.
        public bool DeploysBoundary => effects.Exists(effect => effect is BoundaryEffect);
    }
}
