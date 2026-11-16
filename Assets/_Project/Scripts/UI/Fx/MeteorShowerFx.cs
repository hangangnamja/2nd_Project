using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 메테오 연출 (선브링어 변신 3스 섬멸, 임시). 레퍼런스: 하늘을 끌어내리듯 불덩이가 쏟아진 뒤 거대한 메테오가 떨어져 폭발.
    //  1) 잔챙이 메테오들이 대상들에게 돌아가며 떨어진다 → 맞은 대상에게 피해의 일부를 약한 타격으로 보여줌
    //  2) 거대 메테오가 떨어지기 시작하면 카메라가 적 쪽으로 컷 → 적 뒤에서 하늘을 올려다보며 메테오가 다가오는 모습
    //  3) 땅에 닿기 직전 원래 시점(선브링어 쪽)으로 컷 → 거대 폭발과 함께 대상 전원에게 남은 피해(onImpact 비율 1)·강한 흔들림
    // 로직 피해는 한 번에 들어가 있고, 잔챙이 + 거대 메테오는 그 피해를 나눠 "보여주기"만 한다 (합계는 같음).
    // 메테오·폭발의 모양(불꽃·빛·파편)은 프리팹의 파티클이 담당하고, 이 코드는 위치·순서·시간·카메라 컷만 정한다.
    public class MeteorShowerFx : SkillFx
    {
        [Header("프리팹")]
        [Tooltip("떨어지는 불덩이 (빛 덩어리 + 꼬리 불꽃 파티클)")]
        [SerializeField] private GameObject meteorPrefab;

        [Tooltip("땅에 닿을 때 터지는 폭발 (섬광·불꽃·파편·충격파 고리, 끝나면 스스로 지워짐)")]
        [SerializeField] private GameObject explosionPrefab;

        [Header("잔챙이 메테오")]
        [Tooltip("먼저 쏟아지는 작은 메테오 수")]
        [SerializeField, Min(0)] private int smallMeteors = 7;

        [Tooltip("떨어지기 시작하는 위치 (착탄 지점 기준): 위로 높이 + 비스듬히")]
        [SerializeField] private Vector3 smallSpawnOffset = new Vector3(-2.5f, 10f, 2f);

        [SerializeField, Min(0.05f)] private float smallFallSeconds = 0.42f;

        [Tooltip("작은 메테오 사이 간격(초)")]
        [SerializeField, Min(0f)] private float smallInterval = 0.12f;

        [Tooltip("작은 메테오가 대상 발밑에서 무작위로 비켜 떨어지는 반지름")]
        [SerializeField, Min(0f)] private float smallRadius = 0.6f;

        [Tooltip("잔챙이 메테오들이 합쳐서 보여줄 피해 비율 (대상마다). 나머지는 거대 메테오가 보여준다")]
        [SerializeField, Range(0f, 0.9f)] private float smallDamageShare = 0.3f;

        [SerializeField, Min(0f)] private float smallShake = 0.1f;

        [Header("거대 메테오")]
        [Tooltip("거대 메테오 (용암 균열 바위 + 불길·불씨 파티클). 프리팹 지름 1 기준")]
        [SerializeField] private GameObject giantMeteorPrefab;

        [Tooltip("거대 메테오 지름 (적 캡슐 키가 2)")]
        [SerializeField, Min(1f)] private float giantScale = 12f;

        [Tooltip("떨어지면서 굴러가는 속도 (초당 각도)")]
        [SerializeField] private float giantSpinDegrees = 35f;

        [Tooltip("떨어지기 시작하는 위치 (대상들 가운데 착탄 지점 기준)")]
        [SerializeField] private Vector3 giantSpawnOffset = new Vector3(-9f, 38f, 12f);

        [SerializeField, Min(0.1f)] private float giantFallSeconds = 1.7f;

        [Tooltip("잔챙이를 다 던진 뒤 거대 메테오가 떨어지기 시작할 때까지(초)")]
        [SerializeField, Min(0f)] private float giantDelay = 0.25f;

        [Tooltip("폭발 크기 배율")]
        [SerializeField, Min(1f)] private float giantExplosionScale = 5.5f;

        [SerializeField, Min(0f)] private float giantShake = 0.9f;
        [SerializeField, Min(0f)] private float giantShakeSeconds = 0.8f;

        [Header("적 시점 컷")]
        [Tooltip("적 시점 카메라 위치: 대상들 가운데에서 메테오가 떨어지는 길의 옆으로 이만큼 비켜서, 낮은 곳에서 올려다본다 (꼬리가 화면을 비스듬히 가로지르게)")]
        [SerializeField] private float enemyViewSide = 12f;

        [Tooltip("옆으로 비키면서 메테오가 오는 반대쪽으로도 이만큼 물러난다")]
        [SerializeField] private float enemyViewBack = 4f;

        [SerializeField] private float enemyViewHeight = -0.3f;

        [Tooltip("적 시점 카메라가 바라보는 높이 (착탄 지점 위). 땅의 적들이 화면 아래에, 떨어지는 메테오가 위에서 들어오도록 고정")]
        [SerializeField] private float enemyViewLookHeight = 10f;
        [SerializeField] private float enemyViewFieldOfView = 68f;

        [Tooltip("낙하 진행도가 이 비율이 되면 원래 시점으로 돌아온다 (1이면 착탄 순간)")]
        [SerializeField, Range(0.3f, 1f)] private float cutBackAt = 0.78f;

        [Header("공통")]
        [Tooltip("첫 메테오 전에 기다리는 시간(초) — 시전자가 기를 모으는 동안")]
        [SerializeField, Min(0f)] private float windUpSeconds = 0.35f;

        [Tooltip("캡슐 중심에서 땅까지의 높이 (캡슐 원점이 몸 가운데라 -1이 발밑)")]
        [SerializeField] private float groundOffset = -1f;

        // 연출 컷을 건 카메라. 연출이 중간에 사라져도(씬 이동 등) 카메라가 적 시점에 남지 않게 OnDestroy에서 되돌린다.
        private BattleCameraDirector cinematicCamera;

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
            Vector3 center = Ground(GetCenter(targets));
            yield return new WaitForSeconds(windUpSeconds);

            yield return SmallBarrage(targets, onImpact, camera);
            yield return new WaitForSeconds(giantDelay);
            yield return GiantMeteor(caster, center, targets, onImpact, camera);

            // 폭발이 퍼질 시간을 조금 두고 연출 오브젝트를 지운다 (폭발 파티클은 각자 스스로 지워진다).
            yield return new WaitForSeconds(0.3f);
            Destroy(gameObject);
        }

        // 1) 잔챙이: 대상들에게 돌아가며(i % 대상 수) 떨어져, 맞은 대상에게 피해 일부를 약한 타격으로 보여준다.
        // 한 개가 보여줄 비율 = smallDamageShare ÷ 그 대상이 맞는 잔챙이 수 → 대상마다 합이 smallDamageShare.
        // 모두 던진 뒤 바로 돌아간다 (마지막 몇 개는 거대 메테오가 오는 동안 마저 떨어진다).
        private IEnumerator SmallBarrage(
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            if (targets.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < smallMeteors; i++)
            {
                // 나머지 연산 %: 0, 1, 2, 0, 1, 2 … 순서로 대상을 돌아가며 고른다.
                int index = i % targets.Count;
                BattleUnit unit = targets[index].Unit;
                Vector3 point = Ground(targets[index].Position) + RandomFlat(smallRadius);

                // 이 대상이 맞는 잔챙이 수: 앞쪽 대상(smallMeteors % 대상 수보다 작은 번호)은 하나씩 더 맞는다.
                int hitsOnUnit = smallMeteors / targets.Count + (index < smallMeteors % targets.Count ? 1 : 0);
                float share = smallDamageShare / hitsOnUnit;

                // 람다 () => ...: 착탄했을 때 할 일. unit·share·camera는 바깥 변수를 그대로 붙잡아 쓴다 (클로저).
                StartCoroutine(Fall(meteorPrefab, 1f, point + smallSpawnOffset, point, smallFallSeconds, null, () =>
                {
                    Instantiate(explosionPrefab, point, Quaternion.identity);
                    if (camera != null)
                    {
                        camera.Shake(smallShake, 0.2f);
                    }

                    if (share > 0f)
                    {
                        onImpact(unit, share);
                    }
                }));

                yield return new WaitForSeconds(smallInterval);
            }
        }

        // 2)·3) 거대 메테오: 적 시점으로 컷 → 떨어지는 메테오를 올려다봄 → 착탄 직전 원래 시점으로 컷 → 거대 폭발 + 대상 전원 피해.
        private IEnumerator GiantMeteor(
            Vector3 caster,
            Vector3 center,
            IReadOnlyList<(BattleUnit Unit, Vector3 Position)> targets,
            Action<BattleUnit, float> onImpact,
            BattleCameraDirector camera)
        {
            Vector3 start = center + giantSpawnOffset;
            bool landed = false;

            // 착탄 순간: 원래 시점으로 확실히 돌아온 뒤 폭발·피해·흔들림.
            Coroutine fall = StartCoroutine(Fall(giantMeteorPrefab, giantScale, start, center, giantFallSeconds, progress =>
            {
                // 낙하 진행도(0~1)가 cutBackAt을 넘으면 원래 시점으로 컷. EndCinematic은 이미 돌아왔으면 아무것도 하지 않는다.
                if (progress >= cutBackAt && camera != null)
                {
                    camera.EndCinematic();
                }
            }, () =>
            {
                landed = true;
                if (camera != null)
                {
                    camera.EndCinematic();
                    camera.Shake(giantShake, giantShakeSeconds);
                }

                GameObject explosion = Instantiate(explosionPrefab, center, Quaternion.identity);
                explosion.transform.localScale = Vector3.one * giantExplosionScale;

                foreach ((BattleUnit unit, Vector3 _) in targets)
                {
                    // 비율 1: 잔챙이가 보여주고 남은 피해를 모두 보여준다.
                    onImpact(unit, 1f);
                }
            }));

            // 적 시점: 대상들 가운데에서 메테오가 떨어지는 길의 옆으로 비켜서, 낮은 곳에서 떨어지는 메테오를 올려다본다.
            //  - away: 메테오가 오는 반대 방향(수평). Vector3.Cross(위, away): 그 방향에 수직인 옆 방향
            //  - 옆 방향 두 쪽 중 시전자(caster)에게서 먼 쪽을 고른다 → 화면에 시전자 대신 적들과 메테오가 담긴다
            if (camera != null)
            {
                Vector3 away = new Vector3(-giantSpawnOffset.x, 0f, -giantSpawnOffset.z).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, away);
                if (Vector3.Dot(side, center - caster) < 0f)
                {
                    side = -side;
                }

                Vector3 viewPosition = center + side * enemyViewSide + away * enemyViewBack + Vector3.up * (enemyViewHeight - groundOffset);
                // 바라볼 지점을 메테오가 아니라 착탄 지점 위 고정점으로 둔다 → 적들(아래)과 내려오는 메테오(위)가 함께 담긴다.
                var lookPoint = new GameObject("CinematicLook").transform;
                lookPoint.SetParent(transform, false);
                lookPoint.position = center + Vector3.up * enemyViewLookHeight;
                camera.BeginCinematic(viewPosition, lookPoint, enemyViewFieldOfView);
                cinematicCamera = camera;
            }

            while (!landed)
            {
                yield return null;
            }
        }

        // 메테오 하나: start에서 end로 가속하며 떨어진다. 매 프레임 진행도를 onProgress로 알리고(없으면 null),
        // 땅에 닿으면 꼬리 불꽃만 남기고 본체를 지운 뒤 onLanded를 부른다.
        // Action<float>: 실수 하나를 받는 함수 (진행도 0~1).
        private IEnumerator Fall(GameObject prefab, float scale, Vector3 start, Vector3 end, float seconds, Action<float> onProgress, Action onLanded)
        {
            GameObject meteor = Instantiate(prefab, start, Quaternion.LookRotation(end - start), transform);
            meteor.transform.localScale = Vector3.one * scale;

            // 거대 메테오(크기 > 1)만 떨어지면서 굴러간다. Random.onUnitSphere: 길이 1인 무작위 방향 → 회전축.
            Vector3 spinAxis = UnityEngine.Random.onUnitSphere;
            float spin = scale > 1f ? giantSpinDegrees : 0f;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);

                // t²: 처음엔 느리고 땅에 가까울수록 빨라지는 가속 곡선 → 내리꽂히는 느낌.
                meteor.transform.position = Vector3.Lerp(start, end, t * t);

                // Rotate(축, 각도, 월드 기준): 이번 프레임만큼 축을 중심으로 돌린다.
                meteor.transform.Rotate(spinAxis, spin * Time.deltaTime, Space.World);

                // ?.Invoke: onProgress가 null(잔챙이)이면 부르지 않는다.
                onProgress?.Invoke(t);
                yield return null;
            }

            DetachTrail(meteor);
            onLanded();
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

        // 캡슐 중심 높이 → 발밑(땅) 높이로.
        private Vector3 Ground(Vector3 position)
        {
            return new Vector3(position.x, position.y + groundOffset, position.z);
        }

        // 수평 원 안의 무작위 지점. Random.insideUnitCircle: 반지름 1인 원 안의 무작위 2D 점.
        private static Vector3 RandomFlat(float radius)
        {
            Vector2 point = UnityEngine.Random.insideUnitCircle * radius;
            return new Vector3(point.x, 0f, point.y);
        }

        // 메테오 본체는 지우되, 꼬리 불꽃 파티클은 떼어 내 남은 불씨가 자연스럽게 사라지게 한다.
        // ParticleSystem.Stop: 새로 내뿜는 것만 멈추고 이미 나온 입자는 수명대로 사라진다. stopAction이 Destroy면 다 사라진 뒤 오브젝트도 지워진다.
        private static void DetachTrail(GameObject meteor)
        {
            foreach (ParticleSystem system in meteor.GetComponentsInChildren<ParticleSystem>())
            {
                system.transform.SetParent(null, true);
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ParticleSystem.MainModule main = system.main;
                main.stopAction = ParticleSystemStopAction.Destroy;
            }

            Destroy(meteor);
        }
    }
}
