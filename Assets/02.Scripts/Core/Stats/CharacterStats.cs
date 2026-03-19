using System;
using System.Collections.Generic;
using UnityEngine;

namespace Necrocis
{
    public enum CharacterStatType
    {
        MaxHealth,
        MoveSpeed,
        AttackPower,
        Defense,
        AttackSpeed,
        Range,
        Magic,
        Cooldown
    }

    public enum CharacterStatModifierMode
    {
        Flat,
        PercentAdd,
        PercentMultiply
    }

    [Serializable]
    public struct CharacterStatValue
    {
        public CharacterStatType statType;
        public float value;

        public CharacterStatValue(CharacterStatType statType, float value)
        {
            this.statType = statType;
            this.value = value;
        }
    }

    [Serializable]
    public struct CharacterStatModifierData
    {
        public CharacterStatType statType;
        public float value;
        public CharacterStatModifierMode mode;

        public CharacterStatModifierData(CharacterStatType statType, float value, CharacterStatModifierMode mode)
        {
            this.statType = statType;
            this.value = value;
            this.mode = mode;
        }

        public CharacterStatModifier ToModifier(object source)
        {
            return new CharacterStatModifier(statType, value, mode, source);
        }
    }

    public readonly struct CharacterStatModifier
    {
        public CharacterStatType StatType { get; }
        public float Value { get; }
        public CharacterStatModifierMode Mode { get; }
        public object Source { get; }

        public CharacterStatModifier(
            CharacterStatType statType,
            float value,
            CharacterStatModifierMode mode = CharacterStatModifierMode.Flat,
            object source = null)
        {
            StatType = statType;
            Value = value;
            Mode = mode;
            Source = source;
        }
    }

    public readonly struct CharacterStatChangedEventArgs
    {
        public CharacterStatType StatType { get; }
        public float PreviousValue { get; }
        public float CurrentValue { get; }

        public CharacterStatChangedEventArgs(CharacterStatType statType, float previousValue, float currentValue)
        {
            StatType = statType;
            PreviousValue = previousValue;
            CurrentValue = currentValue;
        }
    }

    public readonly struct CharacterHealthChangedEventArgs
    {
        public float PreviousValue { get; }
        public float CurrentValue { get; }
        public float PreviousMaxValue { get; }
        public float MaxValue { get; }
        public float NormalizedCurrent => MaxValue <= 0f ? 0f : CurrentValue / MaxValue;

        public CharacterHealthChangedEventArgs(float previousValue, float currentValue, float previousMaxValue, float maxValue)
        {
            PreviousValue = previousValue;
            CurrentValue = currentValue;
            PreviousMaxValue = previousMaxValue;
            MaxValue = maxValue;
        }
    }

    /// <summary>
    /// 공통 캐릭터 스탯 런타임 컨테이너.
    /// UI, 아이템, 레벨업 시스템은 이 컴포넌트만 구독/조작하면 된다.
    /// </summary>
    public class CharacterStats : MonoBehaviour
    {
        private readonly Dictionary<CharacterStatType, float> baseStatValues = new Dictionary<CharacterStatType, float>();
        private readonly Dictionary<CharacterStatType, float> finalStatValues = new Dictionary<CharacterStatType, float>();
        private readonly List<CharacterStatModifier> modifiers = new List<CharacterStatModifier>();

        private float currentHealth;

        public event Action<CharacterStats, CharacterStatChangedEventArgs> StatChanged;
        public event Action<CharacterStats, CharacterHealthChangedEventArgs> HealthChanged;

        public float MaxHealth => GetValue(CharacterStatType.MaxHealth);
        public float MoveSpeed => GetValue(CharacterStatType.MoveSpeed);
        public float AttackPower => GetValue(CharacterStatType.AttackPower);
        public float Defense => GetValue(CharacterStatType.Defense);
        public float AttackSpeed => GetValue(CharacterStatType.AttackSpeed);
        public float Range => GetValue(CharacterStatType.Range);
        public float Magic => GetValue(CharacterStatType.Magic);
        public float Cooldown => GetValue(CharacterStatType.Cooldown);
        public float CurrentHealth => currentHealth;
        public float HealthNormalized => MaxHealth <= 0f ? 0f : currentHealth / MaxHealth;
        public bool IsDead => currentHealth <= 0f;

        public void ConfigureBaseStats(IEnumerable<CharacterStatValue> baseStats, bool resetCurrentHealth = true)
        {
            float previousCurrentHealth = currentHealth;
            float previousMaxHealth = MaxHealth;

            baseStatValues.Clear();
            if (baseStats != null)
            {
                foreach (CharacterStatValue stat in baseStats)
                {
                    baseStatValues[stat.statType] = stat.value;
                }
            }

            RecalculateAllStats();

            if (resetCurrentHealth)
            {
                currentHealth = MaxHealth;
            }
            else
            {
                currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
            }

            NotifyHealthChanged(previousCurrentHealth, previousMaxHealth, true);
        }

        public void ConfigureBaseStats(params CharacterStatValue[] baseStats)
        {
            ConfigureBaseStats((IEnumerable<CharacterStatValue>)baseStats, true);
        }

        public void SetBaseStat(CharacterStatType statType, float value, bool restoreHealthToMax = false)
        {
            float previousCurrentHealth = currentHealth;
            float previousMaxHealth = MaxHealth;

            baseStatValues[statType] = value;
            RecalculateAllStats();

            if (statType == CharacterStatType.MaxHealth)
            {
                currentHealth = restoreHealthToMax ? MaxHealth : Mathf.Clamp(currentHealth, 0f, MaxHealth);
                NotifyHealthChanged(previousCurrentHealth, previousMaxHealth, true);
            }
        }

        public float GetBaseValue(CharacterStatType statType)
        {
            return baseStatValues.TryGetValue(statType, out float value) ? value : 0f;
        }

        public float GetValue(CharacterStatType statType)
        {
            return finalStatValues.TryGetValue(statType, out float value) ? value : 0f;
        }

        public bool TryGetValue(CharacterStatType statType, out float value)
        {
            return finalStatValues.TryGetValue(statType, out value);
        }

        public CharacterStatValue[] CreateSnapshot()
        {
            CharacterStatValue[] snapshot = new CharacterStatValue[finalStatValues.Count];
            int index = 0;
            foreach (KeyValuePair<CharacterStatType, float> pair in finalStatValues)
            {
                snapshot[index++] = new CharacterStatValue(pair.Key, pair.Value);
            }
            return snapshot;
        }

        public void AddModifier(CharacterStatModifier modifier)
        {
            float previousCurrentHealth = currentHealth;
            float previousMaxHealth = MaxHealth;

            modifiers.Add(modifier);
            RecalculateAllStats();
            ClampHealthAfterStatRefresh(previousCurrentHealth, previousMaxHealth);
        }

        public void AddModifier(
            CharacterStatType statType,
            float value,
            CharacterStatModifierMode mode = CharacterStatModifierMode.Flat,
            object source = null)
        {
            AddModifier(new CharacterStatModifier(statType, value, mode, source));
        }

        public int RemoveModifiersFromSource(object source)
        {
            if (source == null)
            {
                return 0;
            }

            float previousCurrentHealth = currentHealth;
            float previousMaxHealth = MaxHealth;

            int removedCount = modifiers.RemoveAll(modifier => Equals(modifier.Source, source));
            if (removedCount <= 0)
            {
                return 0;
            }

            RecalculateAllStats();
            ClampHealthAfterStatRefresh(previousCurrentHealth, previousMaxHealth);
            return removedCount;
        }

        public void ClearModifiers()
        {
            if (modifiers.Count == 0)
            {
                return;
            }

            float previousCurrentHealth = currentHealth;
            float previousMaxHealth = MaxHealth;

            modifiers.Clear();
            RecalculateAllStats();
            ClampHealthAfterStatRefresh(previousCurrentHealth, previousMaxHealth);
        }

        public float ApplyDamage(float damage)
        {
            if (damage <= 0f || MaxHealth <= 0f)
            {
                return 0f;
            }

            float previousCurrentHealth = currentHealth;
            currentHealth = Mathf.Max(0f, currentHealth - damage);
            NotifyHealthChanged(previousCurrentHealth, MaxHealth, false);
            return previousCurrentHealth - currentHealth;
        }

        public float RestoreHealth(float amount)
        {
            if (amount <= 0f || MaxHealth <= 0f)
            {
                return 0f;
            }

            float previousCurrentHealth = currentHealth;
            currentHealth = Mathf.Min(MaxHealth, currentHealth + amount);
            NotifyHealthChanged(previousCurrentHealth, MaxHealth, false);
            return currentHealth - previousCurrentHealth;
        }

        public void ResetHealthToMax()
        {
            float previousCurrentHealth = currentHealth;
            currentHealth = MaxHealth;
            NotifyHealthChanged(previousCurrentHealth, MaxHealth, true);
        }

        private void ClampHealthAfterStatRefresh(float previousCurrentHealth, float previousMaxHealth)
        {
            currentHealth = Mathf.Clamp(currentHealth, 0f, MaxHealth);
            NotifyHealthChanged(previousCurrentHealth, previousMaxHealth, false);
        }

        private void NotifyHealthChanged(float previousCurrentHealth, float previousMaxHealth, bool force)
        {
            float currentMaxHealth = MaxHealth;
            if (!force
                && Mathf.Approximately(previousCurrentHealth, currentHealth)
                && Mathf.Approximately(previousMaxHealth, currentMaxHealth))
            {
                return;
            }

            HealthChanged?.Invoke(
                this,
                new CharacterHealthChangedEventArgs(previousCurrentHealth, currentHealth, previousMaxHealth, currentMaxHealth));
        }

        private void RecalculateAllStats()
        {
            Dictionary<CharacterStatType, float> previousValues = new Dictionary<CharacterStatType, float>(finalStatValues);
            HashSet<CharacterStatType> statTypes = new HashSet<CharacterStatType>();

            foreach (KeyValuePair<CharacterStatType, float> pair in baseStatValues)
            {
                statTypes.Add(pair.Key);
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                statTypes.Add(modifiers[i].StatType);
            }

            finalStatValues.Clear();

            foreach (CharacterStatType statType in statTypes)
            {
                float currentValue = EvaluateStat(statType);
                finalStatValues[statType] = currentValue;

                float previousValue = previousValues.TryGetValue(statType, out float cachedValue) ? cachedValue : 0f;
                if (!Mathf.Approximately(previousValue, currentValue))
                {
                    StatChanged?.Invoke(this, new CharacterStatChangedEventArgs(statType, previousValue, currentValue));
                }
            }

            foreach (KeyValuePair<CharacterStatType, float> pair in previousValues)
            {
                if (finalStatValues.ContainsKey(pair.Key) || Mathf.Approximately(pair.Value, 0f))
                {
                    continue;
                }

                StatChanged?.Invoke(this, new CharacterStatChangedEventArgs(pair.Key, pair.Value, 0f));
            }
        }

        private float EvaluateStat(CharacterStatType statType)
        {
            float value = GetBaseValue(statType);
            float additivePercent = 0f;
            float multiplicativePercent = 1f;

            for (int i = 0; i < modifiers.Count; i++)
            {
                CharacterStatModifier modifier = modifiers[i];
                if (modifier.StatType != statType)
                {
                    continue;
                }

                switch (modifier.Mode)
                {
                    case CharacterStatModifierMode.Flat:
                        value += modifier.Value;
                        break;
                    case CharacterStatModifierMode.PercentAdd:
                        additivePercent += modifier.Value;
                        break;
                    case CharacterStatModifierMode.PercentMultiply:
                        multiplicativePercent *= 1f + modifier.Value;
                        break;
                }
            }

            value *= 1f + additivePercent;
            value *= multiplicativePercent;
            return Mathf.Max(0f, value);
        }
    }
}
