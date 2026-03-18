using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 기본 공격 모듈.
    /// 현재는 전방 부채꼴 판정 근접 공격을 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAttackModule : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private bool useDirectionalArrowAttack = true;

        [Header("공격")]
        [SerializeField] private float attackCooldown = 0.35f;
        [SerializeField] private float attackOriginOffset = 0.35f;
        [SerializeField] private float attackRadius = 1.8f;
        [SerializeField, Range(20f, 180f)] private float attackAngle = 100f;
        [SerializeField] private float attackHeightOffset = 0.75f;
        [SerializeField] private float enemyKnockbackDistance = 0.4f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private int overlapBufferSize = 16;

        private readonly HashSet<EnemyController> hitEnemies = new HashSet<EnemyController>();

        private PlayerController playerController;
        private Collider[] overlapResults;
        private float nextAttackTime;

        public event Action<PlayerAttackModule> AttackPerformed;
        public event Action<PlayerAttackModule, EnemyController, float> EnemyHit;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            EnsureOverlapBuffer();
        }

        private void Update()
        {
            if (!ShouldAcceptInput())
            {
                return;
            }

            if (!TryGetAttackDirectionInput(out PlayerController.Direction attackDirection))
            {
                return;
            }

            TryAttack(attackDirection);
        }

        public bool TryAttack(PlayerController.Direction attackDirection)
        {
            if (playerController == null || Time.time < nextAttackTime)
            {
                return false;
            }

            float attackDamage = playerController.AttackPower;
            if (attackDamage <= 0f)
            {
                return false;
            }

            nextAttackTime = Time.time + attackCooldown;
            EnsureOverlapBuffer();
            hitEnemies.Clear();
            playerController.FaceDirection(attackDirection);

            Vector3 attackDirectionVector = DirectionToVector(attackDirection);
            Vector3 attackOrigin = transform.position;
            attackOrigin.y += attackHeightOffset;
            Vector3 overlapCenter = attackOrigin + attackDirectionVector * attackOriginOffset;
            float halfAngle = attackAngle * 0.5f;

            int hitCount = Physics.OverlapSphereNonAlloc(
                overlapCenter,
                attackRadius,
                overlapResults,
                targetMask,
                QueryTriggerInteraction.Collide);

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = overlapResults[i];
                if (collider == null)
                {
                    continue;
                }

                EnemyController enemy = collider.GetComponent<EnemyController>();
                if (enemy == null)
                {
                    enemy = collider.GetComponentInParent<EnemyController>();
                }

                if (enemy == null || enemy.IsDead || !hitEnemies.Add(enemy))
                {
                    continue;
                }

                Vector3 enemyPosition = enemy.transform.position;
                enemyPosition.y = attackOrigin.y;
                Vector3 toEnemy = enemyPosition - attackOrigin;
                if (toEnemy.sqrMagnitude <= 0.0001f)
                {
                    continue;
                }

                float angleToEnemy = Vector3.Angle(attackDirectionVector, toEnemy.normalized);
                if (angleToEnemy > halfAngle)
                {
                    continue;
                }

                enemy.TakeDamage(attackDamage);
                enemy.ApplyKnockback(attackDirectionVector, enemyKnockbackDistance);
                Debug.Log($"[PlayerAttack] {attackDirection} 공격 적중 | 대상: {enemy.gameObject.name} | 데미지: {attackDamage}");
                EnemyHit?.Invoke(this, enemy, attackDamage);
            }

            if (hitEnemies.Count == 0)
            {
                Debug.Log($"[PlayerAttack] {attackDirection} 공격 | 데미지: {attackDamage} | 적중 대상 없음");
            }

            AttackPerformed?.Invoke(this);
            return true;
        }

        private bool ShouldAcceptInput()
        {
            if (!Application.isFocused || Time.timeSinceLevelLoad < 0.5f)
            {
                return false;
            }

            if (playerController == null || playerController.IsDead)
            {
                return false;
            }

            return true;
        }

        private bool TryGetAttackDirectionInput(out PlayerController.Direction attackDirection)
        {
            Keyboard keyboard = Keyboard.current;
            attackDirection = PlayerController.Direction.Down;
            if (!useDirectionalArrowAttack || keyboard == null)
            {
                return false;
            }

            if (keyboard.upArrowKey.isPressed)
            {
                attackDirection = PlayerController.Direction.Up;
                return true;
            }

            if (keyboard.downArrowKey.isPressed)
            {
                attackDirection = PlayerController.Direction.Down;
                return true;
            }

            if (keyboard.leftArrowKey.isPressed)
            {
                attackDirection = PlayerController.Direction.Left;
                return true;
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                attackDirection = PlayerController.Direction.Right;
                return true;
            }

            return false;
        }

        private static Vector3 DirectionToVector(PlayerController.Direction direction)
        {
            return direction switch
            {
                PlayerController.Direction.Up => Vector3.forward,
                PlayerController.Direction.Left => Vector3.left,
                PlayerController.Direction.Right => Vector3.right,
                _ => Vector3.back
            };
        }

        private void EnsureOverlapBuffer()
        {
            int desiredSize = Mathf.Max(1, overlapBufferSize);
            if (overlapResults != null && overlapResults.Length == desiredSize)
            {
                return;
            }

            overlapResults = new Collider[desiredSize];
        }

        private void OnDrawGizmosSelected()
        {
            PlayerController controller = playerController != null ? playerController : GetComponent<PlayerController>();
            Vector3 attackDirection = controller != null
                ? DirectionToVector(controller.GetCurrentDirection())
                : Vector3.forward;

            Vector3 attackOrigin = transform.position;
            attackOrigin.y += attackHeightOffset;
            Vector3 overlapCenter = attackOrigin + attackDirection * attackOriginOffset;
            Vector3 leftBoundary = Quaternion.Euler(0f, -attackAngle * 0.5f, 0f) * attackDirection;
            Vector3 rightBoundary = Quaternion.Euler(0f, attackAngle * 0.5f, 0f) * attackDirection;

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.45f);
            Gizmos.DrawWireSphere(overlapCenter, attackRadius);
            Gizmos.DrawLine(attackOrigin, attackOrigin + leftBoundary * attackRadius);
            Gizmos.DrawLine(attackOrigin, attackOrigin + rightBoundary * attackRadius);
        }
    }
}
