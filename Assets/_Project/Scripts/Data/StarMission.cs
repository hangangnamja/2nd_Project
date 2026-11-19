using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Data
{
    // 별 미션 조건 종류 (기획서 6장 별 미션). 에셋에는 숫자 순서로 저장되므로 새 조건은 맨 뒤에 추가한다.
    public enum StarMissionCondition
    {
        [InspectorName("클리어")] Clear,
        [InspectorName("시간 안에 클리어")] ClearWithinSeconds,
        [InspectorName("아군이 쓰러지지 않고 클리어")] NoAllyFallen,
    }

    // 전투 한 번의 결과 중 별 미션 판정에 필요한 값만 모은 것 (BattleManager가 전투가 끝날 때 만든다).
    // readonly struct: 만든 뒤 바뀌지 않는 값 묶음.
    public readonly struct StarMissionResult
    {
        public bool IsVictory { get; }

        // 전투에 걸린 시간(초). 배속·일시정지와 무관한 "1배속 기준 전투 시간" (BattleManager.BattleSeconds 참고)
        public float BattleSeconds { get; }

        // 쓰러진 아군 수 (부활해도 한 번 쓰러지면 센다)
        public int AlliesFallen { get; }

        public StarMissionResult(bool isVictory, float battleSeconds, int alliesFallen)
        {
            IsVictory = isVictory;
            BattleSeconds = battleSeconds;
            AlliesFallen = alliesFallen;
        }
    }

    // 별 미션 하나: 조건 + 기준 값 + 처음 달성했을 때 한 번 받는 보상.
    // 스테이지 에셋(StageData)마다 목록으로 들고 있어, 스테이지별로 조건·보상을 다르게 둘 수 있다.
    [Serializable]
    public class StarMission
    {
        [Label("조건")]
        [SerializeField] private StarMissionCondition condition = StarMissionCondition.Clear;

        [Tooltip("시간 안에 클리어: 제한 시간(초). 다른 조건은 쓰지 않음")]
        [Label("기준 값 (초)", Min = 0f)]
        [SerializeField] private float limitSeconds = 120f;

        [Tooltip("이 별을 처음 달성했을 때 한 번만 받는 보상")]
        [SerializeField] private List<StageReward> rewards = new List<StageReward>();

        public StarMissionCondition Condition => condition;
        public float LimitSeconds => limitSeconds;
        public IReadOnlyList<StageReward> Rewards => rewards;

        // 이번 전투 결과로 이 별을 달성했는지. 모든 별은 승리가 전제다 (지면 시간·생존과 관계없이 실패).
        // switch 식: 조건마다 판정식을 고른다.
        public bool IsAchieved(StarMissionResult result)
        {
            if (!result.IsVictory)
            {
                return false;
            }

            return condition switch
            {
                StarMissionCondition.ClearWithinSeconds => result.BattleSeconds <= limitSeconds,
                StarMissionCondition.NoAllyFallen => result.AlliesFallen == 0,
                _ => true,
            };
        }

        // 화면에 보이는 조건 문장. 기준 값으로 만들어, 시간을 바꿔도 문장을 따로 고치지 않는다.
        // 120초 → "2분 이내 클리어", 90초 → "1분 30초 이내 클리어". (int)는 소수점을 버린 정수.
        public string Describe()
        {
            return condition switch
            {
                StarMissionCondition.ClearWithinSeconds => $"{FormatDuration((int)limitSeconds)} 이내 클리어",
                StarMissionCondition.NoAllyFallen => "아군이 쓰러지지 않고 클리어",
                _ => "클리어",
            };
        }

        // 초를 "n분 n초" 꼴로. 분이나 초가 0이면 그 부분을 뺀다 (/ 60 = 몫(분), % 60 = 나머지(초)).
        private static string FormatDuration(int seconds)
        {
            int minutes = seconds / 60;
            int rest = seconds % 60;
            if (minutes == 0)
            {
                return $"{rest}초";
            }

            return rest == 0 ? $"{minutes}분" : $"{minutes}분 {rest}초";
        }
    }
}
