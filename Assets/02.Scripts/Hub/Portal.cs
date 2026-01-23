using UnityEngine;
using UnityEngine.Events;

namespace Necrocis
{
    /// <summary>
    /// 바이옴 포털 시스템
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [Header("포털 설정")]
        [SerializeField] private BiomeType targetBiome = BiomeType.None;
        [SerializeField] private bool isActive = true;
        [SerializeField] private float interactionRadius = 2f;

        [Header("시각 효과")]
        [SerializeField] private SpriteRenderer portalRenderer;
        [SerializeField] private Color activeColor = Color.cyan;
        [SerializeField] private Color inactiveColor = Color.gray;
        [SerializeField] private ParticleSystem portalEffect;

        [Header("이벤트")]
        public UnityEvent<BiomeType> OnPortalEnter;

        public BiomeType TargetBiome => targetBiome;
        public bool IsActive => isActive;

        private void Awake()
        {
            // 스프라이트 렌더러 찾기
            if (portalRenderer == null)
            {
                portalRenderer = GetComponent<SpriteRenderer>();
            }

            // Billboard 추가 (2.5D 효과)
            if (GetComponent<Billboard>() == null)
            {
                gameObject.AddComponent<Billboard>();
            }

            // sortingOrder는 Initialize에서 설정

            // 좌우 벽 콜라이더 추가 (위아래로만 진입 가능)
            CreateSideWalls();
        }

        /// <summary>
        /// 좌우 벽 생성 (옆으로 통과 불가) - 현재 비활성화
        /// </summary>
        private void CreateSideWalls()
        {
            // 포털 측면 벽 비활성화 - 이동에 방해됨
            // 필요시 나중에 활성화
            /*
            float wallWidth = 0.3f;
            float wallHeight = 3f;
            float wallDepth = 2f;
            float portalWidth = 1.5f;

            // 왼쪽 벽
            GameObject leftWall = new GameObject("LeftWall");
            leftWall.transform.SetParent(transform);
            leftWall.transform.localPosition = new Vector3(-portalWidth, 0, 0);
            leftWall.transform.localRotation = Quaternion.identity;
            BoxCollider leftCol = leftWall.AddComponent<BoxCollider>();
            leftCol.size = new Vector3(wallWidth, wallHeight, wallDepth);
            leftCol.isTrigger = false;

            // 오른쪽 벽
            GameObject rightWall = new GameObject("RightWall");
            rightWall.transform.SetParent(transform);
            rightWall.transform.localPosition = new Vector3(portalWidth, 0, 0);
            rightWall.transform.localRotation = Quaternion.identity;
            BoxCollider rightCol = rightWall.AddComponent<BoxCollider>();
            rightCol.size = new Vector3(wallWidth, wallHeight, wallDepth);
            rightCol.isTrigger = false;
            */
        }

        /// <summary>
        /// 포털 초기화
        /// </summary>
        public void Initialize(BiomeType biome)
        {
            targetBiome = biome;

            // 스프라이트 렌더러 찾기
            if (portalRenderer == null)
            {
                portalRenderer = GetComponent<SpriteRenderer>();
            }

            // Y정렬 추가 (없으면)
            if (GetComponent<SpriteYSort>() == null)
            {
                gameObject.AddComponent<SpriteYSort>();
            }

            UpdateVisuals();

            BiomeData data = BiomeData.GetBiomeData(biome);
            if (data != null)
            {
                gameObject.name = $"Portal_{data.displayName}";
            }

            Debug.Log($"[Portal] {targetBiome} 포털 초기화 완료");
        }

        /// <summary>
        /// 포털 활성화/비활성화
        /// </summary>
        public void SetActive(bool active)
        {
            isActive = active;
            UpdateVisuals();
        }

        /// <summary>
        /// 포털 진입 시도
        /// </summary>
        public void TryEnterPortal(GameObject player)
        {
            if (!isActive)
            {
                Debug.Log($"[Portal] {targetBiome} 포털이 비활성화 상태입니다.");
                return;
            }

            if (targetBiome == BiomeType.None)
            {
                Debug.LogWarning("[Portal] 목표 바이옴이 설정되지 않았습니다.");
                return;
            }

            // 바이옴 진입 처리
            EnterBiome();
        }

        /// <summary>
        /// 바이옴 진입
        /// </summary>
        private void EnterBiome()
        {
            BiomeData data = BiomeData.GetBiomeData(targetBiome);
            Debug.Log($"[Portal] {data.displayName} 바이옴으로 이동! (크기: {data.mapSize.x}x{data.mapSize.y})");

            // GameManager에 바이옴 진입 알림
            if (GameManager.Instance != null)
            {
                GameManager.Instance.EnterBiome(targetBiome);
            }

            OnPortalEnter?.Invoke(targetBiome);

            // 씬 전환
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadBiome(targetBiome);
            }
            else
            {
                Debug.LogWarning("[Portal] SceneLoader가 없습니다. 씬 전환 불가.");
            }
        }

        /// <summary>
        /// 시각 효과 업데이트
        /// </summary>
        private void UpdateVisuals()
        {
            if (portalRenderer != null)
            {
                portalRenderer.color = isActive ? activeColor : inactiveColor;
            }

            if (portalEffect != null)
            {
                if (isActive)
                    portalEffect.Play();
                else
                    portalEffect.Stop();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 게임 시작 직후 트리거 무시 (스폰 시 오작동 방지)
            if (Time.timeSinceLevelLoad < 1f) return;

            if (other.CompareTag("Player"))
            {
                TryEnterPortal(other.gameObject);
            }
        }

        // 2D 콜라이더 대응
        private void OnTriggerEnter2D(Collider2D other)
        {
            // 게임 시작 직후 트리거 무시
            if (Time.timeSinceLevelLoad < 1f) return;

            if (other.CompareTag("Player"))
            {
                TryEnterPortal(other.gameObject);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // 상호작용 범위 표시
            Gizmos.color = isActive ? Color.cyan : Color.gray;
            Gizmos.DrawWireSphere(transform.position, interactionRadius);

            // 바이옴 타입 라벨
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 2f,
                $"{targetBiome}"
            );
        }
#endif
    }
}
