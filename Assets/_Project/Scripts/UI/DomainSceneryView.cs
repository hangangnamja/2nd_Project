using System.Collections;
using Game.Battle;
using Game.Battle.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.UI
{
    // 영역(경계: 영역)이 펼쳐진 동안 전장에 영역 배경(DomainScenery: 사찰·물바닥·붉은 하늘)을 띄운다 (기획서 4-3 흉신 마라).
    // 전투 상태가 바뀔 때마다(StateChanged) 배경이 있는 영역을 펼친 유닛을 찾아, 새로 펼쳐졌으면 배경을 만들고(아직 땅속·투명) 끝났으면 가라앉혀 지운다.
    // 배경이 솟는 장면은 3스 연출(DomainDeployFx)이 Reveal로 재생한다. 연출이 없거나 늦으면 revealFallbackSeconds 뒤 바로 펼친다.
    // 영역 분위기 세기(DomainScenery.Blend)에 맞춰 안개·카메라 배경·주변광을 영역 색으로 섞고, 물바닥이 보이도록 원래 전투 바닥은 숨긴다.
    // 로직(BattleUnit.ActiveBoundary)은 화면을 모르고, 이 컴포넌트가 읽어서 보여주기만 한다.
    public class DomainSceneryView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private BattleStageView stageView;
        [SerializeField] private Camera targetCamera;

        [Tooltip("영역 동안 숨길 원래 전투 바닥 (물바닥에 사찰이 비쳐 보이게)")]
        [SerializeField] private Renderer battleFloor;

        [Tooltip("스테이지 전투 배경 (영역 동안 지형을 숨김). 비우면 기본 바닥만 숨긴다")]
        [SerializeField] private BattleBackgroundView backgroundView;

        [Tooltip("전투 바닥 높이 (캡슐 발밑)")]
        [SerializeField] private float floorY = -1f;

        [Tooltip("펼치는 연출이 Reveal을 부르지 않으면 이 시간(초) 뒤 바로 펼친다")]
        [SerializeField, Min(0.5f)] private float revealFallbackSeconds = 6f;

        [Tooltip("영역이 끝났는데 해제 연출이 이 시간(초) 안에 배경을 지우지 않으면 바로 가라앉힌다 (연출을 못 보여준 경우의 안전장치)")]
        [SerializeField, Min(1f)] private float closeFallbackSeconds = 20f;

        [Header("영역 색 (분위기 세기 1일 때)")]
        [SerializeField] private Color domainFogColor = new Color(0.1f, 0.01f, 0.02f);
        [SerializeField] private Color domainBackgroundColor = new Color(0.05f, 0f, 0.01f);
        [SerializeField] private Color domainAmbientColor = new Color(0.35f, 0.12f, 0.14f);

        private DomainScenery scenery;
        private DomainScenery closingScenery;
        private float closingSince;
        private DomainBoundary shownBoundary;
        private float spawnedAt;

        // 원래 화면 설정 (영역이 끝나면 돌아갈 값).
        private Color originalFogColor;
        private Color originalBackgroundColor;
        private CameraClearFlags originalClearFlags;
        private AmbientMode originalAmbientMode;
        private Color originalAmbientColor;
        private float appliedBlend;

        // 원래 화면 설정을 기억한다. 전투 배경(BattleBackgroundView)은 전투가 시작된 뒤에 깔리므로,
        // 씬이 켜질 때가 아니라 영역이 처음 드러나는 순간(blend가 0에서 올라갈 때) 부른다.
        private bool hasCaptured;

        private void CaptureOriginals()
        {
            hasCaptured = true;
            originalFogColor = RenderSettings.fogColor;
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientColor = RenderSettings.ambientLight;
            if (targetCamera != null)
            {
                originalBackgroundColor = targetCamera.backgroundColor;
                originalClearFlags = targetCamera.clearFlags;
            }
        }

        private void OnEnable()
        {
            battleManager.StateChanged += Refresh;
        }

        // 구독을 풀고 화면 설정을 원래대로 돌린다 (RenderSettings는 씬 전체 설정이라 남지 않게).
        private void OnDisable()
        {
            battleManager.StateChanged -= Refresh;
            ApplyBlend(0f);
        }

        // 배경이 있는 영역을 펼친 유닛을 찾는다. 새 영역이면 배경을 만들고, 영역이 끝났으면 가라앉힌다.
        private void Refresh()
        {
            DomainBoundary active = FindActiveDomain();
            if (active == shownBoundary)
            {
                return;
            }

            // 끝난 영역의 배경: 해제 연출(DomainCloseFx)이 있으면 그 연출이 재생 순서에 맞춰 흐려지게 하므로 그대로 둔다
            // (로직은 이미 끝났어도 화면은 마지막 공격 연출이 아직 남아 있음). 해제 연출이 없거나 주인이 쓰러져 끝났으면 바로 가라앉힌다.
            if (scenery != null)
            {
                bool closesByPresentation = shownBoundary != null && shownBoundary.ClosePresentationPrefab != null && !shownBoundary.Owner.IsDead;
                if (closesByPresentation)
                {
                    closingScenery = scenery;
                    closingSince = Time.time;
                }
                else
                {
                    // 주인이 쓰러져 끝난 경우: 로직에서는 이미 쓰러졌어도 화면에선 쓰러뜨린 공격 연출이 아직 남아 있다
                    // → 화면에서도 쓰러진 것으로 바뀐 뒤(ShownHp.IsDead) 가라앉힌다.
                    StartCoroutine(SinkAfterOwnerFalls(scenery, shownBoundary));
                }

                scenery = null;
            }

            // 해제 연출을 기다리던 배경이 남아 있는데 새 영역이 펼쳐지면 옛 배경은 바로 지운다 (배경이 둘 겹치지 않게).
            if (active != null && closingScenery != null)
            {
                Destroy(closingScenery.gameObject);
                closingScenery = null;
            }

            shownBoundary = active;
            if (active != null && active.SceneryPrefab != null)
            {
                scenery = Instantiate(active.SceneryPrefab, transform).GetComponent<DomainScenery>();
                hasOwnerPose = false;
                if (scenery != null)
                {
                    scenery.Place(floorY, OwnerPosition(active));
                    spawnedAt = Time.time;
                }
            }
        }

        // 영역 주인이 화면에서도 쓰러질 때까지(쓰러뜨린 공격 연출이 끝날 때까지) 기다렸다가 배경을 가라앉힌다.
        // 안전장치: 실제 시간 20초가 지나면 그냥 가라앉힌다. 주인이 없으면 바로.
        private IEnumerator SinkAfterOwnerFalls(DomainScenery target, DomainBoundary boundary)
        {
            float waited = 0f;
            while (boundary != null && target != null && !ShownHp.IsDead(boundary.Owner) && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (target != null)
            {
                yield return target.Sink();
            }
        }

        private DomainBoundary FindActiveDomain()
        {
            foreach (BattleUnit unit in battleManager.Allies)
            {
                // 패턴 매칭 is: 경계가 영역 종류면 domain으로 꺼낸다.
                if (!unit.IsDead && unit.ActiveBoundary is DomainBoundary domain)
                {
                    return domain;
                }
            }

            foreach (BattleUnit unit in battleManager.Enemies)
            {
                if (!unit.IsDead && unit.ActiveBoundary is DomainBoundary domain)
                {
                    return domain;
                }
            }

            return null;
        }

        // 영역 주인이 화면에 서 있는 자리 (물이 퍼지는 중심). 화면에 없으면 전장 가운데.
        private Vector3 OwnerPosition(DomainBoundary domain)
        {
            UnitView view = stageView != null ? stageView.FindView(domain.Owner) : null;
            return view != null ? view.AnchorPosition : transform.position;
        }

        // 연출 없이 너무 오래 펼쳐지지 않았으면 바로 펼치고, 분위기 세기에 맞춰 전장 색·바닥을 맞춘다.
        // 배경이 가라앉는 중(scenery = null)이어도 DomainScenery.Current의 세기를 따라 색이 함께 돌아온다.
        private void Update()
        {
            if (scenery != null && !scenery.IsRevealed && Time.time - spawnedAt > revealFallbackSeconds)
            {
                scenery.RevealInstant(OwnerPosition(shownBoundary));
            }

            // 해제 연출이 끝나 배경이 지워졌으면 참조가 null(Unity의 == null)이 된다. 너무 오래 남아 있으면 직접 가라앉힌다.
            if (closingScenery != null && Time.time - closingSince > closeFallbackSeconds)
            {
                StartCoroutine(closingScenery.Sink());
                closingScenery = null;
            }

            float blend = DomainScenery.Current != null ? DomainScenery.Current.Blend : 0f;
            if (!Mathf.Approximately(blend, appliedBlend))
            {
                ApplyBlend(blend);
            }
        }

        // 사찰·하늘이 영역 주인을 따라 그 등 뒤에 서게 한다. Update에서 무대가 다시 세워진 뒤(LateUpdate)에 읽는다.
        // 주인이 화면에 없는 장면(다른 아군 차례)에서는 마지막으로 본 주인 자리를 기준으로 둔다.
        // 카메라 자리도 함께 넘겨, 카메라가 주인 등 뒤로 멀리 물러난 구도(큰 보스)에서 사찰이 카메라를 덮지 않게 한다.
        private void LateUpdate()
        {
            if (scenery == null || shownBoundary == null || stageView == null)
            {
                return;
            }

            UnitView view = stageView.FindView(shownBoundary.Owner);
            if (view != null && view.gameObject.activeInHierarchy)
            {
                lastOwnerPosition = view.AnchorPosition;
                lastOwnerForward = view.transform.forward;
                hasOwnerPose = true;
            }

            if (hasOwnerPose && targetCamera != null)
            {
                scenery.Follow(lastOwnerPosition, lastOwnerForward, targetCamera.transform.position);
            }
        }

        // 마지막으로 본 영역 주인의 자리·방향 (주인이 안 보이는 장면에서도 사찰 자리를 카메라에 맞춰 다시 계산하려고 둠).
        private Vector3 lastOwnerPosition;
        private Vector3 lastOwnerForward;
        private bool hasOwnerPose;

        // blend 0~1만큼 안개·배경·주변광을 영역 색으로 섞는다. 조금이라도 영역이면 원래 바닥을 숨긴다 (물바닥과 반사가 보이게).
        private void ApplyBlend(float blend)
        {
            // 아직 영역을 한 번도 보여준 적이 없으면(원래 값을 기억하지 않음) 되돌릴 것도 없으므로 건너뛴다
            // → 전투 배경이 정한 화면 설정을 덮어쓰지 않는다.
            if (!hasCaptured && blend <= 0f)
            {
                return;
            }

            if (appliedBlend <= 0f && blend > 0f)
            {
                CaptureOriginals();
            }

            appliedBlend = blend;
            bool isOriginal = blend <= 0f;
            RenderSettings.fogColor = Color.Lerp(originalFogColor, domainFogColor, blend);
            RenderSettings.ambientMode = isOriginal ? originalAmbientMode : AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(originalAmbientColor, domainAmbientColor, blend);

            if (targetCamera != null)
            {
                targetCamera.clearFlags = isOriginal ? originalClearFlags : CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = Color.Lerp(originalBackgroundColor, domainBackgroundColor, blend);
            }

            if (backgroundView != null)
            {
                backgroundView.SetSceneryVisible(blend < 0.5f);
            }
            else if (battleFloor != null)
            {
                battleFloor.enabled = blend < 0.5f;
            }
        }
    }
}
