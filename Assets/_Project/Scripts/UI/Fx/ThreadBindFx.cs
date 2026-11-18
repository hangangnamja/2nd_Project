using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 실 묶기 연출 (격발 딜러 패시브 추가 공격, 임시). 보라색 실을 쏘아 대상 몸을 칭칭 감고, 그대로 조여 피해를 입힌다.
    //  1) 쏘기: 딜러 손 앞에서 실 여러 가닥이 살짝 휘며 대상에게 쭉 뻗어 간다
    //  2) 감기: 닿은 실이 대상 몸을 나선으로 여러 바퀴 감는다 (실마다 감는 높이·방향이 조금씩 다름)
    //  3) 조이기: 감긴 나선이 몸 쪽으로 오그라들며 밝아지고, 점점 빨라지는 맥동마다 대상이 떨림 → 꽉 조이는 순간 피해(onImpact 비율 1)·불꽃
    //  4) 실이 번쩍인 뒤 가늘어지며 사라진다
    // 실 모양(재질·색·굵기)은 프리팹의 견본 선, 불꽃은 파티클 프리팹이 맡고, 이 코드는 점 위치·순서·시간만 정한다. 카메라 컷은 없다 (자주 나가는 추가 공격이라 짧게).
    public class ThreadBindFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("실 견본 (꺼 둔 자식 LineRenderer)")]
        [SerializeField] private LineRenderer threadLine;

        [Tooltip("조여서 피해가 들어갈 때 대상에게 터지는 불꽃 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject squeezeBurstPrefab;

        [Header("쏘기")]
        [Tooltip("실 가닥 수")]
        [SerializeField, Range(1, 8)] private int threadCount = 3;

        [Tooltip("실이 대상까지 뻗는 시간(초) · 휘는 정도 (실 길이 대비)")]
        [SerializeField, Min(0.01f)] private float shootSeconds = 0.16f;
        [SerializeField, Range(0f, 0.5f)] private float shootCurve = 0.15f;

        [Tooltip("실이 나오는 곳: 딜러 몸 중심에서 대상 쪽으로 · 위로")]
        [SerializeField] private float handForward = 0.5f;
        [SerializeField] private float handHeight = 0.3f;

        [Header("감기")]
        [Tooltip("나선 반지름 · 감는 바퀴 수 · 감는 높이 범위 (캡슐 중심 기준 아래 ~ 위)")]
        [SerializeField, Min(0.1f)] private float coilRadius = 0.75f;
        [SerializeField, Min(0.5f)] private float coilTurns = 3f;
        [SerializeField] private float coilBottom = -0.7f;
        [SerializeField] private float coilTop = 0.8f;

        [SerializeField, Min(0.01f)] private float wrapSeconds = 0.22f;

        [Header("조이기")]
        [Tooltip("조이는 시간(초) · 다 조였을 때 반지름 배율")]
        [SerializeField, Min(0.01f)] private float squeezeSeconds = 0.35f;
        [SerializeField, Range(0.1f, 1f)] private float squeezeScale = 0.45f;

        [Tooltip("밝기: 평소 → 조인 끝 → 피해 순간 (재질 색에 곱함)")]
        [SerializeField] private float idleIntensity = 1.3f;
        [SerializeField] private float tightIntensity = 2.6f;
        [SerializeField] private float snapIntensity = 4.5f;

        [Tooltip("맥동 간격: 처음 → 끝 (점점 빨라짐) · 맥동마다 흔들림")]
        [SerializeField, Min(0.02f)] private float pulseStartInterval = 0.12f;
        [SerializeField, Min(0.02f)] private float pulseEndInterval = 0.05f;
        [SerializeField, Min(0f)] private float pulseShake = 0.04f;

        [SerializeField, Min(0f)] private float squeezeShake = 0.25f;
        [SerializeField, Min(0.1f)] private float burstScale = 0.9f;

        [Tooltip("피해 뒤 실이 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.25f;

        // 실 한 가닥: 선, 감는 높이 범위의 시작·끝(위→아래 또는 아래→위), 감는 시작 각도·방향.
        private class Thread
        {
            public LineRenderer Line;
            public float From;
            public float To;
            public float StartAngle;
            public float Direction;
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            if (targets.Count == 0)
            {
                Destroy(gameObject);
                yield break;
            }

            (BattleUnit target, Vector3 body) = targets[0];
            Vector3 toTarget = body - caster;
            toTarget.y = 0f;
            Vector3 forward = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector3.forward;
            Vector3 hand = caster + forward * handForward + Vector3.up * handHeight;

            var lines = new GlowLines(threadLine, transform);
            lines.SetIntensity(idleIntensity);
            List<Thread> threads = CreateThreads(lines);

            yield return Shoot(threads, hand, body, forward);
            yield return Wrap(threads, hand, body);
            yield return Squeeze(lines, threads, hand, body, camera);

            if (squeezeBurstPrefab != null)
            {
                GameObject burst = Instantiate(squeezeBurstPrefab, body, Quaternion.identity);
                burst.transform.localScale = Vector3.one * burstScale;
            }

            onImpact(target, 1f);
            if (camera != null)
            {
                camera.Shake(squeezeShake, 0.2f);
            }

            yield return Animate(fadeSeconds, t =>
            {
                lines.SetIntensity(Mathf.Lerp(snapIntensity, idleIntensity, t));
                lines.SetWidthScale(Mathf.Lerp(1.4f, 0f, t));
                lines.SetAlpha(1f - t);
            });

            Destroy(gameObject);
        }

        // 실마다 감는 방식을 조금씩 다르게: 절반은 위에서 아래로, 절반은 아래에서 위로, 시작 각도는 고르게 나눔.
        private List<Thread> CreateThreads(GlowLines lines)
        {
            var threads = new List<Thread>();
            for (int i = 0; i < threadCount; i++)
            {
                bool downward = i % 2 == 0;
                threads.Add(new Thread
                {
                    Line = lines.Create(2),
                    From = downward ? coilTop : coilBottom,
                    To = downward ? coilBottom : coilTop,
                    StartAngle = 360f / threadCount * i,
                    Direction = downward ? 1f : -1f,
                });
            }

            return threads;
        }

        // 1) 쏘기: 손 → 감기 시작점까지 살짝 휜 곡선을 머리가 0 → 1로 뻗어 간다 (1 - (1 - t)²: 빠르게 쏘고 닿을 때 느려짐).
        // 곡선 = 2차 베지어: 손과 끝점 사이 가운데를 옆으로 비킨 점을 지나 휜다. 실마다 비키는 방향이 달라 부채꼴로 퍼져 날아감.
        private IEnumerator Shoot(List<Thread> threads, Vector3 hand, Vector3 body, Vector3 forward)
        {
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            var paths = new List<Vector3[]>();
            for (int i = 0; i < threads.Count; i++)
            {
                Vector3 end = CoilPoint(threads[i], body, 0f, 1f);
                float spread = threads.Count > 1 ? Mathf.Lerp(-1f, 1f, i / (float)(threads.Count - 1)) : 0f;
                Vector3 bend = (side * spread + Vector3.up * 0.3f) * Vector3.Distance(hand, end) * shootCurve;
                paths.Add(Bezier(hand, (hand + end) * 0.5f + bend, end, 16));
            }

            yield return Animate(shootSeconds, t =>
            {
                float head = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < threads.Count; i++)
                {
                    GlowLines.DrawPartial(threads[i].Line, paths[i], head);
                }
            });
        }

        // 2) 감기: 손 → 나선 시작점까지는 곧게 남고, 나선이 감기 시작점에서 끝점까지 progress만큼 감긴다.
        private IEnumerator Wrap(List<Thread> threads, Vector3 hand, Vector3 body)
        {
            yield return Animate(wrapSeconds, t => DrawThreads(threads, hand, body, t, 1f));
        }

        // 3) 조이기: 반지름 배율 1 → squeezeScale (Mathf.SmoothStep: 처음·끝이 부드러운 S자). 점점 빨라지는 맥동마다 실이 번쩍이고 화면이 살짝 떨린다.
        private IEnumerator Squeeze(GlowLines lines, List<Thread> threads, Vector3 hand, Vector3 body, BattleCameraDirector camera)
        {
            float elapsed = 0f;
            float nextPulse = 0f;
            float flash = 0f;
            while (elapsed < squeezeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / squeezeSeconds);
                DrawThreads(threads, hand, body, 1f, Mathf.Lerp(1f, squeezeScale, Mathf.SmoothStep(0f, 1f, t)));

                if (elapsed >= nextPulse)
                {
                    nextPulse = elapsed + Mathf.Lerp(pulseStartInterval, pulseEndInterval, t);
                    flash = 1f;
                    if (camera != null)
                    {
                        camera.Shake(pulseShake, 0.06f);
                    }
                }

                // 번쩍임은 매 프레임 사그라든다 (Mathf.MoveTowards: 초당 10씩 0을 향해 줄임).
                flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 10f);
                lines.SetIntensity(Mathf.Lerp(idleIntensity, tightIntensity, t) * (1f + flash * 0.5f));
                yield return null;
            }

            lines.SetIntensity(snapIntensity);
            lines.SetWidthScale(1.4f);
        }

        // 실 전체를 그린다: [손] → [감기 시작점] → 나선 progress만큼. scale: 나선 반지름 배율 (조일 때 줄어듦).
        private void DrawThreads(List<Thread> threads, Vector3 hand, Vector3 body, float progress, float scale)
        {
            const int coilPoints = 48;
            foreach (Thread thread in threads)
            {
                int count = Mathf.Max(2, Mathf.CeilToInt(coilPoints * progress) + 1);
                thread.Line.positionCount = count + 1;
                thread.Line.SetPosition(0, hand);
                for (int i = 0; i < count; i++)
                {
                    thread.Line.SetPosition(i + 1, CoilPoint(thread, body, progress * i / (count - 1), scale));
                }
            }
        }

        // 나선 위의 점: 진행도 p(0~1)에서 높이 = 시작 높이 → 끝 높이, 각도 = 시작 각도 + 방향 × 바퀴 수 × 360 × p.
        // 점 = 몸 중심 + (cos, 0, sin) × 반지름 × scale + 위 × 높이.
        private Vector3 CoilPoint(Thread thread, Vector3 body, float p, float scale)
        {
            float angle = (thread.StartAngle + thread.Direction * coilTurns * 360f * p) * Mathf.Deg2Rad;
            float height = Mathf.Lerp(thread.From, thread.To, p);
            return body + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * coilRadius * scale + Vector3.up * height;
        }

        // 2차 베지어 곡선 점들: (1-t)²·a + 2(1-t)t·b + t²·c. a에서 출발해 b 쪽으로 휘며 c에 닿는다.
        private static Vector3[] Bezier(Vector3 a, Vector3 b, Vector3 c, int segments)
        {
            var points = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float u = 1f - t;
                points[i] = u * u * a + 2f * u * t * b + t * t * c;
            }

            return points;
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
