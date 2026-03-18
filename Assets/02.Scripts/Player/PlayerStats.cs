using System;
using System.Collections.Generic;
using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 전용 스탯 진입점.
    /// 아이템, 레벨업, UI는 PlayerController 대신 이 컴포넌트와 연결하면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
    public class PlayerStats : MonoBehaviour
    {
        private CharacterStats runtimeStats;
        private bool initialized;
        private float baseMoveSpeed = 5f;
        private float baseMaxHealth = 100f;
        private float baseAttackPower = 10f;

        public event Action<CharacterStats, CharacterStatChangedEventArgs> StatChanged
        {
            add => RuntimeStats.StatChanged += value;
            remove => RuntimeStats.StatChanged -= value;
        }

        public event Action<CharacterStats, CharacterHealthChangedEventArgs> HealthChanged
        {
            add => RuntimeStats.HealthChanged += value;
            remove => RuntimeStats.HealthChanged -= value;
        }

        public CharacterStats RuntimeStats
        {
            get
            {
                if (runtimeStats == null)
                {
                    runtimeStats = GetComponent<CharacterStats>();
                    if (runtimeStats == null)
                    {
                        runtimeStats = gameObject.AddComponent<CharacterStats>();
                    }
                }

                return runtimeStats;
            }
        }

        public float MoveSpeed
        {
            get
            {
                EnsureInitialized();
                return RuntimeStats.MoveSpeed;
            }
        }

        public float MaxHealth
        {
            get
            {
                EnsureInitialized();
                return RuntimeStats.MaxHealth;
            }
        }

        public float CurrentHealth
        {
            get
            {
                EnsureInitialized();
                return RuntimeStats.CurrentHealth;
            }
        }

        public float AttackPower
        {
            get
            {
                EnsureInitialized();
                return RuntimeStats.AttackPower;
            }
        }

        public bool IsDead
        {
            get
            {
                EnsureInitialized();
                return RuntimeStats.IsDead;
            }
        }

        public void ConfigureBaseStats(float moveSpeed, float maxHealth, float attackPower, bool resetCurrentHealth = false)
        {
            baseMoveSpeed = moveSpeed;
            baseMaxHealth = maxHealth;
            baseAttackPower = attackPower;

            CharacterStatValue[] baseStats =
            {
                new CharacterStatValue(CharacterStatType.MoveSpeed, baseMoveSpeed),
                new CharacterStatValue(CharacterStatType.MaxHealth, baseMaxHealth),
                new CharacterStatValue(CharacterStatType.AttackPower, baseAttackPower)
            };

            RuntimeStats.ConfigureBaseStats(baseStats, !initialized || resetCurrentHealth);
            initialized = true;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            ConfigureBaseStats(baseMoveSpeed, baseMaxHealth, baseAttackPower, true);
        }

        public void TakeDamage(float damage)
        {
            EnsureInitialized();
            RuntimeStats.ApplyDamage(damage);
        }

        public void Heal(float amount)
        {
            EnsureInitialized();
            RuntimeStats.RestoreHealth(amount);
        }

        public void ApplyModifier(CharacterStatModifier modifier)
        {
            EnsureInitialized();
            RuntimeStats.AddModifier(modifier);
        }

        public void ApplyModifiers(IEnumerable<CharacterStatModifierData> modifiers, object source)
        {
            EnsureInitialized();
            if (modifiers == null)
            {
                return;
            }

            foreach (CharacterStatModifierData modifier in modifiers)
            {
                RuntimeStats.AddModifier(modifier.ToModifier(source));
            }
        }

        public void ApplyOrReplaceSourceModifiers(IEnumerable<CharacterStatModifierData> modifiers, object source)
        {
            EnsureInitialized();
            RuntimeStats.RemoveModifiersFromSource(source);
            ApplyModifiers(modifiers, source);
        }

        public int RemoveModifiersFromSource(object source)
        {
            EnsureInitialized();
            return RuntimeStats.RemoveModifiersFromSource(source);
        }
    }
}
