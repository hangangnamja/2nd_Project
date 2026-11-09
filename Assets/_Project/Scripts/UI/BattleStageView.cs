using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 전투 유닛들을 캡슐(UnitView)로 배치하고, 전투 상태가 바뀔 때마다 모두 갱신한다.
    // BattleManager의 이벤트를 구독해서 움직이므로, BattleManager는 화면의 존재를 몰라도 된다.
    public class BattleStageView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private UnitView unitViewPrefab;

        [Header("배치")]
        [Tooltip("아군은 -x, 적은 +x 방향으로 이 거리부터 배치된다")]
        [SerializeField] private float frontLineOffset = 2f;
        [SerializeField] private float unitSpacing = 2.6f;

        private readonly List<UnitView> views = new List<UnitView>();

        // 오브젝트가 활성화될 때 전투 이벤트를 구독한다.
        // += 로 등록한 함수는 BattleManager가 해당 이벤트를 Invoke할 때마다 호출된다.
        private void OnEnable()
        {
            battleManager.BattleStarted += HandleBattleStarted;
            battleManager.StateChanged += RefreshAll;
        }

        // 비활성화·파괴될 때 구독을 해제한다. 해제하지 않으면 사라진 오브젝트의 함수가 계속 호출될 수 있다.
        private void OnDisable()
        {
            battleManager.BattleStarted -= HandleBattleStarted;
            battleManager.StateChanged -= RefreshAll;
        }

        // 전투가 시작되면 아군·적 유닛마다 캡슐을 하나씩 만들어 배치한다.
        private void HandleBattleStarted()
        {
            SpawnSide(battleManager.Allies, direction: -1f);
            SpawnSide(battleManager.Enemies, direction: 1f);
            RefreshAll();
        }

        // 한쪽 편 유닛들을 편성 순서대로 일렬로 세운다. direction이 -1이면 왼쪽(아군), 1이면 오른쪽(적).
        private void SpawnSide(IReadOnlyList<BattleUnit> units, float direction)
        {
            for (int i = 0; i < units.Count; i++)
            {
                // 편성 0번이 앞줄(중앙 쪽), 뒤 번호일수록 바깥쪽에 선다.
                float x = direction * (frontLineOffset + i * unitSpacing);
                Vector3 position = transform.position + new Vector3(x, 0f, 0f);

                UnitView view = Instantiate(unitViewPrefab, position, Quaternion.identity, transform);
                view.name = $"Unit_{units[i].Name}";
                view.Bind(units[i]);
                views.Add(view);
            }
        }

        // 모든 유닛 표시를 현재 상태로 갱신한다 (현재 행동자, 선택된 스킬의 대상 후보 강조 포함).
        private void RefreshAll()
        {
            foreach (UnitView view in views)
            {
                // 퇴장한 아군(하이퍼캐리 변신 중)은 다른 공간에 남겨진 것이므로 화면에서 아예 숨긴다.
                // SetActive(false): 오브젝트를 끄면 그려지지 않고 콜라이더도 꺼져 클릭 대상에서도 빠진다.
                // 뷰는 views 목록이 계속 들고 있으므로, 복귀하면 다시 켜고 그대로 갱신한다.
                bool isVisible = !view.Unit.IsExited;
                view.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                bool isActing = view.Unit == battleManager.CurrentActor;

                // 플레이어 입력을 기다리는 중일 때만, 선택된 스킬로 고를 수 있는 유닛을 강조한다.
                bool isTargetable = battleManager.IsWaitingForPlayer && battleManager.IsValidTarget(view.Unit);
                view.Refresh(isActing, isTargetable);
            }
        }
    }
}
