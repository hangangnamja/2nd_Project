using UnityEngine;

namespace Game.UI
{
    // 전투 배경 프리팹 맨 위에 붙이는 설정 (평원·제단·동굴 등). 프리팹 안의 지형·소품은 그대로 보이고,
    // 이 컴포넌트는 그 배경에 어울리는 화면 설정(하늘·안개·주변광·태양빛)을 들고 있다가 BattleBackgroundView가 전투 시작 때 적용한다.
    public class BattleBackground : MonoBehaviour
    {
        [Header("하늘")]
        [Tooltip("하늘 머티리얼 (Skybox). 비우면 아래 배경색 단색으로 칠한다 (실내·동굴)")]
        [SerializeField] private Material skybox;

        [Tooltip("하늘이 없을 때 카메라 배경색 (안개 색과 맞추면 끝이 자연스럽게 사라짐)")]
        [SerializeField] private Color backgroundColor = new Color(0.07f, 0.09f, 0.15f);

        [Header("안개 (선형: 시작 거리부터 끝 거리까지 점점 안개 색으로)")]
        [SerializeField] private Color fogColor = new Color(0.07f, 0.09f, 0.15f);
        [SerializeField, Min(0f)] private float fogStart = 14f;
        [SerializeField, Min(0f)] private float fogEnd = 38f;

        [Header("빛")]
        [Tooltip("그림자 쪽에도 깔리는 주변광 색 (단색)")]
        [SerializeField] private Color ambientColor = new Color(0.25f, 0.27f, 0.32f);

        [Tooltip("태양(전투 씬의 방향광) 색 · 세기 · 방향(오일러 각)")]
        [SerializeField] private Color sunColor = Color.white;
        [SerializeField, Min(0f)] private float sunIntensity = 1f;
        [SerializeField] private Vector3 sunRotation = new Vector3(50f, 330f, 0f);

        [Header("바닥")]
        [Tooltip("체크하면 전투 씬의 기본 격자 바닥을 숨긴다 (배경 프리팹에 자기 바닥이 있을 때)")]
        [SerializeField] private bool hidesDefaultFloor = true;

        public Material Skybox => skybox;
        public Color BackgroundColor => backgroundColor;
        public Color FogColor => fogColor;
        public float FogStart => fogStart;
        public float FogEnd => fogEnd;
        public Color AmbientColor => ambientColor;
        public Color SunColor => sunColor;
        public float SunIntensity => sunIntensity;
        public Vector3 SunRotation => sunRotation;
        public bool HidesDefaultFloor => hidesDefaultFloor;
    }
}
