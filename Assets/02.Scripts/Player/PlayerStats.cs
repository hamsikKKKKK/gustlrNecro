using UnityEngine;
using System.Collections.Generic;

namespace Necrocis
{
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }

            InitializeStats();
        }

        // 기본 스탯 (불변)
        private const float BASE_HEALTH = 100f;
        private const float BASE_ATTACK = 30f;
        private const float BASE_DEFENSE = 5f;
        private const float BASE_SPEED = 5f;
        private const float BASE_ATTACK_SPEED = 1f;
        private const float BASE_RANGE = 1f;
        private const float BASE_MAGIC = 20f;

        private Dictionary<StatType, float> flatBonus = new Dictionary<StatType, float>();
        private Dictionary<StatType, float> percentBonus = new Dictionary<StatType, float>();

        private void InitializeStats()
        {
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                flatBonus[statType] = 0f;
                percentBonus[statType] = 0f;
            }
        }

        public void ApplyStatChoice(StatChoice choice)
        {
            StatEffect effect = StatManager.GetStatEffect(choice);

            foreach (var stat in effect.flatStats)
                flatBonus[stat.Key] += stat.Value;

            foreach (var stat in effect.percentStats)
                percentBonus[stat.Key] += stat.Value;
        }

        public float GetHealth()    => CalculateFinalStat(BASE_HEALTH, StatType.Health);
        public float GetAttack()    => CalculateFinalStat(BASE_ATTACK, StatType.Attack);
        public float GetDefense()   => CalculateFinalStat(BASE_DEFENSE, StatType.Defense);
        public float GetSpeed()     => CalculateFinalStat(BASE_SPEED, StatType.Speed);
        public float GetAttackSpeed() => CalculateFinalStat(BASE_ATTACK_SPEED, StatType.AttackSpeed);
        public float GetRange()     => CalculateFinalStat(BASE_RANGE, StatType.Range);
        public float GetMagic()     => CalculateFinalStat(BASE_MAGIC, StatType.Magic);

        private float CalculateFinalStat(float baseStat, StatType statType)
        {
            float withFlat = baseStat + flatBonus[statType];
            float percentMultiplier = 1f + (percentBonus[statType] / 100f);
            return withFlat * percentMultiplier;
        }

        public void ResetStats()
        {
            foreach (StatType statType in System.Enum.GetValues(typeof(StatType)))
            {
                flatBonus[statType] = 0f;
                percentBonus[statType] = 0f;
            }
        }

        public float GetFlatBonus(StatType statType) => flatBonus[statType];
        public float GetPercentBonus(StatType statType) => percentBonus[statType];
    }
}
