using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Necrocis
{
    public enum JobType
    {
        None,
        Warrior,
        Mage,
        Archer
    }

    public static class LevelUpManager
    {
        private static int currentLevel = 1;
        private static int currentExp = 0;
        private static int expRequired = 100;

        private const int MAX_LEVEL = 30;
        private const int BASE_EXP = 100;
        private const float EXP_MULTIPLIER = 1.25f;

        public static Action OnLevelUp;
        public static Action<int> OnExpGained;

        public static void AddExp(int baseAmount)
        {
            if (currentLevel >= MAX_LEVEL) return;

            float multiplier = GetExpMultiplier();
            int actualExp = Mathf.RoundToInt(baseAmount * multiplier);

            currentExp += actualExp;
            OnExpGained?.Invoke(actualExp);

            CheckLevelUp();
        }

        private static float GetExpMultiplier()
        {
            if (currentLevel <= 9)
                return 2.0f;
            else if (currentLevel == 10)
                return 0f;
            else if (currentLevel <= 20)
                return 1.0f;
            else
                return 0.8f;
        }

        private static int pendingLevelUps;

        private static void CheckLevelUp()
        {
            while (currentExp >= expRequired && currentLevel < MAX_LEVEL)
            {
                currentExp -= expRequired;
                currentLevel++;
                CalculateExpRequired();
                pendingLevelUps++;
            }

            if (pendingLevelUps > 0)
            {
                pendingLevelUps--;
                OnLevelUp?.Invoke();
            }
        }

        public static bool HasPendingLevelUp()
        {
            return pendingLevelUps > 0;
        }

        public static void ProcessNextPendingLevelUp()
        {
            if (pendingLevelUps > 0)
            {
                pendingLevelUps--;
                OnLevelUp?.Invoke();
            }
        }

        private static void CalculateExpRequired()
        {
            expRequired = Mathf.RoundToInt(BASE_EXP * Mathf.Pow(EXP_MULTIPLIER, currentLevel - 2));
        }

        public static int GetCurrentLevel() => currentLevel;
        public static int GetCurrentExp() => currentExp;
        public static int GetExpRequired() => expRequired;
        public static float GetExpProgress() => (float)currentExp / expRequired;

        // 직업 시스템
        private static JobType currentJob = JobType.None;
        private static List<StatChoice> selectionHistory = new List<StatChoice>();

        private static Dictionary<JobType, StatChoice> jobStatMap = new Dictionary<JobType, StatChoice>
        {
            [JobType.Warrior] = StatChoice.AttackDefenseUp,
            [JobType.Mage] = StatChoice.MagicCooldownUp,
            [JobType.Archer] = StatChoice.AttackSpeedRangeUp
        };

        public static List<StatChoice> GetRandomChoices()
        {
            if (currentLevel >= 11 && currentJob != JobType.None)
                return GetJobBasedChoices();

            return GetRandomFourChoices();
        }

        private static List<StatChoice> GetRandomFourChoices()
        {
            List<StatChoice> allChoices = Enum.GetValues(typeof(StatChoice))
                                               .Cast<StatChoice>()
                                               .ToList();
            Shuffle(allChoices);
            return allChoices.Take(4).ToList();
        }

        private static List<StatChoice> GetJobBasedChoices()
        {
            List<StatChoice> result = new List<StatChoice>();

            StatChoice jobStat = jobStatMap[currentJob];
            result.Add(jobStat);

            StatChoice mostSelected = GetMostSelectedStat(exclude: jobStat);
            result.Add(mostSelected);

            List<StatChoice> remaining = GetRemainingChoices(result);
            Shuffle(remaining);
            result.Add(remaining[0]);

            remaining = GetRemainingChoices(result);
            Shuffle(remaining);
            result.Add(remaining[0]);

            return result;
        }

        private static StatChoice GetMostSelectedStat(StatChoice exclude)
        {
            Dictionary<StatChoice, int> counts = new Dictionary<StatChoice, int>();

            foreach (StatChoice choice in selectionHistory)
            {
                if (choice == exclude) continue;
                if (!counts.ContainsKey(choice))
                    counts[choice] = 0;
                counts[choice]++;
            }

            int maxCount = 0;
            List<StatChoice> mostSelectedList = new List<StatChoice>();

            foreach (var kvp in counts)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    mostSelectedList.Clear();
                    mostSelectedList.Add(kvp.Key);
                }
                else if (kvp.Value == maxCount)
                {
                    mostSelectedList.Add(kvp.Key);
                }
            }

            if (mostSelectedList.Count > 0)
                return mostSelectedList[UnityEngine.Random.Range(0, mostSelectedList.Count)];

            List<StatChoice> allChoices = Enum.GetValues(typeof(StatChoice))
                                               .Cast<StatChoice>()
                                               .Where(c => c != exclude)
                                               .ToList();
            return allChoices[UnityEngine.Random.Range(0, allChoices.Count)];
        }

        private static List<StatChoice> GetRemainingChoices(List<StatChoice> alreadySelected)
        {
            return Enum.GetValues(typeof(StatChoice))
                        .Cast<StatChoice>()
                        .Where(c => !alreadySelected.Contains(c))
                        .ToList();
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = temp;
            }
        }

        public static void RecordSelection(StatChoice choice) => selectionHistory.Add(choice);
        public static void SetJob(JobType job) => currentJob = job;
        public static void ResetSelectionHistory() => selectionHistory.Clear();
        public static JobType GetCurrentJob() => currentJob;
    }
}
