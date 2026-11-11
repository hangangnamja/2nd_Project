using UnityEngine;

namespace Game.UI
{
    // 전장 이펙트의 띠(악보 등)를 흐르게 하고, 처음 나타날 때 서서히 밝아지게 한다.
    // 모양·빛은 텍스처와 머티리얼(HDR 색 + Bloom)이 맡고, 이 컴포넌트는 텍스처를 옆으로 미는 시간 흐름만 담당한다.
    [RequireComponent(typeof(Renderer))]
    public class FieldFxScroller : MonoBehaviour
    {
        [Tooltip("초당 텍스처가 흘러가는 양 (1 = 텍스처 한 장 길이)")]
        [SerializeField] private Vector2 scrollSpeed = new Vector2(-0.15f, 0f);

        [Tooltip("나타날 때 0에서 원래 밝기까지 걸리는 시간(초)")]
        [SerializeField, Min(0.01f)] private float fadeInTime = 0.6f;

        private Material material;
        private Color baseColor;
        private float elapsed;

        // _BaseColor: URP 파티클·Unlit 셰이더의 색(밝기 포함). 이 값의 알파·밝기로 서서히 나타나게 한다.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            // renderer.material: 이 오브젝트 전용 머티리얼 복사본 → 텍스처를 밀어도 다른 띠에 영향이 없다.
            material = GetComponent<Renderer>().material;
            baseColor = material.GetColor(BaseColorId);
        }

        // 매 프레임: 텍스처 위치(mainTextureOffset)를 속도만큼 옮겨 흐르는 것처럼 보이게 하고, 처음 fadeInTime 동안 밝기를 올린다.
        private void Update()
        {
            elapsed += Time.deltaTime;
            material.mainTextureOffset += scrollSpeed * Time.deltaTime;

            // Mathf.Clamp01: 0~1로 자른다 → 시간이 지나면 1에서 멈춘다.
            float fade = Mathf.Clamp01(elapsed / fadeInTime);
            material.SetColor(BaseColorId, baseColor * fade);
        }

        private void OnDestroy()
        {
            // 복사해 만든 머티리얼은 직접 지워야 메모리에 남지 않는다.
            Destroy(material);
        }
    }
}
