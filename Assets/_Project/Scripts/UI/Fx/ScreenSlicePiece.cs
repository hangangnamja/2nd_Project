using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 베어 낸 화면 조각 하나 (반전 참격 연출). 찍어 둔 화면 그림 중 다각형 모양만큼을 UI로 그린다.
    // 다각형 꼭짓점은 화면 픽셀 좌표이고, 같은 좌표를 화면 크기로 나눈 값을 그림 좌표(UV)로 써서 원래 자리의 그림이 그대로 보인다.
    // 조각을 움직이면(anchoredPosition) 그 그림이 통째로 어긋나 보인다 → 화면이 베여 미끄러지는 느낌.
    // RequireComponent: 이 컴포넌트를 붙일 때 UI를 그리는 CanvasRenderer도 반드시 함께 붙게 한다 (없으면 조각이 보이지 않음).
    [RequireComponent(typeof(CanvasRenderer))]
    public class ScreenSlicePiece : MaskableGraphic
    {
        private Texture texture;
        private readonly List<Vector2> points = new List<Vector2>();

        // 이 Graphic이 쓸 그림 (찍어 둔 화면). 없으면 흰색.
        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        // 조각 모양과 그림을 정한다. 꼭짓점은 화면 픽셀 좌표 (캔버스 왼쪽 아래 = 0, 0).
        public void Set(Texture screen, IReadOnlyList<Vector2> polygon)
        {
            texture = screen;
            points.Clear();
            points.AddRange(polygon);
            SetAllDirty();
        }

        // 조각의 화면 무게중심 (어느 선의 어느 쪽에 있는지 판정용).
        public Vector2 Centroid
        {
            get
            {
                Vector2 sum = Vector2.zero;
                foreach (Vector2 point in points)
                {
                    sum += point;
                }

                return points.Count > 0 ? sum / points.Count : Vector2.zero;
            }
        }

        // UI 메시를 만든다: 다각형을 첫 꼭짓점 기준 부채꼴(삼각형 팬)으로 나눈다 (조각은 볼록 다각형이라 팬으로 충분).
        // UV = 픽셀 좌표 ÷ 화면 크기 → 0~1 그림 좌표. color(투명도 포함)는 모든 꼭짓점에 같이 넣어 조각 전체를 흐리게 할 수 있다.
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (points.Count < 3)
            {
                return;
            }

            var size = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));
            foreach (Vector2 point in points)
            {
                vh.AddVert(point, color, new Vector2(point.x / size.x, point.y / size.y));
            }

            for (int i = 1; i < points.Count - 1; i++)
            {
                vh.AddTriangle(0, i, i + 1);
            }
        }

        // 볼록 다각형을 직선(점 origin을 지나는 방향 direction) 하나로 둘로 자른다.
        // 각 꼭짓점의 "선에서 어느 쪽인지" = 2D 외적 부호 (direction × (꼭짓점 - origin)). 부호가 바뀌는 변에서 교점을 넣는다.
        // 한쪽이 비면(선이 다각형을 지나지 않음) 그쪽 목록은 꼭짓점 3개 미만이라 부른 쪽이 버린다.
        public static void Split(IReadOnlyList<Vector2> polygon, Vector2 origin, Vector2 direction, List<Vector2> left, List<Vector2> right)
        {
            left.Clear();
            right.Clear();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                float sideA = Cross(direction, a - origin);
                float sideB = Cross(direction, b - origin);

                (sideA >= 0f ? left : right).Add(a);

                // 변이 선을 가로지르면(부호가 다르면) 교점을 양쪽 모두에 넣는다. t = 교점이 a→b의 어디쯤인지.
                if ((sideA >= 0f) != (sideB >= 0f))
                {
                    float t = sideA / (sideA - sideB);
                    Vector2 hit = Vector2.Lerp(a, b, t);
                    left.Add(hit);
                    right.Add(hit);
                }
            }
        }

        // 2D 외적 (z 성분): 양수면 point가 direction의 왼쪽, 음수면 오른쪽.
        public static float Cross(Vector2 direction, Vector2 point)
        {
            return direction.x * point.y - direction.y * point.x;
        }
    }
}
