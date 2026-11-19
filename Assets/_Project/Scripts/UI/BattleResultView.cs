using System.Collections;
using System.Collections.Generic;
using Game.Battle;
using Game.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투 결과 화면: 전투가 끝나면 화면 전체를 덮고 결과를 보여준다.
    //  - 승리: 금빛 배경·VICTORY, 패배: 붉은 배경·DEFEAT (배경·제목·강조선 색을 결과에 맞춰 바꿈)
    //  - 스테이지 이름 · 걸린 사이클 · 행동 횟수
    //  - 별 미션 결과 (이번 전투에서 달성한 ★, 처음 달성하면 NEW, 전투 시간)
    //  - 이번 전투에 쓴 파티 (쓰러진 아군은 어둡게)
    //  - 획득 보상 (아이콘·등급 색·개수), 없으면 안내 글
    //  - 로비 버튼
    // 덮는 판이 클릭을 막으므로 끝난 전투의 유닛·스킬 버튼은 더 누를 수 없다.
    // 파티 카드·보상 칸은 씬에 둔 원본(꺼 둔 첫 칸)을 복사해 채운다.
    public class BattleResultView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        [Tooltip("화면 전체를 덮는 판 (처음에는 꺼 둔다)")]
        [SerializeField] private GameObject panel;

        [Tooltip("나타날 때 투명도를 올릴 묶음")]
        [SerializeField] private CanvasGroup group;

        [Header("결과에 따라 색이 바뀌는 부분")]
        [Tooltip("배경 그라데이션 (결과 색으로 물듦)")]
        [SerializeField] private Image backdrop;

        [Tooltip("제목 뒤 빛")]
        [SerializeField] private Image titleGlow;

        [Tooltip("강조선들 (제목 아래 줄, 소제목 막대 등)")]
        [SerializeField] private Image[] accents = new Image[0];

        [SerializeField] private Text titleText;
        [SerializeField] private Text titleEnText;
        [SerializeField] private Text infoText;

        [Header("별 미션")]
        [Tooltip("별 미션 결과 묶음 (별 미션이 없는 스테이지면 숨김)")]
        [SerializeField] private GameObject starGroup;

        [Tooltip("별 미션 줄 (★ + 조건 문장 + 처음 달성하면 NEW)")]
        [SerializeField] private Text starText;

        [SerializeField] private Color starOffColor = new Color(1f, 1f, 1f, 0.25f);

        [Header("파티")]
        [Tooltip("파티 카드 원본 (꺼 둔 첫 칸, 같은 부모 아래에 복사된다)")]
        [SerializeField] private ResultPartyCard partyCardTemplate;

        [Header("보상")]
        [Tooltip("보상 칸 원본 (꺼 둔 첫 칸, 같은 부모 아래에 복사된다)")]
        [SerializeField] private ResultRewardSlot rewardSlotTemplate;

        [Tooltip("보상이 없을 때 보이는 안내 글")]
        [SerializeField] private Text noRewardText;

        [SerializeField] private Button lobbyButton;
        [SerializeField] private string lobbySceneName = "Lobby";

        [Header("색")]
        [SerializeField] private Color victoryColor = new Color(1f, 0.8f, 0.35f);
        [SerializeField] private Color defeatColor = new Color(0.95f, 0.28f, 0.25f);

        [Header("등장")]
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.35f;

        [Tooltip("보상 칸이 하나씩 튀어나오는 간격(초)")]
        [SerializeField, Min(0f)] private float rewardInterval = 0.12f;

        private void Awake()
        {
            panel.SetActive(false);
            partyCardTemplate.gameObject.SetActive(false);
            rewardSlotTemplate.gameObject.SetActive(false);

            // LoadScene: Battle 씬을 닫고 로비 씬을 연다 (Build Settings에 등록된 이름).
            lobbyButton.onClick.AddListener(() => SceneManager.LoadScene(lobbySceneName));
        }

        // 이벤트 구독·해제: 켜져 있는 동안만 전투 상태 변경 알림을 받는다.
        private void OnEnable()
        {
            battleManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            battleManager.StateChanged -= HandleStateChanged;
        }

        // 전투가 끝났으면 결과 화면을 한 번만 띄운다. 끝나기 전에는 아무것도 하지 않는다.
        private void HandleStateChanged()
        {
            if (!battleManager.IsBattleOver || panel.activeSelf)
            {
                return;
            }

            bool isVictory = battleManager.IsVictory;
            ShowHeader(isVictory);
            ShowStars();
            ShowParty(battleManager.Allies);
            List<ResultRewardSlot> slots = ShowRewards(battleManager.Rewards);

            // 투명한 상태로 켠 뒤 PlayAppear가 서서히 보이게 한다.
            group.alpha = 0f;
            panel.SetActive(true);

            // SetAsLastSibling: 같은 Canvas 안에서 맨 마지막(= 맨 위)에 그려, 다른 HUD 위를 덮게 한다.
            panel.transform.SetAsLastSibling();
            StartCoroutine(PlayAppear(slots));
        }

        // 제목·정보 줄과 결과 색을 채운다.
        private void ShowHeader(bool isVictory)
        {
            Color color = isVictory ? victoryColor : defeatColor;
            titleText.text = isVictory ? "승리" : "패배";
            titleText.color = color;
            titleEnText.text = isVictory ? "VICTORY" : "DEFEAT";
            titleEnText.color = color;

            // 배경·빛은 원래 투명도를 지키고 색만 바꾼다.
            backdrop.color = WithAlpha(color, backdrop.color.a);
            titleGlow.color = WithAlpha(color, titleGlow.color.a);
            foreach (Image accent in accents)
            {
                accent.color = WithAlpha(color, accent.color.a);
            }

            // ?.: 스테이지가 없으면(설정 오류) 이름 대신 빈 글자.
            string stageName = battleManager.Stage?.StageName ?? string.Empty;
            infoText.text = $"{stageName}   <color=#8FB4D9>사이클 {battleManager.CurrentCycle} · 행동 {battleManager.ActionCount}회</color>";
        }

        // 별 미션 결과: 첫 줄에 미션들을 가로로 나란히 (이번 전투에서 달성했으면 금색 ★, 못 했으면 흐리게, 처음 달성한 별엔 NEW),
        // 둘째 줄에 전투 시간을 붙여 "n분 이내" 별이 왜 됐는지/안 됐는지 바로 보이게 한다.
        private void ShowStars()
        {
            StageData stage = battleManager.Stage;
            int count = stage != null ? stage.StarMissions.Count : 0;
            starGroup.SetActive(count > 0);
            if (count == 0)
            {
                return;
            }

            string onColor = ColorUtility.ToHtmlStringRGB(victoryColor);
            string offColor = ColorUtility.ToHtmlStringRGBA(starOffColor);
            var missions = new List<string>();
            for (int i = 0; i < count; i++)
            {
                bool achieved = i < battleManager.StarsAchieved.Count && battleManager.StarsAchieved[i];
                bool isNew = i < battleManager.StarsNewlyAchieved.Count && battleManager.StarsNewlyAchieved[i];
                string star = $"<color=#{(achieved ? onColor : offColor)}>★</color>";
                string label = achieved ? stage.StarMissions[i].Describe() : $"<color=#{offColor}>{stage.StarMissions[i].Describe()}</color>";
                string newTag = isNew ? $" <color=#{onColor}><size=15><b>NEW</b></size></color>" : string.Empty;
                missions.Add($"{star} {label}{newTag}");
            }

            // 초 / 60 = 분(정수 나눗셈), % 60 = 남은 초. D2: 두 자리로 맞춤 (5 → 05).
            int seconds = Mathf.FloorToInt(battleManager.BattleSeconds);
            string time = $"<size=18><color=#8FB4D9>전투 시간 {seconds / 60}:{seconds % 60:D2}</color></size>";

            // string.Join(구분 글자, 목록): 미션들을 넓은 간격으로 이어 한 줄로 만든다.
            starText.text = $"{string.Join("        ", missions)}\n{time}";
        }

        // 파티 카드를 아군 수만큼 채운다 (편성 순서대로). 원본은 꺼 둔 채 복사본만 쓴다.
        private void ShowParty(IReadOnlyList<BattleUnit> allies)
        {
            Transform parent = partyCardTemplate.transform.parent;
            foreach (BattleUnit ally in allies)
            {
                ResultPartyCard card = Instantiate(partyCardTemplate, parent);
                card.gameObject.SetActive(true);
                card.Bind(ally);
            }
        }

        // 보상 칸을 받은 재료 수만큼 만든다. 보상이 없으면 안내 글을 보인다 (패배·테스트 전투 등).
        // 반환값: 만든 칸 목록 (하나씩 튀어나오게 하려고 처음에는 크기 0으로 둔다).
        private List<ResultRewardSlot> ShowRewards(IReadOnlyList<(MaterialData Material, int Amount)> rewards)
        {
            var slots = new List<ResultRewardSlot>();
            Transform parent = rewardSlotTemplate.transform.parent;
            foreach ((MaterialData material, int amount) in rewards)
            {
                ResultRewardSlot slot = Instantiate(rewardSlotTemplate, parent);
                slot.gameObject.SetActive(true);
                slot.Bind(material, amount);
                slot.transform.localScale = Vector3.zero;
                slots.Add(slot);
            }

            noRewardText.gameObject.SetActive(rewards.Count == 0);
            noRewardText.text = battleManager.IsVictory ? "획득한 보상이 없습니다" : "전투에서 패배해 보상이 없습니다";
            return slots;
        }

        // 등장 연출: 화면이 서서히 나타난 뒤 보상 칸이 하나씩 튀어나온다.
        // 실제 시간(unscaledDeltaTime)으로 움직여 배속·히트스톱의 영향을 받지 않는다.
        private IEnumerator PlayAppear(List<ResultRewardSlot> slots)
        {
            yield return Animate(fadeSeconds, t => group.alpha = t);

            foreach (ResultRewardSlot slot in slots)
            {
                StartCoroutine(Pop(slot.transform));
                yield return new WaitForSecondsRealtime(rewardInterval);
            }
        }

        // 칸 하나가 0 → 1.15배 → 1배로 튀어나온다. Mathf.Sin(t × π)는 0 → 1 → 0으로 오르내려 중간에 살짝 커지게 한다.
        private static IEnumerator Pop(Transform target)
        {
            yield return Animate(0.22f, t => target.localScale = Vector3.one * (t + Mathf.Sin(t * Mathf.PI) * 0.15f));
        }

        // seconds 동안 0 → 1 진행도를 apply(람다)에 넘긴다 (실제 시간 기준).
        private static IEnumerator Animate(float seconds, System.Action<float> apply)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }

            apply(1f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
