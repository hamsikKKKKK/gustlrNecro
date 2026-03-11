using System.Collections.Generic;
using UnityEngine;

namespace Necrocis
{
    /// <summary>
    /// 플레이어 근처에서만 적을 활성화하고 리스폰을 관리하는 스포너.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        private const int SpawnPositionAttempts = 10;
        private const float MinSpawnSpacing = 0.75f;

        private readonly List<EnemyController> activeEnemies = new List<EnemyController>();

        private EnemySpawnRuleConfig config;
        private Transform playerTransform;
        private Transform spawnParent;
        private Vector3 anchorPosition;
        private float nextSpawnTime;
        private bool initializedWave;
        private int enemyPoolArchetypeId;

        public void Configure(EnemySpawnRuleConfig config, Vector3 anchorPosition)
        {
            ClearSpawnedEnemies();

            this.config = config;
            this.anchorPosition = anchorPosition;
            playerTransform = null;
            spawnParent = transform.parent;
            nextSpawnTime = 0f;
            initializedWave = false;
            enemyPoolArchetypeId = EnemyController.GetPoolArchetypeId(config);
            enabled = config != null;
        }

        private void Update()
        {
            if (config == null)
            {
                return;
            }

            CleanupReleasedEnemies();
            EnsurePlayerTransform();
            if (playerTransform == null)
            {
                return;
            }

            float playerDistance = GetPlanarDistance(playerTransform.position, anchorPosition);
            if (playerDistance > config.activationRadius)
            {
                initializedWave = false;
                if (activeEnemies.Count > 0)
                {
                    ClearSpawnedEnemies();
                }
                return;
            }

            if (!initializedWave)
            {
                FillToMaxAlive();
                initializedWave = true;
                nextSpawnTime = Time.time + config.respawnCooldown;
                return;
            }

            if (activeEnemies.Count >= config.maxAlive || Time.time < nextSpawnTime)
            {
                return;
            }

            if (SpawnEnemy())
            {
                nextSpawnTime = Time.time + config.respawnCooldown;
            }
        }

        private void OnDisable()
        {
            if (ShouldAutoReleaseOnLifecycleEvent())
            {
                ReleaseSpawnedEnemies();
            }
        }

        private void OnDestroy()
        {
            if (ShouldAutoReleaseOnLifecycleEvent())
            {
                ReleaseSpawnedEnemies();
            }
        }

        public void ReleaseSpawnedEnemies()
        {
            ClearSpawnedEnemies();
        }

        private bool ShouldAutoReleaseOnLifecycleEvent()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            if (!gameObject.activeSelf)
            {
                return false;
            }

            Transform parent = transform.parent;
            if (parent != null && !parent.gameObject.activeInHierarchy)
            {
                return false;
            }

            return true;
        }

        public void NotifyEnemyReleased(EnemyController enemy)
        {
            activeEnemies.Remove(enemy);
        }

        private void FillToMaxAlive()
        {
            while (activeEnemies.Count < config.maxAlive)
            {
                if (!SpawnEnemy())
                {
                    break;
                }
            }
        }

        private bool SpawnEnemy()
        {
            if (!TryGetSpawnPosition(out Vector3 spawnPosition))
            {
                return false;
            }

            Transform currentSpawnParent = transform.parent != null ? transform.parent : spawnParent;
            if (currentSpawnParent == null)
            {
                return false;
            }

            spawnParent = currentSpawnParent;
            EnemyController controller = EnemyController.Acquire(currentSpawnParent, $"{config.name}_{activeEnemies.Count}", enemyPoolArchetypeId);
            controller.Configure(this, config, anchorPosition, spawnPosition);
            activeEnemies.Add(controller);
            return true;
        }

        private bool TryGetSpawnPosition(out Vector3 spawnPosition)
        {
            BiomeManager biome = BiomeManager.Active;
            if (biome == null)
            {
                spawnPosition = Vector3.zero;
                return false;
            }

            for (int i = 0; i < SpawnPositionAttempts; i++)
            {
                Vector2 offset2D = Random.insideUnitCircle * Mathf.Max(0f, config.spawnRadius);
                Vector3 candidate = anchorPosition + new Vector3(offset2D.x, 0f, offset2D.y);
                Vector2Int grid = biome.WorldToGrid(candidate);
                if (!biome.IsValidPosition(grid.x, grid.y) || !biome.IsWalkable(grid.x, grid.y))
                {
                    continue;
                }

                candidate.y = biome.GetGroundHeight(candidate) + config.heightOffset;
                if (IsTooCloseToSpawnedEnemy(candidate))
                {
                    continue;
                }

                spawnPosition = candidate;
                return true;
            }

            spawnPosition = Vector3.zero;
            return false;
        }

        private bool IsTooCloseToSpawnedEnemy(Vector3 candidate)
        {
            float minDistanceSq = MinSpawnSpacing * MinSpawnSpacing;
            for (int i = 0; i < activeEnemies.Count; i++)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy == null)
                {
                    continue;
                }

                Vector3 delta = enemy.transform.position - candidate;
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistanceSq)
                {
                    return true;
                }
            }

            return false;
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

        private void CleanupReleasedEnemies()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                if (activeEnemies[i] == null)
                {
                    activeEnemies.RemoveAt(i);
                }
            }
        }

        private void ClearSpawnedEnemies()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                EnemyController enemy = activeEnemies[i];
                if (enemy != null)
                {
                    enemy.ReleaseToPool();
                }
            }

            activeEnemies.Clear();
        }

        private static float GetPlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
