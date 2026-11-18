using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 곰돌이 응원단 연출 (곰인형 버퍼 3스, 임시). 아군 줄 앞에 폼폼을 든 곰인형 여러 마리가 나타나 춤추며 응원한다.
    //  1) 등장: 아군 줄 앞에 곰인형들이 한 줄로 왼쪽부터 차례로 퐁 튀어나온다
    //  2) 춤: 박자마다 통통 뛰고 몸을 좌우로 흔들며 폼폼 든 팔을 번갈아 치켜든다. 곰인형마다 박자를 조금씩 늦춰 파도처럼 보인다
    //  3) 마무리: 다 같이 두 팔을 번쩍 들고 크게 뛰어오르며 반짝임·하트가 터진다
    //  4) 퇴장: 반짝임과 함께 작아지며 사라짐 → 그다음 버프 이름이 뜬다 (BattleFxView가 버프 박자를 이어서 재생)
    // 곰인형·폼폼 모양은 프리팹의 꺼 둔 견본이, 반짝임·하트는 파티클 프리팹이 담당하고, 이 코드는 위치·순서·시간만 정한다.
    // 위치는 같은 편 대상(AllyTargets)으로 정한다 (Play의 targets는 상대 편이라 비어 있음).
    public class TeddyCheerFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("곰인형 견본 (꺼 둔 자식, 폼폼 포함)")]
        [SerializeField] private Transform teddyTemplate;
        [SerializeField] private string leftArmName = "ArmPivotL";
        [SerializeField] private string rightArmName = "ArmPivotR";

        [Tooltip("곰인형이 나타나고 사라질 때 터지는 반짝임 (비우면 생략)")]
        [SerializeField] private GameObject popPrefab;

        [Tooltip("마무리 때 곰인형마다 터지는 하트 (비우면 생략)")]
        [SerializeField] private GameObject heartPrefab;

        [Header("줄 세우기")]
        [Tooltip("곰인형 수")]
        [SerializeField, Range(1, 9)] private int teddyCount = 5;

        [Tooltip("아군 줄보다 카메라 쪽으로 나온 거리")]
        [SerializeField] private float frontDistance = 1.6f;

        [Tooltip("곰인형 줄의 너비 = 아군 줄 너비 + 이 여유 (양쪽 합)")]
        [SerializeField, Min(0f)] private float extraWidth = 1f;

        [Tooltip("곰인형 발 높이 (아군 캡슐 중심 기준, 캡슐 아래 끝 = -1)")]
        [SerializeField] private float footHeight = -1f;

        [Tooltip("곰인형마다 등장이 늦어지는 간격(초, 왼쪽부터)")]
        [SerializeField, Min(0f)] private float appearStagger = 0.06f;

        [Header("춤")]
        [Tooltip("박자 하나의 길이(초)")]
        [SerializeField, Min(0.1f)] private float beatSeconds = 0.42f;

        [Tooltip("춤추는 박자 수 (마무리 제외)")]
        [SerializeField, Range(1, 12)] private int beats = 4;

        [Tooltip("곰인형마다 박자가 늦어지는 비율 (0.15 = 박자의 15%씩) → 파도처럼")]
        [SerializeField, Range(0f, 0.5f)] private float wavePhase = 0.15f;

        [Tooltip("박자마다 뛰는 높이")]
        [SerializeField, Min(0f)] private float hopHeight = 0.25f;

        [Tooltip("좌우로 흔드는 각도")]
        [SerializeField, Range(0f, 30f)] private float swayAngle = 10f;

        [Tooltip("팔 높이: 내린 팔 ~ 치켜든 팔 (0 = 옆으로 수평, 90 = 머리 위로 수직. 90을 많이 넘기면 팔이 머리 너머로 넘어감)")]
        [SerializeField] private float armLow = -40f;
        [SerializeField] private float armHigh = 100f;

        [Header("마무리·퇴장")]
        [SerializeField, Min(0.1f)] private float finaleSeconds = 0.6f;
        [SerializeField, Min(0f)] private float finaleJump = 0.6f;
        [SerializeField, Min(0.05f)] private float appearSeconds = 0.3f;
        [SerializeField, Min(0.05f)] private float vanishSeconds = 0.25f;

        // 곰인형 한 마리의 자리·팔·박자 늦춤.
        private class Dancer
        {
            public Transform Body;
            public Transform LeftArm;
            public Transform RightArm;
            public Vector3 Home;
            public Quaternion Facing;
            public float Phase;
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            Camera view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            List<Dancer> dancers = LineUp(view, caster);
            Vector3 fullScale = teddyTemplate.localScale;

            yield return Appear(dancers, fullScale);
            yield return Dance(dancers);
            yield return Finale(dancers);
            yield return Vanish(dancers, fullScale);
            Destroy(gameObject);
        }

        // 곰인형들을 아군 줄 앞(카메라 쪽)에 가로로 고르게 세우고 카메라를 보게 한다.
        //  - 가로 방향(screenRight) = 위쪽과 카메라 방향 둘 다에 수직 (Vector3.Cross)
        //  - 줄 너비 = 아군들을 가로 방향에 투영한 폭(Vector3.Dot: 그 방향 성분) + 여유
        private List<Dancer> LineUp(Camera view, Vector3 caster)
        {
            Vector3 center = GetAllyCenter(caster);
            Vector3 toCamera = view != null ? view.transform.position - center : Vector3.back;
            toCamera.y = 0f;
            toCamera = toCamera.sqrMagnitude > 0.001f ? toCamera.normalized : Vector3.back;
            Vector3 screenRight = Vector3.Cross(toCamera, Vector3.up).normalized;

            float min = 0f;
            float max = 0f;
            foreach ((BattleUnit _, Vector3 position) in AllyTargets)
            {
                float across = Vector3.Dot(position - center, screenRight);
                min = Mathf.Min(min, across);
                max = Mathf.Max(max, across);
            }

            float width = max - min + extraWidth;
            Vector3 rowCenter = center + toCamera * frontDistance + Vector3.up * footHeight;
            Quaternion facing = Quaternion.LookRotation(toCamera, Vector3.up);

            var dancers = new List<Dancer>();
            for (int i = 0; i < teddyCount; i++)
            {
                // 왼쪽 끝(-0.5) ~ 오른쪽 끝(+0.5) 사이를 고르게 나눈 자리. 1마리면 가운데.
                float slot = teddyCount == 1 ? 0f : i / (float)(teddyCount - 1) - 0.5f;
                Transform body = Instantiate(teddyTemplate, transform);
                body.localScale = Vector3.zero;
                body.gameObject.SetActive(true);
                var dancer = new Dancer
                {
                    Body = body,
                    LeftArm = FindDeep(body, leftArmName),
                    RightArm = FindDeep(body, rightArmName),
                    Home = rowCenter + screenRight * (slot * width),
                    Facing = facing,
                    Phase = i * wavePhase,
                };
                body.SetPositionAndRotation(dancer.Home, facing);
                dancers.Add(dancer);
            }

            return dancers;
        }

        // 1) 등장: 왼쪽부터 appearStagger 간격으로 퐁 튀어나온다 (easeOutBack: 살짝 넘쳤다 돌아오는 곡선).
        private IEnumerator Appear(List<Dancer> dancers, Vector3 fullScale)
        {
            float total = appearSeconds + appearStagger * (dancers.Count - 1);
            var popped = new bool[dancers.Count];
            yield return Animate(total, t =>
            {
                float now = t * total;
                for (int i = 0; i < dancers.Count; i++)
                {
                    float local = Mathf.Clamp01((now - appearStagger * i) / appearSeconds);
                    if (local > 0f && !popped[i])
                    {
                        popped[i] = true;
                        Spawn(popPrefab, dancers[i].Home + Vector3.up * 0.8f);
                    }

                    float u = local - 1f;
                    dancers[i].Body.localScale = local <= 0f ? Vector3.zero : fullScale * (1f + 2.7f * u * u * u + 1.7f * u * u);
                    SetArms(dancers[i], 0.5f, 0.5f);
                }
            });
        }

        // 2) 춤: 곰인형마다 자기 박자(늦춤 포함)로 뛰고·흔들고·팔을 번갈아 든다.
        private IEnumerator Dance(List<Dancer> dancers)
        {
            float total = beatSeconds * beats;
            yield return Animate(total, t =>
            {
                float time = t * total;
                foreach (Dancer dancer in dancers)
                {
                    // beat: 지금까지 지난 박자 수 (곰인형마다 Phase만큼 늦게 시작). 음수면 아직 시작 전 → 0.
                    float beat = Mathf.Max(0f, time / beatSeconds - dancer.Phase);
                    PoseDancer(dancer, beat);
                }
            });
        }

        // 박자 위치 beat에서의 자세.
        //  - 뛰기: |sin(π × beat)| → 박자마다 한 번 떴다 내려옴
        //  - 흔들기: sin(π × beat) → 두 박자에 한 번 왼쪽·오른쪽 (앞 방향 축으로 기울임)
        //  - 팔: 홀수 박자엔 왼팔, 짝수 박자엔 오른팔을 치켜든다 (cos(π × beat)를 0~1로 바꿔 번갈아)
        private void PoseDancer(Dancer dancer, float beat)
        {
            float wave = Mathf.Sin(beat * Mathf.PI);
            dancer.Body.position = dancer.Home + Vector3.up * (hopHeight * Mathf.Abs(wave));
            dancer.Body.rotation = Quaternion.AngleAxis(swayAngle * wave, dancer.Facing * Vector3.forward) * dancer.Facing;

            float alternate = (Mathf.Cos(beat * Mathf.PI) + 1f) * 0.5f;
            SetArms(dancer, alternate, 1f - alternate);
        }

        // 3) 마무리: 두 팔을 번쩍 들고 크게 뛰어올랐다 내려오며, 가장 높이 뜬 순간 반짝임·하트.
        private IEnumerator Finale(List<Dancer> dancers)
        {
            bool burst = false;
            var startRotations = new Quaternion[dancers.Count];
            for (int i = 0; i < dancers.Count; i++)
            {
                startRotations[i] = dancers[i].Body.rotation;
            }

            yield return Animate(finaleSeconds, t =>
            {
                // 4t(1 - t): t = 0.5에서 1이 되는 포물선 → 점프 높이.
                float jump = 4f * t * (1f - t);
                float lift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 3f));
                for (int i = 0; i < dancers.Count; i++)
                {
                    Dancer dancer = dancers[i];
                    dancer.Body.position = dancer.Home + Vector3.up * (finaleJump * jump);
                    dancer.Body.rotation = Quaternion.Slerp(startRotations[i], dancer.Facing, lift);
                    SetArms(dancer, lift, lift);
                }

                if (!burst && t >= 0.5f)
                {
                    burst = true;
                    foreach (Dancer dancer in dancers)
                    {
                        Spawn(popPrefab, dancer.Body.position + Vector3.up * 1.2f);
                        Spawn(heartPrefab, dancer.Body.position + Vector3.up * 1.6f);
                    }
                }
            });
        }

        // 4) 퇴장: 두 팔을 든 채 작아지며 사라진다.
        private IEnumerator Vanish(List<Dancer> dancers, Vector3 fullScale)
        {
            yield return Animate(vanishSeconds, t =>
            {
                foreach (Dancer dancer in dancers)
                {
                    dancer.Body.localScale = fullScale * (1f - t);
                }
            });

            foreach (Dancer dancer in dancers)
            {
                Destroy(dancer.Body.gameObject);
            }
        }

        // 팔 높이: 0 = 내린 팔(armLow), 1 = 치켜든 팔(armHigh). 어깨 축을 앞 방향 축(z)으로 돌려 위아래로 든다.
        // 왼팔은 -x로 뻗어 있어 z를 -로, 오른팔은 +x로 뻗어 있어 +로 돌려야 위로 올라간다.
        private void SetArms(Dancer dancer, float left, float right)
        {
            if (dancer.LeftArm != null)
            {
                dancer.LeftArm.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(armLow, armHigh, left));
            }

            if (dancer.RightArm != null)
            {
                dancer.RightArm.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(armLow, armHigh, right));
            }
        }

        // 아군 대상들의 가운데. 대상이 없으면 시전자 자리.
        private Vector3 GetAllyCenter(Vector3 caster)
        {
            if (AllyTargets.Count == 0)
            {
                return caster;
            }

            Vector3 sum = Vector3.zero;
            foreach ((BattleUnit _, Vector3 position) in AllyTargets)
            {
                sum += position;
            }

            return sum / AllyTargets.Count;
        }

        private static void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab != null)
            {
                Instantiate(prefab, position, Quaternion.identity);
            }
        }

        // 자식 중에서 이름이 같은 것을 깊이 상관없이 찾는다. 없으면 null.
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
