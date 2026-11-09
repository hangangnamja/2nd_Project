using System.Collections.Generic;
using Game.Data;

namespace Game.Battle
{
    // 스테이지의 웨이브 순서만 관리한다: 지금 몇 번째 웨이브인지, 웨이브가 끝났는지, 다음 웨이브가 있는지.
    // 유닛을 만들거나 화면을 바꾸는 일은 BattleManager가 하고, 이 클래스는 "다음에 무엇을 할지" 판단만 돌려준다.
    // 그래서 웨이브 규칙(클리어 조건 등)이 바뀌어도 전투 흐름 코드는 그대로다.
    public class StageRunner
    {
        public StageData Stage { get; }

        // 현재 웨이브 번호 (0부터). 화면 표시는 +1 해서 쓴다.
        public int WaveIndex { get; private set; }
        public int WaveCount => Stage.Waves.Count;
        public StageWave CurrentWave => Stage.Waves[WaveIndex];
        public bool HasNextWave => WaveIndex < WaveCount - 1;

        public StageRunner(StageData stage)
        {
            Stage = stage;
        }

        // 다음 웨이브로 넘어간다. 마지막 웨이브면 아무것도 하지 않고 false.
        public bool MoveToNextWave()
        {
            if (!HasNextWave)
            {
                return false;
            }

            WaveIndex++;
            return true;
        }

        // 현재 웨이브가 끝났는지: 쓰러뜨려야 하는 적(무적이 아닌 적)이 모두 쓰러졌을 때.
        // 무적인 적(보스 스테이지 버프몹)은 죽일 수 없으므로 조건에서 뺀다 → 보스만 쓰러뜨리면 웨이브 클리어.
        public static bool IsWaveCleared(IReadOnlyList<BattleUnit> enemies)
        {
            foreach (BattleUnit enemy in enemies)
            {
                if (!enemy.IsInvulnerable && !enemy.IsDead)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
