using System.Collections.Generic;
using Game.Core;
using Game.Data;
using Game.Outgame;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 모집(가챠) 화면 (기획서 2장·7장).
    //  - 가운데: 배너 제목 + 픽업 캐릭터 칸(일러스트 자리) + 캐릭터 이름
    //  - 좌우 화살표·상단 점: 배너 넘기기 / 지금 몇 번째 배너인지
    //  - 좌하단: 강조 문구, 배너 이름, 종료 일시, 확률 공시 버튼
    //  - 우하단: 1회 진행 / 10회 진행 → 뽑기(GachaService) 후 연출(GachaPresenter) 재생
    //           버튼 위에 S 확정·A 확정까지 남은 횟수 (세이브의 가챠 카운터로 계산)
    // 화면 모양은 씬에서 만들고, 이 컴포넌트는 배너 데이터로 내용을 채우고 버튼을 연결한다.
    public class GachaView : MonoBehaviour
    {

        [Header("데이터")]
        [Tooltip("넘겨 볼 수 있는 배너 (왼쪽부터 순서대로)")]
        [SerializeField] private List<GachaBannerData> banners = new List<GachaBannerData>();

        [Header("배너")]
        [SerializeField] private Image bannerBackground;
        [SerializeField] private Text bannerTitle;

        [Tooltip("픽업 캐릭터 이름 글자")]
        [SerializeField] private Text pickupName;

        [Tooltip("픽업 캐릭터 그림 자리. 캐릭터에 일러스트가 없으면 씬에 넣어 둔 임시 그림(실루엣)을 그대로 보여준다")]
        [SerializeField] private Image pickupIllust;

        [Tooltip("일러스트가 없을 때 보여줄 안내 글자 (일러스트가 있으면 숨긴다)")]
        [SerializeField] private GameObject pickupPlaceholder;

        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Tooltip("배너 위치 표시 점 (배너 수만큼 켜고, 지금 배너만 밝게)")]
        [SerializeField] private List<Image> pageDots = new List<Image>();

        [Header("배너 정보")]
        [SerializeField] private Text catchphraseText;
        [SerializeField] private Text bannerNameText;
        [SerializeField] private Text endDateText;
        [SerializeField] private Button rateInfoButton;

        [Tooltip("확률 공시 창 (지금 배너의 픽업으로 확률을 채워 엶)")]
        [SerializeField] private GachaRatePopup rateInfoPopup;

        [Header("뽑기")]
        [SerializeField] private Button singlePullButton;
        [SerializeField] private Button tenPullButton;
        [SerializeField] private Text singleCostText;
        [SerializeField] private Text tenCostText;
        [SerializeField] private Text gemText;

        [Tooltip("뽑기 버튼 위: S 확정(천장)까지 남은 횟수")]
        [SerializeField] private Text sGuaranteeText;

        [Tooltip("S 확정 표시 위: A 확정까지 남은 횟수")]
        [SerializeField] private Text aGuaranteeText;

        [Tooltip("뽑기 결과 연출 (기어 → 한 장씩 공개 → 결과 목록)")]
        [SerializeField] private GachaPresenter presenter;

        [Header("색")]
        [SerializeField] private Color currentDotColor = Color.white;
        [SerializeField] private Color otherDotColor = new Color(1f, 1f, 1f, 0.3f);

        private int bannerIndex;
        private bool isInitialized;

        // 일러스트가 없는 캐릭터에 보여줄 임시 그림 (씬에 처음 넣어 둔 실루엣)
        private Sprite defaultIllust;

        // 화면이 켜질 때마다(메인 화면에서 모집을 누를 때) 첫 배너와 보유 보석·확정까지 남은 횟수를 다시 보여준다.
        // 켜져 있는 동안은 재화 변경 알림(InventoryChanged)을 받아 보석 표시를 바로 고친다 (디버그 키로 보석을 받았을 때 등).
        private void OnEnable()
        {
            InitializeOnce();
            ShowBanner(0);
            RefreshPlayerState();
            PlayerProfile.InventoryChanged += RefreshPlayerState;
        }

        // 꺼질 때 구독 해제: static 알림이라 해제하지 않으면 꺼진 화면을 계속 고치게 된다.
        private void OnDisable()
        {
            PlayerProfile.InventoryChanged -= RefreshPlayerState;
        }

        // 버튼 연결은 한 번만 한다 (OnEnable은 화면을 열 때마다 불리므로 중복 연결 방지).
        private void InitializeOnce()
        {
            if (isInitialized)
            {
                return;
            }

            isInitialized = true;
            previousButton.onClick.AddListener(() => ShowBanner(bannerIndex - 1));
            nextButton.onClick.AddListener(() => ShowBanner(bannerIndex + 1));
            singlePullButton.onClick.AddListener(() => RequestPull(1));
            tenPullButton.onClick.AddListener(() => RequestPull(10));
            rateInfoButton.onClick.AddListener(ShowRateInfo);

            singleCostText.text = FormatCost(1);
            tenCostText.text = FormatCost(10);
        }

        // 버튼 위 비용 표기. 디버그 무료 뽑기면 "(무료)"를 붙인다.
        // N0: 천 단위 쉼표 (1000 → 1,000)
        private static string FormatCost(int count)
        {
            string free = GachaService.Rules.FreePulls ? " (무료)" : string.Empty;
            return $"보석 {GachaService.Rules.GemsPerPull * count:N0}{free}";
        }

        // 세이브에서 바뀌는 표시(보석, 확정까지 남은 횟수)를 갱신한다 (화면을 열 때, 뽑기 연출이 끝났을 때).
        private void RefreshPlayerState()
        {
            gemText.text = PlayerProfile.GetGems().ToString("N0");
            RefreshGuaranteeCounts();

            // 보석이 모자라면 그 뽑기 버튼을 누를 수 없게 흐리게 한다 (CanPull: 보유 보석 ≥ 횟수 × 1회 비용).
            singlePullButton.interactable = GachaService.CanPull(1);
            tenPullButton.interactable = GachaService.CanPull(10);
        }

        // 확정까지 남은 횟수 = 확정 간격 - 확정 없이 이미 뽑은 횟수.
        //  - S: pityCount는 S 없이 뽑은 횟수(0~89) → 천장 90이면 90 - pityCount = 1~90회 남음. "1회"면 다음 뽑기가 S 확정
        //  - A: aGuaranteeCount는 A 이상 없이 뽑은 횟수(0~9) → 10 - aGuaranteeCount = 1~10회 남음
        // 카운터는 배너와 상관없이 이월되므로(기획서 7-1) 배너를 넘겨도 다시 계산하지 않는다.
        private void RefreshGuaranteeCounts()
        {
            GachaSave state = PlayerProfile.GachaState;
            int sRemaining = GachaService.Rules.HardPity - state.pityCount;
            int aRemaining = GachaService.Rules.AGuaranteeInterval - state.aGuaranteeCount;

            sGuaranteeText.text = $"S 확정까지 <b>{sRemaining}</b>회";
            aGuaranteeText.text = $"A 확정까지 <b>{aRemaining}</b>회";
        }

        // index번 배너를 보여준다. 처음·끝을 넘으면 반대쪽 끝으로 돌아간다.
        private void ShowBanner(int index)
        {
            if (banners.Count == 0)
            {
                return;
            }

            // 나머지 연산으로 0 ~ 배너 수-1 안에 맞춘다. 음수(-1)도 끝 배너가 되도록 배너 수를 한 번 더해 준다.
            bannerIndex = (index % banners.Count + banners.Count) % banners.Count;
            GachaBannerData banner = banners[bannerIndex];

            bannerBackground.color = banner.ThemeColor;
            bannerTitle.text = banner.BannerName;
            catchphraseText.text = banner.Catchphrase;
            bannerNameText.text = banner.BannerName;
            endDateText.text = banner.EndDateText;

            pickupName.text = banner.PickupCharacter != null ? banner.PickupCharacter.DisplayName : string.Empty;
            ShowPickupIllust(banner.PickupCharacter);
            RefreshPageDots();

            // 배너가 하나뿐이면 넘길 곳이 없으므로 화살표를 숨긴다.
            previousButton.gameObject.SetActive(banners.Count > 1);
            nextButton.gameObject.SetActive(banners.Count > 1);
        }

        // 픽업 캐릭터 그림을 보여준다. 일러스트가 있으면 그 그림으로, 없으면 처음 씬에 있던 임시 그림(실루엣)으로 되돌린다.
        // 처음 쓸 때 임시 그림을 기억해 두는 이유: 일러스트가 있는 배너 → 없는 배너로 넘기면 원래 그림으로 돌아가야 하기 때문.
        private void ShowPickupIllust(CharacterData pickup)
        {
            if (pickupIllust == null)
            {
                return;
            }

            // ??=: 아직 기억한 임시 그림이 없을 때만(null) 지금 그림을 기억한다 → 첫 호출 때 한 번만 저장된다.
            defaultIllust ??= pickupIllust.sprite;

            // ?.: pickup이 없으면(null) Illustration을 읽지 않고 null을 돌려준다.
            Sprite illustration = pickup?.Illustration;
            bool hasIllustration = illustration != null;

            pickupIllust.sprite = hasIllustration ? illustration : defaultIllust;

            // 비율 유지: 정사각형·세로형 그림이 섞여 있어도 찌그러지지 않게 칸 안에 맞춘다.
            pickupIllust.preserveAspect = true;

            if (pickupPlaceholder != null)
            {
                pickupPlaceholder.SetActive(!hasIllustration);
            }
        }

        // 배너 수만큼 점을 켜고, 지금 배너의 점만 밝게 한다.
        private void RefreshPageDots()
        {
            for (int i = 0; i < pageDots.Count; i++)
            {
                pageDots[i].gameObject.SetActive(i < banners.Count);
                pageDots[i].color = i == bannerIndex ? currentDotColor : otherDotColor;
            }
        }

        // 뽑기 버튼: 지금 배너로 count회 뽑아 저장하고, 결과 연출을 재생한다. 연출이 끝나면 보석·남은 횟수 표시를 갱신한다.
        private void RequestPull(int count)
        {
            List<GachaResult> results = GachaService.Pull(banners[bannerIndex], count);
            if (results == null)
            {
                Debug.Log($"[Gacha] 보석이 부족합니다. (필요 {GachaService.GetCost(count):N0}, 디버그 키 F9로 보석 +100,000)");
                return;
            }

            presenter.Play(results, RefreshPlayerState);
        }

        // 확률 공시 버튼: 지금 보고 있는 배너 기준으로 공시 창을 연다 (픽업 캐릭터 표시가 배너마다 다름).
        private void ShowRateInfo()
        {
            if (banners.Count == 0)
            {
                return;
            }

            rateInfoPopup.Open(banners[bannerIndex]);
        }
    }
}
