using System.Collections.Generic;
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

        public IReadOnlyList<EnemyPattern> Patterns => patterns;
    }
}
