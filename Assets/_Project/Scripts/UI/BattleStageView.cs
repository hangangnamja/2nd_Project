using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 장면 구성 방식. 전투 위치(로직)와 화면에 서는 자리(연출)를 나눠, 장면마다 필요한 유닛만 그 장면의 자리에 세운다.
    public enum StageLayout
    {
        // 전투 시작·종료: 아군 줄과 적 줄이 마주 봄 (전체를 한눈에)
        Overview,

        // 누군가의 차례: 행동하는 유닛 하나만 앞(원점)에 서고, 같은 편은 숨기고, 상대 편은 멀리 한 줄
        ActorFocus,

        // 행동하는 유닛의 편 전원이 카메라 쪽을 보고 한 줄로 섬 (아군 전체·단일 버프를 고를 때). 상대 편은 숨김
        TeamLine,
    }

    // 전투 유닛들을 캡슐(UnitView)로 만들고, 장면(StageLayout)에 따라 세울 자리와 보일지를 정한다.
    // 붕괴 스타레일처럼 "고정된 전장을 카메라가 돌아다니는" 것이 아니라, 장면마다 무대를 다시 세운다 (BattleCameraDirector가 장면을 고름).
    // BattleManager의 이벤트를 구독해서 움직이므로, BattleManager는 화면의 존재를 몰라도 된다.
    public class BattleStageView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private UnitView unitViewPrefab;

        [Header("무대 배치")]
        [Tooltip("행동하는 유닛 앞쪽(+z) 멀리 상대 편 줄까지의 거리")]
        [SerializeField] private float farLineDistance = 10f;

        [Tooltip("멀리 선 상대 편 줄의 가로 간격")]
        [SerializeField] private float farLineSpacing = 3f;

        [Tooltip("편 전원이 나란히 설 때(아군 정면) 가로 간격")]
        [SerializeField] private float teamLineSpacing = 1.9f;

        [Tooltip("멀리 선 상대 줄을 휘게 하는 정도: 줄 끝의 유닛이 가운데보다 이만큼 앞(행동 유닛 쪽)으로 나온다. 0이면 일직선")]
        [SerializeField, Min(0f)] private float farLineArc = 2.2f;

        [Header("행동 유닛 차례 배치 (붕괴 스타레일식: 행동 유닛은 왼쪽 앞, 상대 무리는 오른쪽 앞)")]
        [Tooltip("상대 무리 줄의 가운데 (행동 유닛 = 원점 기준). 오른쪽 앞에 둔다")]
        [SerializeField] private Vector3 focusLineCenter = new Vector3(5.2f, 0f, 4.6f);

        [Tooltip("상대 무리 줄이 뻗는 방향 (편성 0번 → 끝번). 카메라 시선과 수직에 가깝게 두면 화면에 가로로 고르게 펼쳐진다")]
        [SerializeField] private Vector3 focusLineDirection = new Vector3(0.88f, 0f, -0.47f);

        [Tooltip("상대 무리 줄의 간격 (인원이 적을 때). 인원이 많으면 아래 최대 너비 안에 들어오도록 좁힌다")]
        [SerializeField, Min(0.5f)] private float focusLineSpacing = 3.3f;

        [Tooltip("상대 무리 줄 양 끝 사이의 최대 거리. 인원이 많아도 이 너비를 넘지 않아 화면 밖으로 나가거나 행동 유닛과 겹치지 않는다")]
        [SerializeField, Min(1f)] private float focusLineMaxWidth = 9f;

        [Tooltip("상대가 4명 이상이면 늘어난 인원 1명당 줄을 오른쪽으로 이만큼 더 민다 (왼쪽 끝이 행동 유닛과 겹치지 않게)")]
        [SerializeField, Min(0f)] private float focusLineShiftPerExtra = 0.6f;

        private readonly List<UnitView> views = new List<UnitView>();

        // 이번 장면에서 화면에 세운 유닛 캡슐. 나머지는 숨긴다 (쓰러짐·퇴장도 숨김).
        private readonly HashSet<UnitView> staged = new HashSet<UnitView>();

        // 편 전원이 한 줄로 설 때(TeamLine) 쓰러진 아군이 서 있던 빈자리. 부활 스킬로 고를 수 있을 때만 흐리게 보인다.
        private readonly HashSet<UnitView> fallenSlots = new HashSet<UnitView>();

        // 마지막으로 세운 장면. 웨이브가 바뀌어 적 캡슐을 새로 만들면 같은 장면으로 다시 세운다.
        private StageLayout currentLayout = StageLayout.Overview;
        private BattleUnit currentActor;

        private void OnEnable()
        {
            battleManager.BattleStarted += HandleBattleStarted;
            battleManager.WaveStarted += HandleWaveStarted;
            battleManager.UnitsSpawned += HandleUnitsSpawned;
            battleManager.StateChanged += RefreshAll;
        }

        private void OnDisable()
        {
            battleManager.BattleStarted -= HandleBattleStarted;
            battleManager.WaveStarted -= HandleWaveStarted;
            battleManager.UnitsSpawned -= HandleUnitsSpawned;
            battleManager.StateChanged -= RefreshAll;
        }

        // 전투가 시작되면 유닛마다 캡슐을 하나씩 만들고, 전체가 보이는 장면으로 세운다.
        private void HandleBattleStarted()
        {
            SpawnViews(battleManager.Allies);
            SpawnViews(battleManager.Enemies);
            Stage(StageLayout.Overview, null);
        }

        // 다음 웨이브: 이전 적 캡슐을 지우고 새 적 캡슐을 만든 뒤, 지금 장면을 다시 세워 새 적을 자리에 놓는다.
        private void HandleWaveStarted()
        {
            // RemoveAll(조건): 조건이 true인 원소를 목록에서 모두 지운다. 지우기 전에 그 캡슐 오브젝트를 파괴한다.
            views.RemoveAll(view =>
            {
                if (view.Unit.IsAlly)
                {
                    return false;
                }

                staged.Remove(view);
                Destroy(view.gameObject);
                return true;
            });

            SpawnViews(battleManager.Enemies);
            Stage(currentLayout, currentActor);
        }

        // 전투 중 유닛이 들어옴(부하 소환): 아직 캡슐이 없는 유닛만 만들고, 지금 장면을 다시 세워 새 유닛을 자리에 놓는다.
        // 캡슐 목록 순서는 화면 줄 순서와 무관하다 → 줄 순서는 GetSide가 전투 편 목록 순서대로 다시 정렬해 맞춘다.
        private void HandleUnitsSpawned()
        {
            var newUnits = new List<BattleUnit>();
            foreach (BattleUnit unit in battleManager.Enemies)
            {
                if (FindView(unit) == null)
                {
                    newUnits.Add(unit);
                }
            }

            foreach (BattleUnit unit in battleManager.Allies)
            {
                if (FindView(unit) == null)
                {
                    newUnits.Add(unit);
                }
            }

            SpawnViews(newUnits);
            Stage(currentLayout, currentActor);
        }

        // 전투 유닛을 표시하는 캡슐을 찾는다 (연출이 이펙트를 띄울 위치를 얻을 때). 없으면 null.
        public UnitView FindView(BattleUnit unit)
        {
            return views.Find(view => view.Unit == unit);
        }

        // 행동하는 유닛이 서는 자리(원점)와, 멀리 선 상대 편 줄의 가운데 (카메라 구도의 기준점).
        public Vector3 ActorPoint => transform.position;
        public Vector3 FarLineCenter => transform.position + new Vector3(0f, 0f, farLineDistance);

        // 장면에 맞게 유닛을 세운다. actor = 지금 행동하는 유닛 (Overview면 null이어도 된다).
        public void Stage(StageLayout layout, BattleUnit actor)
        {
            currentLayout = layout;
            currentActor = actor;
            staged.Clear();
            fallenSlots.Clear();

            switch (layout)
            {
                case StageLayout.ActorFocus:
                    StageActorFocus(actor);
                    break;
                case StageLayout.TeamLine:
                    // ?? true: 행동 유닛이 없으면 아군 줄을 세운다.
                    // 쓰러진 아군도 자기 자리를 지켜 빈자리로 남긴다 → 부활 스킬을 고르면 그 자리를 눌러 대상으로 고를 수 있다.
                    StageLine(GetSide(actor?.IsAlly ?? true), Vector3.zero, teamLineSpacing, faceCamera: true, cameraInFront: true, keepFallenSlots: true);
                    break;
                default:
                    StageLine(GetSide(isAlly: true), Vector3.zero, teamLineSpacing, faceCamera: false, cameraInFront: false);
                    StageLine(GetSide(isAlly: false), new Vector3(0f, 0f, farLineDistance), farLineSpacing, faceCamera: true, cameraInFront: false, arc: farLineArc);
                    break;
            }

            RefreshAll();
        }

        // 행동 유닛 하나만 원점에 세우고(+z를 봄), 상대 편은 멀리 한 줄(행동 유닛 쪽을 봄). 같은 편은 숨긴다.
        private void StageActorFocus(BattleUnit actor)
        {
            UnitView actorView = actor != null ? FindView(actor) : null;
            if (actorView != null)
            {
                // 행동 유닛은 오른쪽 앞의 상대 무리 쪽을 바라본다 (모델로 바꿨을 때 등 너머로 상대를 겨누는 자세).
                Vector3 toEnemies = focusLineCenter;
                toEnemies.y = 0f;
                Place(actorView, Vector3.zero, Quaternion.LookRotation(toEnemies, Vector3.up));
            }

            bool actorIsAlly = actor == null || actor.IsAlly;
            StageFocusLine(GetSide(!actorIsAlly));
        }

        // 행동 유닛 차례의 상대 무리: 오른쪽 앞에 비스듬한 한 줄로 세우고, 모두 행동 유닛(원점)을 바라보게 한다.
        // 줄 방향을 카메라 시선과 거의 수직으로 두어 화면에서 가로로 펼쳐지고, 행동 유닛은 화면 왼쪽에 따로 선다.
        private void StageFocusLine(List<UnitView> side)
        {
            // 화면 기준 전장(ShownHp.IsOnField): 쓰러뜨린 공격의 연출이 끝나기 전에 장면이 바뀌어도 맞은 유닛이 그대로 서 있게 한다.
            List<UnitView> standing = side.FindAll(view => ShownHp.IsOnField(view.Unit));

            // normalized: 길이 1짜리 방향. 인원이 많으면 줄이 왼쪽으로 길어지므로, 늘어난 인원만큼 줄 전체를 그 방향(오른쪽)으로 민다.
            Vector3 direction = focusLineDirection.normalized;
            Vector3 center = focusLineCenter + direction * (Mathf.Max(0, standing.Count - 3) * focusLineShiftPerExtra);

            // 간격 = 기본 간격과 (최대 너비 ÷ 칸 사이 수) 중 작은 값 → 3명이면 넓게, 5명이면 최대 너비 안에 맞춰 좁게.
            // Mathf.Max(1, …): 혼자 서 있으면 칸 사이가 0이므로 1로 막는다.
            float spacing = Mathf.Min(focusLineSpacing, focusLineMaxWidth / Mathf.Max(1, standing.Count - 1));

            for (int i = 0; i < standing.Count; i++)
            {
                // 가운데 정렬: (i - (인원 - 1) / 2) × 간격만큼 줄 방향으로 떨어진 자리.
                Vector3 position = center + direction * ((i - (standing.Count - 1) * 0.5f) * spacing);

                // Quaternion.LookRotation(방향): 행동 유닛(원점)을 바라보는 회전. 높이 차는 빼서 기울지 않게 한다.
                Vector3 toActor = -position;
                toActor.y = 0f;
                Place(standing[i], position, Quaternion.LookRotation(toActor, Vector3.up));
            }
        }

        // 한쪽 편 유닛들을 center를 가운데로 가로 한 줄로 세운다 (편성 0번이 화면 왼쪽).
        // faceCamera: 원점 쪽(-z)을 보게 할지. cameraInFront: 카메라가 줄 앞(+z)에서 -z를 보는 장면인지(TeamLine).
        // 쓰러지거나 퇴장한 유닛은 빼고 남은 유닛끼리 가운데 정렬한다.
        // keepFallenSlots: 쓰러진 유닛도 줄에 자리를 남긴다 (fallenSlots에 넣어 두고, 보일지는 RefreshAll이 정함).
        // arc: 줄을 행동 유닛 쪽으로 휘게 하는 정도 (멀리 선 상대 줄만). 줄 끝이 가운데보다 arc만큼 앞으로 나와
        //      양옆 유닛이 원점(행동 유닛)을 감싸듯 서서, 뒤에서 볼 때 깊이가 생기고 가운데 유닛(보스)이 가려지지 않는다.
        private void StageLine(List<UnitView> side, Vector3 center, float spacing, bool faceCamera, bool cameraInFront, bool keepFallenSlots = false, float arc = 0f)
        {
            // FindAll(조건): 화면에 세울 유닛을 모은다. 기본은 살아 있고 퇴장하지 않은 유닛,
            // keepFallenSlots면 쓰러진 유닛도 포함한다 (퇴장한 유닛은 언제나 뺀다).
            List<UnitView> standing = side.FindAll(view =>
                ShownHp.IsOnField(view.Unit) || (keepFallenSlots && ShownHp.IsDead(view.Unit) && !view.Unit.IsExited));

            // Quaternion.Euler(0, 180, 0): y축으로 반 바퀴 → -z(카메라·행동 유닛 쪽)를 바라본다.
            Quaternion facing = faceCamera ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            // 카메라가 줄 앞에서 거꾸로(-z) 보면 화면 왼쪽이 +x가 되므로, 편성 0번이 화면 왼쪽에 오도록 x 방향을 뒤집는다.
            float direction = cameraInFront ? -1f : 1f;

            for (int i = 0; i < standing.Count; i++)
            {
                // 가운데 정렬: (i - (인원 - 1) / 2) × 간격 → 인원이 몇 명이든 줄의 가운데가 center.
                float x = (i - (standing.Count - 1) * 0.5f) * spacing * direction;

                // 휜 줄: 가운데에서 멀수록 앞으로(-z) 나온다. t = 줄 끝까지 거리의 비율(0 = 가운데, 1 = 끝), 앞으로 나오는 양 = arc × t².
                // Mathf.Max(1, …): 혼자 서 있으면 나눌 거리가 0이므로 1로 막는다.
                float halfWidth = Mathf.Max(1f, (standing.Count - 1) * 0.5f * spacing);
                float t = Mathf.Abs(x) / halfWidth;
                Vector3 position = center + new Vector3(x, 0f, -arc * t * t);

                // 휜 줄은 각자 원점(행동 유닛 자리)을 바라보게 돌린다.
                // Quaternion.LookRotation(방향): 그 방향을 앞(z)으로 보는 회전. 높이 차는 빼서(y = 0) 기울지 않게 한다.
                Quaternion rotation = facing;
                if (arc > 0f)
                {
                    Vector3 toOrigin = -position;
                    toOrigin.y = 0f;
                    rotation = Quaternion.LookRotation(toOrigin, Vector3.up);
                }

                Place(standing[i], position, rotation);

                if (ShownHp.IsDead(standing[i].Unit))
                {
                    fallenSlots.Add(standing[i]);
                }
            }
        }

        private void Place(UnitView view, Vector3 localPosition, Quaternion rotation)
        {
            // 캡슐을 바로 옮기지 않고 기준 자리로 넘긴다 → 돌진·피격 모션은 이 자리를 기준으로 어긋남만 더한다.
            view.SetAnchor(transform.position + localPosition, rotation);
            staged.Add(view);
        }

        // 한쪽 편 캡슐 목록 (전투 편 목록 순서 = 화면 줄 순서).
        // 소환된 부하는 편 목록의 앞·뒤에 끼워 넣어지므로(보스를 가운데로), 캡슐을 만든 순서가 아니라 편 목록 순서를 따른다.
        private List<UnitView> GetSide(bool isAlly)
        {
            IReadOnlyList<BattleUnit> units = isAlly ? battleManager.Allies : battleManager.Enemies;
            var side = new List<UnitView>();

            foreach (BattleUnit unit in units)
            {
                UnitView view = FindView(unit);
                if (view != null)
                {
                    side.Add(view);
                }
            }

            return side;
        }

        // 이미 캡슐이 있는 유닛은 건너뛴다 (전투 시작 전에 소환 알림으로 먼저 만들어진 부하가 겹치지 않게).
        private void SpawnViews(IReadOnlyList<BattleUnit> units)
        {
            foreach (BattleUnit unit in units)
            {
                if (FindView(unit) != null)
                {
                    continue;
                }

                UnitView view = Instantiate(unitViewPrefab, transform.position, Quaternion.identity, transform);
                view.name = $"Unit_{unit.Name}";
                view.Bind(unit);
                views.Add(view);
            }
        }

        // 모든 유닛 표시를 현재 상태로 갱신한다. 이번 장면에 세운 유닛만 보이고, 쓰러진 유닛은 장면에서 빠진다.
        private void RefreshAll()
        {
            foreach (UnitView view in views)
            {
                // SetActive(false): 오브젝트를 끄면 그려지지 않고 콜라이더도 꺼져 클릭 대상에서도 빠진다.
                // 쓰러진 유닛은 이번 장면에 세웠더라도 그 공격의 연출이 끝난 뒤 다음 장면부터 빠진다 (사망 이펙트가 보이도록 지금은 둔다).
                // 플레이어 입력을 기다리는 중일 때만, 선택된 스킬로 고를 수 있는 유닛을 강조한다.
                bool isTargetable = battleManager.IsWaitingForPlayer && battleManager.IsValidTarget(view.Unit);

                // 쓰러진 아군의 빈자리는 지금 고른 스킬로 고를 수 있을 때(부활)만 보인다.
                // 부활해 살아났으면 빈자리가 아니므로 일반 유닛처럼 보인다.
                bool isHiddenFallenSlot = fallenSlots.Contains(view) && view.Unit.IsDead && !isTargetable;

                bool isVisible = staged.Contains(view) && !view.Unit.IsExited && !isHiddenFallenSlot;
                view.gameObject.SetActive(isVisible);
                if (!isVisible)
                {
                    continue;
                }

                bool isActing = view.Unit == battleManager.CurrentActor;
                view.Refresh(isActing, isTargetable);
            }
        }
    }
}
