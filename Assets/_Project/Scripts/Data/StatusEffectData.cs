using System;
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

    // 상태효과가 가진 효과 종류. 여러 개를 함께 고를 수 있다 (예: 스탯 보정 + 행동 불가).
    // [Flags]: 각 항목이 서로 다른 비트(1, 2, 4, 8...)를 쓰므로 OR(|)로 여러 종류를 한 값에 담을 수 있다.
    // Inspector(StatusEffectDataEditor)는 여기서 고른 종류의 칸만 보여준다.
    // 에셋에는 비트 값으로 저장되므로 기존 항목의 값은 바꾸지 말고, 새 종류는 다음 비트로 추가한다.
    [Flags]
    public enum StatusFeatures
    {
        // [InspectorName]: Inspector 드롭다운에 보일 한국어 이름. 저장 값(비트)과는 무관하다.
        [InspectorName("없음")] None = 0,
        [InspectorName("스탯 보정")] StatModifiers = 1 << 0,       // 스탯 보정 (공격력 증가 등)
        [InspectorName("지속피해")] DamageOverTime = 1 << 1,       // 지속피해 (화상·독)
        [InspectorName("지속 회복")] HealOverTime = 1 << 2,        // 지속 회복 (소생)
        [InspectorName("행동 불가")] SkipTurn = 1 << 3,            // 행동 불가 (기절)
        [InspectorName("지연 버프")] ConvertOnRemove = 1 << 4,     // 지연 버프 (지워지면 다른 버프로 바뀜)
        [InspectorName("피해 기록")] RecordDamage = 1 << 5,        // 피해 기록 (반격 태세)
        [InspectorName("디버프 면역")] DebuffImmunity = 1 << 6,    // 디버프 면역 (새로 걸리는 디버프를 막음, 도발 탱커 3스)
        [InspectorName("피해 무효")] BlockDamage = 1 << 7,         // 피해 무효 (모든 피해 0, 힐러 3스 무적)
    }

    // 상태효과(화상, 독, 버프 등)의 공통 정의. 여러 캐릭터가 같은 에셋을 공유한다.
    // 실제로 걸린 효과 하나하나(남은 턴, 부여자 등)는 StatusInstance가 따로 들고 있다.
    // 공통 칸(이름·분류·지속 턴·아이콘) + "효과 종류"에서 고른 효과의 칸만 쓴다.
    // 고르지 않은 효과의 값은 남아 있어도 전투에서 쓰이지 않는다 (아래 프로퍼티가 0·없음으로 돌려준다).
    [CreateAssetMenu(fileName = "NewStatusEffect", menuName = "Game/Status Effect Data")]
    public class StatusEffectData : ScriptableObject
    {
        [SerializeField] private string displayName = "새 상태효과";

        // 툴팁 맨 위에 붙는 짧은 설명. 수치(공격력 +30% 등)는 아래 설정에서 자동으로 만들어지므로,
        // 수치로 드러나지 않는 의미만 적는다. 예: "적 전용 버프. 강탈하면 아군 공격력 증가와 따로 쌓인다"
        [Tooltip("툴팁에 표시할 짧은 설명 (선택). 수치는 자동으로 표시된다")]
        [SerializeField, TextArea(1, 3)] private string description;
        [SerializeField] private StatusCategory category = StatusCategory.Debuff;
        [Tooltip("디버프 전용. 버프는 같은 버프끼리 중첩되지 않으므로 이 값과 관계없이 항상 갱신형이다")]
        [SerializeField] private StatusStackMode stackMode = StatusStackMode.Stack;

        [Tooltip("지속 턴 수 (효과를 가진 유닛의 자기 턴 기준)")]
        [SerializeField, Min(1)] private int duration = 3;

        [Tooltip("이 상태효과가 가진 효과 종류. 고른 종류의 설정 칸만 Inspector에 나온다")]
        [SerializeField] private StatusFeatures features;

        // 지속피해 기본 피해는 두 가지 중 하나 (기획서 3-8). 둘 다 0이면 지속피해 없음.
        //  - 화상형: 부여 당시 시전자 공격력 × 배율
        //  - 독형: 대상 최대 HP × 배율, 단 부여 당시 시전자 공격력 × 상한 배율을 넘지 않음
        [Tooltip("공격력 비례: 부여 당시 시전자 공격력 × 이 배율 (화상 = 0.6). 0이면 사용 안 함")]
        [SerializeField, Min(0f)] private float damageAttackRatio;

        [Tooltip("최대 HP 비례: 대상 최대 HP × 이 배율 (독 = 0.06). 0이면 사용 안 함")]
        [SerializeField, Min(0f)] private float damageTargetMaxHpRatio;

        [Tooltip("최대 HP 비례 피해의 상한 = 부여 당시 시전자 공격력 × 이 배율 (독 = 3.0). 0이면 상한 없음")]
        [SerializeField, Min(0f)] private float maxHpDamageAttackCap;

        // 지속 회복: 보유자의 턴이 끝날 때마다 HP를 회복한다 (기획서 4-1 '소생'). 지속피해와 같은 시점에 발동한다.
        // 회복량 = 부여 당시 시전자 최대 HP × 이 비율 (스냅샷). 회복이므로 치명타·방어력은 적용하지 않는다.
        [Tooltip("턴 종료 시 회복량 = 부여 당시 시전자 최대 HP × 이 비율 (소생 = 0.05)")]
        [SerializeField, Min(0f)] private float healSourceMaxHpRatio;

        [Tooltip("이 효과가 걸려 있는 동안 적용되는 스탯 보정 (공격력 증가 등)")]
        [SerializeField] private List<StatModifier> statModifiers = new List<StatModifier>();

        [Tooltip("머리 위 블럭에 겹쳐 그릴 그림 (흰색 그림 + 투명 배경). 비우면 색 블럭만 표시")]
        [SerializeField] private Texture2D icon;

        // 개인 버프: 자기 패시브로만 얻는 버프 (강탈 딜러 추격 등). 남이 걸어준 버프와 구분되도록 그림을 검은색으로 그린다.
        [Tooltip("개인 버프(자기 패시브로만 얻는 버프)면 체크. 머리 위 그림이 흰색 대신 검은색으로 표시된다")]
        [SerializeField] private bool isPersonal;

        // 영구 표시: 상태효과 칸에 걸리는 효과가 아니라 유닛의 고정 상태(무적 등)를 아이콘으로 보여줄 때 쓴다.
        // 남은 턴 숫자 대신 아무것도 표시하지 않고, 툴팁에는 "영구"로 나온다.
        [Tooltip("유닛의 고정 상태를 보여주는 표시 전용 효과면 체크 (남은 턴을 표시하지 않음)")]
        [SerializeField] private bool isPermanent;

        // 지연 버프: 걸려 있는 동안은 효과가 없고, 어떤 방식으로든 지워지면(만료, 해제, 소모, 밀려남) 이 효과로 바뀐다 (기획서 3-7).
        // 바뀐 효과는 그 턴의 종료 단계에 받는다. 사망으로 사라질 때는 바뀌지 않는다.
        [Tooltip("지워지면 대신 받을 효과")]
        [SerializeField] private StatusEffectData convertsTo;

        public string DisplayName => displayName;
        public string Description => description;
        public bool IsPersonal => isPersonal;
        public bool IsPermanent => isPermanent;
        public StatusCategory Category => category;
        public StatusStackMode StackMode => stackMode;
        public int Duration => duration;
        public Texture2D Icon => icon;
        public StatusFeatures Features => features;

        // 효과 종류를 하나 가지고 있는지. 비트 AND(&): 두 값에 같이 켜진 비트만 남아, 0이 아니면 그 종류가 있다.
        public bool Has(StatusFeatures feature) => (features & feature) != 0;

        // 아래 값들은 해당 효과 종류를 골랐을 때만 의미가 있다. 고르지 않았으면 0·없음을 돌려줘 전투에 영향이 없다.
        public float DamageAttackRatio => Has(StatusFeatures.DamageOverTime) ? damageAttackRatio : 0f;
        public float DamageTargetMaxHpRatio => Has(StatusFeatures.DamageOverTime) ? damageTargetMaxHpRatio : 0f;
        public float MaxHpDamageAttackCap => Has(StatusFeatures.DamageOverTime) ? maxHpDamageAttackCap : 0f;
        public bool DealsDamageOverTime => DamageAttackRatio > 0f || DamageTargetMaxHpRatio > 0f;

        public float HealSourceMaxHpRatio => Has(StatusFeatures.HealOverTime) ? healSourceMaxHpRatio : 0f;
        public bool HealsOverTime => HealSourceMaxHpRatio > 0f;

        public bool SkipsTurn => Has(StatusFeatures.SkipTurn);
        public bool RecordsDamageTaken => Has(StatusFeatures.RecordDamage);

        // 이 버프가 걸려 있는 동안 새 디버프가 걸리지 않는다. 추가 설정 칸은 없다.
        public bool GrantsDebuffImmunity => Has(StatusFeatures.DebuffImmunity);

        // 이 버프가 걸려 있는 동안 어떤 피해(지속피해 포함)도 받지 않는다. 보호막도 깎이지 않는다.
        public bool BlocksAllDamage => Has(StatusFeatures.BlockDamage);
        public StatusEffectData ConvertsTo => Has(StatusFeatures.ConvertOnRemove) ? convertsTo : null;

        // Array.Empty: 스탯 보정을 고르지 않았으면 빈 목록을 돌려준다 (새 리스트를 매번 만들지 않음).
        public IReadOnlyList<StatModifier> StatModifiers =>
            Has(StatusFeatures.StatModifiers) ? statModifiers : (IReadOnlyList<StatModifier>)Array.Empty<StatModifier>();

        // 행동 제어(CC) 효과인지. 지금은 기절(행동 불가)뿐이고, 도발·속박 등이 생기면 여기에 조건을 더한다.
        // 하이퍼캐리 변신처럼 "행동 제어 면역" 상태인 유닛에게는 걸리지 않는다.
        public bool IsCrowdControl => SkipsTurn;

        // 지속피해 1회의 기본 피해 (방어력·대상 쪽 보정을 적용하기 전 값).
        // snapshotAttack: 부여 당시 시전자 공격력, targetMaxHp: 대상 최대 HP
        // 예: 독 6%, 대상 HP 30000, 시전자 공격력 400, 상한 3.0 → min(1800, 1200) = 1200
        public double GetDamageOverTimeBase(int snapshotAttack, int targetMaxHp)
        {
            double attackPart = snapshotAttack * (double)DamageAttackRatio;
            double maxHpPart = targetMaxHp * (double)DamageTargetMaxHpRatio;

            // 상한 배율이 있으면 최대 HP 비례 피해가 "공격력 × 상한 배율"을 넘지 않게 자른다.
            // 보스처럼 HP가 매우 큰 대상에게 독이 지나치게 강해지는 것을 막는다 (기획서 3-8).
            if (MaxHpDamageAttackCap > 0f)
            {
                maxHpPart = Math.Min(maxHpPart, snapshotAttack * (double)MaxHpDamageAttackCap);
            }

            return attackPart + maxHpPart;
        }
    }
}
