using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 강탈 연출 (강탈 딜러 2스, 임시). 앞으로 달려가 크게 베고, 뒤돌아 천천히 걸어오는 동안 대상 몸에서 빠져나온 기운이 딜러에게 빨려 든다.
    //  1) 딜러와 대상을 옆에서 보는 시점으로 컷
    //  2) 돌진: 딜러가 대상 바로 앞까지 달려간다 (지나온 길에 빛 줄기 잔상)
    //  3) 베기: 위 뒤쪽에서 아래 앞쪽으로 크게 휘두르는 초승달 검흔이 대상을 가르고 지나감 → 검흔 머리가 대상을 지나는 순간 피해(onImpact 비율 1)·불꽃·흔들림
    //  4) 검흔이 잠시 남았다 사라지고, 딜러는 뒤돌아서 원래 자리로 천천히 걸어온다 (순간 이동 없이 걸어서)
    //  5) 걷는 동안 대상 몸에서 빛 알갱이(빼앗긴 기운)가 계속 빠져나와 딜러를 따라와 몸에 빨려 들어간다 → 다 흡수하면 원래 시점으로
    // 검흔 모양은 견본 선, 기운 알갱이 모양은 파티클(꼬리 포함)이 맡고, 이 코드는 위치·순서·시간·카메라 컷과 알갱이의 길 찾기만 정한다.
    public class StealSlashFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("검흔 견본 (꺼 둔 자식 LineRenderer, 양 끝이 가는 굵기 곡선)")]
        [SerializeField] private LineRenderer slashLine;

        [Tooltip("대상에게서 빠져나와 딜러에게 날아가는 기운 알갱이 (꺼진 방출, 코드가 Emit으로 내보내고 길을 정함)")]
        [SerializeField] private ParticleSystem wisps;

        [Tooltip("벨 때 대상에게 터지는 불꽃 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject hitBurstPrefab;

        [Tooltip("기운을 다 흡수했을 때 딜러 몸에서 터지는 빛 (비우면 생략)")]
        [SerializeField] private GameObject absorbBurstPrefab;

        [Header("돌진")]
        [Tooltip("대상 바로 앞에서 멈추는 거리 (대상과 겹치지 않게)")]
        [SerializeField, Min(0f)] private float stopShort = 1.4f;

        [Tooltip("돌진 시간(초). 순간 이동처럼 보이지 않게 달려가는 모습이 보일 만큼")]
        [SerializeField, Min(0.01f)] private float dashSeconds = 0.3f;

        [Tooltip("돌진 전 숨 고르기(초)")]
        [SerializeField, Min(0f)] private float windUpSeconds = 0.15f;

        [Tooltip("지나온 길에 남는 빛 줄기 굵기 (견본 굵기 대비) · 사라지는 시간(초)")]
        [SerializeField, Min(0f)] private float streakWidth = 0.35f;
        [SerializeField, Min(0.01f)] private float streakFadeSeconds = 0.35f;

        [Header("베기")]
        [Tooltip("휘두르는 호의 각도 범위: 시작(위 뒤쪽) → 끝(아래 앞쪽). 0도 = 정면(대상)")]
        [SerializeField] private float swingStartDegrees = 125f;
        [SerializeField] private float swingEndDegrees = -70f;

        [Tooltip("호 반지름에 더하는 여유 (딜러 ~ 대상 거리 + 이 값)")]
        [SerializeField] private float swingReach = 0.6f;

        [Tooltip("휘두르는 시간(초) · 꼬리 길이(호 전체 대비)")]
        [SerializeField, Min(0.01f)] private float swingSeconds = 0.14f;
        [SerializeField, Range(0.05f, 1f)] private float swingTrail = 0.6f;

        [Tooltip("휘두를 때 딜러가 앞으로 더 들어가는 거리")]
        [SerializeField, Min(0f)] private float swingStep = 0.4f;

        [Tooltip("검흔 겹: 바깥 빛(굵게)과 안쪽 심(가늘고 밝게)의 굵기 배율")]
        [SerializeField, Min(0f)] private float outerWidth = 1f;
        [SerializeField, Min(0f)] private float coreWidth = 0.35f;

        [Tooltip("벤 뒤 검흔이 남아 있는 시간(초) · 사라지는 시간(초)")]
        [SerializeField, Min(0f)] private float slashLingerSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float slashFadeSeconds = 0.35f;

        [SerializeField] private float slashIntensity = 3.5f;
        [SerializeField, Min(0f)] private float hitShake = 0.35f;

        [Header("뒤돌아 걷기")]
        [Tooltip("대시가 끝나고 멈춰 있는 시간(초)")]
        [SerializeField, Min(0f)] private float pauseSeconds = 0.3f;

        [Tooltip("뒤도는 시간(초)")]
        [SerializeField, Min(0.01f)] private float turnSeconds = 0.25f;

        [Tooltip("원래 자리까지 걸어오는 시간(초)")]
        [SerializeField, Min(0.1f)] private float walkSeconds = 1.6f;

        [Tooltip("걸을 때 위아래로 흔들리는 높이")]
        [SerializeField, Min(0f)] private float walkBob = 0.06f;

        [Header("기운 흡수")]
        [Tooltip("초당 빠져나오는 알갱이 수 · 빠져나오는 시간(초, 걷기 시작부터)")]
        [SerializeField, Min(0f)] private float wispRate = 40f;
        [SerializeField, Min(0f)] private float wispEmitSeconds = 1.2f;

        [Tooltip("알갱이가 처음 튀어나오는 속도 · 딜러에게 날아가는 최고 속도")]
        [SerializeField, Min(0f)] private float wispBurstSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float wispChaseSpeed = 9f;

        [Tooltip("알갱이가 딜러 쪽으로 방향을 트는 세기 (클수록 곧게 날아감)")]
        [SerializeField, Min(0f)] private float wispSteer = 4f;

        [Tooltip("딜러 몸에 이만큼 가까워지면 흡수된 것으로 보고 지운다")]
        [SerializeField, Min(0.05f)] private float absorbRadius = 0.35f;

        [Header("카메라")]
        [SerializeField] private bool sideView = true;

        [Tooltip("딜러·대상 사이 선에서 옆으로 물러나는 거리 · 높이(캡슐 중심 기준)")]
        [SerializeField, Min(1f)] private float viewDistance = 9f;
        [SerializeField] private float viewHeight = 1.2f;

        [SerializeField] private float viewFieldOfView = 50f;

        // 연출 컷을 건 카메라. 연출이 중간에 사라져도 카메라·캡슐이 연출 상태로 남지 않게 OnDestroy에서 되돌린다.
        private BattleCameraDirector cinematicCamera;
        private ParticleSystem.Particle[] particles;

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
            if (targets.Count == 0 || CasterView == null)
            {
                Destroy(gameObject);
                yield break;
            }

            (BattleUnit target, Vector3 targetPosition) = targets[0];
            Vector3 origin = CasterView.AnchorPosition;

            // 앞 = 딜러 → 대상(수평). 돌진은 대상 바로 앞(stopShort만큼 떨어진 곳)에서 멈춘다.
            Vector3 forward = Flat(targetPosition - origin);
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Vector3 stop = new Vector3(targetPosition.x, origin.y, targetPosition.z) - forward * stopShort;

            BeginSideView(origin, targetPosition, side, camera);
            CasterView.SetScriptedPose(origin, Quaternion.LookRotation(forward));
            yield return new WaitForSeconds(windUpSeconds);

            yield return Dash(origin, stop, forward);
            Vector3 swingEnd = stop + forward * swingStep;
            yield return Swing(stop, swingEnd, forward, target, targetPosition, onImpact, camera);
            yield return new WaitForSeconds(pauseSeconds);
            yield return Turn(swingEnd, forward, -forward);
            yield return WalkBackAndAbsorb(swingEnd, origin, targetPosition);

            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            CasterView.ClearScriptedPose();
            Destroy(gameObject);
        }

        // 옆 시점으로 컷: 딜러 출발점 ~ 대상 사이 가운데를, 옆(지금 카메라가 있는 쪽)으로 물러나 바라본다.
        private void BeginSideView(Vector3 origin, Vector3 targetPosition, Vector3 side, BattleCameraDirector camera)
        {
            if (!sideView || camera == null)
            {
                return;
            }

            Vector3 middle = (origin + targetPosition) * 0.5f;

            // 두 옆 중 지금 카메라가 있는 쪽을 고른다 (Dot < 0이면 반대쪽).
            Camera view = camera.Camera != null ? camera.Camera : Camera.main;
            if (view != null && Vector3.Dot(side, view.transform.position - middle) < 0f)
            {
                side = -side;
            }

            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = middle;
            camera.BeginCinematic(middle + side * viewDistance + Vector3.up * viewHeight, lookPoint, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 2) 돌진: Mathf.SmoothStep(처음·끝이 부드러운 S자)으로 달려 나가 대상 앞에서 멈춘다. 지나온 길에 빛 줄기를 남긴다.
        private IEnumerator Dash(Vector3 from, Vector3 to, Vector3 forward)
        {
            var streakLines = new GlowLines(slashLine, transform);
            streakLines.SetIntensity(slashIntensity * 0.6f);
            LineRenderer streak = streakLines.Create(2);
            streak.widthMultiplier *= streakWidth;
            Quaternion facing = Quaternion.LookRotation(forward);
            Vector3 lift = Vector3.up * 0.1f;

            yield return Animate(dashSeconds, t =>
            {
                Vector3 position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                CasterView.SetScriptedPose(position, facing);
                streak.SetPosition(0, from + lift);
                streak.SetPosition(1, position + lift);
            });

            StartCoroutine(FadeLines(streakLines, streakFadeSeconds, 0f));
        }

        // 3) 베기: 딜러 몸(조금 위)을 중심으로, 앞 방향·위 방향 평면에 놓인 호를 위 뒤쪽 → 아래 앞쪽으로 휘두른다.
        //  - 호 반지름 = 딜러 ~ 대상 거리 + 여유 → 호가 대상 몸을 가르고 지나감
        //  - 머리는 1 - (1 - t)²로 확 휘두르고, 꼬리가 swingTrail만큼 늦게 따라와 초승달처럼 보임
        //  - 머리가 정면(0도, 대상)을 지나는 순간 벤다. 딜러는 휘두르며 한 발 더 들어간다
        private IEnumerator Swing(
            Vector3 from,
            Vector3 to,
            Vector3 forward,
            BattleUnit target,
            Vector3 targetPosition,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            Vector3 center = from + Vector3.up * 0.4f;
            Vector3 flatTarget = new Vector3(targetPosition.x, center.y, targetPosition.z);
            float radius = Vector3.Distance(center, flatTarget) + swingReach;

            var lines = new GlowLines(slashLine, transform);
            lines.SetIntensity(slashIntensity);
            LineRenderer outer = lines.Create(0);
            LineRenderer core = lines.Create(0);
            outer.widthMultiplier *= outerWidth;
            core.widthMultiplier *= coreWidth;

            // 정면(0도)이 호의 어디쯤인지 (0~1): InverseLerp(시작 각, 끝 각, 0).
            float hitAt = Mathf.InverseLerp(swingStartDegrees, swingEndDegrees, 0f);
            bool hit = false;
            Quaternion facing = Quaternion.LookRotation(forward);
            // 다 휘두르면 머리 = 1, 꼬리 = 1 - swingTrail로 초승달이 그 자리에 남고, FadeLines가 잠시 뒤 지운다.
            float elapsed = 0f;
            while (elapsed < swingSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swingSeconds);
                float head = 1f - (1f - t) * (1f - t);
                float tail = Mathf.Max(0f, head - swingTrail);

                DrawSwing(outer, center, forward, radius, tail, head);
                DrawSwing(core, center, forward, radius, tail, head);
                CasterView.SetScriptedPose(Vector3.Lerp(from, to, head), facing);

                if (!hit && head >= hitAt)
                {
                    hit = true;
                    Hit(target, targetPosition, onImpact, camera);
                }

                yield return null;
            }

            if (!hit)
            {
                Hit(target, targetPosition, onImpact, camera);
            }

            StartCoroutine(FadeLines(lines, slashFadeSeconds, slashLingerSeconds));
        }

        // 호의 from ~ to 구간을 그린다. 진행도 p의 각도 = 시작 각 → 끝 각. 점 = 중심 + (앞 × cos + 위 × sin) × 반지름.
        private void DrawSwing(LineRenderer line, Vector3 center, Vector3 forward, float radius, float from, float to)
        {
            const int points = 40;
            int count = Mathf.Max(2, Mathf.CeilToInt((to - from) * points) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float p = Mathf.Lerp(from, to, i / (float)(count - 1));
                float a = Mathf.Lerp(swingStartDegrees, swingEndDegrees, p) * Mathf.Deg2Rad;
                line.SetPosition(i, center + (forward * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a)) * radius);
            }
        }

        // 벤다: 불꽃 + 피해(비율 1 = 전부) + 흔들림.
        private void Hit(BattleUnit target, Vector3 targetPosition, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            if (hitBurstPrefab != null)
            {
                GameObject burst = Instantiate(hitBurstPrefab, targetPosition, Quaternion.identity);
                burst.transform.localScale = Vector3.one * 1.5f;
            }

            onImpact(target, 1f);
            if (camera != null)
            {
                camera.Shake(hitShake, 0.25f);
            }
        }

        // 선 묶음이 delay초 남아 있다가 fadeSeconds 동안 가늘어지며 투명해진다.
        private static IEnumerator FadeLines(GlowLines lines, float fadeSeconds, float delay)
        {
            if (delay > 0f)
            {
                yield return new WaitForSeconds(delay);
            }

            yield return Animate(fadeSeconds, t =>
            {
                lines.SetWidthScale(1f - t);
                lines.SetAlpha(1f - t);
            });
        }

        // 3) 뒤돌기: 앞 방향 → 반대 방향으로 Slerp(회전을 부드럽게 섞음).
        private IEnumerator Turn(Vector3 position, Vector3 from, Vector3 to)
        {
            Quaternion start = Quaternion.LookRotation(from);
            Quaternion end = Quaternion.LookRotation(to);
            yield return Animate(turnSeconds, t => CasterView.SetScriptedPose(position, Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t))));
        }

        // 4) 천천히 걸어 돌아오며, 대상 몸에서 기운 알갱이를 내보내 딜러에게 흡수시킨다.
        // 걷기: 일정한 속도 + Mathf.Abs(sin)으로 걸음마다 살짝 튀는 높이. 알갱이는 걷는 동안 계속 나오고, 다 흡수될 때까지 조금 더 기다린다.
        private IEnumerator WalkBackAndAbsorb(Vector3 from, Vector3 to, Vector3 source)
        {
            Quaternion facing = Quaternion.LookRotation(Flat(to - from));
            float elapsed = 0f;
            float emitBudget = 0f;
            float afterWalk = 0f;
            while (true)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / walkSeconds);
                float bob = Mathf.Abs(Mathf.Sin(t * Mathf.PI * 6f)) * walkBob;
                Vector3 position = Vector3.Lerp(from, to, t) + Vector3.up * bob;
                CasterView.SetScriptedPose(position, facing);

                // 초당 wispRate개: 이번 프레임에 내보낼 몫을 쌓아 1개가 넘을 때마다 하나씩 내보낸다.
                if (elapsed < wispEmitSeconds)
                {
                    emitBudget += wispRate * Time.deltaTime;
                    while (emitBudget >= 1f)
                    {
                        EmitWisp(source);
                        emitBudget -= 1f;
                    }
                }

                bool absorbedAll = SteerWisps(position);
                if (t >= 1f)
                {
                    afterWalk += Time.deltaTime;
                    if (absorbedAll || afterWalk > 1f)
                    {
                        break;
                    }
                }

                yield return null;
            }

            if (absorbBurstPrefab != null)
            {
                Instantiate(absorbBurstPrefab, to, Quaternion.identity);
            }
        }

        // 알갱이 하나: 대상 몸 안 무작위 지점에서, 바깥으로 튀는 속도로 나온다.
        // EmitParams: 파티클 한 개를 위치·속도를 지정해 내보내는 설정 묶음.
        private void EmitWisp(Vector3 source)
        {
            if (wisps == null)
            {
                return;
            }

            var emit = new ParticleSystem.EmitParams
            {
                position = source + UnityEngine.Random.insideUnitSphere * 0.5f,
                velocity = UnityEngine.Random.onUnitSphere * wispBurstSpeed + Vector3.up * wispBurstSpeed * 0.5f,
                applyShapeToPosition = false,
            };
            wisps.Emit(emit, 1);
        }

        // 살아 있는 알갱이마다 딜러 몸(가슴 높이) 쪽으로 방향을 틀어 날아가게 하고, 가까워지면 지운다.
        // 속도 = Lerp(지금 속도, 딜러 방향 × 최고 속도, 방향 트는 세기 × 시간) → 처음엔 튀어나간 방향으로 가다가 점점 딜러를 쫓는다.
        // GetParticles/SetParticles: 파티클 시스템의 알갱이들을 배열로 꺼내 고친 뒤 되돌려 놓는다. 수명(remainingLifetime)을 0으로 하면 사라진다.
        // 반환값: 남은 알갱이가 하나도 없으면 true.
        private bool SteerWisps(Vector3 casterPosition)
        {
            if (wisps == null)
            {
                return true;
            }

            if (particles == null || particles.Length < wisps.main.maxParticles)
            {
                particles = new ParticleSystem.Particle[wisps.main.maxParticles];
            }

            int count = wisps.GetParticles(particles);
            Vector3 goal = casterPosition + Vector3.up * 0.3f;
            for (int i = 0; i < count; i++)
            {
                Vector3 toGoal = goal - particles[i].position;
                if (toGoal.sqrMagnitude < absorbRadius * absorbRadius)
                {
                    particles[i].remainingLifetime = 0f;
                    continue;
                }

                Vector3 desired = toGoal.normalized * wispChaseSpeed;
                particles[i].velocity = Vector3.Lerp(particles[i].velocity, desired, Mathf.Clamp01(wispSteer * Time.deltaTime));
            }

            wisps.SetParticles(particles, count);
            return count == 0;
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

        // 수평 방향만 남긴 단위 벡터 (0이면 앞).
        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }
    }
}
