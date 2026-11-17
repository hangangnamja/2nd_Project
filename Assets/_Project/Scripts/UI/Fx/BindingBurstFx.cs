using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 실 새장 연출 (지속피해 격발 딜러 3스, 임시). 레퍼런스: 하늘 한 점에서 내려온 보랏빛 실들이 대상 둘레 바닥에 꽂혀 새장처럼 가두고,
    // 조여 든 뒤 번개가 내리쳐 터뜨린다.
    //  1) (선택) 대상 가까이로 카메라 컷
    //  2) 실 꽂기: 대상 머리 위 높은 꼭짓점에서 실이 하나씩 빠르게 내려와 둘레 바닥에 꽂힌다 (꽂힐 때마다 작은 불꽃·떨림)
    //  3) 조이기: 실이 꽂힌 자리가 대상 쪽으로 좁혀지며 새장이 오므라들고, 실이 팽팽하게 떨리며 맥동이 빨라진다 → 잠깐 숨을 죽인다
    //  4) 번개: 하늘에서 대상에게 번개가 내리치고 실들도 번개처럼 지직거림 + 폭발 → 대상 피해(onImpact 비율 1)·강한 흔들림
    //  5) 실과 번개가 사라지고 원래 시점으로
    // 격발로 터지는 지속피해 숫자는 이 피해 뒤에 BattleFxView가 따로 보여준다.
    // 선 모양은 프리팹의 견본 선, 불꽃·폭발은 파티클 프리팹이 담당하고, 이 코드는 점 위치·순서·시간·카메라 컷만 정한다.
    public class BindingBurstFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("새장 실 견본 (꺼 둔 자식 LineRenderer). 재질·색·굵기를 여기서 정한다")]
        [SerializeField] private LineRenderer threadLine;

        [Tooltip("내리치는 번개 견본 (꺼 둔 자식 LineRenderer)")]
        [SerializeField] private LineRenderer lightningLine;

        [Tooltip("실이 바닥에 꽂힐 때 튀는 불꽃 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject stabSparkPrefab;

        [Tooltip("번개가 내리칠 때 대상 자리에서 터지는 폭발 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject burstPrefab;

        [Header("새장")]
        [Tooltip("대상 하나를 가두는 실 수")]
        [SerializeField, Range(4, 32)] private int threadCount = 14;

        [Tooltip("실이 꽂히는 둘레 반지름 (바닥)")]
        [SerializeField, Min(0.2f)] private float cageRadius = 2.2f;

        [Tooltip("실이 모이는 꼭짓점 높이 (바닥 위)")]
        [SerializeField, Min(1f)] private float apexHeight = 11f;

        [Tooltip("실이 꽂힐 자리를 둘레에서 앞뒤로 흩는 정도 (반지름 대비, 0이면 정확한 원)")]
        [SerializeField, Range(0f, 0.4f)] private float radiusJitter = 0.12f;

        [Header("실 꽂기")]
        [Tooltip("실 하나가 꼭짓점에서 바닥까지 내려오는 시간(초)")]
        [SerializeField, Min(0.01f)] private float dropSeconds = 0.14f;

        [Tooltip("실 사이 간격(초)")]
        [SerializeField, Min(0f)] private float dropInterval = 0.035f;

        [SerializeField, Min(0f)] private float stabShake = 0.05f;
        [SerializeField, Min(0.1f)] private float stabSparkScale = 0.45f;

        [Header("조이기")]
        [Tooltip("새장이 오므라드는 시간(초)")]
        [SerializeField, Min(0.01f)] private float tightenSeconds = 0.7f;

        [Tooltip("다 조인 뒤 바닥 반지름 배율")]
        [SerializeField, Range(0.1f, 1f)] private float tightenScale = 0.4f;

        [Tooltip("팽팽해진 실이 떨리는 폭 (조일수록 커짐)")]
        [SerializeField, Min(0f)] private float trembleAmount = 0.06f;

        [Tooltip("밝기: 평소 → 조인 끝 (재질 색에 곱함)")]
        [SerializeField] private float idleIntensity = 1f;
        [SerializeField] private float tightIntensity = 2.4f;

        [Tooltip("맥동 간격: 처음 → 끝 (점점 빨라짐)")]
        [SerializeField, Min(0.03f)] private float pulseStartInterval = 0.25f;
        [SerializeField, Min(0.03f)] private float pulseEndInterval = 0.07f;
        [SerializeField, Min(0f)] private float pulseShake = 0.05f;

        [Tooltip("번개 직전 숨을 죽이는 시간(초): 실이 어두워진다")]
        [SerializeField, Min(0f)] private float hushSeconds = 0.15f;

        [Header("번개")]
        [Tooltip("번개가 시작되는 높이 (꼭짓점 위)")]
        [SerializeField, Min(0f)] private float boltAboveApex = 4f;

        [Tooltip("번개 꺾임 수 · 꺾이는 폭")]
        [SerializeField, Range(3, 40)] private int boltSegments = 16;
        [SerializeField, Min(0f)] private float boltAmplitude = 0.8f;

        [Tooltip("실이 번개처럼 지직거리는 꺾임 수 · 폭")]
        [SerializeField, Range(2, 30)] private int threadSegments = 10;
        [SerializeField, Min(0f)] private float threadAmplitude = 0.25f;

        [Tooltip("번개가 깜빡이며 모양을 바꾸는 간격(초) · 번개가 남아 있는 시간(초)")]
        [SerializeField, Min(0.01f)] private float flickerInterval = 0.04f;
        [SerializeField, Min(0.01f)] private float lightningSeconds = 0.3f;

        [SerializeField] private float burstIntensity = 5f;
        [SerializeField, Min(0.1f)] private float burstScale = 1.8f;
        [SerializeField, Min(0f)] private float burstShake = 0.6f;
        [SerializeField, Min(0f)] private float burstShakeSeconds = 0.5f;

        [Tooltip("번개 뒤 실·번개가 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.3f;

        [Tooltip("사라진 뒤 원래 시점으로 돌아가기까지(초)")]
        [SerializeField, Min(0f)] private float afterSeconds = 0.2f;

        [Header("카메라")]
        [Tooltip("켜면 대상 가까이로 컷해서 보여준다")]
        [SerializeField] private bool closeUp = true;

        [Tooltip("대상에서 카메라까지 수평 거리")]
        [SerializeField, Min(1f)] private float closeUpDistance = 8f;

        [Tooltip("대상 → 시전자 방향에서 옆으로 돌린 각도 (0 = 시전자 쪽 정면)")]
        [SerializeField] private float closeUpAngle = 35f;

        [Tooltip("카메라 높이 (캡슐 중심 기준)")]
        [SerializeField] private float closeUpHeight = 0f;

        [Tooltip("바라보는 높이 (캡슐 중심 기준, 위에서 내려오는 실이 담기게 조금 위)")]
        [SerializeField] private float closeUpLookHeight = 1.8f;

        [SerializeField] private float closeUpFieldOfView = 58f;

        [Header("공통")]
        [Tooltip("시전자가 기를 모으는 시간(초)")]
        [SerializeField, Min(0f)] private float windUpSeconds = 0.2f;

        [Tooltip("캡슐 중심에서 바닥까지의 높이 (캡슐 원점이 몸 가운데라 -1이 발밑)")]
        [SerializeField] private float groundOffset = -1f;

        // 대상 하나를 가두는 새장: 대상, 몸·바닥·꼭짓점 위치, 실마다 꽂힐 방향(수평 단위 벡터 × 반지름 배율)과 실 선.
        private class Cage
        {
            public BattleUnit Unit;
            public Vector3 Body;
            public Vector3 Ground;
            public Vector3 Apex;
            public Vector3[] Spots;
            public LineRenderer[] Threads;
        }

        // 연출 컷을 건 카메라. 연출이 중간에 사라져도 카메라가 컷 상태로 남지 않게 OnDestroy에서 되돌린다.
        private BattleCameraDirector cinematicCamera;

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
            var threads = new GlowLines(threadLine, transform);
            var lightning = new GlowLines(lightningLine, transform);
            List<Cage> cages = BuildCages(targets, threads);
            threads.SetIntensity(idleIntensity);

            if (closeUp && camera != null && cages.Count > 0)
            {
                BeginCloseUp(caster, cages[0].Body, camera);
            }

            yield return new WaitForSeconds(windUpSeconds);
            yield return DropThreads(cages, camera);
            yield return Tighten(threads, cages, camera);
            yield return Hush(threads);
            yield return Strike(threads, lightning, cages, onImpact, camera);
            yield return FadeOut(threads, lightning);
            yield return new WaitForSeconds(afterSeconds);

            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 대상마다 새장을 만든다. 실 i가 꽂힐 자리 = 360 ÷ 실 수 × i 방향(대상마다 다른 시작 각도) × (1 ± 흩음).
        // 실 선은 미리 만들되 꺼 두고, 내려올 차례에 켠다.
        private List<Cage> BuildCages(IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets, GlowLines threads)
        {
            var cages = new List<Cage>();
            foreach ((BattleUnit unit, Vector3 position) in targets)
            {
                Vector3 ground = position + Vector3.up * groundOffset;
                var cage = new Cage
                {
                    Unit = unit,
                    Body = position,
                    Ground = ground,
                    Apex = ground + Vector3.up * apexHeight,
                    Spots = new Vector3[threadCount],
                    Threads = new LineRenderer[threadCount],
                };

                float start = UnityEngine.Random.Range(0f, 360f);
                for (int i = 0; i < threadCount; i++)
                {
                    // Quaternion.Euler(0, 각도, 0) * 앞: 앞 방향을 y축(위)으로 그 각도만큼 돌린 수평 방향.
                    Vector3 direction = Quaternion.Euler(0f, start + 360f / threadCount * i, 0f) * Vector3.forward;
                    cage.Spots[i] = direction * (1f + UnityEngine.Random.Range(-radiusJitter, radiusJitter));
                    cage.Threads[i] = threads.Create(2);
                    cage.Threads[i].enabled = false;
                }

                cages.Add(cage);
            }

            return cages;
        }

        // 대상 가까이로 컷: 대상 → 시전자 방향을 closeUpAngle만큼 옆으로 돌린 쪽에서 대상을 바라본다.
        private void BeginCloseUp(Vector3 caster, Vector3 body, BattleCameraDirector camera)
        {
            Vector3 toCaster = caster - body;
            toCaster.y = 0f;
            toCaster = toCaster.sqrMagnitude > 0.001f ? toCaster.normalized : Vector3.back;
            Vector3 direction = Quaternion.Euler(0f, closeUpAngle, 0f) * toCaster;

            Vector3 position = body + direction * closeUpDistance + Vector3.up * closeUpHeight;
            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = body + Vector3.up * closeUpLookHeight;

            camera.BeginCinematic(position, lookPoint, closeUpFieldOfView);
            cinematicCamera = camera;
        }

        // 2) 실을 무작위 순서로 하나씩 내린다. 대상이 여럿이면 같은 차례의 실이 대상마다 동시에 내려온다.
        private IEnumerator DropThreads(List<Cage> cages, BattleCameraDirector camera)
        {
            int[] order = ShuffledOrder(threadCount);
            for (int step = 0; step < order.Length; step++)
            {
                foreach (Cage cage in cages)
                {
                    StartCoroutine(DropThread(cage, order[step], camera, step % 2 == 0));
                }

                yield return new WaitForSeconds(dropInterval);
            }

            // 마지막 실이 꽂힐 때까지 기다린다.
            yield return new WaitForSeconds(dropSeconds);
        }

        // 실 하나: 위쪽 끝은 꼭짓점에 붙어 있고, 아래쪽 끝이 t²(가속)로 바닥까지 내리꽂힌다 → 불꽃·(shake면) 떨림.
        private IEnumerator DropThread(Cage cage, int index, BattleCameraDirector camera, bool shake)
        {
            LineRenderer thread = cage.Threads[index];
            thread.enabled = true;
            Vector3 spot = SpotAt(cage, index, 1f);
            yield return Animate(dropSeconds, t =>
            {
                thread.SetPosition(0, cage.Apex);
                thread.SetPosition(1, Vector3.Lerp(cage.Apex, spot, t * t));
            });

            if (stabSparkPrefab != null)
            {
                GameObject spark = Instantiate(stabSparkPrefab, spot, Quaternion.identity);
                spark.transform.localScale = Vector3.one * stabSparkScale;
            }

            // 실이 많아 모든 실이 흔들면 화면이 계속 떨리므로 하나 걸러 흔든다.
            if (shake && camera != null)
            {
                camera.Shake(stabShake, 0.1f);
            }
        }

        // 3) 반지름 배율 1 → tightenScale로 오므리며 밝아진다. 실은 바닥 끝이 조금씩 떨려 팽팽한 느낌, 맥동은 점점 빨라짐.
        private IEnumerator Tighten(GlowLines threads, List<Cage> cages, BattleCameraDirector camera)
        {
            float elapsed = 0f;
            float nextPulse = 0f;
            float flash = 0f;
            while (elapsed < tightenSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / tightenSeconds);

                // Mathf.SmoothStep: 0~1을 처음·끝이 부드러운 S자 곡선으로 바꾼다.
                float scale = Mathf.Lerp(1f, tightenScale, Mathf.SmoothStep(0f, 1f, t));
                foreach (Cage cage in cages)
                {
                    DrawStraight(cage, scale, trembleAmount * t);
                }

                if (elapsed >= nextPulse)
                {
                    nextPulse = elapsed + Mathf.Lerp(pulseStartInterval, pulseEndInterval, t);
                    flash = 1f;
                    if (camera != null)
                    {
                        camera.Shake(pulseShake * (0.5f + t), 0.1f);
                    }
                }

                // 맥동 번쩍임은 매 프레임 조금씩 사그라든다 (Mathf.MoveTowards: 초당 8씩 0을 향해 줄임).
                flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 8f);
                threads.SetIntensity(Mathf.Lerp(idleIntensity, tightIntensity, t) * (1f + flash * 0.6f));
                yield return null;
            }
        }

        // 번개 직전: 실이 확 어두워져 다음 번쩍임이 더 세게 보이게 한다.
        private IEnumerator Hush(GlowLines threads)
        {
            yield return Animate(hushSeconds, t => threads.SetIntensity(Mathf.Lerp(tightIntensity, idleIntensity * 0.5f, t)));
        }

        // 4) 번개: 하늘에서 대상에게 번개 + 실이 번개처럼 지직거림 + 폭발 + 피해. 번개는 flickerInterval마다 모양을 바꾸며 깜빡인다.
        private IEnumerator Strike(
            GlowLines threads,
            GlowLines lightning,
            List<Cage> cages,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            var bolts = new LineRenderer[cages.Count];
            for (int i = 0; i < cages.Count; i++)
            {
                bolts[i] = lightning.Create(2);
            }

            threads.SetIntensity(burstIntensity);
            threads.SetWidthScale(1.6f);
            lightning.SetIntensity(burstIntensity);

            foreach (Cage cage in cages)
            {
                if (burstPrefab != null)
                {
                    GameObject burst = Instantiate(burstPrefab, cage.Body, Quaternion.identity);
                    burst.transform.localScale = Vector3.one * burstScale;
                }

                onImpact(cage.Unit, 1f);
            }

            if (camera != null)
            {
                camera.Shake(burstShake, burstShakeSeconds);
            }

            float elapsed = 0f;
            while (elapsed < lightningSeconds)
            {
                for (int i = 0; i < cages.Count; i++)
                {
                    DrawLightning(cages[i], bolts[i]);
                }

                // 깜빡임: 번개 밝기를 매번 조금씩 다르게 (0.6~1.2배).
                lightning.SetIntensity(burstIntensity * UnityEngine.Random.Range(0.6f, 1.2f));
                yield return new WaitForSeconds(flickerInterval);
                elapsed += flickerInterval;
            }
        }

        // 번개 한 번의 모양: 하늘(꼭짓점 위) → 대상 몸 큰 번개, 실 하나하나는 꼭짓점 → 꽂힌 자리 작은 지직거림.
        private void DrawLightning(Cage cage, LineRenderer bolt)
        {
            GlowLines.SetZigzag(bolt, cage.Apex + Vector3.up * boltAboveApex, cage.Body, boltSegments, boltAmplitude);
            for (int i = 0; i < threadCount; i++)
            {
                GlowLines.SetZigzag(cage.Threads[i], cage.Apex, SpotAt(cage, i, tightenScale), threadSegments, threadAmplitude);
            }
        }

        // 5) 실과 번개가 어두워지며 가늘어지고 투명해진다.
        private IEnumerator FadeOut(GlowLines threads, GlowLines lightning)
        {
            yield return Animate(fadeSeconds, t =>
            {
                threads.SetIntensity(Mathf.Lerp(burstIntensity, idleIntensity, t));
                threads.SetWidthScale(Mathf.Lerp(1.6f, 0f, t));
                threads.SetAlpha(1f - t);
                lightning.SetWidthScale(Mathf.Lerp(1f, 0f, t));
                lightning.SetAlpha(1f - t);
            });
        }

        // 실을 곧은 선(꼭짓점 → 꽂힌 자리)으로 다시 그린다. tremble: 꽂힌 자리를 무작위로 흔드는 폭.
        private void DrawStraight(Cage cage, float scale, float tremble)
        {
            for (int i = 0; i < threadCount; i++)
            {
                Vector3 shake = UnityEngine.Random.insideUnitSphere * tremble;
                shake.y = 0f;
                cage.Threads[i].SetPosition(0, cage.Apex);
                cage.Threads[i].SetPosition(1, SpotAt(cage, i, scale) + shake);
            }
        }

        // 실 i가 꽂힌 바닥 자리 (반지름 배율 scale 적용).
        private Vector3 SpotAt(Cage cage, int index, float scale)
        {
            return cage.Ground + cage.Spots[index] * cageRadius * scale;
        }

        // 0 ~ count-1을 무작위 순서로 섞은 배열 (피셔-예이츠 섞기: 뒤에서부터 앞쪽 무작위 칸과 맞바꾼다).
        private static int[] ShuffledOrder(int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
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
