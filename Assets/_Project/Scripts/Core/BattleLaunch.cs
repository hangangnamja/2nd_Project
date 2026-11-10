using Game.Data;

namespace Game.Core
{
    // 로비에서 고른 전투 설정을 Battle 씬으로 넘기는 통로 (Architecture.md 1장: 로비 → "스테이지 + 편성" 전달).
    // 씬이 바뀌면 이전 씬의 오브젝트는 사라지므로, 씬과 무관한 static 값에 잠시 맡겨 두었다가 전투가 시작할 때 꺼낸다.
    public static class BattleLaunch
    {
        private static BattleSetup pending;

        // 다음 전투에서 쓸 설정을 맡긴다. 로비의 작전개시 버튼이 Battle 씬을 불러오기 직전에 호출한다.
        public static void Request(BattleSetup setup)
        {
            pending = setup;
        }

        // 맡겨 둔 설정을 꺼내고 비운다. 없으면 false → Battle 씬을 에디터에서 바로 실행한 경우라 테스트 설정을 쓴다.
        // out: 꺼낸 설정을 setup에 담아 돌려준다.
        public static bool TryTake(out BattleSetup setup)
        {
            setup = pending;
            pending = null;
            return setup != null;
        }
    }
}
