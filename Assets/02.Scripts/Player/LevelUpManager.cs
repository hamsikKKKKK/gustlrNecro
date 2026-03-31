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
        public static Action OnJobSelect;
        public static Action<int> OnExpGained;

        // 외형 변경용 이벤트
        public static Action<JobType, bool> OnClassChanged;

        private static int pendingLevelUps;

        // 직업 / 전직 상태
        private static JobType currentJob = JobType.None;
        private static bool isAdvanced = false;

        private static List<StatChoice> selectionHistory = new List<StatChoice>();

        private static Dictionary<JobType, StatChoice> jobStatMap = new Dictionary<JobType, StatChoice>
        {
            [JobType.Warrior] = StatChoice.AttackDefenseUp,
            [JobType.Mage] = StatChoice.MagicCooldownUp,
            [JobType.Archer] = StatChoice.AttackSpeedRangeUp
        };

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
            else if (currentLevel == 10 && currentJob == JobType.None)
                return 0f; // 10레벨에서 직업 선택 전까지 성장 정지
            else if (currentLevel <= 20)
                return 1.0f;
            else
                return 0.8f;
        }

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

                if (currentLevel == 10 && currentJob == JobType.None)
                    OnJobSelect?.Invoke();
                else
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

                if (currentLevel == 10 && currentJob == JobType.None)
                    OnJobSelect?.Invoke();
                else
                    OnLevelUp?.Invoke();
            }
        }

        private static void CalculateExpRequired()
        {
            expRequired = Mathf.RoundToInt(BASE_EXP * Mathf.Pow(EXP_MULTIPLIER, currentLevel - 2));
        }

        public static void DebugLevelUp()
        {
            if (currentLevel >= MAX_LEVEL) return;

            currentLevel++;
            CalculateExpRequired();

            if (currentLevel == 10 && currentJob == JobType.None)
                OnJobSelect?.Invoke();
            else
                OnLevelUp?.Invoke();
        }

        public static int GetCurrentLevel() => currentLevel;
        public static int GetCurrentExp() => currentExp;
        public static int GetExpRequired() => expRequired;
        public static float GetExpProgress() => expRequired > 0 ? (float)currentExp / expRequired : 0f;

        public static bool SetJob(JobType newJob)
        {
            if (currentJob != JobType.None)
            {
                Debug.LogWarning("[LevelUpManager] 이미 직업이 선택되었습니다.");
                return false;
            }

            if (newJob == JobType.None)
            {
                Debug.LogWarning("[LevelUpManager] 유효하지 않은 직업입니다.");
                return false;
            }

            currentJob = newJob;
            isAdvanced = true; // 10레벨 선택 = 즉시 전직

            Debug.Log($"[LevelUpManager] 직업 선택 완료 | 직업={currentJob} | 전직={isAdvanced}");
            OnClassChanged?.Invoke(currentJob, isAdvanced);

            return true;
        }

        public static bool Promote()
        {
            if (currentJob == JobType.None)
            {
                Debug.LogWarning("[LevelUpManager] 직업이 없어 전직할 수 없습니다.");
                return false;
            }

            if (isAdvanced)
            {
                Debug.LogWarning("[LevelUpManager] 이미 전직된 상태입니다.");
                return false;
            }

            isAdvanced = true;
            OnClassChanged?.Invoke(currentJob, isAdvanced);
            return true;
        }

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
        public static void ResetSelectionHistory() => selectionHistory.Clear();

        public static JobType GetCurrentJob() => currentJob;
        public static bool IsAdvanced() => isAdvanced;

        public static void ResetJob()
        {
            currentJob = JobType.None;
            isAdvanced = false;
            OnClassChanged?.Invoke(currentJob, isAdvanced);
        }
    }
}