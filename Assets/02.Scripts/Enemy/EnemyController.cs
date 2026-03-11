using System.Collections.Generic;
using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 스포너 기준 반경 안에서 배회/추적/복귀하는 1차 적 AI.
    /// </summary>
    public class EnemyController : MonoBehaviour
    {
        private const string PoolRootName = "__EnemyPool";

        private static readonly List<EnemyController> ActiveEnemies = new List<EnemyController>();
        private static readonly Dictionary<int, Stack<EnemyController>> PooledEnemies = new Dictionary<int, Stack<EnemyController>>();

        private static Transform poolRoot;

        private EnemySpawner owner;
        private EnemySpawnRuleConfig config;
        private Transform playerTransform;
        private Transform visualRoot;
        private SpriteRenderer spriteRenderer;
        private AnimatedSprite animatedSprite;
        private Billboard billboard;
        private SpriteYSort ySort;
        private Rigidbody body;
        private BoxCollider boxCollider;
        private Vector3 anchorPosition;
        private Vector3 destination;
        private float idleTimer;
        private bool hasDestination;
        private bool usingMoveAnimation;
        private bool notifiedOwner;
        private int poolArchetypeId;

        public static EnemyController Acquire(Transform parent, string name, int poolArchetypeId)
        {
            EnsurePoolRoot();
            Stack<EnemyController> pool = GetOrCreatePool(poolArchetypeId);

            while (pool.Count > 0)
            {
                EnemyController pooled = pool.Pop();
                if (pooled == null)
                {
                    continue;
                }

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
            hasDestination = false;
            usingMoveAnimation = false;
            notifiedOwner = false;

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
        }

        public void ReleaseToPool()
        {
            if (gameObject == null || !gameObject.activeSelf)
            {
                return;
            }

            PrepareForPool();
            EnsurePoolRoot();
            gameObject.SetActive(false);
            transform.SetParent(poolRoot, false);

            owner = null;
            config = null;
            playerTransform = null;
            destination = Vector3.zero;
            idleTimer = 0f;
            hasDestination = false;
            usingMoveAnimation = false;

            GetOrCreatePool(poolArchetypeId).Push(this);
        }

        private void Update()
        {
            if (config == null)
            {
                return;
            }

            EnsurePlayerTransform();
            bool moved = UpdateMovement(Time.deltaTime);
            UpdateAnimation(moved);
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

        private bool UpdateMovement(float deltaTime)
        {
            Vector3 currentPosition = GetCurrentPosition();
            Vector3 separation = GetSeparationVector(currentPosition);

            if (GetPlanarDistance(currentPosition, anchorPosition) > config.leashRadius)
            {
                SetDestination(anchorPosition);
                return MoveTowardDestination(deltaTime, separation);
            }

            if (TryGetChaseDestination(out Vector3 chaseDestination))
            {
                SetDestination(chaseDestination);
                return MoveTowardDestination(deltaTime, separation);
            }

            if (hasDestination)
            {
                return MoveTowardDestination(deltaTime, separation);
            }

            if (separation.sqrMagnitude > 0.0001f)
            {
                Vector3 step = separation.normalized * config.moveSpeed * 0.5f * deltaTime;
                return TryMove(currentPosition, step);
            }

            if (idleTimer > 0f)
            {
                idleTimer -= deltaTime;
                return false;
            }

            if (!TryPickWanderDestination(out Vector3 wanderDestination))
            {
                idleTimer = GetRandomIdleDelay();
                return false;
            }

            SetDestination(wanderDestination);
            return MoveTowardDestination(deltaTime, separation);
        }

        private bool TryGetChaseDestination(out Vector3 chaseDestination)
        {
            chaseDestination = Vector3.zero;
            if (playerTransform == null)
            {
                return false;
            }

            float distanceToPlayer = GetPlanarDistance(GetCurrentPosition(), playerTransform.position);
            if (distanceToPlayer > config.chaseRadius)
            {
                return false;
            }

            float playerToAnchor = GetPlanarDistance(playerTransform.position, anchorPosition);
            if (playerToAnchor > config.leashRadius)
            {
                return false;
            }

            chaseDestination = playerTransform.position;
            chaseDestination.y = GetCurrentPosition().y;
            return true;
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
                {
                    continue;
                }

                candidate.y = biome.GetGroundHeight(candidate) + config.heightOffset;
                wanderDestination = candidate;
                return true;
            }

            wanderDestination = anchorPosition;
            wanderDestination.y = biome.GetGroundHeight(anchorPosition) + config.heightOffset;
            return true;
        }

        private bool MoveTowardDestination(float deltaTime, Vector3 separation)
        {
            Vector3 currentPosition = GetCurrentPosition();
            Vector3 flatCurrent = new Vector3(currentPosition.x, 0f, currentPosition.z);
            Vector3 flatDestination = new Vector3(destination.x, 0f, destination.z);
            Vector3 toDestination = flatDestination - flatCurrent;
            float distance = toDestination.magnitude;

            if (distance <= Mathf.Max(0.01f, config.stoppingDistance))
            {
                hasDestination = false;
                idleTimer = GetRandomIdleDelay();
                return false;
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
                idleTimer = GetRandomIdleDelay();
                return false;
            }

            if (spriteRenderer != null && Mathf.Abs(step.x) > 0.001f)
            {
                spriteRenderer.flipX = step.x < 0f;
            }

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
            {
                return Vector3.zero;
            }

            float maxDistanceSq = config.separationDistance * config.separationDistance;
            Vector3 separation = Vector3.zero;

            for (int i = 0; i < ActiveEnemies.Count; i++)
            {
                EnemyController other = ActiveEnemies[i];
                if (other == null || other == this || other.config == null)
                {
                    continue;
                }

                Vector3 delta = currentPosition - other.GetCurrentPosition();
                delta.y = 0f;
                float distanceSq = delta.sqrMagnitude;
                if (distanceSq <= 0.0001f || distanceSq > maxDistanceSq)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSq);
                float weight = 1f - (distance / config.separationDistance);
                separation += delta.normalized * weight;
            }

            return separation;
        }

        private void UpdateAnimation(bool moved)
        {
            if (moved)
            {
                if (!usingMoveAnimation)
                {
                    SetMoveAnimation();
                }
                return;
            }

            if (usingMoveAnimation)
            {
                SetIdleAnimation();
            }
        }

        private void SetDestination(Vector3 targetPosition)
        {
            destination = targetPosition;
            hasDestination = true;
        }

        private void SetIdleAnimation()
        {
            usingMoveAnimation = false;
            ApplyAnimation(GetIdleFrames());
        }

        private void SetMoveAnimation()
        {
            usingMoveAnimation = true;
            ApplyAnimation(config.moveSprites);
        }

        private void ApplyAnimation(Sprite[] frames)
        {
            if (spriteRenderer == null)
            {
                return;
            }

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
            {
                return config.idleSprites;
            }

            return config.moveSprites;
        }

        private void SyncHeight()
        {
            if (config == null)
            {
                return;
            }

            BiomeManager biome = BiomeManager.Active;
            if (biome == null)
            {
                return;
            }

            Vector3 position = GetCurrentPosition();
            position.y = biome.GetGroundHeight(position) + config.heightOffset;
            SetPosition(position);
        }

        private void EnsurePlayerTransform()
        {
            if (playerTransform != null)
            {
                return;
            }

            if (PlayerController.Instance != null)
            {
                playerTransform = PlayerController.Instance.transform;
            }
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
            {
                component = target.AddComponent<T>();
            }
            return component;
        }

        private float GetRandomIdleDelay()
        {
            float min = Mathf.Min(config.idleDelayRange.x, config.idleDelayRange.y);
            float max = Mathf.Max(config.idleDelayRange.x, config.idleDelayRange.y);
            return Random.Range(min, max);
        }

        private void NotifyOwnerReleased()
        {
            if (notifiedOwner || owner == null)
            {
                return;
            }

            owner.NotifyEnemyReleased(this);
            notifiedOwner = true;
        }

        private static float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void PrepareForPool()
        {
            if (animatedSprite != null)
            {
                animatedSprite.Stop();
                animatedSprite.enabled = false;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }

            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.angularVelocity = Vector3.zero;
                }
                body.rotation = Quaternion.identity;
            }
        }

        private static void EnsurePoolRoot()
        {
            if (poolRoot != null)
            {
                return;
            }

            GameObject root = GameObject.Find(PoolRootName);
            if (root == null)
            {
                root = new GameObject(PoolRootName);
            }

            poolRoot = root.transform;
        }

        public static int GetPoolArchetypeId(EnemySpawnRuleConfig config)
        {
            if (config == null)
            {
                return 0;
            }

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
