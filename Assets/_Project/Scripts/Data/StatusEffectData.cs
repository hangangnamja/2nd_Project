using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    // 버프인지 디버프인지. 버프 10칸, 디버프 10칸을 따로 쓴다 (기획서 3-7).
    public enum StatusCategory
    {
        Buff,
        Debuff,
    }

    // 같은 효과를 다시 걸었을 때의 처리 방식 (기획서 3-7).
    public enum StatusStackMode
    {
        // 걸 때마다 새 인스턴스가 칸을 하나씩 차지한다 (화상, 독).
        Stack,

        // 이미 걸려 있으면 새로 추가하지 않고 남은 턴만 처음 값으로 되돌린다.
        Refresh,
    }

    // 상태효과(화상, 독, 버프 등)의 공통 정의. 여러 캐릭터가 같은 에셋을 공유한다.
    // 실제로 걸린 효과 하나하나(남은 턴, 부여자 등)는 StatusInstance가 따로 들고 있다.
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Game/Status Effect Data")]
    public class StatusEffectData : ScriptableObject
    {
        [SerializeField] private string displayName = "새 상태효과";
        [SerializeField] private StatusCategory category = StatusCategory.Debuff;
        [SerializeField] private StatusStackMode stackMode = StatusStackMode.Stack;

        [Tooltip("지속 턴 수 (효과를 가진 유닛의 자기 턴 기준)")]
        [SerializeField, Min(1)] private int duration = 3;

        [Header("지속피해")]
        [Tooltip("턴 종료 시 주는 피해 = 부여 당시 시전자 공격력 × 이 배율. 0이면 지속피해 없음 (화상 = 0.6)")]
        [SerializeField, Min(0f)] private float damageAttackRatio;

        [Header("스탯 보정")]
        [Tooltip("이 효과가 걸려 있는 동안 적용되는 스탯 보정 (공격력 증가 등)")]
        [SerializeField] private List<StatModifier> statModifiers = new List<StatModifier>();

        [Header("표시")]
        [Tooltip("머리 위 블럭에 겹쳐 그릴 그림 (흰색 그림 + 투명 배경). 비우면 색 블럭만 표시")]
        [SerializeField] private Texture2D icon;

        public string DisplayName => displayName;
        public StatusCategory Category => category;
        public StatusStackMode StackMode => stackMode;
        public int Duration => duration;
        public float DamageAttackRatio => damageAttackRatio;
        public bool DealsDamageOverTime => damageAttackRatio > 0f;
        public IReadOnlyList<StatModifier> StatModifiers => statModifiers;
        public Texture2D Icon => icon;
    }
}
