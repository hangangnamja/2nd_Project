using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 영역 해제 연출 (흉신 마라 "영역: 혈해신전"이 끝날 때, 임시). 마지막 영역 공격의 피해가 다 들어간 뒤에 나온다.
    //  1) 다시 마라 정면 조금 아래에서 올려다보는 시점으로 컷 (펼칠 때와 같은 구도 → 마라 뒤에 사찰이 보임)
    //  2) 잠깐 머문 뒤 사찰(물에 비친 모습 포함)이 흐려지며 사라지고, 물바닥·붉은 하늘·어두운 전장 색도 함께 옅어진다
    //  3) 원래 전장으로 돌아온 모습을 잠깐 보여준 뒤 원래 시점으로
    // 영역 로직은 이미 끝났고(BattleUnit.ActiveBoundary = null), 배경은 DomainSceneryView가 지우지 않고 남겨 둔 것을 이 연출이 지운다.
    public class DomainCloseFx : SkillFx
    {
        [Header("카메라")]
        [Tooltip("마라 정면으로 떨어지는 거리 · 카메라 높이(캡슐 가운데 기준) · 바라보는 높이 · 화각")]
        [SerializeField, Min(1f)] private float viewDistance = 5.5f;
        [SerializeField] private float viewHeight = -0.3f;
        [SerializeField] private float lookHeight = 1.6f;
        [SerializeField] private float viewFieldOfView = 55f;

        [Header("시간")]
        [Tooltip("컷 직후 영역을 보여주는 시간 · 다 사라진 뒤 머무는 시간(초)")]
        [SerializeField, Min(0f)] private float leadSeconds = 0.5f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.6f;

        private BattleCameraDirector cinematicCamera;

        // 마라는 제자리에 선 채 영역이 풀린다 (떠오르는 기본 시전 모션 없음).
        public override bool CasterCasts => false;

        // 연출이 중간에 사라져도 카메라가 연출 시점에 남지 않게 되돌린다.
        private void OnDestroy()
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            // 지울 배경이 없으면(이미 사라짐) 보여줄 것이 없다.
            DomainScenery scenery = DomainScenery.Current;
            if (CasterView == null || scenery == null)
            {
                Destroy(gameObject);
                yield break;
            }

            BeginFrontView(CasterView.AnchorPosition, CasterView.transform.forward, camera);
            yield return new WaitForSeconds(leadSeconds);
            yield return scenery.Dissolve();
            yield return new WaitForSeconds(holdSeconds);

            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 마라가 바라보는 방향 정면, 조금 아래에서 마라를 올려다보는 시점으로 컷 (펼치는 연출 DomainDeployFx와 같은 구도).
        private void BeginFrontView(Vector3 origin, Vector3 forward, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = origin + Vector3.up * lookHeight;
            camera.BeginCinematic(origin + forward * viewDistance + Vector3.up * viewHeight, lookPoint, viewFieldOfView);
            cinematicCamera = camera;
        }
    }
}
