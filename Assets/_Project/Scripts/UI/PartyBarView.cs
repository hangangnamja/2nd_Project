using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 화면 아래 아군 초상화 바. 전투가 시작되면 아군마다 카드(PartyCardView)를 만들고, 전투 상태가 바뀔 때마다 갱신한다.
    // 아군 대상 스킬(단일 버프·회복)을 고른 상태에서 카드를 누르면 그 아군을 대상으로 고른다 (3D 고리를 눌러도 같음).
    public class PartyBarView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        [Tooltip("복사해서 쓸 카드 원본 (씬에서는 꺼 둔다)")]
        [SerializeField] private PartyCardView cardTemplate;

        [Tooltip("상태효과 목록을 얻을 유닛 표시 (머리 위 상태효과 줄과 같은 목록을 쓴다)")]
        [SerializeField] private BattleStageView stageView;

        [Tooltip("카드를 꾹 누르면 여는 상세 창")]
        [SerializeField] private UnitDetailPanel detailPanel;

        private readonly List<PartyCardView> cards = new List<PartyCardView>();

        private void OnEnable()
        {
            battleManager.BattleStarted += HandleBattleStarted;
            battleManager.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            battleManager.BattleStarted -= HandleBattleStarted;
            battleManager.StateChanged -= Refresh;
        }

        // 아군 편성 순서대로 카드를 만든다. 카드를 누르면 그 아군을 대상으로 선택한다.
        private void HandleBattleStarted()
        {
            cardTemplate.gameObject.SetActive(false);
            foreach (BattleUnit ally in battleManager.Allies)
            {
                // Instantiate(원본, 부모): 원본을 복사해 같은 부모(가로 정렬 묶음) 아래에 붙인다.
                PartyCardView card = Instantiate(cardTemplate, cardTemplate.transform.parent);
                card.gameObject.SetActive(true);
                card.Bind(ally);

                // 람다식이 반복마다 새로 만든 지역 변수 ally를 기억해, 카드마다 자기 아군을 넘긴다.
                // 꾹 눌러 상세 창을 연 뒤 손을 뗄 때 나오는 클릭은 대상 선택으로 처리하지 않는다.
                card.Button.onClick.AddListener(() =>
                {
                    if (!card.ConsumeLongPress())
                    {
                        battleManager.SelectTarget(ally);
                    }
                });
                card.LongPressed += pressed => detailPanel.Open(pressed.Unit);
                cards.Add(card);
            }

            Refresh();
        }

        // 머리 위 상태효과 줄과 같은 효과 목록. 유닛 표시를 못 찾으면 걸린 상태효과만.
        private List<Game.Battle.Status.StatusInstance> GetStatuses(BattleUnit unit)
        {
            UnitView view = stageView.FindView(unit);
            return view != null ? view.BuildDisplayedStatuses() : unit.Statuses.GetAll();
        }

        private void Refresh()
        {
            foreach (PartyCardView card in cards)
            {
                bool isActing = card.Unit == battleManager.CurrentActor;
                bool isTargetable = battleManager.IsWaitingForPlayer && battleManager.IsValidTarget(card.Unit);
                card.Refresh(isActing, isTargetable, GetStatuses(card.Unit));
            }
        }
    }
}
