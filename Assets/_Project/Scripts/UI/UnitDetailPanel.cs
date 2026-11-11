using System.Collections.Generic;
using System.Text;
using Game.Battle;
using Game.Battle.Status;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 아군 상세 창: 아래 초상화 카드를 꾹 누르면 열린다.
    //  - 화면 가운데에 그 캐릭터 일러스트(없으면 직군 색 실루엣)
    //  - 오른쪽 패널: 스탯(공격력·방어력·HP·속도·치명 확률·치명 피해·피해 증가) + 걸린 버프·디버프 목록(이름·남은 턴·효과 수치·부여자)
    // 스탯은 버프가 반영된 지금 값이고, 기본 스탯보다 오른 만큼을 (+n)으로 붙인다. 아무 곳이나 누르면 닫힌다.
    public class UnitDetailPanel : MonoBehaviour
    {
        [SerializeField] private BattleStageView stageView;

        [Tooltip("배경을 덮는 판. 누르면 닫힌다")]
        [SerializeField] private Button closeArea;

        [SerializeField] private IllustrationView illustration;

        [Tooltip("일러스트가 없을 때 보여줄 실루엣")]
        [SerializeField] private Image silhouette;

        [SerializeField] private Text nameText;
        [SerializeField] private Text statsText;
        [SerializeField] private Text effectsText;

        [Tooltip("올라간 수치 색 (기본보다 높을 때)")]
        [SerializeField] private Color upColor = new Color(0.45f, 0.95f, 0.55f);

        [Tooltip("내려간 수치 색 (디버프로 낮아졌을 때)")]
        [SerializeField] private Color downColor = new Color(1f, 0.45f, 0.4f);

        [Tooltip("효과 목록의 버프 제목 색")]
        [SerializeField] private Color buffHeaderColor = new Color(0.4f, 0.7f, 1f);

        [Tooltip("효과 목록의 디버프 제목 색")]
        [SerializeField] private Color debuffHeaderColor = new Color(1f, 0.45f, 0.4f);

        private void Awake()
        {
            closeArea.onClick.AddListener(Close);
            gameObject.SetActive(false);
        }

        // 그 유닛의 상세 정보를 채우고 창을 연다.
        public void Open(BattleUnit unit)
        {
            gameObject.SetActive(true);

            bool hasIllustration = illustration.Show(unit.Data.Illustration);
            silhouette.gameObject.SetActive(!hasIllustration);

            nameText.text = $"{unit.Name}  <size=26>Lv.{unit.Level}</size>";
            statsText.text = BuildStats(unit);
            effectsText.text = BuildEffects(unit);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        // 스탯 줄들. 기본 스탯(레벨 반영, 버프 전) 대비 지금 값의 차이를 색으로 붙인다.
        private string BuildStats(BattleUnit unit)
        {
            var text = new StringBuilder();
            AppendStat(text, "공격력", unit.Attack, unit.BaseAttack, isPercent: false);
            AppendStat(text, "방어력", unit.Defense, unit.BaseDefense, isPercent: false);
            text.AppendLine($"HP  <b>{unit.CurrentHp:N0}</b> / {unit.MaxHp:N0}");
            AppendStat(text, "속도", unit.Speed, unit.Data.BaseSpeed, isPercent: false);
            AppendStat(text, "치명 확률", unit.CritRate, unit.Data.CritRate, isPercent: true);
            AppendStat(text, "치명 피해", unit.CritDamage, unit.Data.CritDamage, isPercent: true);
            AppendStat(text, "피해 증가", unit.DamageBonus, 0d, isPercent: true);
            return text.ToString();
        }

        // "이름  값 (+차이)". isPercent면 0.2 → 20%로 표시. 차이가 거의 없으면(소수점 반올림 오차) 붙이지 않는다.
        private void AppendStat(StringBuilder text, string label, double value, double baseValue, bool isPercent)
        {
            double diff = value - baseValue;
            string Format(double v) => isPercent ? $"{v * 100d:0.#}%" : $"{v:N0}";

            string diffText = string.Empty;
            if (System.Math.Abs(diff) > (isPercent ? 0.0005d : 0.5d))
            {
                // ColorUtility.ToHtmlStringRGB: 색을 "RRGGBB" 글자로 바꿔 리치 텍스트 <color=#...>에 쓴다.
                Color color = diff > 0d ? upColor : downColor;
                string sign = diff > 0d ? "+" : "-";
                diffText = $"  <color=#{ColorUtility.ToHtmlStringRGB(color)}>({sign}{Format(System.Math.Abs(diff))})</color>";
            }

            text.AppendLine($"{label}  <b>{Format(value)}</b>{diffText}");
        }

        // 걸린 효과를 버프·디버프 두 묶음으로 나눠, 효과마다 툴팁과 같은 설명(이름·분류·남은 턴·수치·부여자)을 이어 붙인다.
        private string BuildEffects(BattleUnit unit)
        {
            UnitView view = stageView.FindView(unit);
            List<StatusInstance> statuses = view != null ? view.BuildDisplayedStatuses() : unit.Statuses.GetAll();

            // FindAll(조건 람다): 분류가 같은 효과만 모은다.
            List<StatusInstance> buffs = statuses.FindAll(status => status.Data.Category == StatusCategory.Buff);
            List<StatusInstance> debuffs = statuses.FindAll(status => status.Data.Category == StatusCategory.Debuff);

            return BuildSection("버프", buffs, buffHeaderColor) + "\n\n" + BuildSection("디버프", debuffs, debuffHeaderColor);
        }

        // 묶음 하나: 색 있는 제목(개수) + 효과 설명들. 없으면 "없음".
        private static string BuildSection(string title, List<StatusInstance> statuses, Color color)
        {
            string header = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}><b>{title} ({statuses.Count})</b></color>";
            if (statuses.Count == 0)
            {
                return $"{header}\n없음";
            }

            // ConvertAll(람다): 효과마다 설명 글로 바꾼 목록 → 빈 줄로 이어 붙인다.
            List<string> parts = statuses.ConvertAll(StatusDescriptionBuilder.Build);
            return $"{header}\n{string.Join("\n\n", parts)}";
        }
    }
}
