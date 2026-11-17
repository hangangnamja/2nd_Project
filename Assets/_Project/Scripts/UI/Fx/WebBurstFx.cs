using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 거미줄 폭발 연출 (지속피해 격발 딜러 2스, 임시). 레퍼런스: 적 무리를 덮는 분홍빛 거미줄이 펼쳐졌다가 그 모양대로 터져 갈라진다.
    //  1) 대상들 가운데에 카메라를 마주 보는 거미줄이 펼쳐진다: 중심에서 바퀴살이 뻗고 → 안쪽부터 고리 줄이 감긴다
    //  2) 기 모으기: 거미줄이 점점 빨라지는 맥동과 함께 밝아진다
    //  3) 터짐: 거미줄이 번쩍이며 실이 엇갈리는 매듭마다 불꽃이 터지고, 대상 전원 피해(onImpact 비율 1)·흔들림
    //  4) 갈라짐: 거미줄이 매듭 사이 조각으로 끊어져 중심에서 바깥으로 날아가며 돌고, 가늘어지며 사라진다
    // 선 모양(재질·색·굵기)은 프리팹의 견본 선이, 불꽃은 파티클 프리팹이 담당하고, 이 코드는 선의 점 위치·순서·시간만 정한다.
    public class WebBurstFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("거미줄 선 견본 (꺼 둔 자식 LineRenderer). 재질·색·굵기를 여기서 정한다")]
        [SerializeField] private LineRenderer webLine;

        [Tooltip("실이 엇갈리는 매듭마다 터지는 작은 불꽃 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject knotBurstPrefab;

        [Tooltip("대상마다 터지는 불꽃 (비우면 생략)")]
        [SerializeField] private GameObject hitBurstPrefab;

        [Tooltip("거미줄 중심에서 터지는 섬광 (비우면 생략)")]
        [SerializeField] private GameObject centerBurstPrefab;

        [Header("거미줄 모양")]
        [Tooltip("거미줄 반지름")]
        [SerializeField, Min(1f)] private float radius = 6f;

        [Tooltip("바퀴살 수")]
        [SerializeField, Range(3, 24)] private int spokes = 9;

        [Tooltip("고리 줄 수")]
        [SerializeField, Range(1, 10)] private int rings = 4;

        [Tooltip("가장 안쪽 고리의 위치 (반지름 대비)")]
        [SerializeField, Range(0.05f, 0.9f)] private float innerRing = 0.22f;

        [Tooltip("고리 줄이 바퀴살 사이에서 중심 쪽으로 처지는 정도 (0 = 곧은 다각형)")]
        [SerializeField, Range(0f, 0.5f)] private float ringSag = 0.12f;

        [Tooltip("바퀴살 각도·길이를 비트는 정도 (손으로 친 거미줄처럼)")]
        [SerializeField, Range(0f, 20f)] private float spokeAngleJitter = 7f;
        [SerializeField, Range(0f, 0.4f)] private float spokeLengthJitter = 0.15f;

        [Tooltip("거미줄 중심 높이 (대상들 가운데 캡슐 중심 기준)")]
        [SerializeField] private float centerHeight = 0.6f;

        [Tooltip("거미줄을 대상들보다 카메라 반대쪽으로 미는 거리 (적들이 거미줄 앞에 걸린 것처럼)")]
        [SerializeField] private float pushBehind = 0.8f;

        [Header("펼치기")]
        [Tooltip("시전자가 기를 모으는 시간(초)")]
        [SerializeField, Min(0f)] private float windUpSeconds = 0.25f;

        [Tooltip("바퀴살이 뻗는 시간(초)")]
        [SerializeField, Min(0.01f)] private float spokeSeconds = 0.18f;

        [Tooltip("고리 줄 하나가 감기는 시간(초). 안쪽부터 차례로 감긴다")]
        [SerializeField, Min(0.01f)] private float ringSeconds = 0.09f;

        [Header("기 모으기")]
        [Tooltip("터지기 전 맥동하며 밝아지는 시간(초)")]
        [SerializeField, Min(0f)] private float chargeSeconds = 0.45f;

        [Tooltip("밝기: 평소 → 모으기 끝 (재질 색에 곱함)")]
        [SerializeField] private float idleIntensity = 1f;
        [SerializeField] private float chargeIntensity = 2.4f;

        [Tooltip("맥동 간격: 처음 → 끝 (점점 빨라짐)")]
        [SerializeField, Min(0.03f)] private float pulseStartInterval = 0.18f;
        [SerializeField, Min(0.03f)] private float pulseEndInterval = 0.06f;

        [Header("터짐·갈라짐")]
        [SerializeField] private float burstIntensity = 4.5f;
        [SerializeField, Min(0f)] private float burstShake = 0.4f;
        [SerializeField, Min(0f)] private float burstShakeSeconds = 0.4f;
        [SerializeField, Min(0.1f)] private float knotBurstScale = 0.5f;
        [SerializeField, Min(0.1f)] private float centerBurstScale = 2f;

        [Tooltip("조각이 날아가는 거리 (가장 바깥 조각 기준, 안쪽은 덜 날아감)")]
        [SerializeField, Min(0f)] private float scatterDistance = 4f;

        [Tooltip("조각이 날아가며 도는 최대 각도")]
        [SerializeField, Range(0f, 360f)] private float scatterSpin = 120f;

        [Tooltip("조각이 날아가며 아래로 떨어지는 거리")]
        [SerializeField, Min(0f)] private float scatterDrop = 1.2f;

        [Tooltip("조각이 날아가 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float scatterSeconds = 0.6f;

        // 거미줄 평면: 중심 + 가로·세로 방향 (카메라를 마주 본다).
        private Vector3 center;
        private Vector3 planeRight;
        private Vector3 planeUp;

        // 고리 위치 (반지름 대비 비율, 안쪽부터).
        private float[] ringFractions;

        // 끊어진 조각 하나: 선, 원래 점들, 날아갈 방향·회전.
        private class Fragment
        {
            public LineRenderer Line;
            public Vector3[] Points;
            public Vector3 Middle;
            public Vector3 Offset;
            public Vector3 SpinAxis;
            public float SpinDegrees;
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            SetUpPlane(targets, camera);
            ringFractions = BuildRingFractions();
            var web = new GlowLines(webLine, transform);

            yield return new WaitForSeconds(windUpSeconds);

            // 바퀴살 끝 위치를 먼저 정해 두고(비틀림 포함), 고리 줄과 조각도 같은 바퀴살 위에 찍는다.
            Vector3[] spokeTips = BuildSpokeTips();
            yield return GrowSpokes(web, spokeTips);
            yield return WindRings(web, spokeTips);
            yield return Charge(web, camera);

            // 펼친 거미줄을 매듭 사이 조각으로 바꿔 끼운 뒤(모양은 같음) 터뜨린다.
            HideAll(web);
            var pieces = new GlowLines(webLine, transform);
            List<Fragment> fragments = BuildFragments(pieces, spokeTips);
            Burst(pieces, spokeTips, targets, onImpact, camera);
            yield return Scatter(pieces, fragments);
            Destroy(gameObject);
        }

        // 거미줄을 놓을 평면을 정한다: 대상들 가운데(조금 위, 카메라 반대쪽으로 조금 뒤)에서 카메라를 마주 본다.
        //  - toCamera: 중심 → 카메라 방향. 수평으로만 써서 거미줄이 바닥에 수직으로 선다
        //  - Vector3.Cross(위, toCamera): 위와 카메라 방향 둘 다에 수직인 방향 = 화면 가로
        private void SetUpPlane(IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets, BattleCameraDirector camera)
        {
            center = GetCenter(targets) + Vector3.up * centerHeight;

            // 조건 ? A : B: 연출가의 카메라가 있으면 그것을, 없으면 Camera.main(MainCamera 태그 카메라)을 쓴다.
            Camera view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            Vector3 toCamera = view != null ? view.transform.position - center : Vector3.back;
            toCamera.y = 0f;
            toCamera = toCamera.sqrMagnitude > 0.001f ? toCamera.normalized : Vector3.back;

            center -= toCamera * pushBehind;
            planeRight = Vector3.Cross(Vector3.up, toCamera).normalized;
            planeUp = Vector3.up;
        }

        // 고리 위치: 가장 안쪽(innerRing)부터 바깥(0.95)까지 고르게 나눈다.
        private float[] BuildRingFractions()
        {
            var fractions = new float[rings];
            for (int ring = 0; ring < rings; ring++)
            {
                fractions[ring] = rings == 1 ? innerRing : Mathf.Lerp(innerRing, 0.95f, ring / (float)(rings - 1));
            }

            return fractions;
        }

        // 바퀴살 끝 위치들. i번째 바퀴살 각도 = 360 ÷ 바퀴살 수 × i + 무작위 비틀림, 길이도 조금씩 다르게.
        private Vector3[] BuildSpokeTips()
        {
            var tips = new Vector3[spokes];
            float step = 360f / spokes;
            for (int i = 0; i < spokes; i++)
            {
                float angle = (step * i + UnityEngine.Random.Range(-spokeAngleJitter, spokeAngleJitter)) * Mathf.Deg2Rad;
                float length = radius * (1f + UnityEngine.Random.Range(-spokeLengthJitter, spokeLengthJitter));

                // 평면 위의 점 = 중심 + (가로 × cos + 세로 × sin) × 길이.
                tips[i] = center + (planeRight * Mathf.Cos(angle) + planeUp * Mathf.Sin(angle)) * length;
            }

            return tips;
        }

        // 1-a) 바퀴살: 중심에서 끝까지 동시에 뻗는다. 1 - (1 - t)²: 처음에 빠르고 끝에서 느려지는 곡선.
        private IEnumerator GrowSpokes(GlowLines web, Vector3[] tips)
        {
            var lines = new LineRenderer[tips.Length];
            for (int i = 0; i < tips.Length; i++)
            {
                lines[i] = web.Create(2);
            }

            web.SetIntensity(idleIntensity);
            yield return Animate(spokeSeconds, t =>
            {
                float eased = 1f - (1f - t) * (1f - t);
                for (int i = 0; i < tips.Length; i++)
                {
                    lines[i].SetPosition(0, center);
                    lines[i].SetPosition(1, Vector3.Lerp(center, tips[i], eased));
                }
            });
        }

        // 1-b) 고리 줄: 안쪽 고리부터 하나씩, 바퀴살을 따라 한 바퀴 감기듯 그린다.
        private IEnumerator WindRings(GlowLines web, Vector3[] tips)
        {
            foreach (float fraction in ringFractions)
            {
                Vector3[] points = BuildRingPoints(tips, fraction);
                LineRenderer line = web.Create(1);
                yield return Animate(ringSeconds, t => GlowLines.DrawPartial(line, points, t));
            }
        }

        // 고리 하나의 점들: 바퀴살 위 점 → 처진 가운데 점 → 다음 바퀴살 위 점 … 처음 점으로 닫는다.
        private Vector3[] BuildRingPoints(Vector3[] tips, float fraction)
        {
            var points = new List<Vector3>();
            for (int i = 0; i < tips.Length; i++)
            {
                Vector3[] arc = BuildArc(tips, i, fraction);
                points.Add(arc[0]);
                points.Add(arc[1]);
            }

            points.Add(points[0]);
            return points.ToArray();
        }

        // i번 바퀴살 → 다음 바퀴살 사이의 고리 한 칸 (점 3개: 시작, 중심 쪽으로 처진 가운데, 끝).
        // %: 마지막 바퀴살 다음은 0번으로 돌아온다.
        private Vector3[] BuildArc(Vector3[] tips, int index, float fraction)
        {
            Vector3 current = Vector3.Lerp(center, tips[index], fraction);
            Vector3 next = Vector3.Lerp(center, tips[(index + 1) % tips.Length], fraction);
            Vector3 middle = Vector3.Lerp((current + next) * 0.5f, center, ringSag);
            return new[] { current, middle, next };
        }

        // 2) 기 모으기: 밝기가 점점 오르고, 점점 빨라지는 맥동마다 잠깐 더 밝게 번쩍 + 약한 떨림.
        private IEnumerator Charge(GlowLines web, BattleCameraDirector camera)
        {
            float elapsed = 0f;
            float nextPulse = 0f;
            float flash = 0f;
            while (elapsed < chargeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / chargeSeconds);
                if (elapsed >= nextPulse)
                {
                    nextPulse = elapsed + Mathf.Lerp(pulseStartInterval, pulseEndInterval, t);
                    flash = 1f;
                    if (camera != null)
                    {
                        camera.Shake(0.03f + 0.05f * t, 0.08f);
                    }
                }

                // 번쩍임은 매 프레임 사그라든다 (Mathf.MoveTowards: 초당 8씩 0을 향해 줄임).
                flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 8f);
                web.SetIntensity(Mathf.Lerp(idleIntensity, chargeIntensity, t) * (1f + flash * 0.5f));
                yield return null;
            }
        }

        // 펼칠 때 쓴 선(바퀴살 통짜·고리 통짜)을 숨긴다. 같은 자리에 조각 선이 대신 들어간다.
        private static void HideAll(GlowLines web)
        {
            foreach (LineRenderer line in web.Lines)
            {
                line.enabled = false;
            }
        }

        // 거미줄을 매듭(바퀴살 × 고리) 사이 조각으로 나눈다.
        //  - 바퀴살 조각: 중심 → 1번 고리 → 2번 고리 … → 끝, 고리 사이마다 한 조각
        //  - 고리 조각: 바퀴살 사이 한 칸마다 한 조각
        // 조각마다 날아갈 거리 = scatterDistance × (중심에서 떨어진 정도 0.3~1) → 바깥 조각일수록 멀리 튄다.
        private List<Fragment> BuildFragments(GlowLines pieces, Vector3[] tips)
        {
            var fragments = new List<Fragment>();
            for (int i = 0; i < tips.Length; i++)
            {
                float previous = 0f;
                foreach (float fraction in ringFractions)
                {
                    AddFragment(fragments, pieces, new[] { Vector3.Lerp(center, tips[i], previous), Vector3.Lerp(center, tips[i], fraction) });
                    previous = fraction;
                }

                AddFragment(fragments, pieces, new[] { Vector3.Lerp(center, tips[i], previous), tips[i] });

                foreach (float fraction in ringFractions)
                {
                    AddFragment(fragments, pieces, BuildArc(tips, i, fraction));
                }
            }

            pieces.SetIntensity(burstIntensity);
            return fragments;
        }

        // 조각 하나를 만들고 날아갈 방향·회전을 정한다.
        // 방향 = 중심 → 조각 가운데 (평면 위) + 카메라 쪽·반대쪽으로 조금씩 흩어짐.
        private void AddFragment(List<Fragment> fragments, GlowLines pieces, Vector3[] points)
        {
            LineRenderer line = pieces.Create(points.Length);
            line.SetPositions(points);

            Vector3 middle = Vector3.zero;
            foreach (Vector3 point in points)
            {
                middle += point;
            }

            middle /= points.Length;
            Vector3 outward = middle - center;
            float reach = Mathf.Clamp(outward.magnitude / radius, 0.3f, 1f);
            Vector3 direction = outward.sqrMagnitude > 0.001f ? outward.normalized : UnityEngine.Random.onUnitSphere;
            Vector3 depth = Vector3.Cross(planeRight, planeUp) * UnityEngine.Random.Range(-0.4f, 0.4f);

            fragments.Add(new Fragment
            {
                Line = line,
                Points = points,
                Middle = middle,
                Offset = (direction + depth) * scatterDistance * reach * UnityEngine.Random.Range(0.7f, 1.2f),
                SpinAxis = UnityEngine.Random.onUnitSphere,
                SpinDegrees = UnityEngine.Random.Range(-scatterSpin, scatterSpin),
            });
        }

        // 3) 터짐: 매듭마다 작은 불꽃, 중심 섬광, 대상 전원 피해(비율 1 = 전부)·불꽃, 흔들림.
        private void Burst(
            GlowLines pieces,
            Vector3[] tips,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            pieces.SetWidthScale(1.8f);

            if (knotBurstPrefab != null)
            {
                foreach (Vector3 tip in tips)
                {
                    foreach (float fraction in ringFractions)
                    {
                        GameObject knot = Instantiate(knotBurstPrefab, Vector3.Lerp(center, tip, fraction), Quaternion.identity);
                        knot.transform.localScale = Vector3.one * knotBurstScale;
                    }
                }
            }

            if (centerBurstPrefab != null)
            {
                GameObject flash = Instantiate(centerBurstPrefab, center, Quaternion.identity);
                flash.transform.localScale = Vector3.one * centerBurstScale;
            }

            foreach ((BattleUnit unit, Vector3 position) in targets)
            {
                if (hitBurstPrefab != null)
                {
                    Instantiate(hitBurstPrefab, position, Quaternion.identity);
                }

                onImpact(unit, 1f);
            }

            if (camera != null)
            {
                camera.Shake(burstShake, burstShakeSeconds);
            }
        }

        // 4) 갈라짐: 조각들이 바깥으로 튀어 나가며(처음 빠르고 점점 느려짐) 돌고 조금 떨어지며, 어두워지고 가늘어지고 투명해진다.
        private IEnumerator Scatter(GlowLines pieces, List<Fragment> fragments)
        {
            yield return Animate(scatterSeconds, t =>
            {
                // 1 - (1 - t)³: 처음에 확 튀었다가 점점 느려지는 곡선.
                float burst = 1f - (1f - t) * (1f - t) * (1f - t);
                foreach (Fragment fragment in fragments)
                {
                    // Quaternion.AngleAxis(각도, 축): 축을 중심으로 그 각도만큼 돌리는 회전. 조각 가운데를 기준으로 돌린다.
                    Quaternion spin = Quaternion.AngleAxis(fragment.SpinDegrees * burst, fragment.SpinAxis);
                    Vector3 move = fragment.Offset * burst + Vector3.down * scatterDrop * t * t;
                    for (int i = 0; i < fragment.Points.Length; i++)
                    {
                        Vector3 local = fragment.Points[i] - fragment.Middle;
                        fragment.Line.SetPosition(i, fragment.Middle + move + spin * local);
                    }
                }

                pieces.SetIntensity(Mathf.Lerp(burstIntensity, idleIntensity, t));
                pieces.SetWidthScale(Mathf.Lerp(1.8f, 0.15f, t));
                pieces.SetAlpha(1f - t * t);
            });
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

        // 대상들 화면 위치의 가운데. 대상이 없으면 연출 오브젝트 자리.
        private Vector3 GetCenter(IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets)
        {
            if (targets.Count == 0)
            {
                return transform.position;
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
