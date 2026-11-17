using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.UI
{
    // 땅 가르기 연출 (하이퍼캐리 변신 2스 반격 태세의 반격, 임시). 레퍼런스: 내리친 일격에 땅이 바위 조각으로 쪼개지고 틈마다 용암이 비친다.
    //  1) 위에서 내려다보는 시점으로 컷 + 시전자가 적 무리 쪽으로 튀어 나가며 내리친다
    //  2) 시전자 앞에서 적 무리 너머까지 용암빛 균열이 땅을 타고 빠르게 뻗어 간다
    //     균열이 지나간 띠 안의 땅이 크고 작은 바위 조각으로 쪼개져 솟아오르며 제각각 기울고(일부는 파편으로 튀어 오름),
    //     조각 사이로 갈래 균열(용암 틈)이 퍼진다
    //  3) 균열 머리가 적 발밑에 닿는 순간 그 적 아래가 터지며 피해(onImpact 비율 1)·잔균열·흔들림
    //  4) 용암빛이 잠시 남았다가 식고, 바위 조각이 다시 가라앉은 뒤 원래 시점으로
    // 균열 모양(재질·색·굵기)은 프리팹의 견본 선(바닥에 눕힌 선), 바위 조각은 견본 큐브(바위 재질), 불꽃·폭발은 파티클 프리팹이 담당하고,
    // 이 코드는 위치·기울기·순서·시간·카메라 컷만 정한다.
    public class GroundSplitFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("균열 견본 (꺼 둔 자식 LineRenderer). 바닥에 눕도록 Alignment = Transform Z, 오브젝트를 x축 90도로 눕혀 둔다")]
        [SerializeField] private LineRenderer crackLine;

        [Tooltip("균열이 지나간 자리에서 튀는 불씨 (끝나면 스스로 지워지는 프리팹, 비우면 생략)")]
        [SerializeField] private GameObject trailBurstPrefab;

        [Tooltip("적 발밑에서 터지는 폭발 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject eruptionPrefab;

        // FormerlySerializedAs: 예전 이름(plateTemplate)으로 저장된 프리팹 값을 새 이름으로 이어 받는다.
        [Tooltip("쪼개져 솟는 바위 조각 견본 (꺼 둔 자식 큐브, 바위 재질). 크기는 코드가 정한다")]
        [FormerlySerializedAs("plateTemplate")]
        [SerializeField] private Transform slabTemplate;

        [Header("바위 조각")]
        [Tooltip("균열 한 칸마다 쪼개지는 조각 수 · 균열 양옆으로 조각이 깔리는 너비(한쪽)")]
        [SerializeField, Range(0, 12)] private int slabsPerSegment = 4;
        [SerializeField, Min(0.1f)] private float bandWidth = 2.6f;

        [Tooltip("조각 크기(가로·세로) 최소 · 최대, 두께")]
        [SerializeField, Min(0.05f)] private float slabMinSize = 0.5f;
        [SerializeField, Min(0.05f)] private float slabMaxSize = 1.3f;
        [SerializeField, Min(0.02f)] private float slabThickness = 0.35f;

        [Tooltip("조각이 기우는 최대 각도 · 솟는 높이 · 균열에서 밀려나는 거리 (균열에 가까울수록 크게)")]
        [SerializeField, Range(0f, 80f)] private float slabTilt = 35f;
        [SerializeField, Min(0f)] private float slabRise = 0.3f;
        [SerializeField, Min(0f)] private float slabPush = 0.35f;

        [Tooltip("조각이 솟는 시간(초)")]
        [SerializeField, Min(0.01f)] private float slabHeaveSeconds = 0.18f;

        [Tooltip("균열 한 칸마다 튀어 오르는 파편 수 · 튀는 속도(최소·최대) · 크기 · 중력")]
        [SerializeField, Range(0, 8)] private int debrisPerSegment = 2;
        [SerializeField] private Vector2 debrisSpeed = new Vector2(3f, 7f);
        [SerializeField] private Vector2 debrisSize = new Vector2(0.15f, 0.4f);
        [SerializeField, Min(0f)] private float debrisGravity = 18f;

        [Header("균열 모양")]
        [Tooltip("균열이 시작되는 거리 (시전자 앞)")]
        [SerializeField, Min(0f)] private float startAhead = 1f;

        [Tooltip("균열이 적 무리 가운데를 지나 더 뻗는 거리")]
        [SerializeField, Min(0f)] private float overshoot = 3f;

        [Tooltip("균열 꺾임 수 · 좌우로 꺾이는 폭")]
        [SerializeField, Range(4, 60)] private int segments = 22;
        [SerializeField, Min(0f)] private float zigzag = 0.45f;

        [Tooltip("균열 한 칸마다 조각 사이로 뻗는 갈래 균열(용암 틈) 수 · 길이")]
        [SerializeField, Range(0, 6)] private int lavaBranchesPerSegment = 1;
        [SerializeField, Min(0f)] private float lavaBranchLength = 2f;

        [Tooltip("다 벌어진 본 균열 굵기 배율 (견본 굵기 대비)")]
        [SerializeField, Min(1f)] private float crackOpenWidth = 2.5f;

        [Tooltip("적 발밑에서 사방으로 갈라지는 잔균열 수 · 길이")]
        [SerializeField, Range(0, 8)] private int branchCount = 5;
        [SerializeField, Min(0f)] private float branchLength = 1.8f;

        [Tooltip("균열을 바닥보다 살짝 띄우는 높이 (바닥에 묻혀 깜빡이지 않게)")]
        [SerializeField, Min(0f)] private float lift = 0.03f;

        [Header("시간")]
        [Tooltip("내리친 뒤 균열이 출발하기까지(초)")]
        [SerializeField, Min(0f)] private float windUpSeconds = 0.14f;

        [Tooltip("균열 머리가 끝까지 뻗는 시간(초)")]
        [SerializeField, Min(0.01f)] private float runSeconds = 0.4f;

        [Tooltip("지나간 자리 불씨 간격 (균열 전체 대비, 0.2면 5곳)")]
        [SerializeField, Range(0.02f, 1f)] private float trailBurstSpacing = 0.2f;

        [Tooltip("다 뻗은 뒤 용암빛이 남는 시간(초) · 식어서 사라지는 시간(초)")]
        [SerializeField, Min(0f)] private float glowSeconds = 0.45f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.5f;

        [Header("세기")]
        [Tooltip("용암빛 밝기 (너무 높으면 Bloom으로 하얗게 번진다)")]
        [SerializeField] private float intensity = 1.3f;
        [SerializeField] private float eruptIntensity = 1.9f;
        [SerializeField, Min(0.05f)] private float trailBurstScale = 0.2f;
        [SerializeField, Min(0.1f)] private float eruptionScale = 0.6f;
        [SerializeField, Min(0f)] private float slamShake = 0.2f;
        [SerializeField, Min(0f)] private float eruptShake = 0.45f;

        [Tooltip("캡슐 중심에서 바닥까지의 높이 (캡슐 원점이 몸 가운데라 -1이 발밑)")]
        [SerializeField] private float groundOffset = -1f;

        [Header("카메라")]
        [Tooltip("켜면 위에서 내려다보는 시점으로 컷한다")]
        [SerializeField] private bool topView = true;

        [Tooltip("바닥에서 카메라까지 높이 · 시전자 쪽으로 물러나는 거리 (0이면 바로 위에서 수직으로)")]
        [SerializeField, Min(1f)] private float viewHeight = 13f;
        [SerializeField] private float viewBack = 5f;

        [Tooltip("바라보는 곳: 시전자(0) ~ 적 무리 가운데(1) 사이 비율")]
        [SerializeField, Range(0f, 1f)] private float viewFocus = 0.6f;

        [SerializeField] private float viewFieldOfView = 55f;

        // 솟는 바위 조각 하나: 오브젝트, 바닥 자리, 밀려날 방향, 기울기 회전(다 솟았을 때), 크기, 벌어진 정도(0~1).
        private class Slab
        {
            public Transform Body;
            public Vector3 Rest;
            public Vector3 Push;
            public Quaternion Yaw;
            public Quaternion Tilt;
            public float Open;
        }

        // 튀어 오르는 파편 하나: 오브젝트, 속도, 회전축·속도.
        private class Debris
        {
            public Transform Body;
            public Vector3 Velocity;
            public Vector3 SpinAxis;
            public float SpinSpeed;
        }

        private readonly List<Slab> slabs = new List<Slab>();
        private readonly List<Debris> debris = new List<Debris>();

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
            Vector3 center = GetCenter(targets);
            BeginTopView(caster, center, camera);

            // 시전 모션 대신 적 무리 쪽으로 튀어 나가 내리친다.
            if (CasterView != null)
            {
                CasterView.PlayLunge(center);
            }

            yield return new WaitForSeconds(windUpSeconds);
            if (camera != null)
            {
                camera.Shake(slamShake, 0.2f);
            }

            var cracks = new GlowLines(crackLine, transform);
            cracks.SetIntensity(intensity);
            Vector3[] path = BuildPath(caster, center);
            yield return Run(cracks, path, targets, onImpact, camera);
            yield return Linger(glowSeconds);
            yield return FadeOut(cracks);

            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 위 시점으로 컷: 시전자 ~ 적 무리 사이 viewFocus 지점의 바닥을, 위(viewHeight)에서 시전자 쪽으로 조금 물러나(viewBack) 내려다본다.
        private void BeginTopView(Vector3 caster, Vector3 center, BattleCameraDirector camera)
        {
            if (!topView || camera == null)
            {
                return;
            }

            Vector3 focus = Ground(Vector3.Lerp(caster, center, viewFocus));
            Vector3 back = caster - center;
            back.y = 0f;
            back = back.sqrMagnitude > 0.001f ? back.normalized : Vector3.back;

            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = focus;
            camera.BeginCinematic(focus + Vector3.up * viewHeight + back * viewBack, lookPoint, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 균열 경로: 시전자 발밑 앞 → 적 무리 가운데 → 그 너머까지 직선을 segments칸으로 나누고,
        // 가운데 점들을 좌우(진행 방향에 수직, 수평)로 무작위로 비튼다 → 들쭉날쭉한 균열.
        private Vector3[] BuildPath(Vector3 caster, Vector3 center)
        {
            Vector3 from = Ground(caster);
            Vector3 to = Ground(center);
            Vector3 direction = to - from;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;

            // Vector3.Cross(위, 진행 방향): 진행 방향의 수평 옆 방향.
            Vector3 sideways = Vector3.Cross(Vector3.up, direction);
            Vector3 start = from + direction * startAhead;
            Vector3 end = to + direction * overshoot;

            var path = new Vector3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float jitter = i == 0 || i == segments ? 0f : UnityEngine.Random.Range(-zigzag, zigzag);
                path[i] = Vector3.Lerp(start, end, i / (float)segments) + sideways * jitter;
            }

            return path;
        }

        // 균열 머리가 0 → 1로 뻗는다 (t² 가속: 처음엔 갈라지듯 느리고 점점 빨라짐).
        // 머리가 지나간 칸마다 땅이 쪼개지고(조각·파편·갈래 균열), 대상 발밑(경로 위 가장 가까운 지점)을 지나면 그 대상이 터진다.
        private IEnumerator Run(
            GlowLines cracks,
            Vector3[] path,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            LineRenderer main = cracks.Create(1);
            var pending = new List<(BattleUnit Unit, Vector3 Position, float At)>();
            foreach ((BattleUnit unit, Vector3 position) in targets)
            {
                pending.Add((unit, position, ClosestProgress(path, Ground(position))));
            }

            float nextBurst = 0f;
            int nextSegment = 0;
            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / runSeconds);
                float head = t * t;
                GlowLines.DrawPartial(main, path, head);

                // 균열이 벌어진다: 굵기 배율 1 → crackOpenWidth (머리가 갈수록 틈이 넓어짐).
                cracks.SetWidthScale(Mathf.Lerp(1f, crackOpenWidth, head));

                // 머리가 지나간 칸마다 땅을 쪼갠다. 칸 i는 경로 점 i ~ i+1 사이 (진행도 (i+1) ÷ 칸 수에서 끝남).
                while (nextSegment < path.Length - 1 && head >= (nextSegment + 1) / (float)(path.Length - 1))
                {
                    ShatterSegment(cracks, path[nextSegment], path[nextSegment + 1]);
                    nextSegment++;
                }

                UpdateRubble(Time.deltaTime);

                // 지나간 자리 불씨: 머리가 nextBurst를 넘을 때마다 하나씩.
                while (trailBurstPrefab != null && head >= nextBurst && nextBurst <= 1f)
                {
                    SpawnScaled(trailBurstPrefab, PointOnPath(path, nextBurst), trailBurstScale);
                    nextBurst += trailBurstSpacing;
                }

                // 머리가 지나간 대상을 터뜨린다. 뒤에서부터 지운다 (앞에서 지우면 번호가 밀림).
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (head >= pending[i].At)
                    {
                        Erupt(cracks, pending[i].Unit, pending[i].Position, onImpact, camera);
                        pending.RemoveAt(i);
                    }
                }

                if (t >= 1f)
                {
                    break;
                }

                yield return null;
            }
        }

        // seconds 동안 조각·파편만 마저 움직이며 기다린다 (용암빛이 남아 있는 시간).
        private IEnumerator Linger(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                UpdateRubble(Time.deltaTime);
                yield return null;
            }
        }

        // 균열 한 칸(from → to)을 쪼갠다: 양옆 띠 안에 바위 조각·파편을 만들고, 조각 사이로 갈래 균열을 낸다.
        private void ShatterSegment(GlowLines cracks, Vector3 from, Vector3 to)
        {
            Vector3 along = to - from;
            along.y = 0f;
            if (along.sqrMagnitude < 0.0001f)
            {
                return;
            }

            along.Normalize();
            Vector3 sideways = Vector3.Cross(Vector3.up, along);

            if (slabTemplate != null)
            {
                for (int i = 0; i < slabsPerSegment; i++)
                {
                    SpawnSlab(Vector3.Lerp(from, to, UnityEngine.Random.value), sideways);
                }

                for (int i = 0; i < debrisPerSegment; i++)
                {
                    SpawnDebris(Vector3.Lerp(from, to, UnityEngine.Random.value), sideways);
                }
            }

            // 갈래 균열: 균열 위 무작위 지점에서 옆쪽(앞뒤로 조금 비틀어)으로 뻗는 용암 틈.
            for (int i = 0; i < lavaBranchesPerSegment; i++)
            {
                Vector3 start = Vector3.Lerp(from, to, UnityEngine.Random.value);
                float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Vector3 direction = (sideways * sign + along * UnityEngine.Random.Range(-0.7f, 0.7f)).normalized;
                LineRenderer branch = cracks.Create(2);
                GlowLines.SetZigzag(branch, start, start + direction * lavaBranchLength * UnityEngine.Random.Range(0.5f, 1.2f), 6, 0.25f);
                FlattenToGround(branch, start.y);
            }
        }

        // 바위 조각 하나: 균열 옆 띠 안 무작위 자리, 무작위 크기·방향. 처음에는 바닥 아래에 숨어 있다가 솟는다.
        // 균열에 가까울수록(가운데) 더 크게 기울고 더 멀리 밀려난다 → 균열을 따라 땅이 벌어지는 모양.
        private void SpawnSlab(Vector3 onCrack, Vector3 sideways)
        {
            float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            float offset = UnityEngine.Random.Range(0.2f, bandWidth);
            float closeness = 1f - offset / bandWidth;

            Transform body = Instantiate(slabTemplate, transform);
            body.gameObject.SetActive(true);
            body.localScale = new Vector3(
                UnityEngine.Random.Range(slabMinSize, slabMaxSize),
                slabThickness,
                UnityEngine.Random.Range(slabMinSize, slabMaxSize));

            // 기울기: 균열 방향 축으로 바깥쪽이 내려가게(균열 쪽 끝이 들리게) + 무작위 축으로 조금 더.
            // Quaternion.AngleAxis(각도, 축): 축을 중심으로 각도만큼 돌리는 회전. 곱하면 두 회전을 이어 붙인다.
            Vector3 along = Vector3.Cross(sideways, Vector3.up);
            Quaternion tilt =
                Quaternion.AngleAxis(-sign * slabTilt * Mathf.Lerp(0.3f, 1f, closeness), along) *
                Quaternion.AngleAxis(UnityEngine.Random.Range(-10f, 10f), UnityEngine.Random.onUnitSphere);

            var slab = new Slab
            {
                Body = body,
                Rest = onCrack + sideways * sign * offset,
                Push = sideways * sign * slabPush * Mathf.Lerp(0.3f, 1f, closeness) + Vector3.up * slabRise * UnityEngine.Random.Range(0.4f, 1f),
                Yaw = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f),
                Tilt = tilt,
                Open = 0f,
            };
            PoseSlab(slab, 0f);
            slabs.Add(slab);
        }

        // 파편 하나: 균열 위에서 위·옆으로 튀어 오르며 돈다 (중력으로 떨어져 바닥 밑으로 사라짐).
        private void SpawnDebris(Vector3 onCrack, Vector3 sideways)
        {
            Transform body = Instantiate(slabTemplate, transform);
            body.gameObject.SetActive(true);
            body.position = onCrack;
            body.localScale = Vector3.one * UnityEngine.Random.Range(debrisSize.x, debrisSize.y);
            body.rotation = UnityEngine.Random.rotation;

            float sign = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            debris.Add(new Debris
            {
                Body = body,
                Velocity = Vector3.up * UnityEngine.Random.Range(debrisSpeed.x, debrisSpeed.y) + sideways * sign * UnityEngine.Random.Range(0.5f, 2f),
                SpinAxis = UnityEngine.Random.onUnitSphere,
                SpinSpeed = UnityEngine.Random.Range(200f, 600f),
            });
        }

        // 매 프레임: 조각은 벌어진 정도를 올려 자세를 다시 잡고, 파편은 속도·중력으로 움직인다.
        private void UpdateRubble(float deltaTime)
        {
            foreach (Slab slab in slabs)
            {
                slab.Open = Mathf.Clamp01(slab.Open + deltaTime / slabHeaveSeconds);

                // 1 - (1 - t)²: 툭 솟았다가 멈추는 감속 곡선.
                PoseSlab(slab, 1f - (1f - slab.Open) * (1f - slab.Open));
            }

            foreach (Debris piece in debris)
            {
                if (piece.Body == null || !piece.Body.gameObject.activeSelf)
                {
                    continue;
                }

                // 속도에 중력(아래 방향 가속)을 더하고, 속도만큼 옮긴다. 바닥보다 한참 아래로 떨어지면 숨긴다.
                piece.Velocity += Vector3.down * debrisGravity * deltaTime;
                piece.Body.position += piece.Velocity * deltaTime;
                piece.Body.Rotate(piece.SpinAxis, piece.SpinSpeed * deltaTime, Space.World);
                if (piece.Velocity.y < 0f && piece.Body.position.y < transform.position.y - 5f)
                {
                    piece.Body.gameObject.SetActive(false);
                }
            }
        }

        // 조각 자세: open 0 = 바닥 아래(두께만큼 가라앉아 윗면이 바닥 밑 → 보이지 않음), 1 = 솟아 기울고 밀려난 자리.
        // Quaternion.Slerp(평평, 기울기, open): 두 회전 사이를 open만큼 부드럽게 섞는다.
        private void PoseSlab(Slab slab, float open)
        {
            Vector3 sunk = Vector3.down * slabThickness * (1.5f - open);
            slab.Body.position = slab.Rest + sunk + slab.Push * open;
            slab.Body.rotation = Quaternion.Slerp(Quaternion.identity, slab.Tilt, open) * slab.Yaw;
        }

        // 적 하나가 터진다: 발밑 폭발 + 잔균열 + 피해(비율 1 = 전부) + 흔들림 + 용암빛 번쩍.
        private void Erupt(GlowLines cracks, BattleUnit unit, Vector3 position, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            Vector3 ground = Ground(position);
            if (eruptionPrefab != null)
            {
                SpawnScaled(eruptionPrefab, ground, eruptionScale);
            }

            // 잔균열: 발밑에서 사방(대략 고르게 + 무작위 비틀림)으로 짧게 갈라진다. 그 주변 땅도 조각으로 솟는다.
            float start = UnityEngine.Random.Range(0f, 360f);
            for (int i = 0; i < branchCount; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, start + 360f / branchCount * i + UnityEngine.Random.Range(-25f, 25f), 0f) * Vector3.forward;
                LineRenderer branch = cracks.Create(2);
                GlowLines.SetZigzag(branch, ground, ground + direction * branchLength * UnityEngine.Random.Range(0.6f, 1.2f), 5, 0.2f);
                FlattenToGround(branch, ground.y);

                if (slabTemplate != null)
                {
                    SpawnSlab(ground + direction * branchLength * 0.3f, Vector3.Cross(Vector3.up, direction));
                    SpawnDebris(ground, Vector3.Cross(Vector3.up, direction));
                }
            }

            cracks.SetIntensity(eruptIntensity);
            onImpact(unit, 1f);
            if (camera != null)
            {
                camera.Shake(eruptShake, 0.3f);
            }
        }

        // 용암빛이 식는다: 밝기가 내려가고 가늘어지며 투명해지고, 바위 조각이 다시 가라앉는다.
        private IEnumerator FadeOut(GlowLines cracks)
        {
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                cracks.SetIntensity(Mathf.Lerp(eruptIntensity, intensity * 0.3f, t));
                cracks.SetWidthScale(Mathf.Lerp(crackOpenWidth, 0.3f, t));
                cracks.SetAlpha(1f - t);

                foreach (Slab slab in slabs)
                {
                    PoseSlab(slab, 1f - t);
                }

                UpdateRubble(0f);
                yield return null;
            }
        }

        // 경로에서 point와 가장 가까운 점의 진행도(0~1). 점마다 거리를 재서 가장 가까운 번호 ÷ 마지막 번호.
        private static float ClosestProgress(Vector3[] path, Vector3 point)
        {
            int best = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < path.Length; i++)
            {
                float distance = (path[i] - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best / (float)(path.Length - 1);
        }

        // 경로 위 진행도 progress(0~1)의 위치 (두 점 사이를 보간).
        private static Vector3 PointOnPath(Vector3[] path, float progress)
        {
            float exact = Mathf.Clamp01(progress) * (path.Length - 1);
            int index = Mathf.Min(Mathf.FloorToInt(exact), path.Length - 2);
            return Vector3.Lerp(path[index], path[index + 1], exact - index);
        }

        // 지그재그로 흔든 점들의 높이를 바닥 높이로 맞춘다 (갈래 균열이 공중에 뜨지 않게).
        private static void FlattenToGround(LineRenderer line, float y)
        {
            for (int i = 0; i < line.positionCount; i++)
            {
                Vector3 point = line.GetPosition(i);
                point.y = y;
                line.SetPosition(i, point);
            }
        }

        private static void SpawnScaled(GameObject prefab, Vector3 position, float scale)
        {
            GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
            spawned.transform.localScale = Vector3.one * scale;
        }

        // 캡슐 중심 높이 → 바닥 높이(+ 살짝 띄움).
        private Vector3 Ground(Vector3 position)
        {
            return new Vector3(position.x, position.y + groundOffset + lift, position.z);
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
