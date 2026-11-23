using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Game.UI
{
    // 반전 참격 연출 (흉신 마라 2스 참, 임시). 다가가 손을 대는 순간 화면이 흑백으로 반전되고, 대상 몸통을 지나는 긴 직선이 화면을 가로지른다.
    // 반전이 끝나는 순간 그 선대로 화면 자체가 베어진다: 화면을 찍어 선을 따라 조각내고, 조각들이 선 방향으로 어긋나 틈이 벌어진다.
    //  1) 시전자와 대상을 옆에서 보는 시점으로 컷 → 시전자가 대상 바로 앞까지 달려가 손을 뻗어 댄다
    //  2) 닿는 순간 작은 불꽃 + 화면 흑백 반전 (흑백 = 후처리 Volume 채도 -100, 반전 = 화면 맨 위 UI 판 + ScreenInvert 셰이더)
    //  3) 반전되자마자 대상 몸통을 지나 화면을 가로지르는 검은 직선 전부가 한꺼번에 그어지고, 반전이 끝날 때까지 그대로
    //  4) 반전이 끝나는 순간 화면을 찍어(ScreenCapture) 선마다 조각을 둘로 자른다 → 조각들이 선을 따라 서로 반대로 미끄러져 어긋나고,
    //     틈은 검게 벌어지며 베인 자리에 하얀 선이 번쩍인다. 화면 조각 전체가 흔들린다
    //  5) 잠깐 멈췄다가 어긋난 조각이 흐려지며 사라지는 동안, 그 아래 실제 화면에서 선마다 혈흔이 뿜어지고 피해가 한꺼번에 들어간다
    //  6) 시전자는 뒤로 물러나 제자리로 → 원래 시점
    // 혈흔은 파티클 프리팹이 맡고, 이 코드는 위치·방향·순서·시간·카메라 컷·반전 세기·화면 조각의 모양과 움직임만 정한다.
    // 월드 참격선 모드(useWorldSlashes, 지금 쓰는 방식): 3)~5) 대신 반전 동안 대상 몸에 하얀 참격선(영역 공격과 같은 모양) 전부가 한꺼번에 나타나 있다가,
    //  반전이 끝나면 선마다 차례로 번쩍이며 베인다 (선 하나 = 피해 1타 + 혈흔). 반전 판 아래에 그려지므로 반전 동안은 선도 검게 보인다.
    public class InvertCleaveFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("화면 반전 재질 (ScreenInvert 셰이더)")]
        [SerializeField] private Material invertMaterial;

        [Tooltip("손을 댈 때 터지는 작은 불꽃 (끝나면 스스로 지워지는 프리팹, 비우면 생략)")]
        [SerializeField] private GameObject touchBurstPrefab;

        [Tooltip("체크하면 피해가 들어갈 때 공통 타격 이펙트(펑 터지는 파티클)를 띄우지 않는다. 화면 베기·혈흔이 타격감을 대신함")]
        [SerializeField] private bool hideDefaultHitEffect = true;

        public override bool HidesDefaultHitEffect => hideDefaultHitEffect;

        // 기본 시전 모션(떠오르며 시전 자세)을 끈다: 바로 다가가며 팔을 휘두르는 베기 동작을 보여줘야 해서.
        public override bool CasterCasts => false;

        [Header("혈흔")]
        [Tooltip("베인 자리에서 뿜어지는 피 (끝나면 스스로 지워지는 파티클 프리팹, 파티클이 프리팹의 앞(+z) 방향으로 뿜어지는 모양)")]
        [SerializeField] private GameObject bloodSprayPrefab;

        [Tooltip("선 하나가 몸통을 지나며 피가 뿜어지는 자리 수 · 그 자리들이 몸 중심에서 선을 따라 퍼지는 거리")]
        [SerializeField, Range(1, 6)] private int bloodPerCut = 2;
        [SerializeField, Min(0f)] private float bloodAlongCut = 0.45f;

        [Tooltip("피가 뿜어지는 방향을 위로 기울이는 정도 (0 = 베인 선에 수직 그대로)")]
        [SerializeField, Range(0f, 1f)] private float bloodUpward = 0.35f;

        [SerializeField, Min(0.1f)] private float bloodScale = 1f;

        [Tooltip("마지막 타에는 피를 더 많이·크게")]
        [SerializeField, Range(1, 10)] private int finalBloodCount = 5;
        [SerializeField, Min(0.1f)] private float finalBloodScale = 1.5f;

        [Header("다가가 손대기")]
        [Tooltip("대상 바로 앞에서 멈추는 거리")]
        [SerializeField, Min(0f)] private float stopShort = 1.1f;
        [SerializeField, Min(0f)] private float windUpSeconds = 0.12f;
        [SerializeField, Min(0.01f)] private float dashSeconds = 0.22f;

        [Tooltip("손을 뻗으며 한 발 더 들어가는 거리 · 시간(초)")]
        [SerializeField, Min(0f)] private float touchStep = 0.35f;
        [SerializeField, Min(0.01f)] private float touchSeconds = 0.08f;

        [Tooltip("3D 모델이면 다가가기 시작할 때 재생할 Animator 상태 (팔을 감아 올렸다 휘둘러 베기). 휘두름 끝이 닿는 순간(숨 고르기 + 달리기 + 손 뻗기)에 오도록 동작을 만든다. 캡슐이면 무시")]
        [SerializeField] private string casterModelState = "Slash";

        [Header("화면 반전")]
        [Tooltip("반전이 켜지는 시간 · 반전이 유지되는 시간(초). 반전은 화면을 베는 순간 한 번에 풀린다")]
        [SerializeField, Min(0.01f)] private float invertInSeconds = 0.04f;
        [SerializeField, Min(0.1f)] private float invertHoldSeconds = 1f;

        [Tooltip("반전 중 화면 채도 (-100 = 완전 흑백). 반전 세기에 맞춰 함께 켜지고 꺼진다")]
        [SerializeField, Range(-100f, 0f)] private float invertSaturation = -100f;

        [Header("화면을 가르는 선 (반전 중)")]
        [Tooltip("선 수 = 피해가 나뉘어 들어가는 타수")]
        [SerializeField, Range(2, 16)] private int slashCount = 6;

        [Tooltip("선이 지나는 점이 대상 몸 중심에서 벗어나는 범위 (화면 가로·세로) → 모든 선이 몸통을 지남")]
        [SerializeField, Min(0f)] private float spreadX = 0.15f;
        [SerializeField, Min(0f)] private float spreadY = 0.35f;

        [Tooltip("선들이 모이는 가운데 각도 (화면 가로 = 0도, 45 = 오른쪽 위로 비스듬히)")]
        [SerializeField, Range(-90f, 90f)] private float angleCenter = 50f;

        [Tooltip("선들이 펼쳐지는 각도 폭 (60~90이면 몸통 위주로 한 방향에 몰린 검격)")]
        [SerializeField, Range(0f, 180f)] private float angleSpread = 75f;

        [Tooltip("선 하나의 월드 길이 (화면 끝에서 끝까지 넘도록 길게)")]
        [SerializeField, Min(1f)] private float lineWorldLength = 30f;

        [Tooltip("선을 대상보다 카메라 쪽으로 당기는 거리")]
        [SerializeField] private float towardCamera = 0.6f;

        [Tooltip("선 굵기 (1080p 화면 기준 픽셀) · 색")]
        [SerializeField, Min(0.5f)] private float guideThickness = 3f;
        [SerializeField] private Color guideColor = new Color(0.05f, 0.05f, 0.05f, 1f);

        [Tooltip("선 전부가 한꺼번에 화면을 가로질러 그어지는 시간(초). 남은 반전 시간 동안은 그어진 채로 멈춰 있다")]
        [SerializeField, Min(0.01f)] private float guideDrawSeconds = 0.06f;

        [Header("화면 베기 (반전이 끝나는 순간)")]
        [Tooltip("조각이 선을 따라 어긋나는 거리 (1080p 기준 픽셀, 선마다 더해짐)")]
        [SerializeField, Min(0f)] private float sliceShift = 22f;

        [Tooltip("조각 사이 틈 색 (화면이 베여 갈라진 틈)")]
        [SerializeField] private Color gapColor = Color.black;

        [Tooltip("베인 자리에 번쩍이는 선 색 · 굵기(1080p 기준 픽셀)")]
        [SerializeField] private Color seamColor = Color.white;
        [SerializeField, Min(0.5f)] private float seamThickness = 2f;

        [Tooltip("조각이 어긋나는 시간 · 어긋난 채 멈춰 있는 시간 · 흐려지며 사라지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float sliceOpenSeconds = 0.06f;
        [SerializeField, Min(0f)] private float sliceHoldSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float sliceFadeSeconds = 0.3f;

        [Tooltip("사라지는 동안 조각이 더 벌어지는 배율 (1 = 그 자리, 1.5 = 반 더 벌어지며 흩어짐)")]
        [SerializeField, Min(1f)] private float sliceDriftScale = 1.6f;

        [Tooltip("베는 순간 화면 조각 전체가 흔들리는 세기(픽셀)·시간(초)")]
        [SerializeField, Min(0f)] private float sliceShakePixels = 14f;
        [SerializeField, Min(0.01f)] private float sliceShakeSeconds = 0.18f;

        [Header("피해 (화면 조각이 사라질 때)")]
        [Tooltip("선마다 피해 숫자가 뜨는 간격(초). 아주 짧게 엇갈려 숫자가 한 자리에 겹치지 않게 (0 = 완전히 동시)")]
        [SerializeField, Min(0f)] private float hitInterval = 0.015f;
        [SerializeField, Min(0f)] private float hitShake = 0.08f;
        [SerializeField, Min(0f)] private float finalShake = 0.4f;

        [Header("돌아오기")]
        [SerializeField, Min(0f)] private float afterSeconds = 0.25f;
        [SerializeField, Min(0.01f)] private float returnSeconds = 0.3f;

        [Header("카메라")]
        [SerializeField] private bool sideView = true;

        [Tooltip("시전자·대상 사이 선에서 옆으로 물러나는 거리 · 높이 · 화각")]
        [SerializeField, Min(1f)] private float viewDistance = 6.5f;
        [SerializeField] private float viewHeight = 0.9f;
        [SerializeField] private float viewFieldOfView = 42f;

        [Header("월드 참격선 (체크하면 화면 베기 대신)")]
        [Tooltip("체크하면 반전 동안 대상 몸에 하얀 참격선(영역 공격과 같은 모양)이 한꺼번에 나타나 있다가, 반전이 끝나면 선마다 차례로 베인다")]
        [SerializeField] private bool useWorldSlashes = true;

        [Tooltip("참격선 견본 (꺼 둔 자식 LineRenderer, 흰색·양 끝이 가는 굵기 곡선)")]
        [SerializeField] private LineRenderer slashLine;

        [Tooltip("참격선 길이 범위 · 선 가운데가 대상 몸 중심에서 벗어나는 범위(화면 가로·세로)")]
        [SerializeField, Min(0.2f)] private float worldSlashMinLength = 4.5f;
        [SerializeField, Min(0.2f)] private float worldSlashMaxLength = 6.5f;
        [SerializeField, Min(0f)] private float worldSlashSpreadX = 0.35f;
        [SerializeField, Min(0f)] private float worldSlashSpreadY = 0.6f;

        [Tooltip("반전이 끝난 뒤 선이 하나씩 베이는 간격 · 벤 선이 사라지는 시간(초)")]
        [SerializeField, Min(0f)] private float worldSlashInterval = 0.06f;
        [SerializeField, Min(0.05f)] private float worldSlashFadeSeconds = 0.25f;

        [Tooltip("나타나 있을 때 밝기 · 베이는 순간 번쩍이는 밝기")]
        [SerializeField] private float worldSlashIntensity = 3f;
        [SerializeField] private float worldSlashFlashIntensity = 6f;

        [Tooltip("반전 동안 떠 있을 때의 굵기 배율 (견본 굵기 기준) · 베이는 순간 번쩍일 때의 굵기 배율")]
        [SerializeField, Min(0.01f)] private float worldSlashHoldWidth = 0.35f;
        [SerializeField, Min(0.01f)] private float worldSlashFlashWidth = 1.4f;

        private const int WorldSlashPoints = 12;

        private BattleCameraDirector cinematicCamera;
        private Camera view;
        private RectTransform overlay;
        private Image invertImage;
        private Volume grayscaleVolume;
        private Texture2D screenShot;

        // 화면 평면: 카메라의 오른쪽·위쪽·카메라 쪽 방향 (선을 화면에 비스듬히 보이게 이 평면 위에 놓는다).
        private Vector3 screenRight = Vector3.right;
        private Vector3 screenUp = Vector3.up;
        private Vector3 toCamera = Vector3.back;

        // 화면을 가르는 선 하나: 월드 시작점·끝점(혈흔 자리) + 화면 픽셀 위의 시작점·방향(화면 자르기) + 화면에 그린 선(UI).
        private class SlashPath
        {
            public Vector3 From;
            public Vector3 To;
            public Vector2 ScreenOrigin;
            public Vector2 ScreenDirection;
            public Image Guide;
            public float GuideLength;
        }

        // 대상 몸에 그어 둔 하얀 참격선 하나: 선(밝기를 따로 바꾸려고 선마다 GlowLines 하나) + 점 목록 + 혈흔 자리(SlashPath의 From·To).
        private class WorldSlash
        {
            public GlowLines Lines;
            public LineRenderer Line;
            public Vector3[] Points;
            public SlashPath Path;
        }

        // 연출이 중간에 사라져도 카메라·캡슐이 연출 상태로 남지 않게 되돌린다 (반전 판·선·조각은 이 오브젝트의 자식이라 함께 지워진다).
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

            // 런타임에 만든 후처리 설정·찍어 둔 화면 그림은 에셋이 아니라서 직접 지워야 메모리에 남지 않는다.
            if (grayscaleVolume != null && grayscaleVolume.sharedProfile != null)
            {
                Destroy(grayscaleVolume.sharedProfile);
            }

            if (screenShot != null)
            {
                Destroy(screenShot);
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
            Vector3 forward = Flat(targetPosition - origin);
            Vector3 stop = new Vector3(targetPosition.x, origin.y, targetPosition.z) - forward * stopShort;
            Quaternion facing = Quaternion.LookRotation(forward);

            BeginSideView(origin, targetPosition, forward, camera);
            CreateOverlay();
            CreateGrayscale();
            CasterView.SetScriptedPose(origin, facing);

            // 베기 동작은 지금 시작: 숨 고르기·달리기 동안 팔을 감아 올렸다가, 손이 닿는 순간 가슴 앞을 가로질러 휘두른 자세가 된다.
            CasterView.PlayModelState(casterModelState);
            yield return new WaitForSeconds(windUpSeconds);

            // 1) 달려가서 2) 손을 뻗어 댄다 → 닿는 순간 불꽃 + 반전
            yield return Move(origin, stop, facing, dashSeconds);
            Vector3 touch = stop + forward * touchStep;
            yield return Move(stop, touch, facing, touchSeconds);
            Spawn(touchBurstPrefab, targetPosition, 1f);
            ReadScreenPlane(camera);
            yield return Animate(invertInSeconds, SetInvert);

            // 3') 월드 참격선 모드: 반전되자마자 대상 몸에 하얀 참격선 전부를 한꺼번에 긋고 반전 동안 둔다
            //     → 반전이 끝나면 선마다 차례로 번쩍이며 베인다 (선 하나 = 피해 1타).
            if (useWorldSlashes && slashLine != null)
            {
                List<WorldSlash> cuts = CreateWorldSlashes(targetPosition);
                yield return Animate(guideDrawSeconds, t =>
                {
                    foreach (WorldSlash cut in cuts)
                    {
                        GlowLines.DrawPartial(cut.Line, cut.Points, t);
                    }
                });
                yield return new WaitForSeconds(Mathf.Max(0f, invertHoldSeconds - guideDrawSeconds));

                SetInvert(0f);
                yield return CutWorldSlashes(cuts, target, onImpact, camera);
            }
            else
            {
                yield return DrawAndSliceScreen(targetPosition, target, onImpact, camera);
            }

            // 6) 뒤로 물러나 제자리로.
            yield return new WaitForSeconds(afterSeconds);
            yield return Move(touch, origin, facing, returnSeconds);

            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
                cinematicCamera = null;
            }

            CasterView.ClearScriptedPose();
            Destroy(gameObject);
        }

        // 3)~5) 화면 베기 모드: 화면을 가르는 검은 선 전부를 한꺼번에 긋고, 반전이 끝나는 순간 그 선대로 화면을 베어 낸다.
        private IEnumerator DrawAndSliceScreen(Vector3 targetPosition, BattleUnit target, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            var paths = new List<SlashPath>();
            for (int i = 0; i < slashCount; i++)
            {
                paths.Add(CreatePath(targetPosition, i));
            }

            yield return Animate(guideDrawSeconds, t =>
            {
                foreach (SlashPath path in paths)
                {
                    SetGuideLength(path, t);
                }
            });
            yield return new WaitForSeconds(Mathf.Max(0f, invertHoldSeconds - guideDrawSeconds));

            // 4)·5) 반전이 끝나는 순간 그 선대로 화면을 베어 낸다 → 조각이 사라지며 혈흔·피해.
            yield return SliceScreen(paths, target, onImpact, camera);
        }

        // 대상 몸 근처를 지나는 무작위 각도의 하얀 참격선 slashCount개를 만든다 (아직 길이 0, 화면을 마주 보는 평면).
        // 방향 = 화면 가로 × cos + 화면 세로 × sin. 카메라 쪽으로 조금 당겨 캡슐에 가리지 않게 한다.
        // 굵기 곡선(양 끝 0 → 가운데 1)은 점마다 굵기를 정하므로 점을 여러 개(WorldSlashPoints)로 나눠야 가운데가 굵게 보인다.
        private List<WorldSlash> CreateWorldSlashes(Vector3 targetPosition)
        {
            var cuts = new List<WorldSlash>();
            for (int i = 0; i < slashCount; i++)
            {
                Vector3 center = targetPosition + toCamera * towardCamera
                    + screenRight * UnityEngine.Random.Range(-worldSlashSpreadX, worldSlashSpreadX)
                    + screenUp * UnityEngine.Random.Range(-worldSlashSpreadY, worldSlashSpreadY);
                float angle = UnityEngine.Random.Range(0f, 180f) * Mathf.Deg2Rad;
                float length = UnityEngine.Random.Range(worldSlashMinLength, worldSlashMaxLength);
                Vector3 half = (screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle)) * (length * 0.5f);

                var points = new Vector3[WorldSlashPoints];
                for (int p = 0; p < WorldSlashPoints; p++)
                {
                    points[p] = Vector3.Lerp(center - half, center + half, p / (float)(WorldSlashPoints - 1));
                }

                var lines = new GlowLines(slashLine, transform);
                LineRenderer line = lines.Create(WorldSlashPoints);
                lines.SetIntensity(worldSlashIntensity);
                lines.SetWidthScale(worldSlashHoldWidth);
                GlowLines.DrawPartial(line, points, 0f);
                cuts.Add(new WorldSlash
                {
                    Lines = lines,
                    Line = line,
                    Points = points,
                    Path = new SlashPath { From = center - half, To = center + half },
                });
            }

            return cuts;
        }

        // 반전이 끝난 뒤: 선마다 차례로(worldSlashInterval 간격) 번쩍이며 베인다 → 그 선을 따라 혈흔 + 피해 일부 + 흔들림, 선은 가늘어지며 사라진다.
        // 마지막 선은 비율 1(남은 피해 전부)·피 더 많이·강한 흔들림. 모든 선이 사라질 때까지 기다린다.
        private IEnumerator CutWorldSlashes(List<WorldSlash> cuts, BattleUnit target, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            float share = 1f / cuts.Count;
            for (int i = 0; i < cuts.Count; i++)
            {
                bool isLast = i == cuts.Count - 1;
                StartCoroutine(FlashAndFade(cuts[i]));
                SprayBlood(cuts[i].Path, isLast ? finalBloodCount : bloodPerCut, isLast ? finalBloodScale : bloodScale);
                onImpact(target, isLast ? 1f : share);
                if (camera != null)
                {
                    camera.Shake(isLast ? finalShake : hitShake, isLast ? 0.3f : 0.08f);
                }

                if (!isLast && worldSlashInterval > 0f)
                {
                    yield return new WaitForSeconds(worldSlashInterval);
                }
            }

            yield return new WaitForSeconds(worldSlashFadeSeconds);
        }

        // 베이는 순간 굵고 밝게 번쩍인 뒤 가늘어지고 투명해지며 사라진다.
        private IEnumerator FlashAndFade(WorldSlash cut)
        {
            yield return Animate(worldSlashFadeSeconds, t =>
            {
                cut.Lines.SetIntensity(Mathf.Lerp(worldSlashFlashIntensity, worldSlashIntensity * 0.3f, t));
                cut.Lines.SetWidthScale(Mathf.Lerp(worldSlashFlashWidth, 0.1f, t));
                cut.Lines.SetAlpha(1f - t * t);
            });

            Destroy(cut.Line.gameObject);
        }

        // 옆 시점으로 컷 (지금 카메라가 있는 쪽 옆): 시전자 출발점 ~ 대상 사이(대상 쪽으로 조금)를 바라본다.
        // Vector3.Cross(위, 앞): 앞 방향에 수직인 수평 방향(옆). Dot < 0이면 카메라 반대쪽이라 뒤집는다.
        private void BeginSideView(Vector3 origin, Vector3 targetPosition, Vector3 forward, BattleCameraDirector camera)
        {
            if (!sideView || camera == null)
            {
                return;
            }

            Vector3 middle = (origin + targetPosition) * 0.5f + forward * (Vector3.Distance(origin, targetPosition) * 0.25f);
            Vector3 side = Vector3.Cross(Vector3.up, forward);
            Camera current = camera.Camera != null ? camera.Camera : Camera.main;
            if (current != null && Vector3.Dot(side, current.transform.position - middle) < 0f)
            {
                side = -side;
            }

            var lookPoint = new GameObject("CinematicLook").transform;
            lookPoint.SetParent(transform, false);
            lookPoint.position = middle;
            camera.BeginCinematic(middle + side * viewDistance + Vector3.up * viewHeight, lookPoint, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 화면 맨 위 UI를 만든다: Screen Space - Overlay 캔버스(맨 위 순서) 아래에 반전 판(화면 전체) → 그 위에 선들.
        // 같은 캔버스에서 나중에 놓인 자식이 위에 그려지므로, 선은 반전 판보다 뒤에 붙여 반전되지 않는다.
        // CanvasScaler가 없는 Overlay 캔버스는 1단위 = 화면 1픽셀이라, 선·조각 위치를 화면 픽셀로 바로 쓴다.
        private void CreateOverlay()
        {
            var canvasObject = new GameObject("InvertOverlay", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            overlay = (RectTransform)canvasObject.transform;

            if (invertMaterial != null)
            {
                invertImage = CreateImage("Invert", overlay);
                Stretch(invertImage.rectTransform);
                invertImage.material = invertMaterial;
                SetInvert(0f);
            }
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        // 부모를 꽉 채우게 (화면 전체).
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 흑백 후처리: 이 연출 동안만 쓰는 전역 Volume(우선순위 높게)에 색 조정(채도)을 넣는다. 처음엔 weight 0 = 효과 없음.
        // Volume: 카메라 후처리 값을 덮어쓰는 영역. isGlobal이면 화면 전체, weight(0~1)만큼 섞인다.
        // profile.Add<ColorAdjustments>(): 색 조정 효과를 추가, saturation.Override: 채도 값을 이 Volume이 덮어쓰게 켠다.
        private void CreateGrayscale()
        {
            var volumeObject = new GameObject("Grayscale");
            volumeObject.transform.SetParent(transform, false);
            grayscaleVolume = volumeObject.AddComponent<Volume>();
            grayscaleVolume.isGlobal = true;
            grayscaleVolume.priority = 100f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.Override(invertSaturation);
            grayscaleVolume.sharedProfile = profile;
            grayscaleVolume.weight = 0f;
        }

        // 반전 세기 0~1: 반전 판 Image 색을 그 밝기의 회색으로, 흑백 Volume도 같은 세기로 섞는다.
        private void SetInvert(float strength)
        {
            if (invertImage != null)
            {
                invertImage.color = new Color(strength, strength, strength, 1f);
            }

            if (grayscaleVolume != null)
            {
                grayscaleVolume.weight = strength;
            }
        }

        private void ReadScreenPlane(BattleCameraDirector camera)
        {
            view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            if (view != null)
            {
                screenRight = view.transform.right;
                screenUp = view.transform.up;
                toCamera = -view.transform.forward;
            }
        }

        // 화면을 가르는 선 하나를 정한다: 대상 몸통 근처의 한 점을 지나고, lineWorldLength만큼 길게 → 화면 끝에서 끝까지.
        // 각도: 가운데 각도 ± 펼침 폭의 절반 범위를 선 수만큼 고르게 나눈 뒤 조금씩 흔든다(index) → 한 방향으로 몰린 부채꼴 검격.
        // 선은 월드 시작점·끝점을 화면 좌표로 옮겨(WorldToScreenPoint, 픽셀) UI 막대로 그린다: 시작점에 왼쪽 끝을 두고 끝점 쪽으로 돌린 얇은 사각형.
        // Atan2(세로 차, 가로 차): 두 점을 잇는 선의 화면 각도(라디안) → Rad2Deg로 도.
        private SlashPath CreatePath(Vector3 targetPosition, int index)
        {
            Vector3 center = targetPosition + toCamera * towardCamera
                + screenRight * UnityEngine.Random.Range(-spreadX, spreadX)
                + screenUp * UnityEngine.Random.Range(-spreadY, spreadY);

            // 선이 1개면 가운데, 여러 개면 (index ÷ (선 수 - 1))로 0~1 사이 자리 → 펼침 범위 안에 고르게.
            float slot = slashCount > 1 ? index / (float)(slashCount - 1) : 0.5f;
            float step = slashCount > 1 ? angleSpread / (slashCount - 1) : 0f;
            float degrees = angleCenter - angleSpread * 0.5f + angleSpread * slot + UnityEngine.Random.Range(-step * 0.3f, step * 0.3f);
            float angle = degrees * Mathf.Deg2Rad;
            Vector3 half = (screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle)) * (lineWorldLength * 0.5f);
            var path = new SlashPath { From = center - half, To = center + half };

            if (view == null || overlay == null)
            {
                return path;
            }

            Vector2 from = view.WorldToScreenPoint(path.From);
            Vector2 to = view.WorldToScreenPoint(path.To);
            Vector2 delta = to - from;
            path.ScreenOrigin = from;
            path.ScreenDirection = delta.normalized;

            path.Guide = CreateImage("Guide", overlay);
            path.Guide.color = guideColor;
            RectTransform rect = path.Guide.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = from;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            // 화면 높이 1080 기준 굵기 → 지금 화면 높이에 맞춰 늘리거나 줄인다.
            path.GuideLength = delta.magnitude;
            rect.sizeDelta = new Vector2(0f, ScreenPixels(guideThickness));
            return path;
        }

        // 선을 시작점 → 끝점으로 t(0~1)만큼 그어 나간다.
        private static void SetGuideLength(SlashPath path, float t)
        {
            if (path.Guide != null)
            {
                RectTransform rect = path.Guide.rectTransform;
                rect.sizeDelta = new Vector2(path.GuideLength * t, rect.sizeDelta.y);
            }
        }

        // 1080p 기준 픽셀 값을 지금 화면 높이에 맞춘 픽셀로.
        private static float ScreenPixels(float pixelsAt1080)
        {
            return pixelsAt1080 * Screen.height / 1080f;
        }

        // 4)·5) 화면 베기: 반전·선을 끄고 한 프레임 그린 뒤 그 화면을 찍어 조각낸다 → 조각이 어긋나고 틈·하얀 선 → 잠깐 멈춤
        //  → 조각이 흐려지며 더 벌어져 사라지는 동안 혈흔·피해가 들어간다.
        private IEnumerator SliceScreen(List<SlashPath> paths, BattleUnit target, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            SetInvert(0f);
            foreach (SlashPath path in paths)
            {
                if (path.Guide != null)
                {
                    path.Guide.enabled = false;
                }
            }

            // WaitForEndOfFrame: 이번 프레임이 다 그려진 뒤까지 기다린다 → 반전·선이 빠진 실제 화면이 찍힌다.
            // CaptureScreenshotAsTexture: 지금 화면(UI 포함)을 그림으로 복사해 돌려준다.
            yield return new WaitForEndOfFrame();
            screenShot = ToOpaqueSrgb(ScreenCapture.CaptureScreenshotAsTexture());

            List<(ScreenSlicePiece Piece, Vector2 Offset)> pieces = BuildPieces(paths);
            List<Image> seams = ShowSeams(paths);

            // 조각이 선을 따라 어긋난다 (1 - (1 - t)²: 확 벌어졌다 감속). 조각 묶음 전체는 Shake로 흔들린다.
            RectTransform sliceRoot = pieces.Count > 0 ? (RectTransform)pieces[0].Piece.transform.parent : null;
            StartCoroutine(ShakeRect(sliceRoot));
            yield return Animate(sliceOpenSeconds, t =>
            {
                float open = 1f - (1f - t) * (1f - t);
                SetPieceOffsets(pieces, open);
            });

            yield return new WaitForSeconds(sliceHoldSeconds);

            // 조각이 사라지기 시작할 때 아래 실제 화면에서 혈흔·피해 (조각이 흐려지며 드러난다).
            StartCoroutine(HitAll(paths, target, onImpact, camera));
            yield return Animate(sliceFadeSeconds, t =>
            {
                SetPieceOffsets(pieces, Mathf.Lerp(1f, sliceDriftScale, t));
                foreach ((ScreenSlicePiece piece, Vector2 _) in pieces)
                {
                    piece.color = new Color(1f, 1f, 1f, 1f - t);
                }

                foreach (Image seam in seams)
                {
                    Color color = seamColor;
                    color.a *= 1f - t;
                    seam.color = color;
                }

                if (sliceRoot != null)
                {
                    // 틈(검은 바탕)도 함께 사라진다.
                    sliceRoot.GetComponent<Image>().color = new Color(gapColor.r, gapColor.g, gapColor.b, gapColor.a * (1f - t));
                }
            });

            if (sliceRoot != null)
            {
                Destroy(sliceRoot.gameObject);
            }
        }

        // 찍은 화면 그림을 "불투명한 sRGB 그림"으로 옮겨 담는다 (원본은 지움).
        //  - 찍은 그림은 화면에 보이던 색(sRGB) 그대로인데 리니어 그림으로 만들어져, UI가 색 보정 없이 그리면 허옇게 밝아 보인다
        //    → new Texture2D(..., linear: false) = sRGB 그림으로 새로 만들어 같은 픽셀을 넣으면 원래 밝기로 보인다
        //  - URP 카메라는 화면 알파를 0으로 남기는 경우가 있어 투명도를 모두 255(불투명)로 채운다
        // GetPixels32/SetPixels32: 픽셀 색 배열을 꺼내 넣는다. Apply: 넣은 픽셀을 그래픽 카드로 올린다.
        private static Texture2D ToOpaqueSrgb(Texture2D captured)
        {
            if (captured == null)
            {
                return null;
            }

            Color32[] pixels = captured.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = 255;
            }

            var srgb = new Texture2D(captured.width, captured.height, TextureFormat.RGBA32, false, false);
            srgb.SetPixels32(pixels);
            srgb.Apply(false);
            Destroy(captured);
            return srgb;
        }

        // 찍은 화면을 선마다 둘로 자른 조각들을 만든다. 처음엔 화면 전체 사각형 하나 → 선 하나마다 모든 조각을 그 선으로 자른다.
        // 조각마다 어긋날 거리: 선마다 "그 선의 어느 쪽인지(왼쪽 +1, 오른쪽 -1)" × 선 방향 × sliceShift를 모두 더한다
        //  → 같은 선의 양쪽 조각이 서로 반대로 미끄러져, 그 선을 따라 화면이 베여 어긋난 것처럼 보인다.
        // 바탕(검은 틈) 위에 조각들을 놓아, 조각이 어긋난 틈으로 검은색이 보인다.
        private List<(ScreenSlicePiece Piece, Vector2 Offset)> BuildPieces(List<SlashPath> paths)
        {
            var result = new List<(ScreenSlicePiece, Vector2)>();
            if (overlay == null || screenShot == null)
            {
                return result;
            }

            var polygons = new List<List<Vector2>>
            {
                new List<Vector2> { Vector2.zero, new Vector2(Screen.width, 0f), new Vector2(Screen.width, Screen.height), new Vector2(0f, Screen.height) },
            };

            foreach (SlashPath path in paths)
            {
                var next = new List<List<Vector2>>();
                foreach (List<Vector2> polygon in polygons)
                {
                    var left = new List<Vector2>();
                    var right = new List<Vector2>();
                    ScreenSlicePiece.Split(polygon, path.ScreenOrigin, path.ScreenDirection, left, right);
                    if (left.Count >= 3)
                    {
                        next.Add(left);
                    }

                    if (right.Count >= 3)
                    {
                        next.Add(right);
                    }
                }

                polygons = next;
            }

            Image gap = CreateImage("SliceGap", overlay);
            Stretch(gap.rectTransform);
            gap.color = gapColor;

            float shift = ScreenPixels(sliceShift);
            foreach (List<Vector2> polygon in polygons)
            {
                // CanvasRenderer: UI 그림을 실제로 화면에 그리는 부품. 직접 만든 Graphic(조각)은 자동으로 붙지 않아, 없으면 아무것도 안 그려진다.
                var pieceObject = new GameObject("Piece", typeof(RectTransform), typeof(CanvasRenderer));
                pieceObject.transform.SetParent(gap.transform, false);
                var rect = (RectTransform)pieceObject.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                // 조각 모양은 꼭짓점이 정하지만, 영역 크기가 0이면 UI가 그리지 않고 건너뛸 수 있어 화면 크기로 둔다.
                rect.sizeDelta = new Vector2(Screen.width, Screen.height);

                var piece = pieceObject.AddComponent<ScreenSlicePiece>();
                piece.raycastTarget = false;
                piece.Set(screenShot, polygon);

                Vector2 offset = Vector2.zero;
                foreach (SlashPath path in paths)
                {
                    float side = ScreenSlicePiece.Cross(path.ScreenDirection, piece.Centroid - path.ScreenOrigin) >= 0f ? 1f : -1f;
                    offset += path.ScreenDirection * (side * shift);
                }

                result.Add((piece, offset));
            }

            return result;
        }

        // 조각들을 어긋난 거리 × amount만큼 옮긴다 (0 = 제자리, 1 = 다 어긋남).
        private static void SetPieceOffsets(List<(ScreenSlicePiece Piece, Vector2 Offset)> pieces, float amount)
        {
            foreach ((ScreenSlicePiece piece, Vector2 offset) in pieces)
            {
                piece.rectTransform.anchoredPosition = offset * amount;
            }
        }

        // 베인 자리에 하얀 선: 화면에 그렸던 선을 맨 위로 올려 하얀색·가는 굵기로 다시 보인다.
        private List<Image> ShowSeams(List<SlashPath> paths)
        {
            var seams = new List<Image>();
            foreach (SlashPath path in paths)
            {
                if (path.Guide == null)
                {
                    continue;
                }

                path.Guide.enabled = true;
                path.Guide.color = seamColor;
                path.Guide.rectTransform.SetAsLastSibling();
                RectTransform rect = path.Guide.rectTransform;
                rect.sizeDelta = new Vector2(path.GuideLength, ScreenPixels(seamThickness));
                seams.Add(path.Guide);
            }

            return seams;
        }

        // 화면 조각 묶음을 sliceShakeSeconds 동안 무작위로 흔든다 (점점 약해짐). 조각 묶음은 화면을 찍은 그림이라 카메라 흔들림 대신 이것을 쓴다.
        private IEnumerator ShakeRect(RectTransform rect)
        {
            if (rect == null)
            {
                yield break;
            }

            float elapsed = 0f;
            float strength = ScreenPixels(sliceShakePixels);
            while (elapsed < sliceShakeSeconds && rect != null)
            {
                elapsed += Time.deltaTime;
                float fall = 1f - elapsed / sliceShakeSeconds;
                rect.anchoredPosition = UnityEngine.Random.insideUnitCircle * strength * fall;
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = Vector2.zero;
            }
        }

        // 선마다 혈흔·피해·흔들림. 숫자는 hitInterval씩 아주 조금 엇갈리고, 마지막 선은 비율 1(남은 피해 전부)·피 더 많이·강한 흔들림.
        private IEnumerator HitAll(List<SlashPath> paths, BattleUnit target, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            float share = 1f / paths.Count;
            for (int i = 0; i < paths.Count; i++)
            {
                bool isLast = i == paths.Count - 1;
                SprayBlood(paths[i], isLast ? finalBloodCount : bloodPerCut, isLast ? finalBloodScale : bloodScale);
                onImpact(target, isLast ? 1f : share);
                if (camera != null)
                {
                    camera.Shake(isLast ? finalShake : hitShake, isLast ? 0.3f : 0.08f);
                }

                if (!isLast && hitInterval > 0f)
                {
                    yield return new WaitForSeconds(hitInterval);
                }
            }
        }

        // 베인 선을 따라 count군데에서 피를 뿜는다. 자리 = 선 가운데(몸통)에서 선을 따라 ±bloodAlongCut 안의 무작위 지점.
        // 방향 = 베인 선에 수직(화면 평면 위) → 칼이 지나간 틈에서 양옆으로 뿜어지는 느낌. 위쪽·카메라 쪽으로 조금 기울여 잘 보이게 한다.
        // Vector3.Cross(카메라 쪽, 선 방향): 화면 평면 위에서 선에 수직인 방향. 자리마다 좌우를 무작위로 골라 양쪽으로 튄다.
        // Quaternion.LookRotation(방향): 프리팹의 앞(+z)이 그 방향을 보게 하는 회전 → 파티클이 그쪽으로 뿜어진다.
        private void SprayBlood(SlashPath path, int count, float scale)
        {
            if (bloodSprayPrefab == null)
            {
                return;
            }

            Vector3 along = (path.To - path.From).normalized;
            Vector3 across = Vector3.Cross(toCamera, along).normalized;
            Vector3 center = (path.From + path.To) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector3 position = center + along * UnityEngine.Random.Range(-bloodAlongCut, bloodAlongCut);
                float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                Vector3 direction = (across * side + Vector3.up * bloodUpward + toCamera * 0.3f).normalized;
                GameObject spray = Instantiate(bloodSprayPrefab, position, Quaternion.LookRotation(direction));
                spray.transform.localScale = Vector3.one * scale;
            }
        }

        // from → to로 seconds 동안 움직인다 (Mathf.SmoothStep: 처음·끝이 부드러운 S자).
        private IEnumerator Move(Vector3 from, Vector3 to, Quaternion facing, float seconds)
        {
            yield return Animate(seconds, t => CasterView.SetScriptedPose(Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t)), facing));
        }

        private static void Spawn(GameObject prefab, Vector3 position, float scale)
        {
            if (prefab != null)
            {
                GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
                spawned.transform.localScale = Vector3.one * scale;
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

        // 수평 방향만 남긴 단위 벡터 (0이면 앞).
        private static Vector3 Flat(Vector3 vector)
        {
            vector.y = 0f;
            return vector.sqrMagnitude > 0.0001f ? vector.normalized : Vector3.forward;
        }
    }
}
