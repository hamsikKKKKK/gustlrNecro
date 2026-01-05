using UnityEngine;
using UnityEngine.InputSystem;

namespace Necrocis
{
    /// <summary>
    /// 돈스타브 스타일 2.5D 카메라
    /// - 탑다운 + 약간 기울어진 시점
    /// - 플레이어 추적
    /// </summary>
    public class DontStarveCamera : MonoBehaviour
    {
        public static DontStarveCamera Instance { get; private set; }

        [Header("타겟")]
        [SerializeField] private Transform target;

        [Header("카메라 설정")]
        [SerializeField] private bool useOrthographic = true;  // Orthographic 사용 (돈스타브 스타일)
        [SerializeField] private float height = 10f;           // 카메라 높이
        [SerializeField] private float distance = 5f;          // 뒤로 떨어진 거리
        [SerializeField] private float angle = 45f;            // 내려다보는 각도
        [SerializeField] private float smoothSpeed = 5f;       // 부드러운 이동

        [Header("줌 (Orthographic = Size, Perspective = Height)")]
        [SerializeField] private float orthoSize = 5f;         // Orthographic 크기
        [SerializeField] private float zoomSpeed = 1f;
        [SerializeField] private float minZoom = 3f;
        [SerializeField] private float maxZoom = 10f;

        private Camera cam;
        private Vector3 offset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);  // 씬 전환해도 유지

            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = gameObject.AddComponent<Camera>();
            }
        }

        private void Start()
        {
            // 타겟 없으면 Player 태그로 찾기
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }

            CalculateOffset();
            SetupCamera();

            // 카메라 배경색 설정 (어두운 붉은색 - 내장 테마)
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.15f, 0.05f, 0.05f);  // 어두운 붉은색
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // 줌 처리
            HandleZoom();

            // 부드러운 추적
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;
        }

        /// <summary>
        /// 오프셋 계산
        /// </summary>
        private void CalculateOffset()
        {
            // 각도에 따른 오프셋 계산
            float radian = angle * Mathf.Deg2Rad;
            offset = new Vector3(0, height, -distance);
        }

        /// <summary>
        /// 카메라 초기 설정
        /// </summary>
        private void SetupCamera()
        {
            // Orthographic / Perspective 설정
            if (cam != null)
            {
                cam.orthographic = useOrthographic;
                if (useOrthographic)
                {
                    cam.orthographicSize = orthoSize;
                }
            }

            // 카메라 각도 설정
            transform.rotation = Quaternion.Euler(angle, 0, 0);

            // 초기 위치
            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }

        /// <summary>
        /// 줌 처리
        /// </summary>
        private void HandleZoom()
        {
            // 새 Input System 사용
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            scroll *= 0.01f; // 스크롤 값 정규화

            if (Mathf.Abs(scroll) > 0.001f)
            {
                if (useOrthographic && cam != null)
                {
                    // Orthographic: Size 조절
                    orthoSize -= scroll * zoomSpeed;
                    orthoSize = Mathf.Clamp(orthoSize, minZoom, maxZoom);
                    cam.orthographicSize = orthoSize;
                }
                else
                {
                    // Perspective: 높이 조절
                    height -= scroll * zoomSpeed;
                    height = Mathf.Clamp(height, minZoom, maxZoom);
                    distance = height * 0.5f;
                    CalculateOffset();
                }
            }
        }

        /// <summary>
        /// 타겟 설정
        /// </summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>
        /// 즉시 타겟 위치로 이동
        /// </summary>
        public void SnapToTarget()
        {
            if (target != null)
            {
                transform.position = target.position + offset;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (target != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, target.position);
                Gizmos.DrawWireSphere(target.position, 0.5f);
            }
        }
#endif
    }
}
