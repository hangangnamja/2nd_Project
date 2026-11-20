using Game.Battle;
using Game.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.UI
{
    // 전투가 시작되면 스테이지의 전투 배경 프리팹(없으면 기본 배경)을 깔고, 그 배경의 하늘·안개·빛 설정을 화면에 적용한다.
    // 스테이지는 BattleManager.Start에서 정해지므로, 시작 알림(BattleStarted)을 받은 뒤에 깐다.
    // 영역 전개(흉신 마라)처럼 배경을 통째로 바꾸는 연출은 SetSceneryVisible로 지형을 잠시 숨긴다.
    public class BattleBackgroundView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Light sun;

        [Tooltip("전투 씬의 기본 격자 바닥 (배경이 자기 바닥을 가지면 숨김)")]
        [SerializeField] private Renderer defaultFloor;

        [Tooltip("스테이지에 전투 배경이 없을 때 쓸 배경 (평원)")]
        [SerializeField] private GameObject defaultBackground;

        private GameObject spawned;
        private bool hidesFloor;

        private void OnEnable()
        {
            battleManager.BattleStarted += HandleBattleStarted;
        }

        private void OnDisable()
        {
            battleManager.BattleStarted -= HandleBattleStarted;
        }

        // 스테이지에 전투 배경이 있으면 그것, 없으면 기본 배경 (Unity 오브젝트는 ?? 대신 != null로 비교한다).
        private void HandleBattleStarted()
        {
            StageData stage = battleManager.Stage;
            GameObject prefab = stage != null && stage.BattleBackground != null ? stage.BattleBackground : defaultBackground;
            if (prefab == null)
            {
                return;
            }

            spawned = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            BattleBackground settings = spawned.GetComponent<BattleBackground>();
            if (settings != null)
            {
                Apply(settings);
            }
        }

        // 배경의 화면 설정을 적용한다. RenderSettings: 씬 전체의 하늘·안개·주변광 설정.
        private void Apply(BattleBackground settings)
        {
            bool hasSky = settings.Skybox != null;
            RenderSettings.skybox = settings.Skybox;
            if (targetCamera != null)
            {
                targetCamera.clearFlags = hasSky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                targetCamera.backgroundColor = settings.BackgroundColor;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = settings.FogColor;
            RenderSettings.fogStartDistance = settings.FogStart;
            RenderSettings.fogEndDistance = settings.FogEnd;

            // 주변광은 단색(Flat)으로 정해 하늘 그림과 상관없이 배경마다 원하는 밝기로 맞춘다.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = settings.AmbientColor;

            if (sun != null)
            {
                sun.color = settings.SunColor;
                sun.intensity = settings.SunIntensity;
                sun.transform.rotation = Quaternion.Euler(settings.SunRotation);
            }

            hidesFloor = settings.HidesDefaultFloor;
            SetSceneryVisible(true);
        }

        // 배경 지형을 보이거나 숨긴다 (영역 전개처럼 전장을 다른 공간으로 덮는 연출이 부름).
        // 기본 바닥은 배경이 숨기라고 한 경우 계속 숨긴다.
        public void SetSceneryVisible(bool visible)
        {
            if (spawned != null)
            {
                spawned.SetActive(visible);
            }

            if (defaultFloor != null)
            {
                defaultFloor.enabled = visible && !hidesFloor;
            }
        }
    }
}
