using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 영역 붕괴 연출 (하이퍼캐리 변신 종결기, 임시). 변신 때 펼친 경계(고립)를 터뜨리며 닫는 느낌.
    //  1) 영역 등장: 캐리를 낮은 곳에서 올려다보는 시점으로 컷 → 전장을 둘러싼 붉은 격자 영역 벽이 드러난다
    //  2) 모으기: 영역 벽의 기운이 캐리에게 빨려 들어가고, 심장 박동처럼 점점 빨라지는 맥동(고리 수축·빛 덩어리 부풂·벽 번쩍임)과 함께
    //     카메라가 천천히 다가간다 → 터지기 직전 잠깐 숨을 죽인다(빨아들임 멈춤·빛 덩어리 수축·벽 어두워짐)
    //  3) 붕괴: 영역 벽이 붉은 테두리의 검은 큐브 파편으로 부서져 흩어짐 + 섬광·폭발 → 대상 전원 피해(onImpact)·강한 흔들림
    //  4) 정리: 잠시 파편이 흩어지는 모습을 보여준 뒤 원래 시점으로 돌아온다
    // 벽·파편·빛의 모양은 프리팹(재질·파티클)이 담당하고, 이 코드는 위치·순서·시간·카메라 컷만 정한다.
    public class BoundaryCollapseFx : SkillFx
    {
        [Header("프리팹 부품 (자식)")]
        [Tooltip("영역 벽: 안쪽에서 보이는 구체 (양면 재질). 대상들과 캐리 사이 가운데에 놓인다")]
        [SerializeField] private Renderer shell;

        [Tooltip("캐리에게 빛이 모이는 파티클 (모으기 단계에 재생)")]
        [SerializeField] private ParticleSystem charge;

        [Tooltip("영역 벽에서 캐리에게 빨려 들어가는 기운 줄기 (구 표면에서 안쪽으로 날아오는 파티클)")]
        [SerializeField] private ParticleSystem siphon;

        [Tooltip("맥동마다 캐리를 향해 오그라드는 고리 (Emit으로 한 개씩 낸다)")]
        [SerializeField] private ParticleSystem pulse;

        [Tooltip("캐리 몸에서 커지는 빛 덩어리")]
        [SerializeField] private Transform core;

        [Tooltip("영역 벽이 부서진 큐브 파편 (구 표면에서 터져 나가는 파티클)")]
        [SerializeField] private ParticleSystem shards;

        [Tooltip("터지는 순간의 섬광·폭발 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject burstPrefab;

        [Header("영역 벽")]
        [Tooltip("영역 반지름 (전장 전체를 덮을 만큼)")]
        [SerializeField, Min(1f)] private float shellRadius = 16f;

        [Tooltip("영역 벽이 나타나는 시간(초)")]
        [SerializeField, Min(0f)] private float shellFadeInSeconds = 0.4f;

        [Tooltip("평소 영역 벽 밝기 → 터지기 직전 밝기 (재질 색에 곱함, 1보다 크면 Bloom으로 번짐)")]
        [SerializeField] private float shellIdleIntensity = 1f;
        [SerializeField] private float shellPeakIntensity = 3.5f;

        [Header("모으기")]
        [Tooltip("기를 모으는 전체 시간(초, 숨 죽이는 시간 제외)")]
        [SerializeField, Min(0.1f)] private float chargeSeconds = 2.6f;

        [Tooltip("빛 덩어리 최대 크기")]
        [SerializeField, Min(0f)] private float coreMaxScale = 2.4f;

        [Tooltip("맥동 한 번에 빛 덩어리가 순간 더 부푸는 비율")]
        [SerializeField, Min(0f)] private float corePulseBump = 0.35f;

        [Tooltip("맥동 간격: 처음 → 끝 (점점 빨라짐)")]
        [SerializeField, Min(0.03f)] private float pulseStartInterval = 0.6f;
        [SerializeField, Min(0.03f)] private float pulseEndInterval = 0.12f;

        [Tooltip("빨려 들어가는 기운 줄기 수(초당): 처음 → 끝")]
        [SerializeField, Min(0f)] private float siphonStartRate = 15f;
        [SerializeField, Min(0f)] private float siphonEndRate = 260f;

        [Tooltip("맥동마다의 떨림: 처음 → 끝")]
        [SerializeField, Min(0f)] private float pulseShakeStart = 0.03f;
        [SerializeField, Min(0f)] private float pulseShakeEnd = 0.14f;

        [Tooltip("모으는 동안 카메라가 다가가는 정도 (끝에서 거리 = 처음 거리 × 이 값)")]
        [SerializeField, Range(0.3f, 1f)] private float dollyIn = 0.72f;

        [Tooltip("모으기 끝의 화각 (처음 화각에서 좁아지며 긴장감)")]
        [SerializeField] private float chargeEndFieldOfView = 60f;

        [Tooltip("터지기 직전 숨을 죽이는 시간(초): 빨아들임이 멈추고 빛 덩어리가 오그라들며 벽이 어두워진다")]
        [SerializeField, Min(0f)] private float hushSeconds = 0.3f;

        [Header("붕괴")]
        [SerializeField, Min(1f)] private float burstScale = 3f;
        [SerializeField, Min(0f)] private float burstShake = 1f;
        [SerializeField, Min(0f)] private float burstShakeSeconds = 0.9f;

        [Tooltip("터진 뒤 흩어지는 파편을 보여주는 시간(초), 그다음 원래 시점으로")]
        [SerializeField, Min(0f)] private float holdAfterBurst = 1f;

        [Header("올려다보는 시점")]
        [Tooltip("캐리에게서 카메라까지의 수평 거리 (캐리 정면 기준 비스듬히)")]
        [SerializeField] private float viewDistance = 6f;

        [Tooltip("카메라 높이 (땅 기준, 낮을수록 올려다봄)")]
        [SerializeField] private float viewHeight = 0.3f;

        [Tooltip("카메라가 바라보는 높이 (캐리 발밑 기준) → 캐리 위로 펼쳐진 영역 벽이 화면에 많이 담긴다")]
        [SerializeField] private float viewLookHeight = 4.5f;

        [SerializeField] private float viewFieldOfView = 72f;

        [Tooltip("캡슐 중심에서 땅까지의 높이 (캡슐 원점이 몸 가운데라 -1이 발밑)")]
        [SerializeField] private float groundOffset = -1f;

        // 영역이 깨지는 순간 헤일로·날개가 사라지도록, 변신 모습을 바꾸는 연출로 표시한다.
        public override bool ChangesAppearance => true;

        // 영역 벽 재질 복사본과 원래 색. 밝기는 원래 색 × 배율로 바꾼다.
        private Material shellMaterial;
        private Color shellBaseColor;

        // 올려다보는 시점의 기준 (모으는 동안 카메라를 다가가게 할 때 다시 계산).
        private Vector3 viewOrigin;
        private Vector3 viewForward;
        private Vector3 viewSide;

        // 연출 컷을 건 카메라. 연출이 중간에 사라져도 카메라가 연출 시점에 남지 않게 OnDestroy에서 되돌린다.
        private BattleCameraDirector cinematicCamera;

        private void OnDestroy()
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }

            // renderer.material로 만든 복사본은 직접 지운다 (안 지우면 메모리에 남음).
            if (shellMaterial != null)
            {
                Destroy(shellMaterial);
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            Vector3 casterGround = Ground(caster);
            Vector3 targetCenter = Ground(GetCenter(targets, caster));

            PlaceParts(caster, casterGround, targetCenter);
            BeginLookUp(casterGround, targetCenter, camera);

            yield return FadeInShell();
            yield return Charge(camera);
            yield return Hush();
            Burst(caster, targets, onImpact, camera);

            yield return new WaitForSeconds(holdAfterBurst);
            if (camera != null)
            {
                camera.EndCinematic();
            }

            // 파편이 마저 흩어질 시간을 두고 연출 오브젝트를 지운다.
            yield return new WaitForSeconds(shards != null ? shards.main.startLifetime.constantMax : 0.5f);
            Destroy(gameObject);
        }

        // 부품 배치: 영역 벽은 캐리와 대상들 사이 가운데 땅에 놓아(반구가 땅 위로) 둘 다 감싸고, 빛 덩어리는 캐리 몸에 둔다.
        private void PlaceParts(Vector3 caster, Vector3 casterGround, Vector3 targetCenter)
        {
            // Vector3.Lerp(a, b, 0.5): 두 점의 가운데.
            Vector3 domeCenter = Vector3.Lerp(casterGround, targetCenter, 0.5f);

            if (shell != null)
            {
                shell.transform.position = domeCenter;
                // 기본 구체 메시 지름이 1이라 지름 = 반지름 × 2.
                shell.transform.localScale = Vector3.one * (shellRadius * 2f);
                shellMaterial = shell.material;
                shellBaseColor = shellMaterial.color;
                SetShellIntensity(0f);
            }

            if (shards != null)
            {
                shards.transform.position = domeCenter;
                // ParticleSystem.ShapeModule: 파편이 생기는 모양. 구 표면에서 생기도록 반지름을 영역에 맞춘다.
                ParticleSystem.ShapeModule shape = shards.shape;
                shape.radius = shellRadius;
            }

            if (core != null)
            {
                core.position = caster;
                core.localScale = Vector3.zero;
            }

            if (charge != null)
            {
                charge.transform.position = caster;
            }

            // 빨아들이는 기운은 영역 벽(구 표면)에서 생겨 안쪽으로 날아오므로, 캐리 자리를 중심으로 영역 반지름에 맞춘다.
            if (siphon != null)
            {
                siphon.transform.position = caster;
                ParticleSystem.ShapeModule shape = siphon.shape;
                shape.radius = shellRadius * 0.85f;
            }

            if (pulse != null)
            {
                pulse.transform.position = caster;
            }
        }

        // 올려다보는 카메라 자리: 캐리 발밑에서 대상 쪽(viewForward)으로 distance, 옆으로 그 0.35배, 위로 viewHeight.
        private Vector3 GetViewPosition(float distance)
        {
            return viewOrigin + viewForward * distance + viewSide * (distance * 0.35f) + Vector3.up * viewHeight;
        }

        // 캐리를 낮은 곳에서 올려다보는 시점으로 컷한다.
        //  - toTargets: 캐리에서 대상들 쪽 수평 방향. 카메라는 그 방향으로 viewDistance만큼 나가고, 옆으로 조금 비켜(Cross로 구한 옆 방향) 비스듬히 본다
        //  - 바라볼 점은 캐리 머리 위 높은 곳 → 캐리와 그 위로 펼쳐진 영역 벽이 함께 담긴다
        private void BeginLookUp(Vector3 casterGround, Vector3 targetCenter, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            Vector3 toTargets = targetCenter - casterGround;
            toTargets.y = 0f;
            toTargets = toTargets.sqrMagnitude > 0.01f ? toTargets.normalized : Vector3.forward;
            viewOrigin = casterGround;
            viewForward = toTargets;
            viewSide = Vector3.Cross(Vector3.up, toTargets);
            Vector3 viewPosition = GetViewPosition(viewDistance);

            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = casterGround + Vector3.up * viewLookHeight;

            camera.BeginCinematic(viewPosition, lookPoint, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 1) 영역 벽이 0 → 평소 밝기로 서서히 나타난다.
        private IEnumerator FadeInShell()
        {
            float elapsed = 0f;
            while (elapsed < shellFadeInSeconds)
            {
                elapsed += Time.deltaTime;
                SetShellIntensity(shellIdleIntensity * Mathf.Clamp01(elapsed / shellFadeInSeconds));
                yield return null;
            }

            SetShellIntensity(shellIdleIntensity);
        }

        // 2) 모으기: 기운이 캐리에게 빨려 들어가고, 점점 빨라지는 맥동마다 고리 수축·빛 덩어리 부풂·벽 번쩍임·떨림.
        // 카메라는 천천히 다가가며 화각을 좁혀 긴장감을 쌓는다. t = 진행도(0~1), t * t: 처음엔 느리고 끝으로 갈수록 몰아침.
        private IEnumerator Charge(BattleCameraDirector camera)
        {
            if (charge != null)
            {
                charge.Play(true);
            }

            if (siphon != null)
            {
                siphon.Play(true);
            }

            float elapsed = 0f;
            float nextPulse = 0f;
            float pulseGlow = 0f;
            while (elapsed < chargeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / chargeSeconds);
                float eased = t * t;

                // 맥동: 간격이 처음 값에서 끝 값으로 줄어든다 (Mathf.Lerp(a, b, t) = a에서 b로 t만큼).
                if (elapsed >= nextPulse)
                {
                    Pulse(camera, t);
                    pulseGlow = 1f;
                    nextPulse = elapsed + Mathf.Lerp(pulseStartInterval, pulseEndInterval, t);
                }

                // 맥동 빛은 매 프레임 빠르게 식는다 (Mathf.MoveTowards: 0 쪽으로 이번 프레임 양만큼).
                pulseGlow = Mathf.MoveTowards(pulseGlow, 0f, Time.deltaTime * 6f);

                SetShellIntensity(Mathf.Lerp(shellIdleIntensity, shellPeakIntensity, eased) * (1f + 0.35f * pulseGlow));

                if (core != null)
                {
                    core.localScale = Vector3.one * (coreMaxScale * eased * (1f + corePulseBump * pulseGlow));
                }

                if (siphon != null)
                {
                    // emission.rateOverTime: 초당 만드는 수. 모을수록 줄기가 굵어진다.
                    ParticleSystem.EmissionModule emission = siphon.emission;
                    emission.rateOverTime = Mathf.Lerp(siphonStartRate, siphonEndRate, eased);
                }

                if (camera != null)
                {
                    float fov = Mathf.Lerp(viewFieldOfView, chargeEndFieldOfView, eased);
                    camera.MoveCinematic(GetViewPosition(viewDistance * Mathf.Lerp(1f, dollyIn, eased)), fov);
                }

                yield return null;
            }
        }

        // 맥동 한 번: 캐리를 향해 오그라드는 고리 하나 + 진행도에 맞춘 떨림.
        private void Pulse(BattleCameraDirector camera, float t)
        {
            if (pulse != null)
            {
                // Emit(개수): 파티클을 그 자리에서 바로 만든다 (재생 중이 아니어도 됨).
                pulse.Emit(1);
            }

            if (camera != null)
            {
                camera.Shake(Mathf.Lerp(pulseShakeStart, pulseShakeEnd, t), 0.12f);
            }
        }

        // 터지기 직전 숨 죽이기: 빨아들임·빛 모음을 멈추고, 빛 덩어리를 70%로 오그라뜨리고 벽을 조금 어둡게 → 다음 순간 터짐이 더 크게 느껴진다.
        private IEnumerator Hush()
        {
            if (siphon != null)
            {
                siphon.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (charge != null)
            {
                charge.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            Vector3 coreFrom = core != null ? core.localScale : Vector3.zero;
            float elapsed = 0f;
            while (elapsed < hushSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hushSeconds);
                if (core != null)
                {
                    core.localScale = Vector3.Lerp(coreFrom, coreFrom * 0.7f, t);
                }

                SetShellIntensity(shellPeakIntensity * Mathf.Lerp(1f, 0.6f, t));
                yield return null;
            }
        }

        // 3) 붕괴: 영역 벽을 감추고 그 자리에서 큐브 파편을 터뜨린다. 캐리 자리에서 섬광·폭발, 대상 전원에게 피해 전부를 보여준다.
        private void Burst(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            if (shell != null)
            {
                shell.gameObject.SetActive(false);
            }

            if (core != null)
            {
                core.gameObject.SetActive(false);
            }

            if (charge != null)
            {
                // StopEmitting: 새로 만들지만 않고, 이미 날던 빛은 마저 사라지게 둔다.
                charge.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (shards != null)
            {
                shards.Play(true);
            }

            // 영역이 깨지는 순간 변신 모습(헤일로·날개)도 사라진다.
            RevealAppearance();

            if (burstPrefab != null)
            {
                GameObject burst = Instantiate(burstPrefab, caster, Quaternion.identity);
                burst.transform.localScale = Vector3.one * burstScale;
            }

            if (camera != null)
            {
                camera.Shake(burstShake, burstShakeSeconds);
            }

            // 비율 1: 이 연출 한 번으로 대상마다 피해 전부를 보여준다.
            foreach ((BattleUnit unit, Vector3 _) in targets)
            {
                onImpact(unit, 1f);
            }
        }

        // 영역 벽 밝기: 원래 색의 RGB에 배율을 곱한다 (알파는 그대로, 0이면 안 보임).
        private void SetShellIntensity(float intensity)
        {
            if (shellMaterial == null)
            {
                return;
            }

            Color color = shellBaseColor * intensity;
            color.a = shellBaseColor.a * Mathf.Clamp01(intensity);
            shellMaterial.color = color;
        }

        private Vector3 Ground(Vector3 position)
        {
            return new Vector3(position.x, position.y + groundOffset, position.z);
        }

        // 대상들 위치의 평균. 대상이 없으면 시전자 자리.
        private static Vector3 GetCenter(IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets, Vector3 fallback)
        {
            if (targets.Count == 0)
            {
                return fallback;
            }

            Vector3 sum = Vector3.zero;
            foreach ((BattleUnit _, Vector3 position) in targets)
            {
                sum += position;
            }

            return sum / targets.Count;
        }
    }
}
