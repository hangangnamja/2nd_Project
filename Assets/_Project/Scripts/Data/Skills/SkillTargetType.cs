using UnityEngine;

namespace Game.Data
{
    // 스킬 대상 규칙. "적"과 "아군"은 시전자 기준이다 (적 유닛이 쓰면 "적" = 플레이어 쪽).
    // 에셋에는 이 값이 숫자 순서로 저장되므로, 새 항목은 반드시 맨 뒤에 추가한다 (중간에 넣으면 기존 스킬의 대상이 바뀐다).
    public enum SkillTargetType
    {
        [InspectorName("적 단일")] EnemySingle,
        [InspectorName("적 전체")] EnemyAll,
        [InspectorName("자신")] Self,
        [InspectorName("아군 단일")] AllySingle,
        [InspectorName("아군 전체")] AllyAll,

        // 확산: 고른 적 + 편성 순서상 바로 양옆의 살아 있는 적. 고른 적이 메인 대상이다.
        [InspectorName("확산 (적 1 + 양옆)")] EnemyBlast,

        // 아군 단일인데 쓰러진 아군도 고를 수 있다 (힐러 3스 부활). 퇴장한 아군은 고를 수 없다.
        // 고르는 장면에서는 쓰러진 아군도 원래 자리에 흐리게 서므로, 그 자리나 하단 초상화 카드를 눌러 고른다.
        [InspectorName("아군 단일 (쓰러진 아군 포함)")] AllySingleOrFallen,
    }
}
