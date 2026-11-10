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

        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;

        [Tooltip("배너 위치 표시 점 (배너 수만큼 켜고, 지금 배너만 밝게)")]
        [SerializeField] private List<Image> pageDots = new List<Image>();

        [Header("배너 정보")]
        [SerializeField] private Text catchphraseText;
        [SerializeField] private Text bannerNameText;
        [SerializeField] private Text endDateText;
        [SerializeField] private Button rateInfoButton;

        [Header("뽑기")]
        [SerializeField] private Button singlePullButton;
        [SerializeField] private Button tenPullButton;
        [SerializeField] private Text singleCostText;
        [SerializeField] private Text tenCostText;
        [SerializeField] private Text gemText;

        [Tooltip("뽑기 결과 연출 (기어 → 한 장씩 공개 → 결과 목록)")]
        [SerializeField] private GachaPresenter presenter;

        [Header("색")]
        [SerializeField] private Color currentDotColor = Color.white;
        [SerializeField] private Color otherDotColor = new Color(1f, 1f, 1f, 0.3f);

        private int bannerIndex;
        private bool isInitialized;

        // 화면이 켜질 때마다(메인 화면에서 모집을 누를 때) 첫 배너와 보유 보석을 다시 보여준다.
        private void OnEnable()
        {
            InitializeOnce();
            ShowBanner(0);
            RefreshGems();
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

        // 우상단 보석 표시를 세이브 값으로 갱신한다 (화면을 열 때, 뽑기 연출이 끝났을 때).
        private void RefreshGems()
        {
            gemText.text = PlayerProfile.GetGems().ToString("N0");
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
            RefreshPageDots();

            // 배너가 하나뿐이면 넘길 곳이 없으므로 화살표를 숨긴다.
            previousButton.gameObject.SetActive(banners.Count > 1);
            nextButton.gameObject.SetActive(banners.Count > 1);
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

        // 뽑기 버튼: 지금 배너로 count회 뽑아 저장하고, 결과 연출을 재생한다. 연출이 끝나면 보석 표시를 갱신한다.
        private void RequestPull(int count)
        {
            List<GachaResult> results = GachaService.Pull(banners[bannerIndex], count);
            if (results == null)
            {
                Debug.Log("[Gacha] 보석이 부족합니다.");
                return;
            }

            presenter.Play(results, RefreshGems);
        }

        // 확률 공시 버튼 (임시): 공시 화면을 만들기 전이라 요청만 기록한다.
        private void ShowRateInfo()
        {
            Debug.Log("[Gacha] 확률 공시 화면 미구현");
        }
    }
}
