using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.UI
{
    // 스킬마다 다르게 보여줄 공격 동작 하나 (UnitModel의 "스킬 동작" 목록 한 칸). 스킬 데이터의 "모델 동작" 이름으로 찾는다.
    // 걸어가기를 켜면: 걷는 동작으로 대상 쪽에 다가간 뒤(걷는 시간 = 거리 ÷ 걷는 속도) 공격 동작을 재생한다.
    [Serializable]
    public class ModelAttackMotion
    {
        [Tooltip("스킬 데이터의 \"모델 동작\"에 적는 이름")]
        public string name = "Slam";

        [Tooltip("공격 동작의 Animator 상태 이름")]
        public string state = "Slam";

        [Tooltip("공격 동작을 시작하고 타격하는 순간까지(초) · 타격 뒤 머무는 시간(초)")]
        [Min(0f)] public float impactSeconds = 0.78f;
        [Min(0f)] public float recoverSeconds = 0.5f;

        [Tooltip("체크하면 공격 전에 대상 쪽으로 걸어간다")]
        public bool walkIn = true;

        [Tooltip("걷는 동작의 Animator 상태 이름 · 걷는 속도(초당 거리) · 가장 멀리 걸어가는 거리 · 대상 앞에서 멈추는 거리")]
        public string walkState = "Walk";
        [Min(0.1f)] public float walkSpeed = 2.2f;
        [Min(0f)] public float maxWalkDistance = 4f;
        [Min(0f)] public float stopDistance = 3.5f;

        [Tooltip("타격하는 순간 화면 흔들림 세기 · 시간(초). 0이면 흔들지 않음 (맞는 쪽 기본 흔들림은 따로 나옴)")]
        [Min(0f)] public float impactShake = 0.5f;
        [Min(0f)] public float impactShakeSeconds = 0.35f;

        [Tooltip("체크하면 공격 동작을 시작해 타격 순간까지 몸이 점점 밝게 번쩍이며 빛난다 (기를 모으는 느낌, 맥동이 점점 빨라짐)")]
        public bool chargeGlow;

        [Tooltip("모으는 빛 색 (HDR: 1보다 크면 Bloom으로 번짐)")]
        [ColorUsage(false, true)] public Color chargeGlowColor = new Color(3f, 1.6f, 0.6f);

        [Tooltip("타격 순간 몸(가슴)에서 터뜨릴 이펙트 프리팹 (끝나면 스스로 지워지는 파티클). 비우면 없음")]
        public GameObject impactEffectPrefab;
        [Min(0.01f)] public float impactEffectScale = 3f;
    }

    // 전투 유닛의 3D 모델 (캡슐 대신 보이는 모습). CharacterData의 "전투 3D 모델" 프리팹 맨 위에 붙인다.
    // UnitView가 모델을 몸의 자식으로 만들고, 모션(돌진·피격·처치·시전)이 나올 때 이 컴포넌트의 애니메이션 함수를 부른다.
    //  - 애니메이션: Animator 컨트롤러의 상태 이름을 CrossFade로 바로 재생 (조건·트리거 없이 이름만으로 → 컨트롤러가 단순해짐)
    //  - 색: 캡슐처럼 편 색으로 칠하지 않고, 쓰러지면 어둡게·맞으면 하얗게 번쩍이게만 한다 (MaterialPropertyBlock으로 _BaseColor 곱하기)
    //  - 크기·발 위치·머리 높이·클릭 영역은 프리팹 Inspector에서 정한다 (UnitView가 이름표·클릭 영역을 그에 맞춤)
    public class UnitModel : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        [Header("애니메이션 상태 이름 (Animator 컨트롤러)")]
        [SerializeField] private string idleState = "Idle";
        [SerializeField] private string attackState = "Attack";
        [SerializeField] private string castState = "Cast";
        [SerializeField] private string hitState = "Hit";
        [SerializeField] private string dieState = "Die";

        [Tooltip("다른 동작으로 넘어갈 때 섞는 시간(초)")]
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.08f;

        [Header("공격 타이밍")]
        [Tooltip("공격 동작을 시작하고 내리치는 순간까지 걸리는 시간(초). 피해 숫자·맞는 반응이 이 순간에 뜬다")]
        [SerializeField, Min(0f)] private float attackImpactSeconds = 0.62f;

        [Tooltip("내리친 뒤 제자리로 돌아오기 전까지 머무는 시간(초)")]
        [SerializeField, Min(0f)] private float attackRecoverSeconds = 0.45f;

        [Header("크기 (몸 가운데 기준, 캡슐은 발 -1 ~ 머리 1)")]
        [Tooltip("모델을 뒤(바라보는 반대쪽)로 물려 세운 거리. 큰 모델이 옆 유닛과 겹치지 않게 한다 (프리팹 안 모델의 z 위치와 같게)")]
        [SerializeField] private float bodyOffsetZ = 0f;

        [Tooltip("머리 위 이름표를 띄울 높이 · 상태효과 줄 높이")]
        [SerializeField] private float labelHeight = 2.4f;
        [SerializeField] private float statusBarHeight = 3.0f;

        [Tooltip("누르는 영역(캡슐 콜라이더) 반지름 · 높이 · 가운데 높이")]
        [SerializeField, Min(0.1f)] private float colliderRadius = 1.2f;
        [SerializeField, Min(0.2f)] private float colliderHeight = 3.2f;
        [SerializeField] private float colliderCenterY = 0.6f;

        [Header("시전 (버프·회복)")]
        [Tooltip("끄면 시전할 때 캡슐처럼 떠오르지 않고, 몸 전체가 금빛으로 번쩍이지도 않는다 (아래 빛나는 부품만 빛남)")]
        [SerializeField] private bool liftOnCast = true;

        [Tooltip("시전할 때 빛나는 부품 (예: 탑의 보석). 비우면 빛나는 부품 없음. 재질에 Emission이 켜져 있어야 빛난다")]
        [SerializeField] private Renderer[] castGlowRenderers = new Renderer[0];

        [Tooltip("빛나는 색 (HDR: 1보다 크면 Bloom으로 번짐) · 가장 밝아지기까지 · 다시 꺼지기까지 시간(초)")]
        [SerializeField, ColorUsage(false, true)] private Color castGlowColor = new Color(2.5f, 1.2f, 4f);
        [SerializeField, Min(0.01f)] private float castGlowRiseSeconds = 0.35f;
        [SerializeField, Min(0.01f)] private float castGlowFadeSeconds = 0.6f;

        [Header("색")]
        [Tooltip("쓰러졌을 때 곱하는 색")]
        [SerializeField] private Color deadTint = new Color(0.35f, 0.35f, 0.38f);

        [Header("스킬 동작 (스킬 데이터의 \"모델 동작\" 이름으로 찾음, 없으면 위의 기본 공격)")]
        [SerializeField] private List<ModelAttackMotion> skillMotions = new List<ModelAttackMotion>();

        private Renderer[] renderers;
        private Color[] originalColors;
        private MaterialPropertyBlock block;
        private Color tint = Color.white;
        private bool isDown;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private Coroutine castGlow;

        // 시전할 때 떠오를지 · 빛나는 부품이 있는지 · 가장 밝아지는 시간 (UnitView가 시전 모션·버프 박자 시점을 정할 때 읽음).
        public bool LiftOnCast => liftOnCast;
        public bool HasCastGlow => castGlowRenderers.Length > 0;
        public float CastGlowRiseSeconds => castGlowRiseSeconds;

        public float AttackImpactSeconds => attackImpactSeconds;
        public float AttackRecoverSeconds => attackRecoverSeconds;
        public float BodyOffsetZ => bodyOffsetZ;
        public float LabelHeight => labelHeight;

        // 몸에서 터지는 이펙트를 놓을 자리: 휴머노이드면 가슴 뼈, 아니면 모델 자리 + 이름표 높이의 절반.
        public Vector3 EffectAnchor
        {
            get
            {
                Transform chest = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Chest) : null;
                return chest != null ? chest.position : transform.position + Vector3.up * (labelHeight * 0.5f);
            }
        }
        public float StatusBarHeight => statusBarHeight;
        public float ColliderRadius => colliderRadius;
        public float ColliderHeight => colliderHeight;
        public float ColliderCenterY => colliderCenterY;

        // 쓰러진 자세인지 (부활하면 UnitView가 Idle로 되돌린다).
        public bool IsDown => isDown;

        // 모델 안의 모든 렌더러와 재질의 원래 색을 기억해 둔다 (번쩍임·어둡게는 이 색에 곱한다).
        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            originalColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material material = renderers[i].sharedMaterial;
                originalColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
            }

            block = new MaterialPropertyBlock();
        }

        public void PlayIdle()
        {
            isDown = false;
            Play(idleState);
        }

        public void PlayAttack() => Play(attackState);

        // 상태 이름으로 바로 재생 (스킬 동작의 걷기·공격 상태).
        public void PlayState(string state) => Play(state);

        // 스킬 데이터의 "모델 동작" 이름에 맞는 스킬 동작을 찾는다. 이름이 비었거나 목록에 없으면 null (기본 공격 동작을 쓴다).
        // List.Find(조건 람다): 조건이 참인 첫 칸, 없으면 null.
        public ModelAttackMotion FindSkillMotion(string motionName)
        {
            if (string.IsNullOrEmpty(motionName))
            {
                return null;
            }

            return skillMotions.Find(motion => motion.name == motionName);
        }
        // 시전: 시전 동작을 재생하고, 빛나는 부품이 있으면 빛나게 한다.
        public void PlayCast()
        {
            Play(castState);
            if (HasCastGlow && isActiveAndEnabled)
            {
                if (castGlow != null)
                {
                    StopCoroutine(castGlow);
                }

                castGlow = StartCoroutine(CastGlowRoutine());
            }
        }

        // 빛나는 부품의 발광 색을 0 → castGlowColor(가장 밝음) → 0으로. 위로 볼록한 곡선(1 - (1 - t)²)으로 확 밝아졌다가 천천히 꺼진다.
        // MaterialPropertyBlock: 재질 에셋은 그대로 두고 이 렌더러의 _EmissionColor만 덮어쓴다 (GetPropertyBlock으로 기존 값(색 배율)을 유지).
        private System.Collections.IEnumerator CastGlowRoutine()
        {
            for (float t = 0f; t < castGlowRiseSeconds; t += Time.deltaTime)
            {
                SetGlow(1f - (1f - t / castGlowRiseSeconds) * (1f - t / castGlowRiseSeconds));
                yield return null;
            }

            for (float t = 0f; t < castGlowFadeSeconds; t += Time.deltaTime)
            {
                SetGlow(1f - t / castGlowFadeSeconds);
                yield return null;
            }

            SetGlow(0f);
            castGlow = null;
        }

        private void SetGlow(float amount)
        {
            foreach (Renderer glowRenderer in castGlowRenderers)
            {
                if (glowRenderer == null)
                {
                    continue;
                }

                glowRenderer.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, castGlowColor * amount);
                glowRenderer.SetPropertyBlock(block);
            }
        }

        // 쓰러진 뒤에는 맞는 동작으로 일어나지 않게 한다.
        public void PlayHit()
        {
            if (!isDown)
            {
                Play(hitState);
            }
        }

        public void PlayDie()
        {
            isDown = true;
            Play(dieState);
        }

        // 상태 이름으로 바로 재생한다. CrossFadeInFixedTime(이름, 섞는 시간(초), 레이어 0, 처음부터 0초):
        // 지금 동작에서 그 상태로 섞으며 넘어간다. 컨트롤러에 그 이름의 상태가 없으면 아무것도 하지 않는다.
        // Animator.StringToHash: 상태 이름 → 상태를 찾는 숫자 (HasState로 있는지 확인).
        private void Play(string state)
        {
            if (animator == null || string.IsNullOrEmpty(state) || !animator.isActiveAndEnabled)
            {
                return;
            }

            int hash = Animator.StringToHash(state);
            if (animator.HasState(0, hash))
            {
                animator.CrossFadeInFixedTime(hash, crossFadeSeconds, 0, 0f);
            }
        }

        // 기본 색 배율: 살아 있으면 흰색(원래 색 그대로), 쓰러졌으면 어둡게.
        public void SetDead(bool dead)
        {
            tint = dead ? deadTint : Color.white;
            SetFlash(0f, Color.white);
        }

        // 번쩍임: amount(0~1)만큼 flashColor 쪽으로 밝게 섞는다 (UnitView가 매 프레임 남은 시간 비율로 부른다).
        // Color.Lerp(기본 배율, 번쩍임 색 × 2.5, amount): 1이면 원래 색보다 훨씬 밝게(HDR) 빛났다가 0이면 원래대로.
        public void SetFlash(float amount, Color flashColor)
        {
            if (renderers == null)
            {
                return;
            }

            Color multiplier = Color.Lerp(tint, flashColor * 2.5f, amount);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, originalColors[i] * multiplier);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
