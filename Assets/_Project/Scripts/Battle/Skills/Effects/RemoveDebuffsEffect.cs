using System;
using System.Collections.Generic;
using Game.Battle.Status;
using Game.Core;
using UnityEngine;

namespace Game.Battle.Effects
{
    // 대상에게 걸린 디버프를 지운다 (힐러 2스·3스). 1개만 지울지, 전부 지울지 Inspector에서 고른다.
    // 1개만 지우면 가장 최근에 걸린 디버프가 지워진다.
    // 지운 효과는 StatusApplier.ReportRemoved를 거쳐 "지워짐"으로 처리한다 (만료·소모와 같은 경로).
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("상태효과/디버프 제거", "대상의 디버프를 지운다. '전부'를 끄면 가장 최근에 걸린 디버프 1개만 지운다", 330)]
    [Serializable]
    public class RemoveDebuffsEffect : SkillEffect
    {
        [Tooltip("체크하면 디버프를 전부, 끄면 가장 최근에 걸린 1개만 지운다")]
        [Label("전부 제거")]
        [SerializeField] private bool removeAll = true;

        // 대상마다 디버프를 지우고 로그를 남긴다. 쓰러진 대상은 건너뛴다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                if (target.IsDown)
                {
                    continue;
                }

                List<StatusInstance> removed = target.Statuses.RemoveDebuffs(removeAll);
                if (removed.Count == 0)
                {
                    continue;
                }

                context.Log($"  {target.Name}: 디버프 {removed.Count}개 제거");
                StatusApplier.ReportRemoved(target, removed, context.Services);
            }
        }
    }
}
