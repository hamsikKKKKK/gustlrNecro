using UnityEngine;
using UnityEngine.InputSystem;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 이동 + 방향별 스프라이트 애니메이션
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("이동")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("스프라이트 렌더러")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("대기 애니메이션")]
        [SerializeField] private Sprite[] idleSprites;  // 대기 애니메이션 (1세트)

        [Header("이동 애니메이션 (방향별)")]
        [SerializeField] private Sprite[] walkDownSprites;
        [SerializeField] private Sprite[] walkUpSprites;
        [SerializeField] private Sprite[] walkLeftSprites;
        [SerializeField] private Sprite[] walkRightSprites;

        [Header("애니메이션 설정")]
        [SerializeField] private float idleFrameRate = 4f;   // 대기 애니메이션 속도
        [SerializeField] private float walkFrameRate = 8f;   // 이동 애니메이션 속도

        [Header("위치 고정")]
        [SerializeField] private bool lockYPosition = false;
        [SerializeField] private float lockedY = -2f;

        // 방향
        public enum Direction { Down, Up, Left, Right }
        private Direction currentDirection = Direction.Down;
        private bool isMoving = false;

        // 애니메이션
        private Sprite[] currentAnimation;
        private int currentFrame = 0;
        private float frameTimer = 0f;
        private float currentFrameRate;

        // 이동
        private Vector3 movement;
        private Rigidbody rb;
        private CharacterController characterController;

        private void Awake()
        {
            // 싱글톤 패턴
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 스프라이트 렌더러 찾기
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
                if (spriteRenderer == null)
                {
                    GameObject spriteObj = new GameObject("Sprite");
                    spriteObj.transform.SetParent(transform);
                    spriteObj.transform.localPosition = Vector3.zero;
                    spriteRenderer = spriteObj.AddComponent<SpriteRenderer>();
                }
            }

            // 스프라이트 기본 설정 (SpriteYSort가 동적으로 관리)
            spriteRenderer.color = Color.white;

            // 빌보드 추가
            Billboard billboard = spriteRenderer.GetComponent<Billboard>();
            if (billboard == null)
            {
                spriteRenderer.gameObject.AddComponent<Billboard>();
            }

            // Y 정렬 추가 (앞뒤 순서)
            SpriteYSort ySort = spriteRenderer.GetComponent<SpriteYSort>();
            if (ySort == null)
            {
                ySort = spriteRenderer.gameObject.AddComponent<SpriteYSort>();
            }

            // 물리 컴포넌트 확인
            rb = GetComponent<Rigidbody>();
            characterController = GetComponent<CharacterController>();
        }

        private void Start()
        {
            // 태그 설정
            gameObject.tag = "Player";

            // Y 위치 강제 (바닥 위)
            Vector3 pos = transform.position;
            pos.y = 0f;
            transform.position = pos;

            // 초기 애니메이션 (대기)
            SetAnimation(idleSprites, idleFrameRate);

            Debug.Log($"[Player] 시작 위치: {transform.position}");
        }

        private void Update()
        {
            HandleInput();
            UpdateAnimation();
        }

        private void FixedUpdate()
        {
            Move();
            ApplyLockedY();
        }

        /// <summary>
        /// 입력 처리
        /// </summary>
        private void HandleInput()
        {
            // 포커스 없거나 게임 시작 직후면 입력 무시
            if (!Application.isFocused || Time.timeSinceLevelLoad < 0.5f)
            {
                movement = Vector3.zero;
                isMoving = false;
                return;
            }

            // 새 Input System 사용
            float horizontal = 0f;
            float vertical = 0f;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // 각 키를 명시적으로 체크 (wasUpdatedThisFrame으로 실제 입력인지 확인)
                bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
                bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
                bool down = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
                bool up = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;

                if (left && !right) horizontal = -1f;
                else if (right && !left) horizontal = 1f;

                if (down && !up) vertical = -1f;
                else if (up && !down) vertical = 1f;
            }

            movement = new Vector3(horizontal, 0, vertical).normalized;
            isMoving = movement.sqrMagnitude > 0.01f;

            // 방향 결정 (마지막 입력 방향 유지)
            if (isMoving)
            {
                UpdateDirection(horizontal, vertical);
            }

            // 애니메이션 변경
            UpdateAnimationState();
        }

        /// <summary>
        /// 방향 업데이트
        /// </summary>
        private void UpdateDirection(float h, float v)
        {
            // 수직 우선
            if (Mathf.Abs(v) >= Mathf.Abs(h))
            {
                currentDirection = v > 0 ? Direction.Up : Direction.Down;
            }
            else
            {
                currentDirection = h > 0 ? Direction.Right : Direction.Left;
            }
        }

        /// <summary>
        /// 애니메이션 상태 업데이트
        /// </summary>
        private void UpdateAnimationState()
        {
            Sprite[] newAnimation;
            float newFrameRate;

            if (!isMoving)
            {
                // 대기 애니메이션 (하나만 사용)
                newFrameRate = idleFrameRate;
                newAnimation = idleSprites;
            }
            else
            {
                // 이동 애니메이션
                newFrameRate = walkFrameRate;
                switch (currentDirection)
                {
                    case Direction.Down:
                        newAnimation = walkDownSprites;
                        break;
                    case Direction.Up:
                        newAnimation = walkUpSprites;
                        break;
                    case Direction.Left:
                        newAnimation = walkLeftSprites;
                        break;
                    case Direction.Right:
                        newAnimation = walkRightSprites;
                        break;
                    default:
                        newAnimation = walkDownSprites;
                        break;
                }
            }

            // 애니메이션 변경 시 리셋
            if (newAnimation != currentAnimation)
            {
                SetAnimation(newAnimation, newFrameRate);
            }
        }

        /// <summary>
        /// 애니메이션 설정
        /// </summary>
        private void SetAnimation(Sprite[] sprites, float frameRate)
        {
            currentAnimation = sprites;
            currentFrameRate = frameRate;
            currentFrame = 0;
            frameTimer = 0f;

            // 첫 프레임 즉시 적용
            if (currentAnimation != null && currentAnimation.Length > 0)
            {
                spriteRenderer.sprite = currentAnimation[0];
            }
        }

        /// <summary>
        /// 애니메이션 프레임 업데이트
        /// </summary>
        private void UpdateAnimation()
        {
            if (currentAnimation == null || currentAnimation.Length == 0) return;

            frameTimer += Time.deltaTime;
            float frameDuration = 1f / currentFrameRate;

            if (frameTimer >= frameDuration)
            {
                frameTimer -= frameDuration;
                currentFrame = (currentFrame + 1) % currentAnimation.Length;

                if (spriteRenderer != null && currentFrame < currentAnimation.Length)
                {
                    spriteRenderer.sprite = currentAnimation[currentFrame];
                }
            }
        }

        /// <summary>
        /// 이동 처리
        /// </summary>
        private void Move()
        {
            if (characterController != null)
            {
                if (isMoving)
                {
                    Vector3 moveVector = movement * moveSpeed * Time.fixedDeltaTime;
                    characterController.Move(moveVector);
                }
            }
            else if (rb != null)
            {
                if (isMoving)
                {
                    Vector3 moveVector = movement * moveSpeed * Time.fixedDeltaTime;
                    rb.MovePosition(rb.position + moveVector);
                }
                else
                {
                    // 이동 안 할 때 속도 제거 (드리프트 방지)
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            else
            {
                if (isMoving)
                {
                    Vector3 moveVector = movement * moveSpeed * Time.fixedDeltaTime;
                    transform.position += moveVector;
                }
            }
        }

        /// <summary>
        /// 스폰 위치로 이동
        /// </summary>
        public void SpawnAt(Vector3 position)
        {
            transform.position = position;

            if (characterController != null)
            {
                characterController.enabled = false;
                transform.position = position;
                characterController.enabled = true;
            }

            ApplyLockedY();
        }

        public void LockY(float y)
        {
            lockYPosition = true;
            lockedY = y;
            ApplyLockedY();
        }

        public void UnlockY()
        {
            lockYPosition = false;
        }

        private void ApplyLockedY()
        {
            if (!lockYPosition) return;

            if (characterController != null)
            {
                Vector3 pos = transform.position;
                pos.y = lockedY;
                transform.position = pos;
                return;
            }

            if (rb != null)
            {
                Vector3 pos = rb.position;
                pos.y = lockedY;
                rb.position = pos;

                Vector3 vel = rb.linearVelocity;
                vel.y = 0f;
                rb.linearVelocity = vel;
                return;
            }

            Vector3 fallback = transform.position;
            fallback.y = lockedY;
            transform.position = fallback;
        }

        /// <summary>
        /// 현재 방향 가져오기
        /// </summary>
        public Direction GetCurrentDirection()
        {
            return currentDirection;
        }
    }
}
