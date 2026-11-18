using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 성벽·마법진 방패 연출 (도발 탱커 3스 수호의 맹세, 임시). 레퍼런스: 별빛 아래 흰 성벽이 늘어선 통로 끝에서 성문이 빛난다.
    //  1) 아군 줄 앞 낮은 곳으로 컷 → 탱커 앞에 원형 마법진 방패(빛줄기 8개·톱니 원판)가 펼쳐져 천천히 돈다 (처음부터 방패를 들고 있음)
    //  2) 그 뒤로 아군 양옆에 흰 성벽(파란 깃발·총안)이 앞쪽부터 차례로 땅에서 솟아오른다 (흔들림)
    //  3) 아군 뒤 성문이 보랏빛으로 밝아진다
    //  4) 방패가 크게 번쩍이며 부풀고 → 섬광·흔들림, 아군마다 방패 빛이 터진다
    //  5) 잠시 버틴 뒤 방패가 흐려지고 성벽이 가라앉으며 원래 시점으로
    // 성벽 모양은 프리팹의 꺼 둔 견본(기본 도형 + 재질)이 정한다. 견본의 원점 = 아군 줄 가운데 바닥, +z = 카메라 쪽.
    // 견본 안에서 솟아오를 조각은 "Risers" 아래 자식들이고, 카메라 쪽(+z)에 가까운 조각부터 솟는다.
    public class FortressGuardFx : SkillFx
    {
        [Header("프리팹 부품")]
        [Tooltip("성벽 견본 (꺼 둔 자식). 원점 = 아군 줄 가운데 바닥, 앞(+z) = 카메라 쪽")]
        [SerializeField] private Transform fortressTemplate;

        [Tooltip("견본 안 솟아오를 조각들의 부모 이름")]
        [SerializeField] private string risersName = "Risers";

        [Tooltip("견본 안 성문 빛 판 이름 (밝기를 코드가 바꾼다)")]
        [SerializeField] private string gateGlowName = "GateGlow";

        [Tooltip("탱커가 앞에 드는 원형 마법진 방패 판 견본 (꺼 둔 자식, 가산 발광 재질)")]
        [SerializeField] private Renderer barrierTemplate;

        [Tooltip("조각이 솟을 때 바닥에서 피어오르는 먼지 (비우면 생략)")]
        [SerializeField] private GameObject dustPrefab;

        [Tooltip("방패가 번쩍이는 순간의 섬광 (비우면 생략)")]
        [SerializeField] private GameObject barrierFlashPrefab;

        [Tooltip("아군마다 터지는 방패 빛 (비우면 생략)")]
        [SerializeField] private GameObject allyShieldPrefab;

        [Header("카메라 (아군 줄 가운데 바닥 기준, 앞 = 카메라 쪽)")]
        [SerializeField] private float viewDistance = 9f;
        [SerializeField] private float viewHeight = 0.6f;
        [SerializeField] private float lookHeight = 3.2f;
        [Tooltip("바라볼 곳을 성문 쪽(뒤)으로 미는 거리")]
        [SerializeField] private float lookBack = 6f;
        [SerializeField, Range(20f, 90f)] private float viewFieldOfView = 55f;

        [Header("솟아오르기")]
        [Tooltip("조각이 땅속에서 시작하는 깊이")]
        [SerializeField, Min(0.1f)] private float riseDepth = 9f;
        [SerializeField, Min(0.05f)] private float riseSeconds = 0.45f;

        [Tooltip("조각마다 솟는 시각이 늦어지는 간격(초)")]
        [SerializeField, Min(0f)] private float riseStagger = 0.07f;
        [SerializeField, Min(0f)] private float riseShake = 0.08f;

        [Header("성문")]
        [SerializeField, Min(0f)] private float gateSeconds = 0.45f;
        [SerializeField] private float gateIntensity = 3f;

        [Header("마법진 방패 (탱커 기준)")]
        [Tooltip("방패를 탱커보다 카메라 쪽으로 내민 거리 · 높이 (탱커 캡슐 중심 기준)")]
        [SerializeField] private float shieldForward = 1f;
        [SerializeField] private float shieldHeight = 0.3f;

        [Tooltip("방패 크기: 들고 있을 때 · 번쩍이며 부풀 때")]
        [SerializeField, Min(0.1f)] private float shieldSize = 3.2f;
        [SerializeField, Min(0.1f)] private float shieldFlareSize = 4.4f;

        [Tooltip("방패가 도는 속도 (초당 각도)")]
        [SerializeField] private float shieldSpinSpeed = 25f;

        [SerializeField, Min(0.05f)] private float shieldAppearSeconds = 0.35f;
        [SerializeField, Min(0.05f)] private float shieldFlareSeconds = 0.35f;

        [Tooltip("방패 밝기: 평소 · 번쩍일 때 (재질 색에 곱함)")]
        [SerializeField] private float shieldIntensity = 1.3f;
        [SerializeField] private float shieldFlareIntensity = 3f;
        [SerializeField, Min(0f)] private float barrierShake = 0.35f;

        [Header("버티기·사라지기")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.7f;
        [SerializeField, Min(0.05f)] private float sinkSeconds = 0.45f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform fortress;
        private Renderer gateGlow;
        private Color gateBaseColor;
        private BattleCameraDirector cinematicCamera;

        // 방패 판과 그 기본 회전(카메라를 마주 봄)·도는 각도·크기·밝기. 도는 코루틴이 매 프레임 이 값으로 다시 그린다.
        private Renderer shield;
        private Quaternion shieldFacing;
        private float shieldAngle;
        private float shieldScale;
        private float shieldBrightness;
        private Color shieldBaseColor;

        // 조각 하나: 원래 자리와 솟는 순서.
        private class Riser
        {
            public Transform Piece;
            public Vector3 Home;
            public float Delay;
        }

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
            Camera view = camera != null && camera.Camera != null ? camera.Camera : Camera.main;
            Vector3 center = GetAllyCenter(caster);
            PlaceFortress(center, view);
            BeginView(camera);

            // 처음부터 탱커가 방패를 들고 있다: 펼쳐지는 동안에도, 성벽이 솟는 동안에도 계속 돈다.
            HoldShield(caster);
            StartCoroutine(SpinShield());
            yield return OpenShield();

            List<Riser> risers = CollectRisers();
            yield return RiseWalls(risers, camera);
            yield return LightGate();
            yield return FlareShield(camera);

            yield return new WaitForSeconds(holdSeconds);
            yield return Sink(risers);

            if (camera != null)
            {
                camera.EndCinematic();
                cinematicCamera = null;
            }

            yield return new WaitForSeconds(0.3f);
            Destroy(gameObject);
        }

        // 성벽을 아군 줄 가운데 바닥에 놓고 앞(+z)이 카메라 쪽을 보게 한다 (캡슐 중심에서 1 아래가 바닥).
        private void PlaceFortress(Vector3 center, Camera view)
        {
            Vector3 toCamera = view != null ? view.transform.position - center : Vector3.back;
            toCamera.y = 0f;
            toCamera = toCamera.sqrMagnitude > 0.001f ? toCamera.normalized : Vector3.back;

            fortress = Instantiate(fortressTemplate, transform);
            fortress.SetPositionAndRotation(center + Vector3.down, Quaternion.LookRotation(toCamera, Vector3.up));
            fortress.gameObject.SetActive(true);

            Transform glow = FindDeep(fortress, gateGlowName);
            gateGlow = glow != null ? glow.GetComponent<Renderer>() : null;
            if (gateGlow != null)
            {
                gateBaseColor = gateGlow.sharedMaterial.GetColor(BaseColorId);
                SetIntensity(gateGlow, gateBaseColor, 0f);
            }
        }

        // 낮은 곳에서 성문 쪽 위를 올려다보는 시점으로 컷 (레퍼런스처럼 양옆 성벽이 통로를 이룬다).
        // TransformPoint: 성벽 기준 자리(앞 viewDistance, 위 viewHeight)를 실제 월드 위치로 바꾼다.
        private void BeginView(BattleCameraDirector camera)
        {
            if (camera == null)
            {
                return;
            }

            var look = new GameObject("CinematicLook").transform;
            look.SetParent(transform, false);
            look.position = fortress.TransformPoint(new Vector3(0f, lookHeight, -lookBack));
            camera.BeginCinematic(fortress.TransformPoint(new Vector3(0f, viewHeight, viewDistance)), look, viewFieldOfView);
            cinematicCamera = camera;
        }

        // ---------- 마법진 방패 ----------

        // 탱커 앞(카메라 쪽)에 방패 판을 세운다. 판(Quad)은 앞면이 -z라, 카메라 반대쪽(-toCamera)을 앞(z)으로 두면 카메라가 앞면을 본다.
        // fortress.forward: 성벽의 앞(+z) = 카메라 쪽 수평 방향.
        private void HoldShield(Vector3 caster)
        {
            Vector3 toCamera = fortress.forward;
            shield = Instantiate(barrierTemplate, transform);
            shield.gameObject.SetActive(true);
            shield.transform.position = caster + toCamera * shieldForward + Vector3.up * shieldHeight;
            shieldFacing = Quaternion.LookRotation(-toCamera, Vector3.up);
            shieldBaseColor = barrierTemplate.sharedMaterial.GetColor(BaseColorId);
            shieldScale = 0f;
            shieldBrightness = 0f;
            ApplyShield();
        }

        // 방패가 있는 동안 매 프레임 판의 축(앞 방향)을 중심으로 돌리고 크기·밝기를 반영한다.
        private IEnumerator SpinShield()
        {
            while (shield != null)
            {
                shieldAngle += shieldSpinSpeed * Time.deltaTime;
                ApplyShield();
                yield return null;
            }
        }

        // Quaternion.Euler(0, 0, 각도): 판의 앞 방향(z축)을 중심으로 돌린다 → 카메라를 본 채 바퀴처럼 돈다.
        private void ApplyShield()
        {
            shield.transform.rotation = shieldFacing * Quaternion.Euler(0f, 0f, shieldAngle);
            shield.transform.localScale = Vector3.one * shieldScale;
            SetIntensity(shield, shieldBaseColor, shieldBrightness);
        }

        // 1) 방패 펼치기: 크기 0 → 제 크기(easeOutBack: 살짝 넘쳤다 돌아옴), 밝기 0 → 평소.
        private IEnumerator OpenShield()
        {
            yield return Animate(shieldAppearSeconds, t =>
            {
                float u = t - 1f;
                shieldScale = shieldSize * (1f + 2.7f * u * u * u + 1.7f * u * u);
                shieldBrightness = shieldIntensity * t;
            });
        }

        // 4) 방패 번쩍: 부풀며 밝아졌다가(sin 반 바퀴) 원래 크기로, 가장 클 때 섬광·흔들림·아군 방패 빛. 도는 속도도 잠깐 빨라진다.
        private IEnumerator FlareShield(BattleCameraDirector camera)
        {
            bool burst = false;
            float baseSpin = shieldSpinSpeed;
            yield return Animate(shieldFlareSeconds * 2f, t =>
            {
                float wave = Mathf.Sin(t * Mathf.PI);
                shieldScale = Mathf.Lerp(shieldSize, shieldFlareSize, wave);
                shieldBrightness = Mathf.Lerp(shieldIntensity, shieldFlareIntensity, wave);
                shieldSpinSpeed = baseSpin * (1f + 6f * wave);
                if (!burst && t >= 0.5f)
                {
                    burst = true;
                    BurstShield(camera);
                }
            });

            shieldSpinSpeed = baseSpin;
        }

        // 섬광·강한 흔들림 + 아군마다 방패 빛.
        private void BurstShield(BattleCameraDirector camera)
        {
            if (barrierFlashPrefab != null)
            {
                Instantiate(barrierFlashPrefab, shield.transform.position, Quaternion.identity);
            }

            if (allyShieldPrefab != null)
            {
                foreach ((BattleUnit _, Vector3 position) in AllyTargets)
                {
                    Instantiate(allyShieldPrefab, position, Quaternion.identity);
                }
            }

            if (camera != null)
            {
                camera.Shake(barrierShake, 0.35f);
            }
        }

        // ---------- 성벽 ----------

        // 솟아오를 조각들: 카메라 쪽(+z, 로컬 z가 큰 것)부터 차례로 솟도록 늦춤을 정하고, 땅속으로 내려 둔다.
        // Sort(람다): 두 조각의 z를 비교해 큰 것이 앞에 오게 정렬한다.
        private List<Riser> CollectRisers()
        {
            var risers = new List<Riser>();
            Transform root = FindDeep(fortress, risersName);
            if (root == null)
            {
                return risers;
            }

            foreach (Transform piece in root)
            {
                risers.Add(new Riser { Piece = piece, Home = piece.localPosition });
            }

            risers.Sort((a, b) => b.Home.z.CompareTo(a.Home.z));
            for (int i = 0; i < risers.Count; i++)
            {
                risers[i].Delay = riseStagger * i;
                risers[i].Piece.localPosition = risers[i].Home + Vector3.down * riseDepth;
            }

            return risers;
        }

        // 2) 솟아오르기: 조각마다 늦춤만큼 기다렸다가 땅속에서 제자리로 (끝에서 살짝 넘쳤다 돌아오는 곡선), 솟기 시작할 때 먼지·흔들림.
        private IEnumerator RiseWalls(List<Riser> risers, BattleCameraDirector camera)
        {
            float last = risers.Count > 0 ? risers[risers.Count - 1].Delay : 0f;
            float total = last + riseSeconds;
            var started = new bool[risers.Count];
            yield return Animate(total, t =>
            {
                float now = t * total;
                for (int i = 0; i < risers.Count; i++)
                {
                    Riser riser = risers[i];
                    float local = Mathf.Clamp01((now - riser.Delay) / riseSeconds);
                    if (local > 0f && !started[i])
                    {
                        started[i] = true;
                        OnRiseStart(riser, camera);
                    }

                    // easeOutBack: 1 + 2.7(u)³ + 1.7(u)², u = local - 1 → 제자리를 조금 넘었다가 돌아온다 (쿵 하고 박히는 느낌).
                    float u = local - 1f;
                    float eased = local <= 0f ? 0f : 1f + 2.7f * u * u * u + 1.7f * u * u;
                    riser.Piece.localPosition = riser.Home + Vector3.down * (riseDepth * (1f - eased));
                }
            });
        }

        private void OnRiseStart(Riser riser, BattleCameraDirector camera)
        {
            if (dustPrefab != null)
            {
                Vector3 floor = fortress.TransformPoint(new Vector3(riser.Home.x, 0f, riser.Home.z));
                Instantiate(dustPrefab, floor, Quaternion.identity);
            }

            if (camera != null)
            {
                camera.Shake(riseShake, 0.15f);
            }
        }

        // 3) 성문 빛: 밝기 0 → gateIntensity (Bloom으로 번진다).
        private IEnumerator LightGate()
        {
            yield return Animate(gateSeconds, t =>
            {
                if (gateGlow != null)
                {
                    SetIntensity(gateGlow, gateBaseColor, gateIntensity * Mathf.SmoothStep(0f, 1f, t));
                }
            });
        }

        // 5) 사라지기: 방패가 어두워지며 작아지고 성벽 조각이 모두 땅속으로 가라앉는다 (t²: 점점 빨라짐), 성문 빛도 꺼진다.
        private IEnumerator Sink(List<Riser> risers)
        {
            yield return Animate(sinkSeconds, t =>
            {
                foreach (Riser riser in risers)
                {
                    riser.Piece.localPosition = riser.Home + Vector3.down * (riseDepth * t * t);
                }

                shieldBrightness = shieldIntensity * (1f - t);
                shieldScale = shieldSize * (1f - 0.3f * t);
                if (gateGlow != null)
                {
                    SetIntensity(gateGlow, gateBaseColor, gateIntensity * (1f - t));
                }
            });

            Destroy(shield.gameObject);
            shield = null;
        }

        // ---------- 공용 ----------

        private static void SetIntensity(Renderer renderer, Color baseColor, float intensity)
        {
            Color color = baseColor * intensity;
            color.a = baseColor.a;
            SetColor(renderer, color);
        }

        // MaterialPropertyBlock: 재질을 복사하지 않고 이 렌더러에만 색을 바꾼다.
        private static void SetColor(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
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
