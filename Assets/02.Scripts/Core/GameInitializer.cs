using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 게임 초기화 - 모든 시스템 연결
    /// </summary>
    public class GameInitializer : MonoBehaviour
    {
        [Header("프리팹")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private GameObject cameraPrefab;

        [Header("자동 생성 여부")]
        [SerializeField] private bool autoCreatePlayer = true;
        [SerializeField] private bool autoCreateCamera = true;

        [Header("참조 (자동 할당)")]
        [SerializeField] private PlayerController player;
        [SerializeField] private DontStarveCamera mainCamera;
        [SerializeField] private HubRoom hubRoom;

        private void Awake()
        {
            InitializeGame();
        }

        /// <summary>
        /// 게임 초기화
        /// </summary>
        private void InitializeGame()
        {
            // InputManager 확인/생성
            if (InputManager.Instance == null)
            {
                GameObject inputObj = new GameObject("InputManager");
                inputObj.AddComponent<InputManager>();
            }

            // GameManager 확인/생성
            if (GameManager.Instance == null)
            {
                GameObject gmObj = new GameObject("GameManager");
                gmObj.AddComponent<GameManager>();
            }

            // HubRoom 찾기
            if (hubRoom == null)
            {
                hubRoom = FindFirstObjectByType<HubRoom>();
            }

            // 플레이어 생성
            if (autoCreatePlayer)
            {
                CreatePlayer();
            }

            // 카메라 생성
            if (autoCreateCamera)
            {
                CreateCamera();
            }

            Debug.Log("[GameInitializer] 게임 초기화 완료!");
        }

        /// <summary>
        /// 플레이어 생성
        /// </summary>
        private void CreatePlayer()
        {
            // 기존 플레이어 찾기
            player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                EnsurePlayerComponents(player.gameObject);
                return;
            }

            // 스폰 위치 결정
            Vector3 spawnPos = Vector3.zero;
            if (hubRoom != null)
            {
                spawnPos = hubRoom.GetPlayerSpawnPosition();
            }

            // 프리팹으로 생성
            if (playerPrefab != null)
            {
                GameObject playerObj = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
                playerObj.name = "Player";
                player = playerObj.GetComponent<PlayerController>();
            }
            else
            {
                // 기본 플레이어 생성
                GameObject playerObj = new GameObject("Player");
                playerObj.transform.position = spawnPos;
                playerObj.tag = "Player";

                // 컴포넌트 추가
                player = playerObj.AddComponent<PlayerController>();

                // 콜라이더
                CapsuleCollider col = playerObj.AddComponent<CapsuleCollider>();
                col.height = 2f;
                col.radius = 0.5f;
                col.center = new Vector3(0, 1f, 0);

                // Rigidbody (물리 드리프트 완전 방지)
                Rigidbody rb = playerObj.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
                rb.useGravity = false;
                rb.linearDamping = 10f;        // 높은 감쇠로 드리프트 방지
                rb.angularDamping = 10f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            // 필수 컴포넌트 보장 (프리팹/기존 플레이어에도 적용)
            EnsurePlayerComponents(player.gameObject);

            Debug.Log($"[GameInitializer] 플레이어 생성 완료 - 위치: {spawnPos}");
        }

        /// <summary>
        /// 플레이어에 필요한 컴포넌트가 없으면 추가
        /// </summary>
        private void EnsurePlayerComponents(GameObject playerObj)
        {
            if (playerObj.GetComponent<PlayerAttack>() == null)
                playerObj.AddComponent<PlayerAttack>();

            if (playerObj.GetComponent<Health>() == null)
                playerObj.AddComponent<Health>();

            if (playerObj.GetComponent<PlayerStats>() == null)
                playerObj.AddComponent<PlayerStats>();

            if (playerObj.GetComponent<ExpBarUI>() == null)
                playerObj.AddComponent<ExpBarUI>();

            if (playerObj.GetComponent<LevelUpUI>() == null)
                playerObj.AddComponent<LevelUpUI>();

            if (playerObj.GetComponent<StatUI>() == null)
                playerObj.AddComponent<StatUI>();
        }

        /// <summary>
        /// 카메라 생성
        /// </summary>
        private void CreateCamera()
        {
            // 기존 카메라 찾기
            mainCamera = FindFirstObjectByType<DontStarveCamera>();
            if (mainCamera != null)
            {
                if (player != null)
                {
                    mainCamera.SetTarget(player.transform);
                }
                return;
            }

            // 프리팹으로 생성
            if (cameraPrefab != null)
            {
                GameObject camObj = Instantiate(cameraPrefab);
                camObj.name = "MainCamera";
                mainCamera = camObj.GetComponent<DontStarveCamera>();
            }
            else
            {
                // 기존 Main Camera 찾기
                Camera existingCam = Camera.main;
                if (existingCam != null)
                {
                    mainCamera = existingCam.gameObject.AddComponent<DontStarveCamera>();
                }
                else
                {
                    // 새 카메라 생성
                    GameObject camObj = new GameObject("MainCamera");
                    camObj.tag = "MainCamera";
                    Camera cam = camObj.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
                    mainCamera = camObj.AddComponent<DontStarveCamera>();
                }
            }

            // 타겟 설정
            if (player != null)
            {
                mainCamera.SetTarget(player.transform);
                mainCamera.SnapToTarget();
            }

            Debug.Log("[GameInitializer] 카메라 생성 완료");
        }

        /// <summary>
        /// 플레이어 참조 가져오기
        /// </summary>
        public PlayerController GetPlayer()
        {
            return player;
        }

        /// <summary>
        /// 카메라 참조 가져오기
        /// </summary>
        public DontStarveCamera GetCamera()
        {
            return mainCamera;
        }
    }
}
