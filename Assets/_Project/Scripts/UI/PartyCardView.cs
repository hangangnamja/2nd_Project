using System;
using System.Collections.Generic;
using Game.Battle;
using Game.Battle.Status;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    // 화면 아래 아군 초상화 카드 하나 (붕괴 스타레일 하단 바처럼). 아군이 장면마다 화면에 다 보이지 않으므로 HP·상태는 여기서 본다.
    //  - 일러스트 머리 부분 + 이름 + HP바·숫자
    //  - 지금 차례면 밝은 테두리, 지금 고른 스킬로 고를 수 있으면 하늘색 고리(누르면 그 아군을 대상으로 선택)
    //  - 쓰러졌거나 퇴장했으면 어둡게
    //  - 카드 아래에 버프 줄·디버프 줄을 나눠, 줄마다 작은 아이콘 최대 5개, 넘치면 "…"
    //  - 꾹 누르면(길게 누르기) LongPressed를 알려 상세 창(스탯·버프·디버프)을 연다
    // IPointerDownHandler / IPointerUpHandler / IPointerExitHandler: UI 이벤트 시스템이 누름·뗌·벗어남 때 불러 주는 함수.
    public class PartyCardView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        // 카드 아래 상태효과 아이콘 한 칸: 바탕(버프 파랑·디버프 빨강) + 그림 + 남은 턴
        [Serializable]
        public class StatusSlot
        {
            public Image background;
            public RawImage glyph;
            public Text turns;
        }

        [SerializeField] private Button button;

        [Tooltip("일러스트 머리 부분 (없으면 직군 색 판)")]
        [SerializeField] private IllustrationView portrait;
        [SerializeField] private Image portraitFallback;

        [SerializeField] private Text nameText;

        [Tooltip("HP 비율만큼 채워지는 막대 (Image Type = Filled, 가로)")]
        [SerializeField] private Image hpFill;
        [SerializeField] private Text hpText;

        [Tooltip("보호막이 있을 때 HP바를 감싸는 막대 (HP바보다 조금 크게, Image Type = Filled, 가로). 보호막 양 / 최대 HP만큼 채워진다")]
        [Label("보호막 막대")]
        [SerializeField] private Image shieldFill;

        [Tooltip("보호막이 있을 때 HP 숫자 대신 보호막 숫자를 보여줄 글자 색")]
        [Label("보호막 숫자 색")]
        [SerializeField] private Color shieldTextColor = new Color(0.75f, 0.9f, 1f);

        [SerializeField] private GameObject actingFrame;
        [SerializeField] private GameObject targetMark;

        [Tooltip("쓰러짐·퇴장 때 카드 전체를 어둡게 하는 묶음")]
        [SerializeField] private CanvasGroup group;

        [Header("상태효과 아이콘 (카드 아래, 버프 줄 / 디버프 줄)")]
        [Tooltip("버프 줄: 왼쪽부터 채울 아이콘 칸 (5칸)")]
        [SerializeField] private List<StatusSlot> buffSlots = new List<StatusSlot>();

        [Tooltip("버프가 칸보다 많을 때 보여줄 \"…\" 표시")]
        [SerializeField] private GameObject buffMore;

        [Tooltip("디버프 줄: 왼쪽부터 채울 아이콘 칸 (5칸)")]
        [SerializeField] private List<StatusSlot> debuffSlots = new List<StatusSlot>();

        [Tooltip("디버프가 칸보다 많을 때 보여줄 \"…\" 표시")]
        [SerializeField] private GameObject debuffMore;

        [SerializeField] private Color buffColor = new Color(0.2f, 0.45f, 0.95f);
        [SerializeField] private Color debuffColor = new Color(0.9f, 0.15f, 0.1f);

        [Tooltip("꾹 누르기로 보는 시간(초)")]
        [Label("길게 누르기 시간", Min = 0.1f)]
        [SerializeField] private float longPressSeconds = 0.45f;

        [Header("직군 색 (일러스트가 없을 때)")]
        [SerializeField] private Color attackColor = new Color(0.85f, 0.35f, 0.3f);
        [SerializeField] private Color supportColor = new Color(0.95f, 0.8f, 0.3f);
        [SerializeField] private Color defenseColor = new Color(0.35f, 0.55f, 0.9f);
        [SerializeField] private Color healerColor = new Color(0.4f, 0.8f, 0.45f);

        public BattleUnit Unit { get; private set; }
        public Button Button => button;

        // 꾹 눌렀을 때 알린다 (PartyBarView가 상세 창을 연다). event Action<카드>: 구독한 함수에 이 카드를 넘겨 부른다.
        public event Action<PartyCardView> LongPressed;

        // 보호막이 없을 때의 HP 숫자 색 (Bind에서 씬 값을 기억).
        private Color hpTextColor = Color.white;

        // 누르고 있는 시간. 음수면 누르고 있지 않음.
        private float pressTime = -1f;

        // 이번 누름이 길게 누르기로 처리됐는지. 손을 뗄 때 버튼 클릭(대상 선택)까지 같이 나가지 않게 막는 데 쓴다.
        private bool longPressFired;

        // 이 카드가 보여줄 아군을 연결하고 초상화를 채운다 (전투 시작 때 한 번).
        public void Bind(BattleUnit unit)
        {
            Unit = unit;

            // 씬에서 정한 HP 숫자 색을 기억해 둔다 → 보호막이 사라지면 이 색으로 되돌린다.
            hpTextColor = hpText.color;
            nameText.text = unit.Name;

            // 일러스트가 있으면 머리 부분을, 없으면 직군 색 판을 보여준다. Show는 그림이 없으면 false.
            bool hasIllustration = portrait.Show(unit.Data.Illustration);
            portraitFallback.gameObject.SetActive(!hasIllustration);
            portraitFallback.color = GetRoleColor(unit.Data.Role);
        }

        // HP·차례·대상 표시를 지금 상태로 갱신한다.
        // statuses: 이 아군에게 걸린 효과 목록 (UnitView.BuildDisplayedStatuses와 같은 순서)
        public void Refresh(bool isActing, bool isTargetable, List<StatusInstance> statuses)
        {
            ShowStatuses(statuses);

            ShowHp();

            // 스택형 패시브가 있으면 이름 뒤에 현재 스택을 붙인다 (예: "격발 ★1/2"). 머리 위 이름표 대신 여기서 보여준다.
            nameText.text = $"{Unit.Name}{BuildStackText()}";

            actingFrame.SetActive(isActing);
            targetMark.SetActive(isTargetable);
            button.interactable = isTargetable;

            // 쓰러졌거나 퇴장했으면 반투명하게 (alpha 0.35).
            // 쓰러졌어도 부활 스킬로 고를 수 있으면 덜 흐리게(0.75) 해서 누를 수 있는 카드임을 보여준다.
            bool isFaded = Unit.IsDead || Unit.IsExited;
            group.alpha = !isFaded ? 1f : isTargetable ? 0.75f : 0.35f;
        }

        // 연출에 맞춘 화면 HP(ShownHp)가 바뀔 때마다 HP바만 다시 그린다 (타격 박자에 맞춰 줄어듦).
        private void OnEnable()
        {
            ShownHp.Changed += HandleShownHpChanged;
        }

        private void OnDisable()
        {
            ShownHp.Changed -= HandleShownHpChanged;
        }

        private void HandleShownHpChanged()
        {
            if (Unit != null)
            {
                ShowHp();
            }
        }

        // HP바와 보호막을 그린다. 값은 실제 HP가 아니라 연출에 맞춘 화면 HP(ShownHp)를 쓴다.
        //  - HP 막대: 현재 HP / 최대 HP. fillAmount는 0~1 사이 값만큼 막대를 채운다
        //  - 보호막이 있으면: HP바를 감싸는 보호막 막대를 보호막 양 / 최대 HP만큼 채우고(넘치면 꽉 참), 숫자는 HP 대신 보호막 양
        //    보호막이 먼저 깎이므로, 지금 맞으면 줄어들 숫자를 보여주는 것이다
        private void ShowHp()
        {
            int hp = ShownHp.Hp(Unit);
            float hpRatio = Unit.MaxHp > 0 ? (float)hp / Unit.MaxHp : 0f;
            hpFill.fillAmount = hpRatio;

            int shield = ShownHp.Shield(Unit);
            bool hasShield = shield > 0 && !Unit.IsDead;

            if (shieldFill != null)
            {
                shieldFill.gameObject.SetActive(hasShield);

                // Mathf.Clamp01: 0~1로 자른다 → 보호막이 최대 HP보다 커도 막대는 꽉 찬 데서 멈춘다.
                shieldFill.fillAmount = Unit.MaxHp > 0 ? Mathf.Clamp01((float)shield / Unit.MaxHp) : 0f;
            }

            hpText.text = hasShield ? $"{shield}" : $"{hp}";
            hpText.color = hasShield ? shieldTextColor : hpTextColor;
        }

        // 스택을 쓰는 패시브마다 " ★현재/최대"를 붙인 글자. 스택 패시브가 없으면 빈 글자.
        private string BuildStackText()
        {
            string text = string.Empty;
            foreach (PassiveData passive in Unit.Data.Passives)
            {
                if (passive.UsesStacks)
                {
                    text += $" ★{Unit.GetPassiveStacks(passive)}/{passive.MaxStacks}";
                }
            }

            return text;
        }

        // 효과를 버프·디버프로 나눠 각자의 줄에 보여준다.
        // FindAll(조건 람다): 분류가 같은 효과만 걸러 새 목록을 만든다 (순서는 원래 목록 그대로).
        private void ShowStatuses(List<StatusInstance> statuses)
        {
            ShowRow(statuses.FindAll(status => status.Data.Category == StatusCategory.Buff), buffSlots, buffMore, buffColor);
            ShowRow(statuses.FindAll(status => status.Data.Category == StatusCategory.Debuff), debuffSlots, debuffMore, debuffColor);
        }

        // 한 줄: 앞에서부터 칸 수만큼 아이콘을 채우고, 남는 효과가 있으면 "…"을 켠다.
        // 보스 HP바(BossHpBarView)도 같은 칸 구조로 버프·디버프 줄을 그리므로 public으로 연다.
        public static void ShowRow(List<StatusInstance> statuses, List<StatusSlot> slots, GameObject moreMark, Color color)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                StatusSlot slot = slots[i];
                bool isUsed = i < statuses.Count;
                slot.background.gameObject.SetActive(isUsed);
                if (!isUsed)
                {
                    continue;
                }

                StatusEffectData data = statuses[i].Data;
                slot.background.color = color;
                slot.glyph.texture = data.Icon;
                slot.glyph.enabled = data.Icon != null;

                // 개인 버프(경계·오라 표시 등)는 검은 그림, 나머지는 흰 그림 (머리 위 상태효과 줄과 같은 규칙).
                slot.glyph.color = data.IsPersonal ? Color.black : Color.white;
                // 남은 턴 (스택 효과는 "×스택").
                slot.turns.text = statuses[i].IconText;
            }

            moreMark.SetActive(statuses.Count > slots.Count);
        }

        // 길게 누르기: 누른 순간부터 시간을 재고, 정한 시간이 지나면 한 번만 알린다.
        public void OnPointerDown(PointerEventData eventData)
        {
            pressTime = 0f;
            longPressFired = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pressTime = -1f;
        }

        // 누른 채 카드 밖으로 나가면 취소한다.
        public void OnPointerExit(PointerEventData eventData)
        {
            pressTime = -1f;
        }

        private void Update()
        {
            if (pressTime < 0f || longPressFired)
            {
                return;
            }

            pressTime += Time.unscaledDeltaTime;
            if (pressTime >= longPressSeconds)
            {
                longPressFired = true;

                // ?.Invoke: 구독한 곳이 없으면 아무것도 하지 않는다.
                LongPressed?.Invoke(this);
            }
        }

        // 이번 클릭이 길게 누르기 뒤에 온 것인지 확인하고 표시를 지운다. true면 클릭(대상 선택)을 무시한다.
        public bool ConsumeLongPress()
        {
            bool fired = longPressFired;
            longPressFired = false;
            return fired;
        }

        private Color GetRoleColor(CharacterRole role)
        {
            return role switch
            {
                CharacterRole.Support => supportColor,
                CharacterRole.Defense => defenseColor,
                CharacterRole.Heal => healerColor,
                _ => attackColor,
            };
        }
    }
}
