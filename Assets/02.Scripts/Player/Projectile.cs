using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 침 뱉기용 원거리 투사체
    /// - 앞으로 이동
    /// - 적과 충돌 시 데미지
    /// - 최대 거리 도달 시 비활성화
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Header("이동")]
        [SerializeField] private float speed = 12f;
        [SerializeField] private float maxDistance = 10f;

        [Header("충돌")]
        [SerializeField] private LayerMask hitMask = ~0;
        [SerializeField] private bool disableOnHit = true;

        private Vector3 moveDirection;
        private Vector3 startPosition;
        private float damage;
        private bool launched;
        private GameObject owner;

        public void Launch(Vector3 direction, float projectileDamage, GameObject projectileOwner = null)
        {
            moveDirection = direction.normalized;
            damage = projectileDamage;
            owner = projectileOwner;
            startPosition = transform.position;
            launched = true;

            Debug.Log($"[Projectile] 발사 시작 | 방향={moveDirection} | 데미지={damage} | 위치={transform.position}");

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection);
            }
        }

        private void Update()
        {
            if (!launched)
                return;

            transform.position += moveDirection * speed * Time.deltaTime;

            float distance = Vector3.Distance(startPosition, transform.position);
            if (distance >= maxDistance)
            {
                DisableProjectile();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!launched)
                return;

            Debug.Log($"[Projectile] 충돌 감지 | 대상={other.gameObject.name} | 레이어={other.gameObject.layer}");

            if (owner != null && other.transform.root.gameObject == owner)
                return;

            if (((1 << other.gameObject.layer) & hitMask) == 0)
                return;

            EnemyController enemy = other.GetComponent<EnemyController>();
            if (enemy == null)
                enemy = other.GetComponentInParent<EnemyController>();

            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(damage);
                Debug.Log($"[Projectile] 침 적중 | 대상={enemy.gameObject.name} | 데미지={damage}");

                if (disableOnHit)
                {
                    DisableProjectile();
                }

                return;
            }

            if (disableOnHit)
            {
                DisableProjectile();
            }
        }

        private void DisableProjectile()
        {
            launched = false;
            owner = null;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            launched = false;
            owner = null;
        }
    }
}