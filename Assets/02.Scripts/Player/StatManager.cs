using System.Collections.Generic;

namespace Necrocis
{
    public enum StatType
    {
        Health,
        Speed,
        Attack,
        Defense,
        AttackSpeed,
        Range,
        Magic,
        Cooldown
    }

    public enum StatChoice
    {
        HealthUp,
        SpeedUp,
        AttackDefenseUp,
        AttackSpeedRangeUp,
        MagicCooldownUp
    }

    public class StatEffect
    {
        public Dictionary<StatType, float> flatStats = new Dictionary<StatType, float>();
        public Dictionary<StatType, float> percentStats = new Dictionary<StatType, float>();
    }

    public static class StatManager
    {
        private static Dictionary<StatChoice, StatEffect> statEffects;

        static StatManager()
        {
            InitializeStatData();
        }

        private static void InitializeStatData()
        {
            statEffects = new Dictionary<StatChoice, StatEffect>
            {
                [StatChoice.HealthUp] = new StatEffect
                {
                    flatStats = new Dictionary<StatType, float>
                    {
                        [StatType.Health] = 10
                    }
                },
                [StatChoice.SpeedUp] = new StatEffect
                {
                    percentStats = new Dictionary<StatType, float>
                    {
                        [StatType.Speed] = 3
                    }
                },
                [StatChoice.AttackDefenseUp] = new StatEffect
                {
                    flatStats = new Dictionary<StatType, float>
                    {
                        [StatType.Attack] = 3,
                        [StatType.Defense] = 1
                    }
                },
                [StatChoice.AttackSpeedRangeUp] = new StatEffect
                {
                    percentStats = new Dictionary<StatType, float>
                    {
                        [StatType.AttackSpeed] = 5,
                        [StatType.Range] = 5
                    }
                },
                [StatChoice.MagicCooldownUp] = new StatEffect
                {
                    flatStats = new Dictionary<StatType, float>
                    {
                        [StatType.Magic] = 3
                    },
                    percentStats = new Dictionary<StatType, float>
                    {
                        [StatType.Cooldown] = -3
                    }
                }
            };
        }

        public static StatEffect GetStatEffect(StatChoice choice)
        {
            return statEffects[choice];
        }
    }
}
