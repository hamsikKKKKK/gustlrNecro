using System.Collections.Generic;
using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// FSM 기반 적 AI + 오브젝트 풀링.
    /// 배회(Wander) / 추격(Chase) / 복귀(Return) / 공격(Attack) / 사망(Dead)
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        private const string PoolRootName = "__EnemyPool";

        private static readonly List<EnemyController> ActiveEnemies = new List<EnemyController>();
        private static readonly Dictionary<int, Stack<EnemyController>> PooledEnemies = new Dictionary<int, Stack<EnemyController>>();

        private static Transform poolRoot;

        // 소유자/설정
        private EnemySpawner owner;
        private EnemySpawnRuleConfig config;
        private int poolArchetypeId;

        // 컴포넌트
        private Transform playerTransform;
        private Transform visualRoot;
        private SpriteRenderer spriteRenderer;
        private AnimatedSprite animatedSprite;
        private Billboard billboard;
        private SpriteYSort ySort;
        private Rigidbody body;
        private BoxCollider boxCollider;

        // 이동
        private Vector3 anchorPosition;
        private Vector3 destination;
        private bool hasDestination;

        // FSM
        private IEnemyState currentState;

        // 타이머
        private float idleTimer;
        private float attackTimer;

        // 전투
        private float currentHealth;

        // 애니메이션
        private bool usingMoveAnimation;
        private bool notifiedOwner;

        // ─────────────────────────────────
        // 공개 프로퍼티 (FSM 상태에서 사용)
        // ─────────────────────────────────

        public bool IsDead => currentHealth <= 0f;
        public EnemySpawnRuleConfig Config => config;

        // ─────────────────────────────────
        // 풀링 API (기존 유지)
        // ─────────────────────────────────

        public static EnemyController Acquire(Transform parent, string name, int poolArchetypeId)
        {
            EnsurePoolRoot();
            Stack<EnemyController> pool = GetOrCreatePool(poolArchetypeId);

            while (pool.Count > 0)
            {
                EnemyController pooled = pool.Pop();
                if (pooled == null) continue;

                GameObject pooledObject = pooled.gameObject;
                pooledObject.name = name;
                pooled.poolArchetypeId = poolArchetypeId;
                pooled.transform.SetParent(parent, false);
                pooled.transform.localPosition = Vector3.zero;
                pooled.transform.localRotation = Quaternion.identity;
                pooled.transform.localScale = Vector3.one;
                return pooled;
            }

            GameObject enemyObject = new GameObject(name);
            enemyObject.transform.SetParent(parent, false);
            EnemyController controller = enemyObject.AddComponent<EnemyController>();
            controller.poolArchetypeId = poolArchetypeId;
            return controller;
        }

        public void Configure(EnemySpawner owner, EnemySpawnRuleConfig config, Vector3 anchorPosition, Vector3 spawnPosition)
        {
            this.owner = owner;
            this.config = config;
            poolArchetypeId = GetPoolArchetypeId(config);
            this.anchorPosition = anchorPosition;
            playerTransform = null;
            destination = spawnPosition;
            idleTimer = 0f;
            attackTimer = 0f;
            hasDestination = false;
            usingMoveAnimation = false;
            notifiedOwner = false;
            currentHealth = config != null ? config.maxHealth : 30f;

            transform.position = spawnPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            EnsureComponents();
            ApplyPhysicsSetup();
            ApplyVisualSetup();
            SetIdleAnimation();
            SyncHeight();

            if (!ActiveEnemies.Contains(this))
            {
                ActiveEnemies.Add(this);
            }

            enabled = config != null;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // FSM 시작 → Idle
            currentState = null;
            ChangeState(EnemyIdleState.Instance);
        }

        public void ReleaseToPool()
        {
            if (gameObject == null || !gameObject.activeSelf) return;

            // FSM Exit
            currentState?.Exit(this);
            currentState = null;

            PrepareForPool();
            EnsurePoolRoot();
            gameObject.SetActive(false);
            transform.SetParent(poolRoot, false);

            owner = null;
            config = null;
            playerTransform = null;
            destination = Vector3.zero;
            idleTimer = 0f;
            attackTimer = 0f;
            hasDestination = false;
            usingMoveAnimation = false;

            GetOrCreatePool(poolArchetypeId).Push(this);
        }

        // ─────────────────────────────────
        // Unity 라이프사이클
        // ─────────────────────────────────

        private void Update()
        {
            if (config == null) return;

            EnsurePlayerTransform();

            // FSM Update
            currentState?.Update(this, Time.deltaTime);

            SyncHeight();
        }

        private void OnDisable()
        {
            ActiveEnemies.Remove(this);
            NotifyOwnerReleased();
        }

        private void OnDestroy()
        {
            ActiveEnemies.Remove(this);
            NotifyOwnerReleased();
        }

        // ─────────────────────────────────
        // FSM 상태 전환
        // ─────────────────────────────────

        public void ChangeState(IEnemyState newState)
        {
            if (newState == currentState) return;

            currentState?.Exit(this);
            currentState = newState;
            currentState?.Enter(this);
        }

        // ─────────────────────────────────
        // 조건 검사 (FSM 상태에서 호출)
        // ─────────────────────────────────

        public bool IsPlayerInChaseRange()
        {
            if (playerTransform == null) return false;

            float distToPlayer = GetPlanarDistance(GetCurrentPosition(), playerTransform.position);
            if (distToPlayer > config.chaseRadius) return false;

            // leash 안에 있는 플레이어만 추격
            float playerToAnchor = GetPlanarDistance(playerTransform.position, anchorPosition);
            return playerToAnchor <= config.leashRadius;
        }

        public bool IsPlayerInAttackRange()
        {
            if (playerTransform == null) return false;
            float dist = GetPlanarDistance(GetCurrentPosition(), playerTransform.position);
            return dist <= config.attackRange;
        }

        public bool IsOutOfLeash()
        {
            return GetPlanarDistance(GetCurrentPosition(), anchorPosition) > config.leashRadius;
        }

        public bool IsIdleTimerExpired(float deltaTime)
        {
            idleTimer -= deltaTime;
            return idleTimer <= 0f;
        }

        // ─────────────────────────────────
        // 행동 (FSM 상태에서 호출)
        // ─────────────────────────────────

        public void ResetIdleTimer()
        {
            float min = Mathf.Min(config.idleDelayRange.x, config.idleDelayRange.y);
            float max = Mathf.Max(config.idleDelayRange.x, config.idleDelayRange.y);
            idleTimer = Random.Range(min, max);
        }

        public void PickWanderDestination()
        {
            if (TryPickWanderDestination(out Vector3 wanderDest))
            {
                SetDestination(wanderDest);
            }
        }

        public void SetChaseDestination()
        {
            if (playerTransform == null) return;
            Vector3 chase = playerTransform.position;
            chase.y = GetCurrentPosition().y;
            SetDestination(chase);
        }

        public void SetReturnDestination()
        {
            SetDestination(anchorPosition);
        }

        /// <summary>
        /// 목적지로 이동. 도착하면 false 반환.
        /// </summary>
        public bool MoveTowardDestination(float deltaTime)
        {
            if (!hasDestination) return false;

            Vector3 currentPosition = GetCurrentPosition();
            Vector3 separation = GetSeparationVector(currentPosition);

            Vector3 flatCurrent = new Vector3(currentPosition.x, 0f, currentPosition.z);
            Vector3 flatDestination = new Vector3(destination.x, 0f, destination.z);
            Vector3 toDestination = flatDestination - flatCurrent;
            float distance = toDestination.magnitude;

            if (distance <= Mathf.Max(0.01f, config.stoppingDistance))
            {
                hasDestination = false;
                return false; // 도착
            }

            Vector3 moveDirection = toDestination.normalized;
            if (separation.sqrMagnitude > 0.0001f)
            {
                Vector3 combined = moveDirection + separation * config.separationStrength;
                if (combined.sqrMagnitude > 0.0001f)
                {
                    moveDirection = combined.normalized;
                }
            }

            Vector3 step = moveDirection * config.moveSpeed * deltaTime;
            if (step.sqrMagnitude > toDestination.sqrMagnitude)
            {
                step = toDestination;
            }

            bool moved = TryMove(currentPosition, step);
            if (!moved)
            {
                hasDestination = false;
                return false;
            }

            if (spriteRenderer != null && Mathf.Abs(step.x) > 0.001f)
            {
                spriteRenderer.flipX = step.x < 0f;
            }

            // 이동 애니메이션 전환
            if (!usingMoveAnimation)
            {
                SetMoveAnimation();
            }

            return true; // 아직 이동 중
        }

        public void TryPerformAttack(float deltaTime)
        {
            attackTimer -= deltaTime;
            if (attackTimer > 0f) return;

            attackTimer = config.attackCooldown;

            // TODO: 플레이어 데미지 시스템 연동
            // PlayerController.Instance?.TakeDamage(config.attackDamage);
            Debug.Log($"[{gameObject.name}] 공격! {config.attackDamage} 데미지");
        }

        public void TakeDamage(float damage)
        {
            if (IsDead) return;
            currentHealth -= damage;
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                ChangeState(EnemyDeadState.Instance);
            }
        }

        public void DisableCollider()
        {
            if (boxCollider != null) boxCollider.enabled = false;
        }

        // ─────────────────────────────────
        // 애니메이션
        // ─────────────────────────────────

        public void SetIdleAnimation()
        {
            usingMoveAnimation = false;
            ApplyAnimation(GetIdleFrames());
        }

        public void SetMoveAnimation()
        {
            usingMoveAnimation = true;
            ApplyAnimation(config.moveSprites);
        }

        // ─────────────────────────────────
        // 내부 메서드 (기존 로직 유지)
        // ─────────────────────────────────

        private void SetDestination(Vector3 targetPosition)
        {
            destination = targetPosition;
            hasDestination = true;
        }

        private bool TryPickWanderDestination(out Vector3 wanderDestination)
        {
            BiomeManager biome = BiomeManager.Active;
            if (biome == null)
            {
                wanderDestination = anchorPosition;
                return true;
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = Random.insideUnitCircle * Mathf.Max(0f, config.wanderRadius);
                Vector3 candidate = anchorPosition + new Vector3(offset.x, 0f, offset.y);
                Vector2Int grid = biome.WorldToGrid(candidate);
                if (!biome.IsValidPosition(grid.x, grid.y) || !biome.IsWalkable(grid.x, grid.y))
                    continue;

                candidate.y = biome.GetGroundHeight(candidate) + config.heightOffset;
                wanderDestination = candidate;
                return true;
            }

            wanderDestination = anchorPosition;
            wanderDestination.y = biome.GetGroundHeight(anchorPosition) + config.heightOffset;
            return true;
        }

        private bool TryMove(Vector3 currentPosition, Vector3 step)
        {
            BiomeManager biome = BiomeManager.Active;
            if (biome == null)
            {
                MoveToPosition(currentPosition + step);
                return true;
            }

            Vector3 targetPosition = currentPosition + step;
            if (biome.CanMove(currentPosition, targetPosition))
            {
                MoveToPosition(targetPosition);
                return true;
            }

            Vector3 moveX = new Vector3(step.x, 0f, 0f);
            Vector3 moveZ = new Vector3(0f, 0f, step.z);

            if (Mathf.Abs(step.x) >= Mathf.Abs(step.z))
            {
                if (moveX.sqrMagnitude > 0f && biome.CanMove(currentPosition, currentPosition + moveX))
                {
                    MoveToPosition(currentPosition + moveX);
                    return true;
                }
                if (moveZ.sqrMagnitude > 0f && biome.CanMove(currentPosition, currentPosition + moveZ))
                {
                    MoveToPosition(currentPosition + moveZ);
                    return true;
                }
            }
            else
            {
                if (moveZ.sqrMagnitude > 0f && biome.CanMove(currentPosition, currentPosition + moveZ))
                {
                    MoveToPosition(currentPosition + moveZ);
                    return true;
                }
                if (moveX.sqrMagnitude > 0f && biome.CanMove(currentPosition, currentPosition + moveX))
                {
                    MoveToPosition(currentPosition + moveX);
                    return true;
                }
            }

            return false;
        }

        private Vector3 GetSeparationVector(Vector3 currentPosition)
        {
            if (config == null || config.separationDistance <= 0f)
                return Vector3.zero;

            float maxDistanceSq = config.separationDistance * config.separationDistance;
            Vector3 separation = Vector3.zero;

            for (int i = 0; i < ActiveEnemies.Count; i++)
            {
                EnemyController other = ActiveEnemies[i];
                if (other == null || other == this || other.config == null) continue;

                Vector3 delta = currentPosition - other.GetCurrentPosition();
                delta.y = 0f;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq <= 0.0001f || distanceSq > maxDistanceSq) continue;

                float distance = Mathf.Sqrt(distanceSq);
                float weight = 1f - (distance / config.separationDistance);
                separation += delta.normalized * weight;
            }

            return separation;
        }

        private void ApplyAnimation(Sprite[] frames)
        {
            if (spriteRenderer == null) return;

            if (frames == null || frames.Length == 0)
            {
                animatedSprite.Stop();
                animatedSprite.enabled = false;
                return;
            }

            if (frames.Length == 1)
            {
                animatedSprite.Stop();
                animatedSprite.enabled = false;
                spriteRenderer.sprite = frames[0];
                return;
            }

            animatedSprite.enabled = true;
            animatedSprite.SetFrames(frames, config.animationSpeed);
            animatedSprite.Play();
        }

        private Sprite[] GetIdleFrames()
        {
            if (config.idleSprites != null && config.idleSprites.Length > 0)
                return config.idleSprites;
            return config.moveSprites;
        }

        private void SyncHeight()
        {
            if (config == null) return;
            BiomeManager biome = BiomeManager.Active;
            if (biome == null) return;

            Vector3 position = GetCurrentPosition();
            position.y = biome.GetGroundHeight(position) + config.heightOffset;
            SetPosition(position);
        }

        private void EnsurePlayerTransform()
        {
            if (playerTransform != null) return;
            if (PlayerController.Instance != null)
                playerTransform = PlayerController.Instance.transform;
        }

        private void EnsureComponents()
        {
            if (visualRoot == null)
            {
                Transform child = transform.Find("Visual");
                if (child == null)
                {
                    GameObject visualObject = new GameObject("Visual");
                    child = visualObject.transform;
                    child.SetParent(transform, false);
                }
                visualRoot = child;
            }

            spriteRenderer = GetOrAddComponent<SpriteRenderer>(visualRoot.gameObject);
            animatedSprite = GetOrAddComponent<AnimatedSprite>(visualRoot.gameObject);
            billboard = GetOrAddComponent<Billboard>(visualRoot.gameObject);
            ySort = GetOrAddComponent<SpriteYSort>(visualRoot.gameObject);
            body = GetOrAddComponent<Rigidbody>(gameObject);
            boxCollider = GetOrAddComponent<BoxCollider>(gameObject);
        }

        private void ApplyVisualSetup()
        {
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = config.scale;
            spriteRenderer.sortingOrder = config.sortingOrder;
            spriteRenderer.flipX = false;

            billboard.enabled = config.useBillboard;
            if (config.useBillboard)
            {
                billboard.ResetBaseLocalPosition(Vector3.zero);
                billboard.SetUpdateMode(Billboard.UpdateMode.Continuous);
            }

            ySort.enabled = config.useYSort;
            if (config.useYSort)
            {
                ySort.Configure(SpriteYSort.WorldDynamicBaseSortingOrder, true, SpriteYSort.WorldDynamicMinSortingOrder);
                ySort.SetUpdateMode(SpriteYSort.UpdateMode.Continuous);
            }
        }

        private void ApplyPhysicsSetup()
        {
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            boxCollider.enabled = config.addCollider;
            boxCollider.isTrigger = config.isTrigger;
            boxCollider.size = config.colliderSize;
            boxCollider.center = config.colliderCenter;
        }

        private Vector3 GetCurrentPosition()
        {
            return body != null ? body.position : transform.position;
        }

        private void MoveToPosition(Vector3 position)
        {
            if (body != null)
            {
                body.position = position;
                return;
            }
            transform.position = position;
        }

        private void SetPosition(Vector3 position)
        {
            if (body != null)
            {
                body.position = position;
                return;
            }
            transform.position = position;
        }

        private T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component == null)
                component = target.AddComponent<T>();
            return component;
        }

        private void NotifyOwnerReleased()
        {
            if (notifiedOwner || owner == null) return;
            owner.NotifyEnemyReleased(this);
            notifiedOwner = true;
        }

        private void PrepareForPool()
        {
            if (animatedSprite != null)
            {
                animatedSprite.Stop();
                animatedSprite.enabled = false;
            }

            if (spriteRenderer != null)
                spriteRenderer.flipX = false;

            if (body != null)
            {
                if (!body.isKinematic)
                    body.angularVelocity = Vector3.zero;
                body.rotation = Quaternion.identity;
            }

            // 콜라이더 복원
            if (boxCollider != null && config != null)
                boxCollider.enabled = config.addCollider;
        }

        private static float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static void EnsurePoolRoot()
        {
            if (poolRoot != null) return;
            GameObject root = GameObject.Find(PoolRootName);
            if (root == null) root = new GameObject(PoolRootName);
            poolRoot = root.transform;
        }

        public static int GetPoolArchetypeId(EnemySpawnRuleConfig config)
        {
            if (config == null) return 0;
            return unchecked((config.poissonSalt * 397) ^ Animator.StringToHash(config.name ?? "Enemy"));
        }

        private static Stack<EnemyController> GetOrCreatePool(int poolArchetypeId)
        {
            if (!PooledEnemies.TryGetValue(poolArchetypeId, out Stack<EnemyController> pool))
            {
                pool = new Stack<EnemyController>();
                PooledEnemies.Add(poolArchetypeId, pool);
            }
            return pool;
        }
    }
}
