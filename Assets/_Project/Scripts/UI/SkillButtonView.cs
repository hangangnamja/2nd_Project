using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투 스킬 버튼 하나의 모양 (붕괴 스타레일식 원형 버튼, 기획서 3-11).
    //  - 원 안에 스킬 이름, 원 아래에 칸 표시(1스·2스·3스)
    //  - 쿨타임 중: 원을 어둡게 덮고 남은 턴 숫자를 크게
    //  - 고른 스킬: 테두리가 금색이 되고 뒤에 빛이 켜지며 살짝 커짐
    // 클릭 처리는 BattleHud가 Button에 연결하고, 이 컴포넌트는 BattleHud가 넘겨준 상태를 그리기만 한다.
    public class SkillButtonView : MonoBehaviour
    {
        [SerializeField] private Button button;

        [Tooltip("원 테두리 (색으로 상태 표시)")]
        [SerializeField] private Image ring;

        [Tooltip("고른 스킬일 때 뒤에 켜지는 빛")]
        [SerializeField] private Image glow;

        [SerializeField] private Text nameText;

        [Tooltip("원 아래 칸 표시 (1스·2스·3스)")]
        [SerializeField] private Text tagText;

        [Tooltip("쿨타임 중 원을 덮는 어두운 판과 남은 턴 숫자")]
        [SerializeField] private GameObject cooldownCover;
        [SerializeField] private Text cooldownText;

        [Header("테두리 색")]
        [SerializeField] private Color normalRing = new Color(0.55f, 0.75f, 0.95f, 0.9f);
        [SerializeField] private Color selectedRing = new Color(1f, 0.82f, 0.35f, 1f);
        [SerializeField] private Color disabledRing = new Color(0.4f, 0.42f, 0.48f, 0.7f);

        [Tooltip("고른 스킬의 크기 배율")]
        [SerializeField, Min(1f)] private float selectedScale = 1.12f;

        [Tooltip("크기가 따라가는 빠르기")]
        [SerializeField, Min(0.1f)] private float scaleSpeed = 14f;

        private float targetScale = 1f;

        public Button Button => button;

        // 버튼 상태를 그린다.
        // canInput: 지금 플레이어가 고를 수 있는 차례인지 (아니면 이름 없이 칸 표시만)
        // remainingCooldown: 남은 쿨타임 턴 (0이면 사용 가능), usable: 쿨타임·경계 규칙까지 따져 지금 쓸 수 있는지
        public void Show(int slot, string skillName, bool canInput, int remainingCooldown, bool usable, bool selected)
        {
            tagText.text = $"{slot + 1}스";

            // interactable = false: 클릭을 막는다 (쿨타임·입력 차례가 아님).
            button.interactable = canInput && usable;

            // 쿨타임 중에는 이름을 숨기고 남은 턴 숫자만 크게 보여준다 (덮개 너머로 이름이 비쳐 숫자와 겹치지 않게).
            bool coolingDown = canInput && remainingCooldown > 0;
            nameText.text = canInput && !coolingDown ? skillName : string.Empty;
            cooldownCover.SetActive(coolingDown);
            cooldownText.text = coolingDown ? remainingCooldown.ToString() : string.Empty;

            bool isSelected = canInput && selected;
            ring.color = isSelected ? selectedRing : canInput && usable ? normalRing : disabledRing;
            glow.gameObject.SetActive(isSelected);
            targetScale = isSelected ? selectedScale : 1f;
        }

        // 크기를 목표 배율로 부드럽게 맞춘다. 1 - Exp(-빠르기 × 시간): 프레임 속도와 무관하게 같은 빠르기로 다가가는 비율.
        // unscaledDeltaTime: 히트스톱·일시정지와 상관없이 실제 시간으로 움직인다.
        private void Update()
        {
            float follow = 1f - Mathf.Exp(-scaleSpeed * Time.unscaledDeltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * targetScale, follow);
        }
    }
}
