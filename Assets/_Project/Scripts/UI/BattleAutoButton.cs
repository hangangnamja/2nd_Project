using Game.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 우상단 자동 전투 버튼: 누를 때마다 자동 전투를 켜고 끄며, 켜져 있으면 글자를 강조 색으로 칠한다.
    // 켜고 끈 상태는 다음 전투에도 이어지도록 PlayerPrefs(기기에 남는 개인 설정)에 기억한다.
    public class BattleAutoButton : MonoBehaviour
    {
        private const string AutoPrefsKey = "Battle.Auto";

        [SerializeField] private BattleManager battleManager;
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        [Tooltip("자동 전투가 켜져 있을 때 글자 색")]
        [SerializeField] private Color activeColor = new Color(1f, 0.8f, 0.35f);

        private Color idleColor;

        private void Awake()
        {
            idleColor = label.color;
            button.onClick.AddListener(Toggle);
        }

        // 지난 전투에서 켜 두었으면 켠 채로 시작한다. PlayerPrefs는 bool이 없어 1 = 켜짐, 0 = 꺼짐으로 저장한다.
        private void Start()
        {
            bool enabledLastTime = PlayerPrefs.GetInt(AutoPrefsKey, 0) == 1;
            battleManager.SetAutoBattle(enabledLastTime);
            Show(enabledLastTime);
        }

        // 켜짐 ↔ 꺼짐을 바꾸고 기억한다.
        private void Toggle()
        {
            bool enabled = !battleManager.IsAutoBattle;
            battleManager.SetAutoBattle(enabled);
            PlayerPrefs.SetInt(AutoPrefsKey, enabled ? 1 : 0);
            Show(enabled);
        }

        private void Show(bool enabled)
        {
            label.color = enabled ? activeColor : idleColor;
        }
    }
}
