using Game.Battle;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 결과 화면의 파티 카드 한 장: 일러스트(없으면 직군 색 판) + 이름 + 레벨. 쓰러진 아군은 어둡게 덮는다.
    public class ResultPartyCard : MonoBehaviour
    {
        [SerializeField] private IllustrationView portrait;

        [Tooltip("일러스트가 없을 때 대신 보이는 직군 색 판")]
        [SerializeField] private Image fallback;

        [SerializeField] private Text nameText;
        [SerializeField] private Text levelText;

        [Tooltip("쓰러진 아군을 덮는 어두운 판 (\"전투 불능\" 글자 포함)")]
        [SerializeField] private GameObject downOverlay;

        [Header("직군 색 (일러스트가 없을 때)")]
        [SerializeField] private Color attackColor = new Color(0.85f, 0.3f, 0.3f);
        [SerializeField] private Color supportColor = new Color(0.3f, 0.75f, 0.45f);
        [SerializeField] private Color defenseColor = new Color(0.3f, 0.5f, 0.9f);
        [SerializeField] private Color healerColor = new Color(0.95f, 0.8f, 0.3f);

        // 아군 한 명을 카드에 채운다. 전투가 끝난 시점에 쓰러져 있으면 어둡게 덮는다.
        public void Bind(BattleUnit unit)
        {
            // Show는 그림이 없으면 false → 직군 색 판을 대신 보여준다.
            bool hasIllustration = portrait.Show(unit.Data.Illustration);
            fallback.gameObject.SetActive(!hasIllustration);
            fallback.color = GetRoleColor(unit.Data.Role);

            nameText.text = unit.Name;
            levelText.text = $"Lv.{unit.Level}";
            downOverlay.SetActive(unit.IsDead);
        }

        // switch 식: 직군마다 색 하나를 고른다 (_ = 그 밖의 경우 = 공격).
        private Color GetRoleColor(CharacterRole role) => role switch
        {
            CharacterRole.Support => supportColor,
            CharacterRole.Defense => defenseColor,
            CharacterRole.Heal => healerColor,
            _ => attackColor,
        };
    }
}
