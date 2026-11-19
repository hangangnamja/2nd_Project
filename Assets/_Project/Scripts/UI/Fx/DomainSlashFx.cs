using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 영역 공격 연출 (흉신 마라 영역 "참": 펼칠 때·영역 동안 턴이 끝날 때마다 적 전체, 임시).
    // 적군 쪽에 하얀 참격선이 여러 번 빠르게 그어지며 베인다. 참격 하나 = 그 대상 피해의 일부 (대상마다 여러 타).
    //  1) 대상들을 돌아가며(한 번에 한 대상씩, 아주 짧은 간격) 대상 몸을 비스듬히 가르는 하얀 선이 순식간에 그어진다
    //  2) 그어지는 순간 피해 일부·작은 혈흔·약한 흔들림 → 선은 번쩍인 뒤 가늘어지며 사라진다
    //  3) 대상마다 마지막 참격은 남은 피해 전부(비율 1), 전체 마지막 참격은 조금 더 센 흔들림
    // 매 턴 나가는 공격이라 카메라 컷 없이 지금 화면 그대로 짧게 끝낸다. 공통 타격 이펙트(펑)는 띄우지 않는다(혈흔·선이 대신함).
    // 선 모양(재질·색·굵기 곡선)은 프리팹의 견본 선, 혈흔은 파티클 프리팹이 맡고, 이 코드는 위치·각도·순서·시간만 정한다.
    public class DomainSlashFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("참격선 견본 (꺼 둔 자식 LineRenderer, 흰색·양 끝이 가는 굵기 곡선)")]
        [SerializeField] private LineRenderer slashLine;

        [Tooltip("베일 때 뿜어지는 피 (끝나면 스스로 지워지는 파티클 프리팹, +z로 뿜음). 비우면 생략")]
        [SerializeField] private GameObject bloodSprayPrefab;
        [SerializeField, Min(0.1f)] private float bloodScale = 0.6f;

        [Tooltip("체크하면 공통 타격 이펙트(펑 터지는 파티클)를 띄우지 않는다")]
        [SerializeField] private bool hideDefaultHitEffect = true;

        public override bool HidesDefaultHitEffect => hideDefaultHitEffect;

        // 펼칠 때의 공격은 마라 정면 장면(자기 대상 스킬)에서, 턴 종료 공격은 다음 장면으로 넘어가는 중에 나올 수 있다
        // → 연출 전에 마라 어깨 뒤 + 적 줄 장면으로 컷해 적을 잠깐 보여준 뒤 벤다 (BattleFxView가 처리, 이미 그 장면이면 컷 없음).
        public override bool CutsToOpponents => true;
        public override float OpponentRevealSeconds => revealSeconds;

        // 매 턴 나가는 영역의 공격이라 마라는 떠오르지 않고 제자리에 선 채 벤다.
        public override bool CasterCasts => false;

        [Header("참격")]
        [Tooltip("대상 하나가 맞는 참격 수 (= 그 대상 피해가 나뉘는 타수)")]
        [SerializeField, Range(1, 10)] private int slashesPerTarget = 4;

        [Tooltip("참격 하나의 길이 범위")]
        [SerializeField, Min(0.2f)] private float minLength = 2.2f;
        [SerializeField, Min(0.2f)] private float maxLength = 3.4f;

        [Tooltip("참격 가운데가 대상 몸 중심에서 벗어나는 범위 (화면 가로·세로)")]
        [SerializeField, Min(0f)] private float spreadX = 0.35f;
        [SerializeField, Min(0f)] private float spreadY = 0.6f;

        [Tooltip("참격선을 대상보다 카메라 쪽으로 당기는 거리 (캡슐에 가리지 않게)")]
        [SerializeField] private float towardCamera = 0.7f;

        [Tooltip("적 쪽 장면으로 컷한 뒤, 베기 전에 적을 보여주는 시간(초)")]
        [SerializeField, Min(0f)] private float revealSeconds = 0.45f;

        [Tooltip("참격 하나가 그어지는 시간 · 다음 참격까지 간격(초)")]
        [SerializeField, Min(0.005f)] private float drawSeconds = 0.03f;
        [SerializeField, Min(0f)] private float slashInterval = 0.045f;

        [Header("빛·사라짐")]
        [SerializeField] private float drawIntensity = 2.2f;
        [SerializeField] private float fadeIntensity = 0.8f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.25f;
        [SerializeField, Min(0f)] private float hitShake = 0.05f;
        [SerializeField, Min(0f)] private float finalShake = 0.2f;

        private const int SlashPoints = 12;

        private Vector3 screenRight = Vector3.right;
        private Vector3 screenUp = Vector3.up;
        private Vector3 toCamera = Vector3.back;
        private int fading;

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            if (targets.Count == 0)
            {
                Destroy(gameObject);
                yield break;
            }

            Camera view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            if (view != null)
            {
                screenRight = view.transform.right;
                screenUp = view.transform.up;
                toCamera = -view.transform.forward;
            }

            // 대상들을 돌아가며 벤다: 1번째 참격을 모든 대상에게 → 2번째를 모든 대상에게 … → 적군 전체가 연달아 베이는 느낌.
            // 대상마다 피해 비율: 마지막 참격은 1(남은 전부), 그 전은 1/참격 수씩.
            float share = 1f / slashesPerTarget;
            int total = slashesPerTarget * targets.Count;
            int index = 0;
            for (int round = 0; round < slashesPerTarget; round++)
            {
                bool isTargetLast = round == slashesPerTarget - 1;
                foreach ((BattleUnit unit, Vector3 position) in targets)
                {
                    index++;
                    yield return Slash(position);
                    SprayBlood(position);
                    onImpact(unit, isTargetLast ? 1f : share);
                    if (camera != null)
                    {
                        camera.Shake(index == total ? finalShake : hitShake, index == total ? 0.2f : 0.06f);
                    }

                    if (slashInterval > 0f)
                    {
                        yield return new WaitForSeconds(slashInterval);
                    }
                }
            }

            while (fading > 0)
            {
                yield return null;
            }

            Destroy(gameObject);
        }

        // 참격선 하나: 대상 몸 근처를 지나는 무작위 각도의 선이 drawSeconds 동안 한쪽 끝에서 반대쪽 끝까지 그어진다 (화면을 마주 보는 평면).
        // 방향 = 화면 가로 × cos + 화면 세로 × sin. 그어진 뒤에는 따로 번쩍 → 가늘어지며 사라진다.
        private IEnumerator Slash(Vector3 targetPosition)
        {
            Vector3 center = targetPosition + toCamera * towardCamera
                + screenRight * UnityEngine.Random.Range(-spreadX, spreadX)
                + screenUp * UnityEngine.Random.Range(-spreadY, spreadY);
            float angle = UnityEngine.Random.Range(0f, 180f) * Mathf.Deg2Rad;
            float length = UnityEngine.Random.Range(minLength, maxLength);
            Vector3 half = (screenRight * Mathf.Cos(angle) + screenUp * Mathf.Sin(angle)) * (length * 0.5f);
            Vector3 from = center - half;
            Vector3 to = center + half;

            // 굵기 곡선(양 끝 0 → 가운데 1)은 선의 점마다 굵기를 정하므로, 점이 양 끝 2개뿐이면 굵기가 0이라 안 보인다
            // → 선을 SlashPoints개 점으로 나눠 가운데가 굵게 그려지게 한다. DrawPartial: 점 목록의 앞쪽 t만큼만 그림 (그어지는 모습).
            var points = new Vector3[SlashPoints];
            for (int i = 0; i < SlashPoints; i++)
            {
                points[i] = Vector3.Lerp(from, to, i / (float)(SlashPoints - 1));
            }

            var lines = new GlowLines(slashLine, transform);
            LineRenderer line = lines.Create(SlashPoints);
            lines.SetIntensity(drawIntensity);

            float elapsed = 0f;
            while (elapsed < drawSeconds)
            {
                elapsed += Time.deltaTime;
                GlowLines.DrawPartial(line, points, Mathf.Clamp01(elapsed / drawSeconds));
                yield return null;
            }

            GlowLines.DrawPartial(line, points, 1f);
            StartCoroutine(Fade(lines));
        }

        // 그은 선이 번쩍인 뒤 가늘어지고 투명해지며 사라진다.
        private IEnumerator Fade(GlowLines lines)
        {
            fading++;
            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                lines.SetIntensity(Mathf.Lerp(drawIntensity, fadeIntensity, t));
                lines.SetWidthScale(Mathf.Lerp(1.2f, 0.1f, t));
                lines.SetAlpha(1f - t * t);
                yield return null;
            }

            foreach (LineRenderer line in lines.Lines)
            {
                Destroy(line.gameObject);
            }

            fading--;
        }

        // 베인 대상 몸에서 피가 조금 튄다 (위쪽·카메라 쪽 무작위 방향). Quaternion.LookRotation(방향): 프리팹의 +z가 그쪽을 보게.
        private void SprayBlood(Vector3 targetPosition)
        {
            if (bloodSprayPrefab == null)
            {
                return;
            }

            Vector3 direction = (UnityEngine.Random.onUnitSphere + Vector3.up * 0.6f + toCamera * 0.5f).normalized;
            GameObject spray = Instantiate(bloodSprayPrefab, targetPosition + toCamera * 0.3f, Quaternion.LookRotation(direction));
            spray.transform.localScale = Vector3.one * bloodScale;
        }
    }
}
