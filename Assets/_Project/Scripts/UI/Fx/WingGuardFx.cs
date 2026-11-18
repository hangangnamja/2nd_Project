using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 날개로 감싸기 연출 (하이퍼캐리 변신 2스 반격 태세를 쓰는 순간, 임시).
    //  1) 캐리 정면으로 컷
    //  2) 변신 모습의 금빛·검보랏빛 날개가 앞으로 접히며 몸 앞에서 엇갈려 캐리를 감싼다
    //  3) 다 감싼 순간 금빛 섬광 + 흔들림, 날개 앞에 금빛·검보랏빛 태양 문장이 펼쳐져 천천히 돌며 버틴다 (받아 낼 준비)
    //  4) 날개를 다시 펼치면 문장이 커지며 흩어지고, 원래 시점으로 → 그 뒤 끌어온 적의 턴이 이어진다
    // 날개 움직임은 변신 모습(TransformAppearance)의 SetWrap(0~1)으로 바꾸고, 이 코드는 순서·시간·카메라만 정한다.
    // 자기 강화 스킬이라 상대 타격이 없다 → BattleFxView가 연출이 끝날 때까지 기다린 뒤 다음 박자로 넘어간다.
    public class WingGuardFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("날개를 다 감싼 순간 터지는 섬광 (비우면 생략)")]
        [SerializeField] private GameObject closeBurstPrefab;
        [SerializeField, Min(0.1f)] private float closeBurstScale = 0.6f;

        [Tooltip("감싼 날개 앞에 펼쳐지는 태양 문장 판 견본 (꺼 둔 자식, 가산 발광 재질)")]
        [SerializeField] private Renderer sigilTemplate;

        [Header("태양 문장 (캐리 기준)")]
        [Tooltip("문장을 캐리 앞으로 내민 거리 · 높이 (캐리 캡슐 중심 기준)")]
        [SerializeField] private float sigilForward = 1.6f;
        [SerializeField] private float sigilHeight = 0.4f;
        [SerializeField, Min(0.1f)] private float sigilSize = 2.6f;

        [Tooltip("문장이 흩어질 때 커지는 배율")]
        [SerializeField, Min(1f)] private float sigilFadeGrow = 1.6f;
        [SerializeField] private float sigilSpinSpeed = 30f;
        [SerializeField, Min(0.05f)] private float sigilAppearSeconds = 0.25f;

        [Tooltip("문장 밝기: 평소 · 펼쳐지는 순간 (재질 색에 곱함, 버티는 동안 맥동)")]
        [SerializeField] private float sigilIntensity = 1.4f;
        [SerializeField] private float sigilFlashIntensity = 3f;

        [Header("카메라 (캐리 기준, 앞 = 캐리가 바라보는 쪽)")]
        [SerializeField] private float viewDistance = 5.5f;
        [SerializeField] private float viewHeight = 1.2f;
        [SerializeField] private float viewSide = 1f;
        [SerializeField] private float lookHeight = 0.5f;
        [SerializeField, Range(20f, 90f)] private float viewFieldOfView = 45f;

        [Header("시간")]
        [SerializeField, Min(0f)] private float leadSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float wrapSeconds = 0.4f;
        [SerializeField, Min(0f)] private float holdSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float openSeconds = 0.35f;

        [SerializeField, Min(0f)] private float closeShake = 0.2f;

        private BattleCameraDirector cinematicCamera;
        private TransformAppearance appearance;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Renderer sigil;
        private Quaternion sigilFacing;
        private Color sigilBaseColor;
        private float sigilAngle;

        // 연출이 중간에 사라져도 카메라는 원래 시점으로, 날개는 펼친 상태로 되돌린다.
        private void OnDestroy()
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }

            if (appearance != null)
            {
                appearance.SetWrap(0f);
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            // 변신 모습은 캐리 화면 오브젝트의 자식으로 붙어 있다 (UnitView.ApplyAppearance). 없으면 날개 없이 섬광만.
            appearance = CasterView != null ? CasterView.GetComponentInChildren<TransformAppearance>() : null;
            BeginView(caster, camera);
            yield return new WaitForSeconds(leadSeconds);

            // 감싸기: 1 - (1 - t)²로 처음에 빠르게 접혔다가 끝에서 부드럽게 멈춘다.
            yield return Animate(wrapSeconds, t => SetWrap(1f - (1f - t) * (1f - t)));
            Close(caster, camera);
            ShowSigil(caster);
            yield return HoldSigil();

            // 펼치기: SmoothStep으로 부드럽게. 문장은 같은 시간 동안 커지며 투명해진다.
            yield return Animate(openSeconds, t =>
            {
                SetWrap(1f - Mathf.SmoothStep(0f, 1f, t));
                FadeSigil(t);
            });

            if (camera != null)
            {
                camera.EndCinematic();
                cinematicCamera = null;
            }

            yield return new WaitForSeconds(0.2f);
            Destroy(gameObject);
        }

        // 캐리가 바라보는 방향(정면)에서 캐리를 보는 자리로 컷. Vector3.Cross(위, 정면): 정면과 위쪽에 수직인 옆 방향.
        private void BeginView(Vector3 caster, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            Vector3 front = GetFront();
            Vector3 side = Vector3.Cross(Vector3.up, front).normalized;

            var look = new GameObject("CinematicLook").transform;
            look.SetParent(transform, false);
            look.position = caster + Vector3.up * lookHeight;
            camera.BeginCinematic(caster + front * viewDistance + side * viewSide + Vector3.up * viewHeight, look, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 다 감싼 순간: 금빛 섬광 + 흔들림.
        private void Close(Vector3 caster, BattleCameraDirector camera)
        {
            if (closeBurstPrefab != null)
            {
                GameObject burst = Instantiate(closeBurstPrefab, caster + Vector3.up * 0.4f, Quaternion.identity);
                burst.transform.localScale = Vector3.one * closeBurstScale;
            }

            if (camera != null)
            {
                camera.Shake(closeShake, 0.25f);
            }
        }

        // 태양 문장을 캐리 앞(정면 쪽)에 세운다. 판(Quad)은 앞면이 -z라 정면 반대쪽을 앞(z)으로 두면 카메라가 앞면을 본다.
        private void ShowSigil(Vector3 caster)
        {
            if (sigilTemplate == null)
            {
                return;
            }

            Vector3 front = GetFront();
            sigil = Instantiate(sigilTemplate, transform);
            sigil.gameObject.SetActive(true);
            sigil.transform.position = caster + front * sigilForward + Vector3.up * sigilHeight;
            sigilFacing = Quaternion.LookRotation(-front, Vector3.up);
            sigilBaseColor = sigilTemplate.sharedMaterial.GetColor(BaseColorId);
            ApplySigil(0f, sigilFlashIntensity, 1f);
        }

        // 버티기: 처음 sigilAppearSeconds 동안 0 → 제 크기로 펼쳐지며(살짝 넘침) 번쩍인 밝기가 평소로 내려오고,
        // 남은 시간은 천천히 돌며 숨 쉬듯 맥동한다 (sin으로 밝기 ±20%).
        private IEnumerator HoldSigil()
        {
            float total = Mathf.Max(holdSeconds, sigilAppearSeconds);
            yield return Animate(total, t =>
            {
                float now = t * total;
                float open = Mathf.Clamp01(now / sigilAppearSeconds);
                float u = open - 1f;
                float size = sigilSize * (1f + 2.7f * u * u * u + 1.7f * u * u);
                float pulse = 1f + 0.2f * Mathf.Sin(now * Mathf.PI * 4f);
                float brightness = Mathf.Lerp(sigilFlashIntensity, sigilIntensity * pulse, open);
                ApplySigil(size, brightness, 1f);
            });
        }

        // 흩어지기: 크기 1 → sigilFadeGrow배, 투명도 1 → 0.
        private void FadeSigil(float t)
        {
            ApplySigil(sigilSize * Mathf.Lerp(1f, sigilFadeGrow, t), sigilIntensity, 1f - t);
        }

        // 문장 판의 회전(앞 방향 축으로 계속 돎)·크기·밝기·투명도를 반영한다.
        // MaterialPropertyBlock: 재질을 복사하지 않고 이 판에만 색을 바꾼다.
        private void ApplySigil(float size, float brightness, float alpha)
        {
            if (sigil == null)
            {
                return;
            }

            sigilAngle += sigilSpinSpeed * Time.deltaTime;
            sigil.transform.rotation = sigilFacing * Quaternion.Euler(0f, 0f, sigilAngle);
            sigil.transform.localScale = Vector3.one * size;

            Color color = sigilBaseColor * brightness;
            color.a = sigilBaseColor.a * alpha;
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            sigil.SetPropertyBlock(block);
        }

        // 캐리가 바라보는 수평 방향 (화면 표시 오브젝트의 앞). 없으면 뒤(-z).
        private Vector3 GetFront()
        {
            Vector3 front = CasterView != null ? CasterView.transform.forward : Vector3.back;
            front.y = 0f;
            return front.sqrMagnitude > 0.001f ? front.normalized : Vector3.back;
        }

        private void SetWrap(float amount)
        {
            if (appearance != null)
            {
                appearance.SetWrap(amount);
            }
        }

        // seconds 동안 0 → 1 진행도를 apply(람다)에 넘긴다 (전투 시간 기준이라 히트스톱이면 함께 멈춘다).
        private static IEnumerator Animate(float seconds, Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            apply(1f);
        }
    }
}
