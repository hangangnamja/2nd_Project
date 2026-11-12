using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 행동 패턴(AI)을 가진 적 캐릭터 데이터 (보스, 보스 스테이지의 버프몹 등).
    // 일반 적은 "1스로 무작위 대상"만 반복하므로 패턴 칸이 필요 없어, 패턴이 필요한 적만 이 타입으로 만든다.
    // 기획서 3-10의 Behavior 트리 대신 우선 "조건 → 스킬" 목록으로 판단한다. 판단 결과(스킬·대상)만 전투에 넘기므로
    // 나중에 Behavior 트리로 바꿔도 전투 코드는 그대로다.
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Game/Enemy Character Data")]
    public class EnemyCharacterData : CharacterData
    {
        [Header("행동 패턴 (위에서부터 확인, 조건이 맞는 첫 패턴 실행)")]
        [SerializeField] private List<EnemyPattern> patterns = new List<EnemyPattern>();

        // 보스면 전투 화면 위쪽 가운데에 큰 HP바·버프 줄(BossHpBarView)로 보여준다 (기획서 3-11).
        [Tooltip("체크하면 전투 화면 상단에 보스 HP바로 표시한다")]
        [Label("보스 (상단 HP바)")]
        [SerializeField] private bool isBoss;

        public bool IsBoss => isBoss;

        // 자폭 카운트다운 표시 (부하): 머리 위에 "몇 턴 뒤 무언가 한다"를 남은 턴 숫자로 보여 준다. 비우면 표시 안 함.
        // 실제 행동은 패턴(자기 턴 N번째 이상)이 정하고, 이 칸은 표시만 한다 → 패턴의 N과 같은 값을 넣는다.
        [Header("카운트다운 표시 (선택)")]
        [Tooltip("머리 위에 남은 턴과 함께 보여줄 표시용 효과 (예: 자폭 준비). 비우면 표시 안 함")]
        [Label("표시 효과")]
        [SerializeField] private StatusEffectData countdownDisplay;

        [Tooltip("몇 번째 자기 턴에 행동하는지 (패턴의 '자기 턴 N번째 이상'과 같은 값)")]
        [Label("행동하는 자기 턴", Min = 1f)]
        [SerializeField] private int countdownTurns = 2;

        public IReadOnlyList<EnemyPattern> Patterns => patterns;
        public StatusEffectData CountdownDisplay => countdownDisplay;
        public int CountdownTurns => countdownTurns;
    }
}
