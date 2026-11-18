using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 독 플라스크 연출 (독 서브딜러 3스, 임시). 레퍼런스: 녹색 독이 든 플라스크를 적진에 던져 깨뜨리고, 녹색 연기가 적진에 퍼진다.
    //  1) 시전자 손 위에 해골 라벨이 붙은 둥근 플라스크(목이 긴 둥근 바닥 플라스크)가 나타난다
    //  2) 던지기: 플라스크가 빙글빙글 돌며 포물선을 그려 적 무리 가운데 바닥으로 날아간다
    //  3) 깨짐: 유리 파편·독 방울이 튀고 약한 흔들림
    //  4) 독 연기: 깨진 자리에서 녹색 연기가 바깥으로 퍼져 나가고, 연기가 닿는 적마다 피해(onImpact 비율 1)
    //  5) 연기가 잠시 깔렸다가 걷힌다 (연기 파티클이 스스로 사라짐)
    // 플라스크 모양·재질은 프리팹의 꺼 둔 견본이, 파편·연기는 파티클 프리팹이 담당하고, 이 코드는 위치·순서·시간만 정한다.
    public class PoisonFlaskFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("던질 플라스크 견본 (꺼 둔 자식). 유리·독·해골 라벨 모양은 여기서 정한다")]
        [SerializeField] private Transform flaskTemplate;

        [Tooltip("깨지는 순간의 유리 파편·독 방울 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject shatterPrefab;

        [Tooltip("퍼지는 독 연기 한 뭉치 (끝나면 스스로 지워지는 프리팹)")]
        [SerializeField] private GameObject smokePrefab;

        [Tooltip("연기가 닿은 적에게 터지는 작은 독 이펙트 (비우면 생략)")]
        [SerializeField] private GameObject hitPrefab;

        [Header("들기·던지기")]
        [Tooltip("플라스크가 나타나는 자리 (시전자 중심 기준: 위로, 적 쪽으로)")]
        [SerializeField] private float holdHeight = 1.3f;
        [SerializeField] private float holdForward = 0.5f;

        [Tooltip("손 위에서 플라스크를 보여주는 시간(초)")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.35f;

        [Tooltip("날아가는 시간(초)")]
        [SerializeField, Min(0.05f)] private float throwSeconds = 0.6f;

        [Tooltip("포물선 꼭대기 높이 (출발·도착 중 높은 쪽 기준 더 올라가는 높이)")]
        [SerializeField, Min(0f)] private float arcHeight = 3f;

        [Tooltip("날아가며 도는 바퀴 수")]
        [SerializeField, Min(0f)] private float spinTurns = 2.5f;

        [Tooltip("떨어질 바닥 높이 (적 캡슐 중심 기준, 캡슐 아래 끝 = -1)")]
        [SerializeField] private float groundHeight = -0.9f;

        [Header("깨짐")]
        [SerializeField, Min(0.1f)] private float shatterScale = 1f;
        [SerializeField, Min(0f)] private float shatterShake = 0.15f;
        [SerializeField, Min(0f)] private float shatterShakeSeconds = 0.2f;

        [Header("독 연기")]
        [Tooltip("연기가 가장 먼 적까지 퍼지는 시간(초)")]
        [SerializeField, Min(0.05f)] private float spreadSeconds = 0.7f;

        [Tooltip("퍼지며 남기는 연기 뭉치 수 (가운데부터 바깥으로 고르게)")]
        [SerializeField, Range(4, 60)] private int smokePuffs = 22;

        [Tooltip("연기가 퍼지는 반지름 = 가장 먼 적까지의 거리 + 이 여유")]
        [SerializeField, Min(0f)] private float spreadMargin = 1.2f;

        [Tooltip("연기 뭉치 높이 (바닥 기준)")]
        [SerializeField] private float smokeHeight = 0.6f;

        [SerializeField, Min(0f)] private float hitShake = 0.08f;

        [Tooltip("연기가 다 퍼진 뒤 다음으로 넘어가기 전 기다리는 시간(초)")]
        [SerializeField, Min(0f)] private float lingerSeconds = 0.5f;

        public override IEnumerator Play(
            Vector3 caster,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            Vector3 landing = GetCenter(targets) + Vector3.up * groundHeight;

            // 적 쪽 수평 방향: 손 위 자리를 적 쪽으로 조금 내밀 때 쓴다.
            Vector3 toTargets = landing - caster;
            toTargets.y = 0f;
            toTargets = toTargets.sqrMagnitude > 0.001f ? toTargets.normalized : Vector3.forward;

            Transform flask = Instantiate(flaskTemplate, transform);
            flask.gameObject.SetActive(true);
            Vector3 hold = caster + Vector3.up * holdHeight + toTargets * holdForward;
            yield return ShowInHand(flask, hold);
            yield return Throw(flask, hold, landing, toTargets);

            Destroy(flask.gameObject);
            Shatter(landing, camera);
            yield return SpreadSmoke(landing, targets, onImpact, camera);
            yield return new WaitForSeconds(lingerSeconds);
            Destroy(gameObject);
        }

        // 1) 손 위에 나타남: 크기 0 → 1로 살짝 튀며 커지고, 들고 있는 동안 좌우로 조금 흔들린다.
        private IEnumerator ShowInHand(Transform flask, Vector3 hold)
        {
            Vector3 fullScale = flaskTemplate.localScale;
            yield return Animate(holdSeconds, t =>
            {
                // 1 + 2.7(t-1)³ + 1.7(t-1)²: 끝에서 조금 넘쳤다 돌아오는 곡선 (easeOutBack) → 튀어나오는 느낌.
                float u = t - 1f;
                float pop = 1f + 2.7f * u * u * u + 1.7f * u * u;
                flask.position = hold;
                flask.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 3f) * 12f);
                flask.localScale = fullScale * pop;
            });
        }

        // 2) 던지기: 출발 → 도착을 직선으로 잇고, 위로 4 × 높이 × t(1 - t)만큼 들어 올려 포물선을 만든다 (t = 0.5에서 가장 높음).
        // 도는 축 = 날아가는 방향과 위쪽 둘 다에 수직(Cross) → 앞으로 공중제비 돌듯 회전한다.
        private IEnumerator Throw(Transform flask, Vector3 from, Vector3 to, Vector3 toTargets)
        {
            float peak = Mathf.Max(0f, Mathf.Max(from.y, to.y) - Mathf.Min(from.y, to.y)) + arcHeight;
            Vector3 spinAxis = Vector3.Cross(Vector3.up, toTargets).normalized;
            yield return Animate(throwSeconds, t =>
            {
                Vector3 straight = Vector3.Lerp(from, to, t);
                flask.position = straight + Vector3.up * (4f * peak * t * (1f - t));
                flask.rotation = Quaternion.AngleAxis(spinTurns * 360f * t, spinAxis);
            });
        }

        // 3) 깨짐: 바닥에 유리 파편·독 방울이 튀고 약하게 흔들린다.
        private void Shatter(Vector3 landing, BattleCameraDirector camera)
        {
            if (shatterPrefab != null)
            {
                GameObject burst = Instantiate(shatterPrefab, landing, Quaternion.identity);
                burst.transform.localScale = Vector3.one * shatterScale;
            }

            if (camera != null)
            {
                camera.Shake(shatterShake, shatterShakeSeconds);
            }
        }

        // 4) 독 연기: 깨진 자리에서 반지름이 0 → 최대로 커지는 동안 그 둘레에 연기 뭉치를 남긴다.
        // 적마다 "깨진 자리와의 거리"에 연기가 닿는 순간 피해를 보여준다 → 가까운 적부터 차례로 맞는다.
        private IEnumerator SpreadSmoke(
            Vector3 landing,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            float maxRadius = spreadMargin;
            var distances = new float[targets.Count];
            for (int i = 0; i < targets.Count; i++)
            {
                distances[i] = FlatDistance(landing, targets[i].Position);
                maxRadius = Mathf.Max(maxRadius, distances[i] + spreadMargin);
            }

            var hit = new bool[targets.Count];
            int puffsSpawned = 0;
            float elapsed = 0f;
            while (elapsed < spreadSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / spreadSeconds);

                // 1 - (1 - t)²: 처음에 확 퍼졌다가 점점 느려지는 곡선.
                float radius = maxRadius * (1f - (1f - t) * (1f - t));

                // 지금까지의 진행도만큼 연기 뭉치를 남긴다 (CeilToInt: 올림 → 첫 프레임에도 1개는 나온다).
                int wanted = Mathf.Min(smokePuffs, Mathf.CeilToInt(smokePuffs * t));
                while (puffsSpawned < wanted)
                {
                    SpawnSmoke(landing, radius * UnityEngine.Random.Range(0.6f, 1f));
                    puffsSpawned++;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    if (!hit[i] && radius >= distances[i])
                    {
                        hit[i] = true;
                        HitTarget(targets[i], onImpact, camera);
                    }
                }

                yield return null;
            }

            // 시간이 다 됐는데 못 맞힌 적(반지름 계산 오차)이 없도록 마무리.
            for (int i = 0; i < targets.Count; i++)
            {
                if (!hit[i])
                {
                    HitTarget(targets[i], onImpact, camera);
                }
            }
        }

        // 연기 한 뭉치: 깨진 자리에서 distance만큼 떨어진 무작위 방향(수평)에 놓는다.
        // Random.insideUnitCircle: 반지름 1 원 안의 무작위 점 → 방향만 쓰려고 normalized.
        private void SpawnSmoke(Vector3 landing, float distance)
        {
            if (smokePrefab == null)
            {
                return;
            }

            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
            Vector3 position = landing + new Vector3(direction.x, 0f, direction.y) * distance + Vector3.up * smokeHeight;
            Instantiate(smokePrefab, position, Quaternion.identity);
        }

        private void HitTarget((BattleUnit Unit, Vector3 Position) target, Action<BattleUnit, float> onImpact, BattleCameraDirector camera)
        {
            if (hitPrefab != null)
            {
                Instantiate(hitPrefab, target.Position, Quaternion.identity);
            }

            onImpact(target.Unit, 1f);
            if (camera != null)
            {
                camera.Shake(hitShake, 0.12f);
            }
        }

        // 바닥 평면에서의 거리 (높이 차이는 무시).
        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
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

        // 대상들 화면 위치의 가운데. 대상이 없으면 연출 오브젝트 자리.
        private Vector3 GetCenter(IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets)
        {
            if (targets.Count == 0)
            {
                return transform.position;
            }

            Vector3 sum = Vector3.zero;
            foreach ((BattleUnit _, Vector3 position) in targets)
            {
                sum += position;
            }

            return sum / targets.Count;
        }
    }
}
