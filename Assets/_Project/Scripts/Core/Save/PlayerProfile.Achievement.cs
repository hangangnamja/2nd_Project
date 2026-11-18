using System;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Core
{
    // 업적 (기획서 업적): 누적 기록을 쌓고, 업적마다 진행도·달성·보상 수령을 판정한다.
    //  - 누적 기록(적 처치·모집 횟수 등)은 세이브의 counters에 쌓는다
    //  - 첫 클리어·보유 전투원·최고 레벨은 따로 기록하지 않고 지금 세이브 상태로 계산한다 → 업적이 생기기 전 진행도도 반영된다
    //  - 저장하는 것은 "보상을 받았는지"뿐. 달성 여부는 매번 진행도 ≥ 목표로 판정한다
    public static partial class PlayerProfile
    {
        // 업적 보상을 받아 상태가 바뀐 직후 알린다 (업적 화면·메인 화면 알림 점 갱신용).
        public static event Action AchievementsChanged;

        // 업적 진행도: 그 조건의 지금 값 (목표를 넘어도 그대로 돌려준다. 화면이 목표로 자른다).
        public static int GetAchievementProgress(AchievementData achievement)
        {
            EnsureLoaded();
            switch (achievement.Condition)
            {
                case AchievementCondition.StageFirstClear:
                    return achievement.Stage != null && IsStageCleared(achievement.Stage) ? 1 : 0;
                case AchievementCondition.OwnedCharacters:
                    return data.characters.Count;
                case AchievementCondition.MaxCharacterLevel:
                    return GetHighestCharacterLevel();
                default:
                    return GetCounter(achievement.Condition);
            }
        }

        // 진행도가 목표에 닿았는지 (보상을 받았든 아니든).
        public static bool IsAchievementCompleted(AchievementData achievement)
        {
            return GetAchievementProgress(achievement) >= achievement.Target;
        }

        // 보상을 이미 받았는지 (세이브에 ID가 있으면 받은 것).
        public static bool IsAchievementClaimed(AchievementData achievement)
        {
            EnsureLoaded();
            return data.claimedAchievements.Contains(achievement.Id);
        }

        // 달성했지만 아직 보상을 받지 않은 업적인지 (수령 버튼·알림 점).
        public static bool CanClaimAchievement(AchievementData achievement)
        {
            return IsAchievementCompleted(achievement) && !IsAchievementClaimed(achievement);
        }

        // 업적 하나의 보상을 받는다: 재료(보석)를 더하고 받은 기록을 남긴 뒤 저장. 받을 수 없으면 false.
        public static bool ClaimAchievement(AchievementData achievement)
        {
            if (!GrantAchievementReward(achievement))
            {
                return false;
            }

            Save();
            NotifyAchievementClaimed();
            return true;
        }

        // 여러 업적의 보상을 한꺼번에 받는다 ("모두 수령"). 받은 업적 수를 돌려주고, 하나라도 받았으면 한 번만 저장한다.
        public static int ClaimAchievements(IEnumerable<AchievementData> achievements)
        {
            int claimed = 0;
            foreach (AchievementData achievement in achievements)
            {
                if (GrantAchievementReward(achievement))
                {
                    claimed++;
                }
            }

            if (claimed > 0)
            {
                Save();
                NotifyAchievementClaimed();
            }

            return claimed;
        }

        // 지금 받을 수 있는 업적 보상 수 (메인 화면 업적 버튼의 알림 점).
        public static int CountClaimableAchievements()
        {
            AchievementCatalog catalog = Database != null ? Database.AchievementCatalog : null;
            if (catalog == null)
            {
                return 0;
            }

            int count = 0;
            foreach (AchievementData achievement in catalog.Achievements)
            {
                if (achievement != null && CanClaimAchievement(achievement))
                {
                    count++;
                }
            }

            return count;
        }

        // 로비에서 시작한 전투가 끝났을 때 전투 기록(아군이 처치한 적·치명타·스킬 사용)을 누적 기록에 더하고 저장한다.
        // 이긴 전투만이 아니라 진 전투의 기록도 더한다 (일시정지로 나간 전투는 끝나지 않았으므로 더하지 않음).
        public static void RecordBattle(int kills, int criticalHits, int skillsUsed)
        {
            EnsureLoaded();
            AddCounter(AchievementCondition.EnemiesKilled, kills);
            AddCounter(AchievementCondition.CriticalHits, criticalHits);
            AddCounter(AchievementCondition.SkillsUsed, skillsUsed);
            Save();
        }

        // ---------- 내부 처리 ----------

        // 누적 기록을 amount만큼 늘린다. 저장은 부른 쪽이 한다 (스테이지 클리어·모집처럼 다른 값과 한 번에 저장하기 위해).
        // 기록 이름은 조건 enum의 이름(예: "EnemiesKilled")이라, enum 순서가 바뀌어도 기록이 섞이지 않는다.
        private static void AddCounter(AchievementCondition counter, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            string id = counter.ToString();
            CounterSave saved = data.counters.Find(entry => entry.id == id);
            if (saved == null)
            {
                saved = new CounterSave { id = id };
                data.counters.Add(saved);
            }

            // 아주 오래 쌓여도 int 최댓값을 넘어 음수가 되지 않게 한다 (long으로 더한 뒤 int 범위로 자름).
            saved.count = (int)Math.Min(int.MaxValue, (long)saved.count + amount);
        }

        private static int GetCounter(AchievementCondition counter)
        {
            string id = counter.ToString();
            CounterSave saved = data.counters.Find(entry => entry.id == id);
            return saved != null ? saved.count : 0;
        }

        // 클리어한 스테이지의 종류(기본·토벌·탐색)별 누적 클리어 횟수를 1 늘린다. 저장은 CompleteStage가 한다.
        private static void AddStageClearCounter(StageData stage)
        {
            switch (database.GetStageKind(stage))
            {
                case StageKind.Normal:
                    AddCounter(AchievementCondition.NormalStageClears, 1);
                    break;
                case StageKind.Raid:
                    AddCounter(AchievementCondition.RaidClears, 1);
                    break;
                case StageKind.Explore:
                    AddCounter(AchievementCondition.ExploreClears, 1);
                    break;
            }
        }

        private static int GetHighestCharacterLevel()
        {
            int highest = 0;
            foreach (OwnedCharacterSave saved in data.characters)
            {
                highest = Mathf.Max(highest, saved.level);
            }

            return highest;
        }

        // 받을 수 있으면 보상을 주고 받은 기록을 남긴다. 저장·알림은 부른 쪽이 한다.
        private static bool GrantAchievementReward(AchievementData achievement)
        {
            if (achievement == null || !CanClaimAchievement(achievement))
            {
                return false;
            }

            if (achievement.RewardMaterial != null)
            {
                AddMaterialAmount(achievement.RewardMaterial, achievement.RewardAmount);
            }

            data.claimedAchievements.Add(achievement.Id);
            return true;
        }

        // 보상으로 재화가 늘었으니 인벤토리(재화 표시)와 업적 화면에 알린다. ?.Invoke: 구독한 화면이 있을 때만 호출.
        private static void NotifyAchievementClaimed()
        {
            InventoryChanged?.Invoke();
            AchievementsChanged?.Invoke();
        }
    }
}
