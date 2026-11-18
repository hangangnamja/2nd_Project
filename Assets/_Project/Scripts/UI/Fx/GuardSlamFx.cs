using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 방패 문양 내려찍기 연출 (도발 탱커 2스 방어 태세, 임시).
    //  1) 등장: 탱커 머리 위에 빛나는 방패 문양이 동전처럼 돌며 커진다
    //  2) 기 모으기: 잠깐 떠서 점점 밝아진다
    //  (시작할 때 탱커 정면에서 조금 멀리 뺀 시점으로 컷 → 문양과 탱커가 한 화면에 다 보인다)
    //  3) 내려찍기: 문양이 탱커 앞쪽 땅으로 쾅 내리꽂히며 충격파·불꽃이 퍼지고 화면이 흔들린다
    //  4) 흡수: 문양이 작아지며 탱커 몸 가운데로 빨려 들어가고, 탱커 몸에서 빛이 번진다
    // 문양 모양은 프리팹의 꺼 둔 견본(발광 재질을 쓴 판)이, 충격파·흡수 빛은 파티클 프리팹이 담당한다.
    // 자기 강화 스킬이라 상대 타격이 없다 → BattleFxView가 연출이 끝날 때까지 기다린 뒤 버프 이름을 띄운다.
    public class GuardSlamFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("방패 문양 판 견본 (꺼 둔 자식). 재질의 밝기는 코드가 _BaseColor로 바꾼다")]
        [SerializeField] private Renderer emblemTemplate;

        [Tooltip("땅에 내리꽂히는 순간의 충격파·불꽃 (비우면 생략)")]
        [SerializeField] private GameObject slamPrefab;

        [Tooltip("문양이 몸에 흡수된 순간의 빛 (비우면 생략)")]
        [SerializeField] private GameObject absorbPrefab;

        [Header("자리 (탱커 캡슐 중심 기준)")]
        [SerializeField] private float hoverHeight = 2.4f;
        [SerializeField] private float groundHeight = -0.95f;

        [Tooltip("떠 있을 때 문양을 탱커보다 카메라 쪽으로 내민 거리 (탱커 몸에 가려지지 않게)")]
        [SerializeField] private float towardCamera = 1f;

        [Tooltip("문양이 꽂히는 자리: 탱커 앞(카메라 쪽)으로 떨어진 거리 → 탱커와 겹치지 않는다")]
        [SerializeField] private float slamForward = 1.6f;

        [Header("카메라 (탱커 기준, 앞 = 카메라 쪽)")]
        [Tooltip("탱커 정면에서 떨어진 거리·높이 (클수록 멀리서 넓게 보임)")]
        [SerializeField] private float viewDistance = 6.5f;
        [SerializeField] private float viewHeight = 1.2f;
        [SerializeField] private float viewSide = 0.8f;
        [SerializeField] private float lookHeight = 0.6f;
        [SerializeField, Range(20f, 90f)] private float viewFieldOfView = 45f;

        [Tooltip("문양 크기: 떠 있을 때 · 땅에 박혔을 때")]
        [SerializeField, Min(0.1f)] private float emblemSize = 1.6f;
        [SerializeField, Min(0.1f)] private float slamSize = 2.4f;

        [Header("시간")]
        [SerializeField, Min(0.05f)] private float appearSeconds = 0.4f;
        [SerializeField, Min(0f)] private float hoverSeconds = 0.3f;
        [SerializeField, Min(0.03f)] private float dropSeconds = 0.12f;
        [SerializeField, Min(0f)] private float stuckSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float absorbSeconds = 0.35f;

        [Header("빛·흔들림")]
        [Tooltip("문양 밝기: 평소 → 기 모으기 끝 → 내려찍은 순간 (재질 색에 곱함, 1보다 크면 Bloom으로 번짐)")]
        [SerializeField] private float idleIntensity = 1.2f;
        [SerializeField] private float chargeIntensity = 2.2f;
        [SerializeField] private float slamIntensity = 4f;
        [SerializeField, Min(0f)] private float slamShake = 0.35f;
        [SerializeField, Min(0f)] private float slamShakeSeconds = 0.3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private MaterialPropertyBlock block;
        private Renderer emblem;
        private Color baseColor;
        private BattleCameraDirector cinematicCamera;

        // 연출이 중간에 사라져도 카메라가 컷 시점에 남지 않게 되돌린다.
        private void OnDestroy()
        {
            if (cinematicCamera != null)
            {
                cinematicCamera.EndCinematic();
            }
        }

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            // 탱커가 바라보는 방향(정면)을 기준으로 잡는다. 화면 카메라 위치로 정하면, 차례 장면(탱커 어깨 뒤에서 적을 보는 구도)일 때
            // 정면이 적 쪽 반대로 뒤집혀 카메라가 적 무리를 비추게 된다.
            Vector3 toCamera = GetCasterFront();
            Vector3 front = caster + toCamera * towardCamera;

            emblem = Instantiate(emblemTemplate, transform);
            emblem.gameObject.SetActive(true);
            block = new MaterialPropertyBlock();
            baseColor = emblemTemplate.sharedMaterial.GetColor(BaseColorId);

            Vector3 hover = front + Vector3.up * hoverHeight;
            Vector3 ground = caster + toCamera * slamForward + Vector3.up * groundHeight;
            Quaternion facing = Quaternion.LookRotation(-toCamera, Vector3.up);
            BeginView(caster, toCamera, camera);

            yield return Appear(hover, facing);
            yield return Hover(hover, facing);
            yield return Drop(hover, ground, facing);
            Slam(ground, camera);
            yield return new WaitForSeconds(stuckSeconds);
            yield return Absorb(ground, caster, facing);

            Spawn(absorbPrefab, caster);
            yield return new WaitForSeconds(0.3f);
            if (camera != null)
            {
                camera.EndCinematic();
                cinematicCamera = null;
            }

            Destroy(gameObject);
        }

        // 탱커 정면에서 viewDistance만큼 떨어져 탱커를 마주 보는 자리로 컷 (적 무리는 카메라 뒤쪽이라 보이지 않는다).
        // 정면에서 viewDistance만큼 떨어지고, 옆으로 조금·위로 조금 비킨 자리로 컷. 탱커 발치~머리 위 문양이 다 보인다.
        // Vector3.Cross(위, 정면): 정면과 위쪽 둘 다에 수직인 옆 방향.
        private void BeginView(Vector3 caster, Vector3 toCamera, BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            Vector3 side = Vector3.Cross(Vector3.up, toCamera).normalized;
            var look = new GameObject("CinematicLook").transform;
            look.SetParent(transform, false);
            look.position = caster + toCamera * (slamForward * 0.5f) + Vector3.up * lookHeight;
            camera.BeginCinematic(caster + toCamera * viewDistance + side * viewSide + Vector3.up * viewHeight, look, viewFieldOfView);
            cinematicCamera = camera;
        }

        // 1) 등장: 크기 0 → 1, 세로축으로 한 바퀴 반 돌다가 정면에서 멈춘다 (1 - (1 - t)³: 점점 느려지는 곡선).
        private IEnumerator Appear(Vector3 hover, Quaternion facing)
        {
            yield return Animate(appearSeconds, t =>
            {
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                emblem.transform.position = hover;
                emblem.transform.rotation = facing * Quaternion.Euler(0f, 540f * (1f - eased), 0f);
                emblem.transform.localScale = Vector3.one * (emblemSize * eased);
                SetIntensity(idleIntensity * eased);
            });
        }

        // 2) 떠서 밝아지기: 살짝 위로 떠오르며(sin 반 바퀴) 밝기가 오른다.
        private IEnumerator Hover(Vector3 hover, Quaternion facing)
        {
            yield return Animate(hoverSeconds, t =>
            {
                emblem.transform.position = hover + Vector3.up * (0.15f * Mathf.Sin(t * Mathf.PI * 0.5f));
                SetIntensity(Mathf.Lerp(idleIntensity, chargeIntensity, t));
            });
        }

        // 3) 내려찍기: t²으로 점점 빨라지며 떨어지고, 앞으로 기울어 땅에 박히는 모양이 된다 (X축으로 최대 25도).
        private IEnumerator Drop(Vector3 hover, Vector3 ground, Quaternion facing)
        {
            Vector3 start = hover + Vector3.up * 0.15f;
            yield return Animate(dropSeconds, t =>
            {
                float fall = t * t;
                emblem.transform.position = Vector3.Lerp(start, ground + Vector3.up * (slamSize * 0.45f), fall);
                emblem.transform.rotation = facing * Quaternion.Euler(-25f * fall, 0f, 0f);
                emblem.transform.localScale = Vector3.one * Mathf.Lerp(emblemSize, slamSize, fall);
            });
        }

        // 쾅: 충격파·불꽃, 흔들림, 문양 번쩍.
        private void Slam(Vector3 ground, BattleCameraDirector camera)
        {
            Spawn(slamPrefab, ground);
            SetIntensity(slamIntensity);
            if (camera != null)
            {
                camera.Shake(slamShake, slamShakeSeconds);
            }
        }

        // 4) 흡수: 문양이 탱커 몸 가운데로 날아가며 작아지고(0까지), 밝기는 번쩍인 값에서 서서히 내려간다.
        private IEnumerator Absorb(Vector3 ground, Vector3 caster, Quaternion facing)
        {
            Vector3 start = emblem.transform.position;
            Quaternion tilted = emblem.transform.rotation;
            yield return Animate(absorbSeconds, t =>
            {
                float eased = t * t;
                emblem.transform.position = Vector3.Lerp(start, caster, eased);
                emblem.transform.rotation = Quaternion.Slerp(tilted, facing, t);
                emblem.transform.localScale = Vector3.one * Mathf.Lerp(slamSize, 0f, eased);
                SetIntensity(Mathf.Lerp(slamIntensity, chargeIntensity, t));
            });

            emblem.gameObject.SetActive(false);
        }

        // 재질 색 × 밝기 (MaterialPropertyBlock: 재질을 복사하지 않고 이 판에만 값을 바꾼다).
        private void SetIntensity(float intensity)
        {
            Color color = baseColor * intensity;
            color.a = baseColor.a;
            block.SetColor(BaseColorId, color);
            emblem.SetPropertyBlock(block);
        }

        // 탱커가 바라보는 수평 방향 (화면 표시 오브젝트의 앞 방향). 없으면 뒤(-z).
        private Vector3 GetCasterFront()
        {
            Vector3 forward = CasterView != null ? CasterView.transform.forward : Vector3.back;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.back;
        }

        private static void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab != null)
            {
                Instantiate(prefab, position, Quaternion.identity);
            }
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
