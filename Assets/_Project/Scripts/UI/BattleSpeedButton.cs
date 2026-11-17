using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 우상단 배속 버튼: 누를 때마다 BattleTimeFx의 배속을 다음 단계로 넘기고(1x → 2x → 3x → 1x), 글자에 지금 배속을 보여준다.
    public class BattleSpeedButton : MonoBehaviour
    {
        [SerializeField] private BattleTimeFx timeFx;
        [SerializeField] private Button button;
        [SerializeField] private Text label;

        [Tooltip("1배속이 아닐 때 글자 색 (켜져 있음을 알아보기 쉽게)")]
        [SerializeField] private Color activeColor = new Color(1f, 0.8f, 0.35f);

        private Color idleColor;

        private void Awake()
        {
            idleColor = label.color;
            button.onClick.AddListener(timeFx.CycleSpeed);
        }

        // 이벤트 구독·해제: 켜져 있는 동안만 배속 변경 알림을 받는다 (꺼진 뒤 지워진 버튼을 부르지 않게).
        private void OnEnable()
        {
            timeFx.SpeedChanged += Show;
        }

        // 처음 글자는 Start에서 그린다: BattleTimeFx.Awake가 저장된 배속을 불러온 뒤라 실제 배속과 어긋나지 않는다.
        private void Start()
        {
            Show(timeFx.Speed);
        }

        private void OnDisable()
        {
            timeFx.SpeedChanged -= Show;
        }

        // 글자를 "2x"처럼 바꾸고, 1배속이 아니면 강조 색으로 칠한다. "0.#": 소수점이 없으면 생략 (1.5x는 그대로).
        private void Show(float speed)
        {
            label.text = $"{speed:0.#}x";
            label.color = speed > 1f ? activeColor : idleColor;
        }
    }
}
