using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 스킬 전용 연출의 공통 부모 (스킬 데이터의 "전용 연출" 프리팹에 붙인다).
    // BattleFxView는 연출 종류를 몰라도 Play만 부르고, 연출은 대상에게 타격이 닿는 순간마다 onImpact(대상, 비율)를 부른다
    //  → BattleFxView가 그 순간에 그 대상 피해의 "비율"만큼을 숫자·HP 감소·흔들림으로 보여준다 (로직 피해는 이미 계산돼 있고, 보여주는 시점만 나눈다).
    //  → 비율을 1로 보내거나, 보낸 비율의 합이 1이 되면 남은 피해를 모두 보여주고 그 대상의 타격이 끝난다.
    // 새 연출은 이 클래스를 상속해 Play만 구현하면 된다 (전략 패턴: 스킬마다 다른 연출을 같은 방식으로 끼운다).
    public abstract class SkillFx : MonoBehaviour
    {
        // 시전자의 화면 표시 (BattleFxView가 Play 전에 넣어 줌). 변신 모습을 바꾸는 순간을 알릴 때 쓴다. 없으면 null.
        public UnitView CasterView { get; set; }

        // 스킬 대상 중 시전자와 같은 편(화면에 보이는 유닛)과 그 위치 (BattleFxView가 Play 전에 넣어 줌).
        // Play의 targets는 상대 편만 담으므로, 아군 버프 연출(곰인형 버퍼 3스)은 이 목록을 쓴다. 기본은 빈 목록.
        public IReadOnlyList<(BattleUnit Unit, Vector3 Position)> AllyTargets { get; set; } = Array.Empty<(BattleUnit, Vector3)>();

        // true면 이 연출이 시전자의 변신 모습(헤일로·날개)을 바꾸는 연출이다 (변신기·종결기).
        // BattleFxView가 행동 시작 사건 때 바로 모습을 고정해 두고(로직이 먼저 변신을 바꿔도 화면은 그대로), 연출이 RevealAppearance로 바꾼다.
        public virtual bool ChangesAppearance => false;

        // 변신 모습을 실제 상태로 바꾼다 (변신기: 구체가 터지며 나타남, 종결기: 영역이 깨지며 사라짐).
        protected void RevealAppearance()
        {
            if (CasterView != null)
            {
                CasterView.ReleaseAppearance();
            }
        }

        // 연출 한 번을 재생한다.
        // caster: 시전자 화면 위치, targets: 화면에 보이는 대상과 그 위치
        // onImpact: 대상에게 닿는 순간 부를 함수 (Action<T1, T2> = 값 두 개(대상, 이번에 보여줄 피해 비율 0~1)를 받는 델리게이트)
        // camera: 화면 흔들림용 (없으면 null). 다 끝나면 코루틴이 끝나고, 연출 오브젝트는 스스로 지운다.
        public abstract IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera);
    }
}
