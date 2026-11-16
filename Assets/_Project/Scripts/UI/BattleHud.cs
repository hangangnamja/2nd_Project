using System.Collections.Generic;
using Game.Battle;
using Game.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    // 전투 화면 HUD: 현재 차례 안내, 스킬 버튼 3개(원형, SkillButtonView), 고른 스킬 이름판, 최근 로그를 표시한다.
    public class BattleHud : MonoBehaviour
    {
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private Text turnInfoText;
        [SerializeField] private Text logText;

        [Tooltip("1스, 2스, 3스 순서")]
        [SerializeField] private SkillButtonView[] skillButtons = new SkillButtonView[CharacterData.SkillCount];

        [Header("고른 스킬 이름판 (스킬 버튼 위)")]
        [SerializeField] private GameObject skillPlate;
        [SerializeField] private Text skillPlateName;

        [Tooltip("다음에 할 일 안내 (대상 누르기 / 한 번 더 누르기)")]
        [SerializeField] private Text skillPlateHint;

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
                skillButtons[i].Button.onClick.AddListener(() => battleManager.SelectSkill(slot));
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
            RefreshSkillPlate();
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

            // 변신 중 엑스트라 턴이면 몇 번째인지 보여준다. 예: 5회 중 1회째 → "[엑스트라 턴 1/5]"
            // 마지막 엑스트라 턴은 턴이 끝난 뒤 종결기로 변신이 해제되므로, 같은 턴 안이라도 변신 정보(Transformation)가
            // 이미 없을 수 있다. 그래서 IsTransformed도 함께 확인해 사라진 정보를 읽지 않게 한다.
            TransformationState transformation = actor.Transformation;
            string transform = battleManager.IsTransformTurn && actor.IsTransformed
                ? $" [엑스트라 턴 {transformation.TurnsTaken}/{transformation.Data.TurnLimit}]"
                : string.Empty;
            return $"웨이브 {battleManager.CurrentWaveNumber}/{battleManager.WaveCount} | 사이클 {battleManager.CurrentCycle} | "
                + $"{side} {actor.Name} 차례{extraTurn}{transform}";
        }

        // 아군 차례일 때 스킬 버튼 위 이름판에 고른 스킬 이름과 다음에 할 일을 보여준다. 입력 차례가 아니면 숨긴다.
        private void RefreshSkillPlate()
        {
            SkillData skill = battleManager.GetSelectedSkill();
            bool show = battleManager.IsWaitingForPlayer && skill != null;
            skillPlate.SetActive(show);
            if (!show)
            {
                return;
            }

            skillPlateName.text = $"{battleManager.SelectedSkillSlot + 1}스  {skill.SkillName}";
            skillPlateHint.text = BattleManager.NeedsSingleTarget(skill)
                ? "강조된 대상을 누르세요"
                : "같은 버튼을 한 번 더 눌러 사용";
        }

        // 아군 차례일 때만 버튼을 켜고, 스킬 이름과 남은 쿨타임을 표시한다.
        private void RefreshSkillButtons()
        {
            BattleUnit actor = battleManager.CurrentActor;
            bool canInput = battleManager.IsWaitingForPlayer && actor != null;

            for (int slot = 0; slot < skillButtons.Length; slot++)
            {
                SkillButtonView view = skillButtons[slot];
                if (!canInput)
                {
                    view.Show(slot, string.Empty, canInput: false, remainingCooldown: 0, usable: false, selected: false);
                    continue;
                }

                // 변신 중이면 변신 스킬 이름이 나오도록 유닛을 거쳐 꺼낸다.
                SkillData skill = actor.GetSkill(slot);
                view.Show(
                    slot,
                    skill.SkillName,
                    canInput: true,
                    remainingCooldown: actor.GetRemainingCooldown(slot),
                    usable: actor.CanUseSkill(slot),
                    selected: slot == battleManager.SelectedSkillSlot);
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
