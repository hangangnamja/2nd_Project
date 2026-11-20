using UnityEngine;

namespace Game.UI
{
    // 적 머리 위의 작은 HP바 (임시 그림). 보스 HP바(BossHpBarView)를 작게 줄인 모양: 어두운 테두리 판 + 밝은 잔상 + 빨간 HP.
    // 상태효과 줄(UnitStatusBar) 바로 아래에 붙어, 상태효과 줄과 함께 카메라를 바라본다 (부모의 Billboard).
    // 사각형 면(Quad) 세 장을 겹쳐 그리고, 비율에 맞춰 가로 크기만 바꾼다.
    public class UnitHpBar : MonoBehaviour
    {
        // 바 크기 (월드 단위)와 색. 임시 값이라 코드에 둔다 (모양이 정해지면 프리팹으로 옮김).
        private const float Width = 1.5f;
        private const float Height = 0.13f;
        private const float Border = 0.035f;

        private static readonly Color BackColor = new Color(0.05f, 0.05f, 0.08f, 1f);
        private static readonly Color TrailColor = new Color(1f, 0.82f, 0.75f, 1f);
        private static readonly Color FillColor = new Color(0.86f, 0.13f, 0.1f, 1f);

        // 잔상이 줄어드는 속도 (초당 비율). 깎인 만큼 밝은 잔상이 천천히 따라 줄어 얼마나 맞았는지 보인다.
        private const float TrailSpeed = 0.6f;

        private Transform fill;
        private Transform trail;
        private float ratio = 1f;
        private float trailRatio = 1f;

        // 바를 만든다. material: 단색으로 그리는 머티리얼 (상태효과 블럭과 같은 것). 면마다 복사본을 써서 색을 따로 둔다.
        public void Build(Material material)
        {
            // 카메라는 이 오브젝트의 -z 쪽에 있으므로, z를 조금씩 빼서 위에 그릴 면일수록 카메라에 가깝게 둔다.
            CreateQuad("Back", material, BackColor, Width + Border * 2f, Height + Border * 2f, 0f);
            trail = CreateQuad("Trail", material, TrailColor, Width, Height, -0.005f);
            fill = CreateQuad("Fill", material, FillColor, Width, Height, -0.01f);
            Apply();
        }

        // HP 비율(0~1)을 정한다. 줄어들 때는 잔상이 남고, 늘어날 때(회복)는 잔상도 바로 따라 올라간다.
        // Mathf.Clamp01: 0~1 밖의 값을 잘라 낸다.
        public void SetRatio(float value)
        {
            ratio = Mathf.Clamp01(value);
            if (ratio > trailRatio)
            {
                trailRatio = ratio;
            }

            Apply();
        }

        // 잔상을 HP 쪽으로 천천히 줄인다. Mathf.MoveTowards(현재, 목표, 최대 변화량): 목표까지 최대 변화량만큼만 움직인다.
        private void Update()
        {
            if (trailRatio <= ratio)
            {
                return;
            }

            trailRatio = Mathf.MoveTowards(trailRatio, ratio, TrailSpeed * Time.deltaTime);
            Apply();
        }

        private void Apply()
        {
            if (fill == null)
            {
                return;
            }

            SetLeftAligned(fill, ratio);
            SetLeftAligned(trail, trailRatio);
        }

        // 왼쪽 끝을 고정하고 비율만큼 가로로 늘인다: 가로 크기 = 너비 × 비율, 가운데 = 왼쪽 끝 + 크기의 절반.
        // 비율이 0이면 면을 꺼서 얇은 선도 보이지 않게 한다.
        private static void SetLeftAligned(Transform bar, float barRatio)
        {
            bar.gameObject.SetActive(barRatio > 0f);
            float barWidth = Width * barRatio;
            Vector3 position = bar.localPosition;
            bar.localPosition = new Vector3(-Width * 0.5f + barWidth * 0.5f, position.y, position.z);
            bar.localScale = new Vector3(barWidth, Height, 1f);
        }

        // 사각형 면 하나를 만든다. CreatePrimitive(Quad): 1×1 사각형. 클릭은 상태효과 블럭·몸이 받으므로 콜라이더는 지운다.
        private Transform CreateQuad(string objectName, Material material, Color color, float width, float height, float zOffset)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = objectName;
            Destroy(quad.GetComponent<Collider>());

            quad.transform.SetParent(transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, zOffset);
            quad.transform.localScale = new Vector3(width, height, 1f);

            // renderer.material: 이 면 전용 복사본 → 색을 바꿔도 다른 면·블럭에 영향이 없다.
            Renderer quadRenderer = quad.GetComponent<Renderer>();
            quadRenderer.sharedMaterial = material;
            quadRenderer.material.color = color;
            return quad.transform;
        }
    }
}
