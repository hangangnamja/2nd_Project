using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 황금 검기 연출 (하이퍼캐리 2스, 임시). 레퍼런스: 캐리가 검을 가로로 크게 휘두르면 초승달 검기가 앞으로 날아가 적 무리를 한 번에 벤다.
    //  1) 캐리 어깨 뒤·옆에서 캐리와 적 무리를 함께 보는 시점으로 컷 + 캐리가 적 쪽으로 튀어 나간다
    //  2) 휘두르기: 캐리 앞 가까운 곳에서 초승달 검흔이 한쪽 옆에서 반대쪽 옆으로 가로로 그어진다 (조금 비스듬히)
    //  3) 검기: 그어진 초승달이 캐리를 중심으로 점점 커지며 앞으로 날아가고, 적에게 닿는 순간 그 적에게 피해(onImpact 비율 1)·불꽃·흔들림
    //  4) 적 무리를 지나 가늘어지며 사라지고 원래 시점으로
    // 검흔은 겹마다 색이 다르다: 바깥 날 = 흰빛 도는 금색, 가운데 = 금색 → 보랏빛, 안쪽 = 검보랏빛 (변신 날개의 금·검보라 두 색).
    // 검흔 모양(재질·굵기 곡선)은 프리팹의 견본 선, 겹마다의 색은 Inspector의 겹 목록이 정하고, 이 코드는 점 위치·순서·시간·카메라 컷만 정한다.
    public class GoldenSlashFx : SkillFx
    {
        // 검흔 한 겹: 반지름 배율, 굵기 배율, 꼬리 → 머리 방향 색.
        [Serializable]
        private class SlashLayer
        {
            [Tooltip("반지름 배율 (1 = 본체, 1보다 크면 바깥 날)")]
            public float radiusScale = 1f;

            [Tooltip("견본 굵기 대비 배율")]
            public float widthScale = 1f;

            [Tooltip("꼬리 → 머리 색. 비우면 견본 색")]
            public Gradient color = new Gradient();
        }

        [Header("프리팹 부품")]
        [Tooltip("검흔 견본 (꺼 둔 자식 LineRenderer). 재질과 굵기 곡선(꼬리 가늘고 머리 쪽이 굵은 초승달)을 여기서 정한다")]
        [SerializeField] private LineRenderer slashLine;

        [Tooltip("검기가 지나갈 때 대상마다 튀는 불꽃 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject hitBurstPrefab;

        [Header("검흔 모양")]
        [Tooltip("검흔 겹 (위에서부터 그린다)")]
        [SerializeField] private SlashLayer[] layers = new SlashLayer[0];

        [Tooltip("초승달이 앞을 감싸는 각도 (캐리 앞 방향 기준 좌우 합)")]
        [SerializeField, Range(30f, 300f)] private float sweepDegrees = 150f;

        [Tooltip("휘두르는 면을 앞 방향 축으로 기울인 각도 (0 = 수평으로 가로 베기, 클수록 비스듬히)")]
        [SerializeField, Range(-60f, 60f)] private float tiltDegrees = 15f;

        [Tooltip("휘두르는 높이 (캡슐 중심 기준)")]
        [SerializeField] private float swingHeight = 0.3f;

        [Tooltip("처음 그어지는 반지름 (캐리 바로 앞)")]
        [SerializeField, Min(0.3f)] private float swingRadius = 1.8f;

        [Tooltip("검기가 날아가는 끝: 가장 먼 적까지 거리 + 이 여유")]
        [SerializeField, Min(0f)] private float flyBeyond = 3f;

        [Tooltip("호를 나누는 점 수 (많을수록 매끈함)")]
        [SerializeField, Range(8, 128)] private int arcPoints = 56;

        [Header("시간")]
        [Tooltip("튀어 나간 뒤 휘두르기 시작까지(초)")]
        [SerializeField, Min(0f)] private float windUpSeconds = 0.1f;

        [Tooltip("휘두르기: 검흔이 한쪽 옆에서 반대쪽까지 그어지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float swingSeconds = 0.12f;

        [Tooltip("검기: 앞으로 날아가 끝까지 가는 시간(초)")]
        [SerializeField, Min(0.01f)] private float flySeconds = 0.35f;

        [Tooltip("날아가는 동안 검흔이 퍼지는 정도 (굵기 배율, 끝에서)")]
        [SerializeField, Min(0f)] private float flyWidthScale = 1.6f;

        [Header("세기")]
        [SerializeField] private float intensity = 2.5f;
        [SerializeField, Min(0f)] private float swingShake = 0.12f;
        [SerializeField, Min(0f)] private float hitShake = 0.15f;
        [SerializeField, Min(0.1f)] private float hitBurstScale = 1f;

        [Header("카메라")]
        [Tooltip("켜면 캐리 어깨 뒤·옆에서 캐리와 적 무리를 함께 보는 시점으로 컷한다")]
        [SerializeField] private bool shoulderView = true;

        [Tooltip("캐리에서 뒤로 · 옆으로 물러나는 거리, 높이(캡슐 중심 기준)")]
        [SerializeField] private float viewBack = 5f;
        [SerializeField] private float viewSide = 4.5f;
        [SerializeField] private float viewHeight = 1.8f;

        [Tooltip("바라보는 곳: 캐리(0) ~ 적 무리 가운데(1) 사이 비율")]
        [SerializeField, Range(0f, 1f)] private float viewFocus = 0.55f;

        [SerializeField] private float viewFieldOfView = 55f;

        // 휘두르는 면: 중심(캐리 앞), 앞 방향(적 쪽), 옆 방향(기울기 적용). 점 = 중심 + (앞 × cos + 옆 × sin) × 반지름.
        private Vector3 origin;
        private Vector3 forward;
        private Vector3 side;

        // 겹마다 처음 색·굵기 (퍼지고 사라질 때 기준).
        private readonly List<Gradient> baseColors = new List<Gradient>();
        private readonly List<float> baseWidths = new List<float>();

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
            SetUpSwing(caster, center);
            BeginShoulderView(caster, center, camera);

            // 시전 모션 대신 적 무리 쪽으로 튀어 나간다 (휘두르는 몸짓).
            if (CasterView != null)
            {
                CasterView.PlayLunge(center);
            }

            yield return new WaitForSeconds(windUpSeconds);

            List<(LineRenderer Line, float RadiusScale)> lines = CreateLayers();
            yield return Swing(lines, camera);
            yield return Fly(lines, targets, onImpact, camera);

            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 휘두르는 면을 정한다: 앞 = 캐리 → 적 무리 가운데(수평), 옆 = 앞에 수직인 수평 방향을 앞 방향 축으로 tiltDegrees만큼 기울인 것.
        // Quaternion.AngleAxis(각도, 앞): 앞 방향을 축으로 돌리는 회전 → 옆 방향의 한쪽 끝이 올라가고 반대쪽이 내려간다.
        private void SetUpSwing(Vector3 caster, Vector3 center)
        {
            forward = center - caster;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
            side = Quaternion.AngleAxis(tiltDegrees, forward) * Vector3.Cross(Vector3.up, forward);
            origin = caster + Vector3.up * swingHeight;
        }

        // 어깨 시점으로 컷: 캐리 뒤(viewBack)·옆(viewSide, 지금 카메라가 있는 쪽)·위(viewHeight)에서 캐리 ~ 적 무리 사이를 바라본다.
        private void BeginShoulderView(Vector3 caster, Vector3 center, BattleCameraDirector camera)
        {
            if (!shoulderView || camera == null)
            {
                return;
            }

            // Vector3.Cross(위, 앞): 앞에 수직인 수평 옆. 두 옆 중 지금 카메라가 있는 쪽을 고른다 (Dot < 0이면 반대쪽).
            Vector3 flatSide = Vector3.Cross(Vector3.up, forward);
            Camera view = camera.Camera != null ? camera.Camera : Camera.main;
            if (view != null && Vector3.Dot(flatSide, view.transform.position - caster) < 0f)
            {
                flatSide = -flatSide;
            }

            Vector3 position = caster - forward * viewBack + flatSide * viewSide + Vector3.up * viewHeight;
            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = Vector3.Lerp(caster, center, viewFocus);
            camera.BeginCinematic(position, lookPoint, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 겹마다 선을 만들고 색·굵기를 입힌다.
        private List<(LineRenderer Line, float RadiusScale)> CreateLayers()
        {
            var slash = new GlowLines(slashLine, transform);
            var lines = new List<(LineRenderer Line, float RadiusScale)>();
            foreach (SlashLayer layer in layers)
            {
                LineRenderer line = slash.Create(0);
                line.widthMultiplier *= layer.widthScale;
                if (layer.color != null && layer.color.colorKeys.Length > 0)
                {
                    line.colorGradient = layer.color;
                }

                lines.Add((line, layer.radiusScale));
                baseColors.Add(line.colorGradient);
                baseWidths.Add(line.widthMultiplier);
            }

            slash.SetIntensity(intensity);
            return lines;
        }

        // 2) 휘두르기: 반지름 swingRadius에서 검흔 머리가 한쪽 끝(0) → 반대쪽 끝(1)으로 그어진다. 1 - (1 - t)²: 확 휘두르고 끝에서 느려짐.
        private IEnumerator Swing(List<(LineRenderer Line, float RadiusScale)> lines, BattleCameraDirector camera)
        {
            float elapsed = 0f;
            while (elapsed < swingSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / swingSeconds);
                float head = 1f - (1f - t) * (1f - t);
                foreach ((LineRenderer line, float radiusScale) in lines)
                {
                    DrawArc(line, 0f, head, swingRadius * radiusScale);
                }

                yield return null;
            }

            if (camera != null)
            {
                camera.Shake(swingShake, 0.15f);
            }
        }

        // 3)·4) 검기: 반지름이 swingRadius → (가장 먼 적 + flyBeyond)로 커지며 날아간다.
        // 반지름이 적까지의 수평 거리를 넘는 순간 그 적을 벤다. 날아가며 굵어지고, 후반에 투명해진다.
        private IEnumerator Fly(
            List<(LineRenderer Line, float RadiusScale)> lines,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            var pending = new List<(BattleUnit Unit, Vector3 Position, float Distance)>();
            float farthest = swingRadius;
            foreach ((BattleUnit unit, Vector3 position) in targets)
            {
                float distance = FlatDistance(position);
                pending.Add((unit, position, distance));
                farthest = Mathf.Max(farthest, distance);
            }

            float endRadius = farthest + flyBeyond;
            float elapsed = 0f;
            while (elapsed < flySeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / flySeconds);

                // 1 - (1 - t)²: 처음에 빠르게 날아가다 끝에서 느려진다.
                float radius = Mathf.Lerp(swingRadius, endRadius, 1f - (1f - t) * (1f - t));
                float fade = 1f - Mathf.InverseLerp(0.5f, 1f, t);
                for (int i = 0; i < lines.Count; i++)
                {
                    DrawArc(lines[i].Line, 0f, 1f, radius * lines[i].RadiusScale);
                    lines[i].Line.widthMultiplier = baseWidths[i] * Mathf.Lerp(1f, flyWidthScale, t);
                    lines[i].Line.colorGradient = GlowLines.Faded(baseColors[i], fade);
                }

                // 검기가 닿은 적을 벤다. 뒤에서부터 지운다 (앞에서 지우면 번호가 밀림).
                for (int i = pending.Count - 1; i >= 0; i--)
                {
                    if (radius >= pending[i].Distance)
                    {
                        Hit(pending[i].Unit, pending[i].Position, onImpact, camera);
                        pending.RemoveAt(i);
                    }
                }

                yield return null;
            }

            foreach ((BattleUnit unit, Vector3 position, float _) in pending)
            {
                Hit(unit, position, onImpact, camera);
            }
        }

        // 휘두르는 면 위 호의 from ~ to 구간을 반지름 radius로 그린다.
        // 진행도 p의 각도 = -sweep/2 ~ +sweep/2 (0 = 바로 앞). 점 = 중심 + (앞 × cos + 옆 × sin) × 반지름.
        private void DrawArc(LineRenderer line, float from, float to, float radius)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt((to - from) * arcPoints) + 1);
            line.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float progress = Mathf.Lerp(from, to, i / (float)(count - 1));
                float angle = Mathf.Lerp(-sweepDegrees * 0.5f, sweepDegrees * 0.5f, progress) * Mathf.Deg2Rad;
                line.SetPosition(i, origin + (forward * Mathf.Cos(angle) + side * Mathf.Sin(angle)) * radius);
            }
        }

        // 휘두르는 중심에서 대상까지 수평 거리 (검기 반지름과 비교).
        private float FlatDistance(Vector3 position)
        {
            Vector3 flat = position - origin;
            flat.y = 0f;
            return flat.magnitude;
        }

        // 대상 하나를 벤다: 불꽃 + 피해(비율 1 = 전부) + 약한 흔들림.
        private void Hit(BattleUnit unit, Vector3 position, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            if (hitBurstPrefab != null)
            {
                GameObject burst = Instantiate(hitBurstPrefab, position, Quaternion.identity);
                burst.transform.localScale = Vector3.one * hitBurstScale;
            }

            onImpact(unit, 1f);
            if (camera != null)
            {
                camera.Shake(hitShake, 0.15f);
            }
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
