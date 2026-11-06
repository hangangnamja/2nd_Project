using System;
using Game.Data;

namespace Game.Battle
{
    // 전투 중인 유닛 하나. CharacterData(원본)를 참고해 만들어지고, 전투 중 바뀌는 값을 들고 있다.
    // MonoBehaviour가 아닌 순수 C# 클래스라서 화면(3D 모델, UI)과 분리되어 있다.
    public class BattleUnit
    {
        public CharacterData Data { get; }
        public bool IsAlly { get; }

        // 편성 순서 (0부터). 행동 순서가 동시에 도착했을 때 마지막 기준으로 쓴다.
        public int FormationIndex { get; }

        public int CurrentHp { get; private set; }

        // 행동 게이지 트랙에서 도착점까지 남은 거리. 행동하면 트랙 길이(10000)로 되돌아간다.
        // 행동수치가 아니라 남은 거리를 저장해야, 속도가 중간에 바뀌어도 도착 시간만 다시 계산하면 된다.
        public double RemainingDistance { get; private set; }

        public string Name => Data.DisplayName;
        public int MaxHp => Data.MaxHp;
        public CharacterRole Role => Data.Role;

        // 최종 속도. 지금은 기초 속도 그대로이고, 속도 버프는 상태효과 단계에서
        // "기초 속도 × (1 + 속도 %) + 고정 속도" 공식으로 확장한다.
        public double Speed => Data.BaseSpeed;

        // 행동수치 = 남은 거리 / 속도. 도착까지 걸리는 시간이며, 가장 작은 유닛이 다음에 행동한다.
        // 예: 남은 거리 10000, 속도 120 → 약 83.3
        public double ActionValue => RemainingDistance / Speed;

        // 지금 단계에서는 HP가 0 이하이면 바로 사망으로 본다.
        // 나중에 "공격이 끝난 뒤 사망 처리" 규칙을 넣을 때 별도의 사망 상태로 분리한다.
        public bool IsDead => CurrentHp <= 0;

        // 원본 데이터, 소속(아군/적), 편성 순서를 받아 전투용 유닛을 만든다.
        // 시작 HP는 최대 HP, 시작 거리는 트랙 길이 (기획서: 전투 시작 시 전원 10000).
        public BattleUnit(CharacterData data, bool isAlly, int formationIndex)
        {
            Data = data;
            IsAlly = isAlly;
            FormationIndex = formationIndex;
            CurrentHp = data.MaxHp;
            RemainingDistance = TurnScheduler.TrackLength;
        }

        // 피해를 받아 HP를 줄이고, 실제로 줄어든 양을 돌려준다 (로그·연출용).
        public int TakeDamage(int amount)
        {
            int hpBefore = CurrentHp;

            // Math.Max(0, ...): 남은 HP가 음수가 되지 않도록 0 아래로 내려가면 0으로 고정한다.
            CurrentHp = Math.Max(0, CurrentHp - amount);

            // 남은 HP보다 큰 피해가 들어와도 실제로 깎인 만큼만 돌려준다 (예: HP 30에 100 피해 → 30).
            return hpBefore - CurrentHp;
        }

        // elapsedTime만큼 시간이 흐를 때 그동안 이동한 거리(속도 × 시간)만큼 도착점에 가까워진다.
        // 부동소수점 오차로 음수가 되지 않도록 0 아래는 0으로 고정한다.
        public void Advance(double elapsedTime)
        {
            RemainingDistance = Math.Max(0d, RemainingDistance - Speed * elapsedTime);
        }

        // 행동을 마친 유닛을 트랙 출발점으로 되돌린다.
        public void ResetDistance()
        {
            RemainingDistance = TurnScheduler.TrackLength;
        }
    }
}
