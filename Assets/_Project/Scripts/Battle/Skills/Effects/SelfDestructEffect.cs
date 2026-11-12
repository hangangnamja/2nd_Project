using System;
using Game.Core;

namespace Game.Battle.Effects
{
    // 시전자가 전장에서 사라진다 (부하 자폭). 처치가 아니므로 처치 반응(강탈 딜러 추격 등)이 발동하지 않는다.
    // 같은 스킬의 피해·기절 블록이 먼저 실행되도록 효과 목록의 맨 끝에 둔다.
    // 효과 블록 드롭다운에 보여줄 분류/이름과 설명 (SubclassMenuAttribute).
    [SubclassMenu("변신·전장/자폭 (사라짐)", "시전자가 전장에서 사라진다. 처치로 치지 않는다. 피해·기절 블록 뒤에 둔다", 550)]
    [Serializable]
    public class SelfDestructEffect : SkillEffect
    {
        // 시전자를 퇴장시키고 로그를 남긴다. 이미 쓰러졌으면 아무것도 하지 않는다.
        public override void Apply(SkillContext context)
        {
            if (context.Caster.IsDead)
            {
                return;
            }

            context.Caster.Vanish();
            context.Log($"  {context.Caster.Name} 자폭으로 사라짐");
        }
    }
}
