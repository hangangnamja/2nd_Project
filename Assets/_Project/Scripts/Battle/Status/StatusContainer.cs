using System.Collections.Generic;
using Game.Data;

namespace Game.Battle.Status
{
    // 유닛 한 명의 상태효과 칸 (버프 10칸 + 디버프 10칸).
    // 효과 추가, 가득 찼을 때 밀어내기, 턴 감소·만료 제거만 담당하고 피해 계산은 하지 않는다.
    public class StatusContainer
    {
        public const int SlotsPerCategory = 10;

        // 전투 전체에서 공유하는 부여 순번. 어떤 효과가 먼저 걸렸는지 비교하는 데 쓴다.
        // static: 모든 StatusContainer가 같은 값을 공유한다.
        private static long nextAppliedOrder;

        private readonly List<StatusInstance> buffs = new List<StatusInstance>();
        private readonly List<StatusInstance> debuffs = new List<StatusInstance>();

        public IReadOnlyList<StatusInstance> Buffs => buffs;
        public IReadOnlyList<StatusInstance> Debuffs => debuffs;

        // 상태효과를 하나 추가한다.
        // 갱신형이면 같은 효과의 남은 턴만 되돌리고, 칸이 가득 찼으면 가장 빨리 끝날 효과를 밀어낸다.
        // 밀려난 효과가 있으면 돌려준다 (로그용, 없으면 null).
        public StatusInstance Add(StatusEffectData data, BattleUnit source)
        {
            List<StatusInstance> slots = GetSlots(data.Category);

            // 같은 버프는 중첩되지 않는다 (기획서 3-7): 버프는 데이터 설정과 관계없이 항상 갱신형으로 처리한다.
            // 디버프만 데이터의 중첩 방식(중첩 / 갱신)을 따른다.
            bool refreshOnly = data.Category == StatusCategory.Buff || data.StackMode == StatusStackMode.Refresh;

            if (refreshOnly)
            {
                StatusInstance existing = FindByData(slots, data);
                if (existing != null)
                {
                    existing.Refresh(nextAppliedOrder++);
                    return null;
                }
            }

            StatusInstance pushedOut = null;
            if (slots.Count >= SlotsPerCategory)
            {
                pushedOut = FindPushOutTarget(slots);
                slots.Remove(pushedOut);
            }

            // nextAppliedOrder++: 현재 값을 넘겨준 뒤 1 증가시킨다 (다음 효과는 더 큰 순번을 받는다).
            slots.Add(new StatusInstance(data, source, nextAppliedOrder++));
            return pushedOut;
        }

        // 버프·디버프를 모두 합친 목록 (지속피해 처리, 화면 표시용). 디버프가 앞에 온다.
        public List<StatusInstance> GetAll()
        {
            var all = new List<StatusInstance>(debuffs);
            all.AddRange(buffs);
            return all;
        }

        // 자기 턴이 끝날 때 모든 효과의 남은 턴을 1 줄이고, 끝난 효과를 제거한다.
        public void DecreaseTurnsAndRemoveExpired()
        {
            DecreaseAndRemove(buffs);
            DecreaseAndRemove(debuffs);
        }

        // 분류에 맞는 칸 목록을 고른다.
        private List<StatusInstance> GetSlots(StatusCategory category)
        {
            return category == StatusCategory.Buff ? buffs : debuffs;
        }

        // 칸이 가득 찼을 때 밀어낼 효과를 고른다 (기획서 3-7).
        // 남은 턴이 가장 짧은 것, 남은 턴이 같으면 먼저 걸린(순번이 작은) 것.
        private static StatusInstance FindPushOutTarget(List<StatusInstance> slots)
        {
            StatusInstance target = slots[0];

            foreach (StatusInstance instance in slots)
            {
                bool endsSooner = instance.RemainingTurns < target.RemainingTurns;
                bool sameTurnsButOlder = instance.RemainingTurns == target.RemainingTurns
                    && instance.AppliedOrder < target.AppliedOrder;

                if (endsSooner || sameTurnsButOlder)
                {
                    target = instance;
                }
            }

            return target;
        }

        // 같은 정의(같은 에셋)의 효과가 이미 걸려 있으면 찾아 돌려준다.
        private static StatusInstance FindByData(List<StatusInstance> slots, StatusEffectData data)
        {
            foreach (StatusInstance instance in slots)
            {
                if (instance.Data == data)
                {
                    return instance;
                }
            }

            return null;
        }

        // 목록의 효과를 1턴씩 줄이고 만료된 것을 지운다.
        private static void DecreaseAndRemove(List<StatusInstance> slots)
        {
            foreach (StatusInstance instance in slots)
            {
                instance.DecreaseTurn();
            }

            // RemoveAll: 조건(람다식)이 true인 원소를 한 번에 모두 지운다. 여기서는 남은 턴이 0 이하인 효과.
            slots.RemoveAll(instance => instance.IsExpired);
        }
    }
}
