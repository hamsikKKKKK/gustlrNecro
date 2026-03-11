using UnityEngine;
using UnityEngine.Tilemaps;

namespace Necrocis
{
    /// <summary>
    /// 장(Intestine) 바이옴 맵 생성기 (Tilemap + Voronoi/Perlin + Poisson)
    /// </summary>
    public class IntestineBiomeManager : RegionPoissonBiomeManager
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
        [SerializeField] private float detailNoiseScale = 0.05f;  // Perlin 스케일
        [SerializeField] private float mudVariantThreshold = 0.5f;
        [SerializeField] private float mossMixThreshold = 0.2f;
        [SerializeField] private float fleshVariantThreshold = 0.5f;

        // 노이즈 생성기
        private PerlinNoise detailNoise;

        private const int GroundDecorationOrder = 100;
        private const int SlimePuddleSalt = 2001;
        private const int ItemSalt = 3001;

        private enum RegionType
        {
            Mud = 0,
            Moss = 1,
            Flesh = 2
        }

        protected override void Awake()
        {
            // 바이옴 타입 설정
            biomeType = BiomeType.Intestine;

            base.Awake();

            // 노이즈 초기화
            detailNoise = new PerlinNoise(seed);
            detailNoise.SetFrequency(detailNoiseScale);
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

        protected override void BuildObjectRules()
        {
            objectRules.Clear();
            objectRules.Add(new ObjectRule
            {
                kind = BiomeObjectKind.LargeObstacle,
                density = moldTreeDensity,
                minDistance = moldTreeMinDistance,
                blocksMovement = true,
                regionMask = Mask((int)RegionType.Moss),
                salt = 101,
                configIndex = 0
            });
            objectRules.Add(new ObjectRule
            {
                kind = BiomeObjectKind.LargeObstacle,
                density = rockDensity,
                minDistance = rockMinDistance,
                blocksMovement = true,
                regionMask = Mask((int)RegionType.Mud),
                salt = 102,
                configIndex = 1
            });
            objectRules.Add(new ObjectRule
            {
                kind = BiomeObjectKind.AnimatedDecoration,
                density = parasiteDensity,
                minDistance = parasiteMinDistance,
                blocksMovement = false,
                regionMask = Mask((int)RegionType.Flesh),
                salt = 103,
                configIndex = 2
            });
            objectRules.Add(new ObjectRule
            {
                kind = BiomeObjectKind.FloorDecoration,
                density = slimePuddleDensity,
                minDistance = slimePuddleMinDistance,
                blocksMovement = false,
                regionMask = Mask((int)RegionType.Mud, (int)RegionType.Flesh),
                salt = 104,
                configIndex = 3
            });
            objectRules.Add(new ObjectRule
            {
                kind = BiomeObjectKind.SmallDecoration,
                density = moldPlantDensity,
                minDistance = moldPlantMinDistance,
                blocksMovement = false,
                regionMask = Mask((int)RegionType.Moss),
                salt = 105,
                configIndex = 4
            });
            objectRules.Add(new ObjectRule
            {
                kind = BiomeObjectKind.Item,
                density = itemDensity,
                minDistance = itemMinDistance,
                blocksMovement = false,
                regionMask = Mask((int)RegionType.Mud, (int)RegionType.Moss, (int)RegionType.Flesh),
                salt = 106,
                configIndex = 5
            });
        }

        protected override bool IsObjectAreaAllowed(int x, int y)
        {
            return x >= 5 && x < mapWidth - 5 && y >= 10 && y < mapHeight - 5;
        }

        protected override int GetRegionHeight(int regionType)
        {
            return ((RegionType)regionType) switch
            {
                RegionType.Mud => -1,
                RegionType.Moss => 0,
                RegionType.Flesh => 1,
                _ => 0
            };
        }

        protected override void SpawnChunkRecord(ChunkSpawnRecord record, Chunk chunk)
        {
            int x = record.x;
            int y = record.y;
            ObjectId id = new ObjectId(x, y, record.objectKind);

            if (record.category == ChunkSpawnCategory.Portal)
            {
                SpawnReturnPortal(record, chunk);
                return;
            }

            switch (record.objectKind)
            {
                case BiomeObjectKind.FloorDecoration:
                    PlaceSlimePuddle(record, chunk, id);
                    break;
                case BiomeObjectKind.SmallDecoration:
                    PlaceSmallDecoration(record, moldPlant, "MoldPlant", chunk, id);
                    break;
                case BiomeObjectKind.LargeObstacle:
                    if (record.configIndex == 0)
                    {
                        PlaceLargeObstacle(record, moldTree, "MoldTree", moldTreeColliderSize, moldTreeColliderCenter, chunk, id);
                    }
                    else
                    {
                        PlaceLargeObstacle(record, rock, "Rock", rockColliderSize, rockColliderCenter, chunk, id);
                    }
                    break;
                case BiomeObjectKind.AnimatedDecoration:
                    PlaceParasite(record, chunk, id);
                    break;
                case BiomeObjectKind.Item:
                    PlaceItem(record, chunk, id);
                    break;
            }
        }

        protected override void AddExtraChunkSpawnRecords(Chunk chunk)
        {
            Vector2Int portalGrid = WorldToGrid(GetReturnPortalPosition());
            Vector2Int portalChunk = GridToChunk(portalGrid.x, portalGrid.y);

            if (portalChunk.x != chunk.chunkX || portalChunk.y != chunk.chunkY)
            {
                return;
            }

            AddChunkSpawnRecord(chunk, new ChunkSpawnRecord(
                ChunkSpawnCategory.Portal,
                BiomeObjectKind.Portal,
                0,
                portalGrid.x,
                portalGrid.y,
                false));
        }

        private GameObject AcquireObject(ObjectPoolKey poolKey, string name)
        {
            GameObject obj = GetPooledObject(poolKey, () => new GameObject(name));
            obj.name = name;
            obj.transform.SetParent(objectsParent, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
            obj.SetActive(false);
            return obj;
        }

        private static void ActivateSpawnedObject(GameObject obj)
        {
            if (obj != null && !obj.activeSelf)
            {
                obj.SetActive(true);
            }
        }

        private static void RefreshStaticVisuals(GameObject obj, int sortingOrder)
        {
            if (obj == null)
            {
                return;
            }

            Billboard billboard = obj.GetComponent<Billboard>();
            if (billboard != null && billboard.enabled)
            {
                billboard.ResetBaseLocalPosition(obj.transform.localPosition);
                billboard.SetUpdateMode(Billboard.UpdateMode.Continuous);
            }

            SpriteYSort sorter = obj.GetComponent<SpriteYSort>();
            if (sorter != null && sorter.enabled)
            {
                sorter.Configure(SpriteYSort.WorldDynamicBaseSortingOrder, true, SpriteYSort.WorldDynamicMinSortingOrder);
                sorter.SetUpdateMode(SpriteYSort.UpdateMode.Once);
            }
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

        private void PlaceSlimePuddle(ChunkSpawnRecord record, Chunk chunk, ObjectId id)
        {
            bool large = BiomeDeterministic.Hash01(seed, record.x, record.y, SlimePuddleSalt) > 0.5f;
            Sprite sprite = large ? slimePuddleLarge : slimePuddleSmall;
            if (sprite == null) return;

            PlaceFloorDecoration(record, sprite, "SlimePuddle", chunk, id);
        }

        private void PlaceFloorDecoration(ChunkSpawnRecord record, Sprite sprite, string name, Chunk chunk, ObjectId id)
        {
            Vector3 worldPos = GridToWorldWithHeight(record.x, record.y, 0.01f);
            ObjectPoolKey poolKey = GetPoolKey(record);

            GameObject obj = AcquireObject(poolKey, $"{name}_{record.x}_{record.y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = sprite;
            sr.sortingOrder = GroundDecorationOrder;
            GetOrAddComponent<Billboard>(obj);

            RegisterObject(chunk, obj, id, poolKey, record.blocksMovement);
            RefreshStaticVisuals(obj, GroundDecorationOrder);
            ActivateSpawnedObject(obj);
        }

        private void PlaceSmallDecoration(ChunkSpawnRecord record, Sprite sprite, string name, Chunk chunk, ObjectId id)
        {
            if (sprite == null) return;

            Vector3 worldPos = GridToWorldWithHeight(record.x, record.y);
            ObjectPoolKey poolKey = GetPoolKey(record);

            GameObject obj = AcquireObject(poolKey, $"{name}_{record.x}_{record.y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = sprite;
            sr.sortingOrder = GroundDecorationOrder;
            GetOrAddComponent<Billboard>(obj);

            RegisterObject(chunk, obj, id, poolKey, record.blocksMovement);
            RefreshStaticVisuals(obj, GroundDecorationOrder);
            ActivateSpawnedObject(obj);
        }

        private void PlaceLargeObstacle(ChunkSpawnRecord record, Sprite sprite, string name, Vector3 colliderSize, Vector3 colliderCenter, Chunk chunk, ObjectId id)
        {
            if (sprite == null) return;

            Vector3 worldPos = GridToWorldWithHeight(record.x, record.y);
            ObjectPoolKey poolKey = GetPoolKey(record);

            GameObject obj = AcquireObject(poolKey, $"{name}_{record.x}_{record.y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = sprite;
            GetOrAddComponent<Billboard>(obj);
            GetOrAddComponent<SpriteYSort>(obj);

            BoxCollider col = GetOrAddComponent<BoxCollider>(obj);
            col.size = colliderSize;
            col.center = colliderCenter;

            RegisterObject(chunk, obj, id, poolKey, record.blocksMovement);
            RefreshStaticVisuals(obj, GroundDecorationOrder);
            ActivateSpawnedObject(obj);
        }

        private void PlaceParasite(ChunkSpawnRecord record, Chunk chunk, ObjectId id)
        {
            if (parasiteFrames == null || parasiteFrames.Length == 0) return;

            Vector3 worldPos = GridToWorldWithHeight(record.x, record.y);
            ObjectPoolKey poolKey = GetPoolKey(record);

            GameObject obj = AcquireObject(poolKey, $"Parasite_{record.x}_{record.y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = parasiteFrames[0];
            sr.sortingOrder = GroundDecorationOrder;
            GetOrAddComponent<Billboard>(obj);

            AnimatedSprite anim = GetOrAddComponent<AnimatedSprite>(obj);
            anim.SetFrames(parasiteFrames, parasiteAnimSpeed);

            RegisterObject(chunk, obj, id, poolKey, record.blocksMovement);
            RefreshStaticVisuals(obj, GroundDecorationOrder);
            ActivateSpawnedObject(obj);
        }

        private void PlaceItem(ChunkSpawnRecord record, Chunk chunk, ObjectId id)
        {
            Sprite itemSprite = GetDeterministicSprite(itemSprites, record.x, record.y, ItemSalt);
            if (itemSprite == null) return;

            Vector3 worldPos = GridToWorldWithHeight(record.x, record.y);
            ObjectPoolKey poolKey = GetPoolKey(record);

            GameObject obj = AcquireObject(poolKey, $"Item_{record.x}_{record.y}");
            obj.transform.position = worldPos;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sprite = itemSprite;
            sr.sortingOrder = GroundDecorationOrder;
            GetOrAddComponent<Billboard>(obj);

            BoxCollider col = GetOrAddComponent<BoxCollider>(obj);
            col.isTrigger = true;
            col.size = new Vector3(1f, 1f, 1f);

            RegisterObject(chunk, obj, id, poolKey, record.blocksMovement);
            RefreshStaticVisuals(obj, GroundDecorationOrder);
            ActivateSpawnedObject(obj);
        }

        private void SpawnReturnPortal(ChunkSpawnRecord record, Chunk chunk)
        {
            Vector3 portalPos = GetReturnPortalPosition();
            ObjectId id = new ObjectId(record.x, record.y, record.objectKind);
            ObjectPoolKey poolKey = GetPoolKey(record);
            GameObject portalObj = AcquireObject(poolKey, "ReturnPortal");
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

            RegisterObject(chunk, portalObj, id, poolKey, false);
            RefreshStaticVisuals(portalObj, 1000);
            ActivateSpawnedObject(portalObj);
        }

        private Sprite GetDeterministicSprite(Sprite[] sprites, int x, int y, int salt)
        {
            if (sprites == null || sprites.Length == 0) return null;
            int index = BiomeDeterministic.HashRange(seed, x, y, salt, sprites.Length);
            return sprites[index];
        }

        public override Vector3 GetPlayerSpawnPosition()
        {
            return GridToWorld(mapWidth / 2, 7);
        }

        public override Vector3 GetReturnPortalPosition()
        {
            return GridToWorldWithHeight(mapWidth / 2, 4);
        }

        private static ObjectPoolKey GetPoolKey(ChunkSpawnRecord record)
        {
            int archetypeId = record.category == ChunkSpawnCategory.Portal ? 0 : record.configIndex + 1;
            return new ObjectPoolKey(record.objectKind, archetypeId);
        }
    }
}
