using UnityEngine;
using System.Collections.Generic;

namespace Necrocis
{
    /// <summary>
    /// 장(Intestine) 바이옴 맵 생성기 (청크 기반 오픈월드)
    /// </summary>
    public class IntestineBiomeManager : BiomeManager
    {
        [Header("=== 장 바이옴 스프라이트 ===")]
        [Header("바닥 타일")]
        [SerializeField] private Sprite mudTile1;                 // 진흙바닥재 (텍스처)
        [SerializeField] private Sprite mudTile2;                 // 진흙타일맵 (구멍 패턴)
        [SerializeField] private Sprite mossTile;                 // 이끼바닥재 (특정 구역)

        [Header("바닥 장식 (통과 가능)")]
        [SerializeField] private Sprite slimePuddleLarge;         // 점액웅덩이 (큰)
        [SerializeField] private Sprite slimePuddleSmall;         // 작은점액웅덩이

        [Header("작은 장식물 (통과 가능)")]
        [SerializeField] private Sprite moldPlant;                // 곰팡식물

        [Header("큰 장식물 (통과 불가)")]
        [SerializeField] private Sprite rock;                     // 바위
        [SerializeField] private Sprite moldTree;                 // 곰팡나무

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

        [Header("노이즈 설정")]
        [SerializeField] private float mudNoiseScale = 0.05f;
        [SerializeField] private float mudNoiseThreshold = 0.4f;
        [SerializeField] private float mossNoiseScale = 0.03f;
        [SerializeField] private float mossNoiseThreshold = 0.7f;

        // 노이즈 생성기
        private FastNoiseLite mudNoise;
        private FastNoiseLite mossNoise;

        // 귀환 포털 생성됨
        private bool returnPortalPlaced = false;

        // 스프라이트 키 상수
        private const string KEY_MUD1 = "mud1";
        private const string KEY_MUD2 = "mud2";
        private const string KEY_MOSS = "moss";
        private const string KEY_PUDDLE_LARGE = "puddle_large";
        private const string KEY_PUDDLE_SMALL = "puddle_small";
        private const string KEY_MOLD_PLANT = "mold_plant";
        private const string KEY_ROCK = "rock";
        private const string KEY_MOLD_TREE = "mold_tree";
        private const string KEY_PARASITE = "parasite";
        private const string KEY_ITEM = "item";

        protected override void Awake()
        {
            // 바이옴 타입 설정
            biomeType = BiomeType.Intestine;

            // 장 바이옴 크기
            mapWidth = 90;
            mapHeight = 90;
            chunkSize = 16;

            base.Awake();

            // 노이즈 초기화
            mudNoise = new FastNoiseLite(seed);
            mudNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            mudNoise.SetFrequency(mudNoiseScale);

            mossNoise = new FastNoiseLite(seed + 1000);
            mossNoise.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
            mossNoise.SetFrequency(mossNoiseScale);
        }

        protected override void Start()
        {
            base.Start();

            // 귀환 포털 배치 (스폰 청크에)
            PlaceReturnPortal();
        }

        /// <summary>
        /// 청크 생성
        /// </summary>
        protected override void GenerateChunk(int chunkX, int chunkY)
        {
            Chunk chunk = chunks[chunkX, chunkY];
            if (chunk.isGenerated) return;

            int startX = chunkX * chunkSize;
            int startY = chunkY * chunkSize;

            // 청크별 랜덤 시드 (일관성 유지)
            Random.InitState(seed + chunkX * 1000 + chunkY);

            // 1. 바닥 타일 생성
            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int ly = 0; ly < chunkSize; ly++)
                {
                    int gx = startX + lx;
                    int gy = startY + ly;

                    if (!IsValidPosition(gx, gy)) continue;

                    GenerateTileAt(gx, gy, chunk);
                }
            }

            // 2. 오브젝트 생성
            for (int lx = 0; lx < chunkSize; lx++)
            {
                for (int ly = 0; ly < chunkSize; ly++)
                {
                    int gx = startX + lx;
                    int gy = startY + ly;

                    if (!IsValidPosition(gx, gy)) continue;

                    // 가장자리 & 스폰 영역 제외
                    if (gx < 5 || gx >= mapWidth - 5 || gy < 10 || gy >= mapHeight - 5)
                        continue;

                    GenerateObjectAt(gx, gy, lx, ly, chunk);
                }
            }

            chunk.isGenerated = true;
            Debug.Log($"[IntestineBiome] 청크 ({chunkX}, {chunkY}) 생성 완료");
        }

        /// <summary>
        /// 타일 생성
        /// </summary>
        private void GenerateTileAt(int gx, int gy, Chunk chunk)
        {
            float mossValue = (mossNoise.GetNoise(gx, gy) + 1f) / 2f;
            float mudValue = (mudNoise.GetNoise(gx, gy) + 1f) / 2f;

            Sprite floorSprite;
            BiomeTileType tileType;

            if (mossValue >= mossNoiseThreshold)
            {
                floorSprite = mossTile;
                tileType = BiomeTileType.Decoration;
            }
            else if (mudValue >= mudNoiseThreshold)
            {
                floorSprite = mudTile2;
                tileType = BiomeTileType.FloorVariant;
            }
            else
            {
                floorSprite = mudTile1;
                tileType = BiomeTileType.Floor;
            }

            BiomeTile tile = new BiomeTile(tileType, true, floorSprite);
            tileMap[gx, gy] = tile;

            PlaceTile(gx, gy, tile, chunk);
        }

        /// <summary>
        /// 오브젝트 생성
        /// </summary>
        private void GenerateObjectAt(int gx, int gy, int lx, int ly, Chunk chunk)
        {
            float roll = Random.value;
            float cumulative = 0f;

            ObjectSaveData objData = null;

            // 점액웅덩이
            cumulative += slimePuddleDensity;
            if (roll < cumulative)
            {
                bool large = Random.value > 0.5f;
                Sprite sprite = large ? slimePuddleLarge : slimePuddleSmall;
                string key = large ? KEY_PUDDLE_LARGE : KEY_PUDDLE_SMALL;

                if (sprite != null)
                {
                    PlaceFloorDecoration(gx, gy, sprite, "SlimePuddle", chunk);
                    objData = new ObjectSaveData
                    {
                        localX = lx, localY = ly,
                        objectType = (int)BiomeObjectType.DecorationSmall,
                        spriteKey = key
                    };
                }
            }

            // 아이템
            cumulative += itemDensity;
            if (roll < cumulative && objData == null)
            {
                PlaceItem(gx, gy, chunk);
                objData = new ObjectSaveData
                {
                    localX = lx, localY = ly,
                    objectType = (int)BiomeObjectType.Item,
                    spriteKey = KEY_ITEM
                };
            }

            // 기생충
            cumulative += parasiteDensity;
            if (roll < cumulative && objData == null)
            {
                PlaceParasite(gx, gy, chunk);
                objData = new ObjectSaveData
                {
                    localX = lx, localY = ly,
                    objectType = (int)BiomeObjectType.InteractableDecoration,
                    spriteKey = KEY_PARASITE
                };
            }

            // 곰팡나무
            cumulative += moldTreeDensity;
            if (roll < cumulative && objData == null)
            {
                PlaceLargeObstacle(gx, gy, moldTree, "MoldTree", chunk);
                objData = new ObjectSaveData
                {
                    localX = lx, localY = ly,
                    objectType = (int)BiomeObjectType.DecorationLarge,
                    spriteKey = KEY_MOLD_TREE
                };
            }

            // 바위
            cumulative += rockDensity;
            if (roll < cumulative && objData == null)
            {
                PlaceLargeObstacle(gx, gy, rock, "Rock", chunk);
                objData = new ObjectSaveData
                {
                    localX = lx, localY = ly,
                    objectType = (int)BiomeObjectType.DecorationLarge,
                    spriteKey = KEY_ROCK
                };
            }

            // 곰팡식물
            cumulative += moldPlantDensity;
            if (roll < cumulative && objData == null)
            {
                PlaceSmallDecoration(gx, gy, moldPlant, "MoldPlant", chunk);
                objData = new ObjectSaveData
                {
                    localX = lx, localY = ly,
                    objectType = (int)BiomeObjectType.DecorationSmall,
                    spriteKey = KEY_MOLD_PLANT
                };
            }

            if (objData != null)
            {
                chunk.objectDataList.Add(objData);
            }
        }

        /// <summary>
        /// 타일 배치
        /// </summary>
        protected override void PlaceTile(int x, int y, BiomeTile tile, Chunk chunk)
        {
            if (tile.sprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);
            // 타일을 Y=-0.1에 배치 (스프라이트보다 아래)
            worldPos.y = -0.1f;

            GameObject tileObj = new GameObject($"Tile_{x}_{y}");
            tileObj.transform.SetParent(tilesParent);
            tileObj.transform.position = worldPos;

            MeshFilter mf = tileObj.AddComponent<MeshFilter>();
            MeshRenderer mr = tileObj.AddComponent<MeshRenderer>();

            mf.mesh = CreateQuadMesh();

            // 타일용 셰이더 (항상 먼저 렌더링)
            Material mat = new Material(Shader.Find("Unlit/Transparent"));
            mat.mainTexture = tile.sprite.texture;
            mat.renderQueue = 2000;  // Background queue
            mr.material = mat;

            tileObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            tileObj.transform.localScale = new Vector3(tileSize, tileSize, 1f);

            tile.tileObject = tileObj;
            chunk.tileObjects.Add(tileObj);
        }

        /// <summary>
        /// 바닥 장식 배치
        /// </summary>
        private void PlaceFloorDecoration(int x, int y, Sprite sprite, string name, Chunk chunk)
        {
            if (sprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = new GameObject($"{name}_{x}_{y}");
            obj.transform.SetParent(objectsParent);
            obj.transform.position = worldPos + new Vector3(0, 0.01f, 0);

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            obj.AddComponent<Billboard>();
            obj.AddComponent<SpriteYSort>();

            chunk.gameObjects.Add(obj);
        }

        /// <summary>
        /// 작은 장식물 배치
        /// </summary>
        private void PlaceSmallDecoration(int x, int y, Sprite sprite, string name, Chunk chunk)
        {
            if (sprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = new GameObject($"{name}_{x}_{y}");
            obj.transform.SetParent(objectsParent);
            obj.transform.position = worldPos;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            obj.AddComponent<Billboard>();
            obj.AddComponent<SpriteYSort>();

            chunk.gameObjects.Add(obj);
        }

        /// <summary>
        /// 큰 장애물 배치
        /// </summary>
        private void PlaceLargeObstacle(int x, int y, Sprite sprite, string name, Chunk chunk)
        {
            if (sprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = new GameObject($"{name}_{x}_{y}");
            obj.transform.SetParent(objectsParent);
            obj.transform.position = worldPos;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;

            obj.AddComponent<Billboard>();
            obj.AddComponent<SpriteYSort>();

            BoxCollider col = obj.AddComponent<BoxCollider>();
            col.size = new Vector3(1.5f, 3f, 1.5f);
            col.center = new Vector3(0, 1.5f, 0);

            if (tileMap[x, y] != null)
                tileMap[x, y].isWalkable = false;

            chunk.gameObjects.Add(obj);
        }

        /// <summary>
        /// 기생충 배치
        /// </summary>
        private void PlaceParasite(int x, int y, Chunk chunk)
        {
            if (parasiteFrames == null || parasiteFrames.Length == 0) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = new GameObject($"Parasite_{x}_{y}");
            obj.transform.SetParent(objectsParent);
            obj.transform.position = worldPos;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = parasiteFrames[0];

            obj.AddComponent<Billboard>();
            obj.AddComponent<SpriteYSort>();

            AnimatedSprite anim = obj.AddComponent<AnimatedSprite>();
            anim.SetFrames(parasiteFrames, parasiteAnimSpeed);

            chunk.gameObjects.Add(obj);
        }

        /// <summary>
        /// 아이템 배치
        /// </summary>
        private void PlaceItem(int x, int y, Chunk chunk)
        {
            Sprite itemSprite = GetRandomSprite(itemSprites);
            if (itemSprite == null) return;

            Vector3 worldPos = GridToWorld(x, y);

            GameObject obj = new GameObject($"Item_{x}_{y}");
            obj.transform.SetParent(objectsParent);
            obj.transform.position = worldPos;

            SpriteRenderer sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = itemSprite;

            obj.AddComponent<Billboard>();
            obj.AddComponent<SpriteYSort>();

            BoxCollider col = obj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1f, 1f, 1f);

            chunk.gameObjects.Add(obj);
        }

        /// <summary>
        /// 귀환 포털 배치
        /// </summary>
        private void PlaceReturnPortal()
        {
            if (returnPortalPlaced) return;

            Vector3 portalPos = GetReturnPortalPosition();

            GameObject portalObj = new GameObject("ReturnPortal");
            portalObj.transform.SetParent(objectsParent);
            portalObj.transform.position = portalPos;

            SpriteRenderer sr = portalObj.AddComponent<SpriteRenderer>();
            if (returnPortalSprite != null)
            {
                sr.sprite = returnPortalSprite;
            }
            sr.sortingOrder = 1000;

            portalObj.AddComponent<Billboard>();

            BoxCollider col = portalObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2f, 2f, 1f);

            portalObj.AddComponent<ReturnPortal>();

            returnPortalPlaced = true;
            Debug.Log($"[IntestineBiome] 귀환 포털 배치: {portalPos}");
        }

        /// <summary>
        /// 오브젝트 복원
        /// </summary>
        protected override void RestoreObject(int x, int y, ObjectSaveData objData, Chunk chunk)
        {
            switch (objData.spriteKey)
            {
                case KEY_PUDDLE_LARGE:
                    PlaceFloorDecoration(x, y, slimePuddleLarge, "SlimePuddle", chunk);
                    break;
                case KEY_PUDDLE_SMALL:
                    PlaceFloorDecoration(x, y, slimePuddleSmall, "SlimePuddle", chunk);
                    break;
                case KEY_MOLD_PLANT:
                    PlaceSmallDecoration(x, y, moldPlant, "MoldPlant", chunk);
                    break;
                case KEY_ROCK:
                    PlaceLargeObstacle(x, y, rock, "Rock", chunk);
                    break;
                case KEY_MOLD_TREE:
                    PlaceLargeObstacle(x, y, moldTree, "MoldTree", chunk);
                    break;
                case KEY_PARASITE:
                    PlaceParasite(x, y, chunk);
                    break;
                case KEY_ITEM:
                    PlaceItem(x, y, chunk);
                    break;
            }
        }

        /// <summary>
        /// 타일 스프라이트 키
        /// </summary>
        protected override string GetTileSpriteKey(BiomeTile tile)
        {
            if (tile.sprite == mudTile1) return KEY_MUD1;
            if (tile.sprite == mudTile2) return KEY_MUD2;
            if (tile.sprite == mossTile) return KEY_MOSS;
            return KEY_MUD1;
        }

        /// <summary>
        /// 스프라이트 키로 스프라이트 가져오기
        /// </summary>
        protected override Sprite GetSpriteByKey(string key)
        {
            return key switch
            {
                KEY_MUD1 => mudTile1,
                KEY_MUD2 => mudTile2,
                KEY_MOSS => mossTile,
                KEY_PUDDLE_LARGE => slimePuddleLarge,
                KEY_PUDDLE_SMALL => slimePuddleSmall,
                KEY_MOLD_PLANT => moldPlant,
                KEY_ROCK => rock,
                KEY_MOLD_TREE => moldTree,
                _ => mudTile1
            };
        }

        /// <summary>
        /// 랜덤 스프라이트
        /// </summary>
        private Sprite GetRandomSprite(Sprite[] sprites)
        {
            if (sprites == null || sprites.Length == 0) return null;
            return sprites[Random.Range(0, sprites.Length)];
        }

        /// <summary>
        /// Quad 메시 생성
        /// </summary>
        private Mesh CreateQuadMesh()
        {
            Mesh mesh = new Mesh();

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, -0.5f, 0),
                new Vector3(0.5f, -0.5f, 0),
                new Vector3(-0.5f, 0.5f, 0),
                new Vector3(0.5f, 0.5f, 0)
            };

            int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

            Vector2[] uv = new Vector2[4]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1)
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();

            return mesh;
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
