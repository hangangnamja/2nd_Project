using System;
using Game.Core;
using UnityEngine;
using Game.Data;

namespace Game.Battle.Effects
{
    // 쓰러진 대상을 부활시킨다 (기획서 3-9 부활 규칙). 살아 있는 대상에게는 아무 일도 하지 않는다.
    // 부활하면 HP = 최대 HP × 비율, 버프·디버프는 비워지고, 행동 게이지는 처음부터(10000) 다시 찬다. 쿨타임은 유지.
    // 같은 스킬에서 뒤에 오는 블록(무적 부여 등)은 부활한 대상에게 그대로 들어가므로, 부활 블록을 앞에 둔다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("회복·보호/부활", "쓰러진 대상을 최대 HP × 비율로 부활. 버프·디버프는 사라지고 행동 게이지는 처음부터. 살아 있는 대상은 건너뜀", 220)]
    [Serializable]
    public class ReviveEffect : SkillEffect
    {
        [Tooltip("부활할 때의 HP = 최대 HP × 이 비율 (0.5 = 50%)")]
        [Label("부활 HP 비율", Min = 0.01f, Max = 1f)]
        [SerializeField] private float hpRatio = 0.5f;

        // 대상 중 쓰러진 유닛만 부활시키고, 회복 연출(Healed 사건)로 부활한 HP를 보여준다.
        public override void Apply(SkillContext context)
        {
            foreach (BattleUnit target in context.Targets)
            {
                // Revive: 쓰러진 유닛이 아니면 false를 돌려주고 아무것도 바꾸지 않는다.
                if (!target.Revive(hpRatio))
                {
                    continue;
                }

                context.Log($"  {target.Name} 부활 (HP {target.CurrentHp}/{target.MaxHp})");

                // ?.: 연출 사건 통로가 없으면(테스트 등) 알리지 않는다.
                context.Events?.RaiseHealed(target, target.CurrentHp);
            }
        }

        // 상세 창 스킬 탭 설명.
        public override string Describe(SkillTargetType skillTarget)
        {
            return $"쓰러졌으면 최대 HP {EffectText.Percent(hpRatio)}로 부활";
        }
    }
}
