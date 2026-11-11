using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Battle
{
    // 기간 오라 (기획서 3-7 오라, 4-3 아스트라 공연): 주인이 생성하는 동안 같은 편 전원에게 스탯 보정을 준다.
    // 버프 칸에 들어가지 않으므로 강탈·소모·만료·밀어내기 대상이 아니고, 주인이 퇴장해도 계속 적용된다.
    // 남은 턴은 주인의 정규 턴이 끝날 때마다 1씩 줄고, 0이 되면 끝난다.
    public class TimedAura
    {
        public string Name { get; }
        public IReadOnlyList<StatModifier> StatModifiers { get; }

        // 오라가 켜져 있는 동안 주인의 턴이 강제로 스킵되는지 (공연: 직접 행동하지 않는 대신 오라를 유지)
        public bool SkipsOwnerTurns { get; }

        // 주인 머리 위에 보여줄 표시용 효과 (아이콘·툴팁). 없으면 null.
        public StatusEffectData DisplayStatus { get; }

        // 오라가 켜져 있는 동안 전장에 띄울 이펙트 프리팹 (공연: 흐르는 악보·음표). 없으면 null.
        // 로직은 프리팹을 들고만 있고, 실제로 띄우고 지우는 일은 화면(AuraFieldView)이 한다.
        public GameObject FieldEffect { get; }

        public int RemainingTurns { get; private set; }

        // 오라를 생성한 그 턴인지. 생성한 턴의 끝에서는 남은 턴을 줄이지 않는다
        // → "3턴 동안 공연" = 생성한 다음 자기 턴부터 3턴 (스킵되는 턴 3번)
        public bool IsStartingTurn { get; private set; } = true;

        public TimedAura(
            string name,
            IReadOnlyList<StatModifier> statModifiers,
            int durationTurns,
            bool skipsOwnerTurns,
            StatusEffectData displayStatus,
            GameObject fieldEffect)
        {
            FieldEffect = fieldEffect;
            Name = name;
            StatModifiers = statModifiers;
            RemainingTurns = durationTurns;
            SkipsOwnerTurns = skipsOwnerTurns;
            DisplayStatus = displayStatus;
        }

        // 주인의 정규 턴이 끝날 때 부른다. 생성한 턴이면 표시만 지우고, 아니면 남은 턴을 1 줄인다. 끝났으면 true.
        public bool Tick()
        {
            if (IsStartingTurn)
            {
                IsStartingTurn = false;
                return false;
            }

            RemainingTurns--;
            return RemainingTurns <= 0;
        }
    }
}
