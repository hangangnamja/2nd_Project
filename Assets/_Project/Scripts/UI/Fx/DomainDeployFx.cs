using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 영역 전개 연출 (흉신 마라 3스 "영역: 혈해신전", 임시). 3스를 쓰면 마라만 화면에 잡히고, 영역이 깔린다.
    //  1) 마라 정면 조금 아래에서 올려다보는 시점으로 컷 (마라 뒤로 먼 배경이 보이게)
    //  2) 마라가 제자리에서 살짝 떠오르며 손을 펼치는 동안(시전 자세), 발밑에서 얕은 물이 퍼지고 하늘이 검붉게 물든다
    //  3) 마라 뒤 땅에서 사찰이 솟아오르고(흔들림·먼지), 물바닥에 거꾸로 비친다
    //  4) 잠시 영역을 보여준 뒤 원래 시점으로 → 바로 이어서 영역 공격(적 전체 참) 연출
    // 배경 자체(사찰·물·하늘)는 DomainSceneryView가 영역이 펼쳐질 때 만든 DomainScenery이고, 이 연출은 그 배경의 Reveal을 재생할 뿐이다.
    public class DomainDeployFx : SkillFx
    {
        [Header("카메라")]
        [Tooltip("마라 정면으로 떨어지는 거리 · 카메라 높이(캡슐 가운데 기준) · 바라보는 높이 · 화각")]
        [SerializeField, Min(1f)] private float viewDistance = 5.5f;
        [SerializeField] private float viewHeight = -0.3f;
        [SerializeField] private float lookHeight = 1.6f;
        [SerializeField] private float viewFieldOfView = 55f;

        [Header("시간")]
        [Tooltip("컷 직후 숨 고르기 · 배경이 다 솟은 뒤 보여주는 시간(초)")]
        [SerializeField, Min(0f)] private float leadSeconds = 0.25f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.8f;

        [Tooltip("시전 중 마라가 떠오르는 높이")]
        [SerializeField, Min(0f)] private float liftHeight = 0.25f;

        private BattleCameraDirector cinematicCamera;

        // 연출이 중간에 사라져도 카메라·캡슐이 연출 상태로 남지 않게 되돌린다.
        private void OnDestroy()
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }

            if (CasterView != null)
            {
                CasterView.ClearScriptedPose();
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            if (CasterView == null)
            {
                Destroy(gameObject);
                yield break;
            }

            Vector3 origin = CasterView.AnchorPosition;
            Quaternion facing = CasterView.transform.rotation;
            BeginFrontView(origin, CasterView.transform.forward, camera);
            yield return new WaitForSeconds(leadSeconds);

            // 시전 자세: 살짝 떠올라 머문다 (배경이 솟는 동안).
            CasterView.SetScriptedPose(origin + Vector3.up * liftHeight, facing);

            // 영역 배경은 전투 상태 알림(StateChanged)을 받은 DomainSceneryView가 만든다 → 아직 없으면 잠깐(최대 0.5초) 기다린다.
            // 그래도 없으면(배경 없는 영역) 펼칠 것이 없으니 시전 자세만 보여주고 끝낸다.
            float waited = 0f;
            while (DomainScenery.Current == null && waited < 0.5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            DomainScenery scenery = DomainScenery.Current;
            if (scenery != null)
            {
                yield return scenery.Reveal(origin, camera);
            }

            yield return new WaitForSeconds(holdSeconds);

            CasterView.ClearScriptedPose();
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 마라가 바라보는 방향 정면, 조금 아래에서 마라를 올려다보는 시점으로 컷 → 마라 뒤쪽(카메라 반대편) 먼 배경에 사찰이 선다.
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
