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
        [Label("스킬 이름")]
        [SerializeField] private string skillName = "새 스킬";
        [Label("대상")]
        [SerializeField] private SkillTargetType targetType = SkillTargetType.EnemySingle;

        [Tooltip("사용 후 다시 쓸 수 있을 때까지의 자기 턴 수. 0이면 매 턴 사용 가능")]
        [Label("쿨타임", Min = 0f)]
        [SerializeField] private int cooldown;

        [Tooltip("전투 시작 시 남아 있는 쿨타임. 0이면 첫 턴부터 사용 가능")]
        [Label("시작 쿨타임", Min = 0f)]
        [SerializeField] private int startCooldown;

        // 이 스킬만의 연출 (선택). 비워 두면 기본 연출(돌진·시전)을 쓴다.
        // 프리팹에 SkillFx 컴포넌트(예: MeteorShowerFx)가 있어야 하고, 연출이 대상에 닿는 순간 피해 숫자·타격이 나온다.
        [Tooltip("스킬 전용 연출 프리팹 (SkillFx 컴포넌트). 비우면 기본 돌진·시전 모션")]
        [Label("전용 연출")]
        [SerializeField] private GameObject presentationPrefab;

        // 연출에서만 피해를 몇 번으로 나눠 보여줄지 (피해 계산·합계는 그대로). 1이면 한 번.
        // 예: 4 → 같은 피해를 4등분해 숫자 4개가 빠르게 연달아 뜨고, 매 타마다 번쩍임·흔들림이 나온다.
        [Tooltip("연출용 다단 히트 수. 피해 합계는 같고 숫자만 나눠 보여준다 (1 = 나누지 않음)")]
        [Label("연출 타수", Min = 1f)]
        [SerializeField] private int presentationHits = 1;

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
        public GameObject PresentationPrefab => presentationPrefab;
        public int PresentationHits => Mathf.Max(1, presentationHits);
        public IReadOnlyList<SkillEffect> Effects => effects;

        // 경계를 펼치는 스킬인지 (효과 목록에 경계 블록이 있는지). 같은 편 경계가 펼쳐진 동안 이런 스킬은 쓸 수 없다 (기획서 3-12).
        // Exists(조건): 조건(람다식)이 true인 원소가 하나라도 있으면 true. is: 그 블록이 경계 블록 종류인지 확인한다.
        public bool DeploysBoundary => effects.Exists(effect => effect is BoundaryEffect);
    }
}
