using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 허브로 돌아가는 귀환 포털
    /// </summary>
    public class ReturnPortal : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private bool isActive = true;
        [SerializeField] private float activationDelay = 1f;  // 씬 로드 후 활성화 딜레이

        private float spawnTime;

        private void Start()
        {
            spawnTime = Time.time;
        }

        private void OnTriggerEnter(Collider other)
        {
            // 씬 로드 직후 트리거 방지
            if (Time.time - spawnTime < activationDelay) return;

            if (!isActive) return;

            if (other.CompareTag("Player"))
            {
                ReturnToHub();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Time.time - spawnTime < activationDelay) return;

            if (!isActive) return;

            if (other.CompareTag("Player"))
            {
                ReturnToHub();
            }
        }

        /// <summary>
        /// 허브로 귀환
        /// </summary>
        private void ReturnToHub()
        {
            Debug.Log("[ReturnPortal] 허브로 귀환!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.ReturnToHub();
            }

            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.ReturnToHub();
            }
            else
            {
                Debug.LogWarning("[ReturnPortal] SceneLoader가 없습니다.");
            }
        }

        /// <summary>
        /// 포털 활성화/비활성화
        /// </summary>
        public void SetActive(bool active)
        {
            isActive = active;
        }
    }
}
