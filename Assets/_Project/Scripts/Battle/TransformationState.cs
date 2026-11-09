using System.Collections.Generic;
using Game.Data;

namespace Game.Battle
{
    // 변신 중인 유닛의 전투 중 값 (변신 설정은 TransformationData가, 바뀌는 값은 이 객체가 들고 있다).
    // 변신할 때 만들어지고, 해제되면 버려진다.
    public class TransformationState
    {
        public TransformationData Data { get; }

        // 변신 후 사용한 엑스트라 턴 수. 변신을 쓴 턴 자체는 세지 않는다.
        public int TurnsTaken { get; private set; }

        // 종결기가 이미 예약됐는지. 턴 제한과 강제 해제가 겹쳐도 종결기는 한 번만 나간다.
        public bool IsFinisherQueued { get; private set; }

        // 변신하면서 퇴장시킨 아군. 해제될 때 이 유닛들만 복귀시킨다.
        public IReadOnlyList<BattleUnit> ExitedAllies { get; }

        // 변신 스킬 슬롯별 남은 쿨타임. 원래 스킬의 쿨타임과 따로 센다.
        private readonly int[] cooldowns = new int[CharacterData.SkillCount];

        // 변신 설정과 퇴장시킨 아군 목록을 받는다. 변신 스킬 쿨타임은 각 스킬의 시작 쿨타임으로 채운다.
        public TransformationState(TransformationData data, IReadOnlyList<BattleUnit> exitedAllies)
        {
            Data = data;
            ExitedAllies = exitedAllies;

            for (int slot = 0; slot < cooldowns.Length; slot++)
            {
                cooldowns[slot] = data.GetSkill(slot).StartCooldown;
            }
        }

        // 변신 턴 제한에 도달했는지. 이 턴이 끝나면 종결기를 쓴다.
        public bool HasReachedTurnLimit => TurnsTaken >= Data.TurnLimit;

        // 변신 중 속도로 차례를 얻을 때(엑스트라 턴) 1 센다. 추가 턴은 세지 않는다 (기획서 4-3).
        public void CountTurn()
        {
            TurnsTaken++;
        }

        // 종결기를 예약했다고 표시한다. 이미 예약돼 있었으면 false → 중복 예약을 막는다.
        public bool TryMarkFinisherQueued()
        {
            if (IsFinisherQueued)
            {
                return false;
            }

            IsFinisherQueued = true;
            return true;
        }

        public int GetCooldown(int slot) => cooldowns[slot];

        public void SetCooldown(int slot, int turns) => cooldowns[slot] = turns;
    }
}
