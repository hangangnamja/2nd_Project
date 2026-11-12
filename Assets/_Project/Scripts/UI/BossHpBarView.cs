using System.Collections.Generic;
using Game.Battle;
using Game.Battle.Status;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투 화면 위쪽 가운데의 보스 HP바 (기획서 3-11). 지금 웨이브에 "보스" 표시가 된 적이 있을 때만 보인다.
    //  - 보스 초상화(일러스트 머리 부분, 없으면 색 판) + 이름 + 큰 HP바·숫자 (보호막이 있으면 HP바를 감싸는 막대 + 보호막 숫자)
    //  - 아래에 버프 줄·디버프 줄 (아군 초상화 카드와 같은 칸 구조, 줄마다 최대 칸 수 + "…")
    //  - 바를 누르면 상세 창(스탯·걸린 효과 전부)을 연다
    // 표시만 담당하고 값은 바꾸지 않는다. 전투 상태가 바뀔 때마다(StateChanged) 다시 그린다.
    public class BossHpBarView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        [Tooltip("상태효과 목록을 얻을 유닛 표시 (머리 위 상태효과 줄과 같은 목록을 쓴다)")]
        [SerializeField] private BattleStageView stageView;

        [Tooltip("바를 누르면 여는 상세 창")]
        [SerializeField] private UnitDetailPanel detailPanel;

        [Tooltip("보스가 없으면 통째로 숨길 묶음")]
        [SerializeField] private GameObject root;

        [SerializeField] private Button button;

        [Header("초상화")]
        [SerializeField] private IllustrationView portrait;
        [SerializeField] private Image portraitFallback;
        [SerializeField] private Color fallbackColor = new Color(0.55f, 0.15f, 0.15f);

        [Header("이름·HP")]
        [SerializeField] private Text nameText;

        [Tooltip("HP 비율만큼 채워지는 막대 (Image Type = Filled, 가로)")]
        [SerializeField] private Image hpFill;
        [SerializeField] private Text hpText;

        [Tooltip("HP 막대 뒤에서 깎인 만큼 천천히 따라 줄어드는 밝은 막대 (Image Type = Filled, 가로). 비우면 쓰지 않음")]
        [Label("피해 잔상 막대")]
        [SerializeField] private Image hpTrail;

        [Tooltip("잔상 막대가 1초에 줄어드는 양 (1 = 최대 HP 전체)")]
        [Label("잔상 속도", Min = 0.01f)]
        [SerializeField] private float trailSpeed = 0.35f;

        [Tooltip("보호막이 있을 때 HP바를 감싸는 막대 (보호막 양 / 최대 HP)")]
        [SerializeField] private Image shieldFill;

        [Label("보호막 숫자 색")]
        [SerializeField] private Color shieldTextColor = new Color(0.75f, 0.9f, 1f);

        [Header("상태효과 아이콘 (버프 줄 / 디버프 줄)")]
        [SerializeField] private List<PartyCardView.StatusSlot> buffSlots = new List<PartyCardView.StatusSlot>();
        [SerializeField] private GameObject buffMore;
        [SerializeField] private List<PartyCardView.StatusSlot> debuffSlots = new List<PartyCardView.StatusSlot>();
        [SerializeField] private GameObject debuffMore;
        [SerializeField] private Color buffColor = new Color(0.2f, 0.45f, 0.95f);
        [SerializeField] private Color debuffColor = new Color(0.9f, 0.15f, 0.1f);

        // 지금 보여주고 있는 보스. 초상화는 보스가 바뀔 때만 다시 채운다.
        private BattleUnit shownBoss;

        // HP 숫자의 원래 색 (보호막이 사라지면 되돌림).
        private Color hpTextColor = Color.white;

        private void Awake()
        {
            hpTextColor = hpText.color;

            // 람다식: 버튼을 누르면 그때 보여주는 보스로 상세 창을 연다. 보스가 없으면(null) 아무것도 안 한다.
            button.onClick.AddListener(() =>
            {
                if (shownBoss != null)
                {
                    detailPanel.Open(shownBoss);
                }
            });

            root.SetActive(false);
        }

        // 전투 시작·웨이브 시작·부하 소환·상태 변화마다 다시 그린다.
        private void OnEnable()
        {
            battleManager.BattleStarted += Refresh;
            battleManager.WaveStarted += Refresh;
            battleManager.UnitsSpawned += Refresh;
            battleManager.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            battleManager.BattleStarted -= Refresh;
            battleManager.WaveStarted -= Refresh;
            battleManager.UnitsSpawned -= Refresh;
            battleManager.StateChanged -= Refresh;
        }

        // 보스를 찾아 바를 켜고 끈 뒤, 켜져 있으면 이름·HP·상태효과를 갱신한다.
        private void Refresh()
        {
            BattleUnit boss = FindBoss();
            root.SetActive(boss != null);
            if (boss == null)
            {
                shownBoss = null;
                return;
            }

            if (boss != shownBoss)
            {
                shownBoss = boss;
                ShowPortrait(boss);

                // 새 보스가 나오면 잔상 막대를 지금 HP에 바로 맞춘다 (이전 보스의 잔상이 남지 않게).
                if (hpTrail != null)
                {
                    hpTrail.fillAmount = boss.MaxHp > 0 ? (float)boss.CurrentHp / boss.MaxHp : 0f;
                }
            }

            nameText.text = boss.Name;
            ShowHp(boss);
            ShowStatuses(boss);
        }

        // 피해 잔상: HP 막대가 줄면 뒤의 밝은 막대가 잠시 남았다가 따라 줄어 "얼마나 깎였는지"가 보인다.
        // Mathf.MoveTowards(현재, 목표, 최대 이동량): 목표 쪽으로 이번 프레임 이동량만큼만 다가간다 → 일정한 속도로 줄어듦.
        // 회복으로 HP가 늘면 잔상 없이 바로 맞춘다.
        private void Update()
        {
            if (hpTrail == null || shownBoss == null)
            {
                return;
            }

            float target = hpFill.fillAmount;
            hpTrail.fillAmount = hpTrail.fillAmount > target
                ? Mathf.MoveTowards(hpTrail.fillAmount, target, trailSpeed * Time.deltaTime)
                : target;
        }

        // 지금 전장에 있는 적 중 "보스" 표시가 된 첫 유닛. 없으면 null.
        // 패턴 매칭 is: 데이터가 EnemyCharacterData이고 보스 표시가 켜져 있을 때만 보스로 본다.
        private BattleUnit FindBoss()
        {
            foreach (BattleUnit enemy in battleManager.Enemies)
            {
                if (enemy.IsOnField && enemy.Data is EnemyCharacterData enemyData && enemyData.IsBoss)
                {
                    return enemy;
                }
            }

            return null;
        }

        // 일러스트가 있으면 머리 부분을, 없으면 색 판을 보여준다. Show는 그림이 없으면 false.
        private void ShowPortrait(BattleUnit boss)
        {
            bool hasIllustration = portrait.Show(boss.Data.Illustration);
            portraitFallback.gameObject.SetActive(!hasIllustration);
            portraitFallback.color = fallbackColor;
        }

        // HP바와 보호막 (아군 초상화 카드와 같은 규칙).
        //  - fillAmount: 0~1 사이 값만큼 막대를 채운다 → 현재 HP / 최대 HP
        //  - 보호막이 있으면 감싸는 막대를 보호막 / 최대 HP만큼 채우고(Mathf.Clamp01로 최대 1), 숫자는 보호막 양
        private void ShowHp(BattleUnit boss)
        {
            hpFill.fillAmount = boss.MaxHp > 0 ? (float)boss.CurrentHp / boss.MaxHp : 0f;

            int shield = boss.ShieldAmount;
            bool hasShield = shield > 0;
            shieldFill.gameObject.SetActive(hasShield);
            shieldFill.fillAmount = boss.MaxHp > 0 ? Mathf.Clamp01((float)shield / boss.MaxHp) : 0f;

            hpText.text = hasShield ? $"보호막 {shield}" : $"{boss.CurrentHp} / {boss.MaxHp}";
            hpText.color = hasShield ? shieldTextColor : hpTextColor;
        }

        // 머리 위 상태효과 줄과 같은 목록을 버프·디버프로 나눠 각 줄에 그린다.
        // FindAll(조건 람다): 분류가 같은 효과만 걸러 새 목록을 만든다.
        private void ShowStatuses(BattleUnit boss)
        {
            UnitView view = stageView.FindView(boss);
            List<StatusInstance> statuses = view != null ? view.BuildDisplayedStatuses() : boss.Statuses.GetAll();

            PartyCardView.ShowRow(statuses.FindAll(status => status.Data.Category == StatusCategory.Buff), buffSlots, buffMore, buffColor);
            PartyCardView.ShowRow(statuses.FindAll(status => status.Data.Category == StatusCategory.Debuff), debuffSlots, debuffMore, debuffColor);
        }
    }
}
