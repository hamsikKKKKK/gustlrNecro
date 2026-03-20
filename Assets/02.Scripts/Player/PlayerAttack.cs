using UnityEngine;
using UnityEngine.InputSystem;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 공격 (Q=근거리, E=원거리)
    /// PlayerController의 방향을 사용하여 공격 방향 결정
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
        [Header("근거리 공격 (Q)")]
        [SerializeField] private float meleeAttackDamage = 20f;
        [SerializeField] private Vector3 meleeAttackBoxSize = new Vector3(3, 3, 3);
        [SerializeField] private float meleeAttackOffset = 2f;

        [Header("원거리 공격 (E)")]
        [SerializeField] private Transform projectileSpawnPoint;

        [Header("공통 설정")]
        [SerializeField] private float attackCooldown = 0.3f;

        private float lastAttackTime = float.NegativeInfinity;

        void Update()
        {
            HandleAttackInput();
        }

        private void HandleAttackInput()
        {
            var input = InputManager.Instance;
            if (input == null) return;

            bool canAttack = Time.time >= lastAttackTime + attackCooldown;

            if (input.DebugLevelUpAction.WasPressedThisFrame())
            {
                LevelUpManager.DebugLevelUp();
                return;
            }

            if (input.MeleeAttackAction.WasPressedThisFrame())
            {
                MeleeAttack();
            }
            else if (input.RangedAttackAction.WasPressedThisFrame())
            {
                if (!canAttack) return;
                lastAttackTime = Time.time;
                RangedAttack();
            }
        }

        private Vector3 GetAttackDirection()
        {
            if (PlayerController.Instance == null)
                return Vector3.forward;

            switch (PlayerController.Instance.GetCurrentDirection())
            {
                case PlayerController.Direction.Up:    return Vector3.forward;
                case PlayerController.Direction.Down:  return Vector3.back;
                case PlayerController.Direction.Left:  return Vector3.left;
                case PlayerController.Direction.Right: return Vector3.right;
                default: return Vector3.forward;
            }
        }

        private void MeleeAttack()
        {
            Vector3 direction = GetAttackDirection();
            Vector3 boxCenter = transform.position + direction * meleeAttackOffset;
            // Y를 높여서 높이 차이와 관계없이 적을 감지
            Vector3 tallBoxSize = new Vector3(meleeAttackBoxSize.x, 20f, meleeAttackBoxSize.z);
            Quaternion rotation = Quaternion.LookRotation(direction);
            Collider[] hitColliders = Physics.OverlapBox(boxCenter, tallBoxSize / 2, rotation, ~0, QueryTriggerInteraction.Collide);

            Debug.Log($"[PlayerAttack] 근거리 공격! 플레이어={transform.position}, 방향={direction}, 판정위치={boxCenter}, 히트수={hitColliders.Length}");

            foreach (var hitCollider in hitColliders)
            {
                EnemyController enemy = hitCollider.GetComponentInParent<EnemyController>();
                if (enemy != null && !enemy.IsDead)
                {
                    float damage = meleeAttackDamage;
                    if (PlayerStats.Instance != null)
                        damage = PlayerStats.Instance.GetAttack();
                    enemy.TakeDamage(damage);
                    Debug.Log($"[PlayerAttack] {hitCollider.gameObject.name}에게 {damage} 데미지!");
                }
            }
        }

        private void RangedAttack()
        {
            if (ObjectPooler.Instance == null)
            {
                Debug.LogWarning("[PlayerAttack] ObjectPooler.Instance가 null입니다. 씬에 ObjectPooler가 있는지 확인하세요.");
                return;
            }

            GameObject projectile = ObjectPooler.Instance.GetPooledObject();
            if (projectile == null)
            {
                Debug.LogWarning("[PlayerAttack] 풀에서 사용 가능한 투사체가 없습니다.");
                return;
            }

            Debug.Log($"[PlayerAttack] 원거리 공격! 방향={GetAttackDirection()}");

            Vector3 spawnPos = projectileSpawnPoint != null
                ? projectileSpawnPoint.position
                : transform.position;

            projectile.transform.position = spawnPos;
            projectile.SetActive(true);

            Projectile proj = projectile.GetComponent<Projectile>();
            if (proj != null)
            {
                float damage = 10f;
                if (PlayerStats.Instance != null)
                    damage = PlayerStats.Instance.GetAttack();
                proj.Launch(GetAttackDirection(), damage);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 direction = GetAttackDirection();
            Gizmos.color = Color.red;
            Gizmos.matrix = Matrix4x4.TRS(
                transform.position + direction * meleeAttackOffset,
                Quaternion.LookRotation(direction),
                Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(meleeAttackBoxSize.x, 20f, meleeAttackBoxSize.z));
        }
    }
}
