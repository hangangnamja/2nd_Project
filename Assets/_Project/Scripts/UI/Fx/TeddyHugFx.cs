using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 곰인형 포옹 연출 (곰인형 버퍼 3스, 임시). 아군 전원 뒤에 커다란 곰인형이 하나씩 나타나 꼭 안아 준다.
    //  1) 등장: 아군마다 등 뒤에 곰인형이 퐁 튀어나온다 (팔을 양옆으로 벌린 채, 아군마다 조금씩 늦게)
    //  2) 포옹: 벌린 팔을 앞으로 감싸 안고, 한 번 꼭 껴안으며 고개를 갸웃 기울이고 하트가 피어오른다
    //     (위아래로 반복해 들썩이는 움직임은 어색해 보여 쓰지 않는다. 곰인형은 정확히 등 뒤가 아니라 살짝 옆에 서서 어깨 너머로 안는다)
    //  3) 퇴장: 팔을 풀고 반짝임과 함께 작아지며 사라진다 → 연출이 끝나면 버프 이름이 뜬다 (BattleFxView가 버프 박자를 이어서 재생)
    // 곰인형 모양·재질은 프리팹의 꺼 둔 견본이, 하트·반짝임은 파티클 프리팹이 담당하고, 이 코드는 위치·순서·시간만 정한다.
    // 대상은 같은 편(AllyTargets)을 쓴다 (Play의 targets는 상대 편이라 비어 있음).
    public class TeddyHugFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("곰인형 견본 (꺼 둔 자식). 몸·머리·팔 모양과 재질은 여기서 정한다")]
        [SerializeField] private Transform teddyTemplate;

        [Tooltip("견본 안의 왼팔·오른팔 회전 축 (어깨). 이 축을 돌려 팔을 벌리고 감싼다")]
        [SerializeField] private string leftArmName = "ArmPivotL";
        [SerializeField] private string rightArmName = "ArmPivotR";

        [Tooltip("곰인형이 나타나고 사라질 때 터지는 반짝임 (비우면 생략)")]
        [SerializeField] private GameObject popPrefab;

        [Tooltip("포옹하는 동안 피어오르는 하트 (비우면 생략)")]
        [SerializeField] private GameObject heartPrefab;

        [Header("자리")]
        [Tooltip("아군 등 뒤로 떨어진 거리 (카메라 반대쪽)")]
        [SerializeField] private float behindDistance = 0.7f;

        [Tooltip("아군 옆으로 비켜 선 거리 (화면 오른쪽 +). 정확히 등 뒤에 붙지 않고 어깨 너머로 안는 느낌")]
        [SerializeField] private float sideOffset = 0.35f;

        [Tooltip("곰인형 발 높이 (아군 캡슐 중심 기준, 캡슐 아래 끝 = -1)")]
        [SerializeField] private float footHeight = -1f;

        [Tooltip("아군마다 등장이 늦어지는 간격(초)")]
        [SerializeField, Min(0f)] private float stagger = 0.08f;

        [Header("팔 각도")]
        [Tooltip("벌린 팔: 위로 들어 올린 각도")]
        [SerializeField] private float openRaise = 35f;

        [Tooltip("감싼 팔: 앞으로 감은 각도 · 살짝 내린 각도")]
        [SerializeField] private float hugWrap = 80f;
        [SerializeField] private float hugLower = -10f;

        [Header("시간")]
        [SerializeField, Min(0.05f)] private float appearSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float wrapSeconds = 0.3f;

        [Tooltip("꼭 안고 있는 시간(초). 이 동안 한 번 꼭 껴안는다")]
        [SerializeField, Min(0.05f)] private float squeezeSeconds = 0.9f;

        [Tooltip("꼭 껴안을 때 고르게 커지는 비율 (0.04 = 4%). 위아래로 눌리지 않고 전체가 살짝 커졌다 돌아온다")]
        [SerializeField, Range(0f, 0.3f)] private float squeezeAmount = 0.04f;

        [Tooltip("안는 동안 고개를 갸웃하듯 몸 전체를 기울이는 각도 (아군 쪽으로)")]
        [SerializeField, Range(0f, 30f)] private float tiltAngle = 12f;

        [SerializeField, Min(0.05f)] private float releaseSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float vanishSeconds = 0.25f;

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            Camera view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;

            // 아군마다 곰인형 한 마리씩 동시에(조금씩 늦게) 진행하고, 모두 끝날 때까지 기다린다.
            int running = 0;
            for (int i = 0; i < AllyTargets.Count; i++)
            {
                running++;
                StartCoroutine(HugOne(AllyTargets[i].Position, view, stagger * i, () => running--));
            }

            // 람다 () => running--: 곰인형 하나가 끝날 때마다 남은 수를 줄인다 → 0이 되면 전부 끝난 것.
            while (running > 0)
            {
                yield return null;
            }

            Destroy(gameObject);
        }

        // 곰인형 한 마리: 등장 → 감싸기 → 들썩이기 → 풀기 → 사라짐. 다 끝나면 onDone을 부른다.
        private IEnumerator HugOne(Vector3 ally, Camera view, float delay, Action onDone)
        {
            yield return new WaitForSeconds(delay);

            Transform teddy = SpawnTeddy(ally, view);
            Quaternion baseRotation = teddy.rotation;
            Transform leftArm = FindDeep(teddy, leftArmName);
            Transform rightArm = FindDeep(teddy, rightArmName);
            Vector3 fullScale = teddyTemplate.localScale;

            Pop(teddy.position + Vector3.up * 1.2f);
            yield return Appear(teddy, fullScale, leftArm, rightArm);
            yield return MoveArms(leftArm, rightArm, wrapSeconds, 0f, 1f);
            yield return Squeeze(teddy, fullScale, baseRotation, ally);
            yield return Release(teddy, baseRotation, leftArm, rightArm);
            Pop(teddy.position + Vector3.up * 1.2f);
            yield return Vanish(teddy, fullScale);

            Destroy(teddy.gameObject);
            onDone();
        }

        // 곰인형을 아군 등 뒤(카메라 반대쪽) 바닥에 세우고 카메라를 보게 한다.
        // toCamera: 아군 → 카메라 수평 방향. Quaternion.LookRotation(방향): 앞(z축)이 그 방향을 보는 회전.
        private Transform SpawnTeddy(Vector3 ally, Camera view)
        {
            Vector3 toCamera = view != null ? view.transform.position - ally : Vector3.back;
            toCamera.y = 0f;
            toCamera = toCamera.sqrMagnitude > 0.001f ? toCamera.normalized : Vector3.back;

            // 화면 오른쪽 = 위쪽과 카메라 방향 둘 다에 수직인 방향 (Vector3.Cross(카메라 방향, 위)).
            Vector3 screenRight = Vector3.Cross(toCamera, Vector3.up).normalized;

            Transform teddy = Instantiate(teddyTemplate, transform);
            teddy.position = ally - toCamera * behindDistance + screenRight * sideOffset + Vector3.up * footHeight;
            teddy.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            teddy.localScale = Vector3.zero;
            teddy.gameObject.SetActive(true);
            return teddy;
        }

        // 1) 등장: 크기 0 → 1로 튀어나오며(살짝 넘쳤다 돌아옴) 팔은 벌린 자세.
        private IEnumerator Appear(Transform teddy, Vector3 fullScale, Transform leftArm, Transform rightArm)
        {
            SetArms(leftArm, rightArm, 0f);
            yield return Animate(appearSeconds, t =>
            {
                // easeOutBack: 1 + 2.7(t-1)³ + 1.7(t-1)² → 끝에서 조금 넘쳤다 돌아오는 곡선.
                float u = t - 1f;
                teddy.localScale = fullScale * (1f + 2.7f * u * u * u + 1.7f * u * u);
            });
        }

        // 팔 자세를 from → to로 옮긴다 (0 = 벌림, 1 = 감쌈). SmoothStep: 처음과 끝이 부드러운 곡선.
        private IEnumerator MoveArms(Transform leftArm, Transform rightArm, float seconds, float from, float to)
        {
            yield return Animate(seconds, t => SetArms(leftArm, rightArm, Mathf.SmoothStep(from, to, t)));
        }

        // 팔 자세: hug 0 = 양옆으로 벌려 위로 든 팔, 1 = 앞으로 감아 안은 팔.
        // 어깨 축의 Euler(0, y, z): y = 앞으로 감는 각도, z = 위아래로 드는 각도. 왼팔과 오른팔은 좌우가 반대라 부호를 바꾼다.
        // (왼팔은 -x 방향으로 뻗어 있어 y를 +로 돌리면 앞(+z)으로, 오른팔은 +x 방향이라 -로 돌려야 앞으로 온다.)
        private void SetArms(Transform leftArm, Transform rightArm, float hug)
        {
            float wrap = Mathf.Lerp(0f, hugWrap, hug);
            float raise = Mathf.Lerp(openRaise, hugLower, hug);
            if (leftArm != null)
            {
                leftArm.localRotation = Quaternion.Euler(0f, wrap, -raise);
            }

            if (rightArm != null)
            {
                rightArm.localRotation = Quaternion.Euler(0f, -wrap, raise);
            }
        }

        // 2) 꼭 안기: 한 번 꼭 껴안는다 — 전체 크기가 고르게 살짝 커졌다 돌아오고, 그동안 몸을 아군 쪽(화면 왼쪽, 곰인형이 오른쪽에 서 있으므로)으로 기울인다.
        // Mathf.Sin(t × π): 0 → 1 → 0으로 한 번만 오르내리는 곡선 → 껴안는 세기. 가장 꼭 안는 순간(t = 0.5)에 하트를 띄운다.
        // Quaternion.AngleAxis(각도, 축): 곰인형의 앞 방향(카메라 쪽)을 축으로 돌리면 고개를 갸웃하듯 옆으로 기운다.
        private IEnumerator Squeeze(Transform teddy, Vector3 fullScale, Quaternion baseRotation, Vector3 ally)
        {
            bool heartShown = false;
            yield return Animate(squeezeSeconds, t =>
            {
                float hug = Mathf.Sin(t * Mathf.PI);
                teddy.localScale = fullScale * (1f + squeezeAmount * hug);

                // 기울기는 앞부분에 빨리 기울고 끝까지 유지했다가 팔을 풀 때 돌아오도록, 진행도를 3배 빠르게 채운다.
                float lean = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 3f));
                teddy.rotation = Quaternion.AngleAxis(tiltAngle * lean, baseRotation * Vector3.forward) * baseRotation;

                if (!heartShown && t >= 0.5f)
                {
                    heartShown = true;
                    SpawnHeart(ally + Vector3.up * 1.1f);
                }
            });

            teddy.localScale = fullScale;
        }

        // 팔을 풀면서 기울였던 몸을 똑바로 되돌린다. Quaternion.Slerp: 두 회전 사이를 부드럽게 잇는다.
        private IEnumerator Release(Transform teddy, Quaternion baseRotation, Transform leftArm, Transform rightArm)
        {
            Quaternion leaned = teddy.rotation;
            yield return Animate(releaseSeconds, t =>
            {
                float eased = Mathf.SmoothStep(0f, 1f, t);
                SetArms(leftArm, rightArm, 1f - eased);
                teddy.rotation = Quaternion.Slerp(leaned, baseRotation, eased);
            });
        }

        // 3) 사라짐: 살짝 부풀었다가 0으로 작아진다.
        private IEnumerator Vanish(Transform teddy, Vector3 fullScale)
        {
            yield return Animate(vanishSeconds, t =>
            {
                float size = t < 0.3f ? Mathf.Lerp(1f, 1.1f, t / 0.3f) : Mathf.Lerp(1.1f, 0f, (t - 0.3f) / 0.7f);
                teddy.localScale = fullScale * size;
            });
        }

        private void Pop(Vector3 position)
        {
            if (popPrefab != null)
            {
                Instantiate(popPrefab, position, Quaternion.identity);
            }
        }

        private void SpawnHeart(Vector3 position)
        {
            if (heartPrefab != null)
            {
                Instantiate(heartPrefab, position, Quaternion.identity);
            }
        }

        // 자식 중에서 이름이 같은 것을 깊이 상관없이 찾는다 (팔 축은 몸 아래에 들어 있을 수 있음). 없으면 null.
        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        // seconds 동안 0 → 1 진행도를 apply(람다)에 넘긴다 (전투 시간 기준이라 히트스톱이면 함께 멈춘다).
        private static IEnumerator Animate(float seconds, Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            apply(1f);
        }
    }
}
