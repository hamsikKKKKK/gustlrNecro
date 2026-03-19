using System.Collections.Generic;

namespace Necrocis
{
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
        public Dictionary<CharacterStatType, float> flatStats = new Dictionary<CharacterStatType, float>();
        public Dictionary<CharacterStatType, float> percentStats = new Dictionary<CharacterStatType, float>();
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
                    flatStats = new Dictionary<CharacterStatType, float>
                    {
                        [CharacterStatType.MaxHealth] = 10
                    }
                },
                [StatChoice.SpeedUp] = new StatEffect
                {
                    percentStats = new Dictionary<CharacterStatType, float>
                    {
                        [CharacterStatType.MoveSpeed] = 3
                    }
                },
                [StatChoice.AttackDefenseUp] = new StatEffect
                {
                    flatStats = new Dictionary<CharacterStatType, float>
                    {
                        [CharacterStatType.AttackPower] = 3,
                        [CharacterStatType.Defense] = 1
                    }
                },
                [StatChoice.AttackSpeedRangeUp] = new StatEffect
                {
                    percentStats = new Dictionary<CharacterStatType, float>
                    {
                        [CharacterStatType.AttackSpeed] = 5,
                        [CharacterStatType.Range] = 5
                    }
                },
                [StatChoice.MagicCooldownUp] = new StatEffect
                {
                    flatStats = new Dictionary<CharacterStatType, float>
                    {
                        [CharacterStatType.Magic] = 3
                    },
                    percentStats = new Dictionary<CharacterStatType, float>
                    {
                        [CharacterStatType.Cooldown] = -3
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
