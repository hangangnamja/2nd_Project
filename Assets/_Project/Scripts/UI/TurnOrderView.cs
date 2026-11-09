using System.Collections.Generic;
using Game.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 좌상단 행동 순서 표시 (Canvas, 기획서 3-11). 맨 위가 지금 행동 중인 유닛이고, 아래로 다음 행동 순서다.
    // 칸마다 이름과 행동 게이지(%)를 보여준다. 초상화가 생기면 칸 템플릿에 이미지만 추가하면 된다.
    // 칸 모양(크기·글꼴·색)은 씬의 템플릿 오브젝트에서 조절하고, 이 컴포넌트는 내용만 채운다.
    public class TurnOrderView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        [Tooltip("칸들을 담을 부모 (VerticalLayoutGroup이 위에서부터 차례로 쌓는다)")]
        [SerializeField] private RectTransform container;

        [Tooltip("칸 하나의 템플릿 (Image 배경 + 자식 Text). 씬에서는 꺼 두고, 복제해서 쓴다")]
        [SerializeField] private Image entryTemplate;

        [Tooltip("앞으로 몇 번의 행동까지 보여줄지 (지금 행동 포함)")]
        [SerializeField, Min(1)] private int visibleCount = 8;

        [Header("칸 색")]
        [SerializeField] private Color currentColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        [SerializeField] private Color allyColor = new Color(0.2f, 0.45f, 0.95f, 0.85f);
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.25f, 0.2f, 0.85f);

        // 만들어 둔 칸들. 행동 순서 길이가 매번 같으므로 처음에 만들어 재사용한다.
        private readonly List<Image> entries = new List<Image>();

        private void OnEnable()
        {
            battleManager.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            battleManager.StateChanged -= Refresh;
        }

        // 행동 순서를 다시 계산해 칸을 채운다. 남는 칸은 끈다.
        private void Refresh()
        {
            IReadOnlyList<BattleUnit> order = battleManager.PredictActionOrder(visibleCount);
            EnsureEntryCount(order.Count);

            // 같은 유닛이 여러 번 나오면 게이지는 처음 나온 칸에만 표시한다 (뒤 칸은 그 다음 차례라 지금 게이지와 무관).
            var shown = new HashSet<BattleUnit>();

            for (int i = 0; i < entries.Count; i++)
            {
                bool isUsed = i < order.Count;
                entries[i].gameObject.SetActive(isUsed);

                if (isUsed)
                {
                    // HashSet.Add: 처음 넣으면 true, 이미 있으면 false → 처음 나온 칸인지 판단한다.
                    bool isFirstAppearance = shown.Add(order[i]);
                    ApplyEntry(entries[i], order[i], isCurrent: i == 0, showGauge: isFirstAppearance);
                }
            }
        }

        // 칸 하나에 이름·게이지·색을 채운다. 맨 위 칸(지금 행동 중)은 강조색.
        private void ApplyEntry(Image entry, BattleUnit unit, bool isCurrent, bool showGauge)
        {
            entry.color = isCurrent ? currentColor : unit.IsAlly ? allyColor : enemyColor;

            // 행동 게이지 % = 트랙을 얼마나 달렸는지. 남은 거리 10000 → 0%, 0(도착) → 100%.
            // Mathf.Clamp01: 0~1 범위로 자른다 (행동 당기기로 남은 거리가 0이면 100%).
            float gauge = Mathf.Clamp01(1f - (float)(unit.RemainingDistance / TurnScheduler.TrackLength));
            string gaugeText = isCurrent ? "행동 중" : showGauge ? $"{gauge * 100f:0}%" : string.Empty;

            Text label = entry.GetComponentInChildren<Text>(true);
            label.text = $"{unit.Name}  {gaugeText}";
        }

        // 필요한 개수만큼 템플릿을 복제해 칸을 준비한다.
        private void EnsureEntryCount(int count)
        {
            while (entries.Count < count)
            {
                Image entry = Instantiate(entryTemplate, container);
                entry.name = $"Entry{entries.Count}";
                entries.Add(entry);
            }
        }
    }
}
