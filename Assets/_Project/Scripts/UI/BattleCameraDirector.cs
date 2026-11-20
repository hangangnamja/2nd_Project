using System;
using System.Collections;
using System.Collections.Generic;
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

        // 화면 흔들림: 세기, 지속 시간, 끝나는 시각(unscaledTime 기준), 지난 프레임에 더한 흔들림.
        private float shakeStrength;
        private float shakeSeconds = 1f;
        private float shakeEndTime;
        private Vector3 appliedShake;

        // 진행 중인 페이드 전환. 새 전환이 오면 멈추고 새로 시작한다.
        private Coroutine transition;

        // 연출 컷(시네마틱): 스킬 연출이 잠깐 다른 시점(예: 적 쪽에서 메테오를 올려다봄)을 보여주는 동안 true.
        // 그동안은 구도를 따라가지 않고, 정해진 자리에서 cinematicLook을 계속 바라본다.
        private bool isCinematic;

        // 연출 컷 중 카메라 자리 (MoveCinematic으로 연출이 천천히 밀고 들어갈 수 있게 따로 들고 있는다).
        private Vector3 cinematicPosition;
        private Transform cinematicLook;

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

        // 연출이 지금 화면에 없는 유닛의 행동(다른 아군의 추가 공격·패시브 등)을 보여줄 때 부른다: 그 유닛이 보이는 장면으로 바꾸고
        // 페이드가 끝날 때까지 기다린다 (BattleFxView가 yield return으로 기다린 뒤 모션을 재생).
        //  - 아군이 상대를 노림 → 그 아군 차례 장면(어깨 뒤, 멀리 적 줄)
        //  - 아군이 같은 편 여럿에게 → 아군 줄 정면 / 자신에게만 → 그 아군 정면
        //  - 적 → 평소 아군 시점(아군 줄 + 멀리 적 줄, 적이 모두 보임)
        // 행동이 끝나면 다음 차례의 StateChanged가 원래대로 장면을 고른다.
        public IEnumerator ShowActionBy(BattleUnit caster, bool targetsOpponents, bool targetsTeam)
        {
            if (!caster.IsAlly)
            {
                Apply(StageLayout.Overview, null, overviewShot);
            }
            else if (targetsOpponents)
            {
                Apply(StageLayout.ActorFocus, caster, skillShots[0]);
            }
            else if (targetsTeam)
            {
                Apply(StageLayout.TeamLine, caster, teamLineShot);
            }
            else
            {
                Apply(StageLayout.ActorFocus, caster, selfShot);
            }

            // transition: 진행 중인 페이드 전환. 끝날 때까지 한 프레임씩 기다린다.
            while (transition != null)
            {
                yield return null;
            }
        }

        // 아군 caster가 상대를 노리는 장면(어깨 뒤 + 멀리 상대 줄, 1스 구도)으로 페이드 없이 바로 컷한다.
        // 자기 정면 장면(자신에게 쓰는 스킬)에서 이어지는 광역 공격 연출(흉신 마라 영역 공격)이 상대를 보여주며 시작하도록 부른다.
        // 이미 caster의 차례 장면이고 자기 정면 구도(selfShot)가 아니면 상대가 보이므로 그대로 둔다 (스킬 구도 1~3 어느 것이든, 화면이 튀지 않게). 반환값: 실제로 컷했는지.
        // ReferenceEquals: 같은 구도 객체인지 비교 (CameraShot은 클래스라 같은 Inspector 설정이면 같은 객체).
        public bool CutToOpponentsOf(BattleUnit caster)
        {
            if (caster == null || !caster.IsAlly)
            {
                return false;
            }

            if (hasStaged && currentLayout == StageLayout.ActorFocus && stagedActor == caster && !ReferenceEquals(currentShot, selfShot))
            {
                return false;
            }

            if (transition != null)
            {
                StopCoroutine(transition);
                transition = null;
                if (fadeOverlay != null)
                {
                    SetFadeAlpha(0f);
                }
            }

            currentLayout = StageLayout.ActorFocus;
            stagedActor = caster;
            currentShot = skillShots[0];
            hasStaged = true;
            Restage();
            return true;
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

            // 카메라를 새 자리에 바로 놓았으므로, 지난 흔들림을 빼는 처리가 새 자리를 어긋나게 하지 않도록 비운다.
            appliedShake = Vector3.zero;
            moveVelocity = Vector3.zero;
            fovVelocity = 0f;
        }

        // 매 프레임 지금 구도로 조금씩 옮긴다 (같은 무대 안에서 스킬을 바꿀 때 보이는 움직임).
        // LateUpdate: 모든 Update가 끝난 뒤 카메라를 움직여, 같은 프레임 안에서 흔들리지 않게 한다.
        private void LateUpdate()
        {
            Transform cameraTransform = targetCamera.transform;

            // 지난 프레임에 더한 흔들림을 먼저 빼서, 아래 부드러운 이동이 흔들린 위치를 기준으로 계산하지 않게 한다.
            cameraTransform.position -= appliedShake;
            appliedShake = Vector3.zero;

            // 연출 컷 중: 자리는 그대로 두고 바라볼 대상만 따라간다 (떨어지는 메테오를 계속 화면 가운데에).
            if (isCinematic)
            {
                cameraTransform.position = cinematicPosition;

                // 대상이 사라졌으면(메테오 폭발) 마지막 방향을 유지한다. Unity 오브젝트는 지워지면 == null이 되므로 != null로 확인.
                if (cinematicLook != null)
                {
                    cameraTransform.rotation = Quaternion.LookRotation(cinematicLook.position - cameraTransform.position, Vector3.up);
                }

                ApplyShake(cameraTransform);
                return;
            }

            // 페이드 중에는 움직이지 않는다 (덮인 사이에 Restage가 카메라를 바로 놓는다).
            if (currentShot == null || transition != null)
            {
                return;
            }


            // Vector3.SmoothDamp(현재, 목표, ref 속도, 시간): 처음엔 빠르게, 도착할수록 느려지며 목표에 다가간다.
            cameraTransform.position = Vector3.SmoothDamp(cameraTransform.position, GetShotPosition(currentShot), ref moveVelocity, moveSeconds);

            // Quaternion.Slerp: 두 회전 사이를 비율만큼 부드럽게 섞는다. 1 - Exp(-속도 × 시간)은 프레임 속도와 상관없이 같은 빠르기로 따라가게 하는 비율.
            float follow = 1f - Mathf.Exp(-Time.deltaTime * 4f / moveSeconds);
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, GetShotRotation(currentShot, cameraTransform.position), follow);

            targetCamera.fieldOfView = Mathf.SmoothDamp(targetCamera.fieldOfView, currentShot.FieldOfView, ref fovVelocity, moveSeconds);

            ApplyShake(cameraTransform);
        }

        // 이 연출가가 움직이는 카메라 (연출이 카메라 쪽을 바라보게 놓을 때 쓴다).
        public Camera Camera => targetCamera;

        // 연출 컷 시작: 카메라를 position으로 바로 옮기고(컷) lookTarget을 계속 바라보게 한다. fieldOfView: 화각.
        // 무대 배치(유닛 자리)는 그대로라, 지금 장면의 유닛들을 다른 각도에서 보여주는 것이다.
        public void BeginCinematic(Vector3 position, Transform lookTarget, float fieldOfView)
        {
            isCinematic = true;
            cinematicLook = lookTarget;
            cinematicPosition = position;

            Transform cameraTransform = targetCamera.transform;
            cameraTransform.position = position;
            if (lookTarget != null)
            {
                cameraTransform.rotation = Quaternion.LookRotation(lookTarget.position - position, Vector3.up);
            }

            targetCamera.fieldOfView = fieldOfView;
            appliedShake = Vector3.zero;
        }

        // 연출 컷 중에 카메라 자리·화각을 바꾼다 (기를 모으는 동안 천천히 다가가기 등). 컷 중이 아니면 무시.
        public void MoveCinematic(Vector3 position, float fieldOfView)
        {
            if (!isCinematic)
            {
                return;
            }

            cinematicPosition = position;
            targetCamera.fieldOfView = fieldOfView;
        }

        // 연출 컷 끝: 원래 구도로 바로 돌아온다(컷). 부드럽게 따라가는 속도도 초기화한다.
        public void EndCinematic()
        {
            if (!isCinematic)
            {
                return;
            }

            isCinematic = false;
            cinematicLook = null;
            if (currentShot == null)
            {
                return;
            }

            Transform cameraTransform = targetCamera.transform;
            Vector3 position = GetShotPosition(currentShot);
            cameraTransform.SetPositionAndRotation(position, GetShotRotation(currentShot, position));
            targetCamera.fieldOfView = currentShot.FieldOfView;
            appliedShake = Vector3.zero;
            moveVelocity = Vector3.zero;
            fovVelocity = 0f;
        }

        // 화면 흔들림을 시작한다 (BattleFxView가 타격 세기에 맞춰 부름). 이미 더 센 흔들림 중이면 센 쪽을 유지한다.
        // strength: 흔들리는 최대 거리, seconds: 지속 시간. 시간은 timeScale과 무관하게 흘러 히트스톱 중에도 흔들린다.
        public void Shake(float strength, float seconds)
        {
            if (strength < shakeStrength * GetShakeRemainRatio())
            {
                return;
            }

            shakeStrength = strength;
            shakeSeconds = Mathf.Max(0.01f, seconds);
            shakeEndTime = Time.unscaledTime + shakeSeconds;
        }

        // 남은 흔들림 비율 (1 = 막 시작, 0 = 끝).
        private float GetShakeRemainRatio()
        {
            return Mathf.Clamp01((shakeEndTime - Time.unscaledTime) / shakeSeconds);
        }

        // 카메라 위치에 이번 프레임의 흔들림을 더한다. 남은 비율이 줄수록 약해진다.
        // Random.insideUnitSphere: 반지름 1 공 안의 무작위 점 → 매 프레임 다른 방향으로 떨린다.
        private void ApplyShake(Transform cameraTransform)
        {
            float remain = GetShakeRemainRatio();
            if (remain <= 0f)
            {
                return;
            }

            appliedShake = UnityEngine.Random.insideUnitSphere * (shakeStrength * remain);
            cameraTransform.position += appliedShake;
        }

        // 구도의 카메라 자리. 상대 줄을 담는 구도면 상대 줄에 선 큰 모델에 맞춰 뒤로 뺀다:
        // 자리 = 바라볼 곳 + (원래 자리 - 바라볼 곳) × 배율 → 같은 방향에서 거리만 배율만큼 늘어나 구도(각도)는 그대로, 화면만 넓게 담긴다.
        // 큰 보스가 쓰러지거나 웨이브가 바뀌면 배율이 1로 돌아와 LateUpdate의 SmoothDamp가 부드럽게 다시 다가간다.
        private Vector3 GetShotPosition(CameraShot shot)
        {
            Vector3 position = GetAnchorPosition(shot.Anchor) + shot.Offset;
            // 상대 줄을 담는 구도 = 평소 시점 또는 차례 장면의 스킬 구도 1~3 (Array.IndexOf: 목록에 있으면 0 이상, 없으면 -1).
            // 아군 줄 정면·자기 정면 구도는 상대가 안 보이므로 그대로 둔다.
            bool looksAtOpponents = ReferenceEquals(shot, overviewShot) || Array.IndexOf(skillShots, shot) >= 0;
            if (!looksAtOpponents)
            {
                return position;
            }

            Vector3 lookPoint = GetAnchorPosition(shot.LookAnchor) + shot.LookOffset;
            return lookPoint + (position - lookPoint) * GetOpponentDistanceScale();
        }

        // 지금 장면에서 멀리 선 상대 줄(아군 차례면 적, 평소 시점이면 적)의 유닛 중 가장 큰 카메라 거리 배율.
        // 전장에 있고 화면에 보이는 유닛만 센다 (쓰러진 보스·숨겨진 유닛은 빼고, 화면 생사 ShownHp로 쓰러짐 연출이 끝난 뒤 다가감).
        private float GetOpponentDistanceScale()
        {
            IReadOnlyList<BattleUnit> opponents = stagedActor != null && !stagedActor.IsAlly ? battleManager.Allies : battleManager.Enemies;
            float scale = 1f;
            foreach (BattleUnit unit in opponents)
            {
                UnitView view = unit.IsOnField && !ShownHp.IsDead(unit) ? stageView.FindView(unit) : null;
                if (view != null && view.gameObject.activeInHierarchy)
                {
                    scale = Mathf.Max(scale, view.CameraDistanceScale);
                }
            }

            return scale;
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
