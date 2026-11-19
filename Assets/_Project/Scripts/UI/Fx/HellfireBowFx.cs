using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 업화 연출 (흉신 마라: 영역 동안 1회 3스가 바뀌는 광역 400% + 화상, 임시). 불의 활로 불화살을 쏘아 적 무리 가운데를 크게 터뜨린다.
    //  1) 마라 정면 아래에서 올려다보는 시점으로 컷 (마라만 화면에) → 마라가 하늘로 부웅 떠오른다
    //  2) 마라 손 앞에서 불꽃이 활 모양으로 가운데부터 양 끝으로 뻗어 나가 불의 활이 된다 (일렁이는 불꽃 줄 여러 겹 + 불씨)
    //  3) 마라 뒤 위쪽에서 대각선 아래로 내려다보는 시점으로 컷 → 마라가 활을 적 무리에 겨누고 시위를 당기면 불화살이 맺힌다
    //  4) 시위를 놓으면 불화살이 적 무리 가운데로 날아가 크게 폭발 → 적 전원 피해 + 강한 흔들림, 활은 흩어지며 사라진다
    //  5) 여운 뒤 마라가 내려오고 원래 시점으로
    // 불꽃 줄 모양(재질·색·굵기 곡선)은 프리팹의 견본 선, 불씨·불화살 머리·폭발은 파티클 프리팹이 맡는다. 이 코드는 위치·순서·시간·카메라만 정한다.
    public class HellfireBowFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("활 몸체 불꽃 줄 견본 (꺼 둔 자식 LineRenderer, 주황~노랑, 양 끝이 가늘게)")]
        [SerializeField] private LineRenderer flameLine;

        [Tooltip("시위·화살대 견본 (꺼 둔 자식 LineRenderer, 가늘고 밝은 노랑)")]
        [SerializeField] private LineRenderer stringLine;

        [Tooltip("활에서 피어오르는 불씨 (자식 파티클, 월드 공간·자동 방출 꺼 둠 → 코드가 활 위 자리에서 Emit)")]
        [SerializeField] private ParticleSystem embers;

        [Tooltip("불화살 머리 (불덩이 + 꼬리 파티클 프리팹)")]
        [SerializeField] private GameObject arrowHeadPrefab;
        [SerializeField, Min(0.05f)] private float arrowHeadScale = 0.45f;

        [Tooltip("불화살이 떨어진 자리의 폭발 (끝나면 스스로 지워지는 파티클 프리팹)")]
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField, Min(0.1f)] private float explosionScale = 4f;

        [Header("떠오르기")]
        [Tooltip("마라가 떠오르는 높이 · 시간(초)")]
        [SerializeField, Min(0f)] private float liftHeight = 3.5f;
        [SerializeField, Min(0.05f)] private float liftSeconds = 0.55f;

        [Header("불의 활")]
        [Tooltip("활 위아래 반 길이 · 양 끝이 뒤(마라 쪽)로 휘는 깊이 · 활이 마라 몸 앞으로 나와 있는 거리")]
        [SerializeField, Min(0.2f)] private float bowHalfHeight = 2f;
        [SerializeField, Min(0f)] private float bowDepth = 0.8f;
        [SerializeField, Min(0f)] private float bowForward = 0.9f;

        [Tooltip("활을 비스듬히 눕힌 각도 (0 = 똑바로 세움)")]
        [SerializeField, Range(-60f, 60f)] private float bowCant = 20f;

        [Tooltip("불꽃 줄 겹 수 · 겹마다 일렁이는 폭 · 일렁이는 빠르기")]
        [SerializeField, Range(1, 6)] private int flameLayers = 4;
        [SerializeField, Min(0f)] private float flicker = 0.14f;
        [SerializeField, Min(0f)] private float flickerSpeed = 7f;

        [Tooltip("불꽃 줄 밝기 (HDR 배율, Bloom으로 번짐)")]
        [SerializeField] private float flameIntensity = 2.2f;

        [Tooltip("활이 가운데부터 양 끝으로 뻗어 나가는 시간(초) · 초당 불씨 수")]
        [SerializeField, Min(0.05f)] private float bowFormSeconds = 0.6f;
        [SerializeField, Min(0f)] private float embersPerSecond = 70f;

        [Header("겨누기·쏘기")]
        [Tooltip("시위를 당기는 거리 · 당기는 시간 · 다 당긴 뒤 멈추는 시간(초)")]
        [SerializeField, Min(0f)] private float drawBack = 1f;
        [SerializeField, Min(0.05f)] private float drawSeconds = 0.45f;
        [SerializeField, Min(0f)] private float aimHoldSeconds = 0.35f;

        [Tooltip("불화살이 날아가는 시간 · 화살대 길이")]
        [SerializeField, Min(0.05f)] private float flightSeconds = 0.4f;
        [SerializeField, Min(0.1f)] private float arrowLength = 1.8f;

        [Tooltip("폭발 뒤 여운 · 내려오는 시간(초)")]
        [SerializeField, Min(0f)] private float afterSeconds = 0.9f;
        [SerializeField, Min(0.05f)] private float descendSeconds = 0.35f;

        [SerializeField, Min(0f)] private float explosionShake = 0.9f;
        [SerializeField, Min(0f)] private float explosionShakeSeconds = 0.7f;

        [Header("카메라 1: 떠오르기 (마라 정면 아래)")]
        [SerializeField, Min(1f)] private float riseViewDistance = 7f;
        [SerializeField] private float riseViewHeight = -0.6f;
        [SerializeField] private float riseViewFieldOfView = 55f;

        [Header("카메라 2: 겨누기 (마라 뒤 위에서 대각선 아래로)")]
        [Tooltip("마라 뒤로 · 마라 위로 · 옆으로 떨어진 거리")]
        [SerializeField] private float aimViewBack = 4.5f;
        [SerializeField] private float aimViewUp = 2.6f;
        [SerializeField] private float aimViewSide = 2.2f;

        [Tooltip("바라보는 곳: 마라 → 적 무리 가운데 사이의 비율 (0 = 마라, 1 = 적)")]
        [SerializeField, Range(0f, 1f)] private float aimViewLookAt = 0.45f;
        [SerializeField] private float aimViewFieldOfView = 58f;

        private const int BowPoints = 24;

        // 떠오르기는 이 연출이 직접 하므로 기본 시전 모션(제자리에서 살짝 떠오름)은 끈다.
        public override bool CasterCasts => false;

        private BattleCameraDirector cinematicCamera;
        private Transform lookPoint;
        private readonly List<GlowLines> flameGroups = new List<GlowLines>();
        private GlowLines stringGroup;
        private GameObject arrowHead;

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

            if (arrowHead != null)
            {
                Destroy(arrowHead);
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            if (CasterView == null || targets.Count == 0)
            {
                Destroy(gameObject);
                yield break;
            }

            Vector3 origin = CasterView.AnchorPosition;
            Vector3 enemyCenter = Center(targets);
            Vector3 lifted = origin + Vector3.up * liftHeight;

            // 겨누는 방향: 떠오른 마라 → 적 무리 가운데 (대각선 아래). 마라는 수평으로 적 쪽을 본다.
            Vector3 aim = (enemyCenter - lifted).normalized;
            Quaternion facing = Quaternion.LookRotation(Flat(enemyCenter - origin));

            lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);

            // 1) 정면 아래에서 올려다보며 떠오른다 (바라보는 곳도 마라를 따라 올라감).
            BeginRiseView(origin, facing * Vector3.forward, camera);
            yield return Animate(liftSeconds, t =>
            {
                Vector3 position = Vector3.Lerp(origin, lifted, 1f - (1f - t) * (1f - t));
                CasterView.SetScriptedPose(position, facing);
                lookPoint.position = position + Vector3.up * 0.6f;
            });

            // 2) 불의 활이 가운데부터 양 끝으로 뻗는다.
            BowFrame frame = new BowFrame(lifted, aim, bowCant, bowForward);
            CreateBow();
            yield return Animate(bowFormSeconds, t => DrawBow(frame, Mathf.SmoothStep(0f, 1f, t), 0f, 1f));

            // 3) 마라 뒤 위에서 대각선 아래로 내려다보는 시점으로 컷 → 시위를 당기며 불화살이 맺힌다.
            BeginAimView(lifted, enemyCenter, camera);
            arrowHead = arrowHeadPrefab != null ? Instantiate(arrowHeadPrefab) : null;
            if (arrowHead != null)
            {
                arrowHead.transform.localScale = Vector3.one * arrowHeadScale;
            }

            yield return Animate(drawSeconds, t =>
            {
                float pull = Mathf.SmoothStep(0f, 1f, t);
                DrawBow(frame, 1f, pull, 1f);
                PlaceArrow(frame.Nock(pull * drawBack), aim);
            });

            yield return Animate(aimHoldSeconds, t =>
            {
                DrawBow(frame, 1f, 1f, 1f);
                PlaceArrow(frame.Nock(drawBack), aim);
            });

            // 4) 시위를 놓는다: 화살이 적 무리 가운데로 날아가고, 활은 흩어지며 사라진다.
            Vector3 launch = frame.Nock(drawBack) + aim * arrowLength;
            Coroutine bowFade = StartCoroutine(FadeBow(frame));
            yield return Animate(flightSeconds, t =>
            {
                // t²: 처음엔 느리게 출발해 점점 빨라진다.
                Vector3 head = Vector3.Lerp(launch, enemyCenter, t * t);
                PlaceArrow(head - aim * arrowLength, aim);
            });

            Explode(enemyCenter, targets, onImpact, camera);
            yield return bowFade;
            yield return new WaitForSeconds(afterSeconds);

            // 5) 내려와서 원래 시점으로.
            yield return Animate(descendSeconds, t => CasterView.SetScriptedPose(Vector3.Lerp(lifted, origin, t * t), facing));
            CasterView.ClearScriptedPose();
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 활을 그리는 기준 틀: 손잡이 자리·겨누는 방향(앞)·활의 위아래 방향(위)·옆.
        // 위 = 겨누는 방향에 수직인 세상 위쪽(ProjectOnPlane)을 겨누는 축으로 bowCant만큼 돌린 것 → 활이 비스듬히 선다.
        // Quaternion.AngleAxis(각도, 축): 축을 중심으로 각도만큼 돌리는 회전. Vector3.Cross(위, 앞): 둘에 수직인 옆 방향.
        private readonly struct BowFrame
        {
            public readonly Vector3 Grip;
            public readonly Vector3 Forward;
            public readonly Vector3 Up;
            public readonly Vector3 Side;

            public BowFrame(Vector3 caster, Vector3 aim, float cant, float forwardOffset)
            {
                Forward = aim;
                Vector3 up = Vector3.ProjectOnPlane(Vector3.up, aim).normalized;
                Up = Quaternion.AngleAxis(cant, aim) * up;
                Side = Vector3.Cross(Up, Forward).normalized;
                Grip = caster + Vector3.up * 0.3f + aim * forwardOffset;
            }

            // 시위를 pull만큼 당겼을 때 화살 끝(시위 가운데) 자리.
            public Vector3 Nock(float pull) => Grip - Forward * pull;
        }

        // 불꽃 줄 겹마다 GlowLines 하나 (겹마다 굵기·밝기를 다르게) + 시위·화살대 묶음.
        private void CreateBow()
        {
            for (int layer = 0; layer < flameLayers; layer++)
            {
                var group = new GlowLines(flameLine, transform);
                group.Create(BowPoints);
                group.SetIntensity(flameIntensity * (1f - layer * 0.12f));
                group.SetWidthScale(1f - layer * 0.18f);
                flameGroups.Add(group);
            }

            if (stringLine != null)
            {
                stringGroup = new GlowLines(stringLine, transform);
                stringGroup.Create(3);
                stringGroup.Create(2);
                stringGroup.SetIntensity(flameIntensity);
            }
        }

        // 활을 그린다. extent: 가운데부터 양 끝으로 뻗은 정도(0~1), pull: 시위를 당긴 정도(0~1), emberRate: 불씨 세기 배율.
        // 활 몸체: s(-1 ~ 1)마다 손잡이 + 위 × s × 반 길이 - 앞 × 깊이 × s² → 양 끝이 마라 쪽으로 휜 활 모양.
        // 일렁임: 겹·점마다 다른 Perlin 잡음(시간에 따라 부드럽게 변하는 0~1 값)으로 옆·앞 방향으로 흔든다 (가운데보다 끝이 더 크게).
        private void DrawBow(BowFrame frame, float extent, float pull, float emberRate)
        {
            float time = Time.time * flickerSpeed;
            Vector3 top = Vector3.zero;
            Vector3 bottom = Vector3.zero;
            for (int layer = 0; layer < flameGroups.Count; layer++)
            {
                LineRenderer line = flameGroups[layer].Lines[0];
                for (int i = 0; i < BowPoints; i++)
                {
                    float s = Mathf.Lerp(-1f, 1f, i / (float)(BowPoints - 1)) * extent;
                    Vector3 point = frame.Grip + frame.Up * (s * bowHalfHeight) - frame.Forward * (bowDepth * s * s);
                    float amount = flicker * (layer == 0 ? 0.3f : 1f) * (0.4f + Mathf.Abs(s));
                    float n1 = Mathf.PerlinNoise(time + i * 0.37f, layer * 3.1f) - 0.5f;
                    float n2 = Mathf.PerlinNoise(layer * 5.3f, time + i * 0.29f) - 0.5f;
                    point += (frame.Side * n1 + frame.Forward * n2) * (amount * 2f);
                    line.SetPosition(i, point);
                    if (layer == 0 && i == 0)
                    {
                        bottom = point;
                    }
                    else if (layer == 0 && i == BowPoints - 1)
                    {
                        top = point;
                    }
                }
            }

            DrawString(frame, top, bottom, extent, pull);
            EmitEmbers(frame, extent, emberRate);
        }

        // 시위: 위 끝 → 화살 끝(당긴 자리) → 아래 끝. 활이 다 펼쳐진 뒤에만 보인다. 화살대는 당기기 시작하면 보인다.
        private void DrawString(BowFrame frame, Vector3 top, Vector3 bottom, float extent, float pull)
        {
            if (stringGroup == null)
            {
                return;
            }

            LineRenderer bowString = stringGroup.Lines[0];
            LineRenderer shaft = stringGroup.Lines[1];
            bool shown = extent >= 0.99f;
            bowString.enabled = shown;
            Vector3 nock = frame.Nock(pull * drawBack);
            bowString.SetPosition(0, top);
            bowString.SetPosition(1, nock);
            bowString.SetPosition(2, bottom);

            shaft.enabled = shown && pull > 0.01f;
            shaft.SetPosition(0, nock);
            shaft.SetPosition(1, nock + frame.Forward * arrowLength);
        }

        // 불씨: 이번 프레임에 나올 개수만큼 활 몸체 위 무작위 자리에서 내보낸다 (위로 살짝 떠오르는 속도).
        // EmitParams: 한 알갱이의 자리·속도를 정해 Emit으로 내보낸다 (파티클 설정의 나머지 값은 그대로).
        private void EmitEmbers(BowFrame frame, float extent, float rate)
        {
            if (embers == null)
            {
                return;
            }

            int count = Mathf.RoundToInt(embersPerSecond * rate * Time.deltaTime + UnityEngine.Random.value);
            for (int i = 0; i < count; i++)
            {
                float s = UnityEngine.Random.Range(-1f, 1f) * extent;
                var emit = new ParticleSystem.EmitParams
                {
                    position = frame.Grip + frame.Up * (s * bowHalfHeight) - frame.Forward * (bowDepth * s * s),
                    velocity = Vector3.up * UnityEngine.Random.Range(0.4f, 1.4f) + UnityEngine.Random.insideUnitSphere * 0.4f,
                    applyShapeToPosition = false,
                };
                embers.Emit(emit, 1);
            }
        }

        // 불화살: 화살 끝(꼬리) 자리에서 겨누는 방향으로 화살대 + 머리의 불덩이.
        private void PlaceArrow(Vector3 tail, Vector3 aim)
        {
            Vector3 head = tail + aim * arrowLength;
            if (stringGroup != null)
            {
                LineRenderer shaft = stringGroup.Lines[1];
                shaft.enabled = true;
                shaft.SetPosition(0, tail);
                shaft.SetPosition(1, head);
            }

            if (arrowHead != null)
            {
                arrowHead.transform.SetPositionAndRotation(head, Quaternion.LookRotation(aim));
            }
        }

        // 활이 크게 번쩍인 뒤 흐려지며 사라진다 (시위는 바로 사라짐). 화살대는 날아가는 동안 계속 보인다.
        private IEnumerator FadeBow(BowFrame frame)
        {
            if (stringGroup != null)
            {
                stringGroup.Lines[0].enabled = false;
            }

            yield return Animate(0.5f, t =>
            {
                for (int layer = 0; layer < flameGroups.Count; layer++)
                {
                    LineRenderer line = flameGroups[layer].Lines[0];
                    DrawBowLayerOnly(frame, layer, line);
                    flameGroups[layer].SetIntensity(flameIntensity * Mathf.Lerp(1.6f, 0.4f, t));
                    flameGroups[layer].SetAlpha(1f - t);
                }

                EmitEmbers(frame, 1f, 1f - t);
            });

            foreach (GlowLines group in flameGroups)
            {
                group.Lines[0].enabled = false;
            }
        }

        // 사라지는 동안에도 활 몸체가 계속 일렁이게 한 겹만 다시 그린다 (시위는 건드리지 않음).
        private void DrawBowLayerOnly(BowFrame frame, int layer, LineRenderer line)
        {
            float time = Time.time * flickerSpeed;
            for (int i = 0; i < BowPoints; i++)
            {
                float s = Mathf.Lerp(-1f, 1f, i / (float)(BowPoints - 1));
                Vector3 point = frame.Grip + frame.Up * (s * bowHalfHeight) - frame.Forward * (bowDepth * s * s);
                float amount = flicker * (layer == 0 ? 0.3f : 1f) * (0.4f + Mathf.Abs(s));
                float n1 = Mathf.PerlinNoise(time + i * 0.37f, layer * 3.1f) - 0.5f;
                float n2 = Mathf.PerlinNoise(layer * 5.3f, time + i * 0.29f) - 0.5f;
                line.SetPosition(i, point + (frame.Side * n1 + frame.Forward * n2) * (amount * 2f));
            }
        }

        // 화살이 떨어진 자리에서 크게 폭발: 화살 머리를 지우고 폭발 파티클 + 적 전원 피해(비율 1) + 강한 흔들림.
        private void Explode(Vector3 center, IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            if (arrowHead != null)
            {
                Destroy(arrowHead);
                arrowHead = null;
            }

            if (stringGroup != null)
            {
                stringGroup.Lines[1].enabled = false;
            }

            if (explosionPrefab != null)
            {
                Instantiate(explosionPrefab, center, Quaternion.identity).transform.localScale = Vector3.one * explosionScale;
            }

            foreach ((BattleUnit unit, Vector3 _) in targets)
            {
                onImpact(unit, 1f);
            }

            if (camera != null)
            {
                camera.Shake(explosionShake, explosionShakeSeconds);
            }
        }

        // 떠오르기 시점: 마라 정면, 조금 아래에서 올려다본다.
        private void BeginRiseView(Vector3 origin, Vector3 forward, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            lookPoint.position = origin + Vector3.up * 0.6f;
            camera.BeginCinematic(origin + Flat(forward) * riseViewDistance + Vector3.up * riseViewHeight, lookPoint, riseViewFieldOfView);
            cinematicCamera = camera;
        }

        // 겨누기 시점: 떠오른 마라의 뒤·위·옆에서, 마라와 적 무리 사이를 내려다본다 (대각선 아래 방향).
        private void BeginAimView(Vector3 lifted, Vector3 enemyCenter, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            Vector3 toEnemy = Flat(enemyCenter - lifted);
            Vector3 side = Vector3.Cross(Vector3.up, toEnemy).normalized;
            lookPoint.position = Vector3.Lerp(lifted, enemyCenter, aimViewLookAt);
            camera.BeginCinematic(lifted - toEnemy * aimViewBack + Vector3.up * aimViewUp + side * aimViewSide, lookPoint, aimViewFieldOfView);
            cinematicCamera = camera;
        }

        // 대상들의 가운데 (화면 위치 평균).
        private static Vector3 Center(IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets)
        {
            Vector3 sum = Vector3.zero;
            foreach ((BattleUnit _, Vector3 position) in targets)
            {
                sum += position;
            }

            return sum / targets.Count;
        }

        // 수평 방향만 남긴 단위 벡터 (0이면 앞).
        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }

        // seconds 동안 0 → 1 진행도를 apply(람다)에 넘긴다 (전투 시간 기준).
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
