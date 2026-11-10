namespace Game.Battle
{
    // 전투 연출(이펙트·피해 숫자)을 재생하는 쪽이 구현한다. BattleManager는 화면 클래스를 몰라도 이 약속만 보고 기다린다.
    // 로직은 행동 하나를 한 번에 끝까지 계산하고, 연출은 그동안 쌓인 사건을 순서대로 재생한다 (Architecture.md 3장: 로직이 결과를 정하고 연출은 재생만).
    public interface IBattlePresentation
    {
        // 아직 재생할 연출이 남아 있으면 true. 다음 턴은 이 값이 false가 된 뒤에 시작한다.
        bool IsPlaying { get; }
    }
}
