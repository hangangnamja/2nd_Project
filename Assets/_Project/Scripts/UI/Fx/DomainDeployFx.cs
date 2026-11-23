using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 영역 전개 연출 (흉신 마라 3스 "영역: 혈해신전", 임시). 3스를 쓰면 마라만 화면에 잡히고, 영역이 깔린다.
    //  1) 마라 상반신을 정면에서 가까이 잡는 시점으로 컷 → 마라가 가슴 앞에서 두 손을 모아 합장 (3D 모델의 합장 동작)
    //  2) 합장한 채 잠시 보여준 뒤, 카메라가 뒤로 물러나며(줌아웃) 낮은 곳에서 올려다보는 넓은 시점으로 바뀐다
    //  3) 물러나는 동안 발밑에서 얕은 물이 퍼지고 하늘이 검붉게 물들며, 마라 뒤 땅에서 사찰이 솟아오른다(흔들림·먼지, 물에 거꾸로 비침)
    //  4) 잠시 영역을 보여준 뒤 원래 시점으로 → 바로 이어서 영역 공격(적 전체 참) 연출
    // 배경 자체(사찰·물·하늘)는 DomainSceneryView가 영역이 펼쳐질 때 만든 DomainScenery이고, 이 연출은 그 배경의 Reveal을 재생할 뿐이다.
    public class DomainDeployFx : SkillFx
    {
        [Header("카메라: 상반신 클로즈업 (처음)")]
        [Tooltip("마라 정면으로 떨어지는 거리 · 카메라 높이(캡슐 가운데 기준) · 바라보는 높이(가슴~얼굴) · 화각")]
        [SerializeField, Min(0.3f)] private float closeDistance = 1.7f;
        [SerializeField] private float closeHeight = 0.55f;
        [SerializeField] private float closeLookHeight = 0.5f;
        [SerializeField] private float closeFieldOfView = 40f;

        [Header("카메라: 줌아웃 뒤 넓은 시점")]
        [Tooltip("마라 정면으로 떨어지는 거리 · 카메라 높이(캡슐 가운데 기준) · 바라보는 높이 · 화각. 조금 아래에서 올려다봐 마라 뒤 사찰이 보이게")]
        [SerializeField, Min(1f)] private float viewDistance = 5.5f;
        [SerializeField] private float viewHeight = -0.3f;
        [SerializeField] private float lookHeight = 1.6f;
        [SerializeField] private float viewFieldOfView = 55f;

        [Header("동작")]
        [Tooltip("3D 모델이면 클로즈업 때 재생할 Animator 상태 (합장, 마지막 자세 유지) · 연출이 끝나면 돌아갈 상태. 캡슐이면 무시")]
        [SerializeField] private string prayState = "Gassho";
        [SerializeField] private string returnState = "Idle";

        [Header("시간")]
        [Tooltip("컷 직후 합장이 끝나고 버티는 시간(초) → 그다음 줌아웃 시작")]
        [SerializeField, Min(0f)] private float prayHoldSeconds = 1.1f;

        [Tooltip("클로즈업에서 넓은 시점까지 물러나는 시간(초). 사찰이 솟는 것과 동시에 진행")]
        [SerializeField, Min(0.05f)] private float zoomOutSeconds = 1.6f;

        [Tooltip("배경이 다 솟은 뒤 보여주는 시간(초)")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.8f;

        private BattleCameraDirector cinematicCamera;
        private Transform lookPoint;

        // 기본 시전 모션(떠오르며 시전 자세)을 끈다: 땅에 선 채 합장 동작을 보여줘야 해서 (시전 자세가 합장을 덮지 않게).
        public override bool CasterCasts => false;

        // 연출이 중간에 사라져도 카메라·모델 동작이 연출 상태로 남지 않게 되돌린다.
        private void OnDestroy()
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }

            if (CasterView != null)
            {
                CasterView.PlayModelState(returnState);
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
            Vector3 forward = FlatForward(CasterView.transform.forward);

            // 1) 상반신 클로즈업 + 합장
            BeginCloseUp(origin, forward, camera);
            CasterView.PlayModelState(prayState);
            yield return new WaitForSeconds(prayHoldSeconds);

            // 영역 배경은 전투 상태 알림(StateChanged)을 받은 DomainSceneryView가 만든다 → 아직 없으면 잠깐(최대 0.5초) 기다린다.
            // 그래도 없으면(배경 없는 영역) 줌아웃만 보여주고 끝낸다.
            float waited = 0f;
            while (DomainScenery.Current == null && waited < 0.5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // 2)·3) 줌아웃과 사찰이 솟는 것을 함께: 줌아웃은 따로 돌리는 코루틴(StartCoroutine)으로 시작하고,
            //       이 코루틴은 배경 Reveal이 끝날 때까지 기다린다 → 둘이 같은 시각에 진행된다.
            Coroutine zoom = camera != null ? StartCoroutine(ZoomOut(origin, forward)) : null;
            DomainScenery scenery = DomainScenery.Current;
            if (scenery != null)
            {
                yield return scenery.Reveal(origin, camera);
            }

            if (zoom != null)
            {
                yield return zoom;
            }

            yield return new WaitForSeconds(holdSeconds);

            // 4) 원래 시점으로
            CasterView.PlayModelState(returnState);
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 마라가 바라보는 방향 정면, 가슴 높이에서 상반신을 가까이 잡는 시점으로 컷.
        // lookPoint(바라볼 점)는 줌아웃 동안 함께 옮기려고 이 연출의 자식으로 만들어 둔다.
        private void BeginCloseUp(Vector3 origin, Vector3 forward, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = origin + Vector3.up * closeLookHeight;
            camera.BeginCinematic(origin + forward * closeDistance + Vector3.up * closeHeight, lookPoint, closeFieldOfView);
            cinematicCamera = camera;
        }

        // 클로즈업 자리·화각에서 넓은 시점 자리·화각으로 매 프레임 옮긴다.
        // Mathf.SmoothStep(0, 1, t): 처음과 끝이 부드러운 0~1 곡선 → 천천히 물러나기 시작해 천천히 멈춘다.
        // Vector3.Lerp(a, b, k): a와 b 사이 k 비율 지점 (k = 0이면 a, 1이면 b).
        private IEnumerator ZoomOut(Vector3 origin, Vector3 forward)
        {
            Vector3 fromPosition = origin + forward * closeDistance + Vector3.up * closeHeight;
            Vector3 toPosition = origin + forward * viewDistance + Vector3.up * viewHeight;
            Vector3 fromLook = origin + Vector3.up * closeLookHeight;
            Vector3 toLook = origin + Vector3.up * lookHeight;

            for (float elapsed = 0f; elapsed < zoomOutSeconds; elapsed += Time.deltaTime)
            {
                ApplyZoom(Mathf.SmoothStep(0f, 1f, elapsed / zoomOutSeconds), fromPosition, toPosition, fromLook, toLook);
                yield return null;
            }

            ApplyZoom(1f, fromPosition, toPosition, fromLook, toLook);
        }

        // 줌아웃 진행도 k(0~1)에 맞는 카메라 자리·바라볼 점·화각을 적용한다.
        private void ApplyZoom(float k, Vector3 fromPosition, Vector3 toPosition, Vector3 fromLook, Vector3 toLook)
        {
            if (cinematicCamera == null)
            {
                return;
            }

            if (lookPoint != null)
            {
                lookPoint.position = Vector3.Lerp(fromLook, toLook, k);
            }

            cinematicCamera.MoveCinematic(Vector3.Lerp(fromPosition, toPosition, k), Mathf.Lerp(closeFieldOfView, viewFieldOfView, k));
        }

        // 바라보는 방향에서 높이를 빼고 길이 1로 (비스듬히 서 있어도 수평으로 정면을 잡는다). 방향이 없으면 +z.
        private static Vector3 FlatForward(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }
}
