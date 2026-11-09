using Game.Data;

namespace Game.Battle.Status
{
    // 상태효과를 유닛에게 넣고, 그 결과를 로그와 사건으로 알리는 공용 처리.
    // 스킬로 거는 것, 강탈로 받는 것, 패시브로 얻는 것이 모두 여기를 거쳐야
    // "새 버프 획득" 사건이 빠짐없이 한 곳에서 나간다.
    public static class StatusApplier
    {
        // target에게 상태효과를 새로 건다. 새 버프면 "버프 획득" 사건을 알린다 (갱신은 알리지 않음).
        public static StatusAddResult Apply(BattleUnit target, StatusEffectData data, BattleUnit source, BattleServices services)
        {
            StatusAddResult result = target.Statuses.Add(data, source);
            ReportResult(target, result, services);
            return result;
        }

        // 빼앗아 온 효과를 남은 턴 그대로 target에게 넣는다 (버프 강탈).
        public static StatusAddResult Receive(BattleUnit target, StatusInstance incoming, BattleServices services)
        {
            StatusAddResult result = target.Statuses.Receive(incoming);
            ReportResult(target, result, services);
            return result;
        }

        // 밀려난 효과를 로그로 남기고, 새로 들어간 버프면 사건을 알린다.
        private static void ReportResult(BattleUnit target, StatusAddResult result, BattleServices services)
        {
            if (result.PushedOut != null)
            {
                services.Log($"  {target.Name}: 칸이 가득 차 {result.PushedOut.Data.DisplayName}(남은 {result.PushedOut.RemainingTurns}턴) 밀려남");
            }

            bool isNewBuff = result.IsNew && result.Instance.Data.Category == StatusCategory.Buff;
            if (isNewBuff)
            {
                services.Events.RaiseBuffGained(target, result.Instance.Data.DisplayName);
            }
        }
    }
}
