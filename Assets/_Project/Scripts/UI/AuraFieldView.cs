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

        [Tooltip("켜면 오라 이펙트가 카메라를 따라와 언제나 화면 앞쪽을 가로지른다 (장면마다 카메라가 달라져도 보이도록)")]
        [SerializeField] private bool followCamera = true;

        [Tooltip("카메라 앞으로 얼마나 떨어진 곳에 이펙트를 둘지 (수평 거리)")]
        [SerializeField, Min(0f)] private float followDistance = 7f;

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

        // 이펙트 묶음(이 오브젝트)을 카메라 앞에 두고 카메라와 같은 방향을 보게 한다.
        // 이펙트는 이 오브젝트의 자식이라 함께 움직인다 → 장면마다 카메라 구도가 바뀌어도 이펙트가 화면을 가로지른다.
        // LateUpdate: 카메라가 이번 프레임 위치로 움직인 뒤에 따라간다.
        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;
            if (!followCamera || mainCamera == null)
            {
                return;
            }

            // 카메라가 보는 방향에서 높이 성분을 뺀 수평 방향. 이펙트가 기울지 않고 바닥과 나란히 선다.
            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            forward.Normalize();
            Vector3 position = mainCamera.transform.position + forward * followDistance;

            // 높이는 처음 놓인 높이(바닥 기준) 그대로 두어, 카메라가 올라가도 이펙트가 떠오르지 않게 한다.
            position.y = transform.position.y;

            // Quaternion.LookRotation(forward): 카메라와 같은 수평 방향을 앞(z)으로 보는 회전.
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
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
