using System.Collections;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    // 시작(타이틀) 화면 (기획서 2장, 임시). 화면 아무 곳이나 누르면 세이브를 불러오고 로비로 넘어간다.
    //  - 로그인 없이 로컬 세이브만 쓴다 (DB 사용 금지). 세이브가 없으면 새 게임을 만든다 (PlayerProfile)
    //  - "TOUCH TO START" 글자가 천천히 깜빡이고, 누르면 화면이 어두워진 뒤 로비 씬을 연다
    //  - 세이브 파일이 있으면 "이어하기", 없으면 "새 게임"을 아래에 보여준다
    public class TitleView : MonoBehaviour
    {
        [Tooltip("화면 전체를 덮는 투명 버튼 (아무 곳이나 누르면 시작)")]
        [SerializeField] private Button startButton;

        [Tooltip("깜빡이는 시작 안내 글자")]
        [SerializeField] private Text pressText;

        [Tooltip("세이브 상태 글자 (이어하기 / 새 게임)")]
        [SerializeField] private Text saveStateText;

        [Tooltip("로비로 넘어갈 때 어두워지는 검은 판 (처음엔 투명)")]
        [SerializeField] private Image fadeOverlay;

        [Tooltip("넘어갈 씬 이름 (Build Settings에 등록된 이름)")]
        [SerializeField] private string lobbySceneName = "Lobby";

        [Tooltip("깜빡이는 빠르기 · 가장 흐릴 때 투명도")]
        [SerializeField, Min(0.1f)] private float blinkSpeed = 2.2f;
        [SerializeField, Range(0f, 1f)] private float blinkMinAlpha = 0.25f;

        [Tooltip("누른 뒤 어두워지는 시간(초)")]
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

        private bool isStarting;

        // 버튼에 시작 함수를 연결하고, 세이브 상태 글자를 채운다.
        // AddListener(StartGame): 버튼을 누를 때 StartGame을 부르도록 등록 (메서드 이름을 그대로 넘기는 델리게이트).
        private void Awake()
        {
            startButton.onClick.AddListener(StartGame);
            if (saveStateText != null)
            {
                saveStateText.text = PlayerProfile.HasSaveFile ? "이어하기" : "새 게임";
            }

            SetFade(0f);
        }

        private void OnDestroy()
        {
            startButton.onClick.RemoveListener(StartGame);
        }

        // 시작 안내 글자 깜빡임: Sin(시간)은 -1~1을 오가므로 0.5 + 0.5 × Sin = 0~1 → 가장 흐릴 때 ~ 1 사이 투명도로 바꾼다.
        // 실제 시간(unscaledTime)을 써서 timeScale과 상관없이 같은 빠르기로 깜빡인다.
        private void Update()
        {
            if (pressText == null || isStarting)
            {
                return;
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * blinkSpeed);
            Color color = pressText.color;
            color.a = Mathf.Lerp(blinkMinAlpha, 1f, wave);
            pressText.color = color;
        }

        // 시작: 여러 번 눌러도 한 번만 실행된다.
        private void StartGame()
        {
            if (isStarting)
            {
                return;
            }

            isStarting = true;
            StartCoroutine(LoadLobby());
        }

        // 세이브를 불러온 뒤(없으면 새 게임), 화면을 어둡게 덮고 로비 씬을 연다.
        // 코루틴: yield return null마다 한 프레임 쉬며 어두워지는 모습을 보여준다.
        private IEnumerator LoadLobby()
        {
            PlayerProfile.Load();

            if (pressText != null)
            {
                Color color = pressText.color;
                color.a = 1f;
                pressText.color = color;
            }

            for (float elapsed = 0f; elapsed < fadeSeconds; elapsed += Time.unscaledDeltaTime)
            {
                SetFade(elapsed / fadeSeconds);
                yield return null;
            }

            SetFade(1f);
            SceneManager.LoadScene(lobbySceneName);
        }

        private void SetFade(float alpha)
        {
            if (fadeOverlay == null)
            {
                return;
            }

            fadeOverlay.color = new Color(0f, 0f, 0f, alpha);
            fadeOverlay.raycastTarget = alpha > 0f;
        }
    }
}
