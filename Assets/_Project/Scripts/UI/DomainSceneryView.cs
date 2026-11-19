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

        private void Awake()
        {
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
                    StartCoroutine(scenery.Sink());
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
                if (scenery != null)
                {
                    scenery.Place(floorY, OwnerPosition(active));
                    spawnedAt = Time.time;
                }
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
        // 주인이 화면에 없는 장면(다른 아군 차례)에서는 마지막 자리에 그대로 둔다.
        private void LateUpdate()
        {
            if (scenery == null || shownBoundary == null || stageView == null)
            {
                return;
            }

            UnitView view = stageView.FindView(shownBoundary.Owner);
            if (view != null && view.gameObject.activeInHierarchy)
            {
                scenery.Follow(view.AnchorPosition, view.transform.forward);
            }
        }

        // blend 0~1만큼 안개·배경·주변광을 영역 색으로 섞는다. 조금이라도 영역이면 원래 바닥을 숨긴다 (물바닥과 반사가 보이게).
        private void ApplyBlend(float blend)
        {
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

            if (battleFloor != null)
            {
                battleFloor.enabled = blend < 0.5f;
            }
        }
    }
}
