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
        // 결과에는 "새로 들어갔는지(갱신이 아닌지)"와 밀려난 효과가 담긴다 (새 버프 획득 패시브·로그용).
        public StatusAddResult Add(StatusEffectData data, BattleUnit source)
        {
            List<StatusInstance> slots = GetSlots(data.Category);

            // 스택 방식(화상·독): 이미 걸려 있으면 칸을 더 쓰지 않고 그 칸의 스택만 +1, 남은 턴은 처음 값으로.
            if (data.UsesStackCount)
            {
                StatusInstance stacked = FindByData(slots, data);
                if (stacked != null)
                {
                    stacked.AddStack(source, nextAppliedOrder++);
                    return new StatusAddResult(stacked, isNew: false, pushedOut: null);
                }
            }

            if (IsRefreshOnly(data))
            {
                StatusInstance existing = FindByData(slots, data);
                if (existing != null)
                {
                    existing.Refresh(nextAppliedOrder++);
                    return new StatusAddResult(existing, isNew: false, pushedOut: null);
                }
            }

            StatusInstance pushedOut = MakeRoom(slots);

            // nextAppliedOrder++: 현재 값을 넘겨준 뒤 1 증가시킨다 (다음 효과는 더 큰 순번을 받는다).
            var added = new StatusInstance(data, source, nextAppliedOrder++);
            slots.Add(added);
            return new StatusAddResult(added, isNew: true, pushedOut);
        }

        // 다른 유닛에게서 빼앗아 온 효과를 남은 턴 그대로 받는다 (버프 강탈, 기획서 4-3).
        // 같은 버프를 이미 가지고 있으면 남은 턴이 긴 쪽으로 맞추고 하나만 남긴다 (갱신이므로 isNew = false).
        public StatusAddResult Receive(StatusInstance incoming)
        {
            List<StatusInstance> slots = GetSlots(incoming.Data.Category);

            if (IsRefreshOnly(incoming.Data))
            {
                StatusInstance existing = FindByData(slots, incoming.Data);
                if (existing != null)
                {
                    existing.KeepLongerRemaining(incoming.RemainingTurns, nextAppliedOrder++);
                    return new StatusAddResult(existing, isNew: false, pushedOut: null);
                }
            }

            StatusInstance pushedOut = MakeRoom(slots);
            incoming.ResetAppliedOrder(nextAppliedOrder++);
            slots.Add(incoming);
            return new StatusAddResult(incoming, isNew: true, pushedOut);
        }

        // 버프를 전부 비우고, 비운 버프 목록을 돌려준다 (강탈 딜러 3스의 버프 소모, 강탈 대상).
        public List<StatusInstance> RemoveAllBuffs()
        {
            var removed = new List<StatusInstance>(buffs);
            buffs.Clear();
            return removed;
        }

        // 버프·디버프를 모두 비운다 (부활: 쓰러질 때 걸려 있던 효과는 모두 사라진 것으로 본다, 기획서 3-9).
        // "지워짐" 알림을 보내지 않는다 → 사망으로 사라진 효과처럼 지연 버프 변환·곰인형 패시브가 반응하지 않는다.
        public void Clear()
        {
            buffs.Clear();
            debuffs.Clear();
        }

        // 디버프를 하나(가장 최근에 걸린 것) 또는 전부 칸에서 빼고, 뺀 목록을 돌려준다 (디버프 제거 블록).
        // 가장 최근 것: 디버프 목록은 걸린 순서대로 쌓이므로 맨 끝이 가장 최근이다.
        public List<StatusInstance> RemoveDebuffs(bool removeAll)
        {
            var removed = new List<StatusInstance>();
            if (debuffs.Count == 0)
            {
                return removed;
            }

            if (removeAll)
            {
                removed.AddRange(debuffs);
                debuffs.Clear();
            }
            else
            {
                StatusInstance latest = debuffs[debuffs.Count - 1];
                debuffs.RemoveAt(debuffs.Count - 1);
                removed.Add(latest);
            }

            return removed;
        }

        // 특정 효과 하나를 칸에서 뺀다 (반격 태세처럼 쓰고 나면 사라지는 효과). 실제로 있었으면 true.
        public bool RemoveInstance(StatusInstance instance)
        {
            return GetSlots(instance.Data.Category).Remove(instance);
        }

        // 지금 걸린 보호막 버프를 찾는다. 보호막은 유닛당 하나로 합쳐지므로 많아야 1개다. 없으면 null.
        public StatusInstance FindShield()
        {
            foreach (StatusInstance buff in buffs)
            {
                if (buff.HasShield)
                {
                    return buff;
                }
            }

            return null;
        }

        // 보호막을 더할 때 쓸 부여 순번을 하나 받는다 (ExtendRemaining에 넘김).
        public static long TakeAppliedOrder()
        {
            return nextAppliedOrder++;
        }

        // 같은 정의(같은 에셋)의 효과를 찾는다. 없으면 null.
        public StatusInstance Find(StatusEffectData data)
        {
            return FindByData(GetSlots(data.Category), data);
        }

        // 같은 효과를 다시 걸면 갱신만 하는지.
        // 같은 버프는 중첩되지 않는다 (기획서 3-7): 버프는 데이터 설정과 관계없이 항상 갱신형으로 처리한다.
        // 디버프만 데이터의 중첩 방식(중첩 / 갱신)을 따른다.
        private static bool IsRefreshOnly(StatusEffectData data)
        {
            return data.Category == StatusCategory.Buff || data.StackMode == StatusStackMode.Refresh;
        }

        // 칸이 가득 찼으면 가장 빨리 끝날 효과를 밀어내 자리를 만들고, 밀려난 효과를 돌려준다 (없으면 null).
        private static StatusInstance MakeRoom(List<StatusInstance> slots)
        {
            if (slots.Count < SlotsPerCategory)
            {
                return null;
            }

            StatusInstance pushedOut = FindPushOutTarget(slots);
            slots.Remove(pushedOut);
            return pushedOut;
        }

        // 버프·디버프를 모두 합친 목록 (지속피해 처리, 화면 표시용). 디버프가 앞에 온다.
        public List<StatusInstance> GetAll()
        {
            var all = new List<StatusInstance>(debuffs);
            all.AddRange(buffs);
            return all;
        }

        // 자기 턴이 끝날 때 모든 효과의 남은 턴을 1 줄이고, 끝난 효과를 제거해 목록으로 돌려준다.
        // 돌려준 목록으로 "효과가 지워짐" 알림(지연 버프 변환, 곰인형 버퍼 패시브)을 보낸다.
        public List<StatusInstance> DecreaseTurnsAndRemoveExpired()
        {
            var expired = new List<StatusInstance>();
            DecreaseAndRemove(buffs, expired);
            DecreaseAndRemove(debuffs, expired);
            return expired;
        }

        // 피해를 보호막 버프로 먼저 흡수하고, 남은 피해를 돌려준다. 다 깨진 보호막은 칸에서 빼고 broken에 담는다.
        // 보호막은 유닛당 하나로 합쳐지므로(StatusApplier.GrantShield) 보통 1개만 깎인다.
        public int AbsorbWithShields(int damage, List<StatusInstance> broken)
        {
            foreach (StatusInstance buff in buffs)
            {
                if (damage <= 0)
                {
                    break;
                }

                if (!buff.HasShield)
                {
                    continue;
                }

                damage = buff.Absorb(damage);
                if (!buff.HasShield)
                {
                    broken.Add(buff);
                }
            }

            // 순회가 끝난 뒤에 지운다 (foreach 도중 목록을 바꾸면 오류가 난다).
            foreach (StatusInstance shield in broken)
            {
                buffs.Remove(shield);
            }

            return damage;
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

        // 목록의 효과를 1턴씩 줄이고 만료된 것을 지워 expired에 모은다.
        private static void DecreaseAndRemove(List<StatusInstance> slots, List<StatusInstance> expired)
        {
            foreach (StatusInstance instance in slots)
            {
                instance.DecreaseTurn();

                if (instance.IsExpired)
                {
                    expired.Add(instance);
                }
            }

            // RemoveAll: 조건(람다식)이 true인 원소를 한 번에 모두 지운다. 여기서는 남은 턴이 0 이하인 효과.
            slots.RemoveAll(instance => instance.IsExpired);
        }
    }
}
