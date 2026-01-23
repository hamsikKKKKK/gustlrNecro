using UnityEngine;
using System.Collections.Generic;

namespace Necrocis
{
    /// <summary>
    /// Voronoi/Perlin 기반 지역 + Poisson 배치 공통 로직.
    /// </summary>
    public abstract class RegionPoissonBiomeManager : BiomeManager
    {
        [Header("=== Regions ===")]
        [SerializeField] protected int regionCount = 3;
        [SerializeField] protected float regionCellSize = 20f;
        [SerializeField] protected float regionBlendWidth = 3f;

        [Header("=== Height ===")]
        [SerializeField] protected float heightNoiseScale = 0.02f;
        [SerializeField] protected float heightNoiseAmplitude = 0.45f;

        protected PerlinNoise heightNoise;
        protected readonly List<ObjectRule> objectRules = new List<ObjectRule>();

        private readonly Dictionary<Vector2Int, ChunkCache> chunkCaches = new Dictionary<Vector2Int, ChunkCache>();

        private const int RegionCellJitterSalt = 501;
        private const int RegionTypeSalt = 777;
        private const int RegionBlendSalt = 888;

        protected struct RegionSample
        {
            public int primary;
            public int secondary;
            public float blend;

            public RegionSample(int primary, int secondary, float blend)
            {
                this.primary = primary;
                this.secondary = secondary;
                this.blend = blend;
            }
        }

        protected struct ObjectRule
        {
            public BiomeObjectKind kind;
            public float density;
            public float minDistance;
            public bool blocksMovement;
            public int regionMask;
            public int salt;
            public int configIndex;
        }

        private sealed class ChunkCache
        {
            public int[] regionTypes;
            public bool[] regionValid;
            public int[] heightLevels;
            public bool[] heightValid;

            public ChunkCache(int tileCount)
            {
                regionTypes = new int[tileCount];
                regionValid = new bool[tileCount];
                heightLevels = new int[tileCount];
                heightValid = new bool[tileCount];
            }
        }

        protected override void Awake()
        {
            base.Awake();
            InitializeNoise();
            BuildObjectRules();
        }

        protected virtual void InitializeNoise()
        {
            heightNoise = new PerlinNoise(seed + 97);
            heightNoise.SetFrequency(heightNoiseScale);
        }

        protected abstract void BuildObjectRules();

        protected override int GetBaseHeightLevel(int worldX, int worldY)
        {
            return GetHeightLevelCached(worldX, worldY);
        }

        protected override void GenerateObjectsForChunk(Chunk chunk)
        {
            var enumerator = GenerateObjectsInternal(chunk);
            while (enumerator.MoveNext())
            {
            }
        }

        protected override System.Collections.IEnumerator GenerateObjectsForChunkAsync(Chunk chunk)
        {
            return GenerateObjectsInternal(chunk);
        }

        protected virtual bool IsObjectAreaAllowed(int x, int y)
        {
            return true;
        }

        protected abstract void SpawnObject(ObjectRule rule, int x, int y, Chunk chunk, ObjectId id);

        protected virtual void OnAfterObjectsGenerated(Chunk chunk)
        {
        }

        protected override void OnChunkUnloaded(Chunk chunk)
        {
            Vector2Int chunkPos = new Vector2Int(chunk.chunkX, chunk.chunkY);
            chunkCaches.Remove(chunkPos);
        }

        protected static int Mask(params int[] regionTypes)
        {
            int mask = 0;
            for (int i = 0; i < regionTypes.Length; i++)
            {
                mask |= 1 << regionTypes[i];
            }
            return mask;
        }

        private System.Collections.IEnumerator GenerateObjectsInternal(Chunk chunk)
        {
            int startX = chunk.chunkX * chunkSize;
            int startY = chunk.chunkY * chunkSize;

            HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
            int processed = 0;
            int budget = Mathf.Max(16, objectGenerationBudget);

            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int ly = 0; ly < chunkSize; ly++)
                {
                    int gx = startX + lx;
                    int gy = startY + ly;

                    if (!IsValidPosition(gx, gy)) continue;
                    if (!IsObjectAreaAllowed(gx, gy)) continue;

                    int regionType = GetRegionTypeCached(gx, gy);
                    Vector2Int pos = new Vector2Int(gx, gy);

                    foreach (var rule in objectRules)
                    {
                        if (occupied.Contains(pos)) break;
                        if (!IsRegionAllowed(rule.regionMask, regionType)) continue;
                        if (!IsPoissonSelected(gx, gy, regionType, rule)) continue;

                        ObjectId id = new ObjectId(gx, gy, rule.kind);
                        SpawnObject(rule, gx, gy, chunk, id);
                        occupied.Add(pos);
                        break;
                    }

                    processed++;
                    if (processed >= budget)
                    {
                        processed = 0;
                        yield return null;
                    }
                }
            }

            OnAfterObjectsGenerated(chunk);
        }

        private ChunkCache GetOrCreateChunkCache(Vector2Int chunkPos)
        {
            if (!chunkCaches.TryGetValue(chunkPos, out ChunkCache cache))
            {
                cache = new ChunkCache(chunkSize * chunkSize);
                chunkCaches.Add(chunkPos, cache);
            }
            return cache;
        }

        private int GetChunkIndex(int worldX, int worldY, Vector2Int chunkPos)
        {
            int localX = worldX - chunkPos.x * chunkSize;
            int localY = worldY - chunkPos.y * chunkSize;
            return localY * chunkSize + localX;
        }

        protected int GetRegionTypeCached(int worldX, int worldY)
        {
            if (!IsValidPosition(worldX, worldY))
            {
                RegionSample sample = SampleRegion(worldX, worldY);
                return ResolveRegionType(sample, worldX, worldY);
            }

            Vector2Int chunkPos = GridToChunk(worldX, worldY);
            ChunkCache cache = GetOrCreateChunkCache(chunkPos);
            int index = GetChunkIndex(worldX, worldY, chunkPos);

            if (!cache.regionValid[index])
            {
                RegionSample sample = SampleRegion(worldX, worldY);
                cache.regionTypes[index] = ResolveRegionType(sample, worldX, worldY);
                cache.regionValid[index] = true;
            }

            return cache.regionTypes[index];
        }

        protected int GetHeightLevelCached(int worldX, int worldY)
        {
            if (!IsValidPosition(worldX, worldY))
            {
                RegionSample sample = SampleRegion(worldX, worldY);
                return ResolveHeight(sample, worldX, worldY);
            }

            Vector2Int chunkPos = GridToChunk(worldX, worldY);
            ChunkCache cache = GetOrCreateChunkCache(chunkPos);
            int index = GetChunkIndex(worldX, worldY, chunkPos);

            if (!cache.heightValid[index])
            {
                RegionSample sample = SampleRegion(worldX, worldY);
                cache.heightLevels[index] = ResolveHeight(sample, worldX, worldY);
                cache.heightValid[index] = true;
            }

            return cache.heightLevels[index];
        }

        private RegionSample SampleRegion(int worldX, int worldY)
        {
            float cellSize = Mathf.Max(1f, regionCellSize);
            int cellX = Mathf.FloorToInt(worldX / cellSize);
            int cellY = Mathf.FloorToInt(worldY / cellSize);

            float bestDist = float.MaxValue;
            float secondDist = float.MaxValue;
            int bestType = 0;
            int secondType = 0;

            int safeRegionCount = Mathf.Max(1, regionCount);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = cellX + dx;
                    int ny = cellY + dy;

                    Vector2 offset = BiomeDeterministic.HashInCell(seed, nx, ny, RegionCellJitterSalt);
                    float fx = (nx + offset.x) * cellSize;
                    float fy = (ny + offset.y) * cellSize;

                    float dist = (fx - worldX) * (fx - worldX) + (fy - worldY) * (fy - worldY);
                    int regionType = BiomeDeterministic.HashRange(seed, nx, ny, RegionTypeSalt, safeRegionCount);

                    if (dist < bestDist)
                    {
                        secondDist = bestDist;
                        secondType = bestType;
                        bestDist = dist;
                        bestType = regionType;
                    }
                    else if (dist < secondDist)
                    {
                        secondDist = dist;
                        secondType = regionType;
                    }
                }
            }

            float blend = 0f;
            if (regionBlendWidth > 0f)
            {
                float edge = Mathf.Sqrt(secondDist) - Mathf.Sqrt(bestDist);
                blend = Mathf.Clamp01((regionBlendWidth - edge) / regionBlendWidth);
            }

            return new RegionSample(bestType, secondType, blend);
        }

        private int ResolveRegionType(RegionSample sample, int worldX, int worldY)
        {
            if (sample.blend <= 0f || sample.primary == sample.secondary)
            {
                return sample.primary;
            }

            float mix = BiomeDeterministic.Hash01(seed, worldX, worldY, RegionBlendSalt);
            return mix < sample.blend ? sample.secondary : sample.primary;
        }

        private int ResolveHeight(RegionSample sample, int worldX, int worldY)
        {
            int primaryHeight = GetRegionHeight(sample.primary);
            int secondaryHeight = GetRegionHeight(sample.secondary);

            float baseHeight = primaryHeight;
            if (sample.blend > 0f && primaryHeight != secondaryHeight)
            {
                baseHeight = Mathf.Lerp(primaryHeight, secondaryHeight, sample.blend);
            }

            float noise = 0f;
            if (heightNoise != null)
            {
                noise = heightNoise.GetNoise(worldX, worldY) * heightNoiseAmplitude;
            }

            float heightValue = baseHeight + noise;
            int level = Mathf.RoundToInt(heightValue);
            return Mathf.Clamp(level, minHeightLevel, maxHeightLevel);
        }

        protected abstract int GetRegionHeight(int regionType);

        protected bool IsRegionAllowed(int mask, int regionType)
        {
            int region = 1 << regionType;
            return (mask & region) != 0;
        }

        protected virtual float GetDensityForRule(ObjectRule rule, int worldX, int worldY, int regionType)
        {
            return IsRegionAllowed(rule.regionMask, regionType) ? rule.density : 0f;
        }

        private bool IsPoissonSelected(int worldX, int worldY, int regionType, ObjectRule rule)
        {
            float density = GetDensityForRule(rule, worldX, worldY, regionType);
            if (density <= 0f) return false;

            int cellSize = Mathf.Max(1, Mathf.RoundToInt(rule.minDistance));
            int cellX = Mathf.FloorToInt((float)worldX / cellSize);
            int cellY = Mathf.FloorToInt((float)worldY / cellSize);
            Vector2Int candidate = GetCandidateInCell(cellX, cellY, cellSize, rule.salt);
            if (candidate.x != worldX || candidate.y != worldY) return false;

            float selfValue = BiomeDeterministic.Hash01(seed, worldX, worldY, rule.salt);
            if (selfValue >= density) return false;

            float radius = Mathf.Max(0.5f, rule.minDistance);
            float radiusSq = radius * radius;
            int cellRange = Mathf.CeilToInt(radius / cellSize);

            for (int dx = -cellRange; dx <= cellRange; dx++)
            {
                for (int dy = -cellRange; dy <= cellRange; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    Vector2Int otherCandidate = GetCandidateInCell(cellX + dx, cellY + dy, cellSize, rule.salt);
                    if (!IsValidPosition(otherCandidate.x, otherCandidate.y)) continue;

                    float offsetX = otherCandidate.x - worldX;
                    float offsetY = otherCandidate.y - worldY;
                    if (offsetX * offsetX + offsetY * offsetY > radiusSq) continue;

                    int otherRegionType = GetRegionTypeCached(otherCandidate.x, otherCandidate.y);
                    float otherDensity = GetDensityForRule(rule, otherCandidate.x, otherCandidate.y, otherRegionType);
                    if (otherDensity <= 0f) continue;

                    float otherValue = BiomeDeterministic.Hash01(seed, otherCandidate.x, otherCandidate.y, rule.salt);
                    if (otherValue < otherDensity && otherValue < selfValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private Vector2Int GetCandidateInCell(int cellX, int cellY, int cellSize, int salt)
        {
            Vector2 offset = BiomeDeterministic.HashInCell(seed, cellX, cellY, salt);
            int originX = cellX * cellSize;
            int originY = cellY * cellSize;
            int candidateX = originX + Mathf.FloorToInt(offset.x * cellSize);
            int candidateY = originY + Mathf.FloorToInt(offset.y * cellSize);
            return new Vector2Int(candidateX, candidateY);
        }
    }
}
