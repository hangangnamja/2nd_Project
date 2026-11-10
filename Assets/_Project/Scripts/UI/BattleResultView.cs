using Game.Battle;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투 종료 화면 (임시): 전투가 끝나면 화면을 어둡게 덮고 "전투 종료"와 승패, 로비 버튼을 띄운다.
    // 덮는 패널이 클릭을 막으므로 끝난 전투의 유닛·스킬 버튼은 더 누를 수 없다.
    public class BattleResultView : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        [Tooltip("화면 전체를 어둡게 덮는 패널 (처음에는 꺼 둔다)")]
        [SerializeField] private GameObject panel;

        [SerializeField] private Text resultText;
        [SerializeField] private Button lobbyButton;
        [SerializeField] private string lobbySceneName = "Lobby";

        private void Awake()
        {
            panel.SetActive(false);

            // LoadScene: Battle 씬을 닫고 로비 씬을 연다 (Build Settings에 등록된 이름).
            lobbyButton.onClick.AddListener(() => SceneManager.LoadScene(lobbySceneName));
        }

        private void OnEnable()
        {
            battleManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            battleManager.StateChanged -= HandleStateChanged;
        }

        // 전투가 끝났으면 결과 패널을 띄운다. 끝나기 전에는 아무것도 하지 않는다.
        private void HandleStateChanged()
        {
            if (!battleManager.IsBattleOver || panel.activeSelf)
            {
                return;
            }

            resultText.text = battleManager.IsVictory ? "승리" : "패배";
            panel.SetActive(true);

            // SetAsLastSibling: 같은 Canvas 안에서 맨 마지막(= 맨 위)에 그려, 다른 HUD 위를 덮게 한다.
            panel.transform.SetAsLastSibling();
        }
    }
}
