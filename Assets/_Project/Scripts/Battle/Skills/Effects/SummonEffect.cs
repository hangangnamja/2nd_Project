using System;
using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 시전자 편에 부하를 소환한다 (보스 3 군단 지휘관). 부하는 시전자의 레벨로 만들어지고, 행동 게이지는 처음(10000)부터.
    // 시전자가 소환한 부하가 전장에 "최대 수"만큼 있으면 더 소환하지 않는다 (빈자리만큼만).
    // 소환된 부하는 승리 조건에서 빠지고, 시전자가 쓰러지면 함께 퇴장한다 (BattleManager).
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("변신·전장/부하 소환", "시전자 편에 부하를 소환한다. 시전자가 소환한 부하는 전장에 최대 수까지만 있을 수 있다", 540)]
    [Serializable]
    public class SummonEffect : SkillEffect
    {
        [Tooltip("소환할 부하 캐릭터")]
        [Label("부하")]
        [SerializeField] private CharacterData summonData;

        [Tooltip("한 번에 소환하는 수")]
        [Label("소환 수", Min = 1f)]
        [SerializeField] private int count = 2;

        [Tooltip("시전자가 소환한 부하가 전장에 동시에 있을 수 있는 최대 수")]
        [Label("최대 수", Min = 1f)]
        [SerializeField] private int maxOnField = 4;

        // 빈자리만큼 소환한다. 예: 최대 4, 이미 3마리 → 2마리 중 1마리만 소환
        public override void Apply(SkillContext context)
        {
            if (summonData == null || context.Services.Summon == null)
            {
                context.Log("  [오류] 소환할 부하가 지정되지 않았습니다.");
                return;
            }

            // Math.Min: 소환 수와 남은 빈자리 중 작은 값만큼만 소환한다.
            int free = maxOnField - context.Caster.CountLivingSummons();
            int toSummon = Math.Min(count, free);

            for (int i = 0; i < toSummon; i++)
            {
                context.Services.Summon(context.Caster, summonData);
            }
        }
    }
}
