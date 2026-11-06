using System;

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
    }
}
