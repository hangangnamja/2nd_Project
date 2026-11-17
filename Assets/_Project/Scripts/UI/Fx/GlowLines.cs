using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    // 연출용 빛 선 묶음 (거미줄·구속 선). 프리팹에 둔 견본 LineRenderer(재질·색·굵기)를 복제해 선을 만들고,
    // 밝기·투명도·굵기를 묶음 전체에 한꺼번에 바꾼다. 선의 모양(재질 HDR 밝기 + Bloom)은 견본이 정하고, 코드는 점 위치와 세기만 바꾼다.
    public class GlowLines
    {
        private readonly LineRenderer template;
        private readonly Transform parent;
        private readonly List<LineRenderer> lines = new List<LineRenderer>();

        // MaterialPropertyBlock: 재질 에셋을 복제하지 않고 렌더러마다 재질 값(여기서는 _BaseColor 밝기)만 덮어쓰는 값 묶음.
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly Color materialColor;
        private readonly Gradient tint;
        private readonly float baseWidth;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // template: 견본 선 (프리팹 안에 꺼 둔 자식). parent: 만든 선을 둘 부모 (연출 오브젝트와 함께 지워진다).
        public GlowLines(LineRenderer template, Transform parent)
        {
            this.template = template;
            this.parent = parent;
            materialColor = template.sharedMaterial != null && template.sharedMaterial.HasProperty(BaseColorId)
                ? template.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
            tint = template.colorGradient;
            baseWidth = template.widthMultiplier;
        }

        public IReadOnlyList<LineRenderer> Lines => lines;

        // 점 pointCount개짜리 선 하나를 만든다. 월드 좌표로 점을 찍는다 (useWorldSpace).
        public LineRenderer Create(int pointCount, bool loop = false)
        {
            LineRenderer line = Object.Instantiate(template, parent);
            line.gameObject.SetActive(true);
            line.useWorldSpace = true;
            line.loop = loop;
            line.positionCount = pointCount;
            lines.Add(line);
            return line;
        }

        // 밝기 배율: 재질 색(HDR) × intensity를 모든 선에 덮어쓴다. 1보다 크면 Bloom으로 더 번진다.
        public void SetIntensity(float intensity)
        {
            block.SetColor(BaseColorId, materialColor * intensity);
            foreach (LineRenderer line in lines)
            {
                line.SetPropertyBlock(block);
            }
        }

        // 투명도: 견본 색(그라데이션)은 그대로 두고 알파만 줄인다 (사라질 때).
        public void SetAlpha(float alpha)
        {
            Gradient faded = Faded(tint, alpha);
            foreach (LineRenderer line in lines)
            {
                line.colorGradient = faded;
            }
        }

        // 그라데이션의 색은 그대로 두고 알파 키마다 alpha를 곱한 새 그라데이션.
        public static Gradient Faded(Gradient source, float alpha)
        {
            GradientAlphaKey[] alphaKeys = source.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha *= alpha;
            }

            var faded = new Gradient();
            faded.SetKeys(source.colorKeys, alphaKeys);
            return faded;
        }

        // 굵기 배율: 견본 굵기 × scale.
        public void SetWidthScale(float scale)
        {
            foreach (LineRenderer line in lines)
            {
                line.widthMultiplier = baseWidth * scale;
            }
        }

        // from → to를 segments칸으로 나눠, 가운데 점들을 진행 방향에 수직으로 무작위로 비튼 지그재그(번개) 선으로 그린다.
        // 비트는 폭은 sin(진행도 × π)라 양 끝은 제자리, 가운데가 가장 크게 튄다.
        // Vector3.ProjectOnPlane(무작위 방향, 진행 방향): 진행 방향 성분을 빼 수직 방향만 남긴다.
        public static void SetZigzag(LineRenderer line, Vector3 from, Vector3 to, int segments, float amplitude)
        {
            line.positionCount = segments + 1;
            Vector3 direction = to - from;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 jitter = Vector3.ProjectOnPlane(Random.onUnitSphere, direction).normalized * amplitude * Mathf.Sin(t * Mathf.PI);
                line.SetPosition(i, Vector3.Lerp(from, to, t) + (i == 0 || i == segments ? Vector3.zero : jitter * Random.Range(0.3f, 1f)));
            }
        }

        // 점 목록의 앞쪽 t(0~1)만큼만 선으로 그린다. 마지막 점은 다음 점 쪽으로 보간해 끊김 없이 이어지게 한다.
        public static void DrawPartial(LineRenderer line, Vector3[] points, float t)
        {
            float exact = t * (points.Length - 1);
            int whole = Mathf.FloorToInt(exact);
            int count = Mathf.Min(whole + 2, points.Length);

            line.positionCount = count;
            for (int i = 0; i < count - 1; i++)
            {
                line.SetPosition(i, points[i]);
            }

            int last = count - 1;
            line.SetPosition(last, last <= whole ? points[last] : Vector3.Lerp(points[whole], points[last], exact - whole));
        }
    }
}
