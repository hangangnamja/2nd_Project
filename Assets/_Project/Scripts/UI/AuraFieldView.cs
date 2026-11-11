using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 오라가 켜져 있는 동안 전장에 오라 이펙트를 띄운다 (기획서 3-7 오라: 적용 중인지 눈으로 알 수 있게).
    // 전투 상태가 바뀔 때마다(StateChanged) 유닛들의 오라를 확인해, 새로 켜진 오라는 이펙트를 만들고 꺼진 오라는 지운다.
    // 로직(BattleUnit.ActiveAura)은 화면을 모르고, 이 컴포넌트가 읽어서 보여주기만 한다.
    public class AuraFieldView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        // 오라 주인 → 띄워 둔 이펙트. 주인이 같아도 오라가 바뀌면(다시 공연) 이펙트를 새로 만든다.
        private readonly Dictionary<BattleUnit, (TimedAura aura, GameObject effect)> shown =
            new Dictionary<BattleUnit, (TimedAura aura, GameObject effect)>();

        private void OnEnable()
        {
            battleManager.StateChanged += Refresh;
        }

        private void OnDisable()
        {
            battleManager.StateChanged -= Refresh;
        }

        // 지금 오라를 생성 중인(살아 있는) 유닛의 이펙트는 남기거나 새로 만들고, 그 외는 지운다.
        private void Refresh()
        {
            var active = new HashSet<BattleUnit>();
            AddActive(battleManager.Allies, active);
            AddActive(battleManager.Enemies, active);

            // 꺼진 오라의 이펙트 지우기 (목록을 돌면서 지우면 오류가 나므로 지울 대상을 먼저 모은다)
            var removed = new List<BattleUnit>();
            foreach (KeyValuePair<BattleUnit, (TimedAura aura, GameObject effect)> pair in shown)
            {
                if (!active.Contains(pair.Key) || pair.Key.ActiveAura != pair.Value.aura)
                {
                    Destroy(pair.Value.effect);
                    removed.Add(pair.Key);
                }
            }

            foreach (BattleUnit unit in removed)
            {
                shown.Remove(unit);
            }

            // 새로 켜진 오라의 이펙트 만들기 (전장 가운데 = 이 오브젝트 위치)
            foreach (BattleUnit unit in active)
            {
                if (!shown.ContainsKey(unit))
                {
                    GameObject effect = Instantiate(unit.ActiveAura.FieldEffect, transform);
                    effect.name = $"AuraField_{unit.Name}_{unit.ActiveAura.Name}";
                    shown[unit] = (unit.ActiveAura, effect);
                }
            }
        }

        // 쓰러지지 않았고, 전장 이펙트가 있는 기간 오라를 생성 중인 유닛을 모은다. 퇴장한 유닛의 오라도 적용 중이므로 포함한다.
        private static void AddActive(IReadOnlyList<BattleUnit> units, HashSet<BattleUnit> active)
        {
            foreach (BattleUnit unit in units)
            {
                // ?.: 오라가 없으면(null) FieldEffect를 읽지 않고 null → 이펙트 없음
                if (!unit.IsDead && unit.ActiveAura?.FieldEffect != null)
                {
                    active.Add(unit);
                }
            }
        }
    }
}
