using System;
using Game.Battle;
using UnityEngine;

namespace Game.UI
{
    // 크게 맞은 유닛의 HP바를 흔들라는 알림 (BattleFxView → 아군 카드·보스 HP바).
    // 연출(BattleFxView)은 어떤 화면이 그 유닛의 HP바를 그리는지 모르므로, 알림만 보내고 각 화면이 자기 유닛이면 흔든다 (옵저버 패턴).
    public static class HpBarFeedback
    {
        // 흔들 유닛과 세기(픽셀). event Action<T1, T2>: 값 두 개를 받는 함수를 구독할 수 있는 알림.
        public static event Action<BattleUnit, float> ShakeRequested;

        // 도메인 리로드를 꺼 두었으므로 플레이를 시작할 때 구독을 비운다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ShakeRequested = null;
        }

        // ?.Invoke: 구독한 화면이 없으면 아무것도 하지 않는다.
        public static void RequestShake(BattleUnit unit, float strength)
        {
            ShakeRequested?.Invoke(unit, strength);
        }
    }
}
