using System;
using System.Collections;
using Game.Battle;
using Game.Core;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 카메라 위치·바라보는 곳의 기준점 (BattleStageView의 무대 자리).
    public enum ShotAnchor
    {
        [InspectorName("행동하는 유닛 자리 (원점)")] Actor,
        [InspectorName("멀리 선 상대 줄 가운데")] FarLine,
    }

    // 카메라 구도 하나: "어디(기준점 + 오프셋)에서 어디(기준점 + 오프셋)를 볼지"와 화각.
    // 오프셋은 무대 기준이다: 행동 유닛은 원점에서 +z(상대 쪽)를 보므로 -z = 뒤, +x = 화면 오른쪽, +y = 위.
    // 유닛 캡슐은 원점이 몸 가운데라 몸통은 y -1 ~ 1이다 → 몸 전체가 보이려면 카메라·바라볼 높이를 0 근처로 둔다.
    [Serializable]
    public class CameraShot
    {
        [Label("카메라 기준점")]
        [SerializeField] private ShotAnchor anchor = ShotAnchor.Actor;

        [Tooltip("기준점에서 카메라까지의 거리 (x 오른쪽, y 위, z 앞)")]
        [Label("카메라 오프셋")]
        [SerializeField] private Vector3 offset = new Vector3(1f, 1.7f, -2.6f);

        [Label("바라볼 기준점")]
        [SerializeField] private ShotAnchor lookAnchor = ShotAnchor.FarLine;

        [Label("바라볼 오프셋")]
        [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.4f, 0f);

        [Label("화각", Min = 10f, Max = 90f)]
        [SerializeField] private float fieldOfView = 50f;

        public ShotAnchor Anchor => anchor;
        public Vector3 Offset => offset;
        public ShotAnchor LookAnchor => lookAnchor;
        public Vector3 LookOffset => lookOffset;
        public float FieldOfView => fieldOfView;

        public CameraShot(ShotAnchor anchor, Vector3 offset, ShotAnchor lookAnchor, Vector3 lookOffset, float fieldOfView)
        {
            this.anchor = anchor;
            this.offset = offset;
            this.lookAnchor = lookAnchor;
            this.lookOffset = lookOffset;
            this.fieldOfView = fieldOfView;
        }
    }

    // 전투 장면 연출 (붕괴 스타레일식). 전투 상태를 보고 "장면(무대 배치) + 구도"를 고른다.
    //  - 아군 차례: 그 아군 하나만 앞에 세우고 적은 멀리 (BattleStageView.StageLayout.ActorFocus) → 그 아군 어깨 뒤 구도
    //    차례가 시작되면 기본 구도(1스 구도)로 시작하고, 스킬 버튼을 눌러 고르면 같은 무대에서 스킬마다 다른 각도로 부드럽게 움직인다
    //  - 적 차례: 평소 아군 시점 (아군 줄 + 멀리 적 줄, Overview) 그대로 맞는 모습을 본다
    //  - 아군 전체·단일 스킬: 아군 전원이 카메라를 보고 한 줄로 섬 (TeamLine) → 정면 구도, 단일이면 고리가 뜬 아군을 클릭
    //  - 자신에게 쓰는 스킬: 그 유닛을 정면에서
    // 무대 배치가 바뀌는 전환은 화면을 잠깐 어둡게 덮었다가(페이드) 바꿔, 캡슐이 순간이동하는 모습이 보이지 않게 한다.
    // 전투 로직은 건드리지 않고 BattleManager의 상태 변경 알림만 보고 판단한다 (표시 전용).
    public class BattleCameraDirector : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private BattleStageView stageView;
        [SerializeField] private Camera targetCamera;

        [Tooltip("장면 전환 때 화면을 덮는 검은 판 (HUD 맨 위, 클릭을 막지 않게 raycastTarget 끔)")]
        [SerializeField] private Image fadeOverlay;

        [Header("구도")]
        [Tooltip("전투 시작·종료·적 차례: 아군 뒤에서 아군 줄과 멀리 선 적 줄을 함께 (평소 아군 시점)")]
        [SerializeField] private CameraShot overviewShot = new CameraShot(ShotAnchor.Actor, new Vector3(0f, 3.2f, -8.5f), ShotAnchor.Actor, new Vector3(0f, 0f, 5f), 55f);

        [Tooltip("아군 뒤에서 적을 보는 구도. 1스 / 2스 / 3스 순서로 스킬마다 다른 각도 (차례 시작 때는 1스 구도)")]
        [SerializeField] private CameraShot[] skillShots =
        {
            new CameraShot(ShotAnchor.Actor, new Vector3(1.6f, 0.3f, -4.2f), ShotAnchor.FarLine, new Vector3(-1.5f, 0.2f, 0f), 50f),
            new CameraShot(ShotAnchor.Actor, new Vector3(-2f, 0.7f, -4.8f), ShotAnchor.FarLine, new Vector3(1.5f, 0f, 0f), 52f),
            new CameraShot(ShotAnchor.Actor, new Vector3(1.8f, -0.3f, -3.8f), ShotAnchor.FarLine, new Vector3(-1f, 1.4f, 0f), 50f),
        };

        [Tooltip("아군 전체·아군 단일 스킬: 카메라를 보고 선 아군 줄을 정면에서")]
        [SerializeField] private CameraShot teamLineShot = new CameraShot(ShotAnchor.Actor, new Vector3(0f, 0.5f, 7.5f), ShotAnchor.Actor, new Vector3(0f, 0f, 0f), 50f);

        [Tooltip("자신에게 쓰는 스킬: 행동 유닛을 정면에서")]
        [SerializeField] private CameraShot selfShot = new CameraShot(ShotAnchor.Actor, new Vector3(0.6f, 0.3f, 4.2f), ShotAnchor.Actor, new Vector3(0f, 0f, 0f), 45f);

        [Header("움직임")]
        [Tooltip("같은 무대 안에서 구도가 바뀔 때 거의 도착하는 데 걸리는 시간(초)")]
        [Label("이동 시간", Min = 0.05f)]
        [SerializeField] private float moveSeconds = 0.35f;

        [Tooltip("무대가 바뀔 때 어두워졌다 밝아지는 데 걸리는 시간(초, 각각)")]
        [Label("페이드 시간", Min = 0f)]
        [SerializeField] private float fadeSeconds = 0.12f;

        // 지금 무대 배치와 그 무대의 행동 유닛. 둘 중 하나라도 바뀌면 페이드 전환으로 무대를 다시 세운다.
        private StageLayout currentLayout;
        private BattleUnit stagedActor;
        private bool hasStaged;

        // 지금 향하는 구도
        private CameraShot currentShot;

        // 차례가 시작될 때 자동으로 골라져 있던 스킬 번호. 이 값에서 바뀌어야 "플레이어가 스킬을 눌렀다"고 보고 구도를 바꾼다.
        // (BattleManager는 차례가 오면 쓸 수 있는 첫 스킬을 미리 골라 두므로, 그것만으로는 구도를 바꾸지 않는다)
        private int turnStartSlot = -1;
        private bool hasPressedSkill;

        // SmoothDamp가 다음 프레임에 이어서 쓰는 현재 속도 값 (ref로 넘겨 함수가 갱신한다)
        private Vector3 moveVelocity;
        private float fovVelocity;

        // 진행 중인 페이드 전환. 새 전환이 오면 멈추고 새로 시작한다.
        private Coroutine transition;

        private void OnEnable()
        {
            battleManager.StateChanged += ChooseScene;
        }

        private void OnDisable()
        {
            battleManager.StateChanged -= ChooseScene;
        }

        // 지금 전투 상태에 맞는 무대 배치와 구도를 고른다.
        private void ChooseScene()
        {
            BattleUnit actor = battleManager.CurrentActor;

            // 전투 시작·종료이거나 적 차례: 평소 아군 시점에서 본다 (적이 때려도 아군 쪽 시점 그대로).
            if (battleManager.IsBattleOver || actor == null || !actor.IsAlly)
            {
                Apply(StageLayout.Overview, null, overviewShot);
                return;
            }

            // 새 아군 차례가 시작되면, 자동으로 골라진 스킬을 기억해 두고 기본 구도로 시작한다.
            if (actor != stagedActor || currentLayout == StageLayout.Overview)
            {
                turnStartSlot = battleManager.SelectedSkillSlot;
                hasPressedSkill = false;
            }

            // 스킬을 실행하는 중(입력 대기가 아님)에는 지금 장면을 그대로 둔다 → 아군 정면에서 버프가 들어가는 모습 등이 보인다.
            if (!battleManager.IsWaitingForPlayer)
            {
                if (actor != stagedActor)
                {
                    Apply(StageLayout.ActorFocus, actor, skillShots[0]);
                }

                return;
            }

            // 고른 스킬이 차례 시작 때와 달라졌으면 플레이어가 버튼을 누른 것이다.
            if (battleManager.SelectedSkillSlot != turnStartSlot)
            {
                hasPressedSkill = true;
            }

            if (!hasPressedSkill)
            {
                Apply(StageLayout.ActorFocus, actor, skillShots[0]);
                return;
            }

            ApplySkillScene(actor, battleManager.SelectedSkillSlot);
        }

        // 스킬의 대상 규칙에 따라 장면을 고른다. 아군을 고르는 스킬은 아군 정면, 자신은 정면, 적을 노리면 스킬 번호별 각도.
        private void ApplySkillScene(BattleUnit actor, int slot)
        {
            // Mathf.Clamp(값, 최소, 최대): 아직 스킬을 고르지 않았으면(-1) 0번 구도를 쓴다.
            int index = Mathf.Clamp(slot, 0, skillShots.Length - 1);
            SkillTargetType targetType = slot >= 0 && slot < CharacterData.SkillCount
                ? actor.GetSkill(slot).TargetType
                : SkillTargetType.EnemySingle;

            switch (targetType)
            {
                case SkillTargetType.AllyAll:
                case SkillTargetType.AllySingle:
                case SkillTargetType.AllySingleOrFallen:
                    Apply(StageLayout.TeamLine, actor, teamLineShot);
                    break;
                case SkillTargetType.Self:
                    Apply(StageLayout.ActorFocus, actor, selfShot);
                    break;
                default:
                    Apply(StageLayout.ActorFocus, actor, skillShots[index]);
                    break;
            }
        }

        // 무대 배치가 그대로면 구도만 부드럽게 바꾸고, 배치가 바뀌면 페이드로 덮은 사이에 무대를 다시 세우고 카메라를 바로 놓는다.
        private void Apply(StageLayout layout, BattleUnit actor, CameraShot shot)
        {
            currentShot = shot;
            bool sameStage = hasStaged && layout == currentLayout && actor == stagedActor;
            if (sameStage)
            {
                return;
            }

            currentLayout = layout;
            stagedActor = actor;

            // 처음 한 번은 페이드 없이 바로 세운다 (전투 시작 화면이 깜빡이지 않게).
            if (!hasStaged || fadeOverlay == null || fadeSeconds <= 0f)
            {
                hasStaged = true;
                Restage();
                return;
            }

            if (transition != null)
            {
                StopCoroutine(transition);
            }

            transition = StartCoroutine(FadeAndRestage());
        }

        // 코루틴: 화면을 어둡게 → 무대를 다시 세우고 카메라를 새 구도에 바로 놓음 → 다시 밝게.
        // yield return null: 한 프레임 쉬었다가 다음 프레임에 이어서 실행한다 (그동안 게임은 계속 진행됨).
        private IEnumerator FadeAndRestage()
        {
            yield return Fade(fadeOverlay.color.a, 1f);
            Restage();
            yield return Fade(1f, 0f);
            transition = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            for (float t = 0f; t < fadeSeconds; t += Time.deltaTime)
            {
                SetFadeAlpha(Mathf.Lerp(from, to, t / fadeSeconds));
                yield return null;
            }

            SetFadeAlpha(to);
        }

        private void SetFadeAlpha(float alpha)
        {
            Color color = fadeOverlay.color;
            color.a = alpha;
            fadeOverlay.color = color;
        }

        // 지금 무대 배치로 유닛을 세우고, 카메라를 지금 구도에 바로 놓는다 (부드러운 이동 속도도 초기화).
        private void Restage()
        {
            stageView.Stage(currentLayout, stagedActor);

            Vector3 position = GetShotPosition(currentShot);
            targetCamera.transform.SetPositionAndRotation(position, GetShotRotation(currentShot, position));
            targetCamera.fieldOfView = currentShot.FieldOfView;
            moveVelocity = Vector3.zero;
            fovVelocity = 0f;
        }

        // 매 프레임 지금 구도로 조금씩 옮긴다 (같은 무대 안에서 스킬을 바꿀 때 보이는 움직임).
        // LateUpdate: 모든 Update가 끝난 뒤 카메라를 움직여, 같은 프레임 안에서 흔들리지 않게 한다.
        private void LateUpdate()
        {
            // 페이드 중에는 움직이지 않는다 (덮인 사이에 Restage가 카메라를 바로 놓는다).
            if (currentShot == null || transition != null)
            {
                return;
            }

            Transform cameraTransform = targetCamera.transform;

            // Vector3.SmoothDamp(현재, 목표, ref 속도, 시간): 처음엔 빠르게, 도착할수록 느려지며 목표에 다가간다.
            cameraTransform.position = Vector3.SmoothDamp(cameraTransform.position, GetShotPosition(currentShot), ref moveVelocity, moveSeconds);

            // Quaternion.Slerp: 두 회전 사이를 비율만큼 부드럽게 섞는다. 1 - Exp(-속도 × 시간)은 프레임 속도와 상관없이 같은 빠르기로 따라가게 하는 비율.
            float follow = 1f - Mathf.Exp(-Time.deltaTime * 4f / moveSeconds);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, GetShotRotation(currentShot, cameraTransform.position), follow);

            targetCamera.fieldOfView = Mathf.SmoothDamp(targetCamera.fieldOfView, currentShot.FieldOfView, ref fovVelocity, moveSeconds);
        }

        private Vector3 GetShotPosition(CameraShot shot)
        {
            return GetAnchorPosition(shot.Anchor) + shot.Offset;
        }

        // 카메라 위치에서 바라볼 지점을 향하는 회전. Quaternion.LookRotation(방향): 그 방향을 앞(z)으로 보는 회전.
        private Quaternion GetShotRotation(CameraShot shot, Vector3 cameraPosition)
        {
            Vector3 lookPoint = GetAnchorPosition(shot.LookAnchor) + shot.LookOffset;
            return Quaternion.LookRotation(lookPoint - cameraPosition, Vector3.up);
        }

        private Vector3 GetAnchorPosition(ShotAnchor anchor)
        {
            return anchor == ShotAnchor.FarLine ? stageView.FarLineCenter : stageView.ActorPoint;
        }
    }
}
