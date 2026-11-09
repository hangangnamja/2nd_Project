using Game.Battle;
using Game.Battle.Status;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 상태효과 블럭을 클릭하면 그 효과의 짧은 설명을 띄우는 Canvas 툴팁 (표시 전용).
    // 패널·글자 모양은 씬의 Canvas에서 조절하고, 이 컴포넌트는 내용과 위치만 정한다.
    public class StatusTooltip : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;

        [Tooltip("툴팁 배경 패널 (내용 크기에 맞춰 늘어나도록 ContentSizeFitter를 붙여 둔다)")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text text;

        [Tooltip("클릭 위치에서 툴팁을 얼마나 떨어뜨려 띄울지 (픽셀)")]
        [SerializeField] private Vector2 offset = new Vector2(16f, 16f);

        private void Awake()
        {
            Hide();
        }

        // 전투가 진행되면(턴 넘어감, 피해 등) 툴팁의 남은 턴·보호막 수치가 낡으므로 닫는다.
        private void OnEnable()
        {
            battleManager.StateChanged += Hide;
        }

        private void OnDisable()
        {
            battleManager.StateChanged -= Hide;
        }

        // 상태효과 설명을 채우고, 클릭한 화면 위치 옆에 띄운다.
        public void Show(StatusInstance status, Vector2 screenPosition)
        {
            text.text = StatusDescriptionBuilder.Build(status);
            panel.gameObject.SetActive(true);
            PlaceNear(screenPosition);
        }

        public void Hide()
        {
            panel.gameObject.SetActive(false);
        }

        // 툴팁이 화면 밖으로 나가지 않도록, 클릭 위치에서 화면 가운데 쪽으로 펼친다.
        // pivot: 패널의 기준점 (0,0 = 왼쪽 아래, 1,1 = 오른쪽 위). 오른쪽 절반을 클릭하면 기준점을 오른쪽에 두어 왼쪽으로 펼친다.
        // Screen Space - Overlay Canvas에서는 position이 곧 화면 픽셀 좌표다.
        private void PlaceNear(Vector2 screenPosition)
        {
            bool onRightHalf = screenPosition.x > Screen.width * 0.5f;
            bool onTopHalf = screenPosition.y > Screen.height * 0.5f;

            panel.pivot = new Vector2(onRightHalf ? 1f : 0f, onTopHalf ? 1f : 0f);

            // 펼치는 방향으로 offset만큼 띄워, 커서가 툴팁 글자를 가리지 않게 한다.
            Vector2 direction = new Vector2(onRightHalf ? -1f : 1f, onTopHalf ? -1f : 1f);
            panel.position = screenPosition + Vector2.Scale(offset, direction);
        }
    }
}
