using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace Necrocis
{
    /// <summary>
    /// BiomeConfig로 동작하는 범용 바이옴 매니저
    /// </summary>
    public class ConfigurableBiomeManager : RegionPoissonBiomeManager
    {
        [Header("Biome Config")]
        [SerializeField] private BiomeConfig config;

        private PerlinNoise detailNoise;
        private readonly List<BiomeObjectRuleConfig> runtimeRules = new List<BiomeObjectRuleConfig>();

        protected override void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[ConfigurableBiomeManager] BiomeConfig가 없습니다.");
                enabled = false;
                return;
            }

            if (config.regions == null || config.regions.Count == 0)
            {
                Debug.LogError("[ConfigurableBiomeManager] Region 설정이 비어 있습니다.");
                enabled = false;
                return;
            }

            biomeType = config.biomeType;
            regionCellSize = config.regionCellSize;
            regionBlendWidth = config.regionBlendWidth;
            regionCount = config.regions.Count;
            heightNoiseScale = config.heightNoiseScale;
            heightNoiseAmplitude = config.heightNoiseAmplitude;

            base.Awake();
        }

        protected override void InitializeNoise()
        {
            base.InitializeNoise();
            detailNoise = new PerlinNoise(seed);
            detailNoise.SetFrequency(config.detailNoiseScale);
        }

        protected override TileSample SampleBaseTile(int worldX, int worldY)
        {
            BiomeRegionDefinition region = GetRegionDefinition(worldX, worldY);
            if (region == null)
            {
                return new TileSample(BiomeTileType.None, null, true);
            }

            float detailValue = 0f;
            if (detailNoise != null)
            {
                detailValue = (detailNoise.GetNoise(worldX, worldY) + 1f) * 0.5f;
            }

            bool useVariant = region.variantTile != null && detailValue >= region.variantThreshold;
            TileBase tile = useVariant ? region.variantTile : region.primaryTile;
            BiomeTileType type = useVariant ? region.variantType : region.primaryType;

            return new TileSample(type, tile, IsTileWalkable(type));
        }

        protected override TileBase GetTileAsset(BiomeTileType tileType)
        {
            return config != null ? config.GetTileForType(tileType) : null;
        }

        protected override int GetRegionHeight(int regionType)
        {
            if (config == null || config.regions == null || config.regions.Count == 0)
            {
                return 0;
            }

            int index = Mathf.Clamp(regionType, 0, config.regions.Count - 1);
            return config.regions[index].baseHeight;
        }

        protected override bool IsObjectAreaAllowed(int x, int y)
        {
            if (config == null) return true;

            int left = Mathf.Max(0, config.marginLeft);
            int right = Mathf.Max(0, config.marginRight);
            int bottom = Mathf.Max(0, config.marginBottom);
            int top = Mathf.Max(0, config.marginTop);

            return x >= left && x < mapWidth - right && y >= bottom && y < mapHeight - top;
        }

        protected override void BuildObjectRules()
        {
            objectRules.Clear();
            runtimeRules.Clear();

            if (config == null || config.objectRules == null)
            {
                return;
            }

            int allMask = 0;
            for (int i = 0; i < config.regions.Count; i++)
            {
                allMask |= 1 << i;
            }

            for (int i = 0; i < config.objectRules.Count; i++)
            {
                BiomeObjectRuleConfig ruleConfig = config.objectRules[i];
                if (ruleConfig == null) continue;

                int mask = BuildRegionMask(ruleConfig.allowedRegions, allMask);
                int salt = ruleConfig.poissonSalt != 0 ? ruleConfig.poissonSalt : 200 + i;

                objectRules.Add(new ObjectRule
                {
                    kind = ruleConfig.poolKind,
                    density = ruleConfig.density,
                    minDistance = ruleConfig.minDistance,
                    blocksMovement = ruleConfig.blocksMovement,
                    regionMask = mask,
                    salt = salt,
                    configIndex = runtimeRules.Count
                });

                runtimeRules.Add(ruleConfig);
            }
        }

        protected override void SpawnObject(ObjectRule rule, int x, int y, Chunk chunk, ObjectId id)
        {
            if (rule.configIndex < 0 || rule.configIndex >= runtimeRules.Count) return;

            BiomeObjectRuleConfig ruleConfig = runtimeRules[rule.configIndex];
            SpawnConfiguredObject(ruleConfig, x, y, chunk, id);
        }

        protected override void OnAfterObjectsGenerated(Chunk chunk)
        {
            TrySpawnReturnPortal(chunk);
        }

        private int BuildRegionMask(List<int> regions, int fallbackMask)
        {
            if (regions == null || regions.Count == 0) return fallbackMask;

            int mask = 0;
            foreach (int regionIndex in regions)
            {
                if (regionIndex < 0 || regionIndex >= config.regions.Count) continue;
                mask |= 1 << regionIndex;
            }

            return mask == 0 ? fallbackMask : mask;
        }

        private BiomeRegionDefinition GetRegionDefinition(int worldX, int worldY)
        {
            if (config == null || config.regions == null || config.regions.Count == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(GetRegionTypeCached(worldX, worldY), 0, config.regions.Count - 1);
            return config.regions[index];
        }

        private void SpawnConfiguredObject(BiomeObjectRuleConfig rule, int x, int y, Chunk chunk, ObjectId id)
        {
            if (rule.sprites == null || rule.sprites.Length == 0) return;

            string baseName = string.IsNullOrEmpty(rule.name) ? rule.poolKind.ToString() : rule.name;
            GameObject obj = AcquireObject(rule.poolKind, $"{baseName}_{x}_{y}");
            obj.transform.position = GridToWorldWithHeight(x, y, rule.heightOffset);

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(obj);
            sr.sortingOrder = rule.sortingOrder;

            if (rule.animate)
            {
                AnimatedSprite anim = GetOrAddComponent<AnimatedSprite>(obj);
                anim.enabled = true;
                anim.SetFrames(rule.sprites, rule.animationSpeed);
                anim.Play();
                sr.sprite = rule.sprites[0];
            }
            else
            {
                AnimatedSprite anim = obj.GetComponent<AnimatedSprite>();
                if (anim != null)
                {
                    anim.Stop();
                    anim.enabled = false;
                }

                Sprite sprite = SelectSprite(rule, x, y);
                sr.sprite = sprite;
            }

            ConfigureBillboard(obj, rule.useBillboard);
            ConfigureYSort(obj, rule.useYSort);
            ConfigureCollider(obj, rule);

            RegisterObject(chunk, obj, id, rule.blocksMovement);
        }

        private void TrySpawnReturnPortal(Chunk chunk)
        {
            if (config == null || config.returnPortal == null || !config.returnPortal.enabled) return;

            Vector3 portalPos;
            Vector2Int portalGrid;

            if (config.returnPortal.useCustomPosition)
            {
                portalGrid = config.returnPortal.gridPosition;
                portalPos = GridToWorldWithHeight(portalGrid.x, portalGrid.y, config.returnPortal.heightOffset);
            }
            else
            {
                portalPos = GetReturnPortalPosition();
                portalPos.y += config.returnPortal.heightOffset;
                portalGrid = WorldToGrid(portalPos);
            }

            Vector2Int portalChunk = GridToChunk(portalGrid.x, portalGrid.y);
            if (portalChunk.x != chunk.chunkX || portalChunk.y != chunk.chunkY)
            {
                return;
            }

            ObjectId id = new ObjectId(portalGrid.x, portalGrid.y, config.returnPortal.poolKind);
            GameObject portalObj = AcquireObject(config.returnPortal.poolKind, config.returnPortal.name);
            portalObj.transform.position = portalPos;
            portalObj.transform.localScale = config.returnPortal.scale;

            SpriteRenderer sr = GetOrAddComponent<SpriteRenderer>(portalObj);
            if (config.returnPortal.sprite != null)
            {
                sr.sprite = config.returnPortal.sprite;
            }
            sr.sortingOrder = config.returnPortal.sortingOrder;

            ConfigureBillboard(portalObj, config.returnPortal.useBillboard);
            ConfigurePortalCollider(portalObj, config.returnPortal);
            GetOrAddComponent<ReturnPortal>(portalObj);

            RegisterObject(chunk, portalObj, id, false);
        }

        private Sprite SelectSprite(BiomeObjectRuleConfig rule, int x, int y)
        {
            if (rule.sprites == null || rule.sprites.Length == 0) return null;
            if (!rule.useDeterministicSprite || rule.sprites.Length == 1)
            {
                return rule.sprites[0];
            }

            int salt = rule.spriteSalt != 0 ? rule.spriteSalt : rule.poissonSalt;
            int index = BiomeDeterministic.HashRange(seed, x, y, salt, rule.sprites.Length);
            return rule.sprites[index];
        }

        private GameObject AcquireObject(BiomeObjectKind kind, string name)
        {
            GameObject obj = GetPooledObject(kind, () => new GameObject(name));
            obj.name = name;
            obj.transform.SetParent(objectsParent, false);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;
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

        private void ConfigureBillboard(GameObject obj, bool enabled)
        {
            Billboard billboard = obj.GetComponent<Billboard>();
            if (enabled)
            {
                if (billboard == null)
                {
                    billboard = obj.AddComponent<Billboard>();
                }
                billboard.enabled = true;
            }
            else if (billboard != null)
            {
                billboard.enabled = false;
            }
        }

        private void ConfigureYSort(GameObject obj, bool enabled)
        {
            SpriteYSort sorter = obj.GetComponent<SpriteYSort>();
            if (enabled)
            {
                if (sorter == null)
                {
                    sorter = obj.AddComponent<SpriteYSort>();
                }
                sorter.enabled = true;
            }
            else if (sorter != null)
            {
                sorter.enabled = false;
            }
        }

        private void ConfigureCollider(GameObject obj, BiomeObjectRuleConfig rule)
        {
            BoxCollider col = obj.GetComponent<BoxCollider>();
            if (!rule.addCollider)
            {
                if (col != null) col.enabled = false;
                return;
            }

            if (col == null)
            {
                col = obj.AddComponent<BoxCollider>();
            }

            col.enabled = true;
            col.isTrigger = rule.isTrigger;
            col.size = rule.colliderSize;
            col.center = rule.colliderCenter;
        }

        private void ConfigurePortalCollider(GameObject obj, PortalConfig portal)
        {
            BoxCollider col = obj.GetComponent<BoxCollider>();
            if (!portal.addCollider)
            {
                if (col != null) col.enabled = false;
                return;
            }

            if (col == null)
            {
                col = obj.AddComponent<BoxCollider>();
            }

            col.enabled = true;
            col.isTrigger = portal.isTrigger;
            col.size = Vector3.one;
            col.center = Vector3.zero;
        }
    }
}
