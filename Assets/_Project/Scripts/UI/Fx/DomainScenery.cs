using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    // 영역 배경 (흉신 마라 3스 "영역: 혈해신전", 임시). 영역이 펼쳐진 동안 전장에 깔리는 무대:
    //  - 사찰: 카메라 앞 먼 곳(shrineDistance)에 서서 언제나 카메라를 마주 본다 → 장면마다 구도가 바뀌어도 마라 뒤 배경에 보인다
    //  - 하늘: 사찰보다 더 먼 곳의 붉은 연기 판 (카메라를 따라옴)
    //  - 물바닥: 펼친 자리를 중심으로 전장 전체에 깔린 얕은 물 (반투명, 아래에 뒤집힌 사찰이 비쳐 보임)
    // 펼칠 때(Reveal): 물이 퍼지고 하늘이 물들고 → 사찰이 땅에서 솟는다. 끝날 때(Sink): 사찰이 가라앉고 물·하늘이 사라진 뒤 스스로 지워진다.
    // 모양(기본 도형 사찰·재질·그림)은 프리팹이 정하고, 이 코드는 위치·순서·시간만 정한다. 전장 색(안개·배경)은 DomainSceneryView가 Blend를 보고 섞는다.
    public class DomainScenery : MonoBehaviour
    {
        [Header("부품")]
        [Tooltip("영역 주인 등 뒤에 서는 사찰 묶음 (안에 솟아오르는 몸체와 물에 비친 뒤집힌 몸체가 있음)")]
        [SerializeField] private Transform shrine;

        [Tooltip("땅(물) 아래에서 솟아오르는 몸체들 (사찰 몸체)")]
        [SerializeField] private Transform[] risingBodies = new Transform[0];

        [Tooltip("물에 비친 사찰 (몸체 복사본을 담은 부모, 바닥 높이 기준). 솟은 만큼 아래로 늘어난다 (세로 크기 = -솟은 정도)")]
        [SerializeField] private Transform reflection;

        [Tooltip("사찰보다 먼 곳에 서는 붉은 하늘 판")]
        [SerializeField] private Transform sky;
        [SerializeField] private Renderer skyRenderer;

        [Tooltip("전장에 깔리는 물바닥 (펼친 자리를 중심으로 커지며 퍼짐)")]
        [SerializeField] private Transform water;

        [Tooltip("사찰이 솟을 때 사찰 발밑에 일어나는 먼지 (끝나면 스스로 지워지는 프리팹, 비우면 생략)")]
        [SerializeField] private GameObject riseDustPrefab;

        [Tooltip("사라질 때 사찰 조각들에 바꿔 끼우는 반투명 재질 (조각마다 원래 색을 유지한 채 투명도만 줄인다)")]
        [SerializeField] private Material fadeMaterial;

        [Header("자리")]
        [Tooltip("영역 주인 등 뒤로 사찰까지의 수평 거리 · 하늘 판까지의 수평 거리")]
        [SerializeField, Min(1f)] private float shrineDistance = 9f;
        [SerializeField, Min(1f)] private float skyDistance = 24f;

        [Tooltip("하늘 판 가운데 높이 (바닥 기준)")]
        [SerializeField] private float skyHeight = 8f;

        [Header("시간")]
        [Tooltip("물이 퍼지는 시간 · 사찰이 솟는 시간 · 끝날 때 가라앉는 시간(초)")]
        [SerializeField, Min(0.05f)] private float waterSpreadSeconds = 0.6f;
        [SerializeField, Min(0.05f)] private float riseSeconds = 1.4f;
        [SerializeField, Min(0.05f)] private float sinkSeconds = 1f;

        [Tooltip("영역 해제 때 사찰·물·하늘이 흐려지며 사라지는 시간(초)")]
        [SerializeField, Min(0.05f)] private float dissolveSeconds = 1.6f;

        [Tooltip("땅 속에 묻혀 있을 때의 깊이 (솟기 전 몸체 높이)")]
        [SerializeField, Min(0f)] private float buriedDepth = 14f;

        [Tooltip("사찰이 솟는 동안 흔들림 세기")]
        [SerializeField, Min(0f)] private float riseShake = 0.12f;

        // 지금 전장에 있는 영역 배경 (펼치는 연출 DomainDeployFx가 찾아 Reveal을 부른다). 없으면 null.
        public static DomainScenery Current { get; private set; }

        // 영역 분위기 세기 0~1 (안개·배경색 섞기, 하늘·물 투명도). DomainSceneryView가 읽는다.
        public float Blend { get; private set; }

        // 이미 펼쳐졌는지 (연출 없이 펼쳐진 경우 DomainSceneryView가 바로 펼친다).
        public bool IsRevealed { get; private set; }

        private float floorY;
        private float rise;
        private Vector3 waterFullScale;
        private bool isSinking;

        // MaterialPropertyBlock: 재질 에셋을 복제하지 않고 이 렌더러의 색(_BaseColor 투명도)만 덮어쓴다.
        private MaterialPropertyBlock skyBlock;
        private Color skyColor = Color.white;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            Current = this;
            waterFullScale = water != null ? water.localScale : Vector3.one;
            if (skyRenderer != null && skyRenderer.sharedMaterial != null && skyRenderer.sharedMaterial.HasProperty(BaseColorId))
            {
                skyColor = skyRenderer.sharedMaterial.GetColor(BaseColorId);
            }

            skyBlock = new MaterialPropertyBlock();
            SetHidden();
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        // 처음 상태: 사찰은 땅속, 물은 크기 0, 하늘은 투명.
        private void SetHidden()
        {
            SetRise(0f);
            if (water != null)
            {
                water.localScale = Vector3.zero;
            }

            SetBlend(0f);
        }

        // 바닥 높이를 정한다 (DomainSceneryView가 만들 때 전투 바닥 높이를 넣어 줌).
        public void Place(float groundY, Vector3 waterCenter)
        {
            floorY = groundY;
            if (water != null)
            {
                water.position = new Vector3(waterCenter.x, floorY + 0.02f, waterCenter.z);
            }
        }

        // 펼치기: 물이 center에서 퍼지며 하늘이 물들고 → 사찰이 땅에서 솟는다 (흔들림·먼지).
        public IEnumerator Reveal(Vector3 center, BattleCameraDirector camera)
        {
            if (IsRevealed)
            {
                yield break;
            }

            IsRevealed = true;
            if (water != null)
            {
                water.position = new Vector3(center.x, floorY + 0.02f, center.z);
            }

            yield return Animate(waterSpreadSeconds, t =>
            {
                float spread = 1f - (1f - t) * (1f - t);
                if (water != null)
                {
                    water.localScale = waterFullScale * spread;
                }

                SetBlend(t);
            });

            if (riseDustPrefab != null && shrine != null)
            {
                Instantiate(riseDustPrefab, shrine.position, Quaternion.identity).transform.localScale = Vector3.one * 3f;
            }

            // 솟는 동안 계속 약하게 흔들리고, 끝에 한 번 크게.
            float shakeTimer = 0f;
            yield return Animate(riseSeconds, t =>
            {
                SetRise(Mathf.SmoothStep(0f, 1f, t));
                shakeTimer -= Time.deltaTime;
                if (camera != null && shakeTimer <= 0f)
                {
                    camera.Shake(riseShake, 0.12f);
                    shakeTimer = 0.1f;
                }
            });

            if (camera != null)
            {
                camera.Shake(riseShake * 2.5f, 0.3f);
            }
        }

        // 연출 없이 바로 펼친 상태로 (펼치는 연출이 재생되지 않은 경우의 안전장치).
        public void RevealInstant(Vector3 center)
        {
            IsRevealed = true;
            if (water != null)
            {
                water.position = new Vector3(center.x, floorY + 0.02f, center.z);
                water.localScale = waterFullScale;
            }

            SetRise(1f);
            SetBlend(1f);
        }

        // 끝내기: 사찰이 가라앉고 물·하늘이 사라진 뒤 스스로 지운다.
        public IEnumerator Sink()
        {
            if (isSinking)
            {
                yield break;
            }

            isSinking = true;
            float startRise = rise;
            float startBlend = Blend;
            yield return Animate(sinkSeconds, t =>
            {
                SetRise(Mathf.Lerp(startRise, 0f, t * t));
                SetBlend(Mathf.Lerp(startBlend, 0f, t));
                if (water != null)
                {
                    water.localScale = waterFullScale * Mathf.Lerp(1f, 0f, t);
                }
            });

            Destroy(gameObject);
        }

        // 영역 해제: 사찰(반사 포함)이 흐려지고, 물·하늘·전장 분위기도 함께 옅어진 뒤 스스로 지운다 (영역 해제 연출 DomainCloseFx가 재생).
        // 사찰 조각은 불투명 재질이라 투명도를 줄일 수 없으므로, 반투명 재질(fadeMaterial)로 바꿔 끼우고
        // MaterialPropertyBlock으로 조각마다 원래 색(_BaseColor) × 남은 투명도를 넣는다 (재질 에셋은 그대로).
        public IEnumerator Dissolve()
        {
            if (isSinking)
            {
                yield break;
            }

            isSinking = true;
            List<(Renderer Renderer, Color Color)> pieces = CollectFadePieces();
            Renderer waterRenderer = water != null ? water.GetComponent<Renderer>() : null;
            Color waterColor = waterRenderer != null && waterRenderer.sharedMaterial.HasProperty(BaseColorId)
                ? waterRenderer.sharedMaterial.GetColor(BaseColorId)
                : Color.white;
            var block = new MaterialPropertyBlock();
            float startBlend = Blend;

            yield return Animate(dissolveSeconds, t =>
            {
                // 1 - t²: 처음엔 천천히, 끝으로 갈수록 빠르게 흐려진다.
                float alpha = 1f - t * t;
                foreach ((Renderer renderer, Color color) in pieces)
                {
                    block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, color.a * alpha));
                    renderer.SetPropertyBlock(block);
                }

                if (waterRenderer != null)
                {
                    block.SetColor(BaseColorId, new Color(waterColor.r, waterColor.g, waterColor.b, waterColor.a * alpha));
                    waterRenderer.SetPropertyBlock(block);
                }

                SetBlend(Mathf.Lerp(startBlend, 0f, t));
            });

            Destroy(gameObject);
        }

        // 사찰 아래 모든 조각의 원래 색을 기억하고 반투명 재질로 바꿔 끼운다. 반투명 재질이 없으면 바꾸지 않는다(색만 어두워짐).
        private List<(Renderer Renderer, Color Color)> CollectFadePieces()
        {
            var pieces = new List<(Renderer Renderer, Color Color)>();
            if (shrine == null)
            {
                return pieces;
            }

            foreach (Renderer renderer in shrine.GetComponentsInChildren<Renderer>())
            {
                Material original = renderer.sharedMaterial;
                Color color = original != null && original.HasProperty(BaseColorId) ? original.GetColor(BaseColorId) : Color.white;
                if (fadeMaterial != null)
                {
                    renderer.sharedMaterial = fadeMaterial;
                }

                pieces.Add((renderer, color));
            }

            return pieces;
        }

        // 몸체 높이: 0 = 땅속(-buriedDepth), 1 = 제자리(0).
        // 반사는 몸체처럼 옮기면 땅속 부분이 거꾸로 하늘에 떠 보이므로, 대신 세로 크기를 -솟은 정도로 둬 바닥에서 아래로 자라게 한다
        // (세로 크기가 음수 = 위아래로 뒤집힘, 0이면 안 보임).
        private void SetRise(float value)
        {
            rise = value;
            if (reflection != null)
            {
                reflection.localScale = new Vector3(1f, -value, 1f);
            }

            foreach (Transform body in risingBodies)
            {
                if (body != null)
                {
                    body.localPosition = new Vector3(body.localPosition.x, Mathf.Lerp(-buriedDepth, 0f, value), body.localPosition.z);
                }
            }
        }

        // 분위기 세기: 하늘 판 투명도를 같이 맞춘다.
        private void SetBlend(float value)
        {
            Blend = value;
            if (skyRenderer != null)
            {
                Color color = skyColor;
                color.a *= value;
                skyBlock.SetColor(BaseColorId, color);
                skyRenderer.SetPropertyBlock(skyBlock);
            }
        }

        // 사찰·하늘을 영역 주인(마라) 뒤에 세운다 (DomainSceneryView가 매 프레임 주인의 화면 위치·바라보는 방향을 넣어 줌).
        // 주인이 바라보는 쪽(상대 무리 쪽)의 반대편 = 등 뒤로 shrineDistance·skyDistance만큼 물러난 곳에, 앞(+z)이 주인 쪽을 보게 둔다.
        // Quaternion.LookRotation(forward): 오브젝트의 +z가 forward를 보게 하는 회전 → 사찰 정면이 주인과 상대 무리를 마주 본다.
        // 수평 방향만 써서 기울지 않게 한다. 그래서 주인 정면 시점에서는 주인 뒤에, 어깨 뒤 시점에서는 카메라 뒤(안 보임)에 있다.
        public void Follow(Vector3 ownerPosition, Vector3 ownerForward)
        {
            ownerForward.y = 0f;
            if (ownerForward.sqrMagnitude < 0.0001f)
            {
                return;
            }

            ownerForward.Normalize();
            var ground = new Vector3(ownerPosition.x, floorY, ownerPosition.z);
            Quaternion facingOwner = Quaternion.LookRotation(ownerForward, Vector3.up);
            if (shrine != null)
            {
                shrine.SetPositionAndRotation(ground - ownerForward * shrineDistance, facingOwner);
            }

            if (sky != null)
            {
                sky.SetPositionAndRotation(ground - ownerForward * skyDistance + Vector3.up * skyHeight, facingOwner);
            }
        }

        // seconds 동안 0 → 1 진행도를 apply(람다)에 넘긴다 (전투 시간 기준).
        private static IEnumerator Animate(float seconds, System.Action<float> apply)
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
