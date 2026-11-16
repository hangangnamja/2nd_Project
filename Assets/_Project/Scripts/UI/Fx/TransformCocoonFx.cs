using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 변신 연출 (하이퍼캐리 3스, 임시). 황금 구체 안에서 힘을 모아 변신하고, 구체를 깨고 나오며 영역(고립)을 펼친다.
    //  1) 캐리를 정면에서 보는 시점으로 컷 → 캐리를 감싸는 황금 구체가 생기고, 주변의 빛·돌 조각이 구체 둘레를 돈다
    //  2) 힘 모으기: 빛이 구체로 빨려 들고, 점점 빨라지는 맥동마다 구체가 밝아지고 화면이 떨린다
    //  3) 깨고 나옴: 구체가 황금빛 파편으로 터지고 섬광 → 헤일로·날개(변신 모습)가 나타나고, 붉은 영역 벽이 캐리에게서 퍼져 나간다
    //  4) 변신한 모습을 잠시 보여준 뒤 원래 시점으로
    // 모양(구체·빛·파편)은 프리팹의 재질·파티클, 이 코드는 위치·순서·시간·카메라만 정한다. 종결기 BoundaryCollapseFx와 짝 (영역 열기 ↔ 닫기).
    public class TransformCocoonFx : SkillFx
    {
        [Header("프리팹 부품 (자식)")]
        [Tooltip("캐리를 감싸는 황금 구체 (소용돌이 무늬 재질)")]
        [SerializeField] private Renderer cocoon;

        [Tooltip("구체로 빨려 드는 빛 (모으기 동안 재생)")]
        [SerializeField] private ParticleSystem gather;

        [Tooltip("구체 둘레를 도는 돌 조각·빛 (처음부터 재생)")]
        [SerializeField] private ParticleSystem orbit;

        [Tooltip("구체 안에서 커지는 빛 덩어리")]
        [SerializeField] private Transform core;

        [Tooltip("구체가 깨질 때 터지는 황금빛 파편")]
        [SerializeField] private ParticleSystem shards;

        [Tooltip("깨지는 순간의 섬광·폭발 (스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject burstPrefab;

        [Tooltip("깨고 나온 뒤 캐리에게서 퍼져 나가는 영역 벽 (종결기의 영역과 같은 재질)")]
        [SerializeField] private Renderer domain;

        [Header("구체")]
        [Tooltip("구체 지름 (캡슐 키가 2)")]
        [SerializeField, Min(0.5f)] private float cocoonScale = 3.4f;

        [SerializeField, Min(0.01f)] private float cocoonAppearSeconds = 0.5f;

        [Tooltip("구체 밝기: 처음 → 깨지기 직전 (재질 색에 곱함)")]
        [SerializeField] private float cocoonStartIntensity = 0.8f;
        [SerializeField] private float cocoonPeakIntensity = 3f;

        [Tooltip("소용돌이 무늬가 흐르는 빠르기: 처음 → 끝 (재질 텍스처를 옆으로 민다)")]
        [SerializeField] private float swirlStartSpeed = 0.15f;
        [SerializeField] private float swirlEndSpeed = 1.2f;

        [Header("힘 모으기")]
        [SerializeField, Min(0.1f)] private float chargeSeconds = 2.2f;
        [SerializeField, Min(0f)] private float coreMaxScale = 1.8f;
        [SerializeField, Min(0.03f)] private float pulseStartInterval = 0.55f;
        [SerializeField, Min(0.03f)] private float pulseEndInterval = 0.12f;
        [SerializeField, Min(0f)] private float pulseShakeStart = 0.02f;
        [SerializeField, Min(0f)] private float pulseShakeEnd = 0.1f;

        [Tooltip("깨지기 직전 숨 죽이는 시간(초)")]
        [SerializeField, Min(0f)] private float hushSeconds = 0.2f;

        [Header("깨고 나옴")]
        [SerializeField, Min(1f)] private float burstScale = 2.2f;
        [SerializeField, Min(0f)] private float burstShake = 0.6f;
        [SerializeField, Min(0f)] private float burstShakeSeconds = 0.6f;

        [Tooltip("영역 벽이 퍼져 나가는 반지름과 시간(초). 다 퍼지면 사라진다")]
        [SerializeField, Min(1f)] private float domainRadius = 16f;
        [SerializeField, Min(0.05f)] private float domainExpandSeconds = 0.8f;

        [Tooltip("변신한 모습을 보여주는 시간(초), 그다음 원래 시점으로")]
        [SerializeField, Min(0f)] private float holdAfterBurst = 1.4f;

        [Header("정면 시점")]
        [Tooltip("캐리 정면으로 떨어진 카메라 거리 (처음 → 모으기 끝)")]
        [SerializeField] private float viewDistance = 6.5f;
        [SerializeField] private float viewEndDistance = 4.8f;

        [SerializeField] private float viewHeight = 0.2f;

        [Tooltip("카메라가 바라보는 높이 (캐리 몸 가운데 기준)")]
        [SerializeField] private float viewLookHeight = 0.6f;

        [SerializeField] private float viewFieldOfView = 55f;

        // 구체·영역 재질 복사본과 원래 색. 밝기는 원래 색 × 배율.
        private Material cocoonMaterial;
        private Color cocoonBaseColor;
        private Material domainMaterial;
        private Color domainBaseColor;

        private Vector3 viewOrigin;
        private Vector3 viewForward;
        private Vector3 viewSide;
        private BattleCameraDirector cinematicCamera;

        // 구체가 깨지는 순간 헤일로·날개가 나타나도록, 변신 모습을 바꾸는 연출로 표시한다.
        public override bool ChangesAppearance => true;

        private void OnDestroy()
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }

            // renderer.material로 만든 복사본은 직접 지운다.
            if (cocoonMaterial != null)
            {
                Destroy(cocoonMaterial);
            }

            if (domainMaterial != null)
            {
                Destroy(domainMaterial);
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            PlaceParts(caster);
            BeginFrontView(caster, camera);

            yield return AppearCocoon();
            yield return Charge(camera);
            yield return Hush();
            BreakOut(caster, camera);
            yield return ExpandDomain();

            yield return new WaitForSeconds(Mathf.Max(0f, holdAfterBurst - domainExpandSeconds));
            if (camera != null)
            {
                camera.EndCinematic();
            }

            yield return new WaitForSeconds(0.5f);
            Destroy(gameObject);
        }

        // 부품을 캐리 몸에 모은다. 구체·빛 덩어리·영역은 처음엔 크기 0.
        private void PlaceParts(Vector3 caster)
        {
            transform.position = caster;

            if (cocoon != null)
            {
                cocoonMaterial = cocoon.material;
                cocoonBaseColor = cocoonMaterial.color;
                cocoon.transform.localScale = Vector3.zero;
            }

            if (domain != null)
            {
                domainMaterial = domain.material;
                domainBaseColor = domainMaterial.color;
                domain.gameObject.SetActive(false);
            }

            if (core != null)
            {
                core.localScale = Vector3.zero;
            }

            if (orbit != null)
            {
                orbit.Play(true);
            }
        }

        // 캐리 정면에서 보는 시점으로 컷한다. 정면 = 캐리 몸이 바라보는 방향(CasterView의 forward, 높이 차 제거).
        private void BeginFrontView(Vector3 caster, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            Vector3 forward = CasterView != null ? CasterView.transform.forward : Vector3.back;
            forward.y = 0f;
            viewForward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.back;
            viewSide = Vector3.Cross(Vector3.up, viewForward);
            viewOrigin = caster;

            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = caster + Vector3.up * viewLookHeight;

            camera.BeginCinematic(GetViewPosition(viewDistance), lookPoint, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 정면 카메라 자리: 캐리에게서 정면으로 distance, 옆으로 그 0.25배(살짝 비스듬히), 위로 viewHeight.
        private Vector3 GetViewPosition(float distance)
        {
            return viewOrigin + viewForward * distance + viewSide * (distance * 0.25f) + Vector3.up * viewHeight;
        }

        // 1) 구체가 0에서 제 크기로 부풀며 생긴다. 1 - (1 - t)²: 처음에 빠르고 끝에서 부드럽게 멈춤.
        private IEnumerator AppearCocoon()
        {
            if (gather != null)
            {
                gather.Play(true);
            }

            float elapsed = 0f;
            while (elapsed < cocoonAppearSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / cocoonAppearSeconds);
                float eased = 1f - (1f - t) * (1f - t);
                SetCocoonScale(eased);
                SetIntensity(cocoonMaterial, cocoonBaseColor, cocoonStartIntensity * eased);
                ScrollSwirl(swirlStartSpeed);
                yield return null;
            }
        }

        // 2) 힘 모으기: 맥동 간격이 점점 줄고(빨라짐), 맥동마다 구체가 번쩍·화면 떨림. 구체 밝기·소용돌이·빛 덩어리는 t²로 끝에 몰아친다.
        private IEnumerator Charge(BattleCameraDirector camera)
        {
            float elapsed = 0f;
            float nextPulse = 0f;
            float pulseGlow = 0f;
            while (elapsed < chargeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / chargeSeconds);
                float eased = t * t;

                if (elapsed >= nextPulse)
                {
                    pulseGlow = 1f;
                    nextPulse = elapsed + Mathf.Lerp(pulseStartInterval, pulseEndInterval, t);
                    if (camera != null)
                    {
                        camera.Shake(Mathf.Lerp(pulseShakeStart, pulseShakeEnd, t), 0.12f);
                    }
                }

                // 맥동 빛은 빠르게 식는다. 맥동 순간 구체가 4% 부풀었다 돌아온다.
                pulseGlow = Mathf.MoveTowards(pulseGlow, 0f, Time.deltaTime * 6f);
                SetCocoonScale(1f + 0.04f * pulseGlow);

                float intensity = Mathf.Lerp(cocoonStartIntensity, cocoonPeakIntensity, eased) * (1f + 0.4f * pulseGlow);
                SetIntensity(cocoonMaterial, cocoonBaseColor, intensity);
                ScrollSwirl(Mathf.Lerp(swirlStartSpeed, swirlEndSpeed, eased));

                if (core != null)
                {
                    core.localScale = Vector3.one * (coreMaxScale * eased * (1f + 0.3f * pulseGlow));
                }

                if (camera != null)
                {
                    camera.MoveCinematic(GetViewPosition(Mathf.Lerp(viewDistance, viewEndDistance, eased)), viewFieldOfView);
                }

                yield return null;
            }
        }

        // 깨지기 직전 숨 죽이기: 빨려 드는 빛을 멈추고 구체를 살짝 오그라뜨린다 → 다음 순간 깨짐이 더 크게 느껴진다.
        private IEnumerator Hush()
        {
            if (gather != null)
            {
                gather.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            float elapsed = 0f;
            while (elapsed < hushSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hushSeconds);
                SetCocoonScale(Mathf.Lerp(1f, 0.9f, t));
                yield return null;
            }
        }

        // 3) 깨고 나옴: 구체·빛 덩어리를 감추고 황금빛 파편·섬광을 터뜨린다. 이 순간 헤일로·날개가 나타난다.
        private void BreakOut(Vector3 caster, BattleCameraDirector camera)
        {
            if (cocoon != null)
            {
                cocoon.gameObject.SetActive(false);
            }

            if (core != null)
            {
                core.gameObject.SetActive(false);
            }

            if (orbit != null)
            {
                orbit.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (shards != null)
            {
                shards.Play(true);
            }

            if (burstPrefab != null)
            {
                GameObject burst = Instantiate(burstPrefab, caster, Quaternion.identity);
                burst.transform.localScale = Vector3.one * burstScale;
            }

            if (camera != null)
            {
                camera.Shake(burstShake, burstShakeSeconds);
            }

            RevealAppearance();
        }

        // 영역 벽이 캐리에게서 퍼져 나가며 옅어진다 (변신과 함께 고립 경계를 펼침). 다 퍼지면 감춘다.
        private IEnumerator ExpandDomain()
        {
            if (domain == null)
            {
                yield break;
            }

            domain.gameObject.SetActive(true);
            float elapsed = 0f;
            while (elapsed < domainExpandSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / domainExpandSeconds);
                float eased = 1f - (1f - t) * (1f - t);

                // 기본 구체 메시 지름 1 → 지름 = 반지름 × 2. 퍼질수록 밝기가 2 → 0으로 옅어진다.
                domain.transform.localScale = Vector3.one * (domainRadius * 2f * eased);
                SetIntensity(domainMaterial, domainBaseColor, 2f * (1f - t));
                yield return null;
            }

            domain.gameObject.SetActive(false);
        }

        private void SetCocoonScale(float ratio)
        {
            if (cocoon != null)
            {
                cocoon.transform.localScale = Vector3.one * (cocoonScale * ratio);
            }
        }

        // 소용돌이 무늬 흐름: 재질 텍스처 위치(mainTextureOffset)를 옆으로 계속 밀어 구체 표면이 흐르는 것처럼 보이게 한다.
        private void ScrollSwirl(float speed)
        {
            if (cocoonMaterial != null)
            {
                cocoonMaterial.mainTextureOffset += new Vector2(speed * Time.deltaTime, speed * 0.3f * Time.deltaTime);
            }
        }

        // 재질 밝기: 원래 색의 RGB × 배율 (알파는 0~1로 잘라 0이면 안 보임).
        private static void SetIntensity(Material material, Color baseColor, float intensity)
        {
            if (material == null)
            {
                return;
            }

            Color color = baseColor * intensity;
            color.a = baseColor.a * Mathf.Clamp01(intensity);
            material.color = color;
        }
    }
}
