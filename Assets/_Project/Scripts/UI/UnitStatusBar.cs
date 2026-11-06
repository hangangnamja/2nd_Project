using System.Collections.Generic;
using Game.Battle.Status;
using UnityEngine;

namespace Game.UI
{
    // 유닛 머리 위에 걸린 상태효과를 작은 색 블럭 + 남은 턴 숫자로 표시한다 (테스트용).
    // 효과 개수가 계속 바뀌므로 블럭을 미리 만들어 두지 않고, 필요한 만큼 만들어 재사용한다.
    public class UnitStatusBar : MonoBehaviour
    {
        [SerializeField] private Material iconMaterial;
        [SerializeField] private Font font;

        [Header("배치")]
        [SerializeField, Min(0.01f)] private float iconSize = 0.22f;
        [SerializeField, Min(0.01f)] private float iconSpacing = 0.28f;

        [Tooltip("턴 숫자 글자 크기 (TextMesh characterSize)")]
        [SerializeField, Min(0.001f)] private float turnTextSize = 0.035f;

        // 블럭 하나를 이루는 오브젝트 묶음 (블럭 본체, 그 밑의 턴 숫자).
        private class StatusIcon
        {
            public GameObject Root;
            public Renderer Block;
            public TextMesh TurnText;
        }

        private readonly List<StatusIcon> icons = new List<StatusIcon>();

        // 상태효과 목록을 받아 블럭을 가운데 정렬로 늘어놓고, 블럭 색과 남은 턴을 갱신한다.
        public void Show(IReadOnlyList<StatusInstance> statuses)
        {
            EnsureIconCount(statuses.Count);

            // 블럭 묶음 전체의 가운데가 x = 0에 오도록 첫 블럭 위치를 왼쪽으로 당긴다.
            // 예: 3개, 간격 0.28 → 시작 x = -0.28 → -0.28, 0, 0.28
            float startX = -(statuses.Count - 1) * iconSpacing * 0.5f;

            for (int i = 0; i < icons.Count; i++)
            {
                bool isUsed = i < statuses.Count;
                icons[i].Root.SetActive(isUsed);

                if (isUsed)
                {
                    ApplyIcon(icons[i], statuses[i], new Vector3(startX + i * iconSpacing, 0f, 0f));
                }
            }
        }

        // 블럭 하나의 위치, 색, 턴 숫자를 상태효과에 맞춘다.
        private static void ApplyIcon(StatusIcon icon, StatusInstance status, Vector3 localPosition)
        {
            icon.Root.transform.localPosition = localPosition;

            // material: 블럭마다 전용 머티리얼 복사본을 쓰므로 색을 바꿔도 다른 블럭에 영향이 없다.
            icon.Block.material.color = status.Data.IconColor;
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

        // 블럭(사각형 면) 하나와 그 밑의 턴 숫자를 만든다.
        private StatusIcon CreateIcon()
        {
            var root = new GameObject("StatusIcon");
            root.transform.SetParent(transform, false);

            // CreatePrimitive(Quad): 1×1 크기의 사각형 면을 만든다. 클릭 판정을 막지 않도록 콜라이더는 지운다.
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(block.GetComponent<Collider>());
            block.transform.SetParent(root.transform, false);
            block.transform.localScale = new Vector3(iconSize, iconSize, 1f);

            Renderer blockRenderer = block.GetComponent<Renderer>();
            blockRenderer.sharedMaterial = iconMaterial;

            return new StatusIcon
            {
                Root = root,
                Block = blockRenderer,
                TurnText = CreateTurnText(root.transform),
            };
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
