using UnityEngine;
using System;
using System.Collections;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 체력 관리.
    /// CharacterStats 백엔드를 사용하며, 방어력 감소 + 무적 시간 처리.
    /// </summary>
    public class Health : MonoBehaviour
    {
        [Tooltip("피격 후 무적 시간(초)")]
        [SerializeField] private float invincibilityDuration = 0.2f;

        private bool isInvincible;

        private CharacterStats Stats => PlayerStats.Instance?.RuntimeStats;

        public float CurrentHealth => Stats?.CurrentHealth ?? 0f;
        public float MaxHealth => Stats?.MaxHealth ?? 0f;
        public bool IsDead => Stats?.IsDead ?? false;

        public event Action<float, float> OnHealthChanged;
        public event Action OnDeath;

        private void OnEnable()
        {
            if (Stats != null)
                Stats.HealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (Stats != null)
                Stats.HealthChanged -= HandleHealthChanged;
        }

        private void HandleHealthChanged(CharacterStats sender, CharacterHealthChangedEventArgs args)
        {
            OnHealthChanged?.Invoke(args.CurrentValue, args.MaxValue);

            if (args.CurrentValue <= 0f && args.PreviousValue > 0f)
                OnDeath?.Invoke();
        }

        public void TakeDamage(float damageAmount)
        {
            if (isInvincible || IsDead || damageAmount <= 0f) return;

            float defense = Stats?.Defense ?? 0f;
            float actualDamage = Mathf.Max(1f, damageAmount - defense);
            Stats?.ApplyDamage(actualDamage);

            StartCoroutine(InvincibilityCoroutine());
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Stats?.RestoreHealth(amount);
        }

        public void ResetHealth()
        {
            isInvincible = false;
            Stats?.ResetHealthToMax();
        }

        private IEnumerator InvincibilityCoroutine()
        {
            isInvincible = true;
            yield return new WaitForSeconds(invincibilityDuration);
            isInvincible = false;
        }
    }
}
