using Game.Data;
using UnityEngine;

namespace Game.Battle
{
    // 전투 중인 유닛 하나. CharacterData(원본)를 참고해 만들어지고, 전투 중 바뀌는 값을 들고 있다.
    // MonoBehaviour가 아닌 순수 C# 클래스라서 화면(3D 모델, UI)과 분리되어 있다.
    public class BattleUnit
    {
        public CharacterData Data { get; }
        public bool IsAlly { get; }
        public int CurrentHp { get; private set; }

        public string Name => Data.DisplayName;
        public int MaxHp => Data.MaxHp;

        // 지금 단계에서는 HP가 0 이하이면 바로 사망으로 본다.
        // 나중에 "공격이 끝난 뒤 사망 처리" 규칙을 넣을 때 별도의 사망 상태로 분리한다.
        public bool IsDead => CurrentHp <= 0;

        // 원본 데이터와 소속(아군/적)을 받아 전투용 유닛을 만든다. 시작 HP는 최대 HP.
        public BattleUnit(CharacterData data, bool isAlly)
        {
            Data = data;
            IsAlly = isAlly;
            CurrentHp = data.MaxHp;
        }

        // 피해를 받아 HP를 줄이고, 실제로 줄어든 양을 돌려준다 (로그·연출용).
        public int TakeDamage(int amount)
        {
            int hpBefore = CurrentHp;

            // Mathf.Max(0, ...): 남은 HP가 음수가 되지 않도록 0 아래로 내려가면 0으로 고정한다.
            CurrentHp = Mathf.Max(0, CurrentHp - amount);

            // 남은 HP보다 큰 피해가 들어와도 실제로 깎인 만큼만 돌려준다 (예: HP 30에 100 피해 → 30).
            return hpBefore - CurrentHp;
        }
    }
}
