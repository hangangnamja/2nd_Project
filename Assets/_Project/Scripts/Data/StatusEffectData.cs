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
        [Tooltip("디버프 전용. 버프는 같은 버프끼리 중첩되지 않으므로 이 값과 관계없이 항상 갱신형이다")]
        [SerializeField] private StatusStackMode stackMode = StatusStackMode.Stack;

        [Tooltip("지속 턴 수 (효과를 가진 유닛의 자기 턴 기준)")]
        [SerializeField, Min(1)] private int duration = 3;

        // 지속피해 기본 피해는 두 가지 중 하나 (기획서 3-8). 둘 다 0이면 지속피해 없음.
        //  - 화상형: 부여 당시 시전자 공격력 × 배율
        //  - 독형: 대상 최대 HP × 배율, 단 부여 당시 시전자 공격력 × 상한 배율을 넘지 않음
        [Header("지속피해")]
        [Tooltip("공격력 비례: 부여 당시 시전자 공격력 × 이 배율 (화상 = 0.6). 0이면 사용 안 함")]
        [SerializeField, Min(0f)] private float damageAttackRatio;

        [Tooltip("최대 HP 비례: 대상 최대 HP × 이 배율 (독 = 0.06). 0이면 사용 안 함")]
        [SerializeField, Min(0f)] private float damageTargetMaxHpRatio;

        [Tooltip("최대 HP 비례 피해의 상한 = 부여 당시 시전자 공격력 × 이 배율 (독 = 3.0). 0이면 상한 없음")]
        [SerializeField, Min(0f)] private float maxHpDamageAttackCap;

        [Header("스탯 보정")]
        [Tooltip("이 효과가 걸려 있는 동안 적용되는 스탯 보정 (공격력 증가 등)")]
        [SerializeField] private List<StatModifier> statModifiers = new List<StatModifier>();

        [Header("표시")]
        [Tooltip("머리 위 블럭에 겹쳐 그릴 그림 (흰색 그림 + 투명 배경). 비우면 색 블럭만 표시")]
        [SerializeField] private Texture2D icon;

        // 개인 버프: 자기 패시브로만 얻는 버프 (강탈 딜러 추격 등). 남이 걸어준 버프와 구분되도록 그림을 검은색으로 그린다.
        [Tooltip("개인 버프(자기 패시브로만 얻는 버프)면 체크. 머리 위 그림이 흰색 대신 검은색으로 표시된다")]
        [SerializeField] private bool isPersonal;

        // 지연 버프: 걸려 있는 동안은 효과가 없고, 어떤 방식으로든 지워지면(만료, 해제, 소모, 밀려남) 이 효과로 바뀐다 (기획서 3-7).
        // 바뀐 효과는 그 턴의 종료 단계에 받는다. 사망으로 사라질 때는 바뀌지 않는다.
        [Header("지연 버프")]
        [Tooltip("지워지면 대신 받을 효과. 비우면 일반 효과")]
        [SerializeField] private StatusEffectData convertsTo;

        public string DisplayName => displayName;
        public bool IsPersonal => isPersonal;
        public StatusEffectData ConvertsTo => convertsTo;
        public StatusCategory Category => category;
        public StatusStackMode StackMode => stackMode;
        public int Duration => duration;
        public float DamageAttackRatio => damageAttackRatio;
        public float DamageTargetMaxHpRatio => damageTargetMaxHpRatio;
        public float MaxHpDamageAttackCap => maxHpDamageAttackCap;
        public bool DealsDamageOverTime => damageAttackRatio > 0f || damageTargetMaxHpRatio > 0f;

        // 지속피해 1회의 기본 피해 (방어력·대상 쪽 보정을 적용하기 전 값).
        // snapshotAttack: 부여 당시 시전자 공격력, targetMaxHp: 대상 최대 HP
        // 예: 독 6%, 대상 HP 30000, 시전자 공격력 400, 상한 3.0 → min(1800, 1200) = 1200
        public double GetDamageOverTimeBase(int snapshotAttack, int targetMaxHp)
        {
            double attackPart = snapshotAttack * (double)damageAttackRatio;
            double maxHpPart = targetMaxHp * (double)damageTargetMaxHpRatio;

            // 상한 배율이 있으면 최대 HP 비례 피해가 "공격력 × 상한 배율"을 넘지 않게 자른다.
            // 보스처럼 HP가 매우 큰 대상에게 독이 지나치게 강해지는 것을 막는다 (기획서 3-8).
            if (maxHpDamageAttackCap > 0f)
            {
                maxHpPart = System.Math.Min(maxHpPart, snapshotAttack * (double)maxHpDamageAttackCap);
            }

            return attackPart + maxHpPart;
        }
        public IReadOnlyList<StatModifier> StatModifiers => statModifiers;
        public Texture2D Icon => icon;
    }
}
