using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace Necrocis
{
    /// <summary>
    /// 장(Intestine) 바이옴 맵 생성기 (Tilemap + Voronoi/Perlin + Poisson)
    /// </summary>
    public class IntestineBiomeManager : BiomeManager
    {
        [Header("=== 장 바이옴 타일 ===")]
        [SerializeField] private TileBase mudTile1;   // 기본 진흙
        [SerializeField] private TileBase mudTile2;   // 진흙 변형
        [SerializeField] private TileBase mossTile;   // 이끼

        [Header("바닥 장식 (통과 가능)")]
        [SerializeField] private Sprite slimePuddleLarge;         // 점액웅덩이 (큰)
        [SerializeField] private Sprite slimePuddleSmall;         // 작은점액웅덩이

        [Header("작은 장식물 (통과 가능)")]
        [SerializeField] private Sprite moldPlant;                // 곰팡식물

        [Header("큰 장식물 (통과 불가)")]
        [SerializeField] private Sprite rock;                     // 바위
        [SerializeField] private Sprite moldTree;                 // 곰팡나무
        [SerializeField, HideInInspector] private Vector3 rockColliderSize = new Vector3(1.5f, 3f, 1.5f);
        [SerializeField, HideInInspector] private Vector3 rockColliderCenter = new Vector3(0, 1.5f, 0);
        [SerializeField, HideInInspector] private Vector3 moldTreeColliderSize = new Vector3(1.5f, 3f, 1.5f);
        [SerializeField, HideInInspector] private Vector3 moldTreeColliderCenter = new Vector3(0, 1.5f, 0);

        [Header("애니메이션 장식물")]
        [SerializeField] private Sprite[] parasiteFrames;         // 기생충 (6프레임)
        [SerializeField] private float parasiteAnimSpeed = 0.15f; // 애니메이션 속도

        [Header("아이템")]
        [SerializeField] private Sprite[] itemSprites;            // 아이템

        [Header("귀환 포털")]
        [SerializeField] private Sprite returnPortalSprite;

        [Header("=== 생성 밀도 ===")]
        [SerializeField] private float slimePuddleDensity = 0.03f;
        [SerializeField] private float moldPlantDensity = 0.05f;
        [SerializeField] private float rockDensity = 0.02f;
        [SerializeField] private float moldTreeDensity = 0.015f;
        [SerializeField] private float parasiteDensity = 0.01f;
        [SerializeField] private float itemDensity = 0.005f;

        [Header("=== 오브젝트 최소 거리 (타일 기준) ===")]
        [SerializeField] private float slimePuddleMinDistance = 2f;
        [SerializeField] private float moldPlantMinDistance = 2f;
        [SerializeField] private float rockMinDistance = 3f;
        [SerializeField] private float moldTreeMinDistance = 4f;
        [SerializeField] private float parasiteMinDistance = 3f;
        [SerializeField] private float itemMinDistance = 2f;

        [Header("=== Voronoi/Perlin ===")]
        [SerializeField] private float regionCellSize = 20f;      // Voronoi 셀 크기
        [SerializeField] private float regionBlendWidth = 3f;     // 경계 블렌딩 폭 (타일)
        [SerializeField] private float detailNoiseScale = 0.05f;  // Perlin 스케일
        [SerializeField] private float mudVariantThreshold = 0.5f;
        [SerializeField] private float mossMixThreshold = 0.2f;
        [SerializeField] private float fleshVariantThreshold = 0.5f;

        // 노이즈 생성기
        private PerlinNoise detailNoise;
        private readonly List<ObjectRule> objectRules = new List<ObjectRule>();
        private int[,] regionTypeCache;
        private bool[,] regionTypeCacheValid;

        private const int RegionCount = 3;
        private const int GroundDecorationOrder = 100;
        private const int RegionCellJitterSalt = 501;
        private const int RegionTypeSalt = 777;
        private const int RegionBlendSalt = 888;
        private const int SlimePuddleSalt = 2001;
        private const int ItemSalt = 3001;

        private enum RegionType
        {
            Mud = 0,
            Moss = 1,
            Flesh = 2
        }

        private enum ObjectKind
        {
            SlimePuddle = 1,
            MoldPlant = 2,
            Rock = 3,
            MoldTree = 4,
            Parasite = 5,
            Item = 6,
            ReturnPortal = 100
        }

        [System.Flags]
        private enum RegionMask
        {
            Mud = 1 << 0,
            Moss = 1 << 1,
            Flesh = 1 << 2,
            All = Mud | Moss | Flesh
        }

        private struct RegionSample
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

        private struct ObjectRule
        {
            public ObjectKind kind;
            public float density;
            public float minDistance;
            public bool blocksMovement;
            public RegionMask regionMask;
            public int salt;
        }

        protected override void Awake()
        {
            // 바이옴 타입 설정
            biomeType = BiomeType.Intestine;

            // 장 바이옴 크기
            mapWidth = 90;
            mapHeight = 90;

            base.Awake();

            InitializeRegionCache();

            // 노이즈 초기화
            detailNoise = new PerlinNoise(seed);
            detailNoise.SetFrequency(detailNoiseScale);

            BuildObjectRules();
        }

        protected override TileSample SampleBaseTile(int worldX, int worldY)
        {
            int regionType = GetRegionTypeCached(worldX, worldY);

            float detailValue = (detailNoise.GetNoise(worldX, worldY) + 1f) * 0.5f;

            switch ((RegionType)regionType)
            {
                case RegionType.Moss:
                    if (detailValue < mossMixThreshold)
                    {
                        return new TileSample(BiomeTileType.Floor, mudTile1, true);
                    }
                    return new TileSample(BiomeTileType.Decoration, mossTile, true);

                case RegionType.Flesh:
                    if (detailValue < fleshVariantThreshold)
                    {
                        return new TileSample(BiomeTileType.Floor, mudTile1, true);
                    }
                    return new TileSample(BiomeTileType.FloorVariant, mudTile2, true);

                default:
                    if (detailValue < mudVariantThreshold)
                    {
                        return new TileSample(BiomeTileType.Floor, mudTile1, true);
                    }
                    return new TileSample(BiomeTileType.FloorVariant, mudTile2, true);
            }
        }

        protected override TileBase GetTileAsset(BiomeTileType tileType)
        {
            return tileType switch
            {
                BiomeTileType.Decoration => mossTile,
                BiomeTileType.FloorVariant => mudTile2,
                _ => mudTile1
            };
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

                        if (!IsPoissonSelected(gx, gy, rule))
                        {
                            continue;
                        }

                        ObjectId id = new ObjectId(gx, gy, (int)rule.kind);
                        if (IsObjectSuppressed(chunk, id))
                        {
                            continue;
                        }

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

            TryPlaceReturnPortal(chunk);
        }

        private void BuildObjectRules()
        {
            objectRules.Clear();
            objectRules.Add(new ObjectRule
            {
                kind = ObjectKind.MoldTree,
                density = moldTreeDensity,
                minDistance = moldTreeMinDistance,
                blocksMovement = true,
                regionMask = RegionMask.Moss,
                salt = 101
            });
            objectRules.Add(new ObjectRule
            {
                kind = ObjectKind.Rock,
                density = rockDensity,
                minDistance = rockMinDistance,
                blocksMovement = true,
                regionMask = RegionMask.Mud,
                salt = 102
            });
            objectRules.Add(new ObjectRule
            {
                kind = ObjectKind.Parasite,
                density = parasiteDensity,
                minDistance = parasiteMinDistance,
                blocksMovement = false,
                regionMask = RegionMask.Flesh,
                salt = 103
            });
            objectRules.Add(new ObjectRule
            {
                kind = ObjectKind.SlimePuddle,
                density = slimePuddleDensity,
                minDistance = slimePuddleMinDistance,
                blocksMovement = false,
                regionMask = RegionMask.Mud | RegionMask.Flesh,
                salt = 104
            });
            objectRules.Add(new ObjectRule
            {
                kind = ObjectKind.MoldPlant,
                density = moldPlantDensity,
                minDistance = moldPlantMinDistance,
                blocksMovement = false,
                regionMask = RegionMask.Moss,
                salt = 105
            });
            objectRules.Add(new ObjectRule
            {
                kind = ObjectKind.Item,
                density = itemDensity,
                minDistance = itemMinDistance,
                blocksMovement = false,
                regionMask = RegionMask.All,
                salt = 106
            });
        }

        private bool IsObjectAreaAllowed(int x, int y)
        {
            return x >= 5 && x < mapWidth - 5 && y >= 10 && y < mapHeight - 5;
        }

        private void SpawnObject(ObjectRule rule, int x, int y, Chunk chunk, ObjectId id)
        {
            switch (rule.kind)
            {
                case ObjectKind.SlimePuddle:
                    PlaceSlimePuddle(x, y, chunk, id, rule.blocksMovement);
                    break;
                case ObjectKind.MoldPlant:
                    PlaceSmallDecoration(x, y, moldPlant, "MoldPlant", chunk, id, rule.blocksMovement);
                    break;
                case ObjectKind.Rock:
                    PlaceLargeObstacle(x, y, rock, "Rock", chunk, id, rule.blocksMovement);
                    break;
                case ObjectKind.MoldTree:
                    PlaceLargeObstacle(x, y, moldTree, "MoldTree", chunk, id, rule.blocksMovement);
                    break;
                case ObjectKind.Parasite:
                    PlaceParasite(x, y, chunk, id, rule.blocksMovement);
                    break;
                case ObjectKind.Item:
                    PlaceItem(x, y, chunk, id, rule.blocksMovement);
                    break;
            }
        }

        private GameObject AcquireObject(ObjectKind kind, string name)
        {
            GameObject obj = GetPooledObject((int)kind, () => new GameObject(name));
            obj.name = name;
            obj.transform.SetParent(objectsParent);
            obj.SetActive(true);
            return obj;
        }

        private static T GetOrAddComponent<T>(GameObject obj) where T : Component
        {
            T component = obj.GetComponent<T>();
            if (component == null)
            {
                component = obj.AddComponent<T>();
            }
            return component;
        }

        private void PlaceSlimePuddle(int x, int y, Chunk chunk, ObjectId id, bool blocksMovement)
        {
            bool large = BiomeDeterministic.Hash01(seed, x, y, SlimePuddleSalt) > 0.5f;
            Sprite sprite = large ? slimePuddleLarge : slimePuddleSmall;
            if (sprite == null) return;

            PlaceFloorDecoration(x, y, sprite, "SlimePuddle", chunk, id, blocksMovement);
        }

        private void PlaceFloorDecoration(int x, int y, Sprite sprite, string name, Chunk chunk, ObjectId id, bool blocksMovement)
        {
            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = AcquireObject((ObjectKind)id.type, $"{name}_{x}_{y}");
            obj.transform.position = worldPos + new Vector3(0, 0.01f, 0);

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = sprite;
            sr.sortingOrder = GroundDecorationOrder;

            GetOrAddComponent<Billboard>(obj);

            RegisterObject(chunk, obj, id, blocksMovement);
        }

        private void PlaceSmallDecoration(int x, int y, Sprite sprite, string name, Chunk chunk, ObjectId id, bool blocksMovement)
        {
            if (sprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = AcquireObject((ObjectKind)id.type, $"{name}_{x}_{y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = sprite;
            sr.sortingOrder = GroundDecorationOrder;

            GetOrAddComponent<Billboard>(obj);

            RegisterObject(chunk, obj, id, blocksMovement);
        }

        private void PlaceLargeObstacle(int x, int y, Sprite sprite, string name, Chunk chunk, ObjectId id, bool blocksMovement)
        {
            if (sprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = AcquireObject((ObjectKind)id.type, $"{name}_{x}_{y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = sprite;

            GetOrAddComponent<Billboard>(obj);
            GetOrAddComponent<SpriteYSort>(obj);

            BoxCollider col = GetOrAddComponent<BoxCollider>(obj);
            if (id.type == (int)ObjectKind.Rock)
            {
                col.size = rockColliderSize;
                col.center = rockColliderCenter;
            }
            else
            {
                col.size = moldTreeColliderSize;
                col.center = moldTreeColliderCenter;
            }

            RegisterObject(chunk, obj, id, blocksMovement);
        }

        private void PlaceParasite(int x, int y, Chunk chunk, ObjectId id, bool blocksMovement)
        {
            if (parasiteFrames == null || parasiteFrames.Length == 0) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = AcquireObject((ObjectKind)id.type, $"Parasite_{x}_{y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = parasiteFrames[0];
            sr.sortingOrder = GroundDecorationOrder;

            GetOrAddComponent<Billboard>(obj);

            AnimatedSprite anim = GetOrAddComponent<AnimatedSprite>(obj);
            anim.SetFrames(parasiteFrames, parasiteAnimSpeed);

            RegisterObject(chunk, obj, id, blocksMovement);
        }

        private void PlaceItem(int x, int y, Chunk chunk, ObjectId id, bool blocksMovement)
        {
            Sprite itemSprite = GetDeterministicSprite(itemSprites, x, y, ItemSalt);
            if (itemSprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = AcquireObject((ObjectKind)id.type, $"Item_{x}_{y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = itemSprite;
            sr.sortingOrder = GroundDecorationOrder;

            GetOrAddComponent<Billboard>(obj);

            BoxCollider col = GetOrAddComponent<BoxCollider>(obj);
            col.isTrigger = true;
            col.size = new Vector3(1f, 1f, 1f);

            RegisterObject(chunk, obj, id, blocksMovement);
        }

        private void TryPlaceReturnPortal(Chunk chunk)
        {
            Vector3 portalPos = GetReturnPortalPosition();
            Vector2Int portalGrid = WorldToGrid(portalPos);
            Vector2Int portalChunk = GridToChunk(portalGrid.x, portalGrid.y);

            if (portalChunk.x != chunk.chunkX || portalChunk.y != chunk.chunkY)
            {
                return;
            }

            ObjectId id = new ObjectId(portalGrid.x, portalGrid.y, (int)ObjectKind.ReturnPortal);
            GameObject portalObj = AcquireObject((ObjectKind)id.type, "ReturnPortal");
            portalObj.transform.position = portalPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(portalObj);
            if (returnPortalSprite != null)
            {
                sr.sprite = returnPortalSprite;
            }
            sr.sortingOrder = 1000;

            GetOrAddComponent<Billboard>(portalObj);

            BoxCollider col = GetOrAddComponent<BoxCollider>(portalObj);
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 1f);

            GetOrAddComponent<ReturnPortal>(portalObj);

            RegisterObject(chunk, portalObj, id, false);
        }

        private Sprite GetDeterministicSprite(Sprite[] sprites, int x, int y, int salt)
        {
            if (sprites == null || sprites.Length == 0) return null;
            int index = BiomeDeterministic.HashRange(seed, x, y, salt, sprites.Length);
            return sprites[index];
        }

        private void InitializeRegionCache()
        {
            regionTypeCache = new int[mapWidth, mapHeight];
            regionTypeCacheValid = new bool[mapWidth, mapHeight];
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
                    int regionType = BiomeDeterministic.HashRange(seed, nx, ny, RegionTypeSalt, RegionCount);

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

        private int GetRegionTypeCached(int worldX, int worldY)
        {
            if (!IsValidPosition(worldX, worldY))
            {
                RegionSample sample = SampleRegion(worldX, worldY);
                return ResolveRegionType(sample, worldX, worldY);
            }

            if (!regionTypeCacheValid[worldX, worldY])
            {
                RegionSample sample = SampleRegion(worldX, worldY);
                regionTypeCache[worldX, worldY] = ResolveRegionType(sample, worldX, worldY);
                regionTypeCacheValid[worldX, worldY] = true;
            }

            return regionTypeCache[worldX, worldY];
        }

        private bool IsRegionAllowed(RegionMask mask, int regionType)
        {
            RegionMask region = (RegionMask)(1 << regionType);
            return (mask & region) != 0;
        }

        private bool IsPoissonSelected(int worldX, int worldY, ObjectRule rule)
        {
            float density = GetDensityForRule(rule, worldX, worldY);
            if (density <= 0f) return false;

            float selfValue = BiomeDeterministic.Hash01(seed, worldX, worldY, rule.salt);
            if (selfValue >= density) return false;

            float radius = Mathf.Max(0.5f, rule.minDistance);
            float radiusSq = radius * radius;
            int r = Mathf.CeilToInt(radius);

            for (int x = worldX - r; x <= worldX + r; x++)
            {
                for (int y = worldY - r; y <= worldY + r; y++)
                {
                    if (!IsValidPosition(x, y)) continue;
                    if (x == worldX && y == worldY) continue;

                    float dx = x - worldX;
                    float dy = y - worldY;
                    if (dx * dx + dy * dy > radiusSq) continue;

                    float otherDensity = GetDensityForRule(rule, x, y);
                    if (otherDensity <= 0f) continue;

                    float otherValue = BiomeDeterministic.Hash01(seed, x, y, rule.salt);
                    if (otherValue < otherDensity && otherValue < selfValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private float GetDensityForRule(ObjectRule rule, int worldX, int worldY)
        {
            int regionType = GetRegionTypeCached(worldX, worldY);
            if (!IsRegionAllowed(rule.regionMask, regionType)) return 0f;
            return rule.density;
        }

        public override Vector3 GetPlayerSpawnPosition()
        {
            return GridToWorld(mapWidth / 2, 7);
        }

        public override Vector3 GetReturnPortalPosition()
        {
            return GridToWorld(mapWidth / 2, 4);
        }
    }
}
