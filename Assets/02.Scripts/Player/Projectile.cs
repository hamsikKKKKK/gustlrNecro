using UnityEngine;
using System.Collections;

namespace Necrocis
{
    /// <summary>
    /// 투사체 — 오브젝트 풀링 지원.
    /// Launch()로 방향과 데미지를 설정하여 발사.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 15f;
        [SerializeField] private float lifeTime = 3f;

        private Vector3 moveDirection;
        private float damage;

        public void Launch(Vector3 direction, float damage)
        {
            moveDirection = direction.normalized;
            this.damage = damage;
        }

        private void OnEnable()
        {
            StartCoroutine(DeactivateAfterTime());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        void Update()
        {
            transform.position += moveDirection * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            EnemyController enemy = other.GetComponentInParent<EnemyController>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(damage);
                gameObject.SetActive(false);
            }
        }

        private IEnumerator DeactivateAfterTime()
        {
            yield return new WaitForSeconds(lifeTime);
            gameObject.SetActive(false);
        }
    }
}
