using UnityEngine;
using System;
using System.Collections;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 체력 관리.
    /// PlayerStats와 연동하여 최대 체력을 가져옴.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [SerializeField] private float baseMaxHealth = 100f;
        [Tooltip("피격 후 무적 시간(초)")]
        [SerializeField] private float invincibilityDuration = 0.2f;

        private float currentHealth;
        private bool isInvincible;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => GetMaxHealth();
        public bool IsDead => currentHealth <= 0f;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDeath;

        private void Awake()
        {
            currentHealth = GetMaxHealth();
        }

        private float GetMaxHealth()
        {
            if (PlayerStats.Instance != null)
                return PlayerStats.Instance.GetHealth();
            return baseMaxHealth;
        }

        public void TakeDamage(float damageAmount)
        {
            if (isInvincible || IsDead || damageAmount <= 0f) return;

            float defense = 0f;
            if (PlayerStats.Instance != null)
                defense = PlayerStats.Instance.GetDefense();

            float actualDamage = Mathf.Max(1f, damageAmount - defense);
            currentHealth = Mathf.Max(0f, currentHealth - actualDamage);

            OnHealthChanged?.Invoke(currentHealth, MaxHealth);

            StartCoroutine(InvincibilityCoroutine());

            if (currentHealth <= 0f)
            {
                OnDeath?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            currentHealth = Mathf.Min(currentHealth + amount, MaxHealth);
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        public void ResetHealth()
        {
            currentHealth = GetMaxHealth();
            isInvincible = false;
            OnHealthChanged?.Invoke(currentHealth, MaxHealth);
        }

        private IEnumerator InvincibilityCoroutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(invincibilityDuration);
            isInvincible = false;
        }
    }
}
