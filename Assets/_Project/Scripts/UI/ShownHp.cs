using System;
using System.Collections.Generic;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 화면에 보여줄 HP·보호막 (연출과 맞춘 값).
    // 전투 로직은 행동 하나를 한 프레임에 끝까지 계산해 HP가 바로 바뀌지만, 연출(BattleFxView)은 모션 → 타격 순서로 나중에 재생한다.
    // 그래서 연출이 도는 동안에는 "아직 맞지 않은 것처럼" 이전 값을 붙잡아 두고, 타격 박자가 재생될 때 그 순간의 값으로 바꾼다.
    //  - Hold: 이번 연출에서 처음 보이는 유닛의 "맞기 전" 값을 붙잡는다 (이미 붙잡았으면 그대로)
    //  - Set: 박자를 재생하면서 그 순간의 값으로 바꾼다
    //  - ReleaseAll: 연출이 끝나면 놓아 실제 값을 그대로 보여준다
    // 붙잡은 값이 없는 유닛은 실제 값을 돌려준다. HP바·숫자를 그리는 화면(카드, 보스 바, 머리 위 글자)은 이 값을 읽는다.
    public static class ShownHp
    {
        // 값 묶음 (HP, 보호막). 튜플 (int, int): 이름 붙은 값 두 개를 하나로 다룬다.
        private static readonly Dictionary<BattleUnit, (int Hp, int Shield)> held = new Dictionary<BattleUnit, (int Hp, int Shield)>();

        // 보여줄 값이 바뀌었을 때 알린다 → 화면이 HP바를 다시 그린다. event Action: 구독한 함수를 인자 없이 부른다.
        public static event Action Changed;

        // 도메인 리로드를 꺼 두었으므로(Architecture 2장) 플레이를 시작할 때 static 값을 직접 비운다.
        // RuntimeInitializeOnLoadMethod(SubsystemRegistration): 씬을 불러오기 전에 가장 먼저 한 번 불린다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            held.Clear();
            Changed = null;
        }

        // 화면에서 쓰러진 것으로 보여줄지. 로직에서는 이미 쓰러졌어도, 그 피해를 연출이 아직 붙잡고 있으면(held) 살아 있는 것처럼 둔다.
        // → 회색·보스 바 사라짐·상태효과 지움은 그 공격의 연출이 끝나 ReleaseAll로 놓인 뒤에 일어난다.
        public static bool IsDead(BattleUnit unit)
        {
            return unit.IsDead && !held.ContainsKey(unit);
        }

        // 화면에서 전장에 서 있는 것으로 보여줄지 (BattleUnit.IsOnField의 화면판: 화면상 살아 있고 퇴장하지 않음).
        public static bool IsOnField(BattleUnit unit)
        {
            return !IsDead(unit) && !unit.IsExited;
        }

        public static int Hp(BattleUnit unit)
        {
            // TryGetValue: 붙잡은 값이 있으면 value에 담고 true.
            return held.TryGetValue(unit, out (int Hp, int Shield) value) ? value.Hp : unit.CurrentHp;
        }

        public static int Shield(BattleUnit unit)
        {
            return held.TryGetValue(unit, out (int Hp, int Shield) value) ? value.Shield : unit.ShieldAmount;
        }

        // 이번 연출에서 처음 등장한 유닛의 "맞기 전" 값을 붙잡는다. 이미 붙잡은 유닛은 바꾸지 않는다
        // (같은 행동에서 두 번째 이후 사건의 "전" 값은 앞 사건의 "후" 값이라, 처음 값만 의미가 있다).
        public static void Hold(BattleUnit unit, int hp, int shield)
        {
            if (!held.ContainsKey(unit))
            {
                held[unit] = (hp, shield);
                Changed?.Invoke();
            }
        }

        // 박자를 재생할 때 그 순간의 값으로 바꾼다. 붙잡지 않은 유닛은 이미 실제 값을 보여주므로 건너뛴다.
        public static void Set(BattleUnit unit, int hp, int shield)
        {
            if (held.ContainsKey(unit))
            {
                held[unit] = (hp, shield);
                Changed?.Invoke();
            }
        }

        // 화면에서도 HP가 0까지 깎인(이미 쓰러지는 타격을 보여준) 유닛만 놓는다 → 그 유닛은 화면에서도 쓰러진 것이 된다.
        // 행동(스킬·추가 공격) 하나의 연출이 끝나고 다음 행동이 시작될 때 불러, "공격 → 피해 → 사망 → 추가 공격" 순서로 보이게 한다.
        // 아직 0까지 보여주지 않은 유닛(다음 행동에서 쓰러지는 유닛)은 그대로 붙잡아 둔다.
        public static void ReleaseFallen()
        {
            // RemoveAll이 없는 Dictionary라, 놓을 유닛을 먼저 모은 뒤 지운다 (돌면서 지우면 오류).
            var fallen = new List<BattleUnit>();
            foreach (KeyValuePair<BattleUnit, (int Hp, int Shield)> pair in held)
            {
                if (pair.Key.IsDead && pair.Value.Hp <= 0)
                {
                    fallen.Add(pair.Key);
                }
            }

            if (fallen.Count == 0)
            {
                return;
            }

            foreach (BattleUnit unit in fallen)
            {
                held.Remove(unit);
            }

            Changed?.Invoke();
        }

        // 연출이 끝나면 전부 놓는다 → 화면이 실제 값으로 맞춰진다 (연출에 안 잡힌 변화도 여기서 반영).
        public static void ReleaseAll()
        {
            if (held.Count == 0)
            {
                return;
            }

            held.Clear();
            Changed?.Invoke();
        }
    }
}
