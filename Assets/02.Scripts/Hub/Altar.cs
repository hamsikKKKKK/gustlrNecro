using UnityEngine;
using UnityEngine.Events;

namespace Necrocis
{
    /// <summary>
    /// 재단 시스템 - 4개 부산물 바침 → 대뇌 맵 이동
    /// </summary>
    public class Altar : MonoBehaviour
    {
        [Header("재단 설정")]
        [SerializeField] private float interactionRadius = 3f;
        [SerializeField] private bool isActivated = false;

        [Header("부산물 슬롯 (시각용)")]
        [SerializeField] private Transform[] relicSlots = new Transform[4];
        [SerializeField] private GameObject[] relicVisuals = new GameObject[4];

        [Header("시각 효과")]
        [SerializeField] private SpriteRenderer altarRenderer;
        [SerializeField] private Color inactiveColor = Color.gray;
        [SerializeField] private Color readyColor = Color.yellow;
        [SerializeField] private Color activatedColor = Color.magenta;
        [SerializeField] private ParticleSystem activationEffect;

        [Header("이벤트")]
        public UnityEvent OnAltarActivated;
        public UnityEvent OnFinalBossEnter;

        public bool IsActivated => isActivated;

        private void Start()
        {
            UpdateVisuals();

            // GameManager 이벤트 구독
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnAllRelicsCollected.AddListener(OnAllRelicsCollected);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnAllRelicsCollected.RemoveListener(OnAllRelicsCollected);
            }
        }

        /// <summary>
        /// 모든 부산물 수집 시 호출
        /// </summary>
        private void OnAllRelicsCollected()
        {
            Debug.Log("[Altar] 모든 부산물 수집됨! 재단 활성화 준비");
            UpdateVisuals();
        }

        /// <summary>
        /// 재단 상호작용 시도
        /// </summary>
        public void TryInteract(GameObject player)
        {
            if (GameManager.Instance == null) return;

            if (isActivated)
            {
                // 이미 활성화됨 → 대뇌 맵으로 이동
                EnterFinalBoss();
                return;
            }

            if (GameManager.Instance.HasAllRelics)
            {
                // 4개 부산물 보유 → 재단 활성화
                ActivateAltar();
            }
            else
            {
                // 부산물 부족
                int collected = GameManager.Instance.CollectedRelicCount;
                Debug.Log($"[Altar] 부산물이 부족합니다. ({collected}/4)");
                ShowMissingRelics();
            }
        }

        /// <summary>
        /// 재단 활성화 (부산물 바침)
        /// </summary>
        private void ActivateAltar()
        {
            isActivated = true;

            Debug.Log("[Altar] 재단 활성화! 대뇌 맵 진입 가능");

            // 부산물 시각 효과
            ShowAllRelics();

            // 이펙트 재생
            if (activationEffect != null)
            {
                activationEffect.Play();
            }

            UpdateVisuals();
            OnAltarActivated?.Invoke();
        }

        /// <summary>
        /// 최종 보스(대뇌) 맵 진입
        /// </summary>
        private void EnterFinalBoss()
        {
            Debug.Log("[Altar] 대뇌 맵으로 이동!");

            if (GameManager.Instance != null)
            {
                GameManager.Instance.EnterFinalBoss();
            }

            OnFinalBossEnter?.Invoke();

            // TODO: 실제 대뇌 맵 로드
        }

        /// <summary>
        /// 부족한 부산물 표시
        /// </summary>
        private void ShowMissingRelics()
        {
            if (GameManager.Instance == null) return;

            string missing = "부족한 부산물: ";

            if (!GameManager.Instance.HasRelic(BiomeType.Intestine))
                missing += "장, ";
            if (!GameManager.Instance.HasRelic(BiomeType.Liver))
                missing += "간, ";
            if (!GameManager.Instance.HasRelic(BiomeType.Stomach))
                missing += "위, ";
            if (!GameManager.Instance.HasRelic(BiomeType.Lung))
                missing += "폐, ";

            Debug.Log($"[Altar] {missing.TrimEnd(',', ' ')}");
        }

        /// <summary>
        /// 모든 부산물 시각 표시
        /// </summary>
        private void ShowAllRelics()
        {
            for (int i = 0; i < relicVisuals.Length; i++)
            {
                if (relicVisuals[i] != null)
                {
                    relicVisuals[i].SetActive(true);
                }
            }
        }

        /// <summary>
        /// 시각 효과 업데이트
        /// </summary>
        private void UpdateVisuals()
        {
            if (altarRenderer == null) return;

            if (isActivated)
            {
                altarRenderer.color = activatedColor;
            }
            else if (GameManager.Instance != null && GameManager.Instance.HasAllRelics)
            {
                altarRenderer.color = readyColor;
            }
            else
            {
                altarRenderer.color = inactiveColor;
            }
        }

        /// <summary>
        /// 수집된 부산물 개별 표시
        /// </summary>
        public void UpdateRelicDisplay()
        {
            if (GameManager.Instance == null) return;

            BiomeType[] biomes = { BiomeType.Intestine, BiomeType.Liver, BiomeType.Stomach, BiomeType.Lung };

            for (int i = 0; i < biomes.Length && i < relicVisuals.Length; i++)
            {
                if (relicVisuals[i] != null)
                {
                    relicVisuals[i].SetActive(GameManager.Instance.HasRelic(biomes[i]));
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                TryInteract(other.gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                TryInteract(other.gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 상호작용 범위
            Gizmos.color = isActivated ? Color.magenta : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);

            // 라벨
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2.5f,
                isActivated ? "재단 (활성화)" : "재단"
            );

            // 부산물 슬롯 위치
            Gizmos.color = Color.red;
            foreach (var slot in relicSlots)
            {
                if (slot != null)
                {
                    Gizmos.DrawWireCube(slot.position, Vector3.one * 0.5f);
                }
            }
        }
#endif
    }
}
