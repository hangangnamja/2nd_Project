using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 공간 가르기 연출 (하이퍼캐리 변신 1스 파쇄, 임시). 레퍼런스: 붉고 흰 참격이 허공을 가르고, 갈라진 자리에서 공간이 검은 조각으로 부서진다.
    //  1) 대상마다(메인 → 양옆 순으로 조금씩 늦게) 화면을 마주 보는 평면에서 비스듬한 참격선이 순식간에 그어진다
    //     → 그어질 때마다 그 대상 피해의 일부(onImpact 비율)·붉은 테두리 검은 조각(공간 파편)·약한 흔들림
    //  2) 마무리: 대상 전원을 가로지르는 긴 참격 하나 → 남은 피해 전부(비율 1)·조각·강한 흔들림
    //  3) 참격선은 번쩍인 뒤 가늘어지며 사라진다
    // 자주 쓰는 1스라 카메라 컷 없이 짧게 끝낸다. 선 모양(재질·색·굵기 곡선)은 프리팹의 견본 선, 조각은 파티클 프리팹이 담당한다.
    public class SpaceRendFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("참격선 견본 (꺼 둔 자식 LineRenderer). 색 그라데이션·굵기 곡선은 여기서 정한다")]
        [SerializeField] private LineRenderer slashLine;

        [Tooltip("참격이 지나간 자리에서 부서지는 공간 조각·불꽃 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject shardPrefab;
        [SerializeField, Min(0.1f)] private float shardScale = 1f;
        [SerializeField, Min(0.1f)] private float finalShardScale = 1.6f;

        [Header("참격")]
        [Tooltip("대상 하나를 가르는 참격 수 (마무리 참격 제외)")]
        [SerializeField, Range(1, 6)] private int slashesPerTarget = 2;

        [Tooltip("참격 하나의 길이")]
        [SerializeField, Min(0.5f)] private float slashLength = 5f;

        [Tooltip("참격 기울기 범위 (화면 가로 기준 각도, 위·아래 무작위)")]
        [SerializeField, Range(0f, 89f)] private float minAngle = 20f;
        [SerializeField, Range(0f, 89f)] private float maxAngle = 65f;

        [Tooltip("참격선을 대상보다 카메라 쪽으로 당기는 거리 (캡슐에 가리지 않게)")]
        [SerializeField] private float towardCamera = 0.6f;

        [Tooltip("참격이 그어지는 시간 · 다음 참격까지 간격 · 대상끼리 시작 간격(초)")]
        [SerializeField, Min(0.01f)] private float drawSeconds = 0.06f;
        [SerializeField, Min(0f)] private float slashInterval = 0.09f;
        [SerializeField, Min(0f)] private float targetStagger = 0.07f;

        [Tooltip("참격 하나가 보여주는 피해 비율 (나머지는 마무리 참격)")]
        [SerializeField, Range(0.05f, 0.5f)] private float slashShare = 0.2f;

        [Header("마무리 참격")]
        [SerializeField, Min(0f)] private float finalDelay = 0.12f;
        [SerializeField, Min(0.01f)] private float finalDrawSeconds = 0.09f;
        [Tooltip("마무리 참격 길이 = 대상들이 퍼진 폭 + 이 여유")]
        [SerializeField, Min(0f)] private float finalExtraLength = 4f;
        [SerializeField, Range(0f, 45f)] private float finalAngle = 12f;
        [SerializeField, Min(0.1f)] private float finalWidthScale = 1.8f;

        [Header("빛·사라짐")]
        [SerializeField] private float drawIntensity = 3f;
        [SerializeField] private float fadeIntensity = 1f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.35f;
        [SerializeField, Min(0f)] private float slashShake = 0.07f;
        [SerializeField, Min(0f)] private float finalShake = 0.3f;

        // 화면 평면: 카메라의 오른쪽·위쪽 (참격선이 화면에 비스듬히 보이도록 이 평면 위에 긋는다).
        private Vector3 screenRight = Vector3.right;
        private Vector3 screenUp = Vector3.up;
        private Vector3 toCamera = Vector3.back;
        private int running;

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            Camera view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            if (view != null)
            {
                screenRight = view.transform.right;
                screenUp = view.transform.up;
                toCamera = -view.transform.forward;
            }

            // 대상마다 참격을 동시에(조금씩 늦게) 진행하고 모두 끝날 때까지 기다린다.
            for (int i = 0; i < targets.Count; i++)
            {
                StartCoroutine(SlashTarget(targets[i], targetStagger * i, onImpact, camera));
            }

            while (running > 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(finalDelay);
            yield return FinalSlash(targets, onImpact, camera);

            // 마지막 선이 사라질 시간만큼 기다린 뒤 지운다.
            while (running > 0)
            {
                yield return null;
            }

            Destroy(gameObject);
        }

        // 대상 하나: slashesPerTarget번 비스듬히 가른다. 가를 때마다 피해 일부·조각·흔들림.
        private IEnumerator SlashTarget((BattleUnit Unit, Vector3 Position) target, float delay, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            running++;
            yield return new WaitForSeconds(delay);
            Vector3 center = target.Position + toCamera * towardCamera;
            for (int i = 0; i < slashesPerTarget; i++)
            {
                // 참격마다 위로 기운 선·아래로 기운 선을 번갈아 → X자로 엇갈리게.
                float angle = UnityEngine.Random.Range(minAngle, maxAngle) * (i % 2 == 0 ? 1f : -1f);
                yield return DrawSlash(center, angle, slashLength, drawSeconds, 1f);
                Spawn(shardPrefab, center, shardScale);
                onImpact(target.Unit, slashShare);
                if (camera != null)
                {
                    camera.Shake(slashShake, 0.08f);
                }

                yield return new WaitForSeconds(slashInterval);
            }

            running--;
        }

        // 마무리: 대상들 가운데를 지나 양 끝 대상 너머까지 긴 참격 하나 → 대상 전원 남은 피해(비율 1 = 남은 것 전부).
        private IEnumerator FinalSlash(IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            if (targets.Count == 0)
            {
                yield break;
            }

            Vector3 center = Vector3.zero;
            foreach ((BattleUnit _, Vector3 position) in targets)
            {
                center += position;
            }

            center /= targets.Count;

            // 화면 가로 방향으로 대상들이 퍼진 폭 (Vector3.Dot: 그 방향 성분).
            float min = 0f;
            float max = 0f;
            foreach ((BattleUnit _, Vector3 position) in targets)
            {
                float across = Vector3.Dot(position - center, screenRight);
                min = Mathf.Min(min, across);
                max = Mathf.Max(max, across);
            }

            Vector3 lineCenter = center + toCamera * towardCamera + screenRight * ((min + max) * 0.5f);
            yield return DrawSlash(lineCenter, -finalAngle, max - min + finalExtraLength, finalDrawSeconds, finalWidthScale);

            foreach ((BattleUnit unit, Vector3 position) in targets)
            {
                Spawn(shardPrefab, position + toCamera * towardCamera, finalShardScale);
                onImpact(unit, 1f);
            }

            if (camera != null)
            {
                camera.Shake(finalShake, 0.3f);
            }
        }

        // 참격선 하나: 한쪽 끝에서 반대쪽 끝까지 drawSeconds 동안 그어지고(밝게), 그 뒤 따로 가늘어지며 사라진다.
        // 방향 = 화면 가로 × cos(각도) + 화면 세로 × sin(각도) → 화면에서 그 각도로 기운 선.
        private IEnumerator DrawSlash(Vector3 center, float angle, float length, float seconds, float widthScale)
        {
            float radians = angle * Mathf.Deg2Rad;
            Vector3 direction = screenRight * Mathf.Cos(radians) + screenUp * Mathf.Sin(radians);

            // 왼쪽→오른쪽 또는 오른쪽→왼쪽을 무작위로 (Random.value: 0~1 무작위).
            if (UnityEngine.Random.value < 0.5f)
            {
                direction = -direction;
            }

            Vector3 from = center - direction * (length * 0.5f);
            Vector3 to = center + direction * (length * 0.5f);
            var group = new GlowLines(slashLine, transform);
            LineRenderer line = group.Create(2);
            group.SetIntensity(drawIntensity);
            group.SetWidthScale(widthScale);

            yield return Animate(seconds, t =>
            {
                line.SetPosition(0, from);
                line.SetPosition(1, Vector3.Lerp(from, to, t));
            });

            StartCoroutine(FadeSlash(group, widthScale));
        }

        // 그은 선이 번쩍인 뒤 가늘어지고 투명해지며 사라진다.
        private IEnumerator FadeSlash(GlowLines group, float widthScale)
        {
            running++;
            yield return Animate(fadeSeconds, t =>
            {
                group.SetIntensity(Mathf.Lerp(drawIntensity, fadeIntensity, t));
                group.SetWidthScale(widthScale * Mathf.Lerp(1.3f, 0.1f, t));
                group.SetAlpha(1f - t * t);
            });

            foreach (LineRenderer line in group.Lines)
            {
                Destroy(line.gameObject);
            }

            running--;
        }

        private static void Spawn(GameObject prefab, Vector3 position, float scale)
        {
            if (prefab != null)
            {
                GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
                spawned.transform.localScale = Vector3.one * scale;
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
