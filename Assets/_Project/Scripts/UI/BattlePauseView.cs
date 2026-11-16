using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투 일시정지: 우상단 버튼을 누르면 시간을 멈추고 메뉴(계속하기 / 메인 화면으로)를 띄운다.
    // 시간 정지는 BattleTimeFx가 timeScale을 0으로 두는 방식이라, 연출·대기(WaitForSeconds)·전투 진행이 모두 그 자리에서 멈춘다.
    // 메뉴 판이 화면 전체를 덮어 클릭을 막으므로, 멈춘 동안 스킬 버튼·대상 선택은 눌리지 않는다.
    public class BattlePauseView : MonoBehaviour
    {
        [SerializeField] private BattleTimeFx timeFx;

        [Tooltip("우상단 일시정지 버튼")]
        [SerializeField] private Button pauseButton;

        [Tooltip("화면 전체를 덮는 메뉴 판 (처음에는 꺼 둔다)")]
        [SerializeField] private GameObject menuPanel;

        [SerializeField] private Button resumeButton;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private string lobbySceneName = "Lobby";

        private void Awake()
        {
            menuPanel.SetActive(false);
            pauseButton.onClick.AddListener(Pause);
            resumeButton.onClick.AddListener(Resume);
            lobbyButton.onClick.AddListener(GoToLobby);
        }

        // 시간을 멈추고 메뉴를 연다.
        private void Pause()
        {
            timeFx.SetPaused(true);
            menuPanel.SetActive(true);

            // SetAsLastSibling: 같은 Canvas에서 맨 위에 그려 다른 HUD(상세 창·컷인 등)를 덮는다.
            menuPanel.transform.SetAsLastSibling();
        }

        // 메뉴를 닫고 멈췄던 자리부터 이어서 진행한다.
        private void Resume()
        {
            menuPanel.SetActive(false);
            timeFx.SetPaused(false);
        }

        // 전투를 그만두고 로비(메인 화면)로 돌아간다. 이긴 전투가 아니므로 클리어 기록은 남지 않는다.
        // 씬을 넘기기 전에 정지를 풀어야 로비가 timeScale 0인 채로 열리지 않는다.
        private void GoToLobby()
        {
            timeFx.SetPaused(false);
            Time.timeScale = 1f;
            SceneManager.LoadScene(lobbySceneName);
        }
    }
}
