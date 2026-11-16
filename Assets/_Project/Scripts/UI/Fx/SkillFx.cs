using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 스킬 전용 연출의 공통 부모 (스킬 데이터의 "전용 연출" 프리팹에 붙인다).
    // BattleFxView는 연출 종류를 몰라도 Play만 부르고, 연출은 대상에게 타격이 닿는 순간마다 onImpact(대상)를 부른다
    //  → BattleFxView가 그 순간에 그 대상의 피해 숫자·번쩍임·흔들림을 재생한다 (로직 피해는 이미 계산돼 있고, 보여주는 시점만 맞춘다).
    // 새 연출은 이 클래스를 상속해 Play만 구현하면 된다 (전략 패턴: 스킬마다 다른 연출을 같은 방식으로 끼운다).
    public abstract class SkillFx : MonoBehaviour
    {
        // 연출 한 번을 재생한다.
        // caster: 시전자 화면 위치, targets: 화면에 보이는 대상과 그 위치, onImpact: 대상에게 닿는 순간 부를 함수 (Action<T> = 값 하나를 받는 델리게이트)
        // camera: 화면 흔들림용 (없으면 null). 다 끝나면 코루틴이 끝나고, 연출 오브젝트는 스스로 지운다.
        public abstract IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit> onImpact,
            BattleCameraDirector camera);
    }
}
