using System.Collections.Generic;
using Game.Battle;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투 화면 HUD (테스트용): 현재 차례 안내, 스킬 버튼 3개, 최근 로그를 표시한다.
    public class BattleHud : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private Text turnInfoText;
        [SerializeField] private Text logText;

        [Tooltip("1스, 2스, 3스 순서")]
        [SerializeField] private Button[] skillButtons = new Button[CharacterData.SkillCount];

        [Header("스킬 버튼 색")]
        [SerializeField] private Color normalButtonColor = Color.white;
        [SerializeField] private Color selectedButtonColor = new Color(1f, 0.85f, 0.3f);

        [Tooltip("화면에 남겨둘 최근 로그 줄 수")]
        [SerializeField, Min(1)] private int maxLogLines = 12;

        // Queue: 먼저 넣은 것이 먼저 나가는 자료구조. 줄 수가 넘치면 가장 오래된 로그부터 버린다.
        private readonly Queue<string> logLines = new Queue<string>();

        // 버튼마다 클릭 시 실행할 함수를 등록한다.
        private void Awake()
        {
            for (int i = 0; i < skillButtons.Length; i++)
            {
                // 람다식은 변수 자체를 기억하므로, 반복 변수 i를 그대로 쓰면 모든 버튼이 마지막 값(3)을 쓰게 된다.
                // 반복마다 새 지역 변수 slot에 복사해 두면 버튼마다 자기 번호를 기억한다.
                int slot = i;
                skillButtons[i].onClick.AddListener(() => battleManager.SelectSkill(slot));
            }
        }

        // 활성화될 때 전투 이벤트를 구독한다.
        private void OnEnable()
        {
            battleManager.StateChanged += Refresh;
            battleManager.LogAdded += AppendLog;
        }

        // 비활성화될 때 구독을 해제한다.
        private void OnDisable()
        {
            battleManager.StateChanged -= Refresh;
            battleManager.LogAdded -= AppendLog;
        }

        // 차례 안내와 스킬 버튼 상태를 현재 전투 상태에 맞춘다.
        private void Refresh()
        {
            turnInfoText.text = BuildTurnInfo();
            RefreshSkillButtons();
        }

        // 화면 상단 안내 문구를 만든다.
        private string BuildTurnInfo()
        {
            if (battleManager.IsBattleOver)
            {
                return "전투 종료";
            }

            BattleUnit actor = battleManager.CurrentActor;
            if (actor == null)
            {
                return "전투 준비 중";
            }

            string side = actor.IsAlly ? "아군" : "적";
            string extraTurn = battleManager.IsExtraTurn ? " (추가 턴)" : string.Empty;
            return $"사이클 {battleManager.CurrentCycle} | {side} {actor.Name} 차례{extraTurn}{BuildSkillGuide()}";
        }

        // 아군 차례일 때 선택된 스킬과 다음에 할 행동을 안내한다.
        private string BuildSkillGuide()
        {
            SkillData skill = battleManager.GetSelectedSkill();
            if (!battleManager.IsWaitingForPlayer || skill == null)
            {
                return string.Empty;
            }

            string action = BattleManager.NeedsSingleTarget(skill)
                ? "강조된 대상을 클릭"
                : "같은 버튼을 한 번 더 눌러 사용";
            return $"\n[{skill.SkillName}] {action}";
        }

        // 아군 차례일 때만 버튼을 켜고, 스킬 이름과 남은 쿨타임을 표시한다.
        private void RefreshSkillButtons()
        {
            BattleUnit actor = battleManager.CurrentActor;
            bool canInput = battleManager.IsWaitingForPlayer && actor != null;

            for (int slot = 0; slot < skillButtons.Length; slot++)
            {
                Button button = skillButtons[slot];

                // GetComponentInChildren: 버튼 자식에 있는 글자(Text) 컴포넌트를 찾는다.
                Text label = button.GetComponentInChildren<Text>();

                if (!canInput)
                {
                    button.interactable = false;
                    button.image.color = normalButtonColor;
                    label.text = $"{slot + 1}스";
                    continue;
                }

                SkillData skill = actor.Data.GetSkill(slot);
                int remaining = actor.GetRemainingCooldown(slot);

                // interactable = false: 버튼을 회색으로 바꾸고 클릭을 막는다 (쿨타임 중인 스킬).
                button.interactable = actor.CanUseSkill(slot);

                // 선택된 스킬은 이름 앞에 ▶를 붙이고 버튼 바탕색을 바꿔 강조한다.
                bool isSelected = slot == battleManager.SelectedSkillSlot;
                string marker = isSelected ? "▶ " : string.Empty;
                label.text = remaining > 0
                    ? $"{slot + 1}스 {skill.SkillName}\n(쿨 {remaining})"
                    : $"{marker}{slot + 1}스 {skill.SkillName}";
                button.image.color = isSelected ? selectedButtonColor : normalButtonColor;
            }
        }

        // 새 로그 한 줄을 추가하고, 정해진 줄 수를 넘으면 오래된 줄을 버린 뒤 화면에 표시한다.
        private void AppendLog(string message)
        {
            logLines.Enqueue(message);

            while (logLines.Count > maxLogLines)
            {
                logLines.Dequeue();
            }

            // string.Join: 여러 줄을 줄바꿈 문자로 이어 하나의 문자열로 만든다.
            logText.text = string.Join("\n", logLines);
        }
    }
}
