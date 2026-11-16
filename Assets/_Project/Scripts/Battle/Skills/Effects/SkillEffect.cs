using System;
using System.Reflection;
using Game.Core;
using Game.Data;

namespace Game.Battle.Effects
{
    // 모든 효과 블록의 공통 부모 (전략 패턴).
    // 스킬은 효과 블록 목록을 순서대로 Apply만 호출하고, 실제로 무엇을 하는지는 각 하위 클래스가 정한다.
    // 새 효과가 필요하면 이 클래스를 상속한 클래스만 추가하면 되고, 스킬 실행 코드는 바꾸지 않아도 된다.
    // [Serializable]: [SerializeReference] 리스트에 담겨 에셋에 저장될 수 있게 한다.
    [Serializable]
    public abstract class SkillEffect
    {
        // 효과를 실행한다. 대상 목록, 시전자, 로그 함수는 context로 전달받는다.
        public abstract void Apply(SkillContext context);

        // 상세 창 스킬 탭에 보여줄 이 블록의 한 줄 설명 (표시 전용). 수치를 블록 설정에서 읽으므로 수치를 바꿔도 글을 고칠 필요가 없다.
        // skillTarget: 이 블록이 든 스킬의 대상 규칙 (피해 블록이 확산일 때 양옆 배율을 함께 보여주는 데 쓴다).
        // virtual: 기본은 null이고, 하위 클래스가 override로 자기 설명을 돌려준다.
        public virtual string Describe(SkillTargetType skillTarget) => null;

        // 설명 글을 돌려준다. 하위 클래스가 설명을 만들지 않았으면(null) 드롭다운 이름([SubclassMenu]의 마지막 부분)으로 대신한다.
        // ?? : 왼쪽이 null이면 오른쪽 값을 쓴다. GetCustomAttribute: 클래스에 붙은 [SubclassMenu]를 꺼낸다 (없으면 null).
        public string DescribeOrName(SkillTargetType skillTarget)
        {
            return Describe(skillTarget)
                ?? GetType().GetCustomAttribute<SubclassMenuAttribute>()?.DisplayName
                ?? GetType().Name;
        }
    }
}
