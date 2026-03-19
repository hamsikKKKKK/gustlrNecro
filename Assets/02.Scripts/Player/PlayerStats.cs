using System;
using System.Collections.Generic;
using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 스탯 싱글톤.
    /// CharacterStats를 백엔드로 사용하며, 레벨업 시스템과 연동.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterStats))]
    public class PlayerStats : MonoBehaviour
    {
        public static PlayerStats Instance { get; private set; }

        private CharacterStats runtimeStats;
        private bool initialized;

        // 기본 스탯
        private const float BASE_MAX_HEALTH = 100f;
        private const float BASE_MOVE_SPEED = 5f;
        private const float BASE_ATTACK_POWER = 30f;
        private const float BASE_DEFENSE = 5f;
        private const float BASE_ATTACK_SPEED = 1f;
        private const float BASE_RANGE = 1f;
        private const float BASE_MAGIC = 20f;
        private const float BASE_COOLDOWN = 0f;

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
                        runtimeStats = gameObject.AddComponent<CharacterStats>();
                }
                return runtimeStats;
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (initialized) return;

            RuntimeStats.ConfigureBaseStats(new CharacterStatValue[]
            {
                new(CharacterStatType.MaxHealth, BASE_MAX_HEALTH),
                new(CharacterStatType.MoveSpeed, BASE_MOVE_SPEED),
                new(CharacterStatType.AttackPower, BASE_ATTACK_POWER),
                new(CharacterStatType.Defense, BASE_DEFENSE),
                new(CharacterStatType.AttackSpeed, BASE_ATTACK_SPEED),
                new(CharacterStatType.Range, BASE_RANGE),
                new(CharacterStatType.Magic, BASE_MAGIC),
                new(CharacterStatType.Cooldown, BASE_COOLDOWN),
            }, true);

            initialized = true;
        }

        // ─────────────────────────────────
        // 레벨업 연동
        // ─────────────────────────────────

        public void ApplyStatChoice(StatChoice choice)
        {
            EnsureInitialized();
            StatEffect effect = StatManager.GetStatEffect(choice);

            foreach (var stat in effect.flatStats)
            {
                RuntimeStats.AddModifier(
                    stat.Key,
                    stat.Value,
                    CharacterStatModifierMode.Flat,
                    choice);
            }

            foreach (var stat in effect.percentStats)
            {
                RuntimeStats.AddModifier(
                    stat.Key,
                    stat.Value / 100f,
                    CharacterStatModifierMode.PercentAdd,
                    choice);
            }
        }

        public void ResetStats()
        {
            RuntimeStats.ClearModifiers();
        }

        // ─────────────────────────────────
        // 편의 게터 (기존 코드 호환)
        // ─────────────────────────────────

        public float GetHealth() => RuntimeStats.MaxHealth;
        public float GetAttack() => RuntimeStats.AttackPower;
        public float GetDefense() => RuntimeStats.Defense;
        public float GetSpeed() => RuntimeStats.MoveSpeed;
        public float GetAttackSpeed() => RuntimeStats.AttackSpeed;
        public float GetRange() => RuntimeStats.Range;
        public float GetMagic() => RuntimeStats.Magic;
        public float GetCooldown() => RuntimeStats.Cooldown;

        // ─────────────────────────────────
        // HP 위임
        // ─────────────────────────────────

        public void TakeDamage(float damage) => RuntimeStats.ApplyDamage(damage);
        public void Heal(float amount) => RuntimeStats.RestoreHealth(amount);

        // ─────────────────────────────────
        // 모디파이어 API (아이템/버프용)
        // ─────────────────────────────────

        public void ApplyModifier(CharacterStatModifier modifier)
        {
            EnsureInitialized();
            RuntimeStats.AddModifier(modifier);
        }

        public void ApplyModifiers(IEnumerable<CharacterStatModifierData> modifiers, object source)
        {
            EnsureInitialized();
            if (modifiers == null) return;
            foreach (CharacterStatModifierData modifier in modifiers)
                RuntimeStats.AddModifier(modifier.ToModifier(source));
        }

        public int RemoveModifiersFromSource(object source)
        {
            EnsureInitialized();
            return RuntimeStats.RemoveModifiersFromSource(source);
        }
    }
}
