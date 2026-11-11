using System;
using System.Collections;
using System.Collections.Generic;
using Game.Outgame;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 가챠 결과 연출. 결과는 뽑는 순간(GachaService) 이미 정해져 있고, 이 컴포넌트는 그 결과를 순서대로 보여주기만 한다.
    //  1. 물레: 바퀴를 시계 방향으로 돌리면 실감개로 이어진 줄 가운데 빛이 파랑 → 보라 → 황금빛으로 변한다. 도달하는 색 = 이번 결과의 최고 등급
    //     (3성만 = 파랑, 4성 포함 = 보라, 5성 포함 = 황금빛)
    //  2. 한 장씩 공개 (화면 바닥이 그 카드의 등급 색으로 빛난다): 3성은 바로, 4성은 보라 빛이 차오르다 터지며, 5성은 더 오래 뜸 들이다 더 크게 터지며 공개. 탭하면 다음 장
    //     반전: 5성이 있어도 일정 확률로 줄 빛을 보라까지만 보여주고, 공개 때 보라 → 황금빛으로 바뀌며 5성이 나온다
    //  3. 결과 목록: 뽑은 것을 한꺼번에 보여주고 확인으로 닫는다. SKIP은 언제든 바로 결과 목록으로 넘어간다
    // 빛·불꽃은 파티클(Additive 머티리얼 + Bloom 글로우)이 맡고, 이 코드는 "언제 어떤 색으로 재생할지"만 정한다.
    // 글로우(후처리)를 받으려고 Screen Space - Camera 캔버스에서 재생하며, 그동안 로비의 Overlay 캔버스는 끈다
    // (Overlay 캔버스는 항상 맨 위에 그려져 연출을 가리기 때문).
    public class GachaPresenter : MonoBehaviour
    {
        [Header("화면")]
        [Tooltip("연출 전체 (Screen Space - Camera 캔버스). 처음에는 꺼 둔다")]
        [SerializeField] private GameObject root;

        [Tooltip("연출 중 끌 로비 캔버스")]
        [SerializeField] private Canvas lobbyCanvas;

        [SerializeField] private Button skipButton;

        [Header("바닥 빛 (공개할 때만 등급 색으로 켜진다)")]
        [Tooltip("화면 아래 그라데이션. 평소에는 투명하다")]
        [SerializeField] private Image floorGradient;

        [Tooltip("화면 아래에서 피어오르는 빛")]
        [SerializeField] private ParticleSystem floorGlow;

        [SerializeField, Range(0f, 1f)] private float floorAlpha = 0.85f;
        [SerializeField, Min(0.01f)] private float floorFadeTime = 0.4f;

        // 1단계는 물레(3D 모델): 바퀴를 돌리면 물레와 실감개를 잇는 줄 가운데가 등급 색으로 점점 밝아진다.
        // 필드 이름은 씬 연결을 유지하려고 처음의 "기어" 이름을 그대로 쓴다.
        [Header("1. 물레")]
        [SerializeField] private GameObject gearPhase;
        [SerializeField] private GachaGearDial gearDial;

        [Tooltip("물레 3D 모델 (월드 공간, 물레 단계 동안만 켠다)")]
        [SerializeField] private GameObject wheelModel;

        [Tooltip("물레를 비추는 빛. 돌릴수록 밝아져 물레가 은은하게 빛나 보인다")]
        [SerializeField] private Light wheelLight;
        [SerializeField] private Vector2 wheelLightIntensity = new Vector2(0.4f, 3f);
        [SerializeField] private Text gearHint;

        [Tooltip("물레 바퀴 뒤에서 은은하게 피어오르는 빛 (돌릴수록 밝아진다, 등급과 무관한 따뜻한 색)")]
        [SerializeField] private ParticleSystem gearGlow;

        [Tooltip("줄 가운데의 빛 (돌릴수록 커지고 밝아지며, 등급 색으로 바뀐다)")]
        [SerializeField] private ParticleSystem threadGlow;

        [Tooltip("돌릴 때 줄 가운데에서 튀는 불꽃 (돌린 각도만큼 뿜는다)")]
        [SerializeField] private ParticleSystem gearSparks;

        [Tooltip("다 돌렸을 때 줄 가운데에서 터지는 빛")]
        [SerializeField] private ParticleSystem gearBurst;

        [Tooltip("물레 바퀴 빛의 색. 알파 = 밝기 (처음 → 다 돌렸을 때)")]
        [SerializeField] private Color wheelGlowColor = new Color(1f, 0.86f, 0.62f, 1f);
        [SerializeField, Range(0f, 1f)] private float wheelGlowMinAlpha = 0.06f;
        [SerializeField, Range(0f, 1f)] private float wheelGlowMaxAlpha = 0.4f;

        [Tooltip("줄 가운데 빛의 크기 배율 (처음 → 다 돌렸을 때)")]
        [SerializeField] private Vector2 threadGlowScale = new Vector2(0.3f, 1.4f);

        [Tooltip("1도 돌릴 때마다 뿜는 불꽃 수")]
        [SerializeField, Min(0f)] private float sparksPerDegree = 0.15f;

        [Header("2. 한 장씩 공개")]
        [SerializeField] private GameObject revealPhase;
        [SerializeField] private GachaResultCard revealCard;
        [SerializeField] private Text counterText;
        [SerializeField] private Text tapHint;

        [Tooltip("화면 전체를 덮는 투명 버튼: 연출 중 탭하면 빨리 감기, 끝난 뒤 탭하면 다음 장")]
        [SerializeField] private Button tapCatcher;

        [Tooltip("카드 아래에서 차오르는 빛 (4·5성 공개 전)")]
        [SerializeField] private ParticleSystem cardAura;

        [Tooltip("4성 공개 때 터지는 빛")]
        [SerializeField] private ParticleSystem burstFourStar;

        [Tooltip("5성 공개 때 터지는 빛 (더 크고 오래)")]
        [SerializeField] private ParticleSystem burstFiveStar;

        [Header("3. 결과 목록")]
        [SerializeField] private GameObject summaryPhase;
        [SerializeField] private RectTransform summaryGrid;

        [Tooltip("결과 목록 카드 템플릿 (씬에서는 꺼 두고 복제해서 쓴다)")]
        [SerializeField] private GachaResultCard summaryTemplate;

        [SerializeField] private Button confirmButton;

        [Header("등급 색")]
        [SerializeField] private Color threeStarColor = new Color(0.3f, 0.6f, 1f);
        [SerializeField] private Color fourStarColor = new Color(0.72f, 0.38f, 1f);
        [SerializeField] private Color fiveStarColor = new Color(1f, 0.78f, 0.3f);

        [Header("연출 시간·확률 (임시)")]
        [Tooltip("5성이 있을 때 물레 줄 빛을 보라까지만 보여주고 공개 때 황금빛으로 바꿀 확률")]
        [SerializeField, Range(0f, 1f)] private float fakeOutChance = 0.3f;

        [SerializeField, Min(0f)] private float fourStarBuildUp = 0.9f;
        [SerializeField, Min(0f)] private float fiveStarBuildUp = 1.8f;

        private List<GachaResult> results;
        private Action onClosed;
        private bool isFakeOut;
        private GachaRarity gearRarity;
        private readonly List<GachaResultCard> summaryCards = new List<GachaResultCard>();

        // 공개 카드의 원래 위치 (흔들림 도중 SKIP해도 다음 연출은 제자리에서 시작하도록)
        private Vector3 revealCardPosition;

        // 진행 중인 바닥 빛 색 전환 (새 전환을 시작할 때 이전 것을 멈춘다)
        private Coroutine floorFade;

        // 탭 입력 상태: 연출 중 탭(hurry) = 남은 대기를 건너뜀, 연출이 끝난 뒤 탭(advance) = 다음 장
        private bool isAnimating;
        private bool hurry;
        private bool advance;

        private void Awake()
        {
            root.SetActive(false);
            revealCardPosition = revealCard.transform.localPosition;
            gearDial.Rotated += HandleGearRotated;
            skipButton.onClick.AddListener(SkipToSummary);
            tapCatcher.onClick.AddListener(HandleTap);
            confirmButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            gearDial.Rotated -= HandleGearRotated;
        }

        // 뽑기 결과로 연출을 시작한다. onClosed: 결과 목록에서 확인을 눌러 닫힐 때 호출 (모집 화면 보석 갱신 등).
        public void Play(List<GachaResult> pulledResults, Action closed)
        {
            results = pulledResults;
            onClosed = closed;
            DecideGearRarity();

            lobbyCanvas.enabled = false;
            root.SetActive(true);
            DimFloor();
            gearPhase.SetActive(false);
            wheelModel.SetActive(false);
            revealPhase.SetActive(false);
            summaryPhase.SetActive(false);
            StartCoroutine(PlaySequence());
        }

        // 기어가 도달할 색(등급)을 정한다: 결과 중 최고 등급. 5성이면 일정 확률로 반전 연출(기어는 4성까지만).
        private void DecideGearRarity()
        {
            GachaRarity best = GachaRarity.ThreeStar;
            foreach (GachaResult result in results)
            {
                if (result.Rarity > best)
                {
                    best = result.Rarity;
                }
            }

            // UnityEngine.Random.value: 0~1 난수. 연출용이라 결과(확률)에는 영향이 없다.
            isFakeOut = best == GachaRarity.FiveStar && UnityEngine.Random.value < fakeOutChance;
            gearRarity = isFakeOut ? GachaRarity.FourStar : best;
        }

        // 전체 순서: 기어 → 한 장씩 공개 → 결과 목록. yield return 코루틴: 그 단계가 끝날 때까지 기다렸다가 다음으로 넘어간다.
        private IEnumerator PlaySequence()
        {
            yield return RunGearPhase();

            for (int i = 0; i < results.Count; i++)
            {
                yield return RevealOne(i);
            }

            ShowSummary();
        }

        // ---------- 1. 물레 ----------

        private IEnumerator RunGearPhase()
        {
            gearPhase.SetActive(true);
            wheelModel.SetActive(true);
            gearHint.gameObject.SetActive(true);
            gearDial.ResetDial();
            ApplyGearColor(0f);
            gearGlow.Play();
            threadGlow.Play();

            // 플레이어가 다 돌릴 때까지 매 프레임 확인한다 (yield return null = 다음 프레임까지 대기).
            while (!gearDial.IsComplete)
            {
                yield return null;
            }

            gearHint.gameObject.SetActive(false);
            PlayTinted(gearBurst, ColorOf(gearRarity));
            yield return new WaitForSeconds(0.9f);

            gearGlow.Stop();
            threadGlow.Stop();
            gearPhase.SetActive(false);
            wheelModel.SetActive(false);
        }

        // 물레를 돌릴 때마다: 색을 진행도에 맞추고, 돌린 각도만큼 불꽃을 뿜는다.
        private void HandleGearRotated(float turnedDegrees, float progress)
        {
            ApplyGearColor(progress);

            // Emit(개수): 재생 중이 아니어도 그 자리에서 파티클을 즉시 만든다. CeilToInt: 소수는 올림 (조금만 돌려도 1개 이상).
            gearSparks.Emit(Mathf.CeilToInt(turnedDegrees * sparksPerDegree));
        }

        // 진행도(0~1)에 맞춰 물레 연출을 바꾼다.
        //  - 줄 가운데 빛: 등급 색 + 점점 커짐 (최고 등급까지만 색이 바뀜)
        //  - 바퀴: 따뜻한 빛이 점점 밝아지고, 바퀴 그림도 살짝 밝아짐
        private void ApplyGearColor(float progress)
        {
            Color color = GetThreadColor(progress);
            SetParticleColor(threadGlow, color);
            SetParticleColor(gearSparks, color);

            // Mathf.Lerp(a, b, t): t(0~1)만큼 a에서 b로 섞은 값 → 진행도에 따라 줄 빛이 작게 시작해 크게 커진다.
            // localScale로 키우는 이유: 파티클이 Hierarchy 크기 기준이라 이미 떠 있는 빛도 바로 커진다.
            threadGlow.transform.localScale = Vector3.one * Mathf.Lerp(threadGlowScale.x, threadGlowScale.y, progress);

            Color wheelGlow = wheelGlowColor;
            wheelGlow.a = Mathf.Lerp(wheelGlowMinAlpha, wheelGlowMaxAlpha, progress);
            SetParticleColor(gearGlow, wheelGlow);

            // 물레를 비추는 빛이 점점 세진다 → 처음엔 어둑하다가 다 돌리면 환하게 보인다.
            wheelLight.intensity = Mathf.Lerp(wheelLightIntensity.x, wheelLightIntensity.y, progress);
        }

        // 진행도(0~1)에 따른 줄 가운데 빛의 색. 최고 등급까지만 변한다:
        //   3성: 계속 파랑 / 4성: 파랑 → 보라 / 5성: 파랑 → 보라 → 황금빛
        // Mathf.InverseLerp(a, b, p): p가 a~b 구간의 몇 %인지(0~1, 구간 밖이면 0 또는 1). 그 비율로 두 색을 섞는다(Color.Lerp).
        private Color GetThreadColor(float progress)
        {
            Color color = threeStarColor;
            if (gearRarity == GachaRarity.FourStar)
            {
                color = Color.Lerp(threeStarColor, fourStarColor, Mathf.InverseLerp(0.15f, 0.7f, progress));
            }
            else if (gearRarity == GachaRarity.FiveStar)
            {
                color = progress < 0.5f
                    ? Color.Lerp(threeStarColor, fourStarColor, Mathf.InverseLerp(0.1f, 0.45f, progress))
                    : Color.Lerp(fourStarColor, fiveStarColor, Mathf.InverseLerp(0.55f, 0.9f, progress));
            }

            return color;
        }

        // ---------- 2. 한 장씩 공개 ----------

        private IEnumerator RevealOne(int index)
        {
            GachaResult result = results[index];
            bool isFakeOutCard = isFakeOut && result.Rarity == GachaRarity.FiveStar;
            GachaRarity shownRarity = isFakeOutCard ? GachaRarity.FourStar : result.Rarity;

            revealPhase.SetActive(true);
            tapHint.gameObject.SetActive(false);
            counterText.text = $"{index + 1} / {results.Count}";
            isAnimating = true;
            hurry = false;
            advance = false;

            revealCard.transform.localPosition = revealCardPosition;
            revealCard.Show(result, ColorOf(result.Rarity));
            LightFloor(ColorOf(shownRarity));
            if (result.Rarity == GachaRarity.ThreeStar)
            {
                // 3성: 덮개 없이 바로 보여준다.
                revealCard.Reveal();
                yield return PopIn(revealCard.transform);
            }
            else
            {
                revealCard.Hide(ColorOf(shownRarity));
                yield return PopIn(revealCard.transform);
                yield return BuildUpAndBurst(result, shownRarity, isFakeOutCard);
            }

            isAnimating = false;
            tapHint.gameObject.SetActive(true);
            while (!advance)
            {
                yield return null;
            }

            cardAura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 4·5성 공개: 카드 아래에서 빛이 차오르며 뜸을 들이다(5성이 더 길게) 터지면서 덮개를 걷는다.
        // 반전이면 보라로 차오르다가 → 카드가 흔들리며 황금빛으로 바뀌고 → 5성처럼 한 번 더 차오른 뒤 터진다.
        private IEnumerator BuildUpAndBurst(GachaResult result, GachaRarity shownRarity, bool isFakeOutCard)
        {
            PlayTinted(cardAura, ColorOf(shownRarity));
            yield return Wait(shownRarity == GachaRarity.FiveStar ? fiveStarBuildUp : fourStarBuildUp);

            if (isFakeOutCard)
            {
                PlayTinted(burstFourStar, fourStarColor);
                revealCard.SetCoverColor(fiveStarColor);
                SetParticleColor(cardAura, fiveStarColor);
                LightFloor(fiveStarColor);
                yield return Shake(revealCard.transform, 0.45f);
                yield return Wait(fiveStarBuildUp);
            }

            ParticleSystem burst = result.Rarity == GachaRarity.FiveStar ? burstFiveStar : burstFourStar;
            PlayTinted(burst, ColorOf(result.Rarity));
            revealCard.Reveal();
            cardAura.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return Wait(0.5f);
        }

        // 탭: 연출 중이면 남은 대기를 건너뛰고, 끝났으면 다음 장으로 넘어간다.
        private void HandleTap()
        {
            if (isAnimating)
            {
                hurry = true;
            }
            else
            {
                advance = true;
            }
        }

        // seconds 동안 기다린다. 도중에 탭(hurry)하면 바로 끝난다.
        private IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds && !hurry)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        // 카드가 작게 시작해 원래 크기로 튀어나온다. 1 - (1 - t)^3: 처음엔 빠르고 끝에서 천천히 멈추는 곡선.
        private IEnumerator PopIn(Transform card)
        {
            const float duration = 0.25f;
            for (float elapsed = 0f; elapsed < duration && !hurry; elapsed += Time.deltaTime)
            {
                float t = 1f - Mathf.Pow(1f - elapsed / duration, 3f);
                card.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, t);
                yield return null;
            }

            card.localScale = Vector3.one;
        }

        // 반전 순간 카드를 좌우로 흔든다. Random.insideUnitCircle: 반지름 1 원 안의 임의 위치 → 흔들림 방향.
        private IEnumerator Shake(Transform card, float duration)
        {
            Vector3 origin = revealCardPosition;
            for (float elapsed = 0f; elapsed < duration && !hurry; elapsed += Time.deltaTime)
            {
                float strength = 18f * (1f - elapsed / duration);
                card.localPosition = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * strength);
                yield return null;
            }

            card.localPosition = origin;
        }

        // ---------- 바닥 빛 ----------

        // 바닥을 그 등급 색으로 밝힌다: 그라데이션은 지금 색에서 서서히 바뀌고, 피어오르는 빛 파티클도 같은 색으로 켠다.
        private void LightFloor(Color color)
        {
            if (floorFade != null)
            {
                StopCoroutine(floorFade);
            }

            color.a = floorAlpha;
            floorFade = StartCoroutine(FadeFloor(color));

            SetParticleColor(floorGlow, color);
            if (!floorGlow.isPlaying)
            {
                floorGlow.Play();
            }
        }

        // 바닥 빛을 바로 끈다 (기어 단계·결과 목록에서는 빛나지 않는다).
        private void DimFloor()
        {
            if (floorFade != null)
            {
                StopCoroutine(floorFade);
                floorFade = null;
            }

            floorGradient.color = Color.clear;
            floorGlow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 그라데이션 색을 floorFadeTime 동안 target으로 바꾼다. 투명(Color.clear)에서 시작하면 빛이 서서히 차오르는 것처럼 보인다.
        private IEnumerator FadeFloor(Color target)
        {
            Color start = floorGradient.color;
            for (float elapsed = 0f; elapsed < floorFadeTime; elapsed += Time.deltaTime)
            {
                floorGradient.color = Color.Lerp(start, target, elapsed / floorFadeTime);
                yield return null;
            }

            floorGradient.color = target;
        }

        // ---------- 3. 결과 목록 ----------

        // SKIP: 진행 중인 연출을 모두 멈추고 결과 목록으로 넘어간다 (결과는 이미 저장돼 있다).
        private void SkipToSummary()
        {
            StopAllCoroutines();
            ShowSummary();
        }

        // 뽑은 결과를 한꺼번에 보여준다. 카드는 템플릿을 복제하고, 이전 뽑기 카드는 지운다.
        private void ShowSummary()
        {
            StopAllEffects();
            DimFloor();
            gearPhase.SetActive(false);
            wheelModel.SetActive(false);
            revealPhase.SetActive(false);
            summaryPhase.SetActive(true);
            skipButton.gameObject.SetActive(false);

            foreach (GachaResultCard card in summaryCards)
            {
                Destroy(card.gameObject);
            }

            summaryCards.Clear();
            foreach (GachaResult result in results)
            {
                GachaResultCard card = Instantiate(summaryTemplate, summaryGrid);
                card.gameObject.SetActive(true);
                card.Show(result, ColorOf(result.Rarity));
                summaryCards.Add(card);
            }
        }

        // 확인: 연출을 닫고 로비 캔버스를 다시 켠다.
        private void Close()
        {
            root.SetActive(false);
            skipButton.gameObject.SetActive(true);
            lobbyCanvas.enabled = true;
            onClosed?.Invoke();
        }

        // ---------- 파티클 ----------

        private Color ColorOf(GachaRarity rarity) => rarity switch
        {
            GachaRarity.FiveStar => fiveStarColor,
            GachaRarity.FourStar => fourStarColor,
            _ => threeStarColor,
        };

        // 파티클(자식 파티클 포함)을 등급 색으로 바꿔 처음부터 재생한다.
        private static void PlayTinted(ParticleSystem system, Color color)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetParticleColor(system, color);
            system.Play(true);
        }

        // 앞으로 나올 파티클의 시작 색을 바꾼다 (이미 나온 파티클은 그대로).
        // 파티클의 밝기(글로우)는 머티리얼의 HDR 색이, 색조는 여기서 정한 시작 색이 정한다.
        // GetComponentsInChildren: 자기 자신 + 자식의 파티클 전부 (터지는 빛은 고리·불꽃·광선 여러 파티클로 이루어져 있다).
        private static void SetParticleColor(ParticleSystem system, Color color)
        {
            foreach (ParticleSystem part in system.GetComponentsInChildren<ParticleSystem>())
            {
                // main은 구조체 형태의 접근자라 변수에 받아 값을 바꾸면 파티클에 바로 반영된다.
                ParticleSystem.MainModule main = part.main;
                main.startColor = color;
            }
        }

        private void StopAllEffects()
        {
            foreach (ParticleSystem system in new[] { gearGlow, threadGlow, gearSparks, gearBurst, cardAura, burstFourStar, burstFiveStar })
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
