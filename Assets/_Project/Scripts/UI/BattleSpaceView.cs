using Game.Battle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.UI
{
    // 하이퍼캐리 변신 중 "다른 공간으로 넘어간" 느낌을 주는 전장 색 연출.
    // 변신한 아군이 있으면 배경·조명·주변광을 변신 공간 색으로 서서히 바꾸고, 해제되면 원래대로 되돌린다.
    // 전투 로직은 건드리지 않고 BattleManager의 상태 변경 알림만 보고 판단한다 (표시 전용).
    public class BattleSpaceView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light mainLight;

        [Header("변신 공간 색")]
        [SerializeField] private Color spaceBackgroundColor = new Color(0.12f, 0.02f, 0.16f);
        [SerializeField] private Color spaceLightColor = new Color(1f, 0.35f, 0.55f);
        [SerializeField] private Color spaceAmbientColor = new Color(0.35f, 0.1f, 0.3f);

        [Tooltip("색이 바뀌는 데 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float transitionSeconds = 0.6f;

        // 전투 시작 전의 원래 설정. 변신이 끝나면 이 값으로 돌아간다.
        private CameraClearFlags originalClearFlags;
        private Color originalBackgroundColor;
        private Color originalLightColor;
        private AmbientMode originalAmbientMode;
        private Color originalAmbientColor;

        // 0 = 원래 공간, 1 = 변신 공간. Update에서 목표값(target)을 향해 조금씩 움직여 색이 부드럽게 바뀐다.
        private float blend;
        private float target;

        // 원래 화면 설정을 기억해 둔다.
        private void Awake()
        {
            originalClearFlags = targetCamera.clearFlags;
            originalBackgroundColor = targetCamera.backgroundColor;
            originalLightColor = mainLight.color;
            originalAmbientMode = RenderSettings.ambientMode;
            originalAmbientColor = RenderSettings.ambientLight;
        }

        private void OnEnable()
        {
            battleManager.StateChanged += HandleStateChanged;
        }

        // 구독을 풀고 화면 설정을 원래대로 돌려놓는다 (RenderSettings는 씬 전체 설정이라 남지 않게 한다).
        private void OnDisable()
        {
            battleManager.StateChanged -= HandleStateChanged;
            blend = 0f;
            target = 0f;
            ApplyBlend();
        }

        // 전투 상태가 바뀔 때마다 변신한 아군이 있는지 보고 목표 공간을 정한다.
        private void HandleStateChanged()
        {
            target = HasTransformedAlly() ? 1f : 0f;
        }

        // 목표값까지 blend를 옮기고 색을 적용한다. 이미 도착했으면 아무것도 하지 않는다.
        private void Update()
        {
            if (Mathf.Approximately(blend, target))
            {
                return;
            }

            // Mathf.MoveTowards(현재, 목표, 최대 이동량): 목표를 넘지 않게 한 프레임 분량만 움직인다.
            // Time.deltaTime / transitionSeconds: 전환 시간 동안 0 → 1을 정확히 한 번 지나가도록 한 프레임 비율.
            blend = Mathf.MoveTowards(blend, target, Time.deltaTime / transitionSeconds);
            ApplyBlend();
        }

        // blend 값에 맞춰 배경·조명·주변광 색을 섞어 적용한다.
        // Color.Lerp(a, b, t): t = 0이면 a, 1이면 b, 그 사이면 두 색을 비율대로 섞은 색.
        private void ApplyBlend()
        {
            bool isOriginal = blend <= 0f;

            // 플레이를 끄거나 씬을 닫을 때는 카메라·조명이 이 컴포넌트보다 먼저 파괴될 수 있다.
            // Unity 오브젝트는 파괴되면 == null이 true가 되므로, 이미 사라진 것은 건드리지 않는다
            // (안 그러면 OnDisable의 원래 색 복구에서 MissingReferenceException이 난다).
            if (targetCamera != null)
            {
                // 하늘(Skybox)은 색을 섞을 수 없으므로, 전환이 시작되면 단색 배경으로 바꾸고
                // 원래 배경색에서 변신 공간 색으로 섞는다. 완전히 돌아오면 원래 하늘로 복구한다.
                targetCamera.clearFlags = isOriginal ? originalClearFlags : CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = Color.Lerp(originalBackgroundColor, spaceBackgroundColor, blend);
            }

            if (mainLight != null)
            {
                mainLight.color = Color.Lerp(originalLightColor, spaceLightColor, blend);
            }

            RenderSettings.ambientMode = isOriginal ? originalAmbientMode : AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(originalAmbientColor, spaceAmbientColor, blend);
        }

        // 변신 중인 아군이 하나라도 있는지.
        private bool HasTransformedAlly()
        {
            foreach (BattleUnit ally in battleManager.Allies)
            {
                if (ally.IsTransformed)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
