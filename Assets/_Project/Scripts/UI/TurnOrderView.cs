using System.Collections.Generic;
using Game.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 좌상단 행동 순서 표시 (Canvas, 기획서 3-11). 맨 위가 지금 행동 중인 유닛이고, 아래로 다음 행동 순서다.
    // 칸마다 이름과 행동 게이지(%)를 보여준다. 칸 모양(크기·글꼴·색)은 씬의 템플릿 오브젝트에서 조절한다.
    //
    // 움직이는 순서 바: 칸을 "유닛 + 몇 번째 등장"으로 기억해 두고, 순서가 바뀌면 그 칸이 새 자리로 미끄러져 간다.
    //  - 차례가 지나가면 전체가 한 칸씩 위로 올라가고, 맨 위 칸은 왼쪽으로 빠지며 사라진다
    //  - 행동 당기기(강탈 딜러 버프 가속 등)로 순서가 앞당겨지면 그 칸이 위로 휙 올라가며 "+30%"가 뜬다
    // 레이아웃 그룹이 자리를 정하면 움직임을 줄 수 없으므로, 자리는 이 컴포넌트가 직접 계산한다.
    public class TurnOrderView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        [Tooltip("칸들을 담을 부모. 위에서부터 칸 높이 + 간격으로 쌓는다 (레이아웃 그룹은 시작할 때 끈다)")]
        [SerializeField] private RectTransform container;

        [Tooltip("칸 하나의 템플릿 (Image 배경 + 자식 Text). 씬에서는 꺼 두고, 복제해서 쓴다")]
        [SerializeField] private Image entryTemplate;

        [Tooltip("앞으로 몇 번의 행동까지 보여줄지 (지금 행동 포함)")]
        [SerializeField, Min(1)] private int visibleCount = 8;

        [Header("칸 색")]
        [SerializeField] private Color currentColor = new Color(1f, 0.85f, 0.2f, 0.95f);
        [SerializeField] private Color allyColor = new Color(0.2f, 0.45f, 0.95f, 0.85f);
        [SerializeField] private Color enemyColor = new Color(0.85f, 0.25f, 0.2f, 0.85f);

        [Header("움직임")]
        [Tooltip("칸 사이 간격 (픽셀)")]
        [SerializeField] private float spacing = 4f;

        [Tooltip("칸이 새 자리로 따라가는 빠르기 (클수록 빨리 도착)")]
        [SerializeField, Min(0.1f)] private float moveSpeed = 10f;

        [Tooltip("행동 당기기 때 칸 옆에 뜨는 글자 색")]
        [SerializeField] private Color advanceColor = new Color(0.55f, 1f, 0.6f);

        [Tooltip("행동 당기기 글자가 떠 있는 시간(초)")]
        [SerializeField, Min(0.1f)] private float advanceTextSeconds = 1.2f;

        // 화면에 있는 칸 하나: 누구의 몇 번째 차례인지, 지금·목표 위치, 사라지는 중인지, 당기기 글자.
        private class Entry
        {
            public BattleUnit Unit;
            public int Occurrence;
            public Image Image;
            public RectTransform Rect;
            public CanvasGroup Group;
            public Text Label;
            public Vector2 Target;
            public bool IsLeaving;
            public string AdvanceText;
            public float AdvanceTimer;
        }

        private readonly List<Entry> entries = new List<Entry>();

        // 쓰지 않는 칸 보관함 (사라진 칸을 지우지 않고 다시 써서 매번 새로 만들지 않는다).
        private readonly Stack<Entry> pool = new Stack<Entry>();

        // 유닛마다 지난번에 본 "누적 행동 당기기" 값. 늘어난 만큼이 이번에 당겨진 양이다.
        private readonly Dictionary<BattleUnit, double> seenAdvance = new Dictionary<BattleUnit, double>();

        private float entryHeight;

        private void Awake()
        {
            // 자리를 직접 계산하므로 레이아웃 그룹·크기 맞춤을 끈다 (켜져 있으면 매 프레임 자리를 덮어씀).
            // GetComponent<LayoutGroup>: VerticalLayoutGroup 등 레이아웃 그룹 공통 부모 타입으로 찾는다.
            var layout = container.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                layout.enabled = false;
            }

            var fitter = container.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.enabled = false;
            }

            entryHeight = entryTemplate.rectTransform.rect.height;
            entryTemplate.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            battleManager.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            battleManager.StateChanged -= Refresh;
        }

        // 행동 순서를 다시 계산해 칸마다 새 목표 자리를 정한다. 실제 이동은 Update에서 부드럽게 한다.
        private void Refresh()
        {
            IReadOnlyList<BattleUnit> order = battleManager.PredictActionOrder(visibleCount);
            Dictionary<BattleUnit, string> advanced = CollectAdvances(order);

            // 지금 칸들 중 이번 순서에 남는 칸을 표시하기 위한 목록. 남지 않은 칸은 사라지게 한다.
            var kept = new HashSet<Entry>();

            // 같은 유닛이 여러 번 나오면 몇 번째 등장인지로 칸을 구분한다 (빠른 유닛은 목록에 2번 나올 수 있음).
            var occurrences = new Dictionary<BattleUnit, int>();

            for (int i = 0; i < order.Count; i++)
            {
                BattleUnit unit = order[i];

                // TryGetValue로 지금까지 등장 횟수를 꺼내고, 이번 칸을 센 뒤 다시 넣는다.
                occurrences.TryGetValue(unit, out int occurrence);
                occurrences[unit] = occurrence + 1;

                Entry entry = FindOrCreate(unit, occurrence, i);
                kept.Add(entry);
                entry.Target = SlotPosition(i);

                // 같은 유닛은 처음 나온 칸에만 게이지와 당기기 글자를 보여준다.
                bool isFirst = occurrence == 0;
                if (isFirst && advanced.TryGetValue(unit, out string text))
                {
                    entry.AdvanceText = text;
                    entry.AdvanceTimer = advanceTextSeconds;
                }

                ApplyEntry(entry, isCurrent: i == 0, showGauge: isFirst);
            }

            // 이번 순서에 없는 칸 = 방금 행동을 마친 유닛(맨 위), 쓰러진·퇴장한 유닛 → 왼쪽으로 빠지며 사라진다.
            foreach (Entry entry in entries)
            {
                if (!kept.Contains(entry) && !entry.IsLeaving)
                {
                    entry.IsLeaving = true;
                    entry.Target = entry.Rect.anchoredPosition + new Vector2(-entry.Rect.rect.width, 0f);
                }
            }
        }

        // 지난번 이후 행동 당기기를 받은 유닛과 표시할 글자("+30%")를 모은다.
        private Dictionary<BattleUnit, string> CollectAdvances(IReadOnlyList<BattleUnit> order)
        {
            var result = new Dictionary<BattleUnit, string>();
            foreach (BattleUnit unit in order)
            {
                if (result.ContainsKey(unit))
                {
                    continue;
                }

                seenAdvance.TryGetValue(unit, out double seen);
                double gained = unit.TotalActionAdvanced - seen;
                seenAdvance[unit] = unit.TotalActionAdvanced;

                // 처음 보는 유닛(seen 0)이어도 이미 당겨진 기록이 있으면 표시된다. 아주 작은 오차는 무시한다.
                if (gained > 0.001d)
                {
                    result[unit] = $"+{gained * 100d:0}%";
                }
            }

            return result;
        }

        // 이미 있는 칸이면 돌려주고, 없으면 새 칸을 목록 아래(slot보다 한 칸 아래)에서 나타나게 만든다.
        private Entry FindOrCreate(BattleUnit unit, int occurrence, int slot)
        {
            Entry found = entries.Find(e => e.Unit == unit && e.Occurrence == occurrence && !e.IsLeaving);
            if (found != null)
            {
                return found;
            }

            Entry entry = pool.Count > 0 ? pool.Pop() : CreateEntry();
            entry.Unit = unit;
            entry.Occurrence = occurrence;
            entry.IsLeaving = false;
            entry.AdvanceText = null;
            entry.AdvanceTimer = 0f;
            entry.Image.gameObject.SetActive(true);
            entry.Group.alpha = 0f;

            // 새 칸은 자기 자리보다 한 칸 아래에서 시작해 올라오며 나타난다.
            entry.Rect.anchoredPosition = SlotPosition(slot + 1);
            entries.Add(entry);
            return entry;
        }

        private Entry CreateEntry()
        {
            Image image = Instantiate(entryTemplate, container);
            image.name = $"Entry{entries.Count + pool.Count}";

            // 템플릿 기준점을 위쪽 왼쪽으로 맞춰, 자리 = 위에서부터 아래로 내려가는 값이 되게 한다.
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            // CanvasGroup: 칸과 글자를 한꺼번에 투명하게 만든다 (나타나기·사라지기).
            CanvasGroup group = image.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = image.gameObject.AddComponent<CanvasGroup>();
            }

            return new Entry
            {
                Image = image,
                Rect = rect,
                Group = group,
                Label = image.GetComponentInChildren<Text>(true),
            };
        }

        // i번째 칸의 자리: 위에서부터 (칸 높이 + 간격) × i만큼 아래.
        private Vector2 SlotPosition(int index)
        {
            return new Vector2(0f, -(entryHeight + spacing) * index);
        }

        // 칸 하나에 이름·게이지·색을 채운다. 맨 위 칸(지금 행동 중)은 강조색.
        private void ApplyEntry(Entry entry, bool isCurrent, bool showGauge)
        {
            BattleUnit unit = entry.Unit;
            entry.Image.color = isCurrent ? currentColor : unit.IsAlly ? allyColor : enemyColor;

            // 행동 게이지 % = 트랙을 얼마나 달렸는지. 남은 거리 10000 → 0%, 0(도착) → 100%.
            // Mathf.Clamp01: 0~1 범위로 자른다 (행동 당기기로 남은 거리가 0이면 100%).
            float gauge = Mathf.Clamp01(1f - (float)(unit.RemainingDistance / TurnScheduler.TrackLength));
            string gaugeText = isCurrent ? "행동 중" : showGauge ? $"{gauge * 100f:0}%" : string.Empty;
            entry.Label.text = $"{unit.Name}  {gaugeText}";
        }

        // 매 프레임: 칸을 목표 자리로 부드럽게 옮기고, 나타나기·사라지기·당기기 글자를 처리한다.
        // unscaledDeltaTime: 히트스톱·슬로모션과 상관없이 실제 시간으로 움직인다.
        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 1 - Exp(-속도 × 시간): 프레임 속도와 상관없이 같은 빠르기로 목표에 다가가는 비율.
            float follow = 1f - Mathf.Exp(-moveSpeed * dt);

            // 뒤에서부터 돈다: 다 사라진 칸을 목록에서 빼도 아직 돌지 않은 앞쪽 순서가 꼬이지 않는다.
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry entry = entries[i];
                entry.Rect.anchoredPosition = Vector2.Lerp(entry.Rect.anchoredPosition, entry.Target, follow);

                float targetAlpha = entry.IsLeaving ? 0f : 1f;
                entry.Group.alpha = Mathf.MoveTowards(entry.Group.alpha, targetAlpha, dt * 4f);

                if (entry.IsLeaving && entry.Group.alpha <= 0f)
                {
                    entry.Image.gameObject.SetActive(false);
                    entries.RemoveAt(i);
                    pool.Push(entry);
                    continue;
                }

                UpdateAdvanceText(entry, dt);
            }
        }

        // 당기기 글자: 떠 있는 동안 이름 뒤에 초록 글자로 붙인다. Rich Text(<color>)로 한 줄 안에서 색만 바꾼다.
        private void UpdateAdvanceText(Entry entry, float dt)
        {
            if (entry.AdvanceTimer <= 0f || string.IsNullOrEmpty(entry.AdvanceText))
            {
                return;
            }

            entry.AdvanceTimer -= dt;
            string baseText = entry.Label.text;
            int mark = baseText.IndexOf("  <color", System.StringComparison.Ordinal);
            if (mark >= 0)
            {
                baseText = baseText.Substring(0, mark);
            }

            // ColorUtility.ToHtmlStringRGB: 색을 "RRGGBB" 글자로 바꾼다 → <color=#RRGGBB>로 그 부분만 색칠.
            entry.Label.supportRichText = true;
            entry.Label.text = entry.AdvanceTimer > 0f
                ? $"{baseText}  <color=#{ColorUtility.ToHtmlStringRGB(advanceColor)}>{entry.AdvanceText}</color>"
                : baseText;
        }
    }
}
