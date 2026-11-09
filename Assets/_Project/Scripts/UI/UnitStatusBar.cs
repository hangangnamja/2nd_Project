using System.Collections.Generic;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;

namespace Game.UI
{
    // 유닛 머리 위에 걸린 상태효과를 작은 블럭 + 남은 턴 숫자로 표시한다 (테스트용).
    // 블럭 색은 분류로 정한다: 버프 = 파랑, 디버프 = 빨강. 상태효과에 그림(칼, 방패 등)이 있으면 블럭 위에 겹쳐 그린다.
    // 효과 개수가 계속 바뀌므로 블럭을 미리 만들어 두지 않고, 필요한 만큼 만들어 재사용한다.
    public class UnitStatusBar : MonoBehaviour
    {
        [Header("머티리얼")]
        [Tooltip("블럭 배경용 (단색)")]
        [SerializeField] private Material iconMaterial;

        [Tooltip("그림용 (투명한 부분을 잘라내는 머티리얼)")]
        [SerializeField] private Material glyphMaterial;

        [SerializeField] private Font font;

        [Header("분류별 색")]
        [SerializeField] private Color buffColor = new Color(0.2f, 0.45f, 0.95f);
        [SerializeField] private Color debuffColor = new Color(0.9f, 0.15f, 0.1f);

        [Header("그림 색")]
        [SerializeField] private Color glyphColor = Color.white;

        [Tooltip("개인 버프(StatusEffectData.IsPersonal)의 그림 색")]
        [SerializeField] private Color personalGlyphColor = Color.black;

        [Header("배치")]
        [SerializeField, Min(0.01f)] private float iconSize = 0.45f;
        [SerializeField, Min(0.01f)] private float iconSpacing = 0.5f;

        [Tooltip("한 줄에 놓을 최대 블럭 수. 넘치면 위쪽에 새 줄을 만든다")]
        [SerializeField, Min(1)] private int iconsPerRow = 5;

        [Tooltip("줄 사이 세로 간격 (블럭 + 턴 숫자 높이)")]
        [SerializeField, Min(0.01f)] private float rowSpacing = 0.7f;

        [Tooltip("블럭 대비 그림 크기 비율")]
        [SerializeField, Range(0.1f, 1f)] private float glyphScale = 0.8f;

        [Tooltip("턴 숫자 글자 크기 (TextMesh characterSize)")]
        [SerializeField, Min(0.001f)] private float turnTextSize = 0.035f;

        // 블럭 하나를 이루는 오브젝트 묶음 (배경 블럭, 그림, 턴 숫자).
        private class StatusIcon
        {
            public GameObject Root;
            public Renderer Block;
            public Renderer Glyph;
            public TextMesh TurnText;
        }

        private readonly List<StatusIcon> icons = new List<StatusIcon>();

        // 상태효과 목록을 받아 블럭을 줄마다 가운데 정렬로 늘어놓고, 색·그림·남은 턴을 갱신한다.
        // 블럭이 커서 한 줄에 다 놓으면 옆 유닛과 겹치므로, 한 줄 최대 개수를 넘으면 위로 줄을 쌓는다.
        public void Show(IReadOnlyList<StatusInstance> statuses)
        {
            EnsureIconCount(statuses.Count);

            for (int i = 0; i < icons.Count; i++)
            {
                bool isUsed = i < statuses.Count;
                icons[i].Root.SetActive(isUsed);

                if (isUsed)
                {
                    ApplyIcon(icons[i], statuses[i], GetIconPosition(i, statuses.Count));
                }
            }
        }

        // index번째 블럭의 위치. 줄 번호 = index / 한 줄 개수, 줄 안 순서 = index % 한 줄 개수.
        // 각 줄은 그 줄에 놓인 개수 기준으로 가운데 정렬한다.
        // 예: 7개, 한 줄 5개, 간격 0.5 → 0줄 5개(x = -1.0 ~ 1.0), 1줄 2개(x = -0.25, 0.25, 위로 rowSpacing만큼)
        private Vector3 GetIconPosition(int index, int totalCount)
        {
            int row = index / iconsPerRow;
            int column = index % iconsPerRow;

            // Mathf.Min: 마지막 줄은 남은 개수만큼만 놓이므로, 한 줄 최대 개수와 남은 개수 중 작은 값이 그 줄의 개수다.
            int countInRow = Mathf.Min(iconsPerRow, totalCount - row * iconsPerRow);
            float startX = -(countInRow - 1) * iconSpacing * 0.5f;

            return new Vector3(startX + column * iconSpacing, row * rowSpacing, 0f);
        }

        // 블럭 하나의 위치, 배경색, 그림, 턴 숫자를 상태효과에 맞춘다.
        private void ApplyIcon(StatusIcon icon, StatusInstance status, Vector3 localPosition)
        {
            icon.Root.transform.localPosition = localPosition;

            // material: 블럭마다 전용 머티리얼 복사본을 쓰므로 색·그림을 바꿔도 다른 블럭에 영향이 없다.
            icon.Block.material.color = status.Data.Category == StatusCategory.Buff ? buffColor : debuffColor;

            // 그림이 지정된 효과만 그림을 켠다. mainTexture는 URP Unlit의 기본 텍스처(_BaseMap) 칸이다.
            Texture2D glyphTexture = status.Data.Icon;
            icon.Glyph.gameObject.SetActive(glyphTexture != null);
            if (glyphTexture != null)
            {
                icon.Glyph.material.mainTexture = glyphTexture;

                // 그림은 흰색으로 그려져 있어, material.color(_BaseColor)를 곱하면 원하는 색이 된다.
                // 개인 버프는 검은색, 그 외는 흰색으로 그려 남이 걸어준 버프와 구분한다.
                icon.Glyph.material.color = status.Data.IsPersonal ? personalGlyphColor : glyphColor;
            }

            icon.TurnText.text = status.RemainingTurns.ToString();
        }

        // 필요한 개수만큼 블럭이 준비되어 있도록 부족한 만큼 새로 만든다.
        private void EnsureIconCount(int count)
        {
            while (icons.Count < count)
            {
                icons.Add(CreateIcon());
            }
        }

        // 배경 블럭, 그림, 턴 숫자로 이루어진 표시 하나를 만든다.
        private StatusIcon CreateIcon()
        {
            var root = new GameObject("StatusIcon");
            root.transform.SetParent(transform, false);

            return new StatusIcon
            {
                Root = root,
                Block = CreateQuad("Block", root.transform, iconSize, 0f, iconMaterial),

                // 카메라는 -z 쪽에 있으므로 z를 조금 빼서 그림이 배경보다 카메라에 가깝게(앞에) 보이게 한다.
                Glyph = CreateQuad("Glyph", root.transform, iconSize * glyphScale, -0.01f, glyphMaterial),
                TurnText = CreateTurnText(root.transform),
            };
        }

        // 사각형 면 하나를 만든다. 클릭 판정을 막지 않도록 콜라이더는 지운다.
        private static Renderer CreateQuad(string objectName, Transform parent, float size, float zOffset, Material material)
        {
            // CreatePrimitive(Quad): 1×1 크기의 사각형 면을 만든다.
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = objectName;
            Destroy(quad.GetComponent<Collider>());

            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = new Vector3(0f, 0f, zOffset);
            quad.transform.localScale = new Vector3(size, size, 1f);

            Renderer quadRenderer = quad.GetComponent<Renderer>();
            quadRenderer.sharedMaterial = material;
            return quadRenderer;
        }

        // 블럭 바로 밑에 남은 턴 숫자를 표시할 글자를 만든다.
        private TextMesh CreateTurnText(Transform parent)
        {
            var textObject = new GameObject("Turns");
            textObject.transform.SetParent(parent, false);

            // 블럭 아래쪽 가장자리(-iconSize / 2)에서 조금 더 아래에 글자의 위쪽이 오도록 놓는다.
            textObject.transform.localPosition = new Vector3(0f, -iconSize * 0.5f - 0.02f, 0f);

            var text = textObject.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 48;
            text.characterSize = turnTextSize;
            text.anchor = TextAnchor.UpperCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;

            // TextMesh는 글꼴의 머티리얼로 그려야 글자가 보인다.
            textObject.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return text;
        }
    }
}
